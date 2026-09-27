// ═══════════════════════════════════════════════════════════════════════
//  FILE: ProGlass.Saas.Application/Production/ReplacementService.cs
//  PORTED from the WPF application (read 2026-09-27):
//    • Services/Production/ProductionBreakageService.cs — RecordBreakage
//      (the original row is NEVER silently modified: breakage event +
//      replacement work item + replacement row), RecordRework (event +
//      Rework status when not completed), RecordFoc (FOC = free of charge,
//      cost preserved, selling zero).
//    • Models/Production/SavedProduction/SavedProductionPhase14Entities.cs
//      — SavedProductionReplacement + the deterministic ReplacementId
//      JobRef|Spec|Serial|Position|{stage}|RPL:{n}.
//  SaaS differences (documented, mapping §1.7d): the WPF's int WorkItemId
//  becomes production_pieces uuid; the replacement work item's
//  ParentWorkItemId has no SaaS column — the production_replacements row
//  (original_piece_id → replacement_piece_id) IS the lineage; the WPF's
//  two-step FOC flip (Rework row rewritten after SaveChanges) is persisted
//  as Foc directly — same end state; breakage entries are the Phase 9b
//  production_events rows (the WPF's SavedProductionEntry), with the
//  parent-entry link in the payload.
// ═══════════════════════════════════════════════════════════════════════

using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Production;

public sealed class ReplacementService(
    ICuttingCostStore store, IProductionStore production, IAuditWriter audit)
{
    // ── RecordBreakage (WPF RecordBreakageAsync, verbatim semantics) ──

    /// <summary>
    /// POST /replacements/breakage — breakage for a work item: history is
    /// preserved, a Breakage event lands on the original, and a traceable
    /// replacement work item + replacement row are created. Idempotent on
    /// the deterministic ReplacementId.
    /// </summary>
    public async Task<ProductionResult<ReplacementDto>> RecordBreakageAsync(
        Guid tenantId, Guid actorId, ReplacementBreakageRequest request,
        string? ip, CancellationToken ct)
    {
        if (request.PieceId == Guid.Empty)
        {
            return ProductionResult<ReplacementDto>.Fail(
                ProductionErrorCode.ValidationFailed, "PieceId required.");
        }
        if (request.Quantity <= 0)
        {
            return ProductionResult<ReplacementDto>.Fail(
                ProductionErrorCode.ValidationFailed, "Breakage qty must be >0.");
        }
        string reason = string.IsNullOrWhiteSpace(request.Reason)
            ? "Breakage reported" : request.Reason.Trim();

        var piece = await production.FindAsync(tenantId, request.PieceId, ct);
        if (piece is null)
        {
            return ProductionResult<ReplacementDto>.Fail(
                ProductionErrorCode.NotFound, "Work item not found.");
        }
        if (piece.IsCancelled
            || string.Equals(piece.Status, WorkItemStatuses.Cancelled,
                StringComparison.OrdinalIgnoreCase))
        {
            return ProductionResult<ReplacementDto>.Fail(
                ProductionErrorCode.Conflict,
                "Cancelled work cannot record breakage.");
        }

        var job = await production.FindJobAsync(tenantId, piece.JobOrderId, ct);
        string jobRef = job?.JobNo ?? piece.UniqueKey.Split('|').FirstOrDefault() ?? "";

        // Replacement sequence for this work item (WPF: max + 1).
        var existingReplacements = await store.ListReplacementsByPieceAsync(
            tenantId, piece.Id, ct);
        int nextSeq = existingReplacements.Count == 0
            ? 1
            : existingReplacements.Max(r => r.ReplacementSequence) + 1;

        string replacementId = CuttingIds.BuildReplacementId(
            jobRef, piece.SpecGroupNumber, piece.SerialNumber, piece.Position,
            ProductionStages.Code(piece.Stage), nextSeq);

        // Idempotency: the deterministic id already exists.
        var existing = await store.FindReplacementAsync(tenantId, replacementId, ct);
        if (existing is not null)
        {
            return ProductionResult<ReplacementDto>.Ok(ToDto(existing),
                "Breakage recorded, replacement already exists (idempotent).");
        }

        // Replacement work item — same geometry, replacement sequence,
        // FOC flag; UniqueKey = original|RPL:{n} (never masquerades as the
        // original).
        string replacementUniqueKey = CuttingIds.BuildReplacementWorkItemKey(
            piece.UniqueKey, nextSeq);

        var replacementPiece = new ProductionPiece
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JobOrderId = piece.JobOrderId,
            PieceLabel = $"{piece.PieceLabel} RPL:{nextSeq}",
            SpecGroupItemId = piece.SpecGroupItemId,
            Stage = piece.Stage,
            Status = WorkItemStatuses.Pending,
            WidthMm = piece.WidthMm,
            HeightMm = piece.HeightMm,
            Qty = request.Quantity, // replacement qty = broken qty
            QtyDone = 0,
            UniqueKey = replacementUniqueKey,
            SpecGroupNumber = piece.SpecGroupNumber,
            SerialNumber = piece.SerialNumber,
            Position = piece.Position,
            BuildType = piece.BuildType,
            SpecName = piece.SpecName,
            GlassRef = piece.GlassRef,
            UnitSqm = piece.UnitSqm,
            TotalSqm = piece.UnitSqm * request.Quantity,
            TotalLm = piece.Qty > 0
                ? piece.TotalLm * request.Quantity / piece.Qty
                : piece.TotalLm,
            Treatment = piece.Treatment,
            PredecessorStage = piece.PredecessorStage,
            ChargeTemplateKey = piece.ChargeTemplateKey,
            ChargeMetricQty = piece.ChargeMetricQty,
            ChargeMetricUnit = piece.ChargeMetricUnit,
            PolishSubTypeLabel = piece.PolishSubTypeLabel,
            Notes = $"Replacement for breakage: {reason}",
            IsFoc = request.IsFoc,
            IsCancelled = false,
            ReplacementSequence = nextSeq,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = actorId,
            UpdatedBy = actorId
        };

        var replacement = new ProductionReplacement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReplacementId = replacementId,
            JobOrderId = piece.JobOrderId,
            JobRef = jobRef,
            SourceProformaId = job?.ProformaId.ToString() ?? "",
            SpecGroupNumber = piece.SpecGroupNumber,
            SerialNumber = piece.SerialNumber,
            GlassRef = piece.GlassRef,
            SpecName = piece.SpecName,
            Position = piece.Position,
            Stage = piece.Stage,
            OriginalPieceId = piece.Id,
            ReplacementPieceId = replacementPiece.Id,
            EntryType = request.IsFoc
                ? ProductionEntryTypes.Foc
                : ProductionEntryTypes.Rework,
            Reason = reason,
            Quantity = request.Quantity,
            IsFoc = request.IsFoc,
            ReplacementSequence = nextSeq,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow,
            Status = ReplacementStatuses.Pending
        };

        var (existingRow, createdRow) = await store.InsertReplacementWithPieceAsync(
            tenantId, replacement, replacementPiece, ct);
        if (existingRow is not null)
        {
            return ProductionResult<ReplacementDto>.Ok(ToDto(existingRow),
                "Breakage recorded, replacement already exists (idempotent).");
        }

        // Breakage event on the ORIGINAL work item (WPF breakage entry;
        // the parent-entry link rides the payload — the SaaS event log has
        // no parent id column).
        await production.AppendEventAsync(tenantId, new ProductionEvent
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
            Qty = request.Quantity,
            DoneBy = actorId,
            Reason = reason,
            CreatedAt = DateTime.UtcNow,
            Payload = JsonSerializer.Serialize(new
            {
                replacementId,
                replacementSequence = nextSeq,
                replacementPieceId = replacementPiece.Id,
                isFoc = request.IsFoc,
                notes = request.Notes ?? ""
            })
        }, ct);

        await AuditAsync(tenantId, actorId, "production.replacement_breakage",
            "production_replacements", replacement.Id, null, new
            {
                replacementId,
                originalPieceId = piece.Id,
                replacementPieceId = replacementPiece.Id,
                quantity = request.Quantity,
                reason,
                isFoc = request.IsFoc
            }, ip, ct);

        return ProductionResult<ReplacementDto>.Ok(ToDto(replacement),
            $"Breakage recorded + replacement created: {piece.PieceLabel} → " +
            $"{replacementUniqueKey} ({replacementId}).");
    }

    // ── RecordRework / RecordFoc (WPF verbatim semantics) ────────────

    /// <summary>
    /// POST /replacements/rework — rework event on the work item; the
    /// status flips to Rework unless it is already Completed (WPF
    /// RecordReworkAsync).
    /// </summary>
    public async Task<ProductionResult<ReplacementDto>> RecordReworkAsync(
        Guid tenantId, Guid actorId, ReplacementReworkRequest request,
        string? ip, CancellationToken ct)
    {
        var (ok, message) = await RecordReworkOrFocAsync(
            tenantId, actorId, request.PieceId, request.Quantity,
            request.Reason, request.IsFoc, request.Notes, ip, ct);
        if (!ok)
        {
            return ProductionResult<ReplacementDto>.Fail(
                ProductionErrorCode.Conflict, message);
        }
        return ProductionResult<ReplacementDto>.Ok(null, message);
    }

    /// <summary>Shared engine for rework/FOC (the WPF RecordFocAsync
    /// delegates to RecordReworkAsync with isFoc, then persists the FOC
    /// entry type — the SaaS writes the Foc event type directly, same end
    /// state).</summary>
    private async Task<(bool Ok, string Message)> RecordReworkOrFocAsync(
        Guid tenantId, Guid actorId, Guid pieceId, int quantity,
        string? reason, bool isFoc, string? notes, string? ip,
        CancellationToken ct)
    {
        if (pieceId == Guid.Empty) return (false, "PieceId required.");
        if (quantity <= 0) return (false, "Qty must be >0.");
        string entryType = isFoc ? ProductionEntryTypes.Foc : ProductionEntryTypes.Rework;
        string label = isFoc ? "FOC" : "Rework";

        var piece = await production.FindAsync(tenantId, pieceId, ct);
        if (piece is null) return (false, "Work item not found.");
        if (piece.IsCancelled
            || string.Equals(piece.Status, WorkItemStatuses.Cancelled,
                StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Cancelled work cannot record " + label.ToLowerInvariant() + ".");
        }

        await production.AppendEventAsync(tenantId, new ProductionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductionPieceId = piece.Id,
            EventType = entryType,
            JobOrderId = piece.JobOrderId,
            Stage = piece.Stage,
            SpecGroupNumber = piece.SpecGroupNumber,
            SerialNumber = piece.SerialNumber,
            Position = piece.Position,
            SpecName = piece.SpecName,
            GlassRef = piece.GlassRef,
            Qty = quantity,
            DoneBy = actorId,
            Reason = string.IsNullOrWhiteSpace(reason) ? label : reason.Trim(),
            CreatedAt = DateTime.UtcNow,
            Payload = JsonSerializer.Serialize(new { isFoc, notes = notes ?? "" })
        }, ct);

        // WPF: mark the work item Rework unless already Completed.
        if (!string.Equals(piece.Status, WorkItemStatuses.Completed,
                StringComparison.OrdinalIgnoreCase))
        {
            piece.Status = WorkItemStatuses.Rework;
            piece.UpdatedAt = DateTime.UtcNow;
            piece.UpdatedBy = actorId;
            if (!await production.TryUpdateAsync(piece, ct))
            {
                return (false,
                    "The work item was modified by someone else — reload and retry.");
            }
        }

        await AuditAsync(tenantId, actorId,
            isFoc ? "production.replacement_foc" : "production.replacement_rework",
            "production_pieces", piece.Id, null, new { quantity, reason, isFoc }, ip, ct);

        return (true, $"{label} recorded.");
    }

    /// <summary>GET /replacements?jobOrderId= — WPF replacements by job
    /// (the WPF has no list call; this is the read model for the rows the
    /// breakage flow creates).</summary>
    public async Task<ProductionResult<IReadOnlyList<ReplacementDto>>> ListByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var rows = await store.ListReplacementsByJobAsync(tenantId, jobOrderId, ct);
        return ProductionResult<IReadOnlyList<ReplacementDto>>.Ok(
            rows.Select(ToDto).ToList());
    }

    // ── helpers ──────────────────────────────────────────────────────

    internal static ReplacementDto ToDto(ProductionReplacement r) => new(
        r.Id,
        r.ReplacementId,
        r.JobOrderId,
        r.JobRef,
        r.SourceProformaId,
        r.SpecGroupNumber,
        r.SerialNumber,
        r.GlassRef,
        r.SpecName,
        r.Position,
        r.Stage,
        r.OriginalPieceId,
        r.ReplacementPieceId,
        r.EntryType,
        r.Reason,
        r.Quantity,
        r.IsFoc,
        r.ReplacementSequence,
        r.Status,
        r.CreatedAt);

    private async Task AuditAsync(
        Guid tenantId, Guid? actorUserId, string action, string entityType,
        Guid entityId, object? before, object? after, string? ip, CancellationToken ct)
        => await audit.WriteAsync(tenantId, actorUserId, action, entityType,
            entityId, ip, null,
            before is null ? null : JsonSerializer.Serialize(before),
            after is null ? null : JsonSerializer.Serialize(after),
            ct).ConfigureAwait(false);
}
