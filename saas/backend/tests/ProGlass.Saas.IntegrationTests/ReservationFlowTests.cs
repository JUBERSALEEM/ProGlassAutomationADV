using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.JobOrders;
using ProGlass.Saas.Application.Production;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Application.Stock;
using ProGlass.Saas.Domain.Proforma;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// Material reservations (Phase 9c — WPF MaterialReservationService)
/// against live PostgreSQL. Sheet 3210×2250×8 Bronze with stock 10/used 2
/// → balance 8; a job with one SGU spec row.
/// </summary>
[Collection("database")]
public sealed class ReservationFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly ReservationService _sut;
    private readonly StockService _stock;
    private readonly JobOrderService _jobs;
    private readonly ProformaService _proformas;

    public ReservationFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        var specs = new SpecificationsService(
            new SpecificationsStore(factory), audit);
        _sut = new ReservationService(new ReservationStore(factory), audit);
        _stock = new StockService(new StockStore(factory), audit);
        _jobs = new JobOrderService(new JobOrderStore(factory), audit);
        _proformas = new ProformaService(new ProformaStore(factory), audit, specs);
    }

    private async Task<(Guid TenantId, Guid JobOrderId, Guid SheetId)> SetupAsync(
        string prefix)
    {
        var tenant = _fixture.CreateTenant(prefix);

        var sheet = await _stock.CreateSheetAsync(tenant, Guid.NewGuid(),
            new GlassSheetRequest("Float", 8, "Bronze", 3210, 2250, "GlasImport",
                95m, 140m, Stock: 10, Used: 2),
            null, CancellationToken.None);
        Assert.True(sheet.Success, sheet.Message);

        var pi = (await _proformas.CreateDraftAsync(
            tenant, Guid.NewGuid(), null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None)).Value!;
        var grid = new SaveSpecGroupsRequest(new List<SpecGroupRequest>
        {
            new(1, "SGU", "6mm Clear Annealed Glass", 46m, "manual",
                new List<SpecGroupItemRequest>
                {
                    new(1, "Front", 1000, 2000, 0, 0, 4, null, ""),
                }),
        });
        await _proformas.SaveSpecGroupsAsync(
            tenant, pi.Id, Guid.NewGuid(), null, grid, CancellationToken.None);
        var job = await _jobs.CreateFromProformaAsync(
            tenant, Guid.NewGuid(), new CreateJobOrderRequest(pi.Id),
            null, CancellationToken.None);
        Assert.True(job.Success, job.Message);

        return (tenant, job.Value!.Id, sheet.Value!.Id);
    }

    private Task<ProductionResult<ReservationDto>> ReserveAsync(
        Guid tenant, Guid job, Guid sheet, int qty, int spec = 1, int serial = 1) =>
        _sut.ReserveAsync(tenant, Guid.NewGuid(), new ReserveRequest(
            job, sheet, spec, serial, "Front", "6mm Clear Annealed Glass",
            null, qty), null, CancellationToken.None);

    [Fact]
    public async Task Reserve_is_idempotent_on_the_deterministic_key()
    {
        var (tenant, job, sheet) = await SetupAsync("Rsv1");

        var first = await ReserveAsync(tenant, job, sheet, 3);
        Assert.True(first.Success, first.Message);
        Assert.Equal(ReservationStatuses.Reserved, first.Value!.Status);
        Assert.Equal(3, first.Value.Quantity);
        Assert.Equal("3210×2250mm 8mm Bronze", first.Value.StockSizeLabel);
        Assert.Equal(
            ReservationKeys.Build("", 1, 1, sheet, null).Split('|').Length,
            first.Value.ReservationKey.Split('|').Length);

        // Same key → same row, no duplicate.
        var second = await ReserveAsync(tenant, job, sheet, 3);
        Assert.True(second.Success, second.Message);
        Assert.Equal(first.Value.Id, second.Value!.Id);

        var byJob = await _sut.ListForJobAsync(tenant, job, CancellationToken.None);
        Assert.Single(byJob.Value!);
    }

    [Fact]
    public async Task Availability_is_balance_minus_other_active_holds()
    {
        var (tenant, job, sheet) = await SetupAsync("Rsv2");

        // Balance 8. Hold 5 on spec 1.
        var hold = await ReserveAsync(tenant, job, sheet, 5);
        Assert.True(hold.Success, hold.Message);

        // Another line (spec 2) may only take what is left: 8 − 5 = 3.
        var tooMuch = await ReserveAsync(tenant, job, sheet, 4, spec: 2, serial: 2);
        Assert.False(tooMuch.Success);
        Assert.Equal(
            "Not enough stock. Available: 3, Requested: 4", tooMuch.Message);

        var fits = await ReserveAsync(tenant, job, sheet, 3, spec: 2, serial: 2);
        Assert.True(fits.Success, fits.Message);

        // The availability read model exposes the same math.
        var availability = await _sut.GetAvailabilityAsync(
            tenant, sheet, CancellationToken.None);
        Assert.True(availability.Success, availability.Message);
        Assert.Equal(8, availability.Value!.Balance);
        Assert.Equal(8, availability.Value.ActiveHolds);
        Assert.Equal(0, availability.Value.Available);
    }

    [Fact]
    public async Task Release_frees_the_hold_and_reactivate_rechecks()
    {
        var (tenant, job, sheet) = await SetupAsync("Rsv3");

        var hold = await ReserveAsync(tenant, job, sheet, 6);
        Assert.True(hold.Success, hold.Message);

        var released = await _sut.ReleaseAsync(
            tenant, hold.Value!.Id, Guid.NewGuid(),
            new ReservationActionRequest("plan changed"), null, CancellationToken.None);
        Assert.True(released.Success, released.Message);
        Assert.Equal(ReservationStatuses.Released, released.Value!.Status);
        Assert.Equal("plan changed", released.Value.ReleaseReason);

        // Idempotent.
        var again = await _sut.ReleaseAsync(
            tenant, hold.Value.Id, Guid.NewGuid(), new ReservationActionRequest(),
            null, CancellationToken.None);
        Assert.True(again.Success, again.Message);

        // The freed stock is available again.
        var availability = await _sut.GetAvailabilityAsync(
            tenant, sheet, CancellationToken.None);
        Assert.Equal(8, availability.Value!.Available);

        // Re-reserving the same key reactivates the released row.
        var reactivated = await ReserveAsync(tenant, job, sheet, 8);
        Assert.True(reactivated.Success, reactivated.Message);
        Assert.Equal(hold.Value.Id, reactivated.Value!.Id);
        Assert.Equal(ReservationStatuses.Reserved, reactivated.Value.Status);
        Assert.Equal(8, reactivated.Value.Quantity);
    }

    [Fact]
    public async Task Consume_is_terminal_and_blocks_release_and_cancel()
    {
        var (tenant, job, sheet) = await SetupAsync("Rsv4");

        var hold = await ReserveAsync(tenant, job, sheet, 2);
        Assert.True(hold.Success, hold.Message);

        var consumed = await _sut.ConsumeAsync(
            tenant, hold.Value!.Id, Guid.NewGuid(), new ReservationActionRequest(),
            null, CancellationToken.None);
        Assert.True(consumed.Success, consumed.Message);
        Assert.Equal(ReservationStatuses.Consumed, consumed.Value!.Status);

        // Idempotent.
        var again = await _sut.ConsumeAsync(
            tenant, hold.Value.Id, Guid.NewGuid(), new ReservationActionRequest(),
            null, CancellationToken.None);
        Assert.True(again.Success, again.Message);

        var release = await _sut.ReleaseAsync(
            tenant, hold.Value.Id, Guid.NewGuid(), new ReservationActionRequest(),
            null, CancellationToken.None);
        Assert.False(release.Success);
        Assert.Equal("Cannot release consumed material.", release.Message);

        var cancel = await _sut.CancelAsync(
            tenant, hold.Value.Id, Guid.NewGuid(), new ReservationActionRequest(),
            null, CancellationToken.None);
        Assert.False(cancel.Success);
        Assert.Equal("Cannot cancel consumed material.", cancel.Message);

        // Consumed holds stay consumed in availability.
        var availability = await _sut.GetAvailabilityAsync(
            tenant, sheet, CancellationToken.None);
        Assert.Equal(8, availability.Value!.Available);
    }

    [Fact]
    public async Task Cancel_is_idempotent_and_guards_quantity()
    {
        var (tenant, job, sheet) = await SetupAsync("Rsv5");

        var zero = await ReserveAsync(tenant, job, sheet, 0);
        Assert.False(zero.Success);
        Assert.Equal("Quantity must be > 0.", zero.Message);

        var hold = await ReserveAsync(tenant, job, sheet, 1);
        Assert.True(hold.Success, hold.Message);

        var cancelled = await _sut.CancelAsync(
            tenant, hold.Value!.Id, Guid.NewGuid(),
            new ReservationActionRequest("wrong sheet"), null, CancellationToken.None);
        Assert.True(cancelled.Success, cancelled.Message);
        Assert.Equal(ReservationStatuses.Cancelled, cancelled.Value!.Status);

        var again = await _sut.CancelAsync(
            tenant, hold.Value.Id, Guid.NewGuid(), new ReservationActionRequest(),
            null, CancellationToken.None);
        Assert.True(again.Success, again.Message);
    }

    [Fact]
    public async Task Tenant_isolation_hides_other_tenants_reservations()
    {
        var (tenant, job, sheet) = await SetupAsync("Rsv6a");
        var hold = await ReserveAsync(tenant, job, sheet, 1);
        Assert.True(hold.Success, hold.Message);

        var other = _fixture.CreateTenant("Rsv6b");
        var foreignAvailability = await _sut.GetAvailabilityAsync(
            other, sheet, CancellationToken.None);
        Assert.False(foreignAvailability.Success);
        var foreignJob = await _sut.ListForJobAsync(other, job, CancellationToken.None);
        Assert.False(foreignJob.Success);
    }
}
