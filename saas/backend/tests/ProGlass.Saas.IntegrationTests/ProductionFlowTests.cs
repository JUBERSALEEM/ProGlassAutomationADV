using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.JobOrders;
using ProGlass.Saas.Application.Production;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// The Production core (Phase 9b — WPF ProductionDistributionEngine +
/// ProductionReleaseService + the floor lifecycle) against live
/// PostgreSQL. Grid: one DGU spec (FT outer + annealed inner, 1000×2000 /
/// 900×1800 ×4) → 5 work items (2 cutting, 1 tempering, pairing,
/// assembly).
/// </summary>
[Collection("database")]
public sealed class ProductionFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly ProductionService _sut;
    private readonly JobOrderService _jobs;
    private readonly ProformaService _proformas;

    public ProductionFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        var specs = new SpecificationsService(
            new SpecificationsStore(factory), audit);
        _sut = new ProductionService(new ProductionStore(factory), audit);
        _jobs = new JobOrderService(new JobOrderStore(factory), audit);
        _proformas = new ProformaService(new ProformaStore(factory), audit, specs);
    }

    private async Task<(Guid TenantId, Guid JobOrderId, string JobNo)> CreateJobAsync(
        string prefix)
    {
        var tenant = _fixture.CreateTenant(prefix);
        var pi = (await _proformas.CreateDraftAsync(
            tenant, Guid.NewGuid(), null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None)).Value!;

        var grid = new SaveSpecGroupsRequest(new List<SpecGroupRequest>
        {
            new(1, "DGU", "6mm Clear FT Glass + 12mm ASP + 6mm Green Annealed Glass",
                119.40m, "formula", new List<SpecGroupItemRequest>
                {
                    new(1, "Front", 1000, 2000, 900, 1800, 4, null, ""),
                }),
        });
        var saved = await _proformas.SaveSpecGroupsAsync(
            tenant, pi.Id, Guid.NewGuid(), null, grid, CancellationToken.None);
        Assert.True(saved.Success, saved.Message);

        var job = await _jobs.CreateFromProformaAsync(
            tenant, Guid.NewGuid(), new CreateJobOrderRequest(pi.Id),
            null, CancellationToken.None);
        Assert.True(job.Success, job.Message);
        return (tenant, job.Value!.Id, job.Value.JobOrderNo);
    }

    [Fact]
    public async Task Plan_is_pure_and_deterministic()
    {
        var (tenant, jobId, jobNo) = await CreateJobAsync("Pr1");

        var plan = await _sut.PlanAsync(tenant, jobId, CancellationToken.None);
        Assert.True(plan.Success, plan.Message);
        Assert.Equal(jobNo, plan.Value!.JobOrderNo);
        Assert.Empty(plan.Value.Warnings);
        Assert.Equal(5, plan.Value.Items.Count);
        Assert.Equal(2, plan.Value.Items.Count(i => i.Stage == "Cutting"));
        Assert.Single(plan.Value.Items.Where(i => i.Stage == "Tempering"));

        // Nothing persisted by planning.
        var floor = await _sut.ListForJobAsync(tenant, jobId, CancellationToken.None);
        Assert.Empty(floor.Value!);
    }

    [Fact]
    public async Task Release_persists_and_rerelease_preserves_statuses()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pr2");

        var release = await _sut.ReleaseAsync(
            tenant, Guid.NewGuid(), jobId, null, CancellationToken.None);
        Assert.True(release.Success, release.Message);
        Assert.Equal(5, release.Value!.PlannedItems);
        Assert.Equal(5, release.Value.InsertedItems);
        Assert.Equal(0, release.Value.ExistingItems);

        // Floor progress on the outer cut.
        var floor = await _sut.ListForJobAsync(tenant, jobId, CancellationToken.None);
        var outer = floor.Value!.Single(i =>
            i.Stage == "Cutting" && i.Position == "Outer");
        var staged = await _sut.RecordStageAsync(
            tenant, outer.Id, Guid.NewGuid(),
            new StageProgressRequest(4), null, CancellationToken.None);
        Assert.True(staged.Success, staged.Message);
        Assert.Equal("Completed", staged.Value!.Status);

        // Re-release: the SAME unique keys exist → nothing inserted, the
        // completed status survives.
        var reRelease = await _sut.ReleaseAsync(
            tenant, Guid.NewGuid(), jobId, null, CancellationToken.None);
        Assert.True(reRelease.Success, reRelease.Message);
        Assert.Equal(5, reRelease.Value!.PlannedItems);
        Assert.Equal(0, reRelease.Value!.InsertedItems);
        Assert.Equal(5, reRelease.Value!.ExistingItems);

        var after = await _sut.ListForJobAsync(tenant, jobId, CancellationToken.None);
        var outerAfter = after.Value!.Single(i =>
            i.Stage == "Cutting" && i.Position == "Outer");
        Assert.Equal("Completed", outerAfter.Status);
        Assert.Equal(4, outerAfter.QtyDone);
    }

    [Fact]
    public async Task Stage_progress_autostatuses_and_logs_events()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pr3");
        await _sut.ReleaseAsync(tenant, Guid.NewGuid(), jobId, null, CancellationToken.None);
        var floor = await _sut.ListForJobAsync(tenant, jobId, CancellationToken.None);
        var item = floor.Value!.Single(i => i.Stage == "Pairing");

        var partial = await _sut.RecordStageAsync(
            tenant, item.Id, Guid.NewGuid(), new StageProgressRequest(1),
            null, CancellationToken.None);
        Assert.True(partial.Success, partial.Message);
        Assert.Equal("InProgress", partial.Value!.Status);

        var events = await _sut.GetEventsAsync(tenant, item.Id, CancellationToken.None);
        Assert.Single(events.Value!);
        Assert.Equal("Stage", events.Value![0].EventType);

        // Out-of-range qty is refused.
        var tooMuch = await _sut.RecordStageAsync(
            tenant, item.Id, Guid.NewGuid(), new StageProgressRequest(9),
            null, CancellationToken.None);
        Assert.False(tooMuch.Success);
        Assert.Contains("outside 0–4", tooMuch.Message);
    }

    [Fact]
    public async Task Qc_follows_the_wpf_status_mapping()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pr4");
        await _sut.ReleaseAsync(tenant, Guid.NewGuid(), jobId, null, CancellationToken.None);
        var floor = await _sut.ListForJobAsync(tenant, jobId, CancellationToken.None);
        var item = floor.Value!.Single(i => i.Stage == "Pairing");

        // Fail before any progress: status QcFailed, QtyDone untouched.
        var failed = await _sut.RecordQcAsync(
            tenant, item.Id, Guid.NewGuid(),
            new QcRequest("Failed", "chip on edge"), null, CancellationToken.None);
        Assert.True(failed.Success, failed.Message);
        Assert.Equal("QcFailed", failed.Value!.Status);
        Assert.Equal(0, failed.Value.QtyDone);

        // Complete the work, then pass: Completed.
        await _sut.RecordStageAsync(tenant, item.Id, Guid.NewGuid(),
            new StageProgressRequest(4), null, CancellationToken.None);
        var passed = await _sut.RecordQcAsync(
            tenant, item.Id, Guid.NewGuid(), new QcRequest("Passed"),
            null, CancellationToken.None);
        Assert.True(passed.Success, passed.Message);
        Assert.Equal("Completed", passed.Value!.Status);

        // Rework marks the status.
        var rework = await _sut.RecordQcAsync(
            tenant, item.Id, Guid.NewGuid(),
            new QcRequest("Rework", "size off"), null, CancellationToken.None);
        Assert.True(rework.Success, rework.Message);
        Assert.Equal("Rework", rework.Value!.Status);

        var events = await _sut.GetEventsAsync(tenant, item.Id, CancellationToken.None);
        Assert.Equal(2, events.Value!.Count(e =>
            e.EventType is "QcFailed" or "QcPassed"));
    }

    [Fact]
    public async Task Cancel_is_idempotent_and_locks_the_item()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pr5");
        await _sut.ReleaseAsync(tenant, Guid.NewGuid(), jobId, null, CancellationToken.None);
        var floor = await _sut.ListForJobAsync(tenant, jobId, CancellationToken.None);
        var item = floor.Value!.Single(
            i => i.Stage == "Cutting" && i.Position == "Inner");

        var cancelled = await _sut.CancelAsync(
            tenant, item.Id, Guid.NewGuid(),
            new CancelPieceRequest("wrong size"), null, CancellationToken.None);
        Assert.True(cancelled.Success, cancelled.Message);
        Assert.True(cancelled.Value!.IsCancelled);
        Assert.Equal("Cancelled", cancelled.Value.Status);
        Assert.Equal("wrong size", cancelled.Value.CancelReason);

        // Idempotent.
        var again = await _sut.CancelAsync(
            tenant, item.Id, Guid.NewGuid(), new CancelPieceRequest(),
            null, CancellationToken.None);
        Assert.True(again.Success, again.Message);

        // Cancelled work cannot be staged or QC'd (WPF messages).
        var staged = await _sut.RecordStageAsync(
            tenant, item.Id, Guid.NewGuid(), new StageProgressRequest(1),
            null, CancellationToken.None);
        Assert.False(staged.Success);
        Assert.Equal("Cancelled work cannot be updated.", staged.Message);
        var qc = await _sut.RecordQcAsync(
            tenant, item.Id, Guid.NewGuid(), new QcRequest("Passed"),
            null, CancellationToken.None);
        Assert.False(qc.Success);
        Assert.Equal("Cancelled work cannot be QC'd.", qc.Message);
    }

    [Fact]
    public async Task Breakage_records_an_event_with_guards()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pr6");
        await _sut.ReleaseAsync(tenant, Guid.NewGuid(), jobId, null, CancellationToken.None);
        var floor = await _sut.ListForJobAsync(tenant, jobId, CancellationToken.None);
        var item = floor.Value!.Single(i => i.Stage == "Cutting" && i.Position == "Outer");

        var noQty = await _sut.RecordBreakageAsync(
            tenant, item.Id, Guid.NewGuid(), new BreakageRequest(0, "x"),
            null, CancellationToken.None);
        Assert.False(noQty.Success);

        var noReason = await _sut.RecordBreakageAsync(
            tenant, item.Id, Guid.NewGuid(), new BreakageRequest(1, "  "),
            null, CancellationToken.None);
        Assert.False(noReason.Success);

        var broken = await _sut.RecordBreakageAsync(
            tenant, item.Id, Guid.NewGuid(),
            new BreakageRequest(1, "dropped on the line"), null,
            CancellationToken.None);
        Assert.True(broken.Success, broken.Message);

        var events = await _sut.GetEventsAsync(tenant, item.Id, CancellationToken.None);
        var breakage = events.Value!.Single(e => e.EventType == "Breakage");
        Assert.Equal(1, breakage.Qty);
        Assert.Equal("dropped on the line", breakage.Reason);
    }

    [Fact]
    public async Task Tenant_isolation_hides_other_tenants_work()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pr7a");
        var release = await _sut.ReleaseAsync(
            tenant, Guid.NewGuid(), jobId, null, CancellationToken.None);
        Assert.True(release.Success, release.Message);
        var floor = await _sut.ListForJobAsync(tenant, jobId, CancellationToken.None);
        var item = floor.Value![0];

        var other = _fixture.CreateTenant("Pr7b");
        var read = await _sut.GetAsync(other, item.Id, CancellationToken.None);
        Assert.False(read.Success);
        var foreignPlan = await _sut.PlanAsync(other, jobId, CancellationToken.None);
        Assert.False(foreignPlan.Success);
        var foreignRelease = await _sut.ReleaseAsync(
            other, Guid.NewGuid(), jobId, null, CancellationToken.None);
        Assert.False(foreignRelease.Success);
    }
}
