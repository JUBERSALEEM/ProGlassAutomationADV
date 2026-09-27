using ProGlass.Saas.Domain.Pricing;

namespace ProGlass.Saas.Application.Pricing;

/// <summary>Pricing failure codes (API §0 error contract).</summary>
public enum PricingErrorCode
{
    None = 0,
    ValidationFailed,
}

/// <summary>Typed result: a value or a code + operator-readable message.</summary>
public sealed record PricingResult<T>(
    bool Success, T? Value, PricingErrorCode ErrorCode, string Message)
{
    public static PricingResult<T> Ok(T value) =>
        new(true, value, PricingErrorCode.None, "");

    public static PricingResult<T> Fail(PricingErrorCode code, string message) =>
        new(false, default, code, message);
}

// ── Requests ───────────────────────────────────────────────────────────

/// <summary>
/// PUT /api/v1/pricing/config — the tenant-editable settings. Panel
/// defaults and the fixed catalogs are NOT part of the request: they are
/// WPF constants and read-only.
/// </summary>
public sealed record PricingConfigRequest(
    decimal YieldFactorDefault,
    decimal MarginDefault,
    PricingCatalogs Catalogs,
    VatConfigRequest Vat);

public sealed record VatConfigRequest(decimal Rate, string Currency);

/// <summary>
/// POST /api/v1/pricing/preview — a Step 4 estimate. The glassType picks
/// which parameter set is used; the others must be absent.
/// </summary>
public sealed record PricingPreviewRequest(
    string GlassType,
    SguParameters? Sgu,
    DguParameters? Dgu,
    LamParameters? Lam,
    DguLamParameters? DguLam);

// ── Read models ────────────────────────────────────────────────────────

/// <summary>
/// GET /api/v1/pricing/config — the tenant's editable settings, the WPF
/// panel opening defaults (read-only) and the WPF lists that are not
/// tenant-editable (read-only).
/// </summary>
public sealed record PricingConfigResponse(
    decimal YieldFactorDefault,
    decimal MarginDefault,
    PricingCatalogs Catalogs,
    PanelDefaultsDto PanelDefaults,
    FixedCatalogsDto FixedCatalogs,
    VatConfigDto Vat);

/// <summary>
/// The four panels' opening numbers (WPF view-model constructor
/// initializers, verbatim — the DGU+LAM set IS the business's 344.75
/// worked chain).
/// </summary>
public sealed record PanelDefaultsDto(
    PricingDefaults.SguPanelDefaults Sgu,
    PricingDefaults.DguPanelDefaults Dgu,
    PricingDefaults.LamPanelDefaults Lam,
    PricingDefaults.DguLamPanelDefaults DguLam,
    IReadOnlyDictionary<string, decimal> SpacerMaterialSurcharges);

/// <summary>
/// WPF dropdown lists that live in code, not in tenant_pricing_config
/// (schema 0003 has seven editable catalogs; these are the rest).
/// </summary>
public sealed record FixedCatalogsDto(
    IReadOnlyList<string> SpacerWidthOptions,
    IReadOnlyList<string> SpacerMaterialOptions,
    IReadOnlyList<string> SpacerExtraOptions,
    IReadOnlyList<string> EdgePolishOptions,
    IReadOnlyList<string> PvbColorOptions,
    IReadOnlyList<string> InterlayerColorOptions,
    IReadOnlyDictionary<string, InterlayerPriceDto> InterlayerPrices);

/// <summary>Interlayer buy/sell pair by chemistry (WPF
/// ApplyInterlayerDefaults — SGP is 0/0 on purpose).</summary>
public sealed record InterlayerPriceDto(decimal Buy, decimal Sell);

public sealed record VatConfigDto(decimal Rate, string Currency);

/// <summary>GET/PUT result — mirrors the request shape for round-tripping.</summary>
public sealed record PricingConfigState(
    decimal YieldFactorDefault,
    decimal MarginDefault,
    PricingCatalogs Catalogs,
    VatConfigDto Vat);

/// <summary>
/// POST /api/v1/pricing/preview result — the Step 4 panel's rate, the
/// pre-margin cost basis behind it, and the same warnings the WPF
/// surfaces (BUG-40/41 yield fallback, unpriced interlayer).
/// </summary>
public sealed record PricingPreviewResult(
    string GlassType,
    decimal RatePerSqm,
    decimal CostBasisPerSqm,
    IReadOnlyList<string> Warnings);
