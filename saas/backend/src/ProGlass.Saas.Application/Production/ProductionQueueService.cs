using System.Text.Json;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Production;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 26a service — the WPF queue + dashboard + progress core:
//    • ProductionQueueService.GetQueueAsync (aggregates existing work
//      items — never duplicate jobs; readiness + explainable block
//      reasons + priority/due-date + operator/workstation + material +
//      traceability + DGU lineage; deterministic ordering: Ready →
//      InProgress → other → Blocked, priority desc, due date, process
//      sequence, JobRef, piece identity — never DB insertion order,
//      never random)
//    • ProductionDashboardService.GetKpisAsync / GetQueuePressureAsync /
//      GetWorkstationLoadAsync
//    • ProductionProgressService.GetJobProgressAsync /
//      GetOverallProgressAsync / GetProcessProgressAsync (physical
//      pieces — distinct Spec|Serial units, final stage dictates the
//      bucket, DGU components never double-counted)
//    • ProductionTimingService (timing from existing timestamps, never
//      fabricated — enriched inside the queue build so pressure shows
//      real wait times)
//  SaaS differences (documented, mapping §1.7c): JobRef = the job
//  order's number (joined, not denormalized on the piece); the queue
//  due date falls back to job_orders.due_date when no override exists
//  (the WPF left it null with a TODO — same intent, the column exists
//  here); allocations are the 26b slice so allocated/consumed read 0;
//  the dispatch gateway (ReadyForDelivery primary source) is a later
//  slice — the WPF's own fallback formula is used; CompletedToday uses
//  the stored completed_at (UTC day).
// ═══════════════════════════════════════════════════════════════════════

public sealed class ProductionQueueService(IProcessControlStore store)
{
    private const int DefaultTake = 200;
    private const int DashboardTake = 1000;

    // ── the unified queue ────────────────────────────────────────────

