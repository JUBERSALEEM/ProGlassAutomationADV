// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/TaxInvoiceDnReconcilerTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the pure DN-reference parsing used by the Phase-2 cross-database
// reconciler (TaxInvoiceDnReconciler.ParseDnRefs). The reconciler's full
// sweep needs a database, but the reference parsing is stateless and
// testable.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.TaxInvoice.Repository;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class TaxInvoiceDnReconcilerTests
    {
        [Fact]
        public void Single_dn_reference_parses()
        {
            var ids = TaxInvoiceDnReconciler.ParseDnRefs("DN-2026-001-01");
            Assert.Equal(new[] { "DN-2026-001-01" }, ids);
        }

        [Fact]
        public void Multiple_dn_references_parse()
        {
            var ids = TaxInvoiceDnReconciler.ParseDnRefs(
                "DN-2026-001-01,DN-2026-001-02,DN-2026-001-03");
            Assert.Equal(3, ids.Count);
            Assert.Contains("DN-2026-001-02", ids);
        }

        [Fact]
        public void Whitespace_and_blank_entries_are_tolerated()
        {
            var ids = TaxInvoiceDnReconciler.ParseDnRefs(
                " DN-2026-001-01 , , DN-2026-001-02 ");
            Assert.Equal(new[] { "DN-2026-001-01", "DN-2026-001-02" }, ids);
        }

        [Fact]
        public void Duplicate_references_are_deduplicated()
        {
            var ids = TaxInvoiceDnReconciler.ParseDnRefs(
                "DN-2026-001-01,DN-2026-001-01");
            Assert.Single(ids);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Empty_reference_returns_empty(string? reference)
        {
            Assert.Empty(TaxInvoiceDnReconciler.ParseDnRefs(reference));
        }
    }
}
