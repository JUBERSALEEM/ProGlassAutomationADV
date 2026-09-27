using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Proforma;

// ═══════════════════════════════════════════════════════════════════════
//  Step 9 — Profit & Loss.
//
//  The WPF Step 9 tab recalculates the whole model live from the saved
//  Steps 4–6 state plus two operator inputs; only those two inputs are
//  request-owned. EVERY row, qty and amount is recomputed server-side by
//  PnLCalculator (the port of ProfitLossStepViewModel.RecalculateAsync)
//  — the client can never post a profit figure.
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// POST body: the two operator inputs (WPF ProfitLossStepViewModel
/// defaults: wastage 15%, cost basis = selling price). Null keeps the
/// saved value; a first calculation with no saved value uses the default.
/// </summary>
public sealed record SaveStep9Request(
    decimal? WastagePercent,
    bool? CostAtSellingPrice);

/// <summary>One calculated P&amp;L row (WPF ProfitLossRow).</summary>
public sealed record PnlRowDto(
    Guid Id,
    string Kind,
    int SpecNumber,
    string SpecName,
    string Description,
    int SheetWidthMm,
    int SheetHeightMm,
    decimal SheetSqm,
    decimal Quantity,
    decimal PricePerUnit,
    string UnitLabel,
    decimal Amount,
    string SheetSource,
    int SortOrder);

/// <summary>The job-level KPI card (WPF field-for-field).</summary>
public sealed record PnlKpisDto(
    decimal GrossVatInclusive,
    decimal NetExVat,
    decimal TotalGlassCost,
    decimal TotalProductionCost,
    decimal Profit,
    bool IsLoss,
    decimal MarginPercent,
    decimal OverallMarginPercent,
    decimal NetMarginPercent,
    decimal UInsertCost,
    decimal BasisCost)
{
    /// <summary>WPF PriceBasedTotalActualCost — basis + U-insert.</summary>
    public decimal PriceBasedTotalActualCost => BasisCost + UInsertCost;

    /// <summary>WPF PriceBasedActualProfit — gross revenue against the
    /// price-based total (NOT net, NOT the actual rows).</summary>
    public decimal PriceBasedActualProfit =>
        Math.Round(GrossVatInclusive - PriceBasedTotalActualCost, 2);
}

/// <summary>
/// The Step 9 section: operator inputs, the saved KPI snapshot and the
/// recalculated rows. Rows are returned in WPF build order (glass,
/// spacer/interlayer spliced in, production, Step 6, invoice-wide).
/// </summary>
/// <summary>
/// Per-specification target-price result (WPF SpecTargetPriceResult).
/// Nullable decimals are null when the figure is NOT calculable — never
/// a fake 0.
/// </summary>
public sealed record SpecTargetPriceDto(
    int SpecNumber,
    string SpecName,
    string GlassTypeCode,
    string BuildType,
    decimal GivenPricePerSqm,
    decimal AreaSqm,
    decimal GivenValue,
    decimal OuterGlassRatePerSqm,
    decimal InnerGlassRatePerSqm,
    decimal ThirdGlassRatePerSqm,
    decimal AspRatePerSqm,
    decimal ProcessingRatePerSqm,
    decimal YieldFactor,
    decimal TargetUpliftPercent,
    string FormulaText,
    decimal SpecificationTargetPerSqm,
    string UInsertTreatment,
    decimal UInsertRatePerSqm,
    string UInsertSourceLabel,
    decimal AppliedUInsertPerSqm,
    decimal FinalRequiredTargetPerSqm,
    decimal DifferencePerSqm,
    decimal RequiredTargetValue,
    decimal DifferenceValue,
    decimal BasePricePerSqm,
    decimal GivenPriceJudgedPerSqm,
    decimal? ExtraProfitPerSqm,
    decimal? ExtraProfitMarginPercent,
    decimal? MarginAgainstTargetPercent,
    string Status,
    IReadOnlyList<string> Warnings);

/// <summary>Job-level target aggregate (WPF TargetPriceResult).</summary>
public sealed record TargetPriceSummaryDto(
    decimal TotalAreaSqm,
    decimal TotalGivenValue,
    decimal TotalSpecificationTargetValue,
    decimal TotalRequiredTargetValue,
    decimal TotalDifferenceValue,
    decimal AverageGivenPricePerSqm,
    decimal AverageSpecificationTargetPerSqm,
    decimal AverageFinalRequiredTargetPerSqm,
    decimal AverageBasePricePerSqm,
    decimal AverageGivenJudgedPerSqm,
    decimal? AverageExtraProfitPerSqm,
    decimal? ExtraProfitMarginPercent,
    decimal? CostBasedProfitMarginPercent,
    decimal? MarginAgainstTargetPercent,
    string Status,
    int ValidSpecCount,
    int IncompleteSpecCount,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<SpecTargetPriceDto> Specs);

/// <summary>
/// The Step 9 section: operator inputs, the saved KPI snapshot, the
/// recalculated rows AND the live target-price panel (computed from the
/// saved normalized inputs — nothing client-posted).
/// </summary>
public sealed record Step9Dto(
    decimal WastagePercent,
    bool CostAtSellingPrice,
    bool HasBeenCalculated,
    PnlKpisDto? Kpis,
    IReadOnlyList<PnlRowDto> Rows,
    TargetPriceSummaryDto? Targets);
