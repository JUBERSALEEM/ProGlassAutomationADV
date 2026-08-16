// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Tools/Read/GetScreenContextTool.cs
// PROJECT: ProGlassAutomation — ProGlass Agent
//
// MASTER PROMPT §14/§15 (context awareness), §16 (theme awareness),
//                §17/§44 (reading theme is safe, CHANGING it is not),
//                §21 (read-only), §29/§30 (confidence, no fabrication)
//
// ANSWERS: "What am I looking at?"  "What module is open?"
//          "What theme am I using?"  "Is dark mode on?"
//
// PERMISSION NOTE
//   This tool exposes NO ERP data — only the name of the module on screen
//   and the current theme. There is no natural AppPermissions entry for
//   "may see the window you already have open", and inventing one would be
//   security theatre. It is left with no required permission, which the
//   gate treats as a plain Read. The tool physically cannot reach a
//   customer, a price or a document.
//
// THEME IS READ-ONLY HERE (§17/§44)
//   Reading the active theme is safe. Changing it is not, and no code path
//   in this tool can write one - the agent must never restyle the app as a
//   side effect of answering a question.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Context;
using ProGlassAutomation.Agent.Interfaces;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Tools.Read
{
    /// <summary>Reports the current screen and theme. Never writes.</summary>
    public sealed class GetScreenContextTool : IAgentTool
    {
        public ToolDefinition Definition { get; } = new()
        {
            Name = "get_screen_context",
            Description =
                "Report which module is open, what was recently visited, and the active theme",
            Risk = ToolRisk.Read
            // No RequiredPermission: exposes no ERP data. See header.
        };

        public Task<ToolResult> ExecuteAsync(string argument)
        {
            try
            {
                // Refresh the trail at the moment of asking. No timer and no
                // navigation hook, so nothing is observed while idle (§46).
                AgentScreenContext.Observe();

                var sb = new StringBuilder();

                string here = AgentScreenContext.Describe();

                if (here.Length == 0)
                {
                    // Do not invent a module (§30).
                    sb.AppendLine(
                        "I cannot tell which module is open — the screen " +
                        "tracker has not reported one yet.");
                }
                else
                {
                    sb.AppendLine(here);
                }

                // ── Recent screens, this session only ─────────────────────
                var recent = AgentScreenContext.RecentScreens();

                if (recent.Count > 1)
                {
                    var names = recent
                        .Skip(1)                 // [0] is where we are now
                        .Take(4)
                        .Select(v => v.Module)
                        .ToList();

                    if (names.Count > 0)
                    {
                        sb.AppendLine(
                            "Before that, this session: " +
                            string.Join(" → ", names) + ".");
                    }
                }

                // ── Theme (§16), read-only ────────────────────────────────
                string theme = DescribeTheme();
                if (theme.Length > 0) sb.AppendLine(theme);

                sb.AppendLine();
                sb.Append(
                    "I only track the module name and theme — never what you " +
                    "type, and never a screenshot. None of it is saved to disk.");

                var provenance = AnswerProvenance.Known(
                    "AiScreenContext + ThemeManager");

                string qualifier = provenance.UserQualifier();
                if (qualifier.Length > 0) sb.AppendLine().Append(qualifier);

                return Task.FromResult(ToolResult.Ok(sb.ToString().TrimEnd()));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetScreenContextTool] {ex.Message}");

                return Task.FromResult(ToolResult.Fail(
                    "I could not read the current screen context."));
            }
        }

        /// <summary>
        /// Best-effort theme description. Returns empty rather than guessing
        /// when the theme service does not answer.
        /// </summary>
        private static string DescribeTheme()
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app == null) return "";

                // The accent brush is the one theme value that is reliably
                // present as a resource in this app.
                if (app.TryFindResource("AccentBrush")
                        is System.Windows.Media.SolidColorBrush accent)
                {
                    return $"Accent colour is {accent.Color}.";
                }

                return "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetScreenContextTool] theme: {ex.Message}");
                return "";
            }
        }
    }
}
