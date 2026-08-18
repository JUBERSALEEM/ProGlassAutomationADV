// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ToolReachabilityTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// WHY THIS FILE EXISTS
//   Every other agent test calls ExecuteAsync directly. That proves a tool
//   WORKS; it says nothing about whether a human sentence can ever reach
//   it. Two real defects hid in exactly that gap:
//
//     1. update_sheet_stock shipped in Phase Q completely unreachable. Its
//        intent keywords were "add stock" / "add to sheet", but its parser
//        needs a quantity between the verb and the target ("add 10 to
//        sheet 42"), so no keyword match could ever fire. Registered,
//        permission-gated, audited, unit-tested — and unusable.
//
//     2. The five search tools only matched "search X" / "find X". Every
//        natural phrasing — "show delivery notes", "list job orders" —
//        fell through to the reasoning provider. 11 of 11 tested phrases
//        were unroutable.
//
//   Both were found by hand-running phrases through the catalog, not by
//   the suite. This file closes that hole permanently: every registered
//   tool must be reachable from at least one sentence a person would
//   actually type.
//
// THE INVARIANT
//   registered tools ⊆ routable tools
//   If someone adds a tool to AgentToolRegistry without a routing entry,
//   Every_registered_tool_is_reachable fails and names it.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Intent;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Tools.Registry;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class ToolReachabilityTests
    {
        private static string Detect(string phrase) =>
            IntentDetector.Detect(phrase, Array.Empty<TeachingRuleView>());

        /// <summary>
        /// One sentence per tool, phrased the way a user would actually say
        /// it — not the way the keyword list is written.
        /// </summary>
        private static readonly Dictionary<string, string> Phrases = new()
        {
            ["search_proforma"] = "show proformas",
            ["search_joborder"] = "list job orders",
            ["search_delivery"] = "show delivery notes",
            ["search_taxinvoice"] = "show tax invoices",
            ["search_customer"] = "list customers",
            ["get_inventory"] = "show inventory",
            ["get_activity"] = "what happened recently",
            ["get_profitability"] = "which jobs are losing money",
            ["get_document_chain"] = "document chain for PI-1052",
            ["get_screen_context"] = "what am i looking at",
            ["get_statement"] = "what is outstanding",
            ["get_sales_analytics"] = "how much did we sell this month",
            ["update_sheet_stock"] = "add 10 to sheet 42",
            ["get_stock_detail"] = "running low",
            ["get_stock_history"] = "how much did we purchase",
            ["create_demo_package"] = "create a demo for inventory"
        };

        // ══════════════════════════════════════════════════════════════
        //  THE INVARIANT
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Every_registered_tool_is_reachable()
        {
            // navigate is excluded: it is routed by the navigation catalog,
            // which is a separate mechanism with its own tests.
            var registered = AgentToolRegistry.Build().Values
                .Select(t => t.Definition.Name)
                .Where(n => !string.Equals(n, "navigate",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            var unreachable = new List<string>();

            foreach (var name in registered)
            {
                if (!Phrases.TryGetValue(name, out var phrase))
                {
                    // A tool with no phrase here has never been proven
                    // reachable. Treat that as a failure, not an omission.
                    unreachable.Add($"{name} (no test phrase)");
                    continue;
                }

                if (!Detect(phrase).Contains(name, StringComparison.OrdinalIgnoreCase))
                    unreachable.Add($"{name} (\"{phrase}\" did not route to it)");
            }

            Assert.True(
                unreachable.Count == 0,
                "Unreachable tool(s): " + string.Join("; ", unreachable));
        }

        [Theory]
        [InlineData("show proformas", "search_proforma")]
        [InlineData("list proformas", "search_proforma")]
        [InlineData("show job orders", "search_joborder")]
        [InlineData("list job orders", "search_joborder")]
        [InlineData("show delivery notes", "search_delivery")]
        [InlineData("show me the deliveries", "search_delivery")]
        [InlineData("show tax invoices", "search_taxinvoice")]
        [InlineData("list customers", "search_customer")]
        [InlineData("show all customers", "search_customer")]
        public void Natural_phrasings_route_correctly(string phrase, string tool)
        {
            // REGRESSION: all of these were unroutable before the keyword
            // lists were widened beyond "search X" / "find X".
            Assert.Contains(tool, Detect(phrase));
        }

        // ══════════════════════════════════════════════════════════════
        //  WIDENING MUST NOT HAVE BROKEN PRECEDENCE
        // ══════════════════════════════════════════════════════════════

        [Theory]
        [InlineData("what is outstanding", "get_statement")]
        [InlineData("show the statement for ABC Glass", "get_statement")]
        [InlineData("which jobs are losing money", "get_profitability")]
        [InlineData("how much profit on PI-1052", "get_profitability")]
        [InlineData("show inventory", "get_inventory")]
        [InlineData("what stock do we have", "get_inventory")]
        [InlineData("what am i looking at", "get_screen_context")]
        [InlineData("document chain for PI-1052", "get_document_chain")]
        public void Broad_nouns_did_not_hijack_specific_intents(
            string phrase, string tool)
        {
            // Adding bare nouns like "invoices" and "customers" risks
            // swallowing more specific questions. It must not.
            Assert.Contains(tool, Detect(phrase));
        }

        [Theory]
        [InlineData("how much did we sell this month", "get_sales_analytics")]
        [InlineData("what gross profit this year", "get_sales_analytics")]
        [InlineData("total sales last month", "get_sales_analytics")]
        public void Period_questions_route_to_analytics_not_single_job_profit(
            string phrase, string tool)
        {
            // "profit" appears in BOTH get_profitability and the analytics
            // phrases. A PERIOD question must reach analytics; a question
            // about one job must not.
            Assert.Contains(tool, Detect(phrase));
        }

        [Theory]
        [InlineData("how much profit on PI-1052", "get_profitability")]
        [InlineData("which jobs are losing money", "get_profitability")]
        public void Single_job_questions_still_reach_profitability(
            string phrase, string tool)
        {
            Assert.Contains(tool, Detect(phrase));
        }

        [Theory]
        [InlineData("show inventory")]
        [InlineData("what stock do we have")]
        [InlineData("add some stock")]
        [InlineData("show delivery notes")]
        [InlineData("list customers")]
        public void Nothing_vague_reaches_the_write_tool(string phrase)
        {
            // The single most important routing guarantee: ambiguity
            // resolves to a READ, never to something that changes data.
            Assert.DoesNotContain("update_sheet_stock", Detect(phrase));
        }
    }
}
