// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Services/ProformaInvoice/SpecGroupService.cs (Phase 6 extraction,
//   2026-08-21) — row validation + phantom-row rules
//   ViewModels/ProformaInvoice/Step5DataGrid/GlassItemRow.cs —
//     RecalculateAll / SetAllSilent (dual W/H measurement math)
// The WPF files are the source of truth; behaviour preserved verbatim.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Money;
using ProGlass.Saas.Domain.Pricing;

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>
/// Step 5 grid row validation, WPF-verbatim (SpecGroupService.ValidateRow).
/// A row is OK, failed with an operator-readable message, or a PHANTOM
/// (blank ref + zero dimensions) — phantoms are allowed and filtered from
/// every aggregation.
/// </summary>
public static class SpecGroupValidation
{
    public const int MaxDimensionMm = 10_000;
    public const int MaxQty = 10_000;

    public static bool ValidateRow(int w, int h, int qty, string glassRef)
    {
        // Phantom row — allowed, will be filtered
        if (string.IsNullOrWhiteSpace(glassRef) && w == 0 && h == 0)
            return true;

        if (w <= 0 || h <= 0)
            return false;

        if (qty <= 0)
            return false;

        if (w > MaxDimensionMm || h > MaxDimensionMm)
            return false;

        if (qty > MaxQty)
            return false;

        return true;
    }

    public static string? ValidateRowMessage(int w, int h, int qty, string glassRef)
    {
        if (string.IsNullOrWhiteSpace(glassRef) && w == 0 && h == 0)
            return null; // phantom row — allowed, will be filtered

        if (w <= 0 || h <= 0)
            return $"Dimensions must be >0 for glass ref '{glassRef}'";

        if (qty <= 0)
            return $"Qty must be >0 for glass ref '{glassRef}'";

        if (w > MaxDimensionMm || h > MaxDimensionMm)
            return $"Dimensions too large for '{glassRef}' — max {MaxDimensionMm}mm";

        if (qty > MaxQty)
            return $"Qty too large for '{glassRef}' — max {MaxQty}";

        return null;
    }

    /// <summary>WPF IsPhantomRow, verbatim.</summary>
    public static bool IsPhantomRow(string? glassRef, int w, int h, int qty)
    {
        if (qty <= 0) return true;
        if (string.IsNullOrWhiteSpace(glassRef) && w == 0 && h == 0) return true;
        return false;
    }
}

/// <summary>
/// Step 5 grid row math, WPF-verbatim (GlassItemRow.RecalculateAll).
/// A row carries TWO measurements (W1/H1 primary, W2/H2 secondary — DGU
/// panes differ), and the money line uses the PRIMARY measurement:
///
///   Sqm1     = SqmCalculator.CalculatePanel(W1, H1)   (3 dp, 0.5 m² floor)
///   TotalSqm = round3(Sqm1 × Qty)
///   Sqm2/TotalSqm2 = same for W2/H2
///   Lm1      = round3(((W1 + H1) / 1000) × PerimeterFactor × Qty)
///   Lm2      = same for W2/H2
///   Value    = MoneyRounding.Round(Sqm1 × Qty × Rate)
/// </summary>
public static record SpecGroupRowMath(
    decimal Sqm1, decimal TotalSqm,
    decimal Sqm2, decimal TotalSqm2,
    decimal Lm1, decimal Lm2,
    decimal Value)
{
    /// <summary>WPF default _perimeterFactor = 2 (both sides of the pane).</summary>
    public const int DefaultPerimeterFactor = 2;

    public static SpecGroupRowMath Calculate(
        int w1, int h1, int w2, int h2, int qty, int perimeterFactor, decimal rate)
    {
        var sqm1 = w1 > 0 && h1 > 0 ? SqmCalculator.CalculatePanel(w1, h1) : 0m;
        var totalSqm = Math.Round(sqm1 * qty, 3);

        var sqm2 = w2 > 0 && h2 > 0 ? SqmCalculator.CalculatePanel(w2, h2) : 0m;
        var totalSqm2 = Math.Round(sqm2 * qty, 3);

        var lm1 = w1 > 0 && h1 > 0
            ? Math.Round(((decimal)w1 + h1) / 1000m * perimeterFactor * qty, 3)
            : 0m;
        var lm2 = w2 > 0 && h2 > 0
            ? Math.Round(((decimal)w2 + h2) / 1000m * perimeterFactor * qty, 3)
            : 0m;

        var value = MoneyRounding.Round(sqm1 * qty * rate);

        return new SpecGroupRowMath(sqm1, totalSqm, sqm2, totalSqm2, lm1, lm2, value);
    }
}

/// <summary>
/// Spec-group aggregation rule, WPF-verbatim in spirit
/// (SpecGroupService.AggregateRows — phantom rows are skipped before the
/// sum; Step 7 summary: GlassMaterialsSubtotal = the grid's GrandTotalValue
/// = Σ row values). The sums themselves run in the service, over the
/// stored items, because qty lives on the item row.
/// </summary>
public static class SpecGroupAggregation
{
    /// <summary>Total SQM at the WPF grid precision (3 dp).</summary>
    public static decimal TotalSqm(IEnumerable<decimal> rowTotalSqms) =>
        Math.Round(rowTotalSqms.Sum(), 3);

    /// <summary>Step 7 GlassMaterialsSubtotal = Σ MoneyRounding(row value).</summary>
    public static decimal GlassMaterialsSubtotal(IEnumerable<decimal> rowValues) =>
        MoneyRounding.Round(rowValues.Sum());
}
