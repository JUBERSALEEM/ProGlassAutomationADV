using ProGlass.Saas.Domain.Proforma;
using Xunit;

namespace ProGlass.Saas.Domain.Tests;

/// <summary>
/// WPF goldens for the Phase 26a process/control engine (read
/// 2026-09-27): ProductionProcessRoutingService (routes, gating,
/// deterministic ids), ProductionReadinessEngine (the ten-state readiness
/// with explainable block reasons), ProductionPieceIdentity (pane
/// instances JOB|SPEC|SERIAL|POSITION|WxH|P0001 ± |RPL:n), the queue
/// ordering, timing and progress models, and the block-reason vocabulary.
/// All pure — same inputs in, identical outputs out.
/// </summary>
public sealed class ProductionProcessTests
{
    private static RouteWorkItem Wi(
        string stage, string status = WorkItemStatuses.Pending,
        string position = "Front", int qty = 4, int qtyDone = 0,
        int spec = 1, int serial = 1, bool cancelled = false,
        int width = 1000, int height = 2000, int replacement = 0) => new(
        "JOB-2026-0042", "PI-1", spec, "Spec A", serial, position,
        "SGU", "6CL", stage, status, cancelled, qty, qtyDone,
        $"JOB-2026-0042|{spec}|{serial}|{position}|{stage}",
        width, height, replacement);

    // ── deterministic identities ─────────────────────────────────────

    [Fact]
    public void Execution_id_is_job_spec_serial_type_sequence()
    {
        Assert.Equal(
            "JOB-2026-0042|2|7|6|0",
            ProcessRouting.BuildExecutionId("JOB-2026-0042", 2, 7, 6, 0));
    }

    [Fact]
    public void Execution_id_trims_the_job_ref()
    {
        Assert.Equal(
            "J1|1|1|1|0",
            ProcessRouting.BuildExecutionId("  J1  ", 1, 1, 1, 0));
    }

    [Fact]
    public void Projected_execution_id_appends_sanitized_position()
    {
        Assert.Equal(
            "J1|1|1|6|0|Outer",
            ProcessRouting.BuildProjectedExecutionId("J1", 1, 1, 6, "Outer"));
        Assert.Equal(
            "J1|1|1|6|0|Outer/Left",
            ProcessRouting.BuildProjectedExecutionId("J1", 1, 1, 6, "Outer|Left"));
    }

    [Fact]
    public void Projected_execution_id_without_position_keeps_legacy_id()
    {
        Assert.Equal(
            "J1|1|1|6|0",
            ProcessRouting.BuildProjectedExecutionId("J1", 1, 1, 6, " "));
    }

    [Fact]
    public void Pairing_and_assembly_ids_use_zero_padded_sequences()
    {
        Assert.Equal("J1|2|3|Pair:000",
            ProcessRouting.BuildPairingId("J1", 2, 3, 0));
        Assert.Equal("J1|2|3|Pair:011",
            ProcessRouting.BuildPairingId("J1", 2, 3, 11));
        Assert.Equal("J1|2|3|DGU:007",
            ProcessRouting.BuildAssemblyId("J1", 2, 3, 7, "dgu"));
        Assert.Equal("J1|2|3|LAM:007",
            ProcessRouting.BuildAssemblyId("J1", 2, 3, 7, "LAM"));
        Assert.Equal("J1|2|3|DGU:001",
            ProcessRouting.BuildAssemblyId("J1", 2, 3, 1, null));
    }

    [Fact]
    public void Control_ids_carry_their_markers()
    {
        Assert.Equal("J1|1|1|6|HOLD:002",
            ProcessRouting.BuildHoldId("J1", 1, 1, 6, 2));
        Assert.Equal("J1|1|1|6|ASSIGN:000",
            ProcessRouting.BuildAssignmentId("J1", 1, 1, 6, 0));
        Assert.Equal("J1|1|1|6|CTRL",
            ProcessRouting.BuildControlId("J1", 1, 1, 6));
    }

    [Fact]
    public void Pane_instance_id_is_deterministic_and_replaceable()
    {
        Assert.Equal(
            "J1|1|1|Outer|1000x2000|P0001",
            ProductionPieceIdentity.BuildPaneInstanceId("J1", 1, 1, "Outer", 1000, 2000, 1));
        Assert.Equal(
            "J1|1|1|Outer|1000x2000|P0012|RPL:3",
            ProductionPieceIdentity.BuildPaneInstanceId("J1", 1, 1, "Outer", 1000, 2000, 12, 3));
    }

