using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Production;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 26a service — the WPF process execution core:
//    • ProductionProcessExecutionService.StartAsync (gating via the route
//      + stage statuses derived from QtyDone; idempotent on
//      OutputWorkItem → projected id → legacy id; re-activates paused/
//      cancelled rows; the released work item flips to InProgress)
//    • PauseAsync (completed cannot pause)
//    • CompleteAsync (piece-flow: QtyDone is INCREMENTAL, clamped to
//      QtyRequired, never destructively overwritten; the work item's
//      QtyDone is NOT written here — the stage flow is authoritative)
//    • RecordBreakageAsync (Rejected/Rework + the deterministic child
//      replacement execution …|{reworkSeq+1} with OutputPieceId
//      {PieceId}-R{nn} — Original → Breakage → Replacement lineage)
//    • GetByJob / GetByProcess / GetProgressByJob
//  The pure halves (route, ids, CanBegin, detail summary) live in
//  Domain/Proforma/ProductionProcess.cs.
//  SaaS differences (documented, mapping §1.7c): the WPF's
//  NotifyWorkItemsChanged live-refresh event has no SaaS equivalent
//  (clients re-poll); the WPF's Phase 14 replacement WORK ITEM is the
//  26b slice, so the child execution links the original piece until
//  then; breakage also appends the Phase 9b Breakage event row.
// ═══════════════════════════════════════════════════════════════════════

