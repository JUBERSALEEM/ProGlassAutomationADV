// PricingService unit tests: the config lifecycle (defaults → save →
// read-back → validation) and the preview dispatch. The rate math itself
// is pinned in ProGlass.Saas.Domain.Tests.RateCalculatorTests — these
// tests only prove the service wires the right panel to the right
// calculator and never invents values.

using ProGlass.Saas.Application.Pricing;
using ProGlass.Saas.Domain.Pricing;
using Xunit;

namespace ProGlass.Saas.Application.Tests.Pricing;

public class PricingServiceTests
{
    private readonly FakePricingConfigStore _store = new();
    private readonly FakeAuditWriter _audit = new();
    private readonly PricingService _service;

    public PricingServiceTests()
    {
        _service = new PricingService(_store, _audit);
    }

    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    private static PricingConfigRequest ValidRequest(
        decimal yield = 0.85m, decimal margin = 15m, decimal vatRate = 0.05m) => new(
        yield, margin,
        new PricingCatalogs(
            new[] { "6mm", "8mm" },
            new[] { "FT Glass", "Annealed" },
            new[] { "12mm ASP" },
            new[] { "PVB", "EVA" },
            new[] { "1.52mm" },
            new[] { "None (Standard)" },
            new[] { "None", "With Fanhole" }),
        new VatConfigRequest(vatRate, "AED"));

    // ═════════════════════════════ GET CONFIG ══════════════════════

    [Fact]
    public async Task Fresh_tenant_gets_the_wpf_defaults_without_a_row_being_created()
    {
        var config = await _service.GetConfigAsync(Tenant, CancellationToken.None);

        Assert.Equal(0.85m, config.YieldFactorDefault);
        Assert.Equal(15m, config.MarginDefault);
        Assert.Equal(0.05m, config.Vat.Rate);
        Assert.Equal("AED", config.Vat.Currency);

        // Full WPF catalog lists, verbatim.
        Assert.Equal(PricingDefaults.ThicknessOptions, config.Catalogs.ThicknessOptions);
        Assert.Equal(PricingDefaults.PanelExtraOptions, config.Catalogs.PanelExtraOptions);

        // Read must not persist anything.
        Assert.Empty(_store.SavedPricing);
        Assert.Equal(0, _store.UpsertCalls);

        // Panel defaults: the DGU+LAM set IS the business's worked chain.
        Assert.Equal(344.75m, RateCalculator.Calculate(ToParams(
            config.PanelDefaults.DguLam)).RatePerSqm);
    }

    private static DguLamParameters ToParams(PricingDefaults.DguLamPanelDefaults d) => new(
        d.OuterBasePrice, d.LamOuterBasePrice, d.LamInnerBasePrice,
        d.OuterCuttingCost, d.LamOuterCuttingCost, d.LamInnerCuttingCost,
        d.OuterTemperingCost, d.LamOuterTemperingCost, d.LamInnerTemperingCost,
        d.InterlayerSellPrice, d.InterlayerType, d.SpacerPrice,
        d.YieldFactor, d.MarginProfitPercent);

    [Fact]
    public async Task Saved_settings_round_trip()
    {
        var saved = await _service.SaveConfigAsync(
            Tenant, Actor, "1.2.3.4", ValidRequest(yield: 0.9m, margin: 25m, vatRate: 0m),
            CancellationToken.None);

        Assert.True(saved.Success);
        Assert.Equal(0.9m, saved.Value!.YieldFactorDefault);
        Assert.Equal(25m, saved.Value.MarginDefault);
        Assert.Equal(0m, saved.Value.Vat.Rate);

        var config = await _service.GetConfigAsync(Tenant, CancellationToken.None);
        Assert.Equal(0.9m, config.YieldFactorDefault);
        Assert.Equal(25m, config.MarginDefault);
        Assert.Equal(0m, config.Vat.Rate);
        Assert.Equal(new[] { "6mm", "8mm" }, config.Catalogs.ThicknessOptions);
    }

