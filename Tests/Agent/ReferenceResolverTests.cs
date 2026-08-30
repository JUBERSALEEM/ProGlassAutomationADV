// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ReferenceResolverTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// MASTER PROMPT §14 (context resolver), §29 (inferred confidence),
//                §30 (no fabrication), §31 (ambiguity), §22 (write safety)
//
// Most of these assert what the resolver must REFUSE to do. Silently
// supplying the wrong subject is the failure mode that matters: the answer
// still looks authoritative, it is just about a different document.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Context;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Services.AI.AIContext;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    [Collection("AgentSettings")]   // shares static screen context
    public class ReferenceResolverTests
    {
        private sealed class FakeVm : IAgentContextAware
        {
            public string Detail = "";
            public string AgentContextDetail => Detail;
        }

        private static void OnScreen(string detail)
        {
            AgentScreenContext.CurrentViewModelProvider =
                () => new FakeVm { Detail = detail };
        }

        private static void ClearScreen()
        {
            AgentScreenContext.CurrentViewModelProvider = null;
            AiScreenContext.Set("", "");
        }

        // ══════════════════════════════════════════════════════════════
        //  IT RESOLVES WHEN IT SHOULD
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Fills_an_empty_argument_from_the_open_document()
        {
            OnScreen("editing PI-2026-014");
            try
            {
                var r = AgentReferenceResolver.TryResolve(
                    "get_document_chain", ToolRisk.Read, "");

                Assert.True(r.Resolved);
                Assert.Equal("PI-2026-014", r.Value);
            }
            finally { ClearScreen(); }
        }

        [Fact]
        public void States_the_assumption_so_a_wrong_guess_is_visible()
        {
            OnScreen("editing PI-2026-014");
            try
            {
                var r = AgentReferenceResolver.TryResolve(
                    "get_document_chain", ToolRisk.Read, "");

                Assert.Contains("PI-2026-014", r.Assumption);
                Assert.Contains("open on screen", r.Assumption);
            }
            finally { ClearScreen(); }
        }

        // ══════════════════════════════════════════════════════════════
        //  IT REFUSES WHEN IT SHOULD — THE PART THAT MATTERS
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void An_explicit_subject_always_wins_over_context()
        {
            // Context must never override what the user actually said.
            OnScreen("editing PI-2026-014");
            try
            {
                var r = AgentReferenceResolver.TryResolve(
                    "get_document_chain", ToolRisk.Read, "PI-9999");

                Assert.False(r.Resolved);
            }
            finally { ClearScreen(); }
        }

        [Theory]
        [InlineData(ToolRisk.Write)]
        [InlineData(ToolRisk.Destructive)]
        public void Never_infers_the_target_of_a_change(ToolRisk risk)
        {
            // Guessing which record to MODIFY is how the wrong one gets
            // edited. A write with no stated subject must be refused.
            OnScreen("editing PI-2026-014");
            try
            {
                var r = AgentReferenceResolver.TryResolve(
                    "get_document_chain", risk, "");

                Assert.False(r.Resolved);
            }
            finally { ClearScreen(); }
        }

        [Fact]
        public void Does_not_resolve_for_tools_outside_the_allow_list()
        {
            // Filling a blank on a search tool would silently narrow a
            // deliberately broad query.
            OnScreen("editing PI-2026-014");
            try
            {
                var r = AgentReferenceResolver.TryResolve(
                    "search_delivery", ToolRisk.Read, "");

                Assert.False(r.Resolved);
            }
            finally { ClearScreen(); }
        }

        [Fact]
        public void A_module_name_is_not_a_document()
        {
            // "Sheet Store" is a screen, not a record. Nothing to resolve.
            OnScreen("sheet selected: 6mm Clear 3210x2250");
            try
            {
                var r = AgentReferenceResolver.TryResolve(
                    "get_document_chain", ToolRisk.Read, "");

                Assert.False(r.Resolved);
            }
            finally { ClearScreen(); }
        }

        [Fact]
        public void Resolves_nothing_when_no_document_is_open()
        {
            ClearScreen();

            var r = AgentReferenceResolver.TryResolve(
                "get_document_chain", ToolRisk.Read, "");

            Assert.False(r.Resolved);
        }

        [Theory]
        [InlineData("editing JO-2201", "JO-2201")]
        [InlineData("editing DN-2026-7", "DN-2026-7")]
        [InlineData("editing TI-501", "TI-501")]
        public void Recognises_the_document_prefixes_this_erp_uses(
            string detail, string expected)
        {
            OnScreen(detail);
            try
            {
                var r = AgentReferenceResolver.TryResolve(
                    "get_document_chain", ToolRisk.Read, "");

                Assert.True(r.Resolved);
                Assert.Equal(expected, r.Value);
            }
            finally { ClearScreen(); }
        }

        [Theory]
        [InlineData("Pipeline")]          // contains "PI"
        [InlineData("Job Order")]         // contains "JO"
        [InlineData("Title bar")]         // contains "TI"
        [InlineData("dnata")]             // contains "DN"
        [InlineData("Proforma Invoice")]
        public void Ordinary_words_are_never_mistaken_for_a_document_id(
            string detail)
        {
            // REGRESSION. The first pattern made the separator optional and
            // allowed any trailing characters, so "Pipeline" matched as
            // PI + "peline" and "Job Order" as "Job". A module publishing
            // prose would have produced a FABRICATED document id and the
            // agent would have answered about a record that does not exist.
            // The hyphen and following digit are now both mandatory.
            OnScreen(detail);
            try
            {
                var r = AgentReferenceResolver.TryResolve(
                    "get_document_chain", ToolRisk.Read, "");

                Assert.False(r.Resolved);
            }
            finally { ClearScreen(); }
        }

        [Fact]
        public void Whitespace_only_argument_counts_as_empty()
        {
            OnScreen("editing PI-2026-014");
            try
            {
                var r = AgentReferenceResolver.TryResolve(
                    "get_document_chain", ToolRisk.Read, "   ");

                Assert.True(r.Resolved);
            }
            finally { ClearScreen(); }
        }

        [Theory]
        [InlineData("for PI-1052", "PI-1052")]
        [InlineData("document chain for PI-1052", "PI-1052")]
        [InlineData("status of PI-2026-014 please", "PI-2026-014")]
        [InlineData("JO-2201", "JO-2201")]
        public void Extracts_a_document_id_from_noisy_tool_arguments(
            string text, string expected)
        {
            Assert.True(AgentReferenceResolver.TryExtractDocumentId(text, out var id));
            Assert.Equal(expected, id);
        }

        [Theory]
        [InlineData("for the pipeline")]
        [InlineData("Job Order")]
        [InlineData("")]
        public void Does_not_mint_a_document_id_from_english(string text)
        {
            Assert.False(AgentReferenceResolver.TryExtractDocumentId(text, out _));
        }
    }
}