public sealed class ProcessExecutionService(
    IProcessControlStore store, IProductionStore production, IAuditWriter audit,
    ReplacementService replacements)
{
    private const int MaxListTake = 200;

    // ── Start (WPF StartAsync, verbatim semantics) ───────────────────

    /// <summary>
    /// POST /executions/start — gating: the route predecessor must be
    /// complete. Idempotent: an existing execution for the work item (or
    /// its projected/legacy id) is returned; paused/cancelled rows are
    /// re-activated.
    /// </summary>
    public async Task<ProductionResult<ProcessExecutionDto>> StartAsync(
        Guid tenantId, Guid actorId, StartExecutionRequest request,
        string? ip, CancellationToken ct)
    {
        if (request.WorkItemId == Guid.Empty)
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.ValidationFailed, "WorkItemId required.");
        }

        var piece = await production.FindAsync(tenantId, request.WorkItemId, ct);
        if (piece is null)
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.NotFound,
                $"Work item {request.WorkItemId} was not found.");
        }

        var job = await store.FindJobAsync(tenantId, piece.JobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.NotFound,
                $"Job order {piece.JobOrderId} was not found.");
        }

        int processType = ProductionStages.Code(piece.Stage);
        if (processType < 1 || processType > 8)
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.ValidationFailed,
                $"'{piece.Stage}' is not a process stage.");
        }

        // Unit context for the route (WPF: JobRef+Spec+Serial, not cancelled).
        var jobPieces = await store.ListPiecesByJobAsync(tenantId, job.Id, ct);
        var unitPieces = jobPieces
            .Where(w => w.SpecGroupNumber == piece.SpecGroupNumber
                && w.SerialNumber == piece.SerialNumber
                && !w.IsCancelled)
            .ToList();

        var route = ProcessRouting.BuildRouteForWorkItem(
            ToRouteItem(piece, job.JobNo), unitPieces.Select(p => ToRouteItem(p, job.JobNo)).ToList());

        // Stage statuses derived from the work items' QtyDone (WPF verbatim).
        var stageStatuses = new Dictionary<string, string>();
        foreach (var wi in unitPieces)
        {
            var status = wi.QtyDone >= wi.Qty ? WorkItemStatuses.Completed
                : wi.QtyDone > 0 ? WorkItemStatuses.InProgress
                : WorkItemStatuses.Pending;
            if (wi.IsCancelled) status = WorkItemStatuses.Cancelled;
            stageStatuses[wi.Stage] = status;
        }

        if (!ProcessRouting.CanBegin(piece.Stage, route, stageStatuses))
        {
            var prev = route.PreviousRequiredBefore(piece.Stage);
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.Conflict,
                $"Process {ProductionStages.Name(processType)} cannot begin — " +
                $"predecessor {prev} not complete (gating).");
        }

        // Prefer the released Pending row (work-item link, then positioned
        // id, then legacy id).
        string executionId = ProcessRouting.BuildProjectedExecutionId(
            job.JobNo, piece.SpecGroupNumber, piece.SerialNumber, processType,
            piece.Position);
        string legacyId = ProcessRouting.BuildExecutionId(
            job.JobNo, piece.SpecGroupNumber, piece.SerialNumber, processType, 0);

        ProcessExecution? existing = await store.FindExecutionByPieceAsync(
            tenantId, piece.Id, processType, 0, ct);
        if (existing is null)
        {
            existing = await store.FindExecutionAsync(tenantId, executionId, ct);
        }
        if (existing is null && executionId != legacyId)
        {
            existing = await store.FindExecutionAsync(tenantId, legacyId, ct);
        }

        if (existing is not null)
        {
            if (existing.Status == WorkItemStatuses.Completed)
            {
                return ProductionResult<ProcessExecutionDto>.Ok(
                    ToDto(existing, job.JobNo), "Already completed (idempotent).");
            }
            if (existing.Status == WorkItemStatuses.InProgress)
            {
                return ProductionResult<ProcessExecutionDto>.Ok(
                    ToDto(existing, job.JobNo), "Already in progress (idempotent).");
            }

            // Re-activate if paused/cancelled.
            existing.Status = WorkItemStatuses.InProgress;
            existing.StartedBy = actorId;
            existing.StartedAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
            if (!await store.TryUpdateExecutionAsync(existing, ct))
            {
                return ProductionResult<ProcessExecutionDto>.Fail(
                    ProductionErrorCode.ConcurrencyConflict,
                    "The execution was modified by someone else — reload and retry.");
            }
            await AuditAsync(tenantId, actorId, "production.execution_resumed",
                "process_executions", existing.Id, null, new
                {
                    executionId = existing.ExecutionId,
                    stage = existing.ProcessType
                }, ip, ct);
            return ProductionResult<ProcessExecutionDto>.Ok(
                ToDto(existing, job.JobNo),
                $"Resumed {ProductionStages.Name(processType)} for {existing.PieceId}.");
        }

        // Stage move does not mint a new physical lite: input and output
        // keep the piece identity; pairing/assembly use their own ids.
        string pieceId = ResolvePieceIdentity(request.PieceId, piece, job.JobNo);
        var prevStage = route.PreviousRequiredBefore(piece.Stage) ?? "";
        var nextStage = route.NextRequiredAfter(piece.Stage) ?? "";

        var execution = new ProcessExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExecutionId = executionId,
            JobOrderId = job.Id,
            JobRef = job.JobNo,
            // production_pieces carries no SourceProformaId column — the
            // SaaS stores the source PI's id from the job.
            SourceProformaId = job.ProformaId.ToString(),
            ProductionPieceId = piece.Id,
            InputProductionPieceId = request.InputWorkItemId,
            SpecGroupNumber = piece.SpecGroupNumber,
            SpecName = piece.SpecName,
            SerialNumber = piece.SerialNumber,
            Position = piece.Position,
            PieceId = pieceId,
            InputPieceId = pieceId,
            OutputPieceId = pieceId,
            ProcessType = processType,
            Sequence = 0,
            Status = WorkItemStatuses.InProgress,
            StartedBy = actorId,
            StartedAt = DateTime.UtcNow,
            QtyRequired = request.QtyRequired is > 0 ? request.QtyRequired.Value : piece.Qty,
            QtyDone = 0,
            EntryType = ProductionEntryTypes.Normal,
            Notes = request.Notes ?? "",
            QcStatus = QcResults.Pending,
            IsFoc = request.IsFoc || piece.IsFoc,
            PreviousStage = prevStage,
            NextStage = nextStage,
            ProcessDetailJson = request.ProcessDetailJson ?? "",
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var stored = await store.UpsertExecutionAsync(tenantId, execution, ct);

        // Flip the released work item to InProgress when still Pending (WPF
        // does this in the same SaveChanges; the SaaS rides the stage row's
        // own update — the unique execution id keeps a retry safe).
        if (!piece.IsCancelled && piece.Status == WorkItemStatuses.Pending)
        {
            piece.Status = WorkItemStatuses.InProgress;
            piece.UpdatedAt = DateTime.UtcNow;
            piece.UpdatedBy = actorId;
            await production.TryUpdateAsync(piece, ct);
        }

        await AuditAsync(tenantId, actorId, "production.execution_started",
            "process_executions", stored.Id, null, new
            {
                executionId = stored.ExecutionId,
                stage = stored.ProcessType,
                pieceId = stored.PieceId,
                qtyRequired = stored.QtyRequired
            }, ip, ct);

        return ProductionResult<ProcessExecutionDto>.Ok(
            ToDto(stored, job.JobNo),
            $"Started {ProductionStages.Name(processType)} for {stored.PieceId}.");
    }

    // ── Pause / Complete / Breakage ──────────────────────────────────

    /// <summary>POST /executions/{id}/pause — completed rows cannot be
    /// paused; anything else pauses with the reason.</summary>
    public async Task<ProductionResult<ProcessExecutionDto>> PauseAsync(
        Guid tenantId, Guid actorId, string executionId,
        PauseExecutionRequest request, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(executionId))
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.ValidationFailed, "ExecutionId required.");
        }

        var exec = await store.FindExecutionAsync(tenantId, executionId, ct);
        if (exec is null)
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.NotFound, "Execution not found.");
        }
        if (exec.Status == WorkItemStatuses.Completed)
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.Conflict, "Already completed, cannot pause.");
        }

        exec.Status = WorkItemStatuses.Paused;
        exec.Reason = request.Reason ?? "";
        exec.UpdatedAt = DateTime.UtcNow;
        if (!await store.TryUpdateExecutionAsync(exec, ct))
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.ConcurrencyConflict,
                "The execution was modified by someone else — reload and retry.");
        }

        await AuditAsync(tenantId, actorId, "production.execution_paused",
            "process_executions", exec.Id, null, new
            {
                executionId = exec.ExecutionId,
                reason = request.Reason ?? ""
            }, ip, ct);
        return ProductionResult<ProcessExecutionDto>.Ok(ToDto(exec, exec.JobRef),
            $"Paused {ProductionStages.Name(exec.ProcessType)} {executionId}.");
    }

    /// <summary>
    /// POST /executions/{id}/complete — piece-flow: Required 10, cutting
    /// 10 completed, tempering 8 completed + 1 failed + 1 pending →
    /// 10 total, 8 passed, 1 failed, 1 remaining; no destructive
    /// overwrite. The work item's QtyDone is authoritative and is NOT
    /// written here.
    /// </summary>
    public async Task<ProductionResult<ProcessExecutionDto>> CompleteAsync(
        Guid tenantId, Guid actorId, string executionId,
        CompleteExecutionRequest request, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(executionId))
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.ValidationFailed, "ExecutionId required.");
        }

        var exec = await store.FindExecutionAsync(tenantId, executionId, ct);
        if (exec is null)
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.NotFound, "Execution not found.");
        }
        if (exec.Status == WorkItemStatuses.Completed)
        {
            return ProductionResult<ProcessExecutionDto>.Ok(ToDto(exec, exec.JobRef),
                "Already completed (idempotent).");
        }
        if (exec.Status == WorkItemStatuses.Cancelled)
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.Conflict, "Cancelled, cannot complete.");
        }

        if (exec.ProductionPieceId is { } linkedId)
        {
            var linked = await production.FindAsync(tenantId, linkedId, ct);
            if (linked is not null)
            {
                if (linked.IsCancelled
                    || linked.Status == WorkItemStatuses.Cancelled)
                {
                    return ProductionResult<ProcessExecutionDto>.Fail(
                        ProductionErrorCode.Conflict,
                        "Linked work item cancelled — cannot complete execution.");
                }
                if (linked.Status == WorkItemStatuses.Rejected)
                {
                    return ProductionResult<ProcessExecutionDto>.Fail(
                        ProductionErrorCode.Conflict,
                        "Linked work item rejected — cannot complete execution.");
                }
                if (linked.Status == WorkItemStatuses.QcFailed)
                {
                    return ProductionResult<ProcessExecutionDto>.Fail(
                        ProductionErrorCode.Conflict,
                        "Linked work item QC failed — cannot complete execution.");
                }
            }
        }

        if (request.QtyDone < 0)
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.ValidationFailed,
                "QtyDone cannot be negative.");
        }

        // Incremental, clamped to QtyRequired.
        int newDone = Math.Min(exec.QtyRequired, exec.QtyDone + request.QtyDone);
        exec.QtyDone = newDone;
        if (!string.IsNullOrWhiteSpace(request.Notes)) exec.Notes = request.Notes;
        if (!string.IsNullOrWhiteSpace(request.ProcessDetailJson))
            exec.ProcessDetailJson = request.ProcessDetailJson;

        exec.Status = newDone >= exec.QtyRequired
            ? WorkItemStatuses.Completed
            : WorkItemStatuses.InProgress;
        exec.UpdatedAt = DateTime.UtcNow;
        if (exec.Status == WorkItemStatuses.Completed)
        {
            exec.CompletedBy = actorId;
            exec.CompletedAt = DateTime.UtcNow;
        }

        if (!await store.TryUpdateExecutionAsync(exec, ct))
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.ConcurrencyConflict,
                "The execution was modified by someone else — reload and retry.");
        }

        await AuditAsync(tenantId, actorId, "production.execution_completed",
            "process_executions", exec.Id, null, new
            {
                executionId = exec.ExecutionId,
                stage = exec.ProcessType,
                qtyDone = newDone,
                qtyRequired = exec.QtyRequired
            }, ip, ct);
        return ProductionResult<ProcessExecutionDto>.Ok(ToDto(exec, exec.JobRef),
            $"Completed {ProductionStages.Name(exec.ProcessType)} {exec.PieceId} " +
            $"{newDone}/{exec.QtyRequired}.");
    }

    /// <summary>
    /// POST /executions/{id}/breakage — Original → failure → Breakage/
    /// Rework → Replacement → required process again; the original is
    /// never reset as if nothing happened. EntryType 1 = Breakage,
    /// 2 = Rework. The deterministic child execution keeps the lineage
    /// traceable even before the replacement work item exists (26b).
    /// </summary>
    public async Task<ProductionResult<ProcessExecutionDto>> RecordBreakageAsync(
        Guid tenantId, Guid actorId, string executionId,
        ExecutionBreakageRequest request, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(executionId))
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.ValidationFailed, "ExecutionId required.");
        }
        if (request.EntryType is not (1 or 2))
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.ValidationFailed,
                "EntryType must be 1 (Breakage) or 2 (Rework).");
        }
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.ValidationFailed, "A breakage reason is required.");
        }

        var exec = await store.FindExecutionAsync(tenantId, executionId, ct);
        if (exec is null)
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.NotFound, "Execution not found.");
        }

        exec.Status = request.EntryType == 1
            ? WorkItemStatuses.Rejected
            : WorkItemStatuses.Rework;
        exec.EntryType = request.EntryType == 1
            ? ProductionEntryTypes.Breakage
            : ProductionEntryTypes.Rework;
        exec.Reason = request.Reason;
        exec.IsFoc = request.IsFoc;
        exec.UpdatedAt = DateTime.UtcNow;
        if (!await store.TryUpdateExecutionAsync(exec, ct))
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.ConcurrencyConflict,
                "The execution was modified by someone else — reload and retry.");
        }

        // The Phase 14 replacement flow (WPF reuses
        // ProductionBreakageService.RecordBreakageAsync with qty 1): the
        // breakage event lands on the linked work item AND a traceable
        // replacement work item + replacement row are created — since 26b
        // the child execution links the real replacement piece (the 26a
        // placeholder linked the original; mapping §1.7d).
        Guid? replacementPieceId = null;
        string? replacementId = null;
        if (exec.ProductionPieceId is { } pieceId)
        {
            var piece = await production.FindAsync(tenantId, pieceId, ct);
            if (piece is not null && !piece.IsCancelled)
            {
                var replacementResult = await replacements.RecordBreakageAsync(
                    tenantId, actorId,
                    new ReplacementBreakageRequest(
                        piece.Id, 1, request.Reason, request.IsFoc,
                        $"Breakage during {ProductionStages.Name(exec.ProcessType)} {executionId}"),
                    ip, ct);
                if (!replacementResult.Success)
                {
                    return ProductionResult<ProcessExecutionDto>.Fail(
                        replacementResult.ErrorCode,
                        $"Breakage record failed: {replacementResult.Message}");
                }
                replacementPieceId = replacementResult.Value?.ReplacementPieceId;
                replacementId = replacementResult.Value?.ReplacementId;
            }
        }

        // Deterministic child execution for the replacement (WPF
        // BuildExecutionId with ReworkSequence + 1).
        string newExecId = ProcessRouting.BuildExecutionId(
            exec.JobRef, exec.SpecGroupNumber, exec.SerialNumber,
            exec.ProcessType, exec.ReworkSequence + 1);
        var child = new ProcessExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExecutionId = newExecId,
            JobOrderId = exec.JobOrderId,
            JobRef = exec.JobRef,
            SourceProformaId = exec.SourceProformaId,
            ProductionPieceId = replacementPieceId ?? exec.ProductionPieceId, // the replacement piece (26b); the original until it exists
            InputProductionPieceId = exec.ProductionPieceId,
            SpecGroupNumber = exec.SpecGroupNumber,
            SpecName = exec.SpecName,
            SerialNumber = exec.SerialNumber,
            Position = exec.Position,
            PieceId = exec.PieceId,
            InputPieceId = exec.PieceId,
            OutputPieceId = $"{exec.PieceId}-R{exec.ReworkSequence + 1:00}",
            ProcessType = exec.ProcessType,
            Sequence = exec.ReworkSequence + 1,
            Status = WorkItemStatuses.Pending,
            QtyRequired = 1,
            QtyDone = 0,
            EntryType = ProductionEntryTypes.Normal,
            ParentExecutionId = executionId,
            ReworkSequence = exec.ReworkSequence + 1,
            IsFoc = request.IsFoc,
            PreviousStage = exec.PreviousStage,
            NextStage = exec.NextStage,
            Notes = replacementId is null
                ? $"Replacement for breakage {executionId}"
                : $"Replacement for breakage {executionId} → {replacementId}",
            QcStatus = QcResults.Pending,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await store.UpsertExecutionAsync(tenantId, child, ct);

        await AuditAsync(tenantId, actorId, "production.execution_breakage",
            "process_executions", exec.Id, null, new
            {
                executionId,
                entryType = request.EntryType,
                replacementExecutionId = newExecId,
                outputPieceId = child.OutputPieceId,
                replacementPieceId,
                replacementId
            }, ip, ct);

        return ProductionResult<ProcessExecutionDto>.Ok(ToDto(exec, exec.JobRef),
            $"Breakage recorded: {exec.PieceId} → {executionId} → replacement " +
            $"{newExecId} ({child.OutputPieceId}) traceable.");
    }

    // ── Reads ────────────────────────────────────────────────────────

    public async Task<ProductionResult<ProcessExecutionDto>> GetAsync(
        Guid tenantId, string executionId, CancellationToken ct)
    {
        var exec = await store.FindExecutionAsync(tenantId, executionId, ct);
        if (exec is null)
        {
            return ProductionResult<ProcessExecutionDto>.Fail(
                ProductionErrorCode.NotFound, "Execution not found.");
        }
        return ProductionResult<ProcessExecutionDto>.Ok(ToDto(exec, exec.JobRef));
    }

    /// <summary>GET /executions?jobOrderId= — WPF GetByJobAsync order
    /// (Spec, Serial, ProcessType, Sequence).</summary>
    public async Task<ProductionResult<IReadOnlyList<ProcessExecutionDto>>> ListByJobAsync(
        Guid tenantId, Guid? jobOrderId, CancellationToken ct)
    {
        if (jobOrderId is null)
        {
            return ProductionResult<IReadOnlyList<ProcessExecutionDto>>.Fail(
                ProductionErrorCode.ValidationFailed, "jobOrderId is required.");
        }
        var rows = await store.ListExecutionsByJobAsync(tenantId, jobOrderId.Value, ct);
        return ProductionResult<IReadOnlyList<ProcessExecutionDto>>.Ok(
            rows.Select(e => ToDto(e, e.JobRef)).ToList());
    }

    /// <summary>GET /executions?processType= — WPF GetByProcessAsync
    /// (most recent first, capped at 200).</summary>
    public async Task<ProductionResult<IReadOnlyList<ProcessExecutionDto>>> ListByProcessAsync(
        Guid tenantId, int processType, CancellationToken ct)
    {
        if (processType is < 1 or > 8)
        {
            return ProductionResult<IReadOnlyList<ProcessExecutionDto>>.Fail(
                ProductionErrorCode.ValidationFailed,
                "processType must be 1–8.");
        }
        var rows = await store.ListExecutionsByProcessAsync(
            tenantId, processType, MaxListTake, ct);
        return ProductionResult<IReadOnlyList<ProcessExecutionDto>>.Ok(
            rows.Select(e => ToDto(e, e.JobRef)).ToList());
    }

    /// <summary>GET /executions/progress?jobOrderId= — WPF
    /// GetProgressByJobAsync: required/completed/failed/rework/
    /// qcPending with remaining = required − completed − failed.</summary>
    public async Task<ProductionResult<ExecutionProgressDto>> GetProgressByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var execs = await store.ListExecutionsByJobAsync(tenantId, jobOrderId, ct);
        int required = execs.Sum(e => e.QtyRequired);
        int completed = execs
            .Where(e => e.Status == WorkItemStatuses.Completed)
            .Sum(e => e.QtyDone);
        int failed = execs
            .Where(e => e.Status == WorkItemStatuses.Rejected
                || e.Status == WorkItemStatuses.Cancelled)
            .Sum(e => e.QtyRequired - e.QtyDone);
        int rework = execs.Count(e => e.Status == WorkItemStatuses.Rework);
        int qcPending = execs.Count(e =>
            e.QcStatus == QcResults.Pending && e.Status == WorkItemStatuses.Completed);
        int remaining = Math.Max(0, required - completed - failed);
        return ProductionResult<ExecutionProgressDto>.Ok(new ExecutionProgressDto(
            required, completed, remaining, failed, rework, qcPending));
    }

    // ── helpers ──────────────────────────────────────────────────────

    internal static RouteWorkItem ToRouteItem(ProductionPiece p, string jobRef) => new(
        jobRef,
        jobRef, // SourceProformaId rides the job (pieces carry no column)
        p.SpecGroupNumber,
        p.SpecName,
        p.SerialNumber,
        p.Position,
        p.BuildType,
        p.GlassRef,
        p.Stage,
        p.Status,
        p.IsCancelled,
        p.Qty,
        p.QtyDone,
        p.UniqueKey,
        p.WidthMm,
        p.HeightMm,
        p.ReplacementSequence);

    /// <summary>The pane-instance identity when the caller knows it, else
    /// the stable stage identity (WPF Start callers pass the cutting
    /// identity; the projection falls back to the unique key).</summary>
    private static string ResolvePieceIdentity(
        string? requested, ProductionPiece piece, string jobRef)
    {
        if (!string.IsNullOrWhiteSpace(requested)) return requested.Trim();
        return string.IsNullOrWhiteSpace(piece.UniqueKey)
            ? $"{jobRef}|{piece.SpecGroupNumber}|{piece.SerialNumber}|{piece.Position}"
            : piece.UniqueKey;
    }

    internal static ProcessExecutionDto ToDto(ProcessExecution e, string jobRef) => new(
        e.Id,
        e.ExecutionId,
        string.IsNullOrEmpty(e.JobRef) ? jobRef : e.JobRef,
        e.JobOrderId,
        e.SourceProformaId,
        e.SpecGroupNumber,
        e.SpecName,
        e.SerialNumber,
        e.Position,
        e.PieceId,
        e.InputPieceId,
        e.OutputPieceId,
        e.ProcessType,
        ProductionStages.Name(e.ProcessType),
        e.Sequence,
        e.Status,
        e.ProductionPieceId,
        e.InputProductionPieceId,
        e.StartedAt,
        e.CompletedAt,
        e.QtyRequired,
        e.QtyDone,
        e.EntryType,
        e.Reason,
        e.Notes,
        e.QcStatus,
        e.QcId,
        e.ParentExecutionId,
        e.ReworkSequence,
        e.IsFoc,
        e.PreviousStage,
        e.NextStage,
        e.ProcessDetailJson,
        ProcessDetailSummary.Summarize(e.ProcessDetailJson, e.ProcessType),
        ProductionPieceIdentity.Classify(e.ExecutionId),
        e.CreatedAt,
        e.UpdatedAt);

    private async Task AuditAsync(
        Guid tenantId, Guid? actorUserId, string action, string entityType,
        Guid entityId, object? before, object? after, string? ip, CancellationToken ct)
        => await audit.WriteAsync(tenantId, actorUserId, action, entityType,
            entityId, ip, null,
            before is null ? null : JsonSerializer.Serialize(before),
            after is null ? null : JsonSerializer.Serialize(after),
            ct).ConfigureAwait(false);
}