    [Fact]
    public void Pane_instance_ids_are_recognized()
    {
        Assert.True(ProductionPieceIdentity.IsPaneInstanceId(
            "J1|1|1|Outer|1000x2000|P0001"));
        Assert.True(ProductionPieceIdentity.IsPaneInstanceId(
            "J1|1|1|Outer|1000x2000|P0001|RPL:2"));
        Assert.False(ProductionPieceIdentity.IsPaneInstanceId(
            "J1|1|1|6|0"));
        Assert.False(ProductionPieceIdentity.IsPaneInstanceId(""));
    }

    [Fact]
    public void Classify_separates_the_identity_families()
    {
        Assert.Equal("pane-instance",
            ProductionPieceIdentity.Classify("J1|1|1|Outer|1000x2000|P0001"));
        Assert.Equal("pane-instance-replacement",
            ProductionPieceIdentity.Classify("J1|1|1|Outer|1000x2000|P0001|RPL:1"));
        Assert.Equal("cutting-session",
            ProductionPieceIdentity.Classify("CUT-J1-001"));
        Assert.Equal("pairing",
            ProductionPieceIdentity.Classify("J1|1|1|Pair:000"));
        Assert.Equal("assembly",
            ProductionPieceIdentity.Classify("J1|1|1|DGU:000"));
        Assert.Equal("assembly",
            ProductionPieceIdentity.Classify("J1|1|1|LAM:000"));
        Assert.Equal("process-execution",
            ProductionPieceIdentity.Classify("J1|1|1|6|0"));
        Assert.Equal("workitem-replacement",
            ProductionPieceIdentity.Classify("J1|1|1|Outer|Cutting|RPL:1"));
        Assert.Equal("workitem-or-other",
            ProductionPieceIdentity.Classify("anything-else"));
        Assert.Equal("none", ProductionPieceIdentity.Classify(null));
    }

    // ── routing ──────────────────────────────────────────────────────

    [Fact]
    public void Sgu_route_is_cutting_only()
    {
        var route = ProcessRouting.BuildRouteForWorkItem(
            Wi(ProductionStages.CuttingName), new[] { Wi(ProductionStages.CuttingName) });
        Assert.Equal(new[] { "Cutting" }, route.RequiredStages);
        Assert.Equal("Cutting", route.RouteLabel);
    }

    [Fact]
    public void Full_processing_route_is_canonical_order()
    {
        var unit = new[]
        {
            Wi(ProductionStages.CuttingName),
            Wi(ProductionStages.PolishName),
            Wi(ProductionStages.MitringName),
            Wi(ProductionStages.HolesCutoutsName),
            Wi(ProductionStages.TemperingName)
        };
        var route = ProcessRouting.BuildRouteForWorkItem(Wi(ProductionStages.CuttingName), unit);
        Assert.Equal(
            new[] { "Cutting", "HolesCutouts", "Polish", "Mitring", "Tempering" },
            route.RequiredStages);
    }

    [Fact]
    public void Assembly_stages_only_route_for_unit_positions()
    {
        var unit = new[]
        {
            Wi(ProductionStages.CuttingName, position: "Outer"),
            Wi(ProductionStages.CuttingName, position: "Inner"),
            Wi(ProductionStages.PairingName, position: "Unit"),
            Wi(ProductionStages.DoublingLaminationName, position: "Unit")
        };
        var paneRoute = ProcessRouting.BuildRouteForWorkItem(
            Wi(ProductionStages.CuttingName, position: "Outer"), unit);
        Assert.Equal(new[] { "Cutting" }, paneRoute.RequiredStages);

        var unitRoute = ProcessRouting.BuildRouteForWorkItem(
            Wi(ProductionStages.DoublingLaminationName, position: "Unit"), unit);
        Assert.Equal(
            new[] { "Cutting", "Pairing", "DoublingLamination" },
            unitRoute.RequiredStages);
    }

    [Fact]
    public void Next_and_previous_follow_the_route()
    {
        var unit = new[]
        {
            Wi(ProductionStages.CuttingName),
            Wi(ProductionStages.PolishName),
            Wi(ProductionStages.TemperingName)
        };
        var route = ProcessRouting.BuildRouteForWorkItem(
            Wi(ProductionStages.PolishName), unit);
        Assert.Equal("Cutting", route.PreviousRequiredBefore("Polish"));
        Assert.Equal("Tempering", route.NextRequiredAfter("Polish"));
        Assert.Null(route.PreviousRequiredBefore("Cutting"));
        Assert.Null(route.NextRequiredAfter("Tempering"));
        Assert.Null(route.PreviousRequiredBefore("Pairing"));
    }