    [Fact]
    public async Task Save_is_audited()
    {
        await _service.SaveConfigAsync(
            Tenant, Actor, "1.2.3.4", ValidRequest(), CancellationToken.None);

        var entry = Assert.Single(_audit.Events);
        Assert.Equal(Tenant, entry.TenantId);
        Assert.Equal(Actor, entry.UserId);
        Assert.Equal("pricing.config.saved", entry.Action);
    }

    [Fact]
    public async Task Second_save_updates_the_same_row()
    {
        await _service.SaveConfigAsync(Tenant, Actor, null, ValidRequest(), CancellationToken.None);
        await _service.SaveConfigAsync(
            Tenant, Actor, null, ValidRequest(yield: 0.95m), CancellationToken.None);

        Assert.Equal(2, _store.UpsertCalls);
        var row = Assert.Single(_store.SavedPricing);
        Assert.Equal(0.95m, row.YieldFactorDefault);
    }

    // ═════════════════════════════ VALIDATION ══════════════════════

    [Theory]
    [InlineData(0)]
    [InlineData(-0.85)]
    public async Task Yield_must_be_positive(decimal yield)
    {
        var result = await _service.SaveConfigAsync(
            Tenant, Actor, null, ValidRequest(yield: yield), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PricingErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Contains("Yield", result.Message);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(5)]
    public async Task Vat_rate_must_be_a_fraction_between_0_and_1(decimal rate)
    {
        var result = await _service.SaveConfigAsync(
            Tenant, Actor, null, ValidRequest(vatRate: rate), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("VAT rate", result.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AE")]
    [InlineData("DIRHAM")]
    public async Task Currency_must_be_three_letters(string currency)
    {
        var request = ValidRequest() with
        {
            Vat = new VatConfigRequest(0.05m, currency),
        };

        var result = await _service.SaveConfigAsync(
            Tenant, Actor, null, request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Currency", result.Message);
    }

    [Fact]
    public async Task Missing_catalogs_fail_validation()
    {
        var request = new PricingConfigRequest(
            0.85m, 15m,
            new PricingCatalogs(
                null!, new[] { "FT Glass" }, new[] { "12mm ASP" }, new[] { "PVB" },
                new[] { "1.52mm" }, new[] { "None (Standard)" }, new[] { "None" }),
            new VatConfigRequest(0.05m, "AED"));

        var result = await _service.SaveConfigAsync(
            Tenant, Actor, null, request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ThicknessOptions", result.Message);
    }

    [Fact]
    public async Task Blank_catalog_entries_fail_validation()
    {
        var request = new PricingConfigRequest(
            0.85m, 15m,
            new PricingCatalogs(
                new[] { "6mm", "  " }, new[] { "FT Glass" }, new[] { "12mm ASP" },
                new[] { "PVB" }, new[] { "1.52mm" }, new[] { "None (Standard)" },
                new[] { "None" }),
            new VatConfigRequest(0.05m, "AED"));

        var result = await _service.SaveConfigAsync(
            Tenant, Actor, null, request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("blank", result.Message);
    }

    [Fact]
    public async Task Empty_catalogs_are_allowed()
    {
        // A tenant may trim a list to nothing (they never quote LAM,
        // say) — only missing lists and blank entries are refused.
        var request = new PricingConfigRequest(
            0.85m, 15m,
            new PricingCatalogs(
                Array.Empty<string>(), new[] { "FT Glass" }, new[] { "12mm ASP" },
                new[] { "PVB" }, new[] { "1.52mm" }, new[] { "None (Standard)" },
                new[] { "None" }),
            new VatConfigRequest(0.05m, "AED"));

        var result = await _service.SaveConfigAsync(
            Tenant, Actor, null, request, CancellationToken.None);

        Assert.True(result.Success);

        var config = await _service.GetConfigAsync(Tenant, CancellationToken.None);
        Assert.Empty(config.Catalogs.ThicknessOptions);
        // Untouched catalogs keep the tenant's other choices.
        Assert.Equal(new[] { "FT Glass" }, config.Catalogs.TreatmentOptions);
    }

    // ═════════════════════════════ PREVIEW ═════════════════════════

    [Fact]
    public void Preview_dgu_lam_default_chain_quotes_the_business_rate()
    {
        var d = PricingDefaults.DguLamPanelDefaults.Instance;
        var request = new PricingPreviewRequest(
            "DGU+LAM", null, null, null,
            new DguLamParameters(
                d.OuterBasePrice, d.LamOuterBasePrice, d.LamInnerBasePrice,
                d.OuterCuttingCost, d.LamOuterCuttingCost, d.LamInnerCuttingCost,
                d.OuterTemperingCost, d.LamOuterTemperingCost, d.LamInnerTemperingCost,
                d.InterlayerSellPrice, d.InterlayerType, d.SpacerPrice,
                d.YieldFactor, d.MarginProfitPercent));

        var result = _service.Preview(request);

        Assert.True(result.Success);
        Assert.Equal("DGU+LAM", result.Value!.GlassType);
        Assert.Equal(344.75m, result.Value.RatePerSqm);
        Assert.Equal(287.29m, result.Value.CostBasisPerSqm);
        Assert.Empty(result.Value.Warnings);
    }

    [Fact]
    public void Preview_surfaces_the_wpf_warnings()
    {
        var request = new PricingPreviewRequest(
            "DGU+LAM", null, null, null,
            new DguLamParameters(
                48m, 25m, 25m, 10m, 10m, 10m, 12m, 10m, 10m,
                0m, "SGP", 0m, 0m, 20m));

        var result = _service.Preview(request);

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Warnings.Count);
        Assert.Contains(result.Value.Warnings, w => w.Contains("Yield"));
        Assert.Contains(result.Value.Warnings, w => w.Contains("SGP"));
    }

    [Fact]
    public void Preview_routes_each_glass_type_to_its_own_calculator()
    {
        var sgu = _service.Preview(new PricingPreviewRequest(
            "SGU",
            new SguParameters(46m, 0.85m, 15m, 25m, 15m),
            null, null, null));
        Assert.Equal(108.24m, sgu.Value!.RatePerSqm);

        var dgu = _service.Preview(new PricingPreviewRequest(
            "DGU", null,
            new DguParameters(25m, 25m, 45m, 0.85m, 15m),
            null, null));
        Assert.Equal(119.40m, dgu.Value!.RatePerSqm);

        var lam = _service.Preview(new PricingPreviewRequest(
            "LAM", null, null,
            new LamParameters(25m, 25m, 110m, 10m, 0m, 20m, 0m, 0.85m, 15m),
            null));
        Assert.Equal(228.65m, lam.Value!.RatePerSqm);

        // Glass type is case-insensitive ("lam" = "LAM").
        var lamLower = _service.Preview(new PricingPreviewRequest(
            "lam", null, null,
            new LamParameters(25m, 25m, 110m, 10m, 0m, 20m, 0m, 0.85m, 15m),
            null));
        Assert.Equal(lam.Value.RatePerSqm, lamLower.Value!.RatePerSqm);
    }

    [Fact]
    public void Preview_rejects_unknown_glass_types()
    {
        var result = _service.Preview(new PricingPreviewRequest(
            "TRIPLE", new SguParameters(46m, 0.85m, 15m, 25m, 15m), null, null, null));

        Assert.False(result.Success);
        Assert.Equal(PricingErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Contains("TRIPLE", result.Message);
    }

    [Fact]
    public void Preview_rejects_missing_parameter_set()
    {
        var result = _service.Preview(new PricingPreviewRequest("SGU", null, null, null, null));

        Assert.False(result.Success);
        Assert.Equal(PricingErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Contains("SGU parameters are required", result.Message);
    }
}
