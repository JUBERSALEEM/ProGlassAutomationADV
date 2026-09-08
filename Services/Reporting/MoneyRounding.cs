// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/MoneyRounding.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Services.Reporting
//
// PURPOSE
// ───────
// One declaration of how this product rounds money. See
// docs/MONEY_ROUNDING_POLICY.md for the policy this implements and the
// reasoning behind each choice.
//
// WHY IT EXISTS (Phase 2, 2026-09-09)
// ──────────────────────────────────
// The money pipeline had two rounding policies running at once:
//
//   • Math.Round(x, 2) — the two-argument overload, whose default is
//     MidpointRounding.ToEven (banker's rounding). Used throughout the
//     pricing engine and the profitability calculations.
//   • MidpointRounding.AwayFromZero — used by InvoiceRoundTo and by
//     VatCalculator.
//
// So the pricing layer rounded a half-fil toward the even digit and the
// invoice layer rounded it away from zero. On any invoice whose unrounded
// values land exactly on a half, subtotal + vat does not equal grand —
// reproducibly, on the same invoices, every time.
//
// Neither choice is wrong in isolation. Having both in one pipeline is.
//
// WHAT THIS DOES NOT DO
// ─────────────────────
// It does not round quantities, areas, percentages or display values. Those
// have their own rules and their own precisions — square metres are rounded to
// 3 or 4 decimal places in this codebase, and rounding one of those to a
// currency precision would silently corrupt a measurement. Use this class only
// where the value is money.
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Services.Reporting
{
    /// <summary>
    /// The single rounding rule for monetary values.
    /// </summary>
    public static class MoneyRounding
    {
        /// <summary>
        /// Decimal places for money. Two for AED (fils). Becomes a function of
        /// the active currency when tax and currency configuration lands
        /// (Phase 5) — JPY has zero, BHD has three.
        /// </summary>
        public const int DecimalPlaces = 2;

        /// <summary>
        /// Half away from zero.
        ///
        /// <para>
        /// Chosen over <see cref="MidpointRounding.ToEven"/> because it is what
        /// <c>InvoiceRoundTo</c> and <c>VatCalculator</c> already used, so
        /// routing them through here changes no behaviour at all — the invoice
        /// and VAT layer is the part of the pipeline whose output is legally
        /// filed, and it is the one that stays exactly as it was.
        /// </para>
        ///
        /// <para>
        /// It is also what a human reconciling an invoice by hand expects: a
        /// half rounds up regardless of the digit before it. Banker's rounding
        /// is statistically fairer over a large population, but an invoice is
        /// read one line at a time by someone checking the arithmetic.
        /// </para>
        /// </summary>
        public const MidpointRounding Mode = MidpointRounding.AwayFromZero;

        /// <summary>
        /// Rounds a monetary value to <see cref="DecimalPlaces"/> using
        /// <see cref="Mode"/>.
        /// </summary>
        public static decimal Round(decimal value) =>
            Math.Round(value, DecimalPlaces, Mode);

        /// <summary>
        /// Rounds to an explicit precision, still using <see cref="Mode"/>.
        /// For the rare monetary figure that is not expressed in the currency's
        /// minor unit — an intermediate rate carried at higher precision before
        /// the final line rounding.
        /// </summary>
        public static decimal Round(decimal value, int decimals) =>
            Math.Round(value, decimals, Mode);
    }
}
