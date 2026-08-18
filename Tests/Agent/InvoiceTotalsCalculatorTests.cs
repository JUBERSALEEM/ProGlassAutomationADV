// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/InvoiceTotalsCalculatorTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the shared tax-invoice financial-total rule (UAE 5% VAT) used by
// both TaxInvoiceDetailViewModel.RecalculateFinancials and TaxInvoiceMapper.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.TaxInvoice;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class InvoiceTotalsCalculatorTests
    {
        [Fact]
        public void Compute_with_no_discount_applies_5pct_vat()
        {
            var t = InvoiceTotalsCalculator.Compute(1000m, 0m);

            Assert.Equal(0m, t.DiscountAmount);
            Assert.Equal(1000m, t.AfterDiscount);
            Assert.Equal(50m, t.VatAmount);   // 5% of 1000
            Assert.Equal(1050m, t.GrandTotal);
        }

        [Fact]
        public void Compute_applies_discount_before_vat()
        {
            var t = InvoiceTotalsCalculator.Compute(1000m, 10m);

            Assert.Equal(100m, t.DiscountAmount);
            Assert.Equal(900m, t.AfterDiscount);
            Assert.Equal(45m, t.VatAmount);   // 5% of 900
            Assert.Equal(945m, t.GrandTotal);
        }

        [Fact]
        public void Compute_rounds_discount_and_vat_to_2dp()
        {
            // 333.33 * 33.33% = 111.099... -> rounds to 111.10
            var t = InvoiceTotalsCalculator.Compute(333.33m, 33.33m);
            Assert.Equal(111.10m, t.DiscountAmount);
            Assert.Equal(222.23m, t.AfterDiscount); // 333.33 - 111.10
            Assert.Equal(11.11m, t.VatAmount);      // 5% of 222.23 = 11.1115 -> 11.11
            Assert.Equal(233.34m, t.GrandTotal);    // 222.23 + 11.11
        }
    }
}
