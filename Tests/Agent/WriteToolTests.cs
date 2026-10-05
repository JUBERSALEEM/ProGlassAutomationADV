// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/WriteToolTests.cs
// PROJECT: ProGlassAutomation.Tests — PHASE Q
//
// Tests for the agent's first write capability.
//
// SCOPE NOTE — READ THIS BEFORE ADDING TESTS HERE
//   These tests cover the DECLARED CONTRACT and the REFUSAL PATHS only.
//   Every assertion below returns before UpdateSheetStockTool constructs
//   a SheetStoreRepository, so nothing here touches a database.
//
//   The successful-write path is deliberately NOT tested here: it would
//   need a real AppDbContext and would mutate inventory. That path is
//   covered by the manual regression checklist instead. Pretending a
//   passing test proves the write works would be exactly the kind of
//   unverified claim this project forbids.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Intent;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Tools.Registry;
using ProGlassAutomation.Agent.Tools.Write;
using ProGlassAutomation.Services.Audit;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class WriteToolTests
    {
        // ══════════════════════════════════════════════════════════════
        //  THE DECLARED CONTRACT
        //  These are what the permission gate reads. If any of them
        //  regress, the gate silently stops protecting the write.
        // ══════════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════════
        //  REACHABILITY — the tool must be routable from natural language
        // ══════════════════════════════════════════════════════════════

        [Theory]
        [InlineData("add 10 to sheet 42")]
        [InlineData("buy 10 sheet 42")]
        [InlineData("use 5 from sheet 42")]
        [InlineData("receive 25 to sheet 7")]
        [InlineData("consume 5 sheet 42")]
        [InlineData("issue 3 from sheet 19")]
        public void Stock_phrases_route_to_the_write_tool(string phrase)
        {
            // REGRESSION. The write tool shipped UNREACHABLE: the intent
            // keywords were "add stock" / "add to sheet", but the tool's
            // parser requires a quantity in between ("add 10 to sheet 42"),
            // so no adjacent-keyword match could ever fire. A registered,
            // permission-gated, audited, tested tool that no sentence can
            // reach is a dead feature, and nothing failed loudly enough to
            // reveal it.
            string intent = IntentDetector.Detect(
                phrase, System.Array.Empty<TeachingRuleView>());

            Assert.StartsWith("tool:update_sheet_stock", intent);
        }

        [Theory]
        [InlineData("show inventory")]
        [InlineData("what stock do we have")]
        [InlineData("add some stock")]       // no quantity, no sheet id
        [InlineData("low stock report")]
        public void Vague_stock_phrases_stay_on_the_read_tool(string phrase)
        {
            // Ambiguity must resolve to the READ tool, never the write.
            string intent = IntentDetector.Detect(
                phrase, System.Array.Empty<TeachingRuleView>());

            Assert.DoesNotContain("update_sheet_stock", intent);
        }

        [Fact]
        public void Declares_write_risk_permission_and_confirmation()
        {
            var def = new UpdateSheetStockTool().Definition;

            Assert.Equal(ToolRisk.Write, def.Risk);
            Assert.Equal(AppPermissions.SheetStoreEdit, def.RequiredPermission);
            Assert.True(def.RequiresConfirmation);
        }

        [Fact]
        public void Registry_exposes_exactly_one_write_tool_and_no_destructive_tool()
        {
            // Build returns Dictionary<string, IAgentTool> — iterate Values.
            var tools = AgentToolRegistry.Build().Values;

            Assert.Single(tools.Where(t => t.Definition.Risk == ToolRisk.Write));
            Assert.Empty(tools.Where(t => t.Definition.Risk == ToolRisk.Destructive));
        }

        [Fact]
        public void Every_write_tool_in_the_registry_requires_confirmation()
        {
            // Guards the future: a new write tool added without
            // RequiresConfirmation would slip past review, not past this.
            var writeTools = AgentToolRegistry.Build().Values
                .Where(t => t.Definition.Risk == ToolRisk.Write);

            Assert.All(writeTools, t =>
            {
                Assert.True(t.Definition.RequiresConfirmation);
                Assert.False(string.IsNullOrWhiteSpace(t.Definition.RequiredPermission));
            });
        }

        // ══════════════════════════════════════════════════════════════
        //  AMBIGUITY IS REFUSED, NOT GUESSED
        // ══════════════════════════════════════════════════════════════

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("add some stock")]            // no quantity, no id
        [InlineData("add 10")]                    // no sheet
        [InlineData("sheet 42")]                  // no verb, no quantity
        [InlineData("delete sheet 42")]           // not a supported verb
        [InlineData("add 10 to all sheets")]      // bulk — must never parse
        [InlineData("add -5 to sheet 42")]        // negative
        [InlineData("add 2.5 to sheet 42")]       // fractional
        [InlineData("add 105")]                   // see regression test below
        [InlineData("add 10 to 42")]              // no "sheet"/"#" marker
        public async Task Ambiguous_or_unsupported_input_changes_nothing(string? input)
        {
            var result = await new UpdateSheetStockTool().ExecuteAsync(input!);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Bulk_phrasing_is_rejected_before_any_database_access()
        {
            // The single most dangerous input class: a sentence that reads
            // like a mass update. It must die at the parser.
            var result = await new UpdateSheetStockTool()
                .ExecuteAsync("use 1 from every sheet");

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Digits_without_a_sheet_marker_cannot_split_into_qty_and_id()
        {
            // REGRESSION. An earlier pattern made the "sheet"/"#" token
            // optional, so "add 105" parsed as qty=10, id=5 and adjusted a
            // sheet the user never named. Silently writing to the wrong
            // record is the worst failure this tool can have, so it gets
            // its own named test rather than one line in a Theory.
            var result = await new UpdateSheetStockTool().ExecuteAsync("add 105");

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Quantity_above_the_per_call_cap_is_refused()
        {
            var result = await new UpdateSheetStockTool()
                .ExecuteAsync("add 501 to sheet 42");

            Assert.False(result.Success);
            Assert.Contains("501", result.Message);
        }

        [Fact]
        public async Task Refusals_explain_themselves()
        {
            var result = await new UpdateSheetStockTool().ExecuteAsync("add some stock");

            Assert.False(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(result.Message));
        }
    }
}
