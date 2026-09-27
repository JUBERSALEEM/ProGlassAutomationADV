using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Pricing;

namespace ProGlass.Saas.Application.Pricing;

/// <summary>
/// Tenant pricing settings + the Step 4 rate preview.
///
/// Source of truth (read 2026-09-27):
///   • Rate formulas — ViewModels/ProformaInvoice/Step4PricingEstimator/
///     PricingEstimatorViewModel.Part2.cs L480–740 and
///     DguLamParametersViewModel.CalculateRate. Ported verbatim to
///     Domain.Pricing.RateCalculator (golden values pinned in
///     ProGlass.Saas.Domain.Tests.RateCalculatorTests).
///   • Config defaults — the WPF panel constructor initializers, ported
///     verbatim to Domain.Pricing.PricingDefaults. A tenant with no saved
///     row gets the WPF defaults; nothing is invented on either side.
///   • Warnings — the exact texts the WPF panels surface: yield ≤ 0 falls
///     back to 1.0 WITH a warning (BUG-40/41) and an unpriced interlayer
///     is reported, never silently dropped.
///
/// Differences from WPF (documented in WPF_TO_SAAS_MAPPING §1.11):
///   • Catalogs are per-tenant editable rows instead of hardcoded lists;
///     where the four WPF panels ship different lists the seed takes the
///     documented union (each merge listed in the mapping doc).
///   • The panel defaults are read-only constants (WPF hardcodes them).
/// </summary>
public sealed class PricingService
{
    private static readonly JsonSerializerOptions CatalogJson = new() { WriteIndented = false };

    private readonly IPricingConfigStore _store;
    private readonly IAuditWriter _audit;

    public PricingService(IPricingConfigStore store, IAuditWriter audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    // ═════════════════════════════ CONFIG (GET) ═════════════════════

    /// <summary>
    /// The tenant's settings, or the WPF defaults when nothing was saved
    /// yet (no row is created on read).
    /// </summary>
    public async Task<PricingConfigResponse> GetConfigAsync(
        Guid tenantId, CancellationToken ct)
    {
        var (pricing, vat) = await _store.GetAsync(tenantId, ct);

        var state = ToState(pricing, vat);
        return new PricingConfigResponse(
            state.YieldFactorDefault,
            state.MarginDefault,
            state.Catalogs,
            PanelDefaults(),
            FixedCatalogs(),
            state.Vat);
    }

    // ═════════════════════════════ CONFIG (PUT) ═════════════════════

    public async Task<PricingResult<PricingConfigState>> SaveConfigAsync(
        Guid tenantId, Guid actorId, string? ip, PricingConfigRequest request,
        CancellationToken ct)
    {
        if (request is null)
        {
            return PricingResult<PricingConfigState>.Fail(
                PricingErrorCode.ValidationFailed, "Request body is required.");
        }

        var error = Validate(request);
        if (error is not null)
        {
            return PricingResult<PricingConfigState>.Fail(
                PricingErrorCode.ValidationFailed, error);
        }

        var (existing, _) = await _store.GetAsync(tenantId, ct);

        var pricing = existing ?? NewPricingRow(tenantId);
        pricing.YieldFactorDefault = request.YieldFactorDefault;
        pricing.MarginDefault = request.MarginDefault;
        pricing.ThicknessOptions = Serialize(request.Catalogs.ThicknessOptions);
        pricing.TreatmentOptions = Serialize(request.Catalogs.TreatmentOptions);
        pricing.AspWidths = Serialize(request.Catalogs.AspWidths);
        pricing.InterlayerTypes = Serialize(request.Catalogs.InterlayerTypes);
        pricing.InterlayerThicknesses = Serialize(request.Catalogs.InterlayerThicknesses);
        pricing.PolishOptions = Serialize(request.Catalogs.PolishOptions);
        pricing.PanelExtraOptions = Serialize(request.Catalogs.PanelExtraOptions);

        var vat = NewVatRow(tenantId, request.Vat.Rate, request.Vat.Currency);

        await _store.UpsertAsync(pricing, vat, actorId, ct);

        await _audit.WriteAsync(
            tenantId, actorId, "pricing.config.saved", "tenant_pricing_config",
            pricing.Id, ip, null, ct);

        return PricingResult<PricingConfigState>.Ok(ToState(pricing, vat));
    }

    /// <summary>Validation mirrors the schema CHECKs (yield &gt; 0, VAT
    /// rate 0–1) plus request-shape checks; nothing beyond — do not invent
    /// business rules (§66).</summary>
    private static string? Validate(PricingConfigRequest request)
    {
        // ck_tenant_pricing_yield: yield_factor_default > 0.
        if (request.YieldFactorDefault <= 0m)
        {
            return "Yield factor default must be greater than 0.";
        }

        if (request.MarginDefault < 0m)
        {
            return "Margin default cannot be negative.";
        }

        // ck_tenant_vat_rate: 0 ≤ rate ≤ 1 (stored as a fraction).
        if (request.Vat is null)
        {
            return "Vat is required.";
        }

        if (request.Vat.Rate < 0m || request.Vat.Rate > 1m)
        {
            return "VAT rate must be between 0 and 1 (e.g. 0.05 for 5%).";
        }

        if (string.IsNullOrWhiteSpace(request.Vat.Currency)
            || request.Vat.Currency.Trim().Length != 3)
        {
            return "Currency must be a 3-letter ISO code (e.g. AED).";
        }

        if (request.Catalogs is null)
        {
            return "Catalogs are required.";
        }

        foreach (var (name, list) in new (string, IReadOnlyList<string>?)[]
        {
            ("ThicknessOptions", request.Catalogs.ThicknessOptions),
            ("TreatmentOptions", request.Catalogs.TreatmentOptions),
            ("AspWidths", request.Catalogs.AspWidths),
            ("InterlayerTypes", request.Catalogs.InterlayerTypes),
            ("InterlayerThicknesses", request.Catalogs.InterlayerThicknesses),
            ("PolishOptions", request.Catalogs.PolishOptions),
            ("PanelExtraOptions", request.Catalogs.PanelExtraOptions),
        })
        {
            // A list may legitimately be trimmed to empty (a tenant that
            // never uses a catalog); it just cannot be missing or contain
            // blank entries.
            if (list is null)
            {
                return $"{name} is required.";
            }

            if (list.Any(string.IsNullOrWhiteSpace))
            {
                return $"{name} cannot contain blank entries.";
            }
        }

        return null;
    }

    private static TenantPricingConfig NewPricingRow(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
    };

    private static TenantVatConfig NewVatRow(Guid tenantId, decimal rate, string currency)
    {
        // char(3) in the schema — trim so a padded value cannot sneak in.
        var code = currency.Trim();
        return new TenantVatConfig
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VatRate = rate,
            Currency = code,
        };
    }

