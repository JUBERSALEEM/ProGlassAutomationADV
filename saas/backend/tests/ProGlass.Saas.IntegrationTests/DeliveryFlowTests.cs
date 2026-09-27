using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.Delivery;
using ProGlass.Saas.Application.JobOrders;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;
using static ProGlass.Saas.Domain.Proforma.DeliveryNoteStatuses;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// The Delivery core (Phase 10 — WPF MasterDeliveryNoteService +
/// DeliveryNoteCreateService.CreateOrOpenDispatchingAsync +
/// UpdateMasterDnAsync + the soft-close void) against live PostgreSQL.
/// Grid: group 1 "Front" 1000×2000 ×4, group 2 "Side" 300×300 ×2 →
/// master lines (1|1)=4 pcs and (2|2)=2 pcs, serials CONTINUOUS across
/// groups (WPF FromJobOrder globalSerialCounter).
/// </summary>
[Collection("database")]
public sealed class DeliveryFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly DeliveryService _sut;
    private readonly JobOrderService _jobs;
    private readonly ProformaService _proformas;

    public DeliveryFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        var specs = new SpecificationsService(
            new SpecificationsStore(factory), audit);
        _sut = new DeliveryService(new DeliveryNoteStore(factory), audit);
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
            new(1, "DGU", "6mm Clear FT + 12mm ASP + 6mm Clear FT", 119.40m,
                "formula", new List<SpecGroupItemRequest>
                {
                    new(1, "Front", 1000, 2000, 900, 1800, 4, null, ""),
                }),
            new(2, "SGU", "6mm Clear FT", 46m, "manual", new List<SpecGroupItemRequest>
            {
                new(1, "Side", 300, 300, 0, 0, 2, null, ""),
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

    private Task<DeliveryResult<CreateOrOpenDnDto>> CreateOrOpenAsync(
        Guid tenant, Guid jobId) =>
        _sut.CreateOrOpenDispatchingAsync(
            tenant, Guid.NewGuid(), jobId, null, CancellationToken.None);

    [Fact]
    public async Task First_call_creates_master_and_first_dispatch_note()
    {
        var (tenant, jobId, jobNo) = await CreateJobAsync("Dn1");
        var core = jobNo[4..];

        var result = await CreateOrOpenAsync(tenant, jobId);
        Assert.True(result.Success, result.Message);
        Assert.Equal(DnCreateOutcome.Created, result.Value!.Outcome);

        var dn = result.Value.Note!;
        // DN-…-01, Dispatching, Original — DeliveredQty starts at 0.
        Assert.Equal($"DN-{core}-01", dn.DeliveryNo);
        Assert.Equal(Dispatching, dn.Status);
        Assert.Equal("Original", dn.DeliveryType);
        Assert.False(dn.IsMasterDn);
        Assert.Equal(1, dn.SequenceNumber);

        // Both lines, serials CONTINUOUS across groups (1 then 2).
        Assert.Equal(2, dn.Items.Count);
        Assert.Equal(6, dn.TotalOrderedQty);
        Assert.Equal(0, dn.TotalDeliveredQty);
        Assert.Equal(6, dn.TotalBalanceQty);
        var front = dn.Items.Single(i => i.SpecGroupNumber == 1);
        Assert.Equal(1, front.SerialNumber);
        Assert.Equal("Front", front.GlassRef);
        Assert.Equal(4, front.OrderedQty);
        Assert.Equal(0, front.PreviouslyDeliveredQty);
        Assert.Equal(0, front.DeliveredQty);
        Assert.Equal(4, front.BalanceQty);
        var side = dn.Items.Single(i => i.SpecGroupNumber == 2);
        Assert.Equal(2, side.SerialNumber);   // continuous, not group-local
        Assert.Equal(2, side.OrderedQty);
        Assert.Equal(2, side.BalanceQty);

        // The master tracker: MDN-…, all JO lines, full balance.
        var master = await _sut.GetMasterAsync(tenant, jobId, CancellationToken.None);
        Assert.True(master.Success, master.Message);
        Assert.Equal($"MDN-{core}", master.Value!.DeliveryNo);
        Assert.True(master.Value.IsMasterDn);
        Assert.Equal(6, master.Value.TotalOrderedQty);
        Assert.Equal(0, master.Value.TotalDeliveredQty);
        Assert.Equal(6, master.Value.TotalBalanceQty);
    }

    [Fact]
    public async Task Second_call_opens_the_existing_note()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Dn2");
        var first = await CreateOrOpenAsync(tenant, jobId);
        var firstId = first.Value!.Note!.Id;

        var second = await CreateOrOpenAsync(tenant, jobId);
        Assert.True(second.Success, second.Message);
        Assert.Equal(DnCreateOutcome.OpenExisting, second.Value!.Outcome);
        Assert.Equal(firstId, second.Value.Note!.Id);
        Assert.Contains("Opened existing", second.Value.Message);

        var all = await _sut.ListForJobAsync(tenant, jobId, CancellationToken.None);
        // Master + exactly one dispatch note.
        Assert.Equal(2, all.Value!.Count);
        Assert.Single(all.Value.Where(d => !d.IsMasterDn));
    }

    [Fact]
    public async Task Open_drafts_do_not_consume_the_master_balance()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Dn3");
        var created = await CreateOrOpenAsync(tenant, jobId);
        Assert.True(created.Success, created.Message);

        // The note exists with balance 6, but it is only Dispatching —
        // the master must still show the full 6 remaining.
        var master = await _sut.GetMasterAsync(tenant, jobId, CancellationToken.None);
        Assert.Equal(0, master.Value!.TotalDeliveredQty);
        Assert.Equal(6, master.Value.TotalBalanceQty);
        var line = master.Value.Items.Single(i => i.SpecGroupNumber == 1);
        Assert.Equal(0, line.PreviouslyDeliveredQty);
        Assert.Equal(4, line.BalanceQty);
    }

    [Fact]
    public async Task Partial_delivery_then_next_note_carries_only_balance()
    {
        var (tenant, jobId, jobNo) = await CreateJobAsync("Dn4");
        var core = jobNo[4..];

        var first = await CreateOrOpenAsync(tenant, jobId);
        var dn = first.Value!.Note!;
        var front = dn.Items.Single(i => i.SpecGroupNumber == 1);
        var side = dn.Items.Single(i => i.SpecGroupNumber == 2);

        // Operator fills: 2 of 4 fronts, both sides.
        var edited = await _sut.UpdateDispatchAsync(
            tenant, dn.Id, Guid.NewGuid(), new UpdateDispatchRequest(
                "Ali", "D-1234", "Site Office", null, "10:30", "Handle with care",
                false, new List<DispatchLineRequest>
                {
                    new(front.Id, 2),
                    new(side.Id, 2),
                }),
            null, CancellationToken.None);
        Assert.True(edited.Success, edited.Message);
        Assert.Equal(4, edited.Value!.TotalDeliveredQty);

        // Confirm → master recompute (2 fronts + 2 sides confirmed).
        var dispatched = await _sut.TransitionAsync(
            tenant, dn.Id, Guid.NewGuid(),
            new DeliveryTransitionRequest(Dispatched), null, CancellationToken.None);
        Assert.True(dispatched.Success, dispatched.Message);

        var master = await _sut.GetMasterAsync(tenant, jobId, CancellationToken.None);
        Assert.Equal(6, master.Value!.TotalOrderedQty);
        Assert.Equal(4, master.Value.TotalDeliveredQty);
        Assert.Equal(2, master.Value.TotalBalanceQty);
        var frontLine = master.Value.Items.Single(i => i.SpecGroupNumber == 1);
        Assert.Equal(2, frontLine.PreviouslyDeliveredQty);
        Assert.Equal(2, frontLine.BalanceQty);
        var sideLine = master.Value.Items.Single(i => i.SpecGroupNumber == 2);
        Assert.Equal(0, sideLine.BalanceQty);

        // Next note: only the remaining fronts, previously = 2.
        var second = await CreateOrOpenAsync(tenant, jobId);
        Assert.True(second.Success, second.Message);
        Assert.Equal(DnCreateOutcome.Created, second.Value!.Outcome);
        var dn2 = second.Value.Note!;
        Assert.Equal($"DN-{core}-02", dn2.DeliveryNo);
        Assert.Single(dn2.Items);
        Assert.Equal(1, dn2.Items[0].SpecGroupNumber);
        Assert.Equal(1, dn2.Items[0].SerialNumber);   // never reassigned
        Assert.Equal(4, dn2.Items[0].OrderedQty);
        Assert.Equal(2, dn2.Items[0].PreviouslyDeliveredQty);
        Assert.Equal(2, dn2.Items[0].BalanceQty);
    }

    [Fact]
    public async Task Fully_delivered_returns_the_wpf_message()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Dn5");
        var first = await CreateOrOpenAsync(tenant, jobId);
        var dn = first.Value!.Note!;

        // Deliver everything (operator fills full balance) and confirm.
        var edited = await _sut.UpdateDispatchAsync(
            tenant, dn.Id, Guid.NewGuid(), new UpdateDispatchRequest(
                null, null, null, null, null, null, null,
                dn.Items.Select(i => new DispatchLineRequest(i.Id, i.BalanceQty))
                    .ToList()),
            null, CancellationToken.None);
        Assert.True(edited.Success, edited.Message);
        await _sut.TransitionAsync(
            tenant, dn.Id, Guid.NewGuid(),
            new DeliveryTransitionRequest(Dispatched), null, CancellationToken.None);

        var exhausted = await CreateOrOpenAsync(tenant, jobId);
        Assert.False(exhausted.Success);
        Assert.Equal(DeliveryErrorCode.FullyDelivered, exhausted.ErrorCode);
        Assert.StartsWith(
            "All confirmed deliveries for this job order are complete.",
            exhausted.Message);
        Assert.Contains("Delivered : 6 pcs", exhausted.Message);
        Assert.Contains("Balance   : 0 pcs", exhausted.Message);
    }

    [Fact]
    public async Task Void_soft_closes_and_restores_the_balance()
    {
        var (tenant, jobId, jobNo) = await CreateJobAsync("Dn6");
        var core = jobNo[4..];
        var first = await CreateOrOpenAsync(tenant, jobId);
        var dn = first.Value!.Note!;

        // Fill + confirm, then void: the delivery no longer counts.
        await _sut.UpdateDispatchAsync(
            tenant, dn.Id, Guid.NewGuid(), new UpdateDispatchRequest(
                null, null, null, null, null, null, null,
                dn.Items.Select(i => new DispatchLineRequest(i.Id, i.BalanceQty))
                    .ToList()),
            null, CancellationToken.None);
        await _sut.TransitionAsync(
            tenant, dn.Id, Guid.NewGuid(),
            new DeliveryTransitionRequest(Dispatched), null, CancellationToken.None);

        var voided = await _sut.VoidAsync(
            tenant, dn.Id, Guid.NewGuid(), new VoidDeliveryRequest("truck broke down"),
            null, CancellationToken.None);
        Assert.True(voided.Success, voided.Message);
        Assert.True(voided.Value!.IsVoid);
        Assert.Equal(Cancelled, voided.Value.Status);
        Assert.Equal("truck broke down", voided.Value.VoidReason);

        var master = await _sut.GetMasterAsync(tenant, jobId, CancellationToken.None);
        Assert.Equal(0, master.Value!.TotalDeliveredQty);
        Assert.Equal(6, master.Value.TotalBalanceQty);

        // Voided is history — the next call creates a fresh DN-02.
        var next = await CreateOrOpenAsync(tenant, jobId);
        Assert.Equal(DnCreateOutcome.Created, next.Value!.Outcome);
        Assert.Equal($"DN-{core}-02", next.Value.Note!.DeliveryNo);

        // Idempotent.
        var again = await _sut.VoidAsync(
            tenant, dn.Id, Guid.NewGuid(), new VoidDeliveryRequest(), null,
            CancellationToken.None);
        Assert.True(again.Success, again.Message);
        Assert.Equal(voided.Value.VoidedAt, again.Value!.VoidedAt);
    }

    [Fact]
    public async Task Transition_and_edit_guards_follow_the_wpf_rules()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Dn7");
        var created = await CreateOrOpenAsync(tenant, jobId);
        var dn = created.Value!.Note!;
        var front = dn.Items.Single(i => i.SpecGroupNumber == 1);

        // Balance guard: 5 of 4 is outside 0–4.
        var tooMuch = await _sut.UpdateDispatchAsync(
            tenant, dn.Id, Guid.NewGuid(), new UpdateDispatchRequest(
                null, null, null, null, null, null, null,
                new List<DispatchLineRequest> { new(front.Id, 5) }),
            null, CancellationToken.None);
        Assert.False(tooMuch.Success);
        Assert.Equal(DeliveryErrorCode.ValidationFailed, tooMuch.ErrorCode);
        Assert.Contains("outside the balance 0–4", tooMuch.Message);

        // Machine guard: Dispatching → bogus.
        var bogus = await _sut.TransitionAsync(
            tenant, dn.Id, Guid.NewGuid(),
            new DeliveryTransitionRequest("Bogus"), null, CancellationToken.None);
        Assert.False(bogus.Success);
        Assert.Equal(DeliveryErrorCode.Conflict, bogus.ErrorCode);

        // Forward skips are legal (Dispatching → Received).
        var received = await _sut.TransitionAsync(
            tenant, dn.Id, Guid.NewGuid(),
            new DeliveryTransitionRequest(Received), null, CancellationToken.None);
        Assert.True(received.Success, received.Message);
        Assert.Equal(Received, received.Value!.Status);

        // Received is terminal and confirmed notes are history.
        var back = await _sut.TransitionAsync(
            tenant, dn.Id, Guid.NewGuid(),
            new DeliveryTransitionRequest(Draft), null, CancellationToken.None);
        Assert.False(back.Success);
        var edit = await _sut.UpdateDispatchAsync(
            tenant, dn.Id, Guid.NewGuid(), new UpdateDispatchRequest("X"),
            null, CancellationToken.None);
        Assert.False(edit.Success);
        Assert.Equal(DeliveryErrorCode.Conflict, edit.ErrorCode);
        Assert.Contains("history", edit.Message);

        // Master is a tracker: never edited, never dispatched.
        var master = await _sut.GetMasterAsync(tenant, jobId, CancellationToken.None);
        var masterEdit = await _sut.UpdateDispatchAsync(
            tenant, master.Value!.Id, Guid.NewGuid(), new UpdateDispatchRequest("X"),
            null, CancellationToken.None);
        Assert.False(masterEdit.Success);
        var masterTransition = await _sut.TransitionAsync(
            tenant, master.Value.Id, Guid.NewGuid(),
            new DeliveryTransitionRequest(Dispatched), null, CancellationToken.None);
        Assert.False(masterTransition.Success);
    }

    [Fact]
    public async Task Tenant_isolation_hides_other_tenants_notes()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Dn8a");
        var created = await CreateOrOpenAsync(tenant, jobId);
        var dn = created.Value!.Note!;

        var other = _fixture.CreateTenant("Dn8b");
        var read = await _sut.GetAsync(other, dn.Id, CancellationToken.None);
        Assert.False(read.Success);
        Assert.Equal(DeliveryErrorCode.NotFound, read.ErrorCode);

        var foreignJob = await _sut.ListForJobAsync(other, jobId, CancellationToken.None);
        Assert.False(foreignJob.Success);

        var otherCreate = await CreateOrOpenAsync(other, jobId);
        Assert.False(otherCreate.Success);
        Assert.Equal(DeliveryErrorCode.NotFound, otherCreate.ErrorCode);
    }
}
