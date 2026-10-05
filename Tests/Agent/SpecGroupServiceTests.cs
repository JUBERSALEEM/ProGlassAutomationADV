// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/SpecGroupServiceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Phase 6b + Phase 7 — SpecGroupService
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.ProformaInvoice;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class SpecGroupServiceTests
    {
        [Theory]
        [InlineData(1000, 1000, 1.0)] // 1x1m = 1 sqm
        [InlineData(500, 500, 0.5)] // 0.25 sqm but min 0.5
        [InlineData(100, 100, 0.5)] // tiny → min 0.5
        [InlineData(0, 1000, 0)] // zero dimension → 0
        public void CalculatePanelSqm_respects_min_floor(int w, int h, decimal expected)
        {
            var sqm = SpecGroupService.CalculatePanelSqm(w, h);
            Assert.Equal(expected, sqm);
        }

        [Fact]
        public void CalculateTotalSqm_multiplies_by_qty()
        {
            var total = SpecGroupService.CalculateTotalSqm(1000, 1000, 5);
            Assert.Equal(5m, total);

            total = SpecGroupService.CalculateTotalSqm(500, 500, 10);
            Assert.Equal(5m, total); // 0.5 * 10
        }

        [Fact]
        public void IsPhantomRow_detects_empty()
        {
            Assert.True(SpecGroupService.IsPhantomRow("", 0, 0, 0));
            Assert.True(SpecGroupService.IsPhantomRow(null, 0, 0, 5)); // blank ref + zero dims
            Assert.True(SpecGroupService.IsPhantomRow("G-001", 1000, 1000, 0)); // qty 0
            Assert.False(SpecGroupService.IsPhantomRow("G-001", 1000, 1000, 5));
        }

        [Theory]
        [InlineData(1000, 1000, 5, "G-001", true)]
        [InlineData(0, 1000, 5, "G-001", false)]
        [InlineData(1000, 1000, 0, "G-001", false)]
        [InlineData(100, 100, 5, "", true)] // phantom? No, glass ref blank but dims non-zero and qty>0 → not phantom per IsPhantom, but Validate should allow? Actually blank ref + zero dims is phantom, blank ref + non-zero dims is not phantom per IsPhantom? Let's check: IsPhantom checks qty<=0 OR (blank ref AND w==0 AND h==0). So blank ref + non-zero dims with qty>0 is NOT phantom.
        public void ValidateRow_checks_dimensions_and_qty(int w, int h, int qty, string glassRef, bool shouldBeValid)
        {
            var result = SpecGroupService.ValidateRow(w, h, qty, glassRef, "CORR-TEST");

            if (shouldBeValid)
                Assert.True(result.IsSuccess);
            else
                Assert.True(result.IsFailure);
        }

        [Fact]
        public void ValidateRow_fails_on_too_large()
        {
            var result = SpecGroupService.ValidateRow(20000, 1000, 5, "G-001", "CORR-TEST");
            Assert.True(result.IsFailure);
            Assert.Contains("too large", result.Error.ToLower());
        }

        [Fact]
        public void FormatGlassRef_keeps_existing_spec_prefix()
        {
            var formatted = SpecGroupService.FormatGlassRef(1, 1, "Spec 1 - 6mm Clear");
            Assert.Equal("Spec 1 - 6mm Clear", formatted); // already has prefix

            formatted = SpecGroupService.FormatGlassRef(2, 5, "G-002");
            Assert.Equal("G-002", formatted); // no auto prefix for glass ref (keeps as is)
        }

        [Fact]
        public void AggregateRows_sums_non_phantom()
        {
            var rows = new[]
            {
                new { Qty = 5, Sqm = 2.5m, IsPhantom = false },
                new { Qty = 3, Sqm = 1.0m, IsPhantom = false },
                new { Qty = 0, Sqm = 0m, IsPhantom = true },
            };

            var (totalQty, totalSqm, count) = SpecGroupService.AggregateRows(
                rows,
                r => r.Qty,
                r => r.Sqm,
                r => r.IsPhantom);

            Assert.Equal(8, totalQty);
            Assert.Equal(3.5m, totalSqm);
            Assert.Equal(2, count);
        }
    }
}