    private static string Serialize(IReadOnlyList<string> catalog) =>
        JsonSerializer.Serialize(catalog, CatalogJson);

    private static PricingConfigState ToState(
        TenantPricingConfig? pricing, TenantVatConfig? vat)
    {
        // No saved row (or an empty catalog column, as the schema defaults
        // '[]' would produce) → the WPF default, so a fresh tenant sees
        // the full WPF lists. Per-catalog fallback keeps a tenant's saved
        // choices for everything they did customize.
        var defaults = PricingDefaults.Catalogs();

        PricingCatalogs catalogs;
        decimal yieldFactor;
        decimal margin;

        if (pricing is null)
        {
            catalogs = defaults;
            yieldFactor = PricingDefaults.YieldFactorDefault;
            margin = PricingDefaults.MarginDefault;
        }
        else
        {
            catalogs = new PricingCatalogs(
                Deserialize(pricing.ThicknessOptions) is { Count: > 0 } v
                    ? v : defaults.ThicknessOptions,
                Deserialize(pricing.TreatmentOptions) is { Count: > 0 } v
                    ? v : defaults.TreatmentOptions,
                Deserialize(pricing.AspWidths) is { Count: > 0 } v
                    ? v : defaults.AspWidths,
                Deserialize(pricing.InterlayerTypes) is { Count: > 0 } v
                    ? v : defaults.InterlayerTypes,
                Deserialize(pricing.InterlayerThicknesses) is { Count: > 0 } v
                    ? v : defaults.InterlayerThicknesses,
                Deserialize(pricing.PolishOptions) is { Count: > 0 } v
                    ? v : defaults.PolishOptions,
                Deserialize(pricing.PanelExtraOptions) is { Count: > 0 } v
                    ? v : defaults.PanelExtraOptions);
            yieldFactor = pricing.YieldFactorDefault;
            margin = pricing.MarginDefault;
        }

        var vatConfig = vat is null
            ? new VatConfigDto(PricingDefaults.VatRateDefault, PricingDefaults.CurrencyDefault)
            : new VatConfigDto(vat.VatRate, vat.Currency);

        return new PricingConfigState(yieldFactor, margin, catalogs, vatConfig);
    }

