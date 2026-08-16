// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/PermissionGateTests.cs
// PROJECT: ProGlassAutomation.Tests — PHASE P
//
// Covers two of the eight cases the agent spec requires:
//     • unauthorized-write
//     • missing-confirmation
//
// WHY THESE MATTER MORE THAN THEY LOOK
//   No Write or Destructive tool is registered in AgentToolRegistry, and
//   none ever has been. The permission and confirmation branches that
//   guard them have therefore NEVER EXECUTED in production or in a test.
//   The gates are unproven code.
//
//   These tests exercise them with a FAKE write tool that exists only
//   here. It is deliberately never added to the real registry, so the
//   test suite proves the gates without widening the agent's real
//   capability by a single tool.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Permissions;
using ProGlassAutomation.Services.Audit;
using System.Threading.Tasks;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class PermissionGateTests
    {
        // ── Test doubles ──────────────────────────────────────────────────

        /// <summary>A Write tool that exists ONLY in the test assembly.</summary>
        private static ToolDefinition WriteTool(string permission = AppPermissions.ProformaView) =>
            new()
            {
                Name = "fake_write",
                Description = "Test-only write tool",
                Risk = ToolRisk.Write,
                RequiredPermission = permission
            };

        private static ToolDefinition DestructiveTool() =>
            new()
            {
                Name = "fake_destroy",
                Description = "Test-only destructive tool",
                Risk = ToolRisk.Destructive,
                RequiredPermission = AppPermissions.ProformaView
            };

        private static ToolDefinition ReadTool() =>
            new()
            {
                Name = "fake_read",
                Description = "Test-only read tool",
                Risk = ToolRisk.Read,
                RequiredPermission = AppPermissions.ProformaView
            };

        private static AgentPermissionService Gate(
            string username,
            string role,
            ConfirmationPrompt? prompt = null) =>
            new(prompt, audit: null, identity: () => (username, role));

        // ══════════════════════════════════════════════════════════════
        //  UNAUTHORIZED WRITE
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Write_is_denied_when_role_lacks_permission()
        {
            // Role choice matters here. Viewer maps to ViewAll(), so it DOES
            // hold ProformaInvoice.View and would wrongly pass. Store is the
            // genuinely unprivileged role for this permission — verified
            // against RolePermissions, not assumed.
            var gate = Gate("storekeeper", AppRoles.Store);

            var decision = gate.Check(WriteTool(), "storekeeper");

            Assert.False(decision.Allowed);
            Assert.Contains("permission", decision.Reason, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Sanity_the_role_used_above_really_lacks_the_permission()
        {
            // Guards the test itself: if Store is ever granted ProformaView,
            // the test above would silently stop proving anything.
            Assert.False(RolePermissions.Has(AppRoles.Store, AppPermissions.ProformaView));
            Assert.True(RolePermissions.Has(AppRoles.Administrator, AppPermissions.ProformaView));
        }

        [Fact]
        public void Write_is_denied_when_tool_declares_no_permission()
        {
            // A write tool with an empty RequiredPermission is a
            // mis-configuration. It must fail closed rather than sail
            // through on the "no permission needed" path that Read uses.
            var gate = Gate("admin", AppRoles.Administrator);

            var decision = gate.Check(WriteTool(permission: ""), "admin");

            Assert.False(decision.Allowed);
        }

        [Fact]
        public void Acting_as_another_user_is_refused()
        {
            // Signed in as alice, request claims to be bob.
            var gate = Gate("alice", AppRoles.Administrator);

            var decision = gate.Check(ReadTool(), "bob");

            Assert.False(decision.Allowed);
            Assert.Contains("bob", decision.Reason);
        }

        [Fact]
        public void Empty_identity_is_refused()
        {
            var gate = Gate("alice", AppRoles.Administrator);

            var decision = gate.Check(ReadTool(), "");

            Assert.False(decision.Allowed);
        }

        [Fact]
        public void Navigation_needs_no_permission()
        {
            // Even a low-privilege role may change the view: navigation
            // touches no data.
            var gate = Gate("storekeeper", AppRoles.Store);

            var nav = new ToolDefinition
            {
                Name = "navigate",
                Risk = ToolRisk.Navigation,
                RequiredPermission = ""
            };

            Assert.True(gate.Check(nav, "storekeeper").Allowed);
        }

        // ══════════════════════════════════════════════════════════════
        //  MISSING CONFIRMATION
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public async Task Write_is_denied_when_no_confirmation_prompt_is_wired()
        {
            // Fail-closed: with no way to ask, the answer is no.
            var gate = Gate("admin", AppRoles.Administrator, prompt: null);

            var decision = await gate.ConfirmAsync(WriteTool(), "anything");

            Assert.False(decision.Allowed);
        }

        [Fact]
        public async Task Write_is_denied_when_user_declines()
        {
            var gate = Gate("admin", AppRoles.Administrator,
                prompt: (_, _, _) => Task.FromResult(false));

            var decision = await gate.ConfirmAsync(WriteTool(), "anything");

            Assert.False(decision.Allowed);
        }

        [Fact]
        public async Task Write_proceeds_only_on_explicit_yes()
        {
            var gate = Gate("admin", AppRoles.Administrator,
                prompt: (_, _, _) => Task.FromResult(true));

            var decision = await gate.ConfirmAsync(WriteTool(), "anything");

            Assert.True(decision.Allowed);
        }

        [Fact]
        public async Task A_throwing_prompt_is_not_an_implicit_yes()
        {
            // A broken dialog must never be read as consent.
            var gate = Gate("admin", AppRoles.Administrator,
                prompt: (_, _, _) => throw new System.InvalidOperationException("boom"));

            var decision = await gate.ConfirmAsync(WriteTool(), "anything");

            Assert.False(decision.Allowed);
        }

        [Fact]
        public async Task Destructive_prompt_is_flagged_destructive_and_says_irreversible()
        {
            bool sawDestructiveFlag = false;
            string sawMessage = "";

            var gate = Gate("admin", AppRoles.Administrator,
                prompt: (_, message, destructive) =>
                {
                    sawDestructiveFlag = destructive;
                    sawMessage = message;
                    return Task.FromResult(false);
                });

            await gate.ConfirmAsync(DestructiveTool(), "delete everything");

            Assert.True(sawDestructiveFlag);
            Assert.Contains("IRREVERSIBLE", sawMessage);
        }

        [Fact]
        public async Task Read_tools_do_not_require_confirmation()
        {
            // A read must not nag the user; only Write/Destructive do.
            var gate = Gate("admin", AppRoles.Administrator, prompt: null);

            var decision = await gate.ConfirmAsync(ReadTool(), "search");

            Assert.True(decision.Allowed);
        }
    }
}
