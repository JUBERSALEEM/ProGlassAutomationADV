using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// Step 9 (Profit &amp; Loss) against live PostgreSQL: the server-side
/// recalculation from the SAVED Steps 4–6 state + the tenant Sheet Store,
/// the KPI snapshot landing on the invoice, whole-section replace, the
/// lifecycle gate, and tenant isolation.
/// </summary>
[Collection("database")]
public sealed class Step9FlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly ProformaService _sut;

    public Step9FlowTests(DatabaseFixture fixture)
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

    /// <summary>Seeds the tenant Sheet Store with one Clear 6mm jumbo.</summary>
    private void SeedSheet(Guid tenant)
    {
        using var db = _fixture.CreateContext(tenant);
        db.GlassSheets.Add(new GlassSheet
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            Category = "Glass",
            ThicknessMm = 6,
            Color = "Clear",
            WidthMm = 3210,
            HeightMm = 2250,
            Supplier = "Test Supplier",
            PurchasePricePerSqm = 90m,
            SellPricePerSqm = 110m
        });
        db.SaveChanges();
    }

    /// <summary>
    /// An SGU grid with the full Step 4 provenance: 1 panel 1000×2000
    /// (2 SQM) × qty 2 → 4 SQM cut area, quoted at 100/sqm with cutting 5
    /// and tempering 8. Glass subtotal = 4 SQM × the group's 120/sqm rate
    /// (the Step 5 save computes it).
    /// </summary>
    private static SaveSpecGroupsRequest Grid() => new(
        new List<SpecGroupRequest>
        {
            new(1, "SGU", "6mm Clear FT Glass",
                120m, "formula", new List<SpecGroupItemRequest>
            {
                new(1, "Front", 1000, 2000, 0, 0, 2, null, ""),
            },
            OuterGlassColor: "Clear",
            OuterSheetWidthMm: 3210, OuterSheetHeightMm: 2250,
            OuterSheetPricePerSqm: 100m,
            Step4CuttingCostPerSqm: 5m, Step4TemperingCostPerSqm: 8m),
        });

    private async Task<ProformaInvoice> SeededInvoiceAsync(Guid tenant)
    {
        SeedSheet(tenant);
        var invoice = (await CreateAsync(tenant)).Value!;
        var grid = await _sut.SaveSpecGroupsAsync(
            tenant, invoice.Id, Guid.NewGuid(), null, Grid(), CancellationToken.None);
        Assert.True(grid.Success, grid.Message);
        // The glass subtotal (4 SQM × 120) + the delivery charge (50) feed
        // the Step 7 grand total, which Step 9 reads as gross revenue:
        // 530 net + 5% VAT = 556.50.
        await _sut.SaveStep6Async(
            tenant, invoice.Id, Guid.NewGuid(), null, new SaveStep6Request(
                new List<OtherChargeRowRequest>
                {
                    new(ChargeTemplates.DeliveryKey, "Delivery (per trip)", "", "Qty",
                        true, "QTY", new List<int>(), 50m),
                }, null, null),
            CancellationToken.None);
        var summary = await _sut.SaveStep7Async(
            tenant, invoice.Id, Guid.NewGuid(), null,
            new SaveStep7Request(null, null, null, null), CancellationToken.None);
        Assert.True(summary.Success, summary.Message);
        Assert.Equal(556.50m, summary.Value!.GrandTotalVatInclusive);
        return invoice;
    }

    [DatabaseFact]
    public async Task Step9_recalculates_and_persists_the_kpi_snapshot()
    {
        var tenant = _fixture.CreateTenant("Step9");
        var created = await SeededInvoiceAsync(tenant);

        // Refresh the invoice the service holds (the Step 6 save bumped
        // its subtotals through the store).
        var saved = await _sut.SaveStep9Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new SaveStep9Request(WastagePercent: 15m, CostAtSellingPrice: false),
            CancellationToken.None);
        Assert.True(saved.Success, saved.Message);

        var dto = saved.Value!;
        Assert.True(dto.HasBeenCalculated);
        Assert.Equal(15m, dto.WastagePercent);
        Assert.False(dto.CostAtSellingPrice);

        // Rows: glass (priced by required area), cutting, tempering,
        // then the invoice-wide delivery TRIP.
        Assert.Equal(4, dto.Rows.Count);
        Assert.Equal("Glass", dto.Rows[0].Kind);
        Assert.Equal(4.60m, dto.Rows[0].Quantity);
        Assert.Equal(460.00m, dto.Rows[0].Amount);
        Assert.Equal("Step 4 purchase price", dto.Rows[0].SheetSource);
        Assert.Equal("Production", dto.Rows[1].Kind);
        Assert.Equal(20.00m, dto.Rows[1].Amount);
        Assert.Equal(32.00m, dto.Rows[2].Amount);
        Assert.Equal("Delivery", dto.Rows[3].Kind);
        Assert.Equal("TRIP", dto.Rows[3].UnitLabel);
        Assert.Equal(50.00m, dto.Rows[3].Amount);

        // KPIs: gross is the Step 7 grand total 556.50; net = 556.50/1.05
        // = 530.00; production = 20 + 32 + 50.
        var kpis = dto.Kpis!;
        Assert.Equal(556.50m, kpis.GrossVatInclusive);
        Assert.Equal(530.00m, kpis.NetExVat);
        Assert.Equal(460.00m, kpis.TotalGlassCost);
        Assert.Equal(102.00m, kpis.TotalProductionCost);
        Assert.Equal(-32.00m, kpis.Profit);
        Assert.True(kpis.IsLoss);
        Assert.Equal(522.60m, kpis.BasisCost);
        Assert.Equal(6.49m, kpis.MarginPercent);
        Assert.Equal(-5.69m, kpis.OverallMarginPercent);
        Assert.Equal(-6.04m, kpis.NetMarginPercent);
        Assert.Equal(0m, kpis.UInsertCost);

        // GET returns the same persisted snapshot.
        var section = await _sut.GetStep9Async(tenant, created.Id, CancellationToken.None);
        Assert.True(section.Success, section.Message);
        Assert.Equal(4, section.Value!.Rows.Count);
        Assert.Equal(-32.00m, section.Value.Kpis!.Profit);

        // Recalculating without inputs keeps the saved operator knobs.
        var again = await _sut.SaveStep9Async(
            tenant, created.Id, Guid.NewGuid(), null, new SaveStep9Request(null, null),
            CancellationToken.None);
        Assert.True(again.Success, again.Message);
        Assert.Equal(15m, again.Value!.WastagePercent);
        Assert.False(again.Value.CostAtSellingPrice);
    }

    [DatabaseFact]
    public async Task Step9_replaces_the_previous_row_set()
    {
        var tenant = _fixture.CreateTenant("Step9Replace");
        var created = await SeededInvoiceAsync(tenant);

        var first = await _sut.SaveStep9Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new SaveStep9Request(15m, false), CancellationToken.None);
        Assert.True(first.Success, first.Message);

        // Drop the delivery row: the recalc must not keep stale rows.
        await _sut.SaveStep6Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new SaveStep6Request(null, null, null), CancellationToken.None);
        var second = await _sut.SaveStep9Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new SaveStep9Request(15m, false), CancellationToken.None);
        Assert.True(second.Success, second.Message);

        Assert.Equal(3, second.Value!.Rows.Count);
        Assert.DoesNotContain(second.Value.Rows, r => r.Kind == "Delivery");
        Assert.Equal(52.00m, second.Value.Kpis!.TotalProductionCost);
    }

    [DatabaseFact]
    public async Task Step9_rejects_wastage_out_of_range()
    {
        var tenant = _fixture.CreateTenant("Step9Range");
        var created = await SeededInvoiceAsync(tenant);

        var bad = await _sut.SaveStep9Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new SaveStep9Request(96m, null), CancellationToken.None);
        Assert.False(bad.Success);
        Assert.Equal(ProformaErrorCode.ValidationFailed, bad.ErrorCode);

        var section = await _sut.GetStep9Async(tenant, created.Id, CancellationToken.None);
        Assert.False(section.Value!.HasBeenCalculated);
    }

    [DatabaseFact]
    public async Task Step9_is_tenant_isolated()
    {
        var tenantA = _fixture.CreateTenant("Step9IsoA");
        var tenantB = _fixture.CreateTenant("Step9IsoB");

        var invoiceA = await SeededInvoiceAsync(tenantA);
        var saved = await _sut.SaveStep9Async(
            tenantA, invoiceA.Id, Guid.NewGuid(), null,
            new SaveStep9Request(15m, false), CancellationToken.None);
        Assert.True(saved.Success, saved.Message);

        // Tenant B sees nothing of A's section.
        var invoiceB = (await CreateAsync(tenantB)).Value!;
        var fromB = await _sut.GetStep9Async(tenantB, invoiceA.Id, CancellationToken.None);
        Assert.False(fromB.Success);
        Assert.Equal(ProformaErrorCode.NotFound, fromB.ErrorCode);

        var own = await _sut.GetStep9Async(tenantB, invoiceB.Id, CancellationToken.None);
        Assert.True(own.Success, own.Message);
        Assert.Empty(own.Value!.Rows);
        Assert.False(own.Value.HasBeenCalculated);
        // WPF defaults surface before the first calculation.
        Assert.Equal(15m, own.Value.WastagePercent);
        Assert.True(own.Value.CostAtSellingPrice);
    }
}
