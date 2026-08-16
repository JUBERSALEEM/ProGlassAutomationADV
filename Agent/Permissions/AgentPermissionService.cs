// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Permissions/AgentPermissionService.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// The agent gets NO privileges of its own. It composes the SAME
// Services/Audit RolePermissions the rest of the ERP enforces, so a user
// can never reach through the agent to do something the UI would refuse.
//
// THE IDENTITY CHECK IS THE IMPORTANT ONE: the acting username must equal
// AuditService.CurrentUser. If a request arrives claiming to be somebody
// else — a stale voice session, a replayed request — it is denied. There
// is no "trust the caller" path.
//
// CONFIRMATION POLICY:
//   Write and Destructive ALWAYS require an interactive confirmation.
//   If no prompt is wired, the answer is DENY (fail closed) — a
//   headless/voice context must not be able to slip a write through.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Services.Audit;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Permissions
{
    /// <summary>
    /// Prompts the user to confirm a risky tool. Returns true to proceed.
    /// Injected by the UI layer; null means "no way to ask" → deny.
    /// </summary>
    public delegate Task<bool> ConfirmationPrompt(
        string title, string message, bool destructive);

    /// <summary>Role gate + confirmation gate in front of every tool.</summary>
    public sealed class AgentPermissionService : IAgentPermissionService
    {
        private readonly ConfirmationPrompt? _prompt;
        private readonly IAgentAuditService? _audit;

        /// <summary>
        /// PHASE P — how the gate learns who is signed in.
        ///
        /// This used to read AuditService.CurrentUsername/CurrentUser
        /// directly. Those are static, and the only way to set them is
        /// SwitchUserAsync, which hits audit.db and verifies a password —
        /// so the identity and role branches of this gate could not be
        /// exercised by a test at all.
        ///
        /// Defaulting to AuditService keeps production behaviour byte-for-
        /// byte identical; a test supplies a fake instead. This is a seam,
        /// not a backdoor: nothing mutates AuditService, and the agent
        /// still cannot choose its own identity — AgentHost always
        /// constructs this with the default.
        /// </summary>
        private readonly Func<(string Username, string Role)> _identity;

        private static (string Username, string Role) SignedInUser() =>
            (AuditService.CurrentUsername ?? "",
             AuditService.CurrentUser?.Role ?? "");

        public AgentPermissionService(
            ConfirmationPrompt? prompt = null,
            IAgentAuditService? audit = null,
            Func<(string Username, string Role)>? identity = null)
        {
            _prompt = prompt;
            _audit = audit;
            _identity = identity ?? SignedInUser;
        }

        // ══════════════════════════════════════════════════════════════
        //  ROLE / PERMISSION GATE
        // ══════════════════════════════════════════════════════════════

        public PermissionDecision Check(ToolDefinition tool, string username)
        {
            if (tool == null)
                return PermissionDecision.Deny("No tool was specified.");

            // ── Identity: the caller must BE the signed-in user ──────────
            var (current, role) = _identity();
            current ??= "";
            string acting = (username ?? "").Trim();

            if (acting.Length == 0)
                return PermissionDecision.Deny(
                    "I could not tell who is asking, so I did not act.");

            if (!string.Equals(acting, current, StringComparison.OrdinalIgnoreCase))
            {
                _audit?.Tool(acting, tool.Name, false,
                    $"identity mismatch (signed in as '{current}')");

                return PermissionDecision.Deny(
                    $"You are signed in as '{current}', but the request came from " +
                    $"'{acting}'. I will not act on someone else's behalf.");
            }

            role ??= "";

            switch (tool.Risk)
            {
                // Navigation only changes the view — always allowed.
                case ToolRisk.Navigation:
                    return PermissionDecision.Allow();

                // Read needs the module's View permission.
                case ToolRisk.Read:
                    if (string.IsNullOrWhiteSpace(tool.RequiredPermission))
                        return PermissionDecision.Allow();

                    return RolePermissions.Has(role, tool.RequiredPermission)
                        ? PermissionDecision.Allow()
                        : Denied(tool, role, acting);

                // Write / Destructive need the permission AND a confirmation
                // (enforced separately in ConfirmAsync).
                case ToolRisk.Write:
                case ToolRisk.Destructive:
                    if (string.IsNullOrWhiteSpace(tool.RequiredPermission))
                    {
                        _audit?.Tool(acting, tool.Name, false,
                            "write/destructive tool declares no permission");

                        return PermissionDecision.Deny(
                            "That action is not configured with a permission, so " +
                            "I refused it.");
                    }

                    return RolePermissions.Has(role, tool.RequiredPermission)
                        ? PermissionDecision.Allow()
                        : Denied(tool, role, acting);

                default:
                    return PermissionDecision.Deny("Unknown risk level — refused.");
            }
        }

        private PermissionDecision Denied(
            ToolDefinition tool, string role, string user)
        {
            _audit?.Tool(user, tool.Name, false,
                $"role '{role}' lacks {tool.RequiredPermission}");

            return PermissionDecision.Deny(
                $"Your role ({role}) does not have the " +
                $"'{tool.RequiredPermission}' permission, so I cannot do that.");
        }

        // ══════════════════════════════════════════════════════════════
        //  CONFIRMATION GATE
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Asks the user before anything that changes data. Fail-closed:
        /// with no prompt wired, risky tools are denied.
        /// </summary>
        public async Task<PermissionDecision> ConfirmAsync(
            ToolDefinition tool, string argument)
        {
            if (tool == null)
                return PermissionDecision.Deny("No tool was specified.");

            bool mustConfirm =
                tool.Risk == ToolRisk.Write ||
                tool.Risk == ToolRisk.Destructive ||
                tool.RequiresConfirmation;

            if (!mustConfirm) return PermissionDecision.Allow();

            if (_prompt == null)
            {
                _audit?.Tool(AuditService.CurrentUsername, tool.Name, false,
                    "no confirmation prompt available — denied");

                return PermissionDecision.Deny(
                    "That action needs your confirmation, but I have no way to " +
                    "ask you right now, so I did not do it.");
            }

            bool destructive = tool.Risk == ToolRisk.Destructive;

            string message = destructive
                ? $"This is IRREVERSIBLE and will permanently change data.\n\n" +
                  $"Action: {tool.Description}\n" +
                  $"Details: {argument}\n\n" +
                  "Are you sure you want me to continue?"
                : $"This will change saved data.\n\n" +
                  $"Action: {tool.Description}\n" +
                  $"Details: {argument}\n\n" +
                  "Do you want me to continue?";

            try
            {
                bool approved = await _prompt(
                    destructive ? "Confirm irreversible action" : "Confirm action",
                    message,
                    destructive);

                if (approved) return PermissionDecision.Allow();

                _audit?.Tool(AuditService.CurrentUsername, tool.Name, false,
                    "user declined confirmation");

                return PermissionDecision.Deny("You declined, so I stopped.");
            }
            catch (Exception ex)
            {
                // A broken prompt must not become an implicit yes.
                Debug.WriteLine($"[AgentPermissions] prompt failed: {ex.Message}");

                return PermissionDecision.Deny(
                    "I could not confirm that action with you, so I stopped.");
            }
        }
    }
}
