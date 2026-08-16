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
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
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
        /// Reports the ACTUAL theme state from ThemeManager (§16).
        ///
        /// READ ONLY, AND STRUCTURALLY SO (§17/§44). ThemeManager exposes
        /// ApplyTheme, ToggleTheme, ApplyAccent and ResetAccent; none of
        /// them is referenced here or anywhere else in the agent. Reading
        /// the theme is safe, changing it as a side effect of answering a
        /// question is not, so the agent has no path to it at all.
        ///
        /// Returns empty rather than guessing when the theme service does
        /// not answer — a wrong colour is worse than no colour.
        /// </summary>
        private static string DescribeTheme()
        {
            try
            {
                var mode = ThemeManager.CurrentTheme;
                var accent = ThemeManager.AccentColor;

                // Name the preset when the accent matches one, so the answer
                // is "Emerald" rather than "#FF10B981" — the user picked a
                // name, not a hex code.
                string accentName = MatchPreset(accent);

                string accentText = string.IsNullOrWhiteSpace(accentName)
                    ? $"a custom accent ({accent})"
                    : $"the {accentName} accent ({accent})";

                return $"Theme is {mode} mode with {accentText}.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetScreenContextTool] theme: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Reverse-maps an accent colour to its preset name, or empty when
        /// it is a custom colour.
        ///
        /// AccentPresets exposes ColorFor(key) and SwatchFor(key) but NO
        /// reverse lookup, so the match is done here over the known keys
        /// rather than adding a method to a shared service for one caller.
        /// Empty is honest: calling a custom colour "Emerald" would be a
        /// small fabrication.
        /// </summary>
        private static string MatchPreset(System.Windows.Media.Color accent)
        {
            string[] keys =
            {
                AccentPresets.GlassBlue,
                AccentPresets.Emerald,
                AccentPresets.Amber,
                AccentPresets.Rose,
                AccentPresets.Violet,
                AccentPresets.Teal
            };

            foreach (var key in keys)
            {
                var c = AccentPresets.ColorFor(key);
                if (c.HasValue && c.Value == accent) return key;
            }

            return "";
        }
    }
}
