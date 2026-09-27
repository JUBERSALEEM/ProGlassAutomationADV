using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.JobOrders;
using ProGlass.Saas.Application.Production;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Domain.Proforma;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// Phase 26a against live PostgreSQL — the WPF Phase 16/17 cores: the
/// process execution lifecycle (idempotent start, piece-flow completion,
/// breakage → replacement lineage), the release → execution projection,
/// deterministic pairing + assembly with preserved component lineage,
/// the controlled transitions (start/pause/resume/hold/assign/priority),
/// the unified queue with explainable readiness, and the workstation
/// master. Grid: the same DGU spec as ProductionFlowTests (FT outer +
/// annealed inner, 1000×2000 / 900×1800 ×4) → 5 work items.
/// </summary>
[Collection("database")]
public sealed class ProcessControlFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly ProductionService _production;
    private readonly ProcessExecutionService _executions;
    private readonly PairingAssemblyService _pairing;
    private readonly ProductionControlService _control;
    private readonly ProductionQueueService _queue;
    private readonly WorkstationService _workstations;

    public ProcessControlFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        var controlStore = new ProcessControlStore(factory);
        var specs = new SpecificationsService(
            new SpecificationsStore(factory), audit);
        _production = new ProductionService(
            new ProductionStore(factory), controlStore, audit);
        var productionStore = new ProductionStore(factory);
        _executions = new ProcessExecutionService(
            controlStore, productionStore, audit,
            new ReplacementService(
                new CuttingCostStore(factory), productionStore, audit));
        _proformas = new ProformaService(
            new ProformaStore(factory), audit, specs);
        _jobs = new JobOrderService(new JobOrderStore(factory), audit);
        _pairing = new PairingAssemblyService(
            controlStore, productionStore, audit);
        _control = new ProductionControlService(
            controlStore, productionStore,
            new ReservationStore(factory), _production, audit);
        _queue = new ProductionQueueService(controlStore);
        _workstations = new WorkstationService(controlStore);
    }

    private readonly JobOrderService _jobs;
    private readonly ProformaService _proformas;

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

    private async Task<IReadOnlyList<ProductionWorkItemDto>> ReleaseAndListAsync(
        Guid tenant, Guid jobId)
    {
        var release = await _production.ReleaseAsync(
            tenant, Guid.NewGuid(), jobId, null, CancellationToken.None);
        Assert.True(release.Success, release.Message);
        var floor = await _production.ListForJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.True(floor.Success, floor.Message);
        return floor.Value!;
    }

    // ── the release → execution projection ───────────────────────────

    [Fact]
    public async Task Release_projects_one_pending_execution_per_work_item()
    {
        var (tenant, jobId, jobNo) = await CreateJobAsync("Pc1");
        var release = await _production.ReleaseAsync(
            tenant, Guid.NewGuid(), jobId, null, CancellationToken.None);
        Assert.True(release.Success, release.Message);
        Assert.Equal(5, release.Value!.ProjectedExecutions);

        var rows = await _executions.ListByJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.Equal(5, rows.Value!.Count);
        Assert.All(rows.Value, r =>
        {
            Assert.Equal(WorkItemStatuses.Pending, r.Status);
            Assert.Equal(0, r.QtyDone);
            Assert.Equal(ProductionEntryTypes.Normal, r.EntryType);
        });

        // Deterministic projected ids carry the position so Outer and
        // Inner do not collide.
        Assert.Contains(rows.Value,
            r => r.ExecutionId == $"{jobNo}|1|1|1|0|Outer");
        Assert.Contains(rows.Value,
            r => r.ExecutionId == $"{jobNo}|1|1|1|0|Inner");

        // Re-release: nothing new, statuses preserved.
        await _production.ReleaseAsync(
            tenant, Guid.NewGuid(), jobId, null, CancellationToken.None);
        var again = await _executions.ListByJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.Equal(5, again.Value!.Count);
    }

    // ── execution start / complete / breakage ────────────────────────

    [Fact]
    public async Task Execution_start_is_gated_and_idempotent()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pc2");
        var floor = await ReleaseAndListAsync(tenant, jobId);
        var outer = floor.Single(i => i.Stage == "Cutting" && i.Position == "Outer");
        var tempering = floor.Single(i => i.Stage == "Tempering");

        // Tempering cannot begin — its route predecessor (Cutting) is not
        // complete (fail closed).
        var blocked = await _executions.StartAsync(
            tenant, Guid.NewGuid(),
            new StartExecutionRequest(tempering.Id), null, CancellationToken.None);
        Assert.False(blocked.Success);
        Assert.Contains("cannot begin", blocked.Message);
        Assert.Contains("gating", blocked.Message);

        // Cutting has no predecessor — start succeeds and flips the work
        // item to InProgress.
        var started = await _executions.StartAsync(
            tenant, Guid.NewGuid(),
            new StartExecutionRequest(outer.Id), null, CancellationToken.None);
        Assert.True(started.Success, started.Message);
        Assert.Equal(WorkItemStatuses.InProgress, started.Value!.Status);
        Assert.Equal($"{started.Value.ExecutionId}", started.Value.ExecutionId);
        Assert.Contains("|Outer", started.Value.ExecutionId);

        var floorAfter = await _production.ListForJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.Equal(WorkItemStatuses.InProgress,
            floorAfter.Value!.Single(i => i.Id == outer.Id).Status);

        // Idempotent re-start.
        var again = await _executions.StartAsync(
            tenant, Guid.NewGuid(),
            new StartExecutionRequest(outer.Id), null, CancellationToken.None);
        Assert.True(again.Success);
        Assert.Equal(started.Value.ExecutionId, again.Value!.ExecutionId);
    }

    [Fact]
    public async Task Completion_is_incremental_and_clamped()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pc3");
        var floor = await ReleaseAndListAsync(tenant, jobId);
        var outer = floor.Single(i => i.Stage == "Cutting" && i.Position == "Outer");

        var started = await _executions.StartAsync(
            tenant, Guid.NewGuid(),
            new StartExecutionRequest(outer.Id), null, CancellationToken.None);
        Assert.True(started.Success, started.Message);
        var executionId = started.Value!.ExecutionId;

        var first = await _executions.CompleteAsync(
            tenant, Guid.NewGuid(), executionId,
            new CompleteExecutionRequest(2), null, CancellationToken.None);
        Assert.True(first.Success, first.Message);
        Assert.Equal(2, first.Value!.QtyDone);
        Assert.Equal(WorkItemStatuses.InProgress, first.Value.Status);

        // 2 + 5 would be 7 — clamped to QtyRequired 4 (no destructive
        // overwrite).
        var second = await _executions.CompleteAsync(
            tenant, Guid.NewGuid(), executionId,
            new CompleteExecutionRequest(5), null, CancellationToken.None);
        Assert.True(second.Success, second.Message);
        Assert.Equal(4, second.Value!.QtyDone);
        Assert.Equal(WorkItemStatuses.Completed, second.Value.Status);
        Assert.NotNull(second.Value.CompletedAt);

        // Idempotent re-complete.
        var third = await _executions.CompleteAsync(
            tenant, Guid.NewGuid(), executionId,
            new CompleteExecutionRequest(1), null, CancellationToken.None);
        Assert.True(third.Success);
        Assert.Equal("Already completed (idempotent).", third.Message);

        // The work item's own QtyDone is untouched (the stage flow is
        // authoritative).
        var floorAfter = await _production.ListForJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.Equal(0,
            floorAfter.Value!.Single(i => i.Id == outer.Id).QtyDone);
    }

    [Fact]
    public async Task Stage_flow_mirrors_quantity_onto_the_execution()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pc4");
        var floor = await ReleaseAndListAsync(tenant, jobId);
        var outer = floor.Single(i => i.Stage == "Cutting" && i.Position == "Outer");

        var staged = await _production.RecordStageAsync(
            tenant, outer.Id, Guid.NewGuid(),
            new StageProgressRequest(4), null, CancellationToken.None);
        Assert.True(staged.Success, staged.Message);

        var rows = await _executions.ListByJobAsync(
            tenant, jobId, CancellationToken.None);
        var mirrored = rows.Value!.Single(r =>
            r.ProductionPieceId == outer.Id && r.ProcessType == 1);
        Assert.Equal(4, mirrored.QtyDone);
        Assert.Equal(4, mirrored.QtyRequired);
        Assert.Equal(WorkItemStatuses.Completed, mirrored.Status);
    }

    [Fact]
    public async Task Breakage_creates_the_replacement_lineage()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pc5");
        var floor = await ReleaseAndListAsync(tenant, jobId);
        var outer = floor.Single(i => i.Stage == "Cutting" && i.Position == "Outer");
        var tempering = floor.Single(i => i.Stage == "Tempering");

        // Complete the outer cut so tempering can begin.
        await _production.RecordStageAsync(
            tenant, outer.Id, Guid.NewGuid(),
            new StageProgressRequest(4), null, CancellationToken.None);
        var started = await _executions.StartAsync(
            tenant, Guid.NewGuid(),
            new StartExecutionRequest(tempering.Id), null, CancellationToken.None);
        Assert.True(started.Success, started.Message);
        var executionId = started.Value!.ExecutionId;

        var breakage = await _executions.RecordBreakageAsync(
            tenant, Guid.NewGuid(), executionId,
            new ExecutionBreakageRequest("Edge chipped", 1),
            null, CancellationToken.None);
        Assert.True(breakage.Success, breakage.Message);
        Assert.Equal(WorkItemStatuses.Rejected, breakage.Value!.Status);
        Assert.Equal(ProductionEntryTypes.Breakage, breakage.Value.EntryType);

        var rows = await _executions.ListByJobAsync(
            tenant, jobId, CancellationToken.None);
        var child = rows.Value!.Single(r => r.ParentExecutionId == executionId);
        Assert.Equal(1, child.ReworkSequence);
        Assert.Equal(1, child.Sequence);
        Assert.Equal(WorkItemStatuses.Pending, child.Status);
        Assert.Equal(1, child.QtyRequired);
        Assert.Equal($"{started.Value.PieceId}-R01", child.OutputPieceId);
        // Original → breakage → replacement stays traceable.
        Assert.Equal(executionId, child.ParentExecutionId);

        // The breakage event landed on the work item's log.
        var events = await _production.GetEventsAsync(
            tenant, tempering.Id, CancellationToken.None);
        Assert.Contains(events.Value!, e => e.EventType == "Breakage");

        // Execution progress math (WPF GetProgressByJobAsync): five
        // projected rows × Qty 4 + the replacement child's Qty 1.
        var progress = await _executions.GetProgressByJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.True(progress.Success, progress.Message);
        Assert.Equal(5 * 4 + 1, progress.Value!.Required);
    }

    [Fact]
    public async Task Pause_then_restart_reactivates_the_same_execution()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pc6");
        var floor = await ReleaseAndListAsync(tenant, jobId);
        var inner = floor.Single(i => i.Stage == "Cutting" && i.Position == "Inner");

        var started = await _executions.StartAsync(
            tenant, Guid.NewGuid(),
            new StartExecutionRequest(inner.Id), null, CancellationToken.None);
        Assert.True(started.Success, started.Message);

        var paused = await _executions.PauseAsync(
            tenant, Guid.NewGuid(), started.Value!.ExecutionId,
            new PauseExecutionRequest("Lunch"), null, CancellationToken.None);
        Assert.True(paused.Success, paused.Message);
        Assert.Equal(WorkItemStatuses.Paused, paused.Value!.Status);

        // Re-start re-activates the paused row (same id).
        var resumed = await _executions.StartAsync(
            tenant, Guid.NewGuid(),
            new StartExecutionRequest(inner.Id), null, CancellationToken.None);
        Assert.True(resumed.Success, resumed.Message);
        Assert.Equal(started.Value.ExecutionId, resumed.Value!.ExecutionId);
        Assert.Equal(WorkItemStatuses.InProgress, resumed.Value.Status);
    }

    // ── deterministic pairing + assembly ─────────────────────────────

    [Fact]
    public async Task Pairing_gates_on_cutting_and_pairs_deterministically()
    {
        var (tenant, jobId, jobNo) = await CreateJobAsync("Pc7");
        var floor = await ReleaseAndListAsync(tenant, jobId);

        // Gating: outer cutting incomplete → refused.
        var early = await _pairing.PairAsync(
            tenant, Guid.NewGuid(),
            new PairUnitRequest(jobId, 1, 1), null, CancellationToken.None);
        Assert.False(early.Success);
        Assert.Contains("Outer cutting incomplete", early.Message);

        // Complete both cuttings.
        foreach (var position in new[] { "Outer", "Inner" })
        {
            var item = floor.Single(i =>
                i.Stage == "Cutting" && i.Position == position);
            await _production.RecordStageAsync(
                tenant, item.Id, Guid.NewGuid(),
                new StageProgressRequest(4), null, CancellationToken.None);
        }

        var paired = await _pairing.PairAsync(
            tenant, Guid.NewGuid(),
            new PairUnitRequest(jobId, 1, 1), null, CancellationToken.None);
        Assert.True(paired.Success, paired.Message);
        // Finished-unit count = pairing work item Qty (4), never the
        // pane-cut sum (8).
        Assert.Equal(4, paired.Value!.Count);

        // Deterministic identities — Pair-001..004, outer pane from the
        // cutting work item's dimensions (no placed-piece table).
        Assert.Equal(
            $"Pair-001",
            paired.Value[0].PairId);
        Assert.Equal($"{jobNo}|1|1|Outer|1000x2000|P0001",
            paired.Value[0].OuterPieceId);
        Assert.Equal($"{jobNo}|1|1|Inner|900x1800|P0001",
            paired.Value[0].InnerPieceId);
        Assert.Equal(
            $"{jobNo}|1|1|Pair:002",
            paired.Value[2].PairingId);
        Assert.Equal(WorkItemStatuses.Completed, paired.Value[0].Status);

        // Idempotent re-pair.
        var again = await _pairing.PairAsync(
            tenant, Guid.NewGuid(),
            new PairUnitRequest(jobId, 1, 1), null, CancellationToken.None);
        Assert.True(again.Success);
        Assert.Equal(4, again.Value!.Count);
    }

    [Fact]
    public async Task Assembly_gates_on_pairing_pcs_and_preserves_lineage()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pc8");
        var floor = await ReleaseAndListAsync(tenant, jobId);

        // Gating: the Pairing WORK ITEM PCS must pass first.
        var early = await _pairing.AssembleAsync(
            tenant, Guid.NewGuid(),
            new AssembleUnitRequest(jobId, 1, 1), null, CancellationToken.None);
        Assert.False(early.Success);
        Assert.Contains("pairing has not passed", early.Message);

        foreach (var position in new[] { "Outer", "Inner" })
        {
            var item = floor.Single(i =>
                i.Stage == "Cutting" && i.Position == position);
            await _production.RecordStageAsync(
                tenant, item.Id, Guid.NewGuid(),
                new StageProgressRequest(4), null, CancellationToken.None);
        }
        await _pairing.PairAsync(tenant, Guid.NewGuid(),
            new PairUnitRequest(jobId, 1, 1), null, CancellationToken.None);

        // Pairing PCS is still 0/4 → refused.
        var stillEarly = await _pairing.AssembleAsync(
            tenant, Guid.NewGuid(),
            new AssembleUnitRequest(jobId, 1, 1), null, CancellationToken.None);
        Assert.False(stillEarly.Success);
        Assert.Contains("pairing has not passed", stillEarly.Message);

        var pairingItem = floor.Single(i => i.Stage == "Pairing");
        await _production.RecordStageAsync(
            tenant, pairingItem.Id, Guid.NewGuid(),
            new StageProgressRequest(4), null, CancellationToken.None);

        var assembled = await _pairing.AssembleAsync(
            tenant, Guid.NewGuid(),
            new AssembleUnitRequest(jobId, 1, 1,
                SpacerInfo: "12mm Black", SealantInfo: "Structural"),
            null, CancellationToken.None);
        Assert.True(assembled.Success, assembled.Message);
        Assert.Equal(4, assembled.Value!.Count);
        Assert.All(assembled.Value, a =>
            Assert.Equal(WorkItemStatuses.Completed, a.Status));

        var first = assembled.Value[0];
        Assert.Equal("DGU-001", first.AssemblyLabel);
        Assert.Equal("DGU", first.AssemblyType);
        // Component lineage preserved: Outer + Inner + synthetic spacer.
        Assert.Equal(3, first.Components.Count);
        Assert.Equal("Outer", first.Components[0].Role);
        Assert.Equal("Inner", first.Components[1].Role);
        Assert.Equal("Spacer", first.Components[2].Role);
        Assert.Equal("Spacer-12mm Black", first.Components[2].PieceId);
        Assert.Contains("Outer", first.Traceability);
        Assert.Contains("Inner", first.Traceability);
        Assert.Equal("12mm Black", first.SpacerInfo);
        Assert.Equal("Structural", first.SealantInfo);

        // Idempotent re-assemble.
        var again = await _pairing.AssembleAsync(
            tenant, Guid.NewGuid(),
            new AssembleUnitRequest(jobId, 1, 1, SpacerInfo: "12mm Black"),
            null, CancellationToken.None);
        Assert.True(again.Success);
        Assert.Equal(4, again.Value!.Count);
    }

    // ── controlled transitions ───────────────────────────────────────

    [Fact]
    public async Task Control_start_requires_readiness()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pc9");
        var floor = await ReleaseAndListAsync(tenant, jobId);
        var pairing = floor.Single(i => i.Stage == "Pairing");

        // Pairing blocked: outer + inner cutting incomplete.
        var blocked = await _control.StartAsync(
            tenant, Guid.NewGuid(), pairing.Id,
            new ControlStartRequest("Kareem"), null, CancellationToken.None);
        Assert.False(blocked.Success);
        Assert.Contains("Not ready:", blocked.Message);
        Assert.Contains(ProductionBlockReasons.WaitingForOuterComponent,
            blocked.Message);

        // Complete the components → READY → started.
        foreach (var position in new[] { "Outer", "Inner" })
        {
            var item = floor.Single(i =>
                i.Stage == "Cutting" && i.Position == position);
            await _production.RecordStageAsync(
                tenant, item.Id, Guid.NewGuid(),
                new StageProgressRequest(4), null, CancellationToken.None);
        }
        var started = await _control.StartAsync(
            tenant, Guid.NewGuid(), pairing.Id,
            new ControlStartRequest("Kareem"), null, CancellationToken.None);
        Assert.True(started.Success, started.Message);
        Assert.Equal(WorkItemStatuses.InProgress, started.Value!.Status);

        // Idempotent.
        var again = await _control.StartAsync(
            tenant, Guid.NewGuid(), pairing.Id,
            new ControlStartRequest("Kareem"), null, CancellationToken.None);
        Assert.True(again.Success);
        Assert.Contains("idempotent", again.Message);
    }

    [Fact]
    public async Task Pause_resume_and_hold_release_follow_the_rules()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pc10");
        var floor = await ReleaseAndListAsync(tenant, jobId);
        var item = floor.Single(i => i.Stage == "Pairing");

        // Only InProgress can pause.
        var earlyPause = await _control.PauseAsync(
            tenant, Guid.NewGuid(), item.Id,
            new ControlPauseRequest("wait"), null, CancellationToken.None);
        Assert.False(earlyPause.Success);
        Assert.Equal("Only InProgress can be paused", earlyPause.Message);

        // Hold while pending (idempotent), then release with appended
        // notes — history preserved.
        var held = await _control.HoldAsync(
            tenant, Guid.NewGuid(), item.Id,
            new ControlHoldRequest("Paint cure", "site visit"),
            null, CancellationToken.None);
        Assert.True(held.Success, held.Message);
        Assert.EndsWith("HOLD:000", held.Value!.HoldId);

        var reHold = await _control.HoldAsync(
            tenant, Guid.NewGuid(), item.Id,
            new ControlHoldRequest("Another"), null, CancellationToken.None);
        Assert.True(reHold.Success);
        Assert.Contains("idempotent", reHold.Message);
        Assert.Equal(held.Value.HoldId, reHold.Value!.HoldId);

        // On hold → start refused.
        var startHeld = await _control.StartAsync(
            tenant, Guid.NewGuid(), item.Id,
            new ControlStartRequest(), null, CancellationToken.None);
        Assert.False(startHeld.Success);
        Assert.Contains("On Hold", startHeld.Message);

        var released = await _control.ReleaseHoldAsync(
            tenant, Guid.NewGuid(), item.Id,
            new ReleaseHoldRequest(held.Value.HoldId, "cured"),
            null, CancellationToken.None);
        Assert.True(released.Success, released.Message);
        Assert.True(released.Value!.IsReleased);
        Assert.Contains("| Release: cured", released.Value.Notes);

        // Released hold keeps its row.
        var holds = await _control.ListHoldsAsync(
            tenant, item.Id, CancellationToken.None);
        Assert.Single(holds.Value!);
        Assert.True(holds.Value[0].IsReleased);
    }

    [Fact]
    public async Task Assignment_and_override_upserts()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pc11");
        var floor = await ReleaseAndListAsync(tenant, jobId);
        var item = floor.Single(i => i.Stage == "Cutting" && i.Position == "Outer");

        var assigned = await _control.AssignAsync(
            tenant, Guid.NewGuid(), item.Id,
            new AssignRequest("CUT-01", "Rashid", "morning shift"),
            null, CancellationToken.None);
        Assert.True(assigned.Success, assigned.Message);
        Assert.EndsWith("ASSIGN:000", assigned.Value!.AssignmentId);

        // Idempotent on the same workstation + operator.
        var same = await _control.AssignAsync(
            tenant, Guid.NewGuid(), item.Id,
            new AssignRequest("CUT-01", "Rashid"), null, CancellationToken.None);
        Assert.True(same.Success);
        Assert.Equal(assigned.Value.AssignmentId, same.Value!.AssignmentId);

        // A different operator deactivates the previous (history kept).
        var next = await _control.AssignAsync(
            tenant, Guid.NewGuid(), item.Id,
            new AssignRequest("CUT-02", "Sana"), null, CancellationToken.None);
        Assert.True(next.Success, next.Message);
        Assert.EndsWith("ASSIGN:001", next.Value!.AssignmentId);

        var all = await _control.ListAssignmentsAsync(
            tenant, item.Id, false, CancellationToken.None);
        Assert.Equal(2, all.Value!.Count);
        Assert.Single(all.Value, a => a.IsActive);

        var unassigned = await _control.UnassignAsync(
            tenant, Guid.NewGuid(), item.Id, null, CancellationToken.None);
        Assert.True(unassigned.Success);

        // Priority (idempotent) + due date on the same override row.
        var priority = await _control.SetPriorityAsync(
            tenant, Guid.NewGuid(), item.Id,
            new SetPriorityRequest(2, "rush"), null, CancellationToken.None);
        Assert.True(priority.Success, priority.Message);
        Assert.EndsWith("CTRL", priority.Value!.ControlId);
        Assert.Equal(2, priority.Value.Priority);

        var samePriority = await _control.SetPriorityAsync(
            tenant, Guid.NewGuid(), item.Id,
            new SetPriorityRequest(2), null, CancellationToken.None);
        Assert.True(samePriority.Success);
        Assert.Contains("idempotent", samePriority.Message);

        var due = await _control.SetDueDateAsync(
            tenant, Guid.NewGuid(), item.Id,
            new SetDueDateRequest(new DateTime(2026, 10, 1)), null,
            CancellationToken.None);
        Assert.True(due.Success, due.Message);
        Assert.Equal(2, due.Value!.Priority); // the override row kept the priority
        Assert.Equal(new DateTime(2026, 10, 1), due.Value.DueDate);
    }

    // ── the unified queue ────────────────────────────────────────────

    [Fact]
    public async Task Queue_reports_readiness_with_reasons()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pc12");
        await ReleaseAndListAsync(tenant, jobId);

        var queue = await _queue.GetQueueAsync(tenant, jobId, take: 100,
            ct: CancellationToken.None);
        Assert.True(queue.Success, queue.Message);
        var items = queue.Value!.Items;
        Assert.Equal(5, items.Count);

        // Without reservations the cuttings are blocked with the exact
        // reason; tempering waits on cutting; pairing on components;
        // doubling on pairing PCS.
        var cutting = items.First(i => i.Stage == "Cutting" && i.Position == "Outer");
        Assert.Equal("Blocked", cutting.Readiness);
        Assert.Equal(ProductionBlockReasons.NoReservation, cutting.BlockReason);
        Assert.True(cutting.HasMaterialShortage);

        var pairing = items.Single(i => i.Stage == "Pairing");
        Assert.Equal("Blocked", pairing.Readiness);
        Assert.Contains("Outer Component", pairing.BlockReason);
        Assert.Contains("Inner Component", pairing.BlockReason);

        var doubling = items.Single(i => i.Stage == "DoublingLamination");
        Assert.Equal("Blocked", doubling.Readiness);
        Assert.Equal(ProductionBlockReasons.PairingIncomplete, doubling.BlockReason);

        // Traceability labels + required stages ride along.
        Assert.Equal("Cutting", items[0].Stage); // ordering: stage sequence
        Assert.Contains("→", items[0].TraceabilityLabel);
        Assert.Contains("Cutting", items[0].RequiredStages);

        // The executable queue (Ready only) is empty.
        var executable = await _queue.GetExecutableQueueAsync(
            tenant, CancellationToken.None);
        Assert.True(executable.Success);
        Assert.DoesNotContain(executable.Value!.Items,
            i => i.JobOrderId == jobId);
    }

    [Fact]
    public async Task Dashboard_kpis_and_progress_come_from_real_data()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Pc13");
        var floor = await ReleaseAndListAsync(tenant, jobId);

        var kpis = await _queue.GetKpisAsync(tenant, CancellationToken.None);
        Assert.True(kpis.Success, kpis.Message);
        Assert.Equal(1, kpis.Value!.JobsInProduction);
        Assert.Equal(0, kpis.Value.ReadyToStart);
        Assert.Equal(5, kpis.Value.Blocked);
        Assert.Equal(2, kpis.Value.MaterialShortages); // both cuttings, no reservation
        Assert.Equal(0, kpis.Value.CompletedToday);
        Assert.Equal("", kpis.Value.BottleneckProcess); // nothing ready → no bottleneck

        // Job progress: one unit (Spec 1 / Serial 1), Required 4, nothing
        // complete yet.
        var jobs = await _queue.GetJobProgressAsync(
            tenant, jobId, CancellationToken.None);
        Assert.True(jobs.Success, jobs.Message);
        var job = jobs.Value!.Single();
        Assert.Equal(4, job.Required);
        Assert.Equal(0, job.Completed);
        Assert.Equal(4, job.Blocked); // final stage (Doubling) remaining

        // Complete the whole unit chain → job Completed 4/4.
        foreach (var stage in new[] { "Cutting", "Cutting", "Tempering",
                     "Pairing", "DoublingLamination" })
        {
            var items = await _production.ListForJobAsync(
                tenant, jobId, CancellationToken.None);
            var next = items.Value!
                .Where(i => i.Stage == stage && i.Status != WorkItemStatuses.Completed)
                .OrderBy(i => i.Position)
                .First();
            await _production.RecordStageAsync(
                tenant, next.Id, Guid.NewGuid(),
                new StageProgressRequest(next.Qty), null, CancellationToken.None);
        }

        var after = await _queue.GetJobProgressAsync(
            tenant, jobId, CancellationToken.None);
        Assert.Equal(4, after.Value!.Single().Completed);
        Assert.Equal(100.0, after.Value.Single().ProgressPct);

        var overall = await _queue.GetOverallProgressAsync(
            tenant, CancellationToken.None);
        Assert.Equal(4, overall.Value!.Completed);

        var process = await _queue.GetProcessProgressAsync(
            tenant, CancellationToken.None);
        Assert.Equal(
            new[] { "1", "6", "7", "8" },
            process.Value!.Select(p => p.JobRef).ToArray());
        Assert.Equal(8, process.Value.Single(p => p.JobRef == "1").Required);

        var pressure = await _queue.GetQueuePressureAsync(
            tenant, CancellationToken.None);
        Assert.True(pressure.Success);
        Assert.Empty(pressure.Value!); // everything completed → queue empty
    }

    // ── the workstation master ───────────────────────────────────────

    [Fact]
    public async Task Workstation_seed_is_idempotent_and_create_reuses_codes()
    {
        var tenant = _fixture.CreateTenant("Pc14");

        var first = await _workstations.EnsureSeededAsync(
            tenant, CancellationToken.None);
        Assert.True(first.Success, first.Message);
        Assert.Equal(10, first.Value!.Seeded);
        Assert.Equal(10, first.Value.Total);

        var again = await _workstations.EnsureSeededAsync(
            tenant, CancellationToken.None);
        Assert.Equal(0, again.Value!.Seeded);
        Assert.Equal(10, again.Value.Total);

        var list = await _workstations.ListAsync(
            tenant, activeOnly: true, CancellationToken.None);
        Assert.Equal(10, list.Value!.Count);
        Assert.Equal("CUT-01", list.Value[0].Code);
        Assert.Equal("Cutting", list.Value[0].ProcessName);

        // Create is idempotent on the upper-invariant code.
        var created = await _workstations.CreateAsync(
            tenant, null,
            new CreateWorkstationRequest(" cut-03 ", "Third table", 1),
            CancellationToken.None);
        Assert.True(created.Success, created.Message);
        Assert.Equal("CUT-03", created.Value!.Code);
        var recreated = await _workstations.CreateAsync(
            tenant, null,
            new CreateWorkstationRequest("CUT-03", "Third table", 1),
            CancellationToken.None);
        Assert.Equal(created.Value.Id, recreated.Value!.Id);

        var one = await _workstations.GetAsync(tenant, "TEMPER-01",
            CancellationToken.None);
        Assert.True(one.Success);
        Assert.Equal("Tempering Furnace 01", one.Value!.Name);
    }
}
