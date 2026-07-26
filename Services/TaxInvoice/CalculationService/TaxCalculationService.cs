using System;

namespace ProGlassAutomation.Services.TaxInvoice.CalculationService
{
    public class TaxCalculationService
    {
        private const decimal VatRatePercent = 5.0m;

        /// <summary>
        /// Calculates the UAE VAT amount (5%) from a given taxable amount.
        /// </summary>
        public static decimal CalculateVat(decimal taxableAmount)
        {
            return Math.Round(taxableAmount * (VatRatePercent / 100.0m), 2);
        }
    }
}