using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Production;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 9b service — the WPF production core:
//    • ProductionDistributionEngine.Distribute (the deterministic plan —
//      pure, so planning is a read; nothing persists until release)
//    • ProductionReleaseService (release → work items; a re-release
//      produces the SAME unique keys, so statuses survive)
//    • ProductionRepository stage entries (Normal progress + QtyDone)
//    • ProductionQcService.RecordQcAsync (Cancelled guard, deterministic
//      QcId, status mapping Failed→QcFailed / Passed→Completed when
//      QtyDone≥Qty / Rework→Rework; QcFailed never reverts QtyDone)
//    • ProductionCancellationService.CancelWorkItemAsync (idempotent,
//      completed work cancellable but warned)
//    • ProductionBreakageService (breakage entry with reason; the
//      replacement-piece workflow is a later slice)
//  Permissions: the WPF production services carry no OperationGuard —
//  the UI gates the floor through the Production role; the SaaS gates
//  writes with JobOrder.Edit (the Production role's floor permission) —
//  documented difference.
// ═══════════════════════════════════════════════════════════════════════

/// <summary>The Production module (plan → release → floor lifecycle).</summary>
public sealed class ProductionService(
    IProductionStore store, IProcessControlStore control, IAuditWriter audit)
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    // ── Planning (pure — WPF Distribute) ────────────────────────────

    /// <summary>
    /// GET /job-orders/{id}/plan — the deterministic plan: every spec row
    /// fanned out into stage work items. Nothing is persisted.
    /// </summary>
    public async Task<ProductionResult<ProductionPlanDto>> PlanAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var job = await store.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<ProductionPlanDto>.Fail(
                ProductionErrorCode.NotFound,
                $"Job order {jobOrderId} was not found.");
        }

        var plan = await BuildPlanAsync(job, ct);
        return ProductionResult<ProductionPlanDto>.Ok(new ProductionPlanDto(
            job.JobNo, job.CustomerName, job.ProjectName,
            plan.Items.Select(ToDto).ToList(), plan.Warnings));
    }

    private async Task<ProductionJobPlan> BuildPlanAsync(
        JobOrder job, CancellationToken ct)
    {
        var specs = await store.GetSpecInputsAsync(tenantId: job.TenantId,
            job.ProformaId, ct);
        var charges = await store.GetChargeInputsAsync(
            job.TenantId, job.ProformaId, ct);
        return ProductionDistributionEngine.Distribute(
            job.JobNo, job.CustomerName, job.ProjectName, "", specs, charges);
    }

    // ── Release (WPF ProductionReleaseService) ──────────────────────

    /// <summary>
    /// POST /job-orders/{id}/release — persist the plan as work items.
    /// Deterministic unique keys mean a re-release only inserts NEW
    /// items; existing rows (and their floor statuses) survive.
    /// </summary>
    public async Task<ProductionResult<ReleaseResultDto>> ReleaseAsync(
        Guid tenantId, Guid actorId, Guid jobOrderId, string? ip,
        CancellationToken ct)
    {
        var job = await store.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<ReleaseResultDto>.Fail(
                ProductionErrorCode.NotFound,
                $"Job order {jobOrderId} was not found.");
        }

        var plan = await BuildPlanAsync(job, ct);
        if (plan.Items.Count == 0)
        {
            return ProductionResult<ReleaseResultDto>.Fail(
                ProductionErrorCode.ValidationFailed,
                $"Job order {job.JobNo} has no production items " +
                "(the source grid is empty).");
        }

        var existingKeys = await store.GetExistingKeysAsync(tenantId, job.Id, ct);
        var missing = plan.Items
            .Where(i => !existingKeys.Contains(i.UniqueKey))
            .ToList();

        var inserted = await store.InsertNewAsync(
            tenantId, job.Id, actorId, missing, ct);

        // Phase 26a — project one Pending execution per live work item
        // (WPF ProjectJobAsync inside the release; failures only warn).
        int projected = 0;
        try
        {
            projected = await control.ProjectPendingExecutionsAsync(
                tenantId, job.Id, actorId, ct);
        }
        catch (Exception ex)
        {
            plan.Warnings.Add(
                "Released the job, but the process list could not be updated: "
                + ex.Message);
        }

        await AuditAsync(tenantId, actorId, "production.released",
            "job_orders", job.Id, null, new
            {
                jobOrderNo = job.JobNo,
                planned = plan.Items.Count,
                inserted,
                preserved = plan.Items.Count - inserted,
                projected
            }, ip, ct);
        return ProductionResult<ReleaseResultDto>.Ok(new ReleaseResultDto(
            job.JobNo, plan.Items.Count, inserted,
            plan.Items.Count - inserted, plan.Warnings, projected));
    }

    // ── Queries ─────────────────────────────────────────────────────

    public async Task<ProductionResult<IReadOnlyList<ProductionWorkItemDto>>> ListForJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var job = await store.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<IReadOnlyList<ProductionWorkItemDto>>.Fail(
                ProductionErrorCode.NotFound,
                $"Job order {jobOrderId} was not found.");
        }

        var pieces = await store.ListByJobAsync(tenantId, jobOrderId, ct);
        return ProductionResult<IReadOnlyList<ProductionWorkItemDto>>.Ok(
            pieces.Select(ToDto).ToList());
    }

    public async Task<MasterData.PagedResult<ProductionWorkItemDto>> ListAsync(
        Guid tenantId, Guid? jobOrderId, string? stage, string? status,
        bool includeCancelled, int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = NormalizePaging(page, pageSize);
        var result = await store.SearchAsync(
            tenantId, jobOrderId, stage, status, includeCancelled, p, size, ct);
        return new MasterData.PagedResult<ProductionWorkItemDto>(
            result.Items.Select(ToDto).ToList(),
            result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<ProductionResult<ProductionWorkItemDto>> GetAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var piece = await store.FindAsync(tenantId, id, ct);
        return piece is null
            ? ProductionResult<ProductionWorkItemDto>.Fail(
                ProductionErrorCode.NotFound, $"Work item {id} was not found.")
            : ProductionResult<ProductionWorkItemDto>.Ok(ToDto(piece));
    }

    public async Task<ProductionResult<IReadOnlyList<ProductionEventDto>>> GetEventsAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct)
    {
        var piece = await store.FindAsync(tenantId, pieceId, ct);
        if (piece is null)
        {
            return ProductionResult<IReadOnlyList<ProductionEventDto>>.Fail(
                ProductionErrorCode.NotFound, $"Work item {pieceId} was not found.");
        }

        var events = await store.ListEventsAsync(tenantId, pieceId, ct);
        return ProductionResult<IReadOnlyList<ProductionEventDto>>.Ok(
            events.Select(ToDto).ToList());
    }

    // ── Floor lifecycle ─────────────────────────────────────────────

    /// <summary>
    /// POST /{id}/stage — floor progress (WPF ProductionRepository entry
    /// + QtyDone). Status auto-derives when not supplied: any progress →
    /// InProgress, QtyDone ≥ Qty → Completed.
    /// </summary>
    public async Task<ProductionResult<ProductionWorkItemDto>> RecordStageAsync(
        Guid tenantId, Guid id, Guid actorId, StageProgressRequest request,
        string? ip, CancellationToken ct)
    {
        var piece = await store.FindAsync(tenantId, id, ct);
        if (piece is null)
        {
            return ProductionResult<ProductionWorkItemDto>.Fail(
                ProductionErrorCode.NotFound, $"Work item {id} was not found.");
        }

        if (piece.IsCancelled
            || string.Equals(piece.Status, WorkItemStatuses.Cancelled,
                StringComparison.OrdinalIgnoreCase))
        {
            return ProductionResult<ProductionWorkItemDto>.Fail(
                ProductionErrorCode.Conflict, "Cancelled work cannot be updated.");
        }

        if (request.QtyDone < 0 || request.QtyDone > piece.Qty)
        {
            return ProductionResult<ProductionWorkItemDto>.Fail(
                ProductionErrorCode.ValidationFailed,
                $"Quantity done {request.QtyDone} is outside 0–{piece.Qty}.");
        }

        piece.QtyDone = request.QtyDone;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!WorkItemStatuses.All.Contains(request.Status.Trim(),
                    StringComparer.Ordinal))
            {
                return ProductionResult<ProductionWorkItemDto>.Fail(
                    ProductionErrorCode.ValidationFailed,
                    $"'{request.Status.Trim()}' is not a work item status.");
            }
            piece.Status = request.Status.Trim();
        }
        else
        {
            // WPF SetDoneQtyAsync: QcFailed/Rework statuses are frozen —
            // auto-derivation never overwrites them (Phase 26a parity).
            if (piece.Status != WorkItemStatuses.QcFailed
                && piece.Status != WorkItemStatuses.Rework)
            {
                piece.Status = piece.QtyDone >= piece.Qty
                    ? WorkItemStatuses.Completed
                    : piece.QtyDone > 0 ? WorkItemStatuses.InProgress
                    : WorkItemStatuses.Pending;
            }
        }
        piece.UpdatedAt = DateTime.UtcNow;
        piece.UpdatedBy = actorId;
        piece.CompletedAt = piece.Status == WorkItemStatuses.Completed
            ? DateTime.UtcNow : null;
        piece.CompletedBy = piece.Status == WorkItemStatuses.Completed
            ? actorId : null;

        if (!await store.TryUpdateAsync(piece, ct))
        {
            return ProductionResult<ProductionWorkItemDto>.Fail(
                ProductionErrorCode.ConcurrencyConflict,
                "The work item was modified by someone else — reload and retry.");
        }

        // The work item is authoritative — mirror onto the sequence-0
        // process execution (WPF SyncLinkedExecutionQty, Phase 26a).
        await control.SyncLinkedExecutionsAsync(tenantId, piece, ct);

        await store.AppendEventAsync(tenantId, new ProductionEvent
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
            Qty = request.QtyDone,
            DoneBy = actorId,
            Reason = request.Reason ?? "",
            CreatedAt = DateTime.UtcNow
        }, ct);

        return ProductionResult<ProductionWorkItemDto>.Ok(ToDto(piece));
    }

    /// <summary>
    /// POST /{id}/qc — WPF RecordQcAsync: cancelled work cannot be QC'd;
    /// deterministic sequence per item; Failed → QcFailed (QtyDone is NOT
    /// reverted — it only marks the status), Passed → Completed when
    /// QtyDone ≥ Qty, Rework → Rework.
    /// </summary>
    public async Task<ProductionResult<ProductionWorkItemDto>> RecordQcAsync(
        Guid tenantId, Guid id, Guid actorId, QcRequest request,
        string? ip, CancellationToken ct)
    {
        var piece = await store.FindAsync(tenantId, id, ct);
        if (piece is null)
        {
            return ProductionResult<ProductionWorkItemDto>.Fail(
                ProductionErrorCode.NotFound, $"Work item {id} was not found.");
        }

        if (piece.IsCancelled
            || string.Equals(piece.Status, WorkItemStatuses.Cancelled,
                StringComparison.OrdinalIgnoreCase))
        {
            return ProductionResult<ProductionWorkItemDto>.Fail(
                ProductionErrorCode.Conflict, "Cancelled work cannot be QC'd.");
        }

        var result = (request.Result ?? "").Trim();
        if (result is not ("Passed" or "Failed" or "Rework"))
        {
            return ProductionResult<ProductionWorkItemDto>.Fail(
                ProductionErrorCode.ValidationFailed,
                "QC result must be Passed, Failed or Rework.");
        }

        // Deterministic sequence for this work item.
        var qcEvents = await store.ListQcEventsAsync(tenantId, piece.Id, ct);
        var nextSeq = qcEvents.Count == 0
            ? 1
            : qcEvents.Select(ParseQcSequence).Max() + 1;

        if (piece.QtyDone >= piece.Qty
            && string.Equals(result, QcResults.Passed, StringComparison.Ordinal))
        {
            piece.Status = WorkItemStatuses.Completed;
        }
        else if (string.Equals(result, QcResults.Failed, StringComparison.Ordinal))
        {
            piece.Status = WorkItemStatuses.QcFailed;
        }
        else if (string.Equals(result, QcResults.Rework, StringComparison.Ordinal))
        {
            piece.Status = WorkItemStatuses.Rework;
        }
        piece.UpdatedAt = DateTime.UtcNow;
        piece.UpdatedBy = actorId;

        if (!await store.TryUpdateAsync(piece, ct))
        {
            return ProductionResult<ProductionWorkItemDto>.Fail(
                ProductionErrorCode.ConcurrencyConflict,
                "The work item was modified by someone else — reload and retry.");
        }

        await store.AppendEventAsync(tenantId, new ProductionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductionPieceId = piece.Id,
            EventType = result == QcResults.Passed
                ? ProductionEntryTypes.QcPassed
                : result == QcResults.Failed
                    ? ProductionEntryTypes.QcFailed
                    : ProductionEntryTypes.Rework,
            JobOrderId = piece.JobOrderId,
            Stage = piece.Stage,
            SpecGroupNumber = piece.SpecGroupNumber,
            SerialNumber = piece.SerialNumber,
            Position = piece.Position,
            SpecName = piece.SpecName,
            GlassRef = piece.GlassRef,
            Qty = piece.QtyDone,
            DoneBy = actorId,
            Reason = request.Reason ?? "",
            CreatedAt = DateTime.UtcNow,
            Payload = JsonSerializer.Serialize(new
            {
                qcSequence = nextSeq,
                result,
                notes = request.Notes ?? ""
            })
        }, ct);

        await AuditAsync(tenantId, actorId, "production.qc_recorded",
            "production_pieces", piece.Id, null, new
            {
                uniqueKey = piece.UniqueKey,
                result,
                sequence = nextSeq
            }, ip, ct);
        return ProductionResult<ProductionWorkItemDto>.Ok(ToDto(piece));
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

    /// <summary>
    /// POST /{id}/cancel — WPF CancelWorkItemAsync: idempotent; completed
    /// work IS cancellable (the history is preserved, the warning is the
    /// caller's to read from the plan warnings).
    /// </summary>
    public async Task<ProductionResult<ProductionWorkItemDto>> CancelAsync(
        Guid tenantId, Guid id, Guid actorId, CancelPieceRequest request,
        string? ip, CancellationToken ct)
    {
        var piece = await store.FindAsync(tenantId, id, ct);
        if (piece is null)
        {
            return ProductionResult<ProductionWorkItemDto>.Fail(
                ProductionErrorCode.NotFound, $"Work item {id} was not found.");
        }

        if (!piece.IsCancelled)
        {
            piece.IsCancelled = true;
            piece.CancelledAt = DateTime.UtcNow;
            piece.CancelledBy = actorId;
            piece.CancelReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Cancelled"
                : request.Reason!.Trim();
            piece.Status = WorkItemStatuses.Cancelled;
            piece.UpdatedAt = DateTime.UtcNow;
            piece.UpdatedBy = actorId;

            if (!await store.TryUpdateAsync(piece, ct))
            {
                return ProductionResult<ProductionWorkItemDto>.Fail(
                    ProductionErrorCode.ConcurrencyConflict,
                    "The work item was modified by someone else — reload and retry.");
            }

            await store.AppendEventAsync(tenantId, new ProductionEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductionPieceId = piece.Id,
                EventType = ProductionEntryTypes.Cancelled,
                JobOrderId = piece.JobOrderId,
                Stage = piece.Stage,
                SpecGroupNumber = piece.SpecGroupNumber,
                SerialNumber = piece.SerialNumber,
                Position = piece.Position,
                SpecName = piece.SpecName,
                GlassRef = piece.GlassRef,
                Qty = piece.Qty,
                DoneBy = actorId,
                Reason = piece.CancelReason,
                CreatedAt = DateTime.UtcNow
            }, ct);
            await AuditAsync(tenantId, actorId, "production.cancelled",
                "production_pieces", piece.Id, null,
                new { uniqueKey = piece.UniqueKey, reason = piece.CancelReason },
                ip, ct);
        }

        return ProductionResult<ProductionWorkItemDto>.Ok(ToDto(piece));
    }

    /// <summary>
    /// POST /{id}/breakage — WPF RecordBreakageAsync core: a breakage
    /// entry with qty + reason (qty must be positive and within the
    /// item's remaining work). The replacement-piece workflow and the FOC
    /// re-cut path are later slices.
    /// </summary>
    public async Task<ProductionResult<ProductionWorkItemDto>> RecordBreakageAsync(
        Guid tenantId, Guid id, Guid actorId, BreakageRequest request,
        string? ip, CancellationToken ct)
    {
        var piece = await store.FindAsync(tenantId, id, ct);
        if (piece is null)
        {
            return ProductionResult<ProductionWorkItemDto>.Fail(
                ProductionErrorCode.NotFound, $"Work item {id} was not found.");
        }

        if (piece.IsCancelled
            || string.Equals(piece.Status, WorkItemStatuses.Cancelled,
                StringComparison.OrdinalIgnoreCase))
        {
            return ProductionResult<ProductionWorkItemDto>.Fail(
                ProductionErrorCode.Conflict, "Cancelled work cannot be broken.");
        }

        if (request.QtyBroken <= 0)
        {
            return ProductionResult<ProductionWorkItemDto>.Fail(
                ProductionErrorCode.ValidationFailed,
                "Broken quantity must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ProductionResult<ProductionWorkItemDto>.Fail(
                ProductionErrorCode.ValidationFailed,
                "A breakage reason is required.");
        }

        await store.AppendEventAsync(tenantId, new ProductionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductionPieceId = piece.Id,
            EventType = ProductionEntryTypes.Breakage,
            JobOrderId = piece.JobOrderId,
            Stage = piece.Stage,
            SpecGroupNumber = piece.SpecGroupNumber,
            SerialNumber = piece.SerialNumber,
            Position = piece.Position,
            SpecName = piece.SpecName,
            GlassRef = piece.GlassRef,
            Qty = request.QtyBroken,
            DoneBy = actorId,
            Reason = request.Reason.Trim(),
            CreatedAt = DateTime.UtcNow,
            Payload = JsonSerializer.Serialize(new
            {
                notes = request.Notes ?? ""
            })
        }, ct);
        await AuditAsync(tenantId, actorId, "production.breakage_recorded",
            "production_pieces", piece.Id, null, new
            {
                uniqueKey = piece.UniqueKey,
                qty = request.QtyBroken
            }, ip, ct);

        return ProductionResult<ProductionWorkItemDto>.Ok(ToDto(piece));
    }

    // ── helpers ─────────────────────────────────────────────────────

    private static (int Page, int Size) NormalizePaging(int? page, int? pageSize)
    {
        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        return (Math.Max(page ?? 1, 1), size);
    }

    internal static ProductionWorkItemDto ToDto(ProductionPiece p) => new(
        p.Id, p.UniqueKey, p.SpecGroupNumber, p.SpecName, p.BuildType,
        p.SerialNumber, p.GlassRef, p.Position, p.Stage,
        ProductionStages.StageLabel(p.Stage), p.Status,
        p.WidthMm, p.HeightMm, p.Qty, p.QtyDone, p.UnitSqm, p.TotalSqm,
        p.TotalLm, p.Treatment, p.PredecessorStage,
        p.ChargeTemplateKey, p.ChargeMetricQty, p.ChargeMetricUnit,
        p.PolishSubTypeLabel, p.IsFoc, p.IsCancelled, p.CancelReason, p.Notes);

    private static ProductionEventDto ToDto(ProductionEvent e) => new(
        e.Id, e.EventType, e.Stage, e.SpecGroupNumber, e.SerialNumber,
        e.Position, e.GlassRef, e.Qty, e.Reason, e.CreatedAt);

    private async Task AuditAsync(
        Guid tenantId, Guid? actorUserId, string action, string entityType,
        Guid entityId, object? before, object? after, string? ip, CancellationToken ct)
        => await audit.WriteAsync(tenantId, actorUserId, action, entityType,
            entityId, ip, null,
            before is null ? null : JsonSerializer.Serialize(before),
            after is null ? null : JsonSerializer.Serialize(after),
            ct).ConfigureAwait(false);
}
