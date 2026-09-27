using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Domain.Proforma;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// Step 6 (Other Charges) against live PostgreSQL: the server-side
/// derivation of metric quantities and amounts from the SAVED Step 5
/// grid, the wastage sources/optimization formula, whole-section
/// replace, the subtotal landing on the invoice, and tenant isolation.
/// </summary>
[Collection("database")]
public sealed class Step6FlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly ProformaService _sut;

    public Step6FlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        var specs = new SpecificationsService(
            new SpecificationsStore(factory), audit);
        _sut = new ProformaService(new ProformaStore(factory), audit, specs);
    }

    private Task<ProformaResult<ProformaDto>> CreateAsync(Guid tenant) =>
        _sut.CreateDraftAsync(
            tenant, Guid.NewGuid(), null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None);

    /// <summary>A DGU grid: SQM1 6.00 / SQM2 2.43 / LM1 18.00, with the
    /// Step 4 provenance (colors + stock sheets) the sources read.</summary>
    private static SaveSpecGroupsRequest Grid() => new(
        new List<SpecGroupRequest>
        {
            new(1, "DGU", "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass",
                46m, "formula", new List<SpecGroupItemRequest>
            {
                new(1, "Front", 1000, 2000, 900, 900, 3, null, ""),
            },
            OuterGlassColor: "HD Grey", InnerGlassColor: "Clear",
            OuterSheetWidthMm: 3210, OuterSheetHeightMm: 2250,
            InnerSheetWidthMm: 3210, InnerSheetHeightMm: 2250,
            OuterSheetPricePerSqm: 52m, InnerSheetPricePerSqm: 25m),
        });

    [DatabaseFact]
    public async Task Step6_round_trips_with_server_derived_amounts()
    {
        var tenant = _fixture.CreateTenant("Step6");

        var invoice = (await CreateAsync(tenant)).Value!;
        var grid = await _sut.SaveSpecGroupsAsync(
            tenant, invoice.Id, Guid.NewGuid(), null, Grid(), CancellationToken.None);
        Assert.True(grid.Success, grid.Message);

        var saved = await _sut.SaveStep6Async(
            tenant, invoice.Id, Guid.NewGuid(), null, new SaveStep6Request(
                new List<OtherChargeRowRequest>
                {
                    // SQM1 = 6.00 → 6 × 45 = 270.00
                    new("ARGON_GAS", "Argon Gas (Sqm)", "", "Sqm", true,
                        "SQM1", new List<int> { 1 }, 45m),
                    // Silicon bed on LM1 = 18.00 × 6 = 108.00
                    new("SILICON_BED", "Silicon bed (lm)", "", "Lm", true,
                        "LM1", new List<int> { 1 }, 6m),
                },
                new List<WastageRowRequest>
                {
                    // Optimization: 52 ÷ 78% − 52 ÷ 85% = 5.490196/sqm on
                    // 8.43 sqm → 46.28. The client-supplied rate (0) must
                    // be replaced by the DERIVED rate.
                    new(true, "HD Grey", 0m, false, true, 52m, 78m, 2,
                        new List<string> { "1-Outer (W1/H1)", "1-Inner (W2/H2)" }),
                },
                new List<CustomChargeRowRequest>
                {
                    new(true, "Crane rental", 2m, "Qty", 250m),
                }),
            CancellationToken.None);

        Assert.True(saved.Success, saved.Message);

        Assert.Equal(2, saved.Value!.Charges.Count);
        Assert.Equal(6.000m, saved.Value.Charges[0].MetricQty);
        Assert.Equal(270.00m, saved.Value.Charges[0].Amount);
        Assert.Equal(18.000m, saved.Value.Charges[1].MetricQty);
        Assert.Equal(108.00m, saved.Value.Charges[1].Amount);

        var wastage = Assert.Single(saved.Value.Wastages);
        Assert.Equal(8.430m, wastage.TotalSqm);
        Assert.Equal(5.490196m, wastage.Rate);
        Assert.Equal(46.28m, wastage.Amount);

        // 270 + 108 + 46.28 + 500 = 924.28 lands on the invoice row.
        Assert.Equal(924.28m, saved.Value.Subtotal);

        // GET round-trips the same section + the derived read model.
        var section = await _sut.GetStep6Async(tenant, invoice.Id, CancellationToken.None);
        Assert.True(section.Success, section.Message);
        Assert.Equal(924.28m, section.Value!.Subtotal);
        Assert.Equal(2, section.Value.Charges.Count);

        var metrics = Assert.Single(section.Value.SpecMetrics);
        Assert.True(metrics.IsDoubleGlass);
        Assert.False(metrics.IsMatchedSameGlass); // HD Grey + Clear
        Assert.Equal(18.00m, metrics.Lm1);

        // The wastage sources carry the group's Step 4 provenance.
        var outer = section.Value.WastageSources.Single(s =>
            s.Key == "1-Outer (W1/H1)");
        Assert.Equal("HD Grey", outer.ColorName);
        Assert.Equal(3210, outer.SheetWidthMm);
        Assert.Equal(52m, outer.PricePerSqm);
    }

    [DatabaseFact]
    public async Task Step6_replace_removes_old_rows()
    {
        var tenant = _fixture.CreateTenant("Step6Replace");

        var invoice = (await CreateAsync(tenant)).Value!;
        await _sut.SaveSpecGroupsAsync(
            tenant, invoice.Id, Guid.NewGuid(), null, Grid(), CancellationToken.None);

        var first = await _sut.SaveStep6Async(
            tenant, invoice.Id, Guid.NewGuid(), null, new SaveStep6Request(
                new List<OtherChargeRowRequest>
                {
                    new("ARGON_GAS", "Argon Gas (Sqm)", "", "Sqm", true,
                        "SQM1", new List<int> { 1 }, 45m),
                },
                null, null),
            CancellationToken.None);
        Assert.True(first.Success, first.Message);

        var second = await _sut.SaveStep6Async(
            tenant, invoice.Id, Guid.NewGuid(), null, new SaveStep6Request(
                new List<OtherChargeRowRequest>
                {
                    new("HOLES", "Holes (Qty)", "", "Qty", true,
                        "QTY", new List<int> { 1 }, 15m),
                },
                new List<WastageRowRequest>
                {
                    new(true, "Clear", 12.5m, true, false, 0m, 0m, 1,
                        new List<string> { "1-Inner (W2/H2)" }),
                },
                null),
            CancellationToken.None);

        Assert.True(second.Success, second.Message);
        Assert.Single(second.Value!.Charges);
        Assert.Equal("HOLES", second.Value.Charges[0].TemplateKey);
        Assert.Single(second.Value.Wastages);
        // QTY metric = 3 rows → 3 × 15 = 45; manual wastage 2.43 × 12.5.
        Assert.Equal(3.000m, second.Value.Charges[0].MetricQty);
        Assert.Equal(45.00m, second.Value.Charges[0].Amount);
        Assert.Equal(2.430m, second.Value.Wastages[0].TotalSqm);
        Assert.Equal(30.38m, second.Value.Wastages[0].Amount); // 2.43 × 12.5 = 30.375 → 30.38
        Assert.Equal(75.38m, second.Value.Subtotal);
    }

    [DatabaseFact]
    public async Task Step6_is_tenant_isolated()
    {
        var tenantA = _fixture.CreateTenant("Step6IsoA");
        var tenantB = _fixture.CreateTenant("Step6IsoB");

        var invoiceA = (await CreateAsync(tenantA)).Value!;
        await _sut.SaveSpecGroupsAsync(
            tenantA, invoiceA.Id, Guid.NewGuid(), null, Grid(), CancellationToken.None);
        var saved = await _sut.SaveStep6Async(
            tenantA, invoiceA.Id, Guid.NewGuid(), null, new SaveStep6Request(
                new List<OtherChargeRowRequest>
                {
                    new("ARGON_GAS", "Argon Gas (Sqm)", "", "Sqm", true,
                        "SQM1", new List<int> { 1 }, 45m),
                }, null, null),
            CancellationToken.None);
        Assert.True(saved.Success, saved.Message);

        // Tenant B cannot read A's section (and its own invoice has none).
        var invoiceB = (await CreateAsync(tenantB)).Value!;
        var sectionB = await _sut.GetStep6Async(tenantB, invoiceB.Id, CancellationToken.None);

        Assert.True(sectionB.Success, sectionB.Message);
        Assert.Empty(sectionB.Value!.Charges);
        Assert.Empty(sectionB.Value.WastageSources);
    }
}
