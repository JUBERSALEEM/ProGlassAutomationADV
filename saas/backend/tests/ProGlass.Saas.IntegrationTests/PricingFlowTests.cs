using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.Pricing;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Pricing;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// Pricing flow against live PostgreSQL: the real EF store (query filters
/// + RLS via the tenant-bound factory), the WPF-default fallback for a
/// fresh tenant, the upsert (single row per tenant, audit stamps) and
/// tenant isolation for the config, plus the preview through the service
/// stack that the controller uses.
/// </summary>
[Collection("database")]
public sealed class PricingFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly PricingService _sut;

    public PricingFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        _sut = new PricingService(
            new PricingConfigStore(factory),
            new AuditWriter(factory, NullLogger<AuditWriter>.Instance));
    }

    private static PricingConfigRequest Request(
        decimal yield = 0.85m, decimal margin = 15m, decimal vat = 0.05m) => new(
        yield, margin,
        new PricingCatalogs(
            new[] { "6mm", "8mm", "10mm" },
            new[] { "FT Glass", "Annealed" },
            new[] { "12mm ASP" },
            new[] { "PVB", "EVA" },
            new[] { "1.52mm" },
            new[] { "None (Standard)", "Polished Edges" },
            new[] { "None", "With Fanhole" }),
        new VatConfigRequest(vat, "AED"));

    /// <summary>
    /// A fresh tenant row (config is one row PER tenant, so every test
    /// needs its own — sharing TenantA would make the outcome depend on
    /// test order). Inserted through the owner connection like the
    /// fixture does for TenantA/TenantB.
    /// </summary>
    private Guid NewTenant(string label)
    {
        var tenantId = Guid.NewGuid();
        using var connection = new Npgsql.NpgsqlConnection(_fixture.OwnerDb);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO saas.tenants (id, name, slug)
            VALUES ($1, $2, $3)
            """;
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue($"Pricing {label} {tenantId:N}"[..60]);
        command.Parameters.AddWithValue($"pr-{tenantId:N}"[..40]);
        command.ExecuteNonQuery();
        return tenantId;
    }

    [DatabaseFact]
    public async Task Fresh_tenant_reads_defaults_and_writes_nothing()
    {
        var tenant = NewTenant("defaults");

        var config = await _sut.GetConfigAsync(tenant, CancellationToken.None);

        Assert.Equal(0.85m, config.YieldFactorDefault);
        Assert.Equal(15m, config.MarginDefault);
        Assert.Equal(0.05m, config.Vat.Rate);
        Assert.Equal("AED", config.Vat.Currency);
        Assert.Equal(PricingDefaults.ThicknessOptions, config.Catalogs.ThicknessOptions);

        // No row was created by the read.
        var (pricing, vat) = await StoreGetAsync(tenant);
        Assert.Null(pricing);
        Assert.Null(vat);
    }

    [DatabaseFact]
    public async Task Save_creates_one_row_per_tenant_then_updates_it()
    {
        var tenant = NewTenant("upsert");
        var actor = Guid.NewGuid();

        var first = await _sut.SaveConfigAsync(
            tenant, actor, "127.0.0.1", Request(yield: 0.9m, margin: 22m, vat: 0m),
            CancellationToken.None);
        Assert.True(first.Success, first.Message);

        var (pricing, vat) = await StoreGetAsync(tenant);
        Assert.NotNull(pricing);
        Assert.NotNull(vat);
        Assert.Equal(0.9m, pricing!.YieldFactorDefault);
        Assert.Equal(22m, pricing.MarginDefault);
        Assert.Equal(actor, pricing.CreatedBy);
        Assert.Equal(actor, pricing.UpdatedBy);
        Assert.Equal(0m, vat!.VatRate);
        Assert.Equal("AED", vat.Currency);
        var firstId = pricing.Id;

        // Second save updates the SAME row (UNIQUE(tenant_id)), not a new one.
        var second = await _sut.SaveConfigAsync(
            tenant, actor, null, Request(yield: 0.95m, vat: 0.05m), CancellationToken.None);
        Assert.True(second.Success, second.Message);

        (pricing, vat) = await StoreGetAsync(tenant);
        Assert.NotNull(pricing);
        Assert.Equal(firstId, pricing!.Id);
        Assert.Equal(0.95m, pricing.YieldFactorDefault);
        Assert.Equal(0.05m, vat!.VatRate);

        // The read path returns the saved values, not the defaults.
        var config = await _sut.GetConfigAsync(tenant, CancellationToken.None);
        Assert.Equal(0.95m, config.YieldFactorDefault);
        Assert.Equal(new[] { "6mm", "8mm", "10mm" }, config.Catalogs.ThicknessOptions);
    }

    [DatabaseFact]
    public async Task Config_is_tenant_isolated()
    {
        var tenantA = NewTenant("iso-a");
        var tenantB = NewTenant("iso-b");

        await _sut.SaveConfigAsync(
            tenantA, Guid.NewGuid(), null,
            Request(yield: 0.7m, vat: 0.05m), CancellationToken.None);

        // Tenant B still reads the WPF defaults…
        var configB = await _sut.GetConfigAsync(tenantB, CancellationToken.None);
        Assert.Equal(0.85m, configB.YieldFactorDefault);

        // …and has no row of its own.
        var (pricingB, _) = await StoreGetAsync(tenantB);
        Assert.Null(pricingB);

        // Saving under B never touches A.
        await _sut.SaveConfigAsync(
            tenantB, Guid.NewGuid(), null,
            Request(yield: 0.99m), CancellationToken.None);

        var configA = await _sut.GetConfigAsync(tenantA, CancellationToken.None);
        Assert.Equal(0.7m, configA.YieldFactorDefault);
    }

    [DatabaseFact]
    public async Task Invalid_yield_and_vat_are_refused_before_the_database()
    {
        var tenant = NewTenant("invalid");

        var badYield = await _sut.SaveConfigAsync(
            tenant, Guid.NewGuid(), null, Request(yield: 0m), CancellationToken.None);
        Assert.False(badYield.Success);
        Assert.Equal(PricingErrorCode.ValidationFailed, badYield.ErrorCode);

        var badVat = await _sut.SaveConfigAsync(
            tenant, Guid.NewGuid(), null, Request(vat: 1.5m), CancellationToken.None);
        Assert.False(badVat.Success);

        // Nothing was written by the refused requests.
        var (pricing, _) = await StoreGetAsync(tenant);
        Assert.Null(pricing);
    }

    [DatabaseFact]
    public void Preview_runs_the_wpf_rate_rules_end_to_end()
    {
        // The business's DGU+LAM chain through the service the controller
        // calls — 344.75 with no warnings.
        var d = PricingDefaults.DguLamPanelDefaults.Instance;
        var preview = _sut.Preview(new PricingPreviewRequest(
            "DGU+LAM", null, null, null,
            new DguLamParameters(
                d.OuterBasePrice, d.LamOuterBasePrice, d.LamInnerBasePrice,
                d.OuterCuttingCost, d.LamOuterCuttingCost, d.LamInnerCuttingCost,
                d.OuterTemperingCost, d.LamOuterTemperingCost, d.LamInnerTemperingCost,
                d.InterlayerSellPrice, d.InterlayerType, d.SpacerPrice,
                d.YieldFactor, d.MarginProfitPercent)));

        Assert.True(preview.Success, preview.Message);
        Assert.Equal(344.75m, preview.Value!.RatePerSqm);
        Assert.Equal(287.29m, preview.Value.CostBasisPerSqm);
        Assert.Empty(preview.Value.Warnings);

        // Yield fallback + unpriced SGP interlayer — both WPF warnings.
        var warned = _sut.Preview(new PricingPreviewRequest(
            "DGU+LAM", null, null, null,
            new DguLamParameters(
                48m, 25m, 25m, 10m, 10m, 10m, 12m, 10m, 10m,
                0m, "SGP", 0m, 0m, 20m)));

        Assert.True(warned.Success);
        Assert.Equal(2, warned.Value!.Warnings.Count);
    }

    /// <summary>Direct store read (bypasses the service) — the same
    /// AsNoTracking lesson as Phase 5: read-backs never reuse a tracked
    /// context.</summary>
    private async Task<(TenantPricingConfig?, TenantVatConfig?)> StoreGetAsync(Guid tenantId)
    {
        var factory = new SaasDbContextFactory(_fixture.AppDb);
        var store = new PricingConfigStore(factory);
        return await store.GetAsync(tenantId, CancellationToken.None);
    }
}
