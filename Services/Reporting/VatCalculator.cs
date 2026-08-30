// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/VatCalculator.cs
// PROJECT: ProGlassAutomation
// MODULE: VAT Calculation Engine
// PURPOSE: Centralized, accurate, and consistent VAT calculations for UAE FTA compliance
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Services.Reporting
{
    /// <summary>
    /// Centralized VAT calculation engine for UAE 5% VAT compliance.
    /// Ensures consistent rounding and validation across all VAT operations.
    /// </summary>
    public static class VatCalculator
    {
        /// <summary>
        /// UAE VAT rate (5%)
        /// </summary>
        public const decimal VAT_RATE_PERCENT = 5.0m;
        
        /// <summary>
        /// VAT rate as decimal (0.05)
        /// </summary>
        public const decimal VAT_RATE_DECIMAL = 0.05m;
        
        /// <summary>
        /// VAT multiplier for gross calculation (1.05)
        /// </summary>
        public const decimal VAT_MULTIPLIER = 1.05m;
        
        /// <summary>
        /// Number of decimal places for rounding
        /// </summary>
        private const int DECIMAL_PLACES = 2;
        
        /// <summary>
        /// Rounding strategy (banker's rounding for financial accuracy)
        /// </summary>
        private const MidpointRounding ROUNDING = MidpointRounding.AwayFromZero;
        
        /// <summary>
        /// Default tolerance for validation (1 fils = 0.01 AED)
        /// </summary>
        public const decimal DEFAULT_TOLERANCE = 0.01m;

        /// <summary>
        /// Rounds a decimal value to 2 decimal places using AwayFromZero rounding
        /// </summary>
        /// <param name="value">Value to round</param>
        /// <returns>Rounded value</returns>
        public static decimal Round(decimal value) 
            => Math.Round(value, DECIMAL_PLACES, ROUNDING);

        /// <summary>
        /// Calculates subtotal from grand total and VAT amount
        /// Formula: Subtotal = GrandTotal - VatAmount
        /// </summary>
        /// <param name="grandTotalVatInclusive">Grand total including VAT</param>
        /// <param name="vatAmount">VAT amount</param>
        /// <returns>Subtotal before VAT</returns>
        public static decimal CalculateSubtotal(decimal grandTotalVatInclusive, decimal vatAmount)
            => Round(grandTotalVatInclusive - vatAmount);

        /// <summary>
        /// Calculates VAT from gross amount (back-calculation)
        /// Formula: VAT = Gross - (Gross / 1.05)
        /// </summary>
        /// <param name="grandTotalVatInclusive">Grand total including VAT</param>
        /// <returns>Calculated VAT amount</returns>
        public static decimal CalculateVatFromGross(decimal grandTotalVatInclusive)
        {
            var subtotal = Round(grandTotalVatInclusive / VAT_MULTIPLIER);
            return Round(grandTotalVatInclusive - subtotal);
        }

        /// <summary>
        /// Calculates VAT from subtotal
        /// Formula: VAT = Subtotal * 0.05
        /// </summary>
        /// <param name="subtotal">Subtotal before VAT</param>
        /// <returns>Calculated VAT amount</returns>
        public static decimal CalculateVatFromSubtotal(decimal subtotal)
            => Round(subtotal * VAT_RATE_DECIMAL);

        /// <summary>
        /// Calculates gross from subtotal
        /// Formula: Gross = Subtotal * 1.05
        /// </summary>
        /// <param name="subtotal">Subtotal before VAT</param>
        /// <returns>Grand total including VAT</returns>
        public static decimal CalculateGrossFromSubtotal(decimal subtotal)
            => Round(subtotal * VAT_MULTIPLIER);

        /// <summary>
        /// Validates if VAT amount matches expected calculation
        /// </summary>
        /// <param name="subtotal">Subtotal before VAT</param>
        /// <param name="vatAmount">Actual VAT amount</param>
        /// <param name="tolerance">Allowed tolerance (default: 0.01)</param>
        /// <returns>True if VAT is valid</returns>
        public static bool ValidateVat(decimal subtotal, decimal vatAmount, decimal tolerance = DEFAULT_TOLERANCE)
        {
            var expectedVat = CalculateVatFromSubtotal(subtotal);
            return Math.Abs(expectedVat - vatAmount) <= tolerance;
        }

        /// <summary>
        /// Validates if VAT amount matches gross calculation
        /// </summary>
        /// <param name="grandTotalVatInclusive">Grand total including VAT</param>
        /// <param name="vatAmount">Actual VAT amount</param>
        /// <param name="tolerance">Allowed tolerance (default: 0.01)</param>
        /// <returns>True if VAT is valid</returns>
        public static bool ValidateVatFromGross(decimal grandTotalVatInclusive, decimal vatAmount, decimal tolerance = DEFAULT_TOLERANCE)
        {
            var expectedVat = CalculateVatFromGross(grandTotalVatInclusive);
            return Math.Abs(expectedVat - vatAmount) <= tolerance;
        }

        /// <summary>
        /// Validates invoice data for VAT submission
        /// </summary>
        /// <param name="grandTotalVatInclusive">Grand total including VAT</param>
        /// <param name="vatAmount">VAT amount</param>
        /// <param name="issueDate">Invoice issue date</param>
        /// <returns>Validation result with warnings</returns>
        public static VatValidationResult ValidateInvoice(decimal grandTotalVatInclusive, decimal vatAmount, DateTime issueDate)
        {
            var result = new VatValidationResult { IsValid = true };

            // Check for negative values
            if (vatAmount < 0)
            {
                result.IsValid = false;
                result.Errors.Add("VAT amount cannot be negative");
            }

            if (grandTotalVatInclusive < 0)
            {
                result.IsValid = false;
                result.Errors.Add("Grand total cannot be negative");
            }

            // Check for future dates (allow 1 day tolerance)
            if (issueDate > DateTime.Now.AddDays(1))
            {
                result.Warnings.Add($"Invoice date is in the future: {issueDate:yyyy-MM-dd}");
            }

            // Validate VAT calculation if both values are present
            if (vatAmount > 0 && grandTotalVatInclusive > 0)
            {
                var calculatedSubtotal = CalculateSubtotal(grandTotalVatInclusive, vatAmount);
                if (!ValidateVat(calculatedSubtotal, vatAmount))
                {
                    var expectedVat = CalculateVatFromGross(grandTotalVatInclusive);
                    result.Warnings.Add(
                        $"VAT mismatch: Stored={vatAmount:N2}, Calculated={expectedVat:N2}, " +
                        $"Difference={Math.Abs(expectedVat - vatAmount):N2}");
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Result of VAT validation
    /// </summary>
    public class VatValidationResult
    {
        /// <summary>
        /// Whether the invoice is valid (no errors)
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Critical errors that prevent processing
        /// </summary>
        public System.Collections.Generic.List<string> Errors { get; } = new();

        /// <summary>
        /// Non-critical warnings
        /// </summary>
        public System.Collections.Generic.List<string> Warnings { get; } = new();

        /// <summary>
        /// Whether there are any warnings
        /// </summary>
        public bool HasWarnings => Warnings.Count > 0;

        /// <summary>
        /// Whether there are any errors
        /// </summary>
        public bool HasErrors => Errors.Count > 0;
    }
}