    /// <summary>
    /// GET /queue — filters: job, stage, readiness, priority,
    /// workstation, search, includeCompleted, includeCancelled. The
    /// readiness/priority/workstation filters run after the readiness
    /// calculation (WPF verbatim).
    /// </summary>
    public async Task<ProductionResult<ProductionQueueDto>> GetQueueAsync(
        Guid tenantId, Guid? jobOrderId = null, string? stage = null,
        string? readiness = null, string? priority = null,
        string? workstation = null, string? search = null,
        string? customer = null, bool includeCompleted = false,
        bool includeCancelled = false, int take = DefaultTake,
        CancellationToken ct = default)
    {
        if (take <= 0) take = DefaultTake;

        // WPF fetches take*2 so post-readiness filters still have rows.
        var items = await BuildQueueAsync(
            tenantId, jobOrderId, includeCompleted, includeCancelled, take * 2, ct: ct);

        // Stage filter (name key).
        if (!string.IsNullOrWhiteSpace(stage))
        {
            items = items.Where(q => string.Equals(q.Stage, stage.Trim(),
                StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Search — JobRef / Customer / Spec / GlassRef / source PI.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            items = items.Where(q =>
                q.JobRef.Contains(term, StringComparison.OrdinalIgnoreCase)
                || q.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || q.SpecName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || q.GlassRef.Contains(term, StringComparison.OrdinalIgnoreCase)
                || q.SourceProformaId.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(customer))
        {
            var term = customer.Trim();
            items = items.Where(q => q.CustomerName.Contains(term,
                StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(readiness)
            && Enum.TryParse<ProductionReadiness>(readiness.Trim(), true, out var readinessFilter))
        {
            items = items.Where(q => q.Readiness == readinessFilter).ToList();
        }

        if (!string.IsNullOrWhiteSpace(priority)
            && Enum.TryParse<ProductionPriority>(priority.Trim(), true, out var priorityFilter))
        {
            items = items.Where(q => q.Priority == priorityFilter).ToList();
        }

        if (!string.IsNullOrWhiteSpace(workstation))
        {
            var ws = workstation.Trim();
            items = items.Where(q => string.Equals(q.WorkstationId, ws,
                StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var ordered = items.Order().Take(take).ToList();

        var dto = new ProductionQueueDto(
            ordered.Select(ToDto).ToList(),
            ordered.Count,
            new QueueFiltersDto(
                ordered.FirstOrDefault()?.JobRef,
                stage, readiness, priority, workstation, search,
                includeCompleted, includeCancelled, take));
        return ProductionResult<ProductionQueueDto>.Ok(dto);
    }

    /// <summary>GET /queue/executable — Ready only, deterministic.</summary>
    public async Task<ProductionResult<ProductionQueueDto>> GetExecutableQueueAsync(
        Guid tenantId, int take = 100, CancellationToken ct = default)
        => await GetQueueAsync(tenantId,
            readiness: nameof(ProductionReadiness.Ready),
            includeCompleted: false, includeCancelled: false,
            take: take, ct: ct);

    /// <summary>GET /queue/blocked — the explainable-blocked list.</summary>
    public async Task<ProductionResult<ProductionQueueDto>> GetBlockedQueueAsync(
        Guid tenantId, int take = 100, CancellationToken ct = default)
        => await GetQueueAsync(tenantId,
            readiness: nameof(ProductionReadiness.Blocked),
            includeCompleted: false, includeCancelled: false,
            take: take, ct: ct);

    // ── dashboard (WPF ProductionDashboardService) ───────────────────

    /// <summary>GET /dashboard — factory KPIs from real production data.
    /// </summary>
    public async Task<ProductionResult<DashboardKpisDto>> GetKpisAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var data = await store.LoadQueueDataAsync(
            tenantId, null, includeCancelled: false, includeCompleted: true, ct);
        var queue = await BuildQueueAsync(
            tenantId, null, includeCompleted: true, includeCancelled: false,
            DashboardTake, data, ct);

        var today = DateTime.UtcNow.Date;

        var kpis = new DashboardKpisDto(
            JobsInProduction: data.Pieces
                .Where(w => !w.IsCancelled).Select(w => w.JobOrderId).Distinct().Count(),
            ReadyToStart: queue.Count(q => q.Readiness == ProductionReadiness.Ready),
            InProgress: queue.Count(q => q.Readiness == ProductionReadiness.InProgress),
            OnHold: queue.Count(q => q.Readiness == ProductionReadiness.OnHold),
            Blocked: queue.Count(q => q.Readiness == ProductionReadiness.Blocked),
            QcPending: queue.Count(q => q.Readiness == ProductionReadiness.QcPending),
            QcFailed: queue.Count(q => q.Readiness == ProductionReadiness.QcFailed),
            Rework: queue.Count(q => q.Readiness == ProductionReadiness.Rework),
            CompletedToday: data.Pieces.Count(w =>
                w.CompletedAt.HasValue && w.CompletedAt.Value.Date == today),
            // The dispatch gateway (WPF GetUnitReadinessAsync) is a later
            // SaaS slice — the WPF's own fallback formula applies.
            ReadyForDelivery: queue.Count(q =>
                q.NextLabel == "Ready" && q.Readiness == ProductionReadiness.Completed),
            OverdueJobs: queue.Where(q => q.DueRisk == DueDateRisk.Overdue)
                .Select(q => q.JobRef).Distinct().Count(),
            DueSoon: queue.Count(q => q.DueRisk == DueDateRisk.DueSoon
                || q.DueRisk == DueDateRisk.AtRisk),
            MaterialShortages: queue.Count(q => q.HasMaterialShortage),
            BottleneckProcess: "",
            BottleneckCount: 0,
            ProcessLoad: queue.GroupBy(q => ProductionStages.Code(q.Stage))
                .ToDictionary(g => g.Key, g => g.Count()));

        // Bottleneck — largest ready queue.
        var bottleneck = queue.Where(q => q.Readiness == ProductionReadiness.Ready)
            .GroupBy(q => q.Stage)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();
        if (bottleneck is not null)
        {
            kpis = kpis with
            {
                BottleneckProcess = ProductionStages.StageLabel(bottleneck.Key),
                BottleneckCount = bottleneck.Count()
            };
        }

        return ProductionResult<DashboardKpisDto>.Ok(kpis);
    }

    /// <summary>GET /dashboard/pressure — per-stage queue pressure (High
    /// &gt; 15 / Medium &gt; 8 / Low &gt; 0 / Idle).</summary>
    public async Task<ProductionResult<IReadOnlyList<QueuePressureDto>>> GetQueuePressureAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var queue = await BuildQueueAsync(
            tenantId, null, includeCompleted: false, includeCancelled: false,
            DashboardTake, ct: ct);

        var pressures = queue.GroupBy(q => q.Stage).Select(g =>
        {
            var ready = g.Count(x => x.Readiness == ProductionReadiness.Ready);
            var blocked = g.Count(x => x.Readiness == ProductionReadiness.Blocked);
            var overdue = g.Count(x => x.DueRisk == DueDateRisk.Overdue);
            var qcBacklog = g.Count(x => x.Readiness == ProductionReadiness.QcPending);
            var reworkBacklog = g.Count(x => x.Readiness == ProductionReadiness.Rework);
            var avgWait = g.Where(x => x.QueueWaitTime.HasValue)
                .Select(x => x.QueueWaitTime!.Value.TotalHours)
                .DefaultIfEmpty(0).Average();
            int total = ready + blocked;
            return new QueuePressureDto(
                ProductionStages.StageLabel(g.Key),
                ProductionStages.Code(g.Key),
                ready, blocked, overdue, qcBacklog, reworkBacklog,
                Math.Round(avgWait, 1),
                total,
                total > 15 ? "High Pressure"
                    : total > 8 ? "Medium Pressure"
                    : total > 0 ? "Low Pressure"
                    : "Idle");
        })
        .OrderByDescending(p => p.Total)
        .ToList();

        return ProductionResult<IReadOnlyList<QueuePressureDto>>.Ok(pressures);
    }

    /// <summary>GET /dashboard/workstation-load — assigned/ready/
    /// in-progress/blocked per active station.</summary>
    public async Task<ProductionResult<IReadOnlyList<WorkstationLoadDto>>> GetWorkstationLoadAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var workstations = await store.ListWorkstationsAsync(
            tenantId, activeOnly: true, ct);
        var assignments = (await store.LoadQueueDataAsync(
                tenantId, null, includeCancelled: false, includeCompleted: false, ct))
            .Assignments.Where(a => a.IsActive).ToList();
        var queue = await BuildQueueAsync(
            tenantId, null, includeCompleted: false, includeCancelled: false,
            DashboardTake, ct: ct);

        var byPiece = queue
            .GroupBy(q => q.WorkItemId)
            .ToDictionary(g => g.Key, g => g.First());

        var loads = workstations.Select(ws =>
        {
            var assigned = assignments
                .Where(a => string.Equals(a.WorkstationId, ws.WorkstationId,
                    StringComparison.Ordinal))
                .ToList();
            var assignedQueue = assigned
                .Where(a => byPiece.TryGetValue(a.ProductionPieceId, out _))
                .Select(a => byPiece[a.ProductionPieceId])
                .ToList();

            return new WorkstationLoadDto(
                ws.WorkstationId, ws.Code, ws.Name, ws.ProcessType,
                ProductionStages.Name(ws.ProcessType),
                assigned.Count,
                assignedQueue.Count(q => q.Readiness == ProductionReadiness.Ready),
                assignedQueue.Count(q => q.Readiness == ProductionReadiness.InProgress),
                assignedQueue.Count(q => q.Readiness == ProductionReadiness.Blocked));
        })
        .OrderBy(l => l.ProcessType).ThenBy(l => l.Code, StringComparer.Ordinal)
        .ToList();

        return ProductionResult<IReadOnlyList<WorkstationLoadDto>>.Ok(loads);
    }

    // ── progress (WPF ProductionProgressService) ─────────────────────

    /// <summary>GET /progress?level=job — physical pieces per job:
    /// distinct Spec|Serial units, the final-stage item dictates the
    /// bucket (Required = each unit's first row Qty; Completed += Qty;
    /// the other buckets += Remaining; cancelled counts units).</summary>
    public async Task<ProductionResult<IReadOnlyList<ProductionProgressDto>>> GetJobProgressAsync(
        Guid tenantId, Guid? jobOrderId, CancellationToken ct = default)
    {
        var queue = await BuildQueueAsync(
            tenantId, jobOrderId, includeCompleted: true, includeCancelled: true,
            DashboardTake, ct: ct);

        var result = new List<ProductionProgressDto>();
        foreach (var g in queue.GroupBy(q => q.JobRef))
        {
            var items = g.ToList();
            var units = items.GroupBy(i => (i.SpecGroupNumber, i.SerialNumber)).ToList();

            int required = units.Sum(u => u.First().Qty);
            int completed = 0, inProgress = 0, blocked = 0, paused = 0,
                rework = 0, qcPending = 0, qcFailed = 0, onHold = 0, cancelled = 0;

            foreach (var unit in units)
            {
                var finalItem = unit.ToList()
                    .OrderByDescending(i => ProductionStages.Code(i.Stage))
                    .FirstOrDefault();
                if (finalItem is null) continue;

                if (finalItem.IsCancelled) cancelled++;
                else if (finalItem.Readiness == ProductionReadiness.Completed) completed += finalItem.Qty;
                else if (finalItem.Readiness == ProductionReadiness.InProgress) inProgress += finalItem.Remaining;
                else if (finalItem.Readiness == ProductionReadiness.Blocked) blocked += finalItem.Remaining;
                else if (finalItem.Readiness == ProductionReadiness.Paused) paused += finalItem.Remaining;
                else if (finalItem.Readiness == ProductionReadiness.Rework) rework += finalItem.Remaining;
                else if (finalItem.Readiness == ProductionReadiness.QcPending) qcPending += finalItem.Remaining;
                else if (finalItem.Readiness == ProductionReadiness.QcFailed) qcFailed += finalItem.Remaining;
                else if (finalItem.Readiness == ProductionReadiness.OnHold) onHold += finalItem.Remaining;
                // Ready counts as remaining — no bucket.
            }

            result.Add(ToProgressDto(g.Key, "", "", 0, 0,
                required, completed, inProgress, blocked, paused, rework,
                qcPending, qcFailed, onHold, cancelled));
        }

        return ProductionResult<IReadOnlyList<ProductionProgressDto>>.Ok(result);
    }

    /// <summary>GET /progress?level=overall — the sum over jobs.</summary>
    public async Task<ProductionResult<ProductionProgressDto>> GetOverallProgressAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var jobs = await GetJobProgressAsync(tenantId, null, ct);
        if (!jobs.Success || jobs.Value is null)
        {
            return ProductionResult<ProductionProgressDto>.Fail(
                jobs.ErrorCode, jobs.Message);
        }

        var all = jobs.Value;
        return ProductionResult<ProductionProgressDto>.Ok(ToProgressDto("OVERALL", "", "", 0, 0,
            all.Sum(j => j.Required), all.Sum(j => j.Completed),
            all.Sum(j => j.InProgress), all.Sum(j => j.Blocked),
            all.Sum(j => j.Paused), all.Sum(j => j.Rework),
            all.Sum(j => j.QcPending), all.Sum(j => j.QcFailed),
            all.Sum(j => j.OnHold), all.Sum(j => j.Cancelled)));
    }

    /// <summary>GET /progress?level=process — workload per stage
    /// (Required = Σ Qty; Completed = Σ Completed Qty).</summary>
    public async Task<ProductionResult<IReadOnlyList<ProductionProgressDto>>> GetProcessProgressAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var queue = await BuildQueueAsync(
            tenantId, null, includeCompleted: true, includeCancelled: false,
            DashboardTake, ct: ct);

        var result = new List<ProductionProgressDto>();
        foreach (var g in queue.GroupBy(q => q.Stage))
        {
            var items = g.ToList();
            result.Add(ToProgressDto(
                ProductionStages.Code(g.Key).ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                ProductionStages.StageLabel(g.Key), "", 0, 0,
                items.Sum(i => i.Qty),
                items.Where(i => i.Readiness == ProductionReadiness.Completed).Sum(i => i.Qty),
                items.Where(i => i.Readiness == ProductionReadiness.InProgress).Sum(i => i.Remaining),
                items.Where(i => i.Readiness == ProductionReadiness.Blocked).Sum(i => i.Remaining),
                items.Where(i => i.Readiness == ProductionReadiness.Paused).Sum(i => i.Remaining),
                items.Where(i => i.Readiness == ProductionReadiness.Rework).Sum(i => i.Remaining),
                items.Where(i => i.Readiness == ProductionReadiness.QcPending).Sum(i => i.Remaining),
                items.Where(i => i.Readiness == ProductionReadiness.QcFailed).Sum(i => i.Remaining),
                items.Where(i => i.Readiness == ProductionReadiness.OnHold).Sum(i => i.Remaining),
                0));
        }

        result = result
            .OrderBy(p => p.JobRef, StringComparer.Ordinal)
            .ToList();
        return ProductionResult<IReadOnlyList<ProductionProgressDto>>.Ok(result);
    }

    // ── the queue build (WPF GetQueueAsync body) ─────────────────────

    /// <summary>Builds the domain queue items with readiness, timing and
    /// lineage — the internal shape the dashboard and progress readers
    /// reuse. When <paramref name="preloaded"/> is supplied the batched
    /// load is skipped (KPIs reuse one load).</summary>
    internal async Task<List<ProductionQueueItem>> BuildQueueAsync(
        Guid tenantId, Guid? jobOrderId, bool includeCompleted,
        bool includeCancelled, int take,
        ProductionQueueData? preloaded = null,
        CancellationToken ct = default)
    {
        var data = preloaded ?? await store.LoadQueueDataAsync(
            tenantId, jobOrderId, includeCancelled, includeCompleted, ct);
        var jobs = data.Jobs.ToDictionary(j => j.Id, j => j);
        var qcDict = BuildQcResults(data.QcEvents);

        var pairingComplete = data.Pieces
            .Where(w => w.Stage == ProductionStages.PairingName && !w.IsCancelled)
            .GroupBy(w => (w.SpecGroupNumber, w.SerialNumber))
            .ToDictionary(g => g.Key, g => g.Any(x => x.QtyDone >= x.Qty));
        var assemblyComplete = data.Pieces
            .Where(w => w.Stage == ProductionStages.DoublingLaminationName && !w.IsCancelled)
            .GroupBy(w => (w.SpecGroupNumber, w.SerialNumber))
            .ToDictionary(g => g.Key, g => g.Any(x => x.QtyDone >= x.Qty));

        var activeReservations = data.Reservations
            .Where(r => ReservationStatuses.IsActiveHold(r.Status))
            .ToList();

        var byJob = data.Pieces.GroupBy(w => w.JobOrderId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ProductionPiece>)g.ToList());
        var byUnit = data.Pieces.GroupBy(w => new { w.JobOrderId, w.SpecGroupNumber, w.SerialNumber })
            .ToDictionary(g => g.Key,
                g => (IReadOnlyList<ProductionPiece>)g.ToList());

        var queue = new List<ProductionQueueItem>();
        foreach (var wi in data.Pieces)
        {
            ct.ThrowIfCancellationRequested();

            jobs.TryGetValue(wi.JobOrderId, out var job);
            string jobRef = job?.JobNo ?? wi.JobOrderId.ToString();
            string sourceProforma = job?.ProformaId.ToString() ?? "";

            var jobGroup = byJob.TryGetValue(wi.JobOrderId, out var jg) ? jg : new[] { wi };
            var unitKey = new { wi.JobOrderId, wi.SpecGroupNumber, wi.SerialNumber };
            var unitGroup = byUnit.TryGetValue(unitKey, out var ug) ? ug : new[] { wi };

            // Active hold — by piece, or by job+spec+serial+stage.
            var hold = data.Holds.FirstOrDefault(h =>
                h.ProductionPieceId == wi.Id
                || (h.JobOrderId == wi.JobOrderId
                    && h.SpecGroupNumber == wi.SpecGroupNumber
                    && h.SerialNumber == wi.SerialNumber
                    && h.Stage == wi.Stage
                    && !h.IsReleased));

            // Cutting reservations are often job-level sheet holds; the
            // other stages match spec+serial.
            bool hasReservation = wi.Stage == ProductionStages.CuttingName
                ? activeReservations.Any(r => r.JobOrderId == wi.JobOrderId)
                : activeReservations.Any(r => r.JobOrderId == wi.JobOrderId
                    && r.SpecGroupNumber == wi.SpecGroupNumber
                    && r.SerialNumber == wi.SerialNumber);

            var (readiness, blockReason) = ProductionReadinessEngine.Calculate(
                ProcessExecutionService.ToRouteItem(wi, jobRef),
                jobGroup.Select(p => ProcessExecutionService.ToRouteItem(p, jobRef)).ToList(),
                unitGroup.Select(p => ProcessExecutionService.ToRouteItem(p, jobRef)).ToList(),
                qcDict, pairingComplete, assemblyComplete,
                hasReservation,
                hasAllocation: false, // the allocation ledger is the 26b slice
                hold is not null,
                hold?.HoldReason ?? "");

            var assignment = data.Assignments.FirstOrDefault(a =>
                a.ProductionPieceId == wi.Id && a.IsActive);
            var over = data.Overrides.FirstOrDefault(o =>
                o.ProductionPieceId == wi.Id
                || (o.JobOrderId == wi.JobOrderId
                    && o.SpecGroupNumber == wi.SpecGroupNumber
                    && o.SerialNumber == wi.SerialNumber
                    && o.Stage == wi.Stage));
            var ws = data.Workstations.FirstOrDefault(w =>
                string.Equals(w.WorkstationId, assignment?.WorkstationId ?? "",
                    StringComparison.Ordinal));

            var priority = over is not null
                ? ProductionReadinessEngine.ParsePriority(over.Priority)
                : ProductionPriority.Normal;

            // Due date — override first, the job order's due date as the
            // fallback (the WPF left null with a TODO; the column exists
            // here).
            DateTime? dueDate = over?.DueDate
                ?? (job?.DueDate is { } d ? d.ToDateTime(TimeOnly.MinValue) : null);
            var dueRisk = ProductionReadinessEngine.CalculateDueRisk(dueDate, DateTime.UtcNow);

            qcDict.TryGetValue((wi.SpecGroupNumber, wi.SerialNumber, wi.Position, wi.Stage),
                out var qcResult);

            // Stage Qty is PCS; reservations are sheets.
            int reserved = activeReservations
                .Where(r => r.JobOrderId == wi.JobOrderId)
                .Sum(r => r.ReservedQty);

            var route = ProcessRouting.BuildRouteForWorkItem(
                ProcessExecutionService.ToRouteItem(wi, jobRef),
                unitGroup.Select(p => ProcessExecutionService.ToRouteItem(p, jobRef)).ToList());

            var item = new ProductionQueueItem
            {
                WorkItemId = wi.Id,
                UniqueKey = wi.UniqueKey,
                JobRef = jobRef,
                JobOrderId = wi.JobOrderId,
                SourceProformaId = sourceProforma,
                CustomerName = job?.CustomerName ?? "",
                ProjectName = job?.ProjectName ?? "",
                Salesman = job?.Salesman?.Name ?? "",
                SpecGroupNumber = wi.SpecGroupNumber,
                SpecName = wi.SpecName,
                BuildType = wi.BuildType,
                SerialNumber = wi.SerialNumber,
                GlassRef = wi.GlassRef,
                Position = wi.Position,
                WidthMm = wi.WidthMm,
                HeightMm = wi.HeightMm,
                Qty = wi.Qty,
                QtyDone = wi.QtyDone,
                Stage = wi.Stage,
                Status = wi.Status,
                Readiness = readiness,
                BlockReason = blockReason,
                Priority = priority,
                DueDate = dueDate,
                DueRisk = dueRisk,
                QcResult = qcResult ?? "",
                IsRework = wi.ReplacementSequence > 0,
                IsFoc = wi.IsFoc,
                IsCancelled = wi.IsCancelled,
                ReplacementSequence = wi.ReplacementSequence,
                Operator = assignment?.OperatorName ?? "",
                WorkstationId = assignment?.WorkstationId ?? "",
                WorkstationName = ws?.Name ?? "",
                AssignedBy = assignment?.AssignedBy?.ToString() ?? "",
                AssignedAt = assignment?.AssignedAt,
                IsOnHold = hold is not null,
                HoldReason = hold?.HoldReason ?? "",
                HeldAt = hold?.HeldAt,
                MaterialRequired = wi.Qty,
                MaterialReserved = reserved,
                MaterialAllocated = 0, // 26b (allocation ledger)
                MaterialConsumed = 0,  // 26b
                MaterialAvailable = reserved, // WPF verbatim (same sum)
                MaterialShortage = hasReservation || wi.Stage != ProductionStages.CuttingName
                    ? 0 : 1,
                CreatedAt = wi.CreatedAt,
                UpdatedAt = wi.UpdatedAt,
                CompletedAt = wi.CompletedAt,
                CompletedBy = wi.CompletedBy?.ToString() ?? "",
                Notes = wi.Notes,
                RequiredStages = ProcessRouting.BuildFullJobRoute(
                    jobGroup.Select(p => ProcessExecutionService.ToRouteItem(p, jobRef))
                        .ToList())
            };

            // Previous/Next/Traceability.
            item.PreviousStage = route.PreviousRequiredBefore(wi.Stage);
            item.NextStage = route.NextRequiredAfter(wi.Stage);
            item.PreviousLabel = item.PreviousStage is null ? ""
                : ProductionStages.StageLabel(item.PreviousStage);
            item.CurrentLabel = item.StageLabel;
            item.NextLabel = item.NextStage is null ? "Ready"
                : ProductionStages.StageLabel(item.NextStage);
            item.TraceabilityLabel = $"{item.PreviousLabel} → {item.CurrentLabel} → {item.NextLabel}";

            // DGU specifics — deterministic pick (lowest PairId/label).
            var pair = data.Pairings
                .Where(p => p.JobOrderId == wi.JobOrderId
                    && p.SpecGroupNumber == wi.SpecGroupNumber
                    && p.SerialNumber == wi.SerialNumber)
                .OrderBy(p => p.PairId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (pair is not null)
            {
                item.OuterPieceId = pair.OuterPieceId;
                item.InnerPieceId = pair.InnerPieceId;
                item.PairId = pair.PairId;
            }
            var asm = data.Assemblies
                .Where(a => a.JobOrderId == wi.JobOrderId
                    && a.SpecGroupNumber == wi.SpecGroupNumber
                    && a.SerialNumber == wi.SerialNumber)
                .OrderBy(a => a.AssemblyLabel, StringComparer.Ordinal)
                .FirstOrDefault();
            if (asm is not null)
            {
                item.AssemblyId = asm.AssemblyLabel; // WPF surfaces the label
            }

            queue.Add(item);
        }

        // Deterministic ordering + timing enrichment (the timing service
        // exists for exactly this; the WPF dashboard forgot to call it, so
        // its AvgWait read 0 — fixed here, documented). Unlike the WPF's
        // pre-readiness fetch cap (which can starve predecessor context),
        // the SaaS loads the full filtered set and caps after ordering.
        var ordered = queue.Order().Take(Math.Max(take, 1)).ToList();
        ProductionTiming.EnrichTiming(ordered, DateTime.UtcNow);
        return ordered;
    }

    /// <summary>Latest QC outcome per (spec, serial, position, stage) from
    /// the typed QC events (payload sequence decides the latest).</summary>
    internal static IReadOnlyDictionary<
            (int Spec, int Serial, string Position, string Stage), string>
        BuildQcResults(IReadOnlyList<ProductionEvent> qcEvents)
    {
        var latest = new Dictionary<
            (int Spec, int Serial, string Position, string Stage), (int Seq, DateTime At, string Result)>();
        foreach (var e in qcEvents)
        {
            var key = (e.SpecGroupNumber, e.SerialNumber, e.Position, e.Stage);
            int seq = ParseQcSequence(e);
            if (!latest.TryGetValue(key, out var cur) || seq >= cur.Seq)
            {
                latest[key] = (seq, e.CreatedAt, e.EventType switch
                {
                    var t when t == ProductionEntryTypes.QcPassed => QcResults.Passed,
                    var t when t == ProductionEntryTypes.QcFailed => QcResults.Failed,
                    var t when t == ProductionEntryTypes.Rework => QcResults.Rework,
                    _ => QcResults.Pending
                });
            }
        }
        return latest.ToDictionary(kv => kv.Key, kv => kv.Value.Result);
    }

    private static int ParseQcSequence(ProductionEvent e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.Payload ?? "{}");
            return doc.RootElement.TryGetProperty("qcSequence", out var v)
                && v.TryGetInt32(out var seq) ? seq : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static ProductionProgressDto ToProgressDto(
        string jobRef, string specName, string buildType, int spec, int serial,
        int required, int completed, int inProgress, int blocked, int paused,
        int rework, int qcPending, int qcFailed, int onHold, int cancelled)
    {
        int remaining = Math.Max(0, required - completed);
        double pct = required == 0 ? 0
            : Math.Round((double)completed / required * 100, 1);
        return new ProductionProgressDto(
            jobRef, specName, buildType, spec, serial,
            required, completed, inProgress, blocked, paused, rework,
            qcPending, qcFailed, onHold, cancelled,
            remaining, pct, $"{completed}/{required} ({pct}%)");
    }

    internal static QueueItemDto ToDto(ProductionQueueItem q) => new(
        q.WorkItemId,
        q.UniqueKey,
        q.JobRef,
        q.JobOrderId,
        q.SourceProformaId,
        q.CustomerName,
        q.ProjectName,
        q.Salesman,
        q.SpecGroupNumber,
        q.SpecName,
        q.BuildType,
        q.SerialNumber,
        q.GlassRef,
        q.Position,
        q.WidthMm,
        q.HeightMm,
        q.Qty,
        q.QtyDone,
        q.Remaining,
        q.Stage,
        q.StageLabel,
        q.PreviousStage,
        q.NextStage,
        q.RequiredStages,
        q.Status,
        q.Readiness.ToString(),
        q.BlockReason,
        q.Priority.ToString(),
        q.DueDate,
        q.DueRisk.ToString(),
        q.QcResult,
        q.QcId,
        q.IsRework,
        q.IsFoc,
        q.IsCancelled,
        q.ReplacementSequence,
        q.Operator,
        q.WorkstationId,
        q.WorkstationName,
        q.AssignedBy,
        q.AssignedAt,
        q.IsOnHold,
        q.HoldReason,
        q.HeldBy,
        q.HeldAt,
        q.MaterialRequired,
        q.MaterialReserved,
        q.MaterialAllocated,
        q.MaterialConsumed,
        q.MaterialAvailable,
        q.MaterialShortage,
        q.HasMaterialShortage,
        q.CreatedAt,
        q.UpdatedAt,
        q.CompletedAt,
        q.CompletedBy,
        q.QueueWaitTime.HasValue
            ? Math.Round(q.QueueWaitTime.Value.TotalHours, 2) : null,
        q.ProcessingDuration.HasValue
            ? Math.Round(q.ProcessingDuration.Value.TotalHours, 2) : null,
        q.TraceabilityLabel,
        q.OuterPieceId,
        q.InnerPieceId,
        q.PairId,
        q.AssemblyId,
        q.Notes);
}
