// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application:
//   Services/Reporting/MoneyRounding.cs (Phase 2, 2026-09-09)
// The WPF file is the source of truth; this port changes no behaviour.
// Policy: money = 2 decimal places (AED fils), half AWAY FROM ZERO — the
// mode the legally-filed invoice/VAT layer has always used, so subtotal +
// VAT + grand reconcile by hand. Never use this for areas/quantities
// (panels are 3 dp, sheet areas 4 dp — see SqmCalculator in Phase 6).
// ═══════════════════════════════════════════════════════════════════════

using System.Globalization;

namespace ProGlass.Saas.Domain.Money;

/// <summary>The single rounding rule for monetary values.</summary>
public static class MoneyRounding
{
    /// <summary>Minor unit of the currency the books are kept in (AED).</summary>
    public const int DecimalPlaces = 2;

    /// <summary>Half away from zero — matches WPF InvoiceRoundTo/VatCalculator.</summary>
    public const MidpointRounding Mode = MidpointRounding.AwayFromZero;

    /// <summary>Rounds a monetary value to <see cref="DecimalPlaces"/>.</summary>
    public static decimal Round(decimal value) =>
        Math.Round(value, DecimalPlaces, Mode);

    /// <summary>
    /// Rounds to an explicit precision (still half away from zero) — for
    /// intermediate rates carried at higher precision before final line
    /// rounding.
    /// </summary>
    public static decimal Round(decimal value, int decimals) =>
        Math.Round(value, decimals, Mode);

    /// <summary>Formats money for storage/API transport: always 2 decimals,
    /// invariant culture, no currency symbol (the caller knows the currency).</summary>
    public static string Format(decimal value) =>
        value.ToString("F" + DecimalPlaces, CultureInfo.InvariantCulture);
}
