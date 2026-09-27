using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.JobOrders;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;
using static ProGlass.Saas.Domain.Proforma.JobOrderStatuses;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// The Job Order core (Phase 9 — WPI JobOrderMapper + JobOrderRepository
/// + the convert guards) against live PostgreSQL.
/// </summary>
[Collection("database")]
public sealed class JobOrderFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly JobOrderService _sut;
    private readonly ProformaService _proformas;

    public JobOrderFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        var specs = new SpecificationsService(
            new SpecificationsStore(factory), audit);
        _sut = new JobOrderService(new JobOrderStore(factory), audit);
        _proformas = new ProformaService(new ProformaStore(factory), audit, specs);
    }

    private async Task<ProformaDto> CreateProformaAsync(Guid tenant) =>
        (await _proformas.CreateDraftAsync(
            tenant, Guid.NewGuid(), null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None)).Value!;

    [Fact]
    public async Task Conversion_copies_the_pi_and_starts_draft()
    {
        var tenant = _fixture.CreateTenant("Jo");
        var pi = await CreateProformaAsync(tenant);

        var created = await _sut.CreateFromProformaAsync(
            tenant, Guid.NewGuid(), new CreateJobOrderRequest(pi.Id),
            null, CancellationToken.None);
        Assert.True(created.Success, created.Message);

        // PI-…-0001 → JOB-…-0001; a Draft PI produces a Draft job.
        Assert.StartsWith("JOB-", created.Value!.JobOrderNo);
        Assert.Contains(pi.ProformaNo[2..], created.Value.JobOrderNo);
        Assert.Equal(Draft, created.Value.Status);
        Assert.Equal("In Production", created.Value.StatusDisplay);
        Assert.Equal("Medium", created.Value.Priority);
        Assert.Equal(pi.GrandTotalVatInclusive, created.Value.GrandTotalVatInclusive);
        Assert.Equal(pi.ProformaNo, created.Value.SourceProformaNo);
        Assert.False(created.Value.IsVoid);

        var byProforma = await _sut.GetByProformaAsync(tenant, pi.Id, CancellationToken.None);
        Assert.Single(byProforma.Value!);
    }

    [Fact]
    public async Task An_approved_pi_releases_the_job_to_the_factory()
    {
        var tenant = _fixture.CreateTenant("JoRel");
        var pi = await CreateProformaAsync(tenant);
        var sent = await _proformas.TransitionAsync(
            tenant, pi.Id, Guid.NewGuid(), null,
            new TransitionRequest("Sent", null), CancellationToken.None);
        Assert.True(sent.Success, sent.Message);
        var approved = await _proformas.TransitionAsync(
            tenant, pi.Id, Guid.NewGuid(), null,
            new TransitionRequest("Approved", null), CancellationToken.None);
        Assert.True(approved.Success, approved.Message);

        var created = await _sut.CreateFromProformaAsync(
            tenant, Guid.NewGuid(), new CreateJobOrderRequest(pi.Id),
            null, CancellationToken.None);

        Assert.Equal(Released, created.Value!.Status);
    }

    [Fact]
    public async Task A_voided_pi_is_never_convertible()
    {
        var tenant = _fixture.CreateTenant("JoVoidPi");
        var pi = await CreateProformaAsync(tenant);
        var voided = await _proformas.VoidAsync(
            tenant, pi.Id, Guid.NewGuid(), null, "wrong client",
            CancellationToken.None);
        Assert.True(voided.Success, voided.Message);

        var created = await _sut.CreateFromProformaAsync(
            tenant, Guid.NewGuid(), new CreateJobOrderRequest(pi.Id),
            null, CancellationToken.None);

        Assert.False(created.Success);
        Assert.Equal(JobOrderErrorCode.Conflict, created.ErrorCode);
        Assert.Contains("is void and cannot be converted", created.Message);
    }

    [Fact]
    public async Task A_second_conversion_needs_the_explicit_confirm()
    {
        var tenant = _fixture.CreateTenant("JoDup");
        var pi = await CreateProformaAsync(tenant);

        var first = await _sut.CreateFromProformaAsync(
            tenant, Guid.NewGuid(), new CreateJobOrderRequest(pi.Id),
            null, CancellationToken.None);
        Assert.True(first.Success, first.Message);

        var second = await _sut.CreateFromProformaAsync(
            tenant, Guid.NewGuid(), new CreateJobOrderRequest(pi.Id),
            null, CancellationToken.None);
        Assert.False(second.Success);
        Assert.Equal(JobOrderErrorCode.Conflict, second.ErrorCode);
        Assert.Contains("already has 1 job order(s)", second.Message);
        Assert.Contains(first.Value!.JobOrderNo, second.Message);

        // The confirmed re-post succeeds — and the number allocator bumps
        // past the first job's number (SuggestNext, never a collision).
        var confirmed = await _sut.CreateFromProformaAsync(
            tenant, Guid.NewGuid(), new CreateJobOrderRequest(pi.Id, AllowExisting: true),
            null, CancellationToken.None);
        Assert.True(confirmed.Success, confirmed.Message);
        Assert.NotEqual(first.Value!.JobOrderNo, confirmed.Value!.JobOrderNo);
    }

    [Fact]
    public async Task Status_changes_follow_the_centralized_table()
    {
        var tenant = _fixture.CreateTenant("JoTr");
        var pi = await CreateProformaAsync(tenant);
        var job = (await _sut.CreateFromProformaAsync(
            tenant, Guid.NewGuid(), new CreateJobOrderRequest(pi.Id),
            null, CancellationToken.None)).Value!;

        // Draft → Released → InProduction → Completed → Delivered.
        foreach (var next in new[] { Released, InProduction, Completed, Delivered })
        {
            var moved = await _sut.TransitionAsync(
                tenant, job.Id, Guid.NewGuid(),
                new JobOrderTransitionRequest(next), null, CancellationToken.None);
            Assert.True(moved.Success, moved.Message);
            Assert.Equal(next, moved.Value!.Status);
        }

        // Delivered → Draft is not allowed.
        var refused = await _sut.TransitionAsync(
            tenant, job.Id, Guid.NewGuid(),
            new JobOrderTransitionRequest(Draft), null, CancellationToken.None);
        Assert.False(refused.Success);
        Assert.Equal(JobOrderErrorCode.Conflict, refused.ErrorCode);

        // Delivered → Rejected (void target) is allowed.
        var reject = await _sut.TransitionAsync(
            tenant, job.Id, Guid.NewGuid(),
            new JobOrderTransitionRequest(Rejected), null, CancellationToken.None);
        Assert.True(reject.Success, reject.Message);
    }

    [Fact]
    public async Task Void_is_a_soft_close_and_idempotent()
    {
        var tenant = _fixture.CreateTenant("JoVoid");
        var pi = await CreateProformaAsync(tenant);
        var job = (await _sut.CreateFromProformaAsync(
            tenant, Guid.NewGuid(), new CreateJobOrderRequest(pi.Id),
            null, CancellationToken.None)).Value!;

        var voided = await _sut.VoidAsync(
            tenant, job.Id, Guid.NewGuid(), new VoidJobOrderRequest(null),
            null, CancellationToken.None);
        Assert.True(voided.Success, voided.Message);
        Assert.True(voided.Value!.IsVoid);
        Assert.Equal(Rejected, voided.Value.Status);
        Assert.Equal("(no reason given)", voided.Value.VoidReason);
        Assert.NotNull(voided.Value.VoidedAt);

        // Idempotent — the second call returns the same state.
        var again = await _sut.VoidAsync(
            tenant, job.Id, Guid.NewGuid(), new VoidJobOrderRequest("second"),
            null, CancellationToken.None);
        Assert.True(again.Success, again.Message);
        Assert.Equal("(no reason given)", again.Value!.VoidReason);

        // The row survives for the audit trail; lists hide it unless asked.
        var hidden = await _sut.ListAsync(
            tenant, null, null, false, null, null, CancellationToken.None);
        Assert.Empty(hidden.Items);
        var shown = await _sut.ListAsync(
            tenant, null, null, true, null, null, CancellationToken.None);
        Assert.Single(shown.Items);
    }

    [Fact]
    public async Task Production_details_update_with_row_version_guard()
    {
        var tenant = _fixture.CreateTenant("JoUpd");
        var pi = await CreateProformaAsync(tenant);
        var job = (await _sut.CreateFromProformaAsync(
            tenant, Guid.NewGuid(), new CreateJobOrderRequest(pi.Id),
            null, CancellationToken.None)).Value!;
        var version = job.RowVersion;

        var updated = await _sut.UpdateAsync(
            tenant, job.Id, Guid.NewGuid(),
            new UpdateJobOrderRequest(
                "High", new DateOnly(2026, 10, 15),
                AssignedTo: "Team B", ExpectedRowVersion: version),
            null, CancellationToken.None);
        Assert.True(updated.Success, updated.Message);
        Assert.Equal("High", updated.Value!.Priority);
        Assert.Equal(new DateOnly(2026, 10, 15), updated.Value.DeliveryDate);
        Assert.Equal("Team B", updated.Value.AssignedTo);
        Assert.Equal(version + 1, updated.Value.RowVersion);

        // A stale RowVersion is a conflict, never a silent overwrite.
        var stale = await _sut.UpdateAsync(
            tenant, job.Id, Guid.NewGuid(),
            new UpdateJobOrderRequest("Low", null, ExpectedRowVersion: version),
            null, CancellationToken.None);
        Assert.False(stale.Success);
        Assert.Equal(JobOrderErrorCode.ConcurrencyConflict, stale.ErrorCode);

        var badPriority = await _sut.UpdateAsync(
            tenant, job.Id, Guid.NewGuid(),
            new UpdateJobOrderRequest("Urgent", null), null, CancellationToken.None);
        Assert.Equal(JobOrderErrorCode.ValidationFailed, badPriority.ErrorCode);
    }

    [Fact]
    public async Task Tenants_are_isolated()
    {
        var a = _fixture.CreateTenant("JoA");
        var b = _fixture.CreateTenant("JoB");
        var pi = await CreateProformaAsync(a);
        var job = (await _sut.CreateFromProformaAsync(
            a, Guid.NewGuid(), new CreateJobOrderRequest(pi.Id),
            null, CancellationToken.None)).Value!;

        var theirs = await _sut.GetAsync(b, job.Id, CancellationToken.None);
        Assert.Equal(JobOrderErrorCode.NotFound, theirs.ErrorCode);

        var convertTheirs = await _sut.CreateFromProformaAsync(
            b, Guid.NewGuid(), new CreateJobOrderRequest(pi.Id),
            null, CancellationToken.None);
        Assert.Equal(JobOrderErrorCode.NotFound, convertTheirs.ErrorCode);

        var listTheirs = await _sut.ListAsync(
            b, null, null, true, null, null, CancellationToken.None);
        Assert.Empty(listTheirs.Items);
    }
}
