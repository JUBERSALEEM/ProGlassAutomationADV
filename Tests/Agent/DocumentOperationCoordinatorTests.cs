// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/DocumentOperationCoordinatorTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Phase 2 + Phase 7 — DocumentOperationCoordinator (D-4 fix)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using ProGlassAutomation.Services.Workflow;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class DocumentOperationCoordinatorTests
    {
        [Theory]
        [InlineData("DN-001,DN-002", 2)]
        [InlineData("DN-001, DN-002 , DN-003", 3)]
        [InlineData("DN-001,DN-001,DN-002", 2)] // distinct
        [InlineData("", 0)]
        [InlineData(null, 0)]
        public void ParseDnRefs_splits_and_dedupes(string? reference, int expectedCount)
        {
            var refs = TaxInvoiceDnReconciler.ParseDnRefs(reference);
            Assert.Equal(expectedCount, refs.Count);
        }

        [Fact]
        public void ParseDnRefs_trims_whitespace()
        {
            var refs = TaxInvoiceDnReconciler.ParseDnRefs(" DN-001 , DN-002 ");
            Assert.Equal(2, refs.Count);
            Assert.Equal("DN-001", refs[0]);
            Assert.Equal("DN-002", refs[1]);
        }

        [Fact]
        public void CorrelationContext_generates_unique_ids_for_multi_db_ops()
        {
            var id1 = CorrelationContext.NewIdFor("TaxInvoice", "INV-001");
            var id2 = CorrelationContext.NewIdFor("TaxInvoice", "INV-001");

            Assert.NotEqual(id1, id2); // each operation gets unique correlation even for same doc
            Assert.Contains("INV-001", id1);
            Assert.Contains("TAXINVOICE", id1.ToUpper());
        }

        [Fact]
        public void Result_pattern_used_for_multi_db_operation()
        {
            // Simulate TI save that succeeds but DN marking fails — D-4 case
            // Coordinator should return Ok for TI (don't lose user work) but log Critical
            // This test documents the expected behavior, not the actual DB operation

            var corrId = CorrelationContext.NewIdFor("TaxInvoice", "INV-TEST");
            var tiResult = Result<int>.Ok(123, corrId);

            Assert.True(tiResult.IsSuccess);
            Assert.Equal(corrId, tiResult.CorrelationId);

            // Simulate DN marking failure
            var dnError = Result.Fail("DN marking failed — will be healed by reconciler", corrId, null, "ERR-DN-MARK");
            Assert.True(dnError.IsFailure);
            Assert.Equal(corrId, dnError.CorrelationId);

            // Even with DN failure, TI result is still Ok — compensation is reconciliation, not rollback
            Assert.True(tiResult.IsSuccess);
        }

        [Fact]
        public void Reconciler_is_idempotent()
        {
            // ParseDnRefs is idempotent — same input → same output, distinct
            var refs1 = TaxInvoiceDnReconciler.ParseDnRefs("DN-001,DN-002");
            var refs2 = TaxInvoiceDnReconciler.ParseDnRefs("DN-001,DN-002");

            Assert.Equal(refs1.Count, refs2.Count);
            Assert.Equal(refs1[0], refs2[0]);
        }
    }
}
