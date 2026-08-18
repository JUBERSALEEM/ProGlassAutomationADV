// ═══════════════════════════════════════════════════════════════════════
//  Services/TaxInvoice/InvoiceTotalsCalculator.cs
//
//  Single source of truth for Tax Invoice financial totals:
//
//      DiscountAmount = round(Subtotal × DiscountPercent / 100)
//      AfterDiscount  = Subtotal − DiscountAmount
//      VatAmount      = round(AfterDiscount × VAT_RATE)    // UAE 5%
//      GrandTotal     = AfterDiscount + VatAmount
//
//  Previously duplicated in TaxInvoiceDetailViewModel.RecalculateFinancials
//  and TaxInvoiceMapper. Centralised here (Phase 11/12) so the UAE VAT
//  business rule lives in one place and is trivially unit-testable.
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Services.TaxInvoice
{
    /// <summary>
    /// Pure financial-total computation for tax invoices.
    /// </summary>
    public static class InvoiceTotalsCalculator
    {
        /// <summary>UAE standard VAT rate (5%).</summary>
        public const decimal VatRate = 0.05m;

        /// <summary>Computed tax-invoice financial totals.</summary>
        public readonly record struct Totals(
            decimal DiscountAmount,
            decimal AfterDiscount,
            decimal VatAmount,
            decimal GrandTotal);

        /// <summary>
        /// Computes discount, VAT and grand total from a subtotal and a
        /// discount percentage. Rounding matches the previous behaviour:
        /// discount and VAT are rounded to 2dp.
        /// </summary>
        public static Totals Compute(decimal subtotal, decimal discountPercent)
        {
            decimal discountAmt = Math.Round(
                subtotal * (discountPercent / 100m), 2);
            decimal afterDiscount = subtotal - discountAmt;
            decimal vatAmount = Math.Round(afterDiscount * VatRate, 2);
            decimal grandTotal = afterDiscount + vatAmount;

            return new Totals(discountAmt, afterDiscount, vatAmount, grandTotal);
        }
    }
}