    [Fact]
    public void Full_job_route_inserts_cutting_and_orders()
    {
        var job = new[]
        {
            Wi(ProductionStages.TemperingName),
            Wi(ProductionStages.CuttingName)
        };
        var stages = ProcessRouting.BuildFullJobRoute(job);
        Assert.Equal(new[] { "Cutting", "Tempering" }, stages);
        Assert.Equal(
            new[] { "Cutting" },
            ProcessRouting.BuildFullJobRoute(Array.Empty<RouteWorkItem>()));
    }

    [Fact]
    public void Can_begin_fails_closed_on_unknown_predecessor()
    {
        var unit = new[] { Wi(ProductionStages.CuttingName), Wi(ProductionStages.PolishName) };
        var route = ProcessRouting.BuildRouteForWorkItem(Wi(ProductionStages.PolishName), unit);

        Assert.True(ProcessRouting.CanBegin("Cutting", route,
            new Dictionary<string, string>()));
        Assert.False(ProcessRouting.CanBegin("Polish", route,
            new Dictionary<string, string>()));
        Assert.True(ProcessRouting.CanBegin("Polish", route,
            new Dictionary<string, string> { ["Cutting"] = WorkItemStatuses.Completed }));
        Assert.False(ProcessRouting.CanBegin("Polish", route,
            new Dictionary<string, string> { ["Cutting"] = WorkItemStatuses.InProgress }));
    }

    // ── readiness ────────────────────────────────────────────────────

    private static readonly IReadOnlyDictionary<(int, int, string, string), string> NoQc =
        new Dictionary<(int, int, string, string), string>();

    private static readonly IReadOnlyDictionary<(int, int), bool> NoCompletion =
        new Dictionary<(int, int), bool>();

    private static (ProductionReadiness, string) Readiness(
        RouteWorkItem wi,
        IReadOnlyList<RouteWorkItem>? job = null,
        IReadOnlyList<RouteWorkItem>? unit = null,
        IReadOnlyDictionary<(int, int, string, string), string>? qc = null,
        bool hasReservation = true,
        bool onHold = false,
        string holdReason = "") =>
        ProductionReadinessEngine.Calculate(
            wi, job ?? new[] { wi }, unit ?? new[] { wi },
            qc ?? NoQc, NoCompletion, NoCompletion,
            hasReservation, hasAllocation: false, onHold, holdReason);

    [Fact]
    public void Cancelled_work_is_cancelled()
    {
        var (readiness, reason) = Readiness(
            Wi(ProductionStages.CuttingName, cancelled: true));
        Assert.Equal(ProductionReadiness.Cancelled, readiness);
        Assert.Equal(ProductionBlockReasons.Cancelled, reason);
    }

    [Fact]
    public void Held_work_reports_the_hold_reason()
    {
        var (readiness, reason) = Readiness(
            Wi(ProductionStages.CuttingName), onHold: true, holdReason: "Paint cure");
        Assert.Equal(ProductionReadiness.OnHold, readiness);
        Assert.Equal("Paint cure", reason);
    }

    [Fact]
    public void Held_work_without_reason_uses_the_default_label()
    {
        var (_, reason) = Readiness(Wi(ProductionStages.CuttingName), onHold: true);
        Assert.Equal(ProductionBlockReasons.OnHold, reason);
    }

    [Fact]
    public void Completed_needs_status_and_quantity()
    {
        var (done, _) = Readiness(Wi(ProductionStages.CuttingName,
            status: WorkItemStatuses.Completed, qtyDone: 4));
        Assert.Equal(ProductionReadiness.Completed, done);

        var (notDone, _) = Readiness(Wi(ProductionStages.CuttingName,
            status: WorkItemStatuses.Completed, qtyDone: 3));
        Assert.NotEqual(ProductionReadiness.Completed, notDone);
    }

