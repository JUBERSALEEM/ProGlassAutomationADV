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
        /// Default tolerance for validation (1 fils = 0.01 AED)
        /// </summary>
        public const decimal DEFAULT_TOLERANCE = 0.01m;

        /// <summary>
        /// Rounds a monetary value using the product's single rounding rule.
        ///
        /// Delegates to <see cref="MoneyRounding"/> rather than rounding inline
        /// so this class cannot drift from the rest of the money pipeline.
        /// Kept as <c>VatCalculator.Round</c> — the public name every existing
        /// call site uses — so routing it here changed no caller.
        /// </summary>
        /// <param name="value">Value to round</param>
        /// <returns>Rounded value</returns>
        public static decimal Round(decimal value)
            => MoneyRounding.Round(value);

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
        /// Validates invoice data for VAT submission.
        ///
        /// <para>
        /// PHASE 3 (2026-09-09) — this method could not fail. <c>IsValid</c> was
        /// initialised to <c>true</c> and nothing ever set it to <c>false</c>, and
        /// <c>Errors</c> was never populated, so every problem below was recorded
        /// as a warning and the caller's <c>if (!validation.IsValid)</c> gate was
        /// unreachable. <c>ReportingDataService.Vat</c> consequently printed
        /// "(filtered from N, 0 invalid)" on every filing.
        /// </para>
        ///
        /// <para>
        /// Three outcomes, deliberately separated:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>
        ///     <b>Reject</b> — the arithmetic does not reconcile, or the figures
        ///     are mutually impossible. Excluded from the filing and named to the
        ///     operator. A tax return that silently omits rows is worse than one
        ///     that refuses to generate, so the caller must report which rows and
        ///     why.
        ///   </description></item>
        ///   <item><description>
        ///     <b>Warn</b> — legitimate but worth a human eye: negative amounts
        ///     (credit notes and corrections), a zero-VAT invoice, a future date.
        ///     Included in the filing.
        ///   </description></item>
        ///   <item><description><b>Pass</b> — everything else.</description></item>
        /// </list>
        ///
        /// <para>
        /// A positive total with zero VAT is a WARNING, not a rejection. Zero-rated
        /// and exempt supplies legitimately carry no VAT, and this calculator has
        /// no notion of tax treatment yet — rejecting here would silently drop
        /// valid zero-rated invoices from the filing. Revisit when tax treatments
        /// land (Phase 5).
        /// </para>
        /// </summary>
        /// <param name="grandTotalVatInclusive">Grand total including VAT</param>
        /// <param name="vatAmount">VAT amount</param>
        /// <param name="issueDate">Invoice issue date</param>
        /// <returns>Validation result; <see cref="VatValidationResult.IsValid"/>
        /// is false when <see cref="VatValidationResult.Errors"/> is non-empty.</returns>
        public static VatValidationResult ValidateInvoice(decimal grandTotalVatInclusive, decimal vatAmount, DateTime issueDate)
        {
            var result = new VatValidationResult();

            // ── Negative amounts: legitimate for credit notes ──────────
            // Warn, do not reject. A corrections workflow depends on these
            // reaching the filing.
            if (vatAmount < 0)
                result.Warnings.Add($"Negative VAT amount: {vatAmount:N2}");

            if (grandTotalVatInclusive < 0)
                result.Warnings.Add($"Negative grand total: {grandTotalVatInclusive:N2}");

            // ── Both zero: valid but empty, nothing to report ──────────
            if (vatAmount == 0 && grandTotalVatInclusive == 0)
                return result;

            // ── Future dates: warn, allow a day of clock skew ──────────
            if (issueDate > DateTime.Now.AddDays(1))
                result.Warnings.Add($"Invoice date is in the future: {issueDate:yyyy-MM-dd}");

            // ── Mutually impossible figures: REJECT ────────────────────
            if (vatAmount > 0 && grandTotalVatInclusive == 0)
            {
                result.Errors.Add(
                    $"VAT of {vatAmount:N2} is charged on a zero grand total.");
                return result;
            }

            if (grandTotalVatInclusive > 0 && vatAmount == 0)
            {
                // Zero-rated / exempt supplies look identical to this check,
                // so this is a warning until tax treatments exist.
                result.Warnings.Add(
                    $"Grand total {grandTotalVatInclusive:N2} carries no VAT " +
                    "(zero-rated or exempt supply, or a missing VAT amount).");
                return result;
            }

            // ── Reconciliation: REJECT on failure ──────────────────────
            // This is the check that matters. Stored VAT that does not
            // reconcile with the stored gross means one of the two figures on
            // a tax filing is wrong.
            var calculatedSubtotal = CalculateSubtotal(grandTotalVatInclusive, vatAmount);
            if (!ValidateVat(calculatedSubtotal, vatAmount))
            {
                var expectedVat = CalculateVatFromGross(grandTotalVatInclusive);
                result.Errors.Add(
                    $"VAT does not reconcile: stored={vatAmount:N2}, " +
                    $"expected={expectedVat:N2} for gross {grandTotalVatInclusive:N2} " +
                    $"(difference {Math.Abs(expectedVat - vatAmount):N2}, " +
                    $"tolerance {DEFAULT_TOLERANCE:N2}).");
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
        /// Whether the invoice may be included in the filing.
        ///
        /// Derived, not stored. It was previously a settable bool initialised to
        /// <c>true</c> and never assigned anywhere, which is exactly how the
        /// validation gate became unreachable — a stored flag can drift out of
        /// sync with the list it is supposed to summarise, and this one did.
        /// Deriving it makes the two impossible to disagree.
        /// </summary>
        public bool IsValid => Errors.Count == 0;

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
