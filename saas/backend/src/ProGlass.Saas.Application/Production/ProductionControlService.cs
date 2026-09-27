using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Production;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 26a service — the WPF production control core:
//    • ProductionControlService.StartAsync (controlled transition:
//      status guards + active-hold check + the readiness engine must say
//      READY, then a serializable transaction with a re-fetch)
//    • PauseAsync / ResumeAsync (only InProgress ↔ Paused)
//    • HoldAsync / ReleaseHoldAsync (hold history preserved — released
//      rows are flagged, never deleted; Release appends " | Release: …")
//    • AssignAsync / UnassignAsync (previous actives deactivated, never
//      deleted; idempotent on workstation+operator)
//    • SetPriorityAsync / SetDueDateAsync (the override upsert; priority
//      influences queue ordering only, never bypasses dependencies)
//    • CompleteAsync (delegates to the stage flow — the WPF reuses
//      SetDoneQtyAsync)
//  The readiness engine + id builders live in
//  Domain/Proforma/ProductionProcess.cs. SaaS difference (documented):
//  every transition appends a typed ProductionEvent (the WPF writes
//  SavedProductionEntry rows — same shape, same reason strings); no
//  NotifyFloor live-refresh (clients re-poll).
// ═══════════════════════════════════════════════════════════════════════

