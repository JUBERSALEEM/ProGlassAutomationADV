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
using ProGlassAutomation.Agent.Nlu;
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
            (new[] { "inventory", "stock", "how many sheets", "sheets do we have",
                     "howmuch glass", "glass left" },
                "get_inventory"),

            (new[] { "purchased", "purchase quantity", "how much did we buy",
                     "how much did we purchase", "consumed", "how much used",
                     "sheet history", "stock history", "buy history" },
                "get_stock_history"),

            (new[] { "last purchase", "which supplier", "running low",
                     "stock breakdown", "available quantity" },
                "get_stock_detail"),

            (new[] { "create a demo", "make a demo", "record a demo",
                     "demo video", "workflow demo", "generate demo" },
                "create_demo_package"),

            // Central reporting. Specific keys only — bare "proforma",
            // "invoice", "this month" would steal search/analytics.
            // Must sit BEFORE those catalogs so "how many PI this month"
            // is a count, not a document list or a sales-period total.
            (new[] { "reporting", "erp overview",
                     "how many proforma", "how many pi",
                     "confirmed proforma", "confirmed pi",
                     "proforma this month", "pi this month",
                     "spec rows", "glass rows",
                     "without job order", "not converted",
                     "pending invoices", "pending invoice",
                     "rows in pi", "rows inside pi", "rows inside",
                     "quantity reconciliation",
                     "companies ordered", "exact spec",
                     "dgu specification" },
                "get_reporting"),

            // Verb-led forms PLUS the bare noun. Soft-correct used to
            // rewrite "customers" → "customer", which then matched nothing.
            (new[] { "search customer", "find customer", "customer named",
                     "look up customer", "client named",
                     "customers", "customer", "customer list", "clients" },
                "search_customer"),

            (new[] { "search proforma", "find proforma", "search quotation",
                     "find quotation", "proforma number",
                     "proformas", "proforma", "quotations", "quotation" },
                "search_proforma"),

            (new[] { "search job", "find job", "job order number", "search job order",
                     "job orders", "job order", "jobs list",
                     "today's orders", "todays orders", "today orders" },
                "search_joborder"),

            (new[] { "search delivery", "find delivery", "delivery note number",
                     "delivery notes", "delivery note", "deliveries",
                     "pending deliveries", "pending delivery" },
                "search_delivery"),

            (new[] { "search tax invoice", "find tax invoice", "search invoice",
                     "find invoice", "invoice number",
                     "tax invoices", "tax invoice", "invoices" },
                "search_taxinvoice"),

            (new[] { "activity", "audit", "recent actions", "who did", "what happened" },
                "get_activity"),

            // Cross-module analytics (§33). BEFORE get_profitability so
            // "how much profit this month" is a PERIOD question, while a
            // question about one job still hits profitability below.
            (new[] { "this month", "last month", "this year", "last year",
                     "year to date", "how much did we sell", "total sales",
                     "gross profit", "sales analytics", "revenue" },
                "get_sales_analytics"),

            // PHASE N — profit QUESTIONS answer with data.
            //
            // The nav catalog also owns "profit" (opens Job Profitability)
            // and tools are checked FIRST. Keys are phrased as questions
            // so "how much profit" returns figures while "open
            // profitability" still navigates.
            (new[] { "how much profit", "what profit", "profit on",
                     "margin on", "did we make money", "are we losing",
                     "losing money", "loss making", "profitable" },
                "get_profitability"),

            // Statements / receivables (§9, §33).
            (new[] { "statement", "outstanding", "receivable", "overdue",
                     "owes us", "unpaid", "aging", "ageing" },
                "get_statement"),

            // Screen/theme awareness. Deliberately narrow phrases so a
            // question about ERP data never lands here.
            (new[] { "what am i looking at", "what screen", "which module",
                     "what module", "current screen", "where am i",
                     "what theme", "accent colour", "accent color",
                     "dark mode" },
                "get_screen_context"),

            // Document chain. Placed BEFORE the write intent and after the
            // plain searches: these phrases are unambiguous requests to
            // follow a document through the workflow.
            (new[] { "document chain", "chain for", "status of pi", "linked to",
                     "delivered so far", "how much delivered", "still pending",
                     "remaining quantity", "which job order", "which delivery",
                     "which tax invoice", "trace" },
                "get_document_chain"),

            // NOTE: the write intent is deliberately ABSENT from this
            // catalog. It needs a quantity between the verb and the target
            // ("add 10 to sheet 42"), which adjacent-keyword matching
            // cannot express - see StockAdjustPattern, matched before this
            // loop runs.
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

            var parsed = ErpQueryParser.Parse(raw);
            string lower = string.IsNullOrEmpty(parsed.Prepared)
                ? raw.ToLowerInvariant()
                : parsed.Prepared;

            bool leftoverAsk = lower.Contains("left") || lower.Contains("remaining")
                || lower.Contains("bacha") || lower.Contains("available quantity");
            bool mentionsStockWord = lower.Contains("inventory") || lower.Contains("stock")
                || lower.Contains("sheets");
            bool freshAllStock = !parsed.HasMaterialHint && !leftoverAsk && mentionsStockWord
                && parsed.Metric is ErpMetric.Available or ErpMetric.Unknown;
            if (!freshAllStock)
                ConversationSlots.ApplyIfContinuing(parsed);

            var stockAdjust = StockAdjustPattern.Match(raw);
            if (stockAdjust.Success)
                return $"tool:update_sheet_stock:{Clean(stockAdjust.Value)}";

            if (parsed.WantsDemo)
            {
                string arg = parsed.DemoModule ?? "";
                ConversationSlots.Remember(parsed, "create_demo_package");
                return arg.Length > 0
                    ? $"tool:create_demo_package:{arg}"
                    : "tool:create_demo_package";
            }

            if (parsed.Metric is ErpMetric.Purchased or ErpMetric.Consumed)
            {
                ConversationSlots.ApplyMissing(parsed);
                string histFilter = parsed.FilterText();
                ConversationSlots.Remember(parsed, "get_stock_history");
                return histFilter.Length > 0
                    ? $"tool:get_stock_history:{histFilter}"
                    : "tool:get_stock_history";
            }

            if (parsed.Metric == ErpMetric.Delivered)
            {
                ConversationSlots.ApplyMissing(parsed);
                string filter = parsed.FilterText();
                ConversationSlots.Remember(parsed, "search_delivery");
                return filter.Length > 0
                    ? $"tool:search_delivery:{filter}"
                    : "tool:search_delivery";
            }

            if (parsed.Metric is ErpMetric.Supplier or ErpMetric.LastPurchase
                or ErpMetric.Wastage or ErpMetric.LowStock)
            {
                string filter = parsed.FilterText();
                ConversationSlots.Remember(parsed, "get_stock_detail");
                return filter.Length > 0
                    ? $"tool:get_stock_detail:{filter}"
                    : "tool:get_stock_detail";
            }

            if (parsed.HasMaterialHint
                && parsed.Metric is ErpMetric.Available or ErpMetric.Unknown)
            {
                string filter = parsed.FilterText();
                ConversationSlots.Remember(parsed, "get_inventory");
                return filter.Length > 0
                    ? $"tool:get_inventory:{filter}"
                    : "tool:get_inventory";
            }

            if (leftoverAsk && ConversationSlots.HasSubject)
            {
                ConversationSlots.ApplyMissing(parsed);
                string filter = parsed.FilterText();
                ConversationSlots.Remember(parsed, "get_inventory");
                return filter.Length > 0
                    ? $"tool:get_inventory:{filter}"
                    : "tool:get_inventory";
            }

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

            // Stock adjustment is matched once above, before metric
            // routing. A second copy here was dead code.

            // 4 ── tool intents (BEFORE navigation)
            foreach (var (keys, tool) in ToolCatalog)
            {
                if (!keys.Any(k => lower.Contains(k))) continue;

                // Reporting interprets the whole utterance (period, status,
                // PI id). Stripping the matched key would drop "PI" and
                // "this month" and the tool would answer a generic overview.
                string query = string.Equals(tool, "get_reporting",
                        StringComparison.OrdinalIgnoreCase)
                    ? Clean(raw)
                    : ExtractQuery(raw, keys);
                if (query.Length == 0 && parsed.HasMaterialHint)
                    query = parsed.FilterText();
                ConversationSlots.Remember(parsed, tool);
                return query.Length > 0 ? $"tool:{tool}:{query}" : $"tool:{tool}";
            }

            if (parsed.IsFollowUp && ConversationSlots.HasSubject)
            {
                string resume = ConversationSlots.LastTool ?? "get_inventory";
                string filter = parsed.FilterText();
                ConversationSlots.Remember(parsed, resume);
                return filter.Length > 0
                    ? $"tool:{resume}:{filter}"
                    : $"tool:{resume}";
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
        /// <summary>
        /// Mirrors UpdateSheetStockTool.Syntax: verb + quantity + sheet
        /// marker + id. Kept deliberately strict so an ambiguous sentence
        /// never routes to the write tool; the tool re-validates anyway.
        /// </summary>
        private static readonly Regex StockAdjustPattern = new(
            @"\b(?:add|buy|receive|use|consume|issue)\s+\d{1,4}\s+" +
            @"(?:(?:to|from|on|for)\s+)?(?:sheet\s*|#)\d{1,9}\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
