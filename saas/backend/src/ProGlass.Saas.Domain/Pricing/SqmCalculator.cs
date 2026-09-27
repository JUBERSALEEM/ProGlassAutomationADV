// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Utilities/SqmCalculator.cs — Ship 8c-2 (Chat 11)
// The WPF file is the source of truth; this port changes no behaviour.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Saas.Domain.Pricing;

/// <summary>
/// Single source of truth for SQM (square meter) calculations.
///
/// Business rule (UAE glass industry standard):
///   Panel SQM = (Width mm × Height mm) / 1,000,000
///   Minimum billable SQM per panel = 0.5 m²
///   If actual SQM &lt; 0.5, bill as 0.5.
/// </summary>
public static class SqmCalculator
{
    /// <summary>Minimum billable SQM per panel (UAE industry standard).</summary>
    public const decimal MinimumSqmPerPanel = 0.5m;

    /// <summary>
    /// Calculate SQM for a single panel. Returns 0 if dimensions are
    /// invalid. Applies the 0.5 minimum floor. Rounded to 3 decimal places.
    /// </summary>
    public static decimal CalculatePanel(int widthMm, int heightMm)
    {
        if (widthMm <= 0 || heightMm <= 0) return 0m;

        var actual = Math.Round((decimal)widthMm * heightMm / 1_000_000m, 3);
        return actual < MinimumSqmPerPanel ? MinimumSqmPerPanel : actual;
    }

    /// <summary>
    /// Calculate total SQM for a quantity of identical panels:
    /// SQM per panel × quantity, rounded to 3 dp.
    /// </summary>
    public static decimal CalculateTotal(int widthMm, int heightMm, int quantity)
    {
        if (quantity <= 0) return 0m;
        return Math.Round(CalculatePanel(widthMm, heightMm) * quantity, 3);
    }

    /// <summary>
    /// Calculate the PHYSICAL area of a stock sheet in square meters
    /// (Width mm × Height mm / 1,000,000). Unlike panel SQM, a stock sheet
    /// has NO billable minimum floor — it is the raw physical area used for
    /// inventory value/cost. Rounded to 4 decimal places (matches the
    /// existing sheet-store convention). Returns 0 for invalid dimensions.
    /// </summary>
    public static decimal CalculateSheetArea(int widthMm, int heightMm)
    {
        if (widthMm <= 0 || heightMm <= 0) return 0m;
        return Math.Round((decimal)widthMm * heightMm / 1_000_000m, 4);
    }

    /// <summary>
    /// Physical area of ONE panel face in square meters, with NO billable
    /// minimum floor (Width mm × Height mm / 1,000,000). Used for cut-area
    /// aggregates (e.g. Step 9 P&amp;L, delivery analytics) where the true
    /// material area is required, not the billed 0.5 m² floor.
    /// Returns 0 for invalid dimensions. Callers sum this over panels as
    /// needed; this returns the single-face value (not rounded) so an
    /// aggregate stays exact before any final rounding.
    /// WPF: Utilities/SqmCalculator.CalculatePhysicalPanelArea (Phase 20).
    /// </summary>
    public static decimal CalculatePhysicalPanelArea(int widthMm, int heightMm)
    {
        if (widthMm <= 0 || heightMm <= 0) return 0m;
        return (decimal)widthMm * heightMm / 1_000_000m;
    }
}
