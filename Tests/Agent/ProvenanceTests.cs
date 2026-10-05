// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ProvenanceTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// MASTER PROMPT §28 (provenance), §29 (confidence), §30 (no fabrication),
//                §32 (document chain), §21 (read-only)
//
// SCOPE NOTE
//   These cover the provenance model and the document-chain tool's DECLARED
//   CONTRACT and refusal paths. The chain tool's happy path needs live
//   repositories (App.Jobs et al.) and is therefore on the manual checklist,
//   not faked here — a green test that never touched a repository would be
//   a false assurance about the thing most worth checking.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Context;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Tools.Read;
using ProGlassAutomation.Agent.Tools.Registry;
using ProGlassAutomation.Services.Audit;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class ProvenanceTests
    {
        // ══════════════════════════════════════════════════════════════
        //  CONFIDENCE LEVELS (§29)
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Unknown_is_the_default_so_silence_is_never_confidence()
        {
            // A provenance nobody filled in must not read as authoritative.
            var p = new AnswerProvenance();

            Assert.Equal(AnswerConfidence.Unknown, p.Confidence);
        }

        [Fact]
        public void Known_facts_carry_no_hedge()
        {
            // Qualifying a plain fact makes every answer noisier and trains
            // the user to ignore the qualifier that actually matters.
            var p = AnswerProvenance.Known("ProformaInvoiceRepository", "PI-1052");

            Assert.Equal("", p.UserQualifier());
        }

        [Fact]
        public void Calculated_answers_state_the_formula()
        {
            var p = AnswerProvenance.Calculated(
                "DeliveryNoteRepository", "delivered ÷ ordered × 100", "PI-1052");

            Assert.Contains("delivered ÷ ordered × 100", p.UserQualifier());
        }

        [Fact]
        public void Inferred_answers_name_the_assumption_and_invite_correction()
        {
            var p = AnswerProvenance.Inferred(
                "AgentContext", "you meant the proforma currently open");

            string q = p.UserQualifier();

            Assert.Contains("you meant the proforma currently open", q);
            Assert.Contains("wrong", q);   // must invite correction
        }

        [Fact]
        public void Unknown_admits_the_gap_rather_than_hedging_a_number()
        {
            var p = AnswerProvenance.Unknown("no payment table exists");

            Assert.Contains("does not hold enough information", p.UserQualifier());
        }

        [Fact]
        public void Confidence_is_ordered_so_it_can_be_compared()
        {
            Assert.True(AnswerConfidence.Known > AnswerConfidence.Calculated);
            Assert.True(AnswerConfidence.Calculated > AnswerConfidence.Inferred);
            Assert.True(AnswerConfidence.Inferred > AnswerConfidence.Unknown);
        }

        [Fact]
        public void Audit_string_records_source_documents_and_time()
        {
            var p = AnswerProvenance.Calculated(
                "App.Jobs", "a - b", "PI-1052", "JO-2201");

            string audit = p.ToAuditString();

            Assert.Contains("App.Jobs", audit);
            Assert.Contains("PI-1052", audit);
            Assert.Contains("JO-2201", audit);
            Assert.Contains("Calculated", audit);
        }

        // ══════════════════════════════════════════════════════════════
        //  DOCUMENT CHAIN TOOL (§32, §21)
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Chain_tool_is_read_only_and_needs_proforma_view()
        {
            var def = new GetDocumentChainTool().Definition;

            Assert.Equal(ToolRisk.Read, def.Risk);
            Assert.Equal(AppPermissions.ProformaView, def.RequiredPermission);
            Assert.False(def.RequiresConfirmation);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task Chain_tool_refuses_an_empty_reference(string? input)
        {
            // Without a document number there is nothing to follow, and
            // guessing which PI was meant is exactly what §30/§31 forbid.
            var result = await new GetDocumentChainTool().ExecuteAsync(input!);

            Assert.False(result.Success);
        }

        [Fact]
        public void Registry_exposes_the_chain_tool_as_read()
        {
            var tools = AgentToolRegistry.Build().Values;

            var chain = tools.FirstOrDefault(
                t => t.Definition.Name == "get_document_chain");

            Assert.NotNull(chain);
            Assert.Equal(ToolRisk.Read, chain!.Definition.Risk);
        }

        [Fact]
        public void Read_tools_still_outnumber_write_tools()
        {
            // §21: read-only first. If this ever inverts, someone has been
            // adding write capability without discussion.
            var tools = AgentToolRegistry.Build().Values.ToList();

            int reads = tools.Count(t => t.Definition.Risk == ToolRisk.Read);
            int writes = tools.Count(t => t.Definition.Risk == ToolRisk.Write);

            Assert.True(reads > writes);
            Assert.Empty(tools.Where(
                t => t.Definition.Risk == ToolRisk.Destructive));
        }
    }
}
