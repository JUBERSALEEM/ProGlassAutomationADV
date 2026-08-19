// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Tools/Read/GetActivityTool.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// "What happened recently?" — reads the existing audit trail.
//
// Gated on Audit.View, so a role that cannot open the Activity screen
// cannot get the same information by asking the agent instead. That
// equivalence is the whole point of composing the app's permissions
// rather than inventing agent-specific ones.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Audit;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Tools.Read
{
    /// <summary>Reports the most recent audited actions.</summary>
    public sealed class GetActivityTool : IAgentTool
    {
        /// <summary>How many entries the agent reads back.</summary>
        public const int RecentCount = 5;

        public ToolDefinition Definition { get; } = new()
        {
            Name = "get_activity",
            Description = "Report the most recent recorded user actions",
            Risk = ToolRisk.Read,
            RequiredPermission = AppPermissions.AuditView
        };

        public async Task<ToolResult> ExecuteAsync(string argument)
        {
            try
            {
                var entries = await AuditService
                    .GetRecentAsync(RecentCount)
                    .ConfigureAwait(false);

                if (entries == null)
                    return ToolResult.Fail(
                        "I could not read the activity log, so I cannot tell you " +
                        "what happened recently.");

                if (entries.Count == 0)
                    return ToolResult.Ok("No activity has been recorded yet.");

                var sb = new StringBuilder();
                sb.AppendLine($"Last {entries.Count} recorded action(s):");

                foreach (var e in entries)
                {
                    string when = e.TimestampUtc.ToLocalTime()
                        .ToString("dd MMM HH:mm");

                    sb.Append($"• {when} — {e.Username} — {e.Section}: {e.Action}");

                    if (!string.IsNullOrWhiteSpace(e.Details))
                        sb.Append($" ({e.Details})");

                    // PHASE D reasons make the log far more useful here.
                    if (!string.IsNullOrWhiteSpace(e.Reason))
                        sb.Append($" — why: {e.Reason}");

                    sb.AppendLine();
                }

                return ToolResult.Ok(sb.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[get_activity] {ex.Message}");
                return ToolResult.Fail(
                    "I could not read the activity log, so I cannot tell you what " +
                    "happened recently.");
            }
        }
    }
}