    [Theory]
    [InlineData(WorkItemStatuses.Rework, ProductionReadiness.Rework,
        ProductionBlockReasons.WaitingForRework)]
    [InlineData(WorkItemStatuses.Rejected, ProductionReadiness.QcFailed,
        ProductionBlockReasons.QcFailed)]
    [InlineData(WorkItemStatuses.QcPending, ProductionReadiness.QcPending,
        ProductionBlockReasons.WaitingForQc)]
    [InlineData(WorkItemStatuses.QcFailed, ProductionReadiness.QcFailed,
        ProductionBlockReasons.QcFailed)]
    [InlineData(WorkItemStatuses.InProgress, ProductionReadiness.InProgress,
        ProductionBlockReasons.None)]
    [InlineData(WorkItemStatuses.Paused, ProductionReadiness.Paused,
        ProductionBlockReasons.None)]
    public void Status_map_is_verbatim(string status, ProductionReadiness expected,
        string expectedReason)
    {
        var (readiness, reason) = Readiness(Wi(ProductionStages.CuttingName, status: status));
        Assert.Equal(expected, readiness);
        Assert.Equal(expectedReason, reason);
    }

    [Fact]
    public void Cutting_without_reservation_is_blocked()
    {
        var (readiness, reason) = Readiness(
            Wi(ProductionStages.CuttingName), hasReservation: false);
        Assert.Equal(ProductionReadiness.Blocked, readiness);
        Assert.Equal(ProductionBlockReasons.NoReservation, reason);
    }

    [Fact]
    public void Cutting_with_reservation_is_ready()
    {
        var (readiness, reason) = Readiness(Wi(ProductionStages.CuttingName));
        Assert.Equal(ProductionReadiness.Ready, readiness);
        Assert.Equal(ProductionBlockReasons.None, reason);
    }

    [Fact]
    public void Incomplete_predecessor_blocks_with_stage_reason()
    {
        var unit = new[]
        {
            Wi(ProductionStages.CuttingName, qtyDone: 2),   // 2/4 — incomplete
            Wi(ProductionStages.HolesCutoutsName)
        };
        var (readiness, reason) = Readiness(
            unit[1], job: unit, unit: unit);
        Assert.Equal(ProductionReadiness.Blocked, readiness);
        Assert.Equal(ProductionBlockReasons.WaitingForCutting, reason);
    }

    [Fact]
    public void Predecessor_qc_failed_blocks()
    {
        var unit = new[]
        {
            Wi(ProductionStages.CuttingName,
                status: WorkItemStatuses.Completed, qtyDone: 4),
            Wi(ProductionStages.HolesCutoutsName)
        };
        var qc = new Dictionary<(int, int, string, string), string>
        {
            [(1, 1, "Front", ProductionStages.CuttingName)] = QcResults.Failed
        };
        var (readiness, reason) = Readiness(unit[1], job: unit, unit: unit, qc: qc);
        Assert.Equal(ProductionReadiness.Blocked, readiness);
        Assert.Equal(ProductionBlockReasons.QcFailed, reason);
    }

    [Fact]
    public void Predecessor_qc_pending_blocks_but_rework_does_not()
    {
        var unit = new[]
        {
            Wi(ProductionStages.CuttingName,
                status: WorkItemStatuses.Completed, qtyDone: 4),
            Wi(ProductionStages.HolesCutoutsName)
        };

        var pendingQc = new Dictionary<(int, int, string, string), string>
        {
            [(1, 1, "Front", ProductionStages.CuttingName)] = QcResults.Pending
        };
        var (blocked, waiting) = Readiness(unit[1], job: unit, unit: unit, qc: pendingQc);
        Assert.Equal(ProductionReadiness.Blocked, blocked);
        Assert.Equal(ProductionBlockReasons.WaitingForQc, waiting);

        var reworkQc = new Dictionary<(int, int, string, string), string>
        {
            [(1, 1, "Front", ProductionStages.CuttingName)] = QcResults.Rework
        };
        var (ready, _) = Readiness(unit[1], job: unit, unit: unit, qc: reworkQc);
        Assert.Equal(ProductionReadiness.Ready, ready);
    }

    [Fact]
    public void Complete_predecessor_with_passed_qc_unblocks()
    {
        var unit = new[]
        {
            Wi(ProductionStages.CuttingName,
                status: WorkItemStatuses.Completed, qtyDone: 4),
            Wi(ProductionStages.HolesCutoutsName)
        };
        var qc = new Dictionary<(int, int, string, string), string>
        {
            [(1, 1, "Front", ProductionStages.CuttingName)] = QcResults.Passed
        };
        var (readiness, _) = Readiness(unit[1], job: unit, unit: unit, qc: qc);
        Assert.Equal(ProductionReadiness.Ready, readiness);
    }

