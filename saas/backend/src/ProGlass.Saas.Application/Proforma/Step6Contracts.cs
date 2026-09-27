using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Proforma;

// ═══════════════════════════════════════════════════════════════════════
//  Step 6 — Other Charges / Wastages / Custom Charges.
//
//  The request mirrors the WPF save contract (ProformaInvoiceMapper:
//  PredefinedChargeSnapshot / WastageRowSnapshot / CustomChargeSnapshot).
//  Metric quantities and amounts are NEVER taken from the client — the
//  service recomputes them from the SAVED Step 5 grid exactly as the WPF
//  recomputes them live (MetricHub → SpecTargetSelection → row.
//  Recalculate), so a saved row can never claim a qty the grid does not
//  support.
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// One predefined charge row (WPF OtherChargeRow →
/// PredefinedChargeSnapshot). MetricKey + CheckedSpecGroupNumbers are the
/// Chat-20 two-part targeting; polish rows carry their sub-type.
/// </summary>
public sealed record OtherChargeRowRequest(
    string? TemplateKey,
    string? ChargeName,
    string? Description,
    string? Unit,
    bool IsActive,
    string? MetricKey,
    IReadOnlyList<int>? CheckedSpecGroupNumbers,
    decimal Rate,
    string? PolishSubType = null,
    string? CustomPolishDescription = null,
    decimal? CustomPolishQty = null);

/// <summary>
/// One wastage row (WPF WastageRow → WastageRowSnapshot). Manual mode:
/// TotalSqm × Rate. Optimization mode: the 85%-baseline formula — the
/// rate is DERIVED, never trusted from the client.
/// </summary>
public sealed record WastageRowRequest(
    bool IsActive,
    string? ColorName,
    decimal Rate,
    bool IsManuallyAdded,
    bool UseOptimizationFormula,
    decimal FormulaPricePerSqm,
    decimal OptimizationPercent,
    int SheetQuantity,
    IReadOnlyList<string>? CheckedSourceKeys);

/// <summary>One custom charge row (WPF CustomChargeRow).</summary>
public sealed record CustomChargeRowRequest(
    bool IsActive,
    string? ChargeName,
    decimal Qty,
    string? Unit,
    decimal Rate);

/// <summary>The whole Step 6 section — saved as one unit, like the grid.</summary>
public sealed record SaveStep6Request(
    IReadOnlyList<OtherChargeRowRequest>? Charges,
    IReadOnlyList<WastageRowRequest>? Wastages,
    IReadOnlyList<CustomChargeRowRequest>? Customs);

/// <summary>A saved predefined charge row, with its derived qty + amount.</summary>
public sealed record OtherChargeDto(
    Guid Id,
    string TemplateKey,
    string ChargeName,
    string Description,
    string Unit,
    bool IsActive,
    string MetricKey,
    IReadOnlyList<int> CheckedSpecGroupNumbers,
    decimal Rate,
    decimal MetricQty,
    decimal Amount,
    string PolishSubType,
    string CustomPolishDescription,
    decimal CustomPolishQty,
    int SortOrder);

/// <summary>A saved wastage row, with its derived SQM / rate / amount.</summary>
public sealed record WastageRowDto(
    Guid Id,
    bool IsActive,
    string ColorName,
    IReadOnlyList<string> CheckedSourceKeys,
    decimal TotalSqm,
    bool IsManuallyAdded,
    bool UseOptimizationFormula,
    decimal FormulaPricePerSqm,
    decimal OptimizationPercent,
    int SheetQuantity,
    decimal Rate,
    decimal Amount,
    int SortOrder);

/// <summary>A saved custom charge row.</summary>
public sealed record CustomChargeDto(
    Guid Id,
    bool IsActive,
    string ChargeName,
    decimal Qty,
    string Unit,
    decimal Rate,
    decimal Amount,
    int SortOrder);

/// <summary>One selectable pane source for a wastage row (read model).</summary>
public sealed record WastageSourceDto(
    string Key,
    int SpecGroupNumber,
    string SideLabel,
    string ColorName,
    decimal SqmValue,
    int SheetWidthMm,
    int SheetHeightMm,
    decimal PricePerSqm);

/// <summary>Per-spec metrics strip (the Step 6 top panel read model).</summary>
public sealed record SpecGroupMetricsDto(
    int SpecGroupNumber,
    string GlassType,
    string SpecName,
    bool IsDoubleGlass,
    int PaneFactor,
    bool IsMatchedSameGlass,
    decimal Lm1,
    decimal Lm2,
    decimal Sqm1,
    decimal Sqm2,
    int Qty);

/// <summary>One predefined charge the automation rules imply for a spec
/// (WPF AutoChargeSuggestion — Step 6 Advanced toggle preselection).</summary>
public sealed record AutoChargeSuggestionDto(
    string TemplateKey,
    string MetricKey,
    string? PolishSubType,
    string Reason);

/// <summary>One wastage pane the automation rules suggest preselecting
/// (WPF AutoWastageSuggestion).</summary>
public sealed record AutoWastageSuggestionDto(
    int SpecGroupNumber,
    string SourceSideLabel,
    string ColorName,
    string DisplayName,
    string Reason);

/// <summary>Per-spec-group automation decisions.</summary>
public sealed record Step6SuggestionsGroupDto(
    int SpecGroupNumber,
    string GlassType,
    string SpecName,
    bool MatchedSameGlass,
    IReadOnlyList<AutoChargeSuggestionDto> Charges,
    IReadOnlyList<AutoWastageSuggestionDto> Wastages);

/// <summary>GET step6/suggestions — the Step 6 Advanced automation
/// read model (which predefined charges / wastage sides each saved spec
/// group implies). The client applies the decisions to its rows.</summary>
public sealed record Step6SuggestionsDto(
    IReadOnlyList<Step6SuggestionsGroupDto> Groups);

/// <summary>The Step 6 section: saved rows + the derived read model.</summary>
public sealed record Step6Dto(
    IReadOnlyList<OtherChargeDto> Charges,
    IReadOnlyList<WastageRowDto> Wastages,
    IReadOnlyList<CustomChargeDto> Customs,
    decimal Subtotal,
    IReadOnlyList<SpecGroupMetricsDto> SpecMetrics,
    IReadOnlyList<WastageSourceDto> WastageSources);
