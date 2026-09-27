using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ProGlass.Saas.Application.JobOrders;
using ProGlass.Saas.Application.Production;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Application.Stock;
using ProGlass.Saas.Application.Subscriptions;
using ProGlass.Saas.Domain.Proforma;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// Phase 26b against live PostgreSQL — the WPF Phase 14/15 cores: the
/// cutting workflow (requirements → grouping → stock selection →
/// reservation → optimization → verified layout → allocation), cutting
/// completion (QtyDone + allocation consumption + reservation cascade),
/// breakage during cutting and via the process execution (the real
/// Phase 14 replacement), the allocation ledger + actual costs, the
/// consistency scanner and snapshot integrity. Grid: the same DGU spec
/// as ProcessControlFlowTests (6mm Clear FT outer + annealed inner,
/// 1000×2000 / 900×1800 ×4) on a 3210×2250 6mm rack.
/// </summary>
[Collection("database")]
public sealed class CuttingCostFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly ProductionService _production;
    private readonly ProcessExecutionService _executions;
    private readonly ReplacementService _replacements;
    private readonly AllocationCostService _allocationCosts;
    private readonly CuttingService _cutting;
    private readonly ProductionIntegrityService _integrity;
    private readonly ProductionReportsService _reports;
    private readonly StockService _stock;
    private readonly JobOrderService _jobs;
    private readonly ProformaService _proformas;

    public CuttingCostFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        var productionStore = new ProductionStore(factory);
        var controlStore = new ProcessControlStore(factory);
        var cuttingStore = new CuttingCostStore(factory);
        var reservationService = new ReservationService(
            new ReservationStore(factory), audit);
        _replacements = new ReplacementService(
            cuttingStore, productionStore, audit);
        _production = new ProductionService(productionStore, controlStore, audit);
        _executions = new ProcessExecutionService(
            controlStore, productionStore, audit, _replacements);
        _allocationCosts = new AllocationCostService(
            cuttingStore, new ReservationStore(factory), productionStore, audit);
        _cutting = new CuttingService(
            cuttingStore, productionStore, controlStore,
            new OptimizationStore(factory), reservationService,
            _replacements, new PlanLimitGuard(new SubscriptionStore(factory)), audit);
        _integrity = new ProductionIntegrityService(cuttingStore, productionStore);
        _reports = new ProductionReportsService(
            controlStore, productionStore,
            new PairingAssemblyService(controlStore, productionStore, audit),
            new ProductionQueueService(controlStore));
        _stock = new StockService(new StockStore(factory), audit);
        var specs = new SpecificationsService(
            new SpecificationsStore(factory), audit);
        _proformas = new ProformaService(
            new ProformaStore(factory), audit, specs);
        _jobs = new JobOrderService(new JobOrderStore(factory), audit);
    }

    private async Task<(Guid TenantId, Guid JobOrderId, string JobNo)> CreateJobAsync(
        string prefix, bool withRows = true)
    {
        var tenant = _fixture.CreateTenant(prefix);
        var pi = (await _proformas.CreateDraftAsync(
            tenant, Guid.NewGuid(), null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None)).Value!;

        var items = withRows
            ? new List<SpecGroupItemRequest>
            {
                new(1, "Front", 1000, 2000, 900, 1800, 4, null, ""),
            }
            : new List<SpecGroupItemRequest>();
        var grid = new SaveSpecGroupsRequest(new List<SpecGroupRequest>
        {
            new(1, "DGU", "6mm Clear FT Glass + 12mm ASP + 6mm Green Annealed Glass",
                119.40m, "formula", items),
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

    private async Task<ProGlass.Saas.Application.Stock.GlassSheetDto> CreateRackAsync(
        Guid tenant, int stock = 10)
    {
        var result = await _stock.CreateSheetAsync(
            tenant, Guid.NewGuid(), new GlassSheetRequest(
                "Green", 6, "Green", 3210, 2250, "Test Supplier",
                45m, 80m, stock, 0), null, CancellationToken.None);
        Assert.True(result.Success, result.Message);
        return result.Value!;
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

    // ── requirements ──────────────────────────────────────────────────

    [Fact]
    public async Task Requirements_expand_cutting_work_items_per_piece()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Cc1");
        await ReleaseAndListAsync(tenant, jobId);

        var result = await _cutting.BuildRequirementsAsync(
            tenant, jobId, CancellationToken.None);
        Assert.True(result.Success, result.Message);

        // Outer + Inner cutting items × Qty 4 → 8 per-piece requirements.
        Assert.Equal(8, result.Value!.Count);
        Assert.All(result.Value, r =>
        {
            Assert.Contains("|P000", r.PieceId); // pane-instance identity
            Assert.True(r.PieceSequence > 0); // deterministic global sequence
            Assert.Equal(6, r.ThicknessMm); // single distinct pane thickness
        });
        // The spec keyword table sees "green" → Green family/color (WPF
        // parses the whole spec name, not per pane).
        Assert.All(result.Value, r => Assert.Equal("Green", r.GlassFamily));
        Assert.Contains(result.Value, r => r.RequiresTempering); // FT outer
    }

    // ── the workflow ──────────────────────────────────────────────────

    [Fact]
    public async Task Cutting_workflow_reserves_verifies_persists_and_allocates()
    {
        var (tenant, jobId, jobNo) = await CreateJobAsync("Cc2");
        await ReleaseAndListAsync(tenant, jobId);
        await CreateRackAsync(tenant);

        var run = await _cutting.RunAsync(
            tenant, Guid.NewGuid(),
            new RunCuttingRequest(jobId, EffortLevel: 1), // QUICK pass
            null, CancellationToken.None);
        Assert.True(run.Success, run.Message);
        Assert.True(run.Value!.Success, run.Value.Message);

        // Two material groups (FT treatment vs annealed ride the group
        // key) → two sessions, 8 placed pieces, 8 allocations.
        Assert.Equal(2, run.Value.Sessions);
        Assert.Equal(8, run.Value.Pieces);
        Assert.Equal(8, run.Value.Allocations);
        Assert.True(run.Value.Reservations >= 1);
        Assert.Empty(run.Value.Errors);

        // Every group's session: Verified → Allocated, linked to a
        // persisted optimization session.
        var sessions = await _cutting.ListSessionsByJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.True(sessions.Success, sessions.Message);
        Assert.Equal(2, sessions.Value!.Count);
        Assert.All(sessions.Value, s =>
        {
            Assert.Equal(CuttingSessionStatuses.Allocated, s.Status);
            Assert.True(s.IsVerified);
            Assert.NotNull(s.OptimizationSessionId);
            Assert.True(s.SheetsUsed >= 1);
            Assert.StartsWith("CUT-", s.CuttingSessionId);
        });

        // Placed pieces: Allocated, per-sheet layout coordinates, lineage
        // back to the source work items.
        var pieces = new List<CuttingPlacedPieceDto>();
        foreach (var session in sessions.Value)
        {
            var sheetRows = await _cutting.ListSheetsBySessionAsync(
                tenant, session.CuttingSessionId, CancellationToken.None);
            Assert.True(sheetRows.Success, sheetRows.Message);
            Assert.All(sheetRows.Value!, sheet =>
            {
                Assert.True(sheet.UsableWidthMm > 0);
                Assert.True(sheet.UsedSqm > 0);
                Assert.Equal(100m - sheet.UtilizationPct, sheet.WastagePct);
            });

            var pieceRows = await _cutting.ListPiecesBySessionAsync(
                tenant, session.CuttingSessionId, CancellationToken.None);
            Assert.True(pieceRows.Success, pieceRows.Message);
            pieces.AddRange(pieceRows.Value!);
        }
        Assert.Equal(8, pieces.Count);
        Assert.All(pieces, p =>
        {
            Assert.Equal(CuttingPieceStatuses.Allocated, p.Status);
            Assert.NotNull(p.SourcePieceId);
            Assert.False(string.IsNullOrWhiteSpace(p.ReservationKey));
            Assert.False(string.IsNullOrWhiteSpace(p.AllocationId));
            Assert.True(p.CutSequence > 0);
        });

        // The reservation flipped to Allocated (WPF AllocateAsync).
        var allocations = await _allocationCosts.ListByJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.True(allocations.Success, allocations.Message);
        Assert.Equal(8, allocations.Value!.Count);

        // Progress: required 8, nothing cut yet.
        var progress = await _cutting.GetProgressByJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.True(progress.Success, progress.Message);
        Assert.Equal(8, progress.Value!.Required);
        Assert.Equal(0, progress.Value.Completed);
        Assert.Equal(8, progress.Value.Remaining);
    }

    [Fact]
    public async Task Cutting_run_without_racks_errors_and_skips()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Cc3");
        await ReleaseAndListAsync(tenant, jobId);
        // No rack created — no compatible stock.

        var run = await _cutting.RunAsync(
            tenant, Guid.NewGuid(), new RunCuttingRequest(jobId),
            null, CancellationToken.None);
        Assert.True(run.Success, run.Message);
        Assert.False(run.Value!.Success);
        Assert.All(run.Value.Groups, g => Assert.False(g.Ok));
        Assert.Contains(run.Value.Errors, e => e.Contains("No compatible stock"));
        // Nothing was reserved when everything failed.
        Assert.Equal(0, run.Value.Reservations);
    }

    // ── completion ────────────────────────────────────────────────────

    [Fact]
    public async Task Complete_piece_consumes_allocation_and_cascades_when_all_done()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Cc4");
        await ReleaseAndListAsync(tenant, jobId);
        await CreateRackAsync(tenant);
        var run = await _cutting.RunAsync(
            tenant, Guid.NewGuid(), new RunCuttingRequest(jobId, EffortLevel: 1),
            null, CancellationToken.None);
        Assert.True(run.Value!.Success, run.Value.Message);

        var sessions = await _cutting.ListSessionsByJobAsync(
            tenant, jobId, CancellationToken.None);
        var pieceRows = await _cutting.ListPiecesBySessionAsync(
            tenant, sessions.Value![0].CuttingSessionId, CancellationToken.None);
        var piece = pieceRows.Value![0];
        Assert.Equal(4, pieceRows.Value.Count); // one group's 4 pieces

        // Complete every piece of the session.
        foreach (var p in pieceRows.Value)
        {
            var done = await _cutting.CompletePieceAsync(
                tenant, Guid.NewGuid(), p.PlacedPieceId,
                new CompletePieceRequest(null), null, CancellationToken.None);
            Assert.True(done.Success, done.Message);
        }

        // Idempotent re-complete.
        var again = await _cutting.CompletePieceAsync(
            tenant, Guid.NewGuid(), piece.PlacedPieceId,
            new CompletePieceRequest(null), null, CancellationToken.None);
        Assert.True(again.Success, again.Message);
        Assert.Contains("Already completed", again.Message);

        // The source work item gained its pieces (QtyDone clamped at Qty).
        var floor = await _production.ListForJobAsync(
            tenant, jobId, CancellationToken.None);
        var source = floor.Value!.Single(w => w.Id == piece.SourcePieceId);
        Assert.Equal(source.Qty, source.QtyDone);

        // All the reservation's allocations consumed → reservation
        // Consumed, allocations Consumed.
        var allocations = await _allocationCosts.ListByReservationAsync(
            tenant, piece.ReservationKey, CancellationToken.None);
        Assert.True(allocations.Success, allocations.Message);
        Assert.All(allocations.Value!, a =>
            Assert.Equal(AllocationStatuses.Consumed, a.Status));

        // Progress math.
        var progress = await _cutting.GetProgressByJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.Equal(8, progress.Value!.Required);
        Assert.Equal(4, progress.Value.Completed);
        Assert.Equal(4, progress.Value.Remaining);

        // Session complete flips the remaining group too.
        var completed = await _cutting.CompleteSessionAsync(
            tenant, Guid.NewGuid(), sessions.Value[1].CuttingSessionId,
            null, CancellationToken.None);
        Assert.True(completed.Success, completed.Message);
        Assert.Contains("4/4", completed.Value);
    }

    [Fact]
    public async Task Breakage_during_cutting_marks_piece_and_creates_replacement()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Cc5");
        await ReleaseAndListAsync(tenant, jobId);
        await CreateRackAsync(tenant);
        await _cutting.RunAsync(
            tenant, Guid.NewGuid(), new RunCuttingRequest(jobId, EffortLevel: 1),
            null, CancellationToken.None);

        var sessions = await _cutting.ListSessionsByJobAsync(
            tenant, jobId, CancellationToken.None);
        var pieceRows = await _cutting.ListPiecesBySessionAsync(
            tenant, sessions.Value![0].CuttingSessionId, CancellationToken.None);
        var piece = pieceRows.Value![0];

        var breakage = await _cutting.RecordBreakageDuringCuttingAsync(
            tenant, Guid.NewGuid(), piece.PlacedPieceId,
            new CuttingBreakageRequest("Chipped on the table"),
            null, CancellationToken.None);
        Assert.True(breakage.Success, breakage.Message);
        Assert.Equal(CuttingPieceStatuses.Breakage, breakage.Value!.Status);

        // The Phase 14 replacement: original never cancelled, replacement
        // piece + row created with the deterministic id.
        var replacementRows = await _replacements.ListByJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.True(replacementRows.Success, replacementRows.Message);
        var replacement = replacementRows.Value!.Single();
        Assert.Equal(1, replacement.Quantity);
        Assert.False(replacement.IsFoc);
        Assert.Equal(piece.SourcePieceId, replacement.OriginalPieceId);
        Assert.NotNull(replacement.ReplacementPieceId);
        Assert.EndsWith("|RPL:1", replacement.ReplacementId);

        var floor = await _production.ListForJobAsync(
            tenant, jobId, CancellationToken.None);
        var replacementPiece = floor.Value!.Single(w => w.Id == replacement.ReplacementPieceId);
        Assert.EndsWith("|RPL:1", replacementPiece.UniqueKey);
        Assert.Equal(WorkItemStatuses.Pending, replacementPiece.Status);

        // Progress counts breakage out of remaining.
        var progress = await _cutting.GetProgressByJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.Equal(1, progress.Value!.Breakage);
        Assert.Equal(7, progress.Value.Remaining);
    }

    [Fact]
    public async Task Execution_breakage_links_the_real_replacement_piece()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Cc6");
        var floor = await ReleaseAndListAsync(tenant, jobId);
        var outer = floor.Single(i => i.Stage == "Cutting" && i.Position == "Outer");
        var tempering = floor.Single(i => i.Stage == "Tempering");

        await _production.RecordStageAsync(
            tenant, outer.Id, Guid.NewGuid(),
            new StageProgressRequest(4), null, CancellationToken.None);
        var started = await _executions.StartAsync(
            tenant, Guid.NewGuid(), new StartExecutionRequest(tempering.Id),
            null, CancellationToken.None);
        Assert.True(started.Success, started.Message);

        var breakage = await _executions.RecordBreakageAsync(
            tenant, Guid.NewGuid(), started.Value!.ExecutionId,
            new ExecutionBreakageRequest("Cracked in the furnace", 1),
            null, CancellationToken.None);
        Assert.True(breakage.Success, breakage.Message);

        // The 26b closure: the child execution links the REAL replacement
        // piece (26a linked the original — mapping §1.7d).
        var rows = await _executions.ListByJobAsync(
            tenant, jobId, CancellationToken.None);
        var child = rows.Value!.Single(r => r.ParentExecutionId == started.Value.ExecutionId);
        var replacementRows = await _replacements.ListByJobAsync(
            tenant, jobId, CancellationToken.None);
        var replacement = replacementRows.Value!.Single();
        Assert.Equal(replacement.ReplacementPieceId, child.ProductionPieceId);
        Assert.Equal(tempering.Id, child.InputProductionPieceId);

        // The breakage event landed on the ORIGINAL work item.
        var events = await _production.GetEventsAsync(
            tenant, tempering.Id, CancellationToken.None);
        Assert.Contains(events.Value!, e => e.EventType == ProductionEntryTypes.Breakage);

        // Execution progress: 5 projected rows × Qty 4 + the replacement
        // child's Qty 1 (26a math, unchanged).
        var progress = await _executions.GetProgressByJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.Equal(5 * 4 + 1, progress.Value!.Required);
    }

    // ── the allocation ledger + actual costs ──────────────────────────

    [Fact]
    public async Task Costs_record_idempotently_derive_from_allocations_and_summarize()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Cc7");
        await ReleaseAndListAsync(tenant, jobId);
        await CreateRackAsync(tenant);
        var run = await _cutting.RunAsync(
            tenant, Guid.NewGuid(), new RunCuttingRequest(jobId, EffortLevel: 1),
            null, CancellationToken.None);
        Assert.True(run.Value!.Success, run.Value.Message);

        // Estimated bucket — idempotent update, never duplicated.
        var est1 = await _allocationCosts.RecordCostAsync(
            tenant, Guid.NewGuid(), new RecordCostRequest(
                jobId, 1, 1, ActualCostTypes.Estimated,
                Quantity: 10m, UnitCost: 20m, SellingValue: 200m),
            null, CancellationToken.None);
        Assert.True(est1.Success, est1.Message);
        var est2 = await _allocationCosts.RecordCostAsync(
            tenant, Guid.NewGuid(), new RecordCostRequest(
                jobId, 1, 1, ActualCostTypes.Estimated,
                Quantity: 12m, UnitCost: 20m, SellingValue: 240m),
            null, CancellationToken.None);
        Assert.True(est2.Success, est2.Message);
        Assert.Contains("updated (idempotent)", est2.Message);

        // FOC keeps cost, zeroes selling.
        var foc = await _allocationCosts.RecordCostAsync(
            tenant, Guid.NewGuid(), new RecordCostRequest(
                jobId, 1, 2, ActualCostTypes.Production,
                Quantity: 1m, UnitCost: 30m, SellingValue: 50m, IsFoc: true),
            null, CancellationToken.None);
        Assert.True(foc.Success, foc.Message);
        Assert.Equal(0m, foc.Value!.SellingValue);
        Assert.Equal(30m, foc.Value.TotalCost);

        // Derive the ActualMaterial costs from the allocations: piece sqm
        // × the rack's 45/sqm purchase price.
        var derive = await _allocationCosts.DeriveFromAllocationAsync(
            tenant, Guid.NewGuid(), jobId, null, CancellationToken.None);
        Assert.True(derive.Success, derive.Message);
        Assert.Contains("Derived 8 actual costs", derive.Value);

        var summary = await _allocationCosts.GetSummaryByJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.True(summary.Success, summary.Message);
        var s = summary.Value!;
        Assert.Equal(240m, s.Estimated); // 12 × 20, the updated row
        Assert.Equal(30m, s.Production);
        Assert.Equal(30m, s.FocImpact);  // cost incurred, selling zero
        // 8 pieces: 4 × (1000×2000) + 4 × (900×1800) = 8 + 6.48 sqm × 45.
        var expectedMaterial = Math.Round(
            (4m * 1000 * 2000 / 1_000_000m + 4m * 900 * 1800 / 1_000_000m) * 45m, 4);
        Assert.True(Math.Abs(s.ActualMaterial - expectedMaterial) < 0.01m,
            $"ActualMaterial {s.ActualMaterial} != {expectedMaterial}");
        // TotalActual falls back to the component sum when no explicit
        // TotalActual row exists.
        Assert.Equal(s.ActualMaterial + s.Production, s.TotalActual);
    }

    [Fact]
    public async Task Allocation_consume_never_overwrites_a_released_reservation()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Cc8");
        await ReleaseAndListAsync(tenant, jobId);
        var rack = await CreateRackAsync(tenant);

        // A manual reservation on the rack, then an allocation on it.
        var reservationService = new ReservationService(
            new ReservationStore(new SaasDbContextFactory(_fixture.AppDb)),
            new AuditWriter(new SaasDbContextFactory(_fixture.AppDb),
                NullLogger<AuditWriter>.Instance));
        var reserved = await reservationService.ReserveAsync(
            tenant, Guid.NewGuid(), new ReserveRequest(
                jobId, rack.Id, 1, 1, "Green", "6mm Green", null, 2),
            null, CancellationToken.None);
        Assert.True(reserved.Success, reserved.Message);

        var allocated = await _allocationCosts.AllocateAsync(
            tenant, Guid.NewGuid(), new AllocateRequest(
                jobId, reserved.Value!.ReservationKey, 1, "G-101 · 1/1 · Outer · 1/4",
                SpecGroupNumber: 1, SerialNumber: 1, GlassRef: "G-101",
                WidthMm: 1000, HeightMm: 2000),
            null, CancellationToken.None);
        Assert.True(allocated.Success, allocated.Message);
        Assert.Equal(AllocationStatuses.Allocated, allocated.Value!.Status);

        // Idempotent re-allocate.
        var again = await _allocationCosts.AllocateAsync(
            tenant, Guid.NewGuid(), new AllocateRequest(
                jobId, reserved.Value.ReservationKey, 1, "G-101 · 1/1 · Outer · 1/4"),
            null, CancellationToken.None);
        Assert.True(again.Success, again.Message);
        Assert.Contains("idempotent", again.Message);

        // Release the reservation, then consume the allocation: the
        // cascade must NOT overwrite Released.
        var released = await reservationService.ReleaseAsync(
            tenant, reserved.Value.Id, Guid.NewGuid(),
            new ReservationActionRequest("manual release"), null, CancellationToken.None);
        Assert.True(released.Success, released.Message);

        var consumed = await _allocationCosts.ConsumeAsync(
            tenant, Guid.NewGuid(), allocated.Value.AllocationId,
            null, CancellationToken.None);
        Assert.True(consumed.Success, consumed.Message);
        Assert.Equal("Consumed.", consumed.Value);

        var byReservation = await _allocationCosts.ListByReservationAsync(
            tenant, reserved.Value.ReservationKey, CancellationToken.None);
        Assert.Equal(AllocationStatuses.Consumed, byReservation.Value![0].Status);

        var afterRelease = await reservationService.ListForJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.All(afterRelease.Value!, r =>
            Assert.NotEqual(ReservationStatuses.Consumed, r.Status));
    }

    // ── replacements: sequence growth ─────────────────────────────────

    [Fact]
    public async Task Replacements_grow_the_sequence_per_work_item()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Cc9");
        var floor = await ReleaseAndListAsync(tenant, jobId);
        var outer = floor.Single(i => i.Stage == "Cutting" && i.Position == "Outer");

        var first = await _replacements.RecordBreakageAsync(
            tenant, Guid.NewGuid(), new ReplacementBreakageRequest(
                outer.Id, 2, "Two broke"),
            null, CancellationToken.None);
        Assert.True(first.Success, first.Message);
        Assert.Equal(2, first.Value!.Quantity);
        Assert.EndsWith("|RPL:1", first.Value.ReplacementId);

        var second = await _replacements.RecordBreakageAsync(
            tenant, Guid.NewGuid(), new ReplacementBreakageRequest(
                outer.Id, 1, "One more broke", IsFoc: true),
            null, CancellationToken.None);
        Assert.True(second.Success, second.Message);
        Assert.EndsWith("|RPL:2", second.Value!.ReplacementId);
        Assert.True(second.Value.IsFoc);
        Assert.Equal(ProductionEntryTypes.Foc, second.Value.EntryType);

        // The replacement work items carry the broken qty and the FOC
        // flag; the original is untouched.
        var updated = await _production.ListForJobAsync(
            tenant, jobId, CancellationToken.None);
        Assert.Equal(0, updated.Value!.Single(w => w.Id == outer.Id).QtyDone);
        var repl1 = updated.Value.Single(w => w.Id == first.Value.ReplacementPieceId);
        Assert.Equal(2, repl1.Qty);
        Assert.False(repl1.IsFoc);
        var repl2 = updated.Value.Single(w => w.Id == second.Value.ReplacementPieceId);
        Assert.True(repl2.IsFoc);
    }

    // ── integrity scanner + snapshot integrity ────────────────────────

    [Fact]
    public async Task Consistency_scan_reports_facts_observations_and_limits()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Cc10");
        await ReleaseAndListAsync(tenant, jobId);
        await CreateRackAsync(tenant);
        await _cutting.RunAsync(
            tenant, Guid.NewGuid(), new RunCuttingRequest(jobId, EffortLevel: 1),
            null, CancellationToken.None);

        var scan = await _integrity.ScanAsync(tenant, CancellationToken.None);
        Assert.True(scan.Success, scan.Message);
        var report = scan.Value!;

        Assert.True(report.ReservationRows >= 1);
        Assert.True(report.AllocationRows >= 8);
        Assert.Equal(1, report.GlassSheetRows);

        // The scanner's own contract: read-only verdicts, never a repair.
        Assert.Contains(report.Findings, f =>
            f.Code == ConsistencyCodes.RetryVsNewConsumption && f.Kind == "NotDetectable");
        Assert.Contains(report.Limitations, l => l.Contains("never writes"));

        // No fact findings for a healthy tenant.
        Assert.DoesNotContain(report.Findings, f =>
            f.Kind == nameof(ConsistencyKind.Fact));
    }

    [Fact]
    public async Task Snapshot_integrity_classifies_valid_and_empty()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Cc11");
        var valid = await _integrity.CheckSnapshotAsync(
            tenant, jobId, CancellationToken.None);
        Assert.True(valid.Success, valid.Message);
        Assert.Equal(nameof(ProductionSnapshotKind.Valid), valid.Value!.Kind);
        Assert.True(valid.Value.HasCutRows);

        // A job converted from a PI without spec rows is ValidEmpty — not
        // corrupt, not missing.
        var (emptyTenant, emptyJob, _) = await CreateJobAsync("Cc12", withRows: false);
        var empty = await _integrity.CheckSnapshotAsync(
            emptyTenant, emptyJob, CancellationToken.None);
        Assert.True(empty.Success, empty.Message);
        Assert.Equal(nameof(ProductionSnapshotKind.ValidEmpty), empty.Value!.Kind);
        Assert.Equal("no specification rows", empty.Value.Message);

        // Corrupt JSON is corrupt — never "a job with no glass" (raw
        // update through the owner connection).
        var (corruptTenant, corruptJob, _) = await CreateJobAsync("Cc13");
        using (var connection = new NpgsqlConnection(_fixture.OwnerDb))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "UPDATE saas.job_orders SET snapshot_json = '{corrupt' WHERE id = $1";
            command.Parameters.AddWithValue(corruptJob);
            command.ExecuteNonQuery();
        }
        var corrupt = await _integrity.CheckSnapshotAsync(
            corruptTenant, corruptJob, CancellationToken.None);
        Assert.True(corrupt.Success, corrupt.Message);
        Assert.Equal(nameof(ProductionSnapshotKind.Corrupt), corrupt.Value!.Kind);
        Assert.Contains("corrupt", corrupt.Value.Message);
    }

    // ── reports ───────────────────────────────────────────────────────

    [Fact]
    public async Task Reports_build_process_status_and_route_rows()
    {
        var (tenant, jobId, _) = await CreateJobAsync("Cc14");
        var floor = await ReleaseAndListAsync(tenant, jobId);
        var outer = floor.Single(i => i.Stage == "Cutting" && i.Position == "Outer");
        var tempering = floor.Single(i => i.Stage == "Tempering");

        await _production.RecordStageAsync(
            tenant, outer.Id, Guid.NewGuid(),
            new StageProgressRequest(4), null, CancellationToken.None);
        var started = await _executions.StartAsync(
            tenant, Guid.NewGuid(), new StartExecutionRequest(tempering.Id),
            null, CancellationToken.None);
        Assert.True(started.Success, started.Message);

        var status = await _reports.GenerateProcessStatusAsync(
            tenant, jobId, CancellationToken.None);
        Assert.True(status.Success, status.Message);
        Assert.Equal(5, status.Value!.Count);
        Assert.All(status.Value, r => Assert.Contains("→ QC", r.Traceability));
        Assert.All(status.Value, r => Assert.Contains("PI ", r.Traceability));

        var route = await _reports.GenerateRouteReportAsync(
            tenant, jobId, CancellationToken.None);
        Assert.True(route.Success, route.Message);
        // Two units (Outer/Inner of spec 1 serial 1) → two route rows.
        Assert.Equal(2, route.Value!.Count);
        Assert.All(route.Value, r => Assert.Contains("Cutting", r.Route));

        var pairing = await _reports.GeneratePairingAssemblyTraceabilityAsync(
            tenant, jobId, CancellationToken.None);
        Assert.True(pairing.Success, pairing.Message);
        Assert.Empty(pairing.Value!); // no pairings computed yet

        var rework = await _reports.GenerateReworkQueueReportAsync(
            tenant, CancellationToken.None);
        Assert.True(rework.Success, rework.Message);

        var qc = await _reports.GenerateQcQueueReportAsync(
            tenant, CancellationToken.None);
        Assert.True(qc.Success, qc.Message);
    }
}