    [Fact]
    public void Tempering_waits_for_every_edge_stage()
    {
        var predecessors = new[]
        {
            ProductionBlockReasons.WaitingForCutting,
            ProductionBlockReasons.WaitingForHolesCutouts,
            ProductionBlockReasons.WaitingForPolish,
            ProductionBlockReasons.WaitingForEdgeDeletion,
            ProductionBlockReasons.WaitingForMitring
        };
        foreach (var stage in new[]
                 {
                     ProductionStages.CuttingName, ProductionStages.HolesCutoutsName,
                     ProductionStages.PolishName, ProductionStages.EdgeDeletionName,
                     ProductionStages.MitringName
                 })
        {
            var unit = new[]
            {
                Wi(stage, status: WorkItemStatuses.InProgress, qtyDone: 1),
                Wi(ProductionStages.TemperingName)
            };
            var (readiness, reason) = Readiness(unit[1], job: unit, unit: unit);
            Assert.Equal(ProductionReadiness.Blocked, readiness);
            Assert.Contains(reason, predecessors);
        }
    }

    [Fact]
    public void Unit_pairing_waits_for_outer_and_inner()
    {
        var job = new[]
        {
            Wi(ProductionStages.CuttingName, position: "Outer",
                status: WorkItemStatuses.InProgress, qtyDone: 1),
            Wi(ProductionStages.CuttingName, position: "Inner",
                status: WorkItemStatuses.InProgress, qtyDone: 1),
            Wi(ProductionStages.PairingName, position: "Unit")
        };
        var (both, bothReason) = Readiness(job[2], job: job, unit: job);
        Assert.Equal(ProductionReadiness.Blocked, both);
        Assert.Equal(
            ProductionBlockReasons.WaitingForOuterComponent + " & "
            + ProductionBlockReasons.WaitingForInnerComponent,
            bothReason);

        var outerDone = job.Select(w => w).ToList();
        outerDone[0] = Wi(ProductionStages.CuttingName, position: "Outer",
            status: WorkItemStatuses.Completed, qtyDone: 4);
        var (outer, outerReason) = Readiness(job[2], job: outerDone, unit: outerDone);
        Assert.Equal(ProductionReadiness.Blocked, outer);
        Assert.Equal(ProductionBlockReasons.WaitingForInnerComponent, outerReason);
    }

    [Fact]
    public void Unit_pairing_ready_when_components_complete()
    {
        var job = new[]
        {
            Wi(ProductionStages.CuttingName, position: "Outer",
                status: WorkItemStatuses.Completed, qtyDone: 4),
            Wi(ProductionStages.CuttingName, position: "Inner",
                status: WorkItemStatuses.Completed, qtyDone: 4),
            Wi(ProductionStages.PairingName, position: "Unit")
        };
        var (readiness, _) = Readiness(job[2], job: job, unit: job);
        Assert.Equal(ProductionReadiness.Ready, readiness);
    }

    [Fact]
    public void Unit_doubling_gates_on_pairing_work_item_pcs()
    {
        // Supporting pairing rows may read Completed while the work
        // item's QtyDone is still 0 — the PCS row is authoritative.
        var job = new[]
        {
            Wi(ProductionStages.PairingName, position: "Unit", qty: 4, qtyDone: 2),
            Wi(ProductionStages.DoublingLaminationName, position: "Unit")
        };
        var (readiness, reason) = Readiness(job[1], job: job, unit: job);
        Assert.Equal(ProductionReadiness.Blocked, readiness);
        Assert.Equal(ProductionBlockReasons.PairingIncomplete, reason);

        var done = new[]
        {
            Wi(ProductionStages.PairingName, position: "Unit",
                status: WorkItemStatuses.Completed, qty: 4, qtyDone: 4),
            Wi(ProductionStages.DoublingLaminationName, position: "Unit")
        };
        var (ready, _) = Readiness(done[1], job: done, unit: done);
        Assert.Equal(ProductionReadiness.Ready, ready);
    }

    // ── due risk + priority ──────────────────────────────────────────

