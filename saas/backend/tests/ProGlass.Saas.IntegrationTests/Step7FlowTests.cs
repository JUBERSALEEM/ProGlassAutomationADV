using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Domain.Proforma;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// Step 7 (financial summary) against live PostgreSQL: the waterfall over
/// the saved Step 5/Step 6 subtotals (BUG-56 discount clamp, BUG-58 VAT on
/// the discounted value), the round-to-target solver, the persisted
/// operator inputs, and tenant isolation.
/// </summary>
[Collection("database")]
public sealed class Step7FlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly ProformaService _sut;

    public Step7FlowTests(DatabaseFixture fixture)
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

    /// <summary>
    /// 480 glass (4 SQM × 120) + 50 delivery = 530 net. The Step 9 grid
    /// shape is irrelevant here — only the subtotals matter.
    /// </summary>
    private async Task<ProformaInvoice> SeededInvoiceAsync(Guid tenant)
    {
        var invoice = (await CreateAsync(tenant)).Value!;
        var grid = await _sut.SaveSpecGroupsAsync(
            tenant, invoice.Id, Guid.NewGuid(), null, new SaveSpecGroupsRequest(
                new List<SpecGroupRequest>
                {
                    new(1, "SGU", "6mm Clear FT Glass",
                        120m, "formula", new List<SpecGroupItemRequest>
                    {
                        new(1, "Front", 1000, 2000, 0, 0, 2, null, ""),
                    }),
                }), CancellationToken.None);
        Assert.True(grid.Success, grid.Message);
        var step6 = await _sut.SaveStep6Async(
            tenant, invoice.Id, Guid.NewGuid(), null, new SaveStep6Request(
                new List<OtherChargeRowRequest>
                {
                    new(ChargeTemplates.DeliveryKey, "Delivery (per trip)", "", "Qty",
                        true, "QTY", new List<int>(), 50m),
                }, null, null),
            CancellationToken.None);
        Assert.True(step6.Success, step6.Message);
        return invoice;
    }

    [DatabaseFact]
    public async Task Step7_defaults_recompute_the_waterfall_over_the_subtotals()
    {
        var tenant = _fixture.CreateTenant("Step7");
        var created = await SeededInvoiceAsync(tenant);

        var saved = await _sut.SaveStep7Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new SaveStep7Request(null, null, null, null), CancellationToken.None);
        Assert.True(saved.Success, saved.Message);

        // WPF defaults: Percentage 0, VAT applicable at 5%.
        var dto = saved.Value!;
        Assert.Equal("Percentage", dto.DiscountType);
        Assert.Equal(0m, dto.DiscountValue);
        Assert.True(dto.IsVatApplicable);
        Assert.Equal(5m, dto.VatPercent);
        Assert.Equal(530m, dto.NetSubtotal);
        Assert.Equal(0m, dto.DiscountAmount);
        Assert.Equal(530m, dto.SubtotalBeforeVat);
        Assert.Equal(26.50m, dto.VatAmount);
        Assert.Equal(556.50m, dto.GrandTotalVatInclusive);

        // GET returns the persisted inputs + recomputed waterfall.
        var section = await _sut.GetStep7Async(tenant, created.Id, CancellationToken.None);
        Assert.True(section.Success, section.Message);
        Assert.Equal(556.50m, section.Value!.GrandTotalVatInclusive);
    }

    [DatabaseFact]
    public async Task Step7_percentage_discount_comes_off_before_vat()
    {
        var tenant = _fixture.CreateTenant("Step7Disc");
        var created = await SeededInvoiceAsync(tenant);

        var saved = await _sut.SaveStep7Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new SaveStep7Request("Percentage", 5m, null, null), CancellationToken.None);
        Assert.True(saved.Success, saved.Message);

        // 530 − 26.50 = 503.50 before VAT; VAT on 503.50 = 25.18.
        var dto = saved.Value!;
        Assert.Equal(26.50m, dto.DiscountAmount);
        Assert.Equal(503.50m, dto.SubtotalBeforeVat);
        Assert.Equal(25.18m, dto.VatAmount);
        Assert.Equal(528.68m, dto.GrandTotalVatInclusive);

        // A follow-up save with omitted inputs keeps the saved knobs.
        var again = await _sut.SaveStep7Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new SaveStep7Request(null, null, null, null), CancellationToken.None);
        Assert.True(again.Success, again.Message);
        Assert.Equal(5m, again.Value!.DiscountValue);
        Assert.Equal(528.68m, again.Value.GrandTotalVatInclusive);
    }

    [DatabaseFact]
    public async Task Step7_fixed_discount_clamps_to_the_net()
    {
        var tenant = _fixture.CreateTenant("Step7Clamp");
        var created = await SeededInvoiceAsync(tenant);

        var saved = await _sut.SaveStep7Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new SaveStep7Request("FixedAmount", 999m, null, null), CancellationToken.None);
        Assert.True(saved.Success, saved.Message);

        // BUG-56: a 999 AED discount on a 530 net clamps to 530.
        Assert.Equal(530m, saved.Value!.DiscountAmount);
        Assert.Equal(0m, saved.Value.VatAmount);
        Assert.Equal(0m, saved.Value.GrandTotalVatInclusive);
    }

    [DatabaseFact]
    public async Task Step7_rejects_a_negative_discount_and_bad_type()
    {
        var tenant = _fixture.CreateTenant("Step7Bad");
        var created = await SeededInvoiceAsync(tenant);

        var negative = await _sut.SaveStep7Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new SaveStep7Request(null, -1m, null, null), CancellationToken.None);
        Assert.False(negative.Success);
        Assert.Equal(ProformaErrorCode.ValidationFailed, negative.ErrorCode);

        var badType = await _sut.SaveStep7Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new SaveStep7Request("LoyaltyPoints", null, null, null), CancellationToken.None);
        Assert.False(badType.Success);

        var badVat = await _sut.SaveStep7Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new SaveStep7Request(null, null, null, 101m), CancellationToken.None);
        Assert.False(badVat.Success);
    }

    [DatabaseFact]
    public async Task Round_to_target_switches_to_a_fixed_discount_and_lands()
    {
        var tenant = _fixture.CreateTenant("Step7Round");
        var created = await SeededInvoiceAsync(tenant);
        await _sut.SaveStep7Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new SaveStep7Request(null, null, null, null), CancellationToken.None);

        var rounded = await _sut.RoundToStep7Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new RoundToStep7Request(500m), CancellationToken.None);
        Assert.True(rounded.Success, rounded.Message);

        // beforeVat = Round(500/1.05) = 476.19 → discount 53.81,
        // grand = Round(476.19 + 23.81) = 500.00.
        Assert.Equal(53.81m, rounded.Value!.SolvedDiscount);
        Assert.True(rounded.Value.Changed);
        Assert.Equal("FixedAmount", rounded.Value.Summary.DiscountType);
        Assert.Equal(500.00m, rounded.Value.Summary.GrandTotalVatInclusive);

        // The solved discount is PERSISTED (GET agrees).
        var section = await _sut.GetStep7Async(tenant, created.Id, CancellationToken.None);
        Assert.Equal("FixedAmount", section.Value!.DiscountType);
        Assert.Equal(53.81m, section.Value.DiscountValue);
        Assert.Equal(500.00m, section.Value.GrandTotalVatInclusive);

        // Re-rounding to the same target is stable.
        var again = await _sut.RoundToStep7Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new RoundToStep7Request(500m), CancellationToken.None);
        Assert.True(again.Success, again.Message);
        Assert.Equal(53.81m, again.Value!.SolvedDiscount);
        Assert.False(again.Value.Changed);
    }

    [DatabaseFact]
    public async Task Round_to_target_refuses_to_round_up()
    {
        var tenant = _fixture.CreateTenant("Step7RoundUp");
        var created = await SeededInvoiceAsync(tenant);
        await _sut.SaveStep7Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new SaveStep7Request(null, null, null, null), CancellationToken.None);

        // The undiscounted grand is 556.50 — the tool only discounts down.
        var up = await _sut.RoundToStep7Async(
            tenant, created.Id, Guid.NewGuid(), null,
            new RoundToStep7Request(600m), CancellationToken.None);
        Assert.False(up.Success);
        Assert.Equal(ProformaErrorCode.ValidationFailed, up.ErrorCode);
        Assert.Contains("Cannot round up", up.Message);
    }

    [DatabaseFact]
    public async Task Step7_is_tenant_isolated()
    {
        var tenantA = _fixture.CreateTenant("Step7IsoA");
        var tenantB = _fixture.CreateTenant("Step7IsoB");

        var invoiceA = await SeededInvoiceAsync(tenantA);
        var saved = await _sut.SaveStep7Async(
            tenantA, invoiceA.Id, Guid.NewGuid(), null,
            new SaveStep7Request(null, null, null, null), CancellationToken.None);
        Assert.True(saved.Success, saved.Message);

        var fromB = await _sut.GetStep7Async(tenantB, invoiceA.Id, CancellationToken.None);
        Assert.False(fromB.Success);
        Assert.Equal(ProformaErrorCode.NotFound, fromB.ErrorCode);

        var invoiceB = (await CreateAsync(tenantB)).Value!;
        var own = await _sut.GetStep7Async(tenantB, invoiceB.Id, CancellationToken.None);
        Assert.True(own.Success, own.Message);
        // A brand-new invoice reports the WPF defaults.
        Assert.Equal("Percentage", own.Value!.DiscountType);
        Assert.True(own.Value.IsVatApplicable);
        Assert.Equal(5m, own.Value.VatPercent);
    }
}
