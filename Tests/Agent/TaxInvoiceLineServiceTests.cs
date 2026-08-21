// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/TaxInvoiceLineServiceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Phase 6b + Phase 7 — TaxInvoiceLineService
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.TaxInvoice;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class TaxInvoiceLineServiceTests
    {
        [Fact]
        public void CalculateTotals_sums_qty_and_sqm()
        {
            var items = new[]
            {
                new TaxInvoiceItem { Qty = 5, TotalSqm = 2.5m },
                new TaxInvoiceItem { Qty = 3, TotalSqm = 1.2m },
            };

            var (qty, sqm) = TaxInvoiceLineService.CalculateTotals(items);

            Assert.Equal(8, qty);
            Assert.Equal(3.7m, sqm);
        }

        [Fact]
        public void CalculateFinancials_computes_vat_and_grand_total()
        {
            var items = new[]
            {
                new TaxInvoiceItem { Qty = 2, UnitPrice = 100m, LineTotal = 200m },
                new TaxInvoiceItem { Qty = 1, UnitPrice = 50m, LineTotal = 50m },
            };

            var (subtotal, vat, grandTotal) = TaxInvoiceLineService.CalculateFinancials(items, discountAmount: 10m, vatRate: 0.05m);

            Assert.Equal(250m, subtotal);
            Assert.Equal(240m, subtotal - 10m);
            Assert.Equal(12m, vat); // 240 * 0.05
            Assert.Equal(252m, grandTotal);
        }

        [Fact]
        public void BuildInvoiceNumber_strips_PI_prefix()
        {
            var inv1 = TaxInvoiceLineService.BuildInvoiceNumber("PI-2026-001");
            Assert.Equal("INV-2026-001", inv1);

            var inv2 = TaxInvoiceLineService.BuildInvoiceNumber("PI-2026-001", "A");
            Assert.Equal("INV-2026-001-A", inv2);

            var inv3 = TaxInvoiceLineService.BuildInvoiceNumber("2026-001", "B");
            Assert.Equal("INV-2026-001-B", inv3);
        }

        [Fact]
        public void ValidateInvoice_fails_when_no_items()
        {
            var invoice = new SavedTaxInvoice
            {
                InvoiceNumber = "INV-001",
                ProformaId = "PI-001",
                Items = new System.Collections.Generic.List<TaxInvoiceItem>(),
                GrandTotal = 100m
            };

            var result = TaxInvoiceLineService.ValidateInvoice(invoice, "CORR-TEST");
            Assert.True(result.IsFailure);
            Assert.Contains("line item", result.Error.ToLower());
        }

        [Fact]
        public void ValidateInvoice_fails_when_grand_total_zero()
        {
            var invoice = new SavedTaxInvoice
            {
                InvoiceNumber = "INV-001",
                ProformaId = "PI-001",
                Items = new System.Collections.Generic.List<TaxInvoiceItem> { new TaxInvoiceItem { Qty = 1, LineTotal = 0 } },
                GrandTotal = 0m
            };

            var result = TaxInvoiceLineService.ValidateInvoice(invoice, "CORR-TEST");
            Assert.True(result.IsFailure);
        }

        [Fact]
        public void ValidateInvoice_succeeds_on_valid()
        {
            var invoice = new SavedTaxInvoice
            {
                InvoiceNumber = "INV-001",
                ProformaId = "PI-001",
                Items = new System.Collections.Generic.List<TaxInvoiceItem> { new TaxInvoiceItem { Qty = 5, LineTotal = 100m } },
                GrandTotal = 105m
            };

            var result = TaxInvoiceLineService.ValidateInvoice(invoice, "CORR-TEST");
            Assert.True(result.IsSuccess);
        }

        [Theory]
        [InlineData("Draft", "Issued", true)]
        [InlineData("Issued", "Paid", true)]
        [InlineData("Paid", "Draft", false)]
        [InlineData("Draft", "Draft", true)]
        public void ValidateStatusTransition_uses_transition_table(string from, string to, bool shouldBeValid)
        {
            var result = TaxInvoiceLineService.ValidateStatusTransition(from, to, "CORR-TEST");

            if (shouldBeValid)
                Assert.True(result.IsSuccess);
            else
                Assert.True(result.IsFailure);
        }

        [Fact]
        public void CalculateFamily_sums_issued_only()
        {
            var members = new[]
            {
                new SavedTaxInvoice { InvoiceNumber = "INV-001-A", Status = "Issued", GrandTotal = 100m },
                new SavedTaxInvoice { InvoiceNumber = "INV-001-B", Status = "Draft", GrandTotal = 50m },
                new SavedTaxInvoice { InvoiceNumber = "INV-001-C", Status = "Paid", GrandTotal = 200m },
            };

            var (count, totalInvoiced, balance) = TaxInvoiceLineService.CalculateFamily(members, proformaTotal: 500m);

            Assert.Equal(3, count);
            Assert.Equal(300m, totalInvoiced); // only Issued + Paid
            Assert.Equal(200m, balance); // 500 - 300
        }
    }
}