    [Fact]
    public void Due_risk_buckets()
    {
        var today = new DateTime(2026, 9, 27);
        Assert.Equal(DueDateRisk.NoDate,
            ProductionReadinessEngine.CalculateDueRisk(null, today));
        Assert.Equal(DueDateRisk.Overdue,
            ProductionReadinessEngine.CalculateDueRisk(today.AddDays(-1), today));
        Assert.Equal(DueDateRisk.AtRisk,
            ProductionReadinessEngine.CalculateDueRisk(today.AddDays(2), today));
        Assert.Equal(DueDateRisk.DueSoon,
            ProductionReadinessEngine.CalculateDueRisk(today.AddDays(7), today));
        Assert.Equal(DueDateRisk.OnTrack,
            ProductionReadinessEngine.CalculateDueRisk(today.AddDays(8), today));
    }

    [Fact]
    public void Priority_parse()
    {
        Assert.Equal(ProductionPriority.Normal,
            ProductionReadinessEngine.ParsePriority(0));
        Assert.Equal(ProductionPriority.High,
            ProductionReadinessEngine.ParsePriority(1));
        Assert.Equal(ProductionPriority.Urgent,
            ProductionReadinessEngine.ParsePriority(2));
        Assert.Equal(ProductionPriority.Normal,
            ProductionReadinessEngine.ParsePriority(99));
    }

    // ── queue ordering ───────────────────────────────────────────────

    private static ProductionQueueItem Item(
        string jobRef, string stage, ProductionReadiness readiness,
        ProductionPriority priority = ProductionPriority.Normal,
        DateTime? due = null, int serial = 1, string position = "Front",
        string uniqueKey = "") => new()
    {
        JobRef = jobRef,
        Stage = stage,
        SerialNumber = serial,
        Position = position,
        UniqueKey = uniqueKey,
        Readiness = readiness,
        Priority = priority,
        DueDate = due
    };

    [Fact]
    public void Queue_orders_ready_then_inprogress_then_other_then_blocked()
    {
        var queue = new List<ProductionQueueItem>
        {
            Item("J2", "Cutting", ProductionReadiness.Blocked),
            Item("J2", "Cutting", ProductionReadiness.Rework),
            Item("J2", "Cutting", ProductionReadiness.InProgress),
            Item("J2", "Cutting", ProductionReadiness.Ready)
        };
        var ordered = queue.Order().ToList();
        Assert.Equal(ProductionReadiness.Ready, ordered[0].Readiness);
        Assert.Equal(ProductionReadiness.InProgress, ordered[1].Readiness);
        Assert.Equal(ProductionReadiness.Rework, ordered[2].Readiness);
        Assert.Equal(ProductionReadiness.Blocked, ordered[3].Readiness);
    }

    [Fact]
    public void Queue_breaks_ties_by_priority_then_due_date()
    {
        var queue = new List<ProductionQueueItem>
        {
            Item("J1", "Cutting", ProductionReadiness.Ready,
                due: new DateTime(2026, 10, 5)),
            Item("J1", "Cutting", ProductionReadiness.Ready,
                priority: ProductionPriority.Urgent),
            Item("J1", "Cutting", ProductionReadiness.Ready,
                due: new DateTime(2026, 10, 1))
        };
        var ordered = queue.Order().ToList();
        Assert.Equal(ProductionPriority.Urgent, ordered[0].Priority);
        Assert.Equal(new DateTime(2026, 10, 1), ordered[1].DueDate);
        Assert.Null(ordered[2].DueDate); // null due dates sort last
    }

    [Fact]
    public void Queue_orders_by_stage_job_and_piece_identity()
    {
        var queue = new List<ProductionQueueItem>
        {
            Item("J2", "Tempering", ProductionReadiness.Ready, serial: 1),
            Item("J1", "Polish", ProductionReadiness.Ready, serial: 2),
            Item("J1", "Polish", ProductionReadiness.Ready, serial: 1),
            Item("J1", "Cutting", ProductionReadiness.Ready, serial: 9)
        };
        var ordered = queue.Order().ToList();
        Assert.Equal("Cutting", ordered[0].Stage);
        Assert.Equal(1, ordered[1].SerialNumber);
        Assert.Equal(2, ordered[2].SerialNumber);
        Assert.Equal("J2", ordered[3].JobRef);
    }

    // ── progress math ────────────────────────────────────────────────

    [Fact]
    public void Progress_math_is_physical_pieces()
    {
        var p = new ProductionProgress
        {
            Required = 20,
            Completed = 14
        };
        Assert.Equal(6, p.Remaining);
        Assert.Equal(70.0, p.ProgressPct);
        Assert.Equal("14/20 (70%)", p.Label);
    }

