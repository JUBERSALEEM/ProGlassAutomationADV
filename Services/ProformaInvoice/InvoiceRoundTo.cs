using System;

namespace ProGlassAutomation.Services.ProformaInvoice
{
    /// <summary>
    /// Turns a target grand total (VAT inclusive) into the AED discount
    /// that must come off net subtotal. Clerks type 15,500.00 instead of
    /// working the VAT formula by hand.
    /// </summary>
    public static class InvoiceRoundTo
    {
        /// <summary>
        /// Discount in AED applied to <paramref name="netSubtotal"/> so that
        /// (net − discount) + VAT equals <paramref name="targetGrand"/>,
        /// matching Step 7 rounding (2 decimal places).
        /// Returns 0 when the target is at or above the undiscounted total.
        /// </summary>
        public static decimal DiscountOnNet(
            decimal netSubtotal,
            decimal vatPercent,
            bool vatApplicable,
            decimal targetGrand)
        {
            netSubtotal = Math.Round(netSubtotal, 2, MidpointRounding.AwayFromZero);
            targetGrand = Math.Round(targetGrand, 2, MidpointRounding.AwayFromZero);
            if (netSubtotal <= 0m) return 0m;
            if (targetGrand < 0m) targetGrand = 0m;

            decimal maxGrand = GrandFromDiscount(netSubtotal, 0m, vatPercent, vatApplicable);
            if (targetGrand >= maxGrand) return 0m;

            decimal factor = vatApplicable ? 1m + vatPercent / 100m : 1m;
            decimal beforeVat = factor <= 0m
                ? 0m
                : Math.Round(targetGrand / factor, 2, MidpointRounding.AwayFromZero);
            if (beforeVat < 0m) beforeVat = 0m;
            if (beforeVat > netSubtotal) beforeVat = netSubtotal;

            decimal discount = Math.Round(netSubtotal - beforeVat, 2, MidpointRounding.AwayFromZero);

            for (int i = 0; i < 40; i++)
            {
                decimal grand = GrandFromDiscount(netSubtotal, discount, vatPercent, vatApplicable);
                if (grand == targetGrand) break;
                discount += grand > targetGrand ? 0.01m : -0.01m;
                if (discount < 0m) { discount = 0m; break; }
                if (discount > netSubtotal) { discount = netSubtotal; break; }
            }

            return Math.Round(discount, 2, MidpointRounding.AwayFromZero);
        }

        public static decimal GrandFromDiscount(
            decimal netSubtotal,
            decimal discountAed,
            decimal vatPercent,
            bool vatApplicable)
        {
            decimal after = netSubtotal - discountAed;
            if (after < 0m) after = 0m;
            decimal vat = vatApplicable
                ? Math.Round(after * vatPercent / 100m, 2, MidpointRounding.AwayFromZero)
                : 0m;
            return Math.Round(after + vat, 2, MidpointRounding.AwayFromZero);
        }
    }
}
