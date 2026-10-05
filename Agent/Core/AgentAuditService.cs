// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Core/AgentAuditService.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// Every agent decision lands in the SAME audit trail as human actions,
// under the "AI Assistant" section. That is the point: an agent that
// acts on the ERP must be as reviewable as a user who does, and its
// entries appear in the existing Activity feeds with no new UI.
//
// Writes go through Services/Audit/AuditService, which is already
// insert-only and non-throwing.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Audit;
using System;
using System.Diagnostics;

namespace ProGlassAutomation.Agent.Core
{
    /// <summary>Routes agent events into the application audit trail.</summary>
    public sealed class AgentAuditService : IAgentAuditService
    {
        /// <summary>Audit section used by every agent entry.</summary>
        public const string Section = "AI Assistant";

        public void Request(string username, string text, bool fromVoice) =>
            Write("Agent request",
                $"{(fromVoice ? "[voice] " : "")}{Cap(text, 400)}", username);

        public void Intent(string username, string intent) =>
            Write("Agent intent", Cap(intent, 200), username);

        public void Tool(string username, string tool, bool allowed, string detail) =>
            Write(allowed ? "Agent tool run" : "Agent tool denied",
                $"{tool} — {Cap(detail, 300)}", username);

        public void Teaching(string username, string action, string detail) =>
            Write(action, Cap(detail, 300), username);

        public void Security(string username, string action, string detail) =>
            Write(action, Cap(detail, 300), username);

        public void AgentError(string username, string detail) =>
            Write("Agent error", Cap(detail, 400), username);

        private static void Write(string action, string details, string username)
        {
            try
            {
                // The reason column carries the acting user when it differs
                // from the signed-in one, so a mismatch is visible in the feed.
                string current = AuditService.CurrentUsername ?? "";
                string reason =
                    !string.IsNullOrWhiteSpace(username) &&
                    !string.Equals(username, current, StringComparison.OrdinalIgnoreCase)
                        ? $"requested by '{username}'"
                        : "";

                AuditService.Log(Section, action, details, "AGENT", "", reason);
            }
            catch (Exception ex)
            {
                // Auditing must never break the agent.
                AppLogService.Debug($"[AgentAudit] write failed: {ex.Message}");
            }
        }

        private static string Cap(string value, int max)
        {
            value ??= "";
            return value.Length <= max ? value : value[..max];
        }
    }
}