    [Fact]
    public void Progress_pct_is_zero_when_nothing_required()
    {
        var p = new ProductionProgress { Required = 0, Completed = 0 };
        Assert.Equal(0, p.ProgressPct);
        Assert.Equal("0/0 (0%)", p.Label);
    }

    // ── timing (from existing timestamps, never fabricated) ─────────

    [Fact]
    public void Timing_pending_waits_since_creation()
    {
        var now = new DateTime(2026, 9, 27, 12, 0, 0);
        var item = new ProductionQueueItem
        {
            Status = WorkItemStatuses.Pending,
            CreatedAt = now.AddHours(-3),
            UpdatedAt = now.AddHours(-1)
        };
        ProductionTiming.EnrichTiming(new List<ProductionQueueItem> { item }, now);
        Assert.Equal(3, item.QueueWaitTime!.Value.TotalHours);
        Assert.Null(item.ProcessingDuration);
    }

    [Fact]
    public void Timing_completed_processing_spans_creation_to_completion()
    {
        var now = new DateTime(2026, 9, 27, 12, 0, 0);
        var item = new ProductionQueueItem
        {
            Status = WorkItemStatuses.Completed,
            CreatedAt = now.AddHours(-5),
            CompletedAt = now.AddHours(-1),
            UpdatedAt = now.AddHours(-2)
        };
        ProductionTiming.EnrichTiming(new List<ProductionQueueItem> { item }, now);
        Assert.Equal(4, item.ProcessingDuration!.Value.TotalHours);
        Assert.Equal(3, item.QueueWaitTime!.Value.TotalHours);
    }

    [Fact]
    public void Timing_active_work_splits_wait_and_processing()
    {
        var now = new DateTime(2026, 9, 27, 12, 0, 0);
        var item = new ProductionQueueItem
        {
            Status = WorkItemStatuses.InProgress,
            CreatedAt = now.AddHours(-4),
            UpdatedAt = now.AddHours(-1)
        };
        ProductionTiming.EnrichTiming(new List<ProductionQueueItem> { item }, now);
        Assert.Equal(4, item.QueueWaitTime!.Value.TotalHours);
        Assert.Equal(1, item.ProcessingDuration!.Value.TotalHours);
    }

    // ── queue pressure thresholds ────────────────────────────────────

    [Fact]
    public void Pressure_labels_follow_the_thresholds()
    {
        Assert.Equal("Idle", new ProductionQueuePressure().PressureLabel);
        Assert.Equal("Low Pressure",
            new ProductionQueuePressure { ReadyQueue = 1 }.PressureLabel);
        Assert.Equal("Medium Pressure",
            new ProductionQueuePressure { ReadyQueue = 9 }.PressureLabel);
        Assert.Equal("High Pressure",
            new ProductionQueuePressure { ReadyQueue = 12, BlockedQueue = 4 }
                .PressureLabel);
        Assert.Equal(16,
            new ProductionQueuePressure { ReadyQueue = 12, BlockedQueue = 4 }.Total);
    }

    // ── process detail summaries ─────────────────────────────────────

    [Fact]
    public void Detail_summary_counts_holes_and_cutouts()
    {
        Assert.Equal("3 hole(s), 1 cutout(s)",
            ProcessDetailSummary.Summarize(
                """{"holes":3,"cutouts":1}""", 2));
    }

    [Fact]
    public void Detail_summary_reads_polish_type_and_temperature()
    {
        Assert.Equal("Double Polish",
            ProcessDetailSummary.Summarize("""{"polishType":"Double Polish"}""", 3));
        Assert.Equal("680°C",
            ProcessDetailSummary.Summarize("""{"temperature":680}""", 6));
        Assert.Equal("Bright Polish",
            ProcessDetailSummary.Summarize("""{"edgeDeleted":"Bright Polish"}""", 4));
        Assert.Equal("45 degrees",
            ProcessDetailSummary.Summarize("""{"edges":"45 degrees"}""", 5));
    }

    [Fact]
    public void Detail_summary_is_empty_for_unknown_shapes()
    {
        Assert.Equal("", ProcessDetailSummary.Summarize("{}", 2));
        Assert.Equal("", ProcessDetailSummary.Summarize("not json", 2));
        Assert.Equal("", ProcessDetailSummary.Summarize("""{"anything":1}""", 7));
    }
}
