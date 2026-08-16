// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Intent/IntentDetector.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// DETERMINISTIC intent detection. No LLM, no network, no randomness:
// the same sentence always produces the same intent, which is what makes
// the agent auditable and testable.
//
// PRECEDENCE (deliberate, do not reorder casually)
//   1. help              — must always be reachable
//   2. teach capture     — "whenever I say X, Y" / "remember that X means Y"
//   3. approve rule N    — approving a taught rule
//   4. TOOL intents      — "stock", "search customer", ...
//   5. NAVIGATION        — "open sheet store"
//   6. approved teaching — user rules match LAST so a taught rule can
//                          never shadow a built-in system behaviour
//
// Tool intents deliberately precede navigation: "show me the stock" should
// report numbers, not merely open the Sheet Store screen.
//
// Returned intent strings are a tiny DSL the planner understands:
//   "help" · "teach:<trigger>|<action>" · "approve:<id>"
//   "tool:<name>" · "tool:<name>:<query>" · "navigate:<target>" · "answer"
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProGlassAutomation.Agent.Intent
{
    /// <summary>Deterministic rule-based intent detection.</summary>
    public static class IntentDetector
    {
        // ── Teaching capture ──────────────────────────────────────────────
        private static readonly Regex WheneverPattern = new(
            @"whenever i say (.+?)[,\.]\s*,?\s*(.+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RememberPattern = new(
            @"remember that (.+?) means (.+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ApprovePattern = new(
            @"\bapprove (?:rule )?(\d+)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Phrases that always reach help.</summary>
        private static readonly string[] HelpKeys =
        {
            "help", "what can you do", "commands", "how do i use you",
            "what do you do"
        };

        // ══════════════════════════════════════════════════════════════
        //  CATALOGS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Tool catalog — phrase fragments mapped to a tool name.
        /// Checked BEFORE navigation so a data question answers with data.
        /// </summary>
        private static readonly (string[] Keys, string Tool)[] ToolCatalog =
        {
            (new[] { "inventory", "stock", "how many sheets", "sheets do we have" },
                "get_inventory"),

            (new[] { "search customer", "find customer", "customer named",
                     "look up customer", "client named" },
                "search_customer"),

            (new[] { "search proforma", "find proforma", "search quotation",
                     "find quotation", "proforma number" },
                "search_proforma"),

            (new[] { "search job", "find job", "job order number", "search job order" },
                "search_joborder"),

            (new[] { "search delivery", "find delivery", "delivery note number" },
                "search_delivery"),

            (new[] { "search tax invoice", "find tax invoice", "search invoice",
                     "find invoice", "invoice number" },
                "search_taxinvoice"),

            (new[] { "activity", "audit", "recent actions", "who did", "what happened" },
                "get_activity"),

            // PHASE N — profit QUESTIONS answer with data.
            //
            // Ordering note: the nav catalog also owns "profit" (it opens the
            // Job Profitability module) and tools are checked FIRST. These
            // keys are therefore deliberately phrased as questions - "how
            // much profit", "did we make money" - so that asking a question
            // returns figures, while "open profitability" still navigates.
            (new[] { "how much profit", "what profit", "profit on",
                     "margin on", "did we make money", "are we losing",
                     "losing money", "loss making", "profitable" },
                "get_profitability")
        };

        /// <summary>
        /// Navigation catalog — phrase fragments mapped to a shell key.
        /// These MUST match the cases in MainViewModel.Navigate().
        /// </summary>
        private static readonly (string[] Keys, string Target)[] NavCatalog =
        {
            (new[] { "dashboard", "home screen", "overview" }, "Dashboard"),
            (new[] { "sheet store", "inventory screen", "glass store", "stock screen" },
                "SheetStore"),
            (new[] { "proforma", "quotation", "quote screen" }, "ProformaInvoice"),
            (new[] { "job order", "production order", "jobs" }, "JobOrder"),
            (new[] { "delivery" }, "DeliveryNote"),
            (new[] { "tax invoice", "invoicing" }, "TaxInvoice"),
            (new[] { "statement", "report", "sales report" }, "SalesReport"),
            (new[] { "company" }, "CompanyCenter"),
            (new[] { "user center", "users" }, "UserCenter"),
            (new[] { "profit", "profitability", "margin" }, "JobProfitability"),
            (new[] { "settings", "preferences" }, "Settings")
        };

        /// <summary>Verbs that mean "go there" in a taught action.</summary>
        private static readonly string[] NavigationVerbs =
        {
            "open", "go to", "show me the", "take me to", "switch to", "navigate to"
        };

        // ══════════════════════════════════════════════════════════════
        //  DETECTION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Maps free text to an intent string. Never throws; unknown text
        /// falls through to "answer" (the reasoning provider).
        /// </summary>
        /// <param name="text">Raw (already sanitized) user text.</param>
        /// <param name="activeTeachingRules">Approved rules only.</param>
        public static string Detect(
            string text,
            IReadOnlyList<TeachingRuleView>? activeTeachingRules = null)
        {
            string raw = (text ?? "").Trim();
            if (raw.Length == 0) return "answer";

            string lower = raw.ToLowerInvariant();

            // 1 ── help
            if (HelpKeys.Any(k => lower.Contains(k)))
                return "help";

            // 2 ── teach capture
            var whenever = WheneverPattern.Match(raw);
            if (whenever.Success)
            {
                string trigger = Clean(whenever.Groups[1].Value);
                string action = MapAction(whenever.Groups[2].Value);
                if (trigger.Length > 0 && action.Length > 0)
                    return $"teach:{trigger}|{action}";
            }

            var remember = RememberPattern.Match(raw);
            if (remember.Success)
            {
                string trigger = Clean(remember.Groups[1].Value);
                string action = MapAction(remember.Groups[2].Value);
                if (trigger.Length > 0 && action.Length > 0)
                    return $"teach:{trigger}|{action}";
            }

            // 3 ── approve a taught rule
            var approve = ApprovePattern.Match(raw);
            if (approve.Success)
                return $"approve:{approve.Groups[1].Value}";

            // 4 ── tool intents (BEFORE navigation)
            foreach (var (keys, tool) in ToolCatalog)
            {
                if (!keys.Any(k => lower.Contains(k))) continue;

                string query = ExtractQuery(raw, keys);
                return query.Length > 0 ? $"tool:{tool}:{query}" : $"tool:{tool}";
            }

            // 5 ── navigation
            foreach (var (keys, target) in NavCatalog)
            {
                if (keys.Any(k => lower.Contains(k)))
                    return $"navigate:{target}";
            }

            // 6 ── approved teaching rules match LAST so user rules can
            //      never shadow built-in system behaviour.
            if (activeTeachingRules != null)
            {
                foreach (var rule in activeTeachingRules)
                {
                    if (!rule.IsApproved) continue;

                    string trigger = (rule.Trigger ?? "").Trim().ToLowerInvariant();
                    if (trigger.Length == 0) continue;

                    if (lower.Contains(trigger))
                        return rule.Action ?? "answer";
                }
            }

            return "answer";
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Turns the free-text half of a teaching sentence into the action
        /// DSL ("navigate:SheetStore" / "tool:get_inventory"). Returns ""
        /// when nothing recognizable was requested — an unmappable action
        /// is REFUSED rather than stored as a vague instruction.
        /// </summary>
        public static string MapAction(string actionText)
        {
            string lower = Clean(actionText).ToLowerInvariant();
            if (lower.Length == 0) return "";

            // Tools first — same precedence rule as Detect().
            foreach (var (keys, tool) in ToolCatalog)
            {
                if (keys.Any(k => lower.Contains(k)))
                {
                    string query = ExtractQuery(lower, keys);
                    return query.Length > 0 ? $"tool:{tool}:{query}" : $"tool:{tool}";
                }
            }

            foreach (var (keys, target) in NavCatalog)
            {
                if (keys.Any(k => lower.Contains(k)))
                    return $"navigate:{target}";
            }

            // A bare navigation verb with an unknown destination is not
            // actionable — refuse instead of guessing.
            if (NavigationVerbs.Any(v => lower.StartsWith(v)))
                return "";

            return "";
        }

        /// <summary>
        /// Pulls the search term out of "search customer Al Futtaim" →
        /// "Al Futtaim". Returns "" when the phrase carried no term.
        /// </summary>
        private static string ExtractQuery(string text, string[] keys)
        {
            string working = text ?? "";

            foreach (var key in keys.OrderByDescending(k => k.Length))
            {
                int idx = working.IndexOf(key, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) continue;

                string tail = working[(idx + key.Length)..];
                return Clean(tail.TrimStart(':', '-', ' ', '"'));
            }

            return "";
        }

        private static string Clean(string value) =>
            (value ?? "").Trim().Trim('"', '\'', '.', ',', '?', '!').Trim();
    }
}