public sealed class ProductionControlService(
    IProcessControlStore store,
    IProductionStore production,
    IReservationStore reservations,
    ProductionService stageFlow,
    IAuditWriter audit)
{
    // ── Start (WPF StartAsync) ───────────────────────────────────────

    /// <summary>
    /// POST /control/{id}/start — controlled transition; must be READY
    /// (predecessors, QC, material, holds all clear).
    /// </summary>
    public async Task<ProductionResult<ProductionWorkItemDto>> StartAsync(
        Guid tenantId, Guid actorId, Guid pieceId,
        ControlStartRequest request, string? ip, CancellationToken ct)
    {
        var (piece, job, allForJob) = await LoadContextAsync(tenantId, pieceId, ct);
        if (piece is null)
        {
            return Fail(ProductionErrorCode.NotFound, $"Work item {pieceId} was not found.");
        }

        if (piece.IsCancelled)
        {
            return Fail(ProductionErrorCode.Conflict,
                $"WorkItem cancelled: {piece.CancelReason}");
        }
        if (piece.Status == WorkItemStatuses.InProgress)
        {
            return ProductionResult<ProductionWorkItemDto>.Ok(
                ProductionService.ToDto(piece), "Already in progress — idempotent no-op.");
        }
        if (piece.Status == WorkItemStatuses.Completed)
        {
            return Fail(ProductionErrorCode.Conflict, "WorkItem already completed");
        }
        if (piece.Status == WorkItemStatuses.Paused)
        {
            return Fail(ProductionErrorCode.Conflict, "WorkItem is paused — use Resume");
        }
        if (piece.Status == WorkItemStatuses.Rejected)
        {
            return Fail(ProductionErrorCode.Conflict, "Rejected work cannot start");
        }
        if (piece.Status == WorkItemStatuses.QcFailed)
        {
            return Fail(ProductionErrorCode.Conflict, "QC failed — cannot start");
        }

        var activeHold = await store.FindActiveHoldByPieceAsync(tenantId, pieceId, ct);
        if (activeHold is not null)
        {
            return Fail(ProductionErrorCode.Conflict,
                $"On Hold: {activeHold.HoldReason}");
        }

        // Readiness check — must be READY.
        var readiness = await CalculateReadinessAsync(
            tenantId, job!.JobNo, piece, allForJob!, activeHold, ct);
        if (readiness.Readiness != ProductionReadiness.Ready)
        {
            return Fail(ProductionErrorCode.Conflict,
                $"Not ready: {readiness.BlockReason} — {readiness.Readiness}");
        }

        // Concurrency: serializable transaction with a re-fetch (the store
        // owns it — the WPF re-fetches inside BeginTransaction).
        var (ok, message) = await store.TransitionPieceAsync(
            tenantId, pieceId, actorId,
            toStatus: WorkItemStatuses.InProgress,
            entryReason: $"Start by {request.OperatorName ?? ""}",
            entryNotes: $"Production Control Start — Operator {request.OperatorName ?? ""}",
            syncExecutions: true, ct);
        if (!ok)
        {
            return Fail(ProductionErrorCode.ConcurrencyConflict, message);
        }

        await AuditAsync(tenantId, actorId, "production.control_started",
            "production_pieces", pieceId, null, new
            {
                uniqueKey = piece.UniqueKey,
                operatorName = request.OperatorName ?? ""
            }, ip, ct);

        var fresh = await production.FindAsync(tenantId, pieceId, ct);
        return ProductionResult<ProductionWorkItemDto>.Ok(
            ProductionService.ToDto(fresh!), "Started");
    }

    /// <summary>POST /control/{id}/pause — only InProgress can pause.</summary>
    public async Task<ProductionResult<ProductionWorkItemDto>> PauseAsync(
        Guid tenantId, Guid actorId, Guid pieceId,
        ControlPauseRequest request, string? ip, CancellationToken ct)
    {
        var (piece, job, _) = await LoadContextAsync(tenantId, pieceId, ct);
        if (piece is null)
        {
            return Fail(ProductionErrorCode.NotFound, $"Work item {pieceId} was not found.");
        }
        if (piece.Status == WorkItemStatuses.Paused)
        {
            return ProductionResult<ProductionWorkItemDto>.Ok(
                ProductionService.ToDto(piece), "Already paused — idempotent");
        }
        if (piece.Status != WorkItemStatuses.InProgress)
        {
            return Fail(ProductionErrorCode.Conflict, "Only InProgress can be paused");
        }

        var (ok, message) = await store.TransitionPieceAsync(
            tenantId, pieceId, actorId,
            toStatus: WorkItemStatuses.Paused,
            entryReason: request.Reason ?? "Pause",
            entryNotes: $"Production Control Pause — {request.Reason ?? ""}",
            syncExecutions: true, ct);
        if (!ok)
        {
            return Fail(ProductionErrorCode.ConcurrencyConflict, message);
        }

        await AuditAsync(tenantId, actorId, "production.control_paused",
            "production_pieces", pieceId, null, new
            {
                uniqueKey = piece.UniqueKey,
                reason = request.Reason ?? ""
            }, ip, ct);

        var fresh = await production.FindAsync(tenantId, pieceId, ct);
        return ProductionResult<ProductionWorkItemDto>.Ok(
            ProductionService.ToDto(fresh!), "Paused");
    }

    /// <summary>POST /control/{id}/resume — only Paused can resume.</summary>
    public async Task<ProductionResult<ProductionWorkItemDto>> ResumeAsync(
        Guid tenantId, Guid actorId, Guid pieceId, string? ip, CancellationToken ct)
    {
        var (piece, job, _) = await LoadContextAsync(tenantId, pieceId, ct);
        if (piece is null)
        {
            return Fail(ProductionErrorCode.NotFound, $"Work item {pieceId} was not found.");
        }
        if (piece.Status == WorkItemStatuses.InProgress)
        {
            return ProductionResult<ProductionWorkItemDto>.Ok(
                ProductionService.ToDto(piece), "Already in progress — idempotent");
        }
        if (piece.Status != WorkItemStatuses.Paused)
        {
            return Fail(ProductionErrorCode.Conflict, "Only Paused can be resumed");
        }

        var (ok, message) = await store.TransitionPieceAsync(
            tenantId, pieceId, actorId,
            toStatus: WorkItemStatuses.InProgress,
            entryReason: "Resume",
            entryNotes: "Production Control Resume",
            syncExecutions: true, ct);
        if (!ok)
        {
            return Fail(ProductionErrorCode.ConcurrencyConflict, message);
        }

        await AuditAsync(tenantId, actorId, "production.control_resumed",
            "production_pieces", pieceId, null,
            new { uniqueKey = piece.UniqueKey }, ip, ct);

        var fresh = await production.FindAsync(tenantId, pieceId, ct);
        return ProductionResult<ProductionWorkItemDto>.Ok(
            ProductionService.ToDto(fresh!), "Resumed");
    }

    // ── Hold / Release (history preserved) ───────────────────────────

    /// <summary>POST /control/{id}/hold — idempotent while a hold is
    /// active; the HoldId sequence is the piece's prior hold count.</summary>
    public async Task<ProductionResult<ProductionHoldDto>> HoldAsync(
        Guid tenantId, Guid actorId, Guid pieceId,
        ControlHoldRequest request, string? ip, CancellationToken ct)
    {
        var (piece, job, _) = await LoadContextAsync(tenantId, pieceId, ct);
        if (piece is null)
        {
            return ProductionResult<ProductionHoldDto>.Fail(
                ProductionErrorCode.NotFound, $"Work item {pieceId} was not found.");
        }
        if (piece.IsCancelled)
        {
            return ProductionResult<ProductionHoldDto>.Fail(
                ProductionErrorCode.Conflict, "Cancelled work cannot be held");
        }

        var existing = await store.FindActiveHoldByPieceAsync(tenantId, pieceId, ct);
        if (existing is not null)
        {
            return ProductionResult<ProductionHoldDto>.Ok(
                ToDto(existing), $"Already on hold: {existing.HoldReason} — idempotent");
        }

        if (string.IsNullOrWhiteSpace(request.HoldReason))
        {
            return ProductionResult<ProductionHoldDto>.Fail(
                ProductionErrorCode.ValidationFailed, "A hold reason is required.");
        }

        int seq = await store.CountHoldsByPieceAsync(tenantId, pieceId, ct);
        string holdId = ProcessRouting.BuildHoldId(
            job!.JobNo, piece.SpecGroupNumber, piece.SerialNumber,
            ProductionStages.Code(piece.Stage), seq);

        var hold = new ProductionControlHold
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            HoldId = holdId,
            ProductionPieceId = piece.Id,
            JobOrderId = piece.JobOrderId,
            JobRef = job.JobNo,
            SpecGroupNumber = piece.SpecGroupNumber,
            SerialNumber = piece.SerialNumber,
            Position = piece.Position,
            Stage = piece.Stage,
            HoldReason = request.HoldReason.Trim(),
            HeldBy = actorId,
            HeldAt = DateTime.UtcNow,
            IsReleased = false,
            Notes = request.Notes ?? "",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await store.InsertHoldAsync(tenantId, hold, ct);

        // The hold event (WPF SavedProductionEntry Reason "Hold: …").
        await production.AppendEventAsync(tenantId, new ProductionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductionPieceId = piece.Id,
            EventType = ProductionEntryTypes.Stage,
            JobOrderId = piece.JobOrderId,
            Stage = piece.Stage,
            SpecGroupNumber = piece.SpecGroupNumber,
            SerialNumber = piece.SerialNumber,
            Position = piece.Position,
            SpecName = piece.SpecName,
            GlassRef = piece.GlassRef,
            Qty = piece.QtyDone,
            DoneBy = actorId,
            Reason = $"Hold: {request.HoldReason.Trim()}",
            Notes = request.Notes ?? "",
            CreatedAt = DateTime.UtcNow
        }, ct);

        await AuditAsync(tenantId, actorId, "production.control_hold",
            "production_pieces", pieceId, null, new
            {
                holdId,
                reason = request.HoldReason.Trim()
            }, ip, ct);

        return ProductionResult<ProductionHoldDto>.Ok(ToDto(hold), "On Hold");
    }

    /// <summary>POST /control/{id}/release-hold — idempotent; the notes
    /// append " | Release: …" and the history row is kept.</summary>
    public async Task<ProductionResult<ProductionHoldDto>> ReleaseHoldAsync(
        Guid tenantId, Guid actorId, Guid pieceId,
        ReleaseHoldRequest request, string? ip, CancellationToken ct)
    {
        var hold = await store.FindHoldAsync(tenantId, request.HoldId, ct);
        if (hold is null)
        {
            return ProductionResult<ProductionHoldDto>.Fail(
                ProductionErrorCode.NotFound, "Hold not found");
        }
        if (hold.ProductionPieceId != pieceId)
        {
            return ProductionResult<ProductionHoldDto>.Fail(
                ProductionErrorCode.ValidationFailed,
                "The hold belongs to a different work item.");
        }
        if (hold.IsReleased)
        {
            return ProductionResult<ProductionHoldDto>.Ok(
                ToDto(hold), "Already released — idempotent");
        }

        hold.IsReleased = true;
        hold.ReleasedBy = actorId;
        hold.ReleasedAt = DateTime.UtcNow;
        hold.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Notes))
            hold.Notes += $" | Release: {request.Notes}";
        if (!await store.TryUpdateHoldAsync(hold, ct))
        {
            return ProductionResult<ProductionHoldDto>.Fail(
                ProductionErrorCode.ConcurrencyConflict,
                "The hold was modified by someone else — reload and retry.");
        }

        var piece = await production.FindAsync(tenantId, pieceId, ct);
        if (piece is not null)
        {
            await production.AppendEventAsync(tenantId, new ProductionEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductionPieceId = piece.Id,
                EventType = ProductionEntryTypes.Stage,
                JobOrderId = piece.JobOrderId,
                Stage = piece.Stage,
                SpecGroupNumber = piece.SpecGroupNumber,
                SerialNumber = piece.SerialNumber,
                Position = piece.Position,
                SpecName = piece.SpecName,
                GlassRef = piece.GlassRef,
                Qty = piece.QtyDone,
                DoneBy = actorId,
                Reason = $"Release Hold: {hold.HoldReason}",
                Notes = request.Notes ?? "",
                CreatedAt = DateTime.UtcNow
            }, ct);
        }

        await AuditAsync(tenantId, actorId, "production.control_hold_released",
            "production_pieces", pieceId, null, new
            {
                holdId = hold.HoldId,
                notes = request.Notes ?? ""
            }, ip, ct);

        return ProductionResult<ProductionHoldDto>.Ok(ToDto(hold), "Hold Released");
    }

    // ── Assignment ───────────────────────────────────────────────────

    /// <summary>POST /control/{id}/assign — idempotent on the same
    /// workstation+operator; previous actives are deactivated (history
    /// preserved, never deleted).</summary>
    public async Task<ProductionResult<ProductionAssignmentDto>> AssignAsync(
        Guid tenantId, Guid actorId, Guid pieceId,
        AssignRequest request, string? ip, CancellationToken ct)
    {
        var (piece, job, _) = await LoadContextAsync(tenantId, pieceId, ct);
        if (piece is null)
        {
            return ProductionResult<ProductionAssignmentDto>.Fail(
                ProductionErrorCode.NotFound, $"Work item {pieceId} was not found.");
        }

        var (ok, message, assignmentId) = await store.AssignAsync(
            tenantId, piece,
            (request.WorkstationId ?? "").Trim(),
            (request.OperatorName ?? "").Trim(),
            request.Notes ?? "", actorId, ct);
        if (!ok)
        {
            return ProductionResult<ProductionAssignmentDto>.Fail(
                ProductionErrorCode.Conflict, message);
        }

        await AuditAsync(tenantId, actorId, "production.control_assigned",
            "production_pieces", pieceId, null, new
            {
                assignmentId,
                workstation = request.WorkstationId ?? "",
                operatorName = request.OperatorName ?? ""
            }, ip, ct);

        var rows = await store.ListAssignmentsByPieceAsync(tenantId, pieceId, true, ct);
        var current = rows.FirstOrDefault(a => a.AssignmentId == assignmentId);
        return ProductionResult<ProductionAssignmentDto>.Ok(
            current is null ? new ProductionAssignmentDto(
                Guid.Empty, assignmentId, pieceId, job?.JobNo ?? "",
                piece.SpecGroupNumber, piece.SerialNumber, piece.Position,
                piece.Stage, request.WorkstationId ?? "",
                request.OperatorName ?? "", DateTime.UtcNow, null, true,
                request.Notes ?? "")
            : ToDto(current),
            "Assigned");
    }

    /// <summary>POST /control/{id}/unassign — deactivate every active
    /// assignment (idempotent when none).</summary>
    public async Task<ProductionResult<ProductionAssignmentDto>> UnassignAsync(
        Guid tenantId, Guid actorId, Guid pieceId, string? ip, CancellationToken ct)
    {
        var (ok, message) = await store.UnassignAsync(tenantId, pieceId, actorId, ct);
        if (!ok)
        {
            return ProductionResult<ProductionAssignmentDto>.Fail(
                ProductionErrorCode.Conflict, message);
        }

        await AuditAsync(tenantId, actorId, "production.control_unassigned",
            "production_pieces", pieceId, null,
            new { pieceId }, ip, ct);
        return ProductionResult<ProductionAssignmentDto>.Ok(
            null, "Unassigned");
    }

    // ── Priority / Due date ──────────────────────────────────────────

    /// <summary>POST /control/{id}/priority — 0 Normal / 1 High /
    /// 2 Urgent; influences queue ordering only (never bypasses safety
    /// dependencies); idempotent when unchanged.</summary>
    public async Task<ProductionResult<ProductionOverrideDto>> SetPriorityAsync(
        Guid tenantId, Guid actorId, Guid pieceId,
        SetPriorityRequest request, string? ip, CancellationToken ct)
    {
        if (request.Priority is < 0 or > 2)
        {
            return ProductionResult<ProductionOverrideDto>.Fail(
                ProductionErrorCode.ValidationFailed,
                "Priority must be 0 (Normal), 1 (High) or 2 (Urgent).");
        }

        var (piece, job, controlId) = await LoadOverrideContextAsync(tenantId, pieceId, ct);
        if (piece is null)
        {
            return ProductionResult<ProductionOverrideDto>.Fail(
                ProductionErrorCode.NotFound, $"Work item {pieceId} was not found.");
        }

        var existing = await store.FindOverrideAsync(tenantId, controlId, ct);
        if (existing is not null && existing.Priority == request.Priority)
        {
            return ProductionResult<ProductionOverrideDto>.Ok(
                ToDto(existing), "Priority already set — idempotent");
        }

        ProductionControlOverride row = existing ?? NewOverride(
            tenantId, piece, job!, controlId);
        row.Priority = request.Priority;
        row.PrioritySetBy = actorId;
        row.PrioritySetAt = DateTime.UtcNow;
        row.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Notes)) row.Notes = request.Notes;

        var stored = await store.UpsertOverrideAsync(tenantId, row, ct);
        await AuditAsync(tenantId, actorId, "production.control_priority",
            "production_pieces", pieceId, null, new
            {
                controlId,
                priority = request.Priority
            }, ip, ct);

        return ProductionResult<ProductionOverrideDto>.Ok(
            ToDto(stored), $"Priority set to {ProductionReadinessEngine.ParsePriority(request.Priority)}");
    }

    /// <summary>POST /control/{id}/due-date — set or clear (null).</summary>
    public async Task<ProductionResult<ProductionOverrideDto>> SetDueDateAsync(
        Guid tenantId, Guid actorId, Guid pieceId,
        SetDueDateRequest request, string? ip, CancellationToken ct)
    {
        var (piece, job, controlId) = await LoadOverrideContextAsync(tenantId, pieceId, ct);
        if (piece is null)
        {
            return ProductionResult<ProductionOverrideDto>.Fail(
                ProductionErrorCode.NotFound, $"Work item {pieceId} was not found.");
        }

        var existing = await store.FindOverrideAsync(tenantId, controlId, ct);
        ProductionControlOverride row = existing ?? NewOverride(
            tenantId, piece, job!, controlId);
        row.DueDate = request.DueDate;
        row.PrioritySetBy = actorId;
        row.PrioritySetAt = DateTime.UtcNow;
        row.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Notes)) row.Notes = request.Notes;

        var stored = await store.UpsertOverrideAsync(tenantId, row, ct);
        await AuditAsync(tenantId, actorId, "production.control_due_date",
            "production_pieces", pieceId, null, new
            {
                controlId,
                dueDate = request.DueDate?.ToString("yyyy-MM-dd")
            }, ip, ct);

        return ProductionResult<ProductionOverrideDto>.Ok(ToDto(stored),
            request.DueDate.HasValue
                ? $"Due date set to {request.DueDate:yyyy-MM-dd}"
                : "Due date cleared");
    }

    // ── Complete (reuses the piece flow) ─────────────────────────────

    /// <summary>POST /control/{id}/complete — WPF delegates to
    /// SetDoneQtyAsync; the SaaS delegates to the stage flow
    /// (RecordStage), which clamps and auto-derives the status.</summary>
    public async Task<ProductionResult<ProductionWorkItemDto>> CompleteAsync(
        Guid tenantId, Guid actorId, Guid pieceId,
        ControlCompleteRequest request, string? ip, CancellationToken ct)
    {
        if (request.Qty < 0)
        {
            return Fail(ProductionErrorCode.ValidationFailed,
                "Quantity cannot be negative.");
        }

        return await stageFlow.RecordStageAsync(tenantId, pieceId, actorId,
            new StageProgressRequest(request.Qty), ip, ct);
    }

    // ── reads ────────────────────────────────────────────────────────

    public async Task<ProductionResult<IReadOnlyList<ProductionHoldDto>>> ListHoldsAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct)
    {
        var piece = await production.FindAsync(tenantId, pieceId, ct);
        if (piece is null)
        {
            return ProductionResult<IReadOnlyList<ProductionHoldDto>>.Fail(
                ProductionErrorCode.NotFound, $"Work item {pieceId} was not found.");
        }
        // The store lists by piece; the caller sees released history too.
        var rows = await store.ListHoldsByPieceAsync(tenantId, pieceId, ct);
        return ProductionResult<IReadOnlyList<ProductionHoldDto>>.Ok(
            rows.Select(ToDto).ToList());
    }

    public async Task<ProductionResult<IReadOnlyList<ProductionAssignmentDto>>> ListAssignmentsAsync(
        Guid tenantId, Guid pieceId, bool activeOnly, CancellationToken ct)
    {
        var piece = await production.FindAsync(tenantId, pieceId, ct);
        if (piece is null)
        {
            return ProductionResult<IReadOnlyList<ProductionAssignmentDto>>.Fail(
                ProductionErrorCode.NotFound, $"Work item {pieceId} was not found.");
        }
        var rows = await store.ListAssignmentsByPieceAsync(tenantId, pieceId, activeOnly, ct);
        return ProductionResult<IReadOnlyList<ProductionAssignmentDto>>.Ok(
            rows.Select(ToDto).ToList());
    }

    // ── readiness context (WPF StartAsync's full-context load) ───────

    internal async Task<(ProductionReadiness Readiness, string BlockReason)>
        CalculateReadinessAsync(
            Guid tenantId, string jobRef, ProductionPiece piece,
            IReadOnlyList<ProductionPiece> allForJob,
            ProductionControlHold? activeHold, CancellationToken ct)
    {
        var jobPieces = allForJob;
        var allForUnit = jobPieces
            .Where(w => w.SpecGroupNumber == piece.SpecGroupNumber
                && w.SerialNumber == piece.SerialNumber)
            .ToList();

        // Latest QC per (spec, serial, position, stage) from the typed
        // events (the WPF reads its QcEntries table).
        var qcDict = await BuildQcResultsAsync(tenantId, jobPieces, ct);

        var pairingComplete = jobPieces
            .Where(w => w.Stage == ProductionStages.PairingName && !w.IsCancelled)
            .GroupBy(w => (w.SpecGroupNumber, w.SerialNumber))
            .ToDictionary(g => g.Key, g => g.Any(x => x.QtyDone >= x.Qty));
        var assemblyComplete = jobPieces
            .Where(w => w.Stage == ProductionStages.DoublingLaminationName && !w.IsCancelled)
            .GroupBy(w => (w.SpecGroupNumber, w.SerialNumber))
            .ToDictionary(g => g.Key, g => g.Any(x => x.QtyDone >= x.Qty));

        var reservationsForJob = await reservations.ListByJobAsync(
            tenantId, piece.JobOrderId, ct);
        bool hasRes = piece.Stage == ProductionStages.CuttingName
            ? reservationsForJob.Any(r => ReservationStatuses.IsActiveHold(r.Status))
            : reservationsForJob.Any(r => ReservationStatuses.IsActiveHold(r.Status)
                && r.SpecGroupNumber == piece.SpecGroupNumber
                && r.SerialNumber == piece.SerialNumber);
        // The allocation ledger is the 26b slice — the WPF engine accepts
        // the flag but never blocks on it (same here).
        bool hasAlloc = false;

        return ProductionReadinessEngine.Calculate(
            ProcessExecutionService.ToRouteItem(piece, jobRef),
            jobPieces.Select(p => ProcessExecutionService.ToRouteItem(p, jobRef)).ToList(),
            allForUnit.Select(p => ProcessExecutionService.ToRouteItem(p, jobRef)).ToList(),
            qcDict, pairingComplete, assemblyComplete,
            hasRes, hasAlloc,
            activeHold is not null, activeHold?.HoldReason ?? "");
    }

    /// <summary>Latest QC outcome per (spec, serial, position, stage):
    /// Passed / Failed / Rework — derived from the event log's payload
    /// sequence (the SaaS stores QC rows as typed events).</summary>
    internal static async Task<
            IReadOnlyDictionary<(int Spec, int Serial, string Position, string Stage), string>>
        BuildQcResultsAsync(
            Guid tenantId, IReadOnlyList<ProductionPiece> jobPieces,
            IProductionStore production, CancellationToken ct)
    {
        var dict = new Dictionary<
            (int Spec, int Serial, string Position, string Stage), (int Seq, string Result)>();
        foreach (var group in jobPieces.GroupBy(p => p.JobOrderId))
        {
            foreach (var piece in group)
            {
                var qcEvents = await production.ListQcEventsAsync(
                    tenantId, piece.Id, ct);
                foreach (var e in qcEvents)
                {
                    var key = (piece.SpecGroupNumber, piece.SerialNumber,
                        piece.Position, piece.Stage);
                    int seq = ParseQcSequence(e);
                    if (!dict.TryGetValue(key, out var current) || seq >= current.Seq)
                    {
                        dict[key] = (seq, e.EventType switch
                        {
                            var t when t == ProductionEntryTypes.QcPassed => QcResults.Passed,
                            var t when t == ProductionEntryTypes.QcFailed => QcResults.Failed,
                            var t when t == ProductionEntryTypes.Rework => QcResults.Rework,
                            _ => QcResults.Pending
                        });
                    }
                }
            }
        }
        return dict.ToDictionary(
            kv => kv.Key, kv => kv.Value.Result);
    }

    private async Task<
            IReadOnlyDictionary<(int Spec, int Serial, string Position, string Stage), string>>
        BuildQcResultsAsync(
            Guid tenantId, IReadOnlyList<ProductionPiece> jobPieces, CancellationToken ct)
        => await BuildQcResultsAsync(tenantId, jobPieces, production, ct);

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

    private async Task<(ProductionPiece? Piece, JobOrder? Job,
        IReadOnlyList<ProductionPiece>? AllForJob)> LoadContextAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct)
    {
        var piece = await production.FindAsync(tenantId, pieceId, ct);
        if (piece is null) return (null, null, null);
        var job = await store.FindJobAsync(tenantId, piece.JobOrderId, ct);
        var allForJob = await production.ListByJobAsync(tenantId, piece.JobOrderId, ct);
        return (piece, job, allForJob);
    }

    private async Task<(ProductionPiece? Piece, JobOrder? Job, string ControlId)>
        LoadOverrideContextAsync(Guid tenantId, Guid pieceId, CancellationToken ct)
    {
        var (piece, job, _) = await LoadContextAsync(tenantId, pieceId, ct);
        if (piece is null || job is null) return (null, null, "");
        string controlId = ProcessRouting.BuildControlId(
            job.JobNo, piece.SpecGroupNumber, piece.SerialNumber,
            ProductionStages.Code(piece.Stage));
        return (piece, job, controlId);
    }

    private static ProductionControlOverride NewOverride(
        Guid tenantId, ProductionPiece piece, JobOrder job, string controlId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        ControlId = controlId,
        ProductionPieceId = piece.Id,
        JobOrderId = piece.JobOrderId,
        JobRef = job.JobNo,
        SpecGroupNumber = piece.SpecGroupNumber,
        SerialNumber = piece.SerialNumber,
        Position = piece.Position,
        Stage = piece.Stage,
        Priority = 0,
        PrioritySetAt = DateTime.UtcNow,
        Notes = "",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static ProductionHoldDto ToDto(ProductionControlHold h) => new(
        h.Id, h.HoldId, h.ProductionPieceId, h.JobRef,
        h.SpecGroupNumber, h.SerialNumber, h.Position, h.Stage,
        h.HoldReason, h.HeldAt, h.ReleasedAt, h.IsReleased, h.Notes);

    private static ProductionAssignmentDto ToDto(ProductionControlAssignment a) => new(
        a.Id, a.AssignmentId, a.ProductionPieceId, a.JobRef,
        a.SpecGroupNumber, a.SerialNumber, a.Position, a.Stage,
        a.WorkstationId, a.OperatorName, a.AssignedAt, a.UnassignedAt,
        a.IsActive, a.Notes);

    private static ProductionOverrideDto ToDto(ProductionControlOverride o) => new(
        o.Id, o.ControlId, o.ProductionPieceId, o.JobRef,
        o.SpecGroupNumber, o.SerialNumber, o.Position, o.Stage,
        o.Priority, o.DueDate, o.PrioritySetAt, o.Notes);

    private static ProductionResult<ProductionWorkItemDto> Fail(
        ProductionErrorCode code, string message) =>
        ProductionResult<ProductionWorkItemDto>.Fail(code, message);

    private async Task AuditAsync(
        Guid tenantId, Guid? actorUserId, string action, string entityType,
        Guid entityId, object? before, object? after, string? ip, CancellationToken ct)
        => await audit.WriteAsync(tenantId, actorUserId, action, entityType,
            entityId, ip, null,
            before is null ? null : JsonSerializer.Serialize(before),
            after is null ? null : JsonSerializer.Serialize(after),
            ct).ConfigureAwait(false);
}
