using ProGlassAutomation.Agent.Intent;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Nlu;
using System;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class NluRoutingTests
    {
        public NluRoutingTests() => ConversationSlots.Clear();

        private static string Detect(string phrase) =>
            IntentDetector.Detect(phrase, Array.Empty<TeachingRuleView>());

        [Theory]
        [InlineData("show glas stok", "get_inventory")]
        [InlineData("6 clr glas", "get_inventory")]
        [InlineData("how much 6mm clear glass left", "get_inventory")]
        [InlineData("kitna clear glass bacha hai", "get_inventory")]
        public void Typos_and_hinglish_reach_inventory(string phrase, string tool)
        {
            Assert.Contains(tool, Detect(phrase));
        }

        [Theory]
        [InlineData("how much 6mm clear purchased", "get_stock_history")]
        [InlineData("6 clr kitna kharida", "get_stock_history")]
        [InlineData("how much did we buy", "get_stock_history")]
        [InlineData("how much 6 clr glas purches", "get_stock_history")]
        public void Purchase_questions_reach_stock_history(string phrase, string tool)
        {
            Assert.Contains(tool, Detect(phrase));
        }

        [Fact]
        public void Typo_stock_question_reaches_inventory()
        {
            Assert.Contains("get_inventory", Detect("how much 6 clr glas stok"));
        }

        [Fact]
        public void Follow_up_purchase_keeps_thickness()
        {
            Detect("how much 6 clr glas stok");
            string second = Detect("what about purchase");
            Assert.Contains("get_stock_history", second);
            Assert.Contains("6mm", second);
        }

        [Fact]
        public void Follow_up_keeps_thickness_and_changes_colour()
        {
            Detect("6mm clear stock");
            string second = Detect("what about grey");
            Assert.Contains("get_inventory", second);
            Assert.Contains("6mm", second);
        }

        [Fact]
        public void Write_tool_still_requires_quantity_syntax()
        {
            Assert.Contains("update_sheet_stock", Detect("add 10 to sheet 42"));
            Assert.DoesNotContain("update_sheet_stock", Detect("add some stock"));
        }

        [Fact]
        public void Demo_command_is_reachable()
        {
            Assert.Contains("create_demo_package", Detect("make a demo for delivery note"));
        }

        [Fact]
        public void Proforma_invoice_demo_names_the_module()
        {
            string intent = Detect("Create a Proforma Invoice demo.");
            Assert.Contains("create_demo_package", intent);
            Assert.Contains("ProformaInvoice", intent);
        }

        [Fact]
        public void Soft_correct_does_not_invent_unknown_words()
        {
            string prepared = QueryNormalizer.Prepare("xyzzy");
            Assert.Equal("xyzzy", prepared);
        }
    }
}