    private static IReadOnlyList<string> Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<List<string>>(json, CatalogJson)
              ?? new List<string>();

    private static PanelDefaultsDto PanelDefaults() => new(
        PricingDefaults.SguPanelDefaults.Instance,
        PricingDefaults.DguPanelDefaults.Instance,
        PricingDefaults.LamPanelDefaults.Instance,
        PricingDefaults.DguLamPanelDefaults.Instance,
        new Dictionary<string, decimal>(PricingDefaultsSurcharges()));

    private static IEnumerable<KeyValuePair<string, decimal>> PricingDefaultsSurcharges()
    {
        // SpacerPricing owns the surcharge table; expose it through the
        // read-only defaults payload instead of duplicating the numbers.
        foreach (var material in SpacerPricing.MaterialOptions)
        {
            yield return new KeyValuePair<string, decimal>(
                material, SpacerPricing.SuggestedPrice("6mm ASP", material) - 37.5m);
        }
    }

    private static FixedCatalogsDto FixedCatalogs() => new(
        PricingDefaults.AspWidths,              // WPF SpacerWidthOptions
        SpacerPricing.MaterialOptions,
        PricingDefaults.SpacerExtraOptions,
        PricingDefaults.EdgePolishOptions,
        PricingDefaults.PvbColorOptions,
        PricingDefaults.InterlayerColorOptions,
        PricingDefaults.InterlayerPrices.ToDictionary(
            p => p.Key, p => new InterlayerPriceDto(p.Value.Buy, p.Value.Sell)));

    // ═════════════════════════════ PREVIEW ═════════════════════════

    /// <summary>
    /// POST /api/v1/pricing/preview — runs the WPF Step 4 rate rules on
    /// the supplied panel values. Pure calculation: no rows are written,
    /// so it needs no audit and no permission beyond authentication.
    /// </summary>
    public PricingResult<PricingPreviewResult> Preview(PricingPreviewRequest request)
    {
        if (request is null)
        {
            return PricingResult<PricingPreviewResult>.Fail(
                PricingErrorCode.ValidationFailed, "Request body is required.");
        }

        var glassType = (request.GlassType ?? "").Trim().ToUpperInvariant();

        RateResult rate;
        switch (glassType)
        {
            case GlassTypes.Sgu:
                if (request.Sgu is null)
                {
                    return MissingParameters(GlassTypes.Sgu);
                }
                rate = RateCalculator.Calculate(request.Sgu);
                break;

            case GlassTypes.Dgu:
                if (request.Dgu is null)
                {
                    return MissingParameters(GlassTypes.Dgu);
                }
                rate = RateCalculator.Calculate(request.Dgu);
                break;

            case GlassTypes.Lam:
                if (request.Lam is null)
                {
                    return MissingParameters(GlassTypes.Lam);
                }
                rate = RateCalculator.Calculate(request.Lam);
                break;

            case GlassTypes.DguLam:
                if (request.DguLam is null)
                {
                    return MissingParameters(GlassTypes.DguLam);
                }
                rate = RateCalculator.Calculate(request.DguLam);
                break;

            default:
                return PricingResult<PricingPreviewResult>.Fail(
                    PricingErrorCode.ValidationFailed,
                    $"Unknown glass type '{request.GlassType}'. " +
                    $"Expected one of: {string.Join(", ", GlassTypes.All)}.");
        }

        return PricingResult<PricingPreviewResult>.Ok(new PricingPreviewResult(
            glassType, rate.RatePerSqm, rate.CostBasisPerSqm, rate.Warnings));

        static PricingResult<PricingPreviewResult> MissingParameters(string glassType) =>
            PricingResult<PricingPreviewResult>.Fail(
                PricingErrorCode.ValidationFailed,
                $"{glassType} parameters are required for a {glassType} preview.");
    }
}
