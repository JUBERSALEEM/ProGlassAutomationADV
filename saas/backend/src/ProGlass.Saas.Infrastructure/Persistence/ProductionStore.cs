using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.Production;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Infrastructure.Persistence;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 9b storage — the WPF production.db port:
//    • unique_key preservation (a re-release inserts only NEW items —
//      statuses survive, WPF IX_ProductionWorkItem_UniqueKey)
//    • the immutable event log (SavedProductionEntry)
//    • RLS-scoped contexts per tenant.
//  The grid + charge reads feed the pure engine with normalized inputs.
// ═══════════════════════════════════════════════════════════════════════

public sealed class ProductionStore(SaasDbContextFactory factory) : IProductionStore
{
    public async Task<JobOrder?> FindJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.JobOrders.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobOrderId, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProductionSpecInput>> GetSpecInputsAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var groups = await db.SpecGroups.AsNoTracking()
            .Where(g => g.ProformaId == proformaId)
            .OrderBy(g => g.GroupNumber)
            .ToListAsync(ct).ConfigureAwait(false);
        if (groups.Count == 0)
        {
            return Array.Empty<ProductionSpecInput>();
        }

        var groupIds = groups.Select(g => g.Id).ToList();
        var items = await db.SpecGroupItems.AsNoTracking()
            .Where(i => groupIds.Contains(i.SpecGroupId))
            .OrderBy(i => i.RowSerial)
            .ToListAsync(ct).ConfigureAwait(false);

        return groups
            .Select(g => new ProductionSpecInput(
                g.GroupNumber,
                g.SpecName,
                g.GlassType,
                items.Where(i => i.SpecGroupId == g.Id)
                    .Select(r => new ProductionRowInput(
                        r.RowSerial, r.Position ?? "",
                        r.W1, r.H1, r.W2, r.H2, r.Qty,
                        r.PerimeterFactor > 0 ? r.PerimeterFactor : 2))
                    .ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<ProductionChargeInput>> GetChargeInputsAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var charges = await db.OtherCharges.AsNoTracking()
            .Where(c => c.ProformaId == proformaId && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct).ConfigureAwait(false);

        return charges
            .Select(c => new ProductionChargeInput(
                c.TemplateKey ?? "",
                c.MetricQty,
                c.PolishSubType ?? "",
                c.CustomPolishDescription ?? "",
                ParseSpecTargets(c.CheckedSpecGroupNumbers)))
            .ToList();
    }

    /// <summary>The checked-spec list is stored as CSV (the Step 6
    /// contract); empty = all specs.</summary>
    private static IReadOnlyList<int> ParseSpecTargets(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Array.Empty<int>();
        }

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)
            .Select(t => int.TryParse(t, out var n) ? n : 0)
            .Where(n => n > 0)
            .Distinct()
            .ToList();
    }

    public async Task<ProductionPiece?> FindAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProductionPieces.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProductionPiece>> ListByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProductionPieces.AsNoTracking()
            .Where(p => p.JobOrderId == jobOrderId)
            .OrderBy(p => p.SpecGroupNumber)
            .ThenBy(p => p.SerialNumber)
            .ThenBy(p => ProductionStages.Code(p.Stage))
            .ThenBy(p => p.Position)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<MasterData.PagedResult<ProductionPiece>> SearchAsync(
        Guid tenantId, Guid? jobOrderId, string? stage, string? status,
        bool includeCancelled, int page, int pageSize, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var query = db.ProductionPieces.AsNoTracking().AsQueryable();

        if (!includeCancelled)
        {
            query = query.Where(p => !p.IsCancelled);
        }

        if (jobOrderId.HasValue)
        {
            query = query.Where(p => p.JobOrderId == jobOrderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(p => p.Stage == stage.Trim());
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => p.Status == status.Trim());
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);
        return new MasterData.PagedResult<ProductionPiece>(
            items, page, pageSize, total);
    }

    public async Task<HashSet<string>> GetExistingKeysAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var keys = await db.ProductionPieces.AsNoTracking()
            .Where(p => p.JobOrderId == jobOrderId && p.UniqueKey != "")
            .Select(p => p.UniqueKey)
            .ToListAsync(ct).ConfigureAwait(false);
        return keys.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<int> InsertNewAsync(
        Guid tenantId, Guid jobOrderId, Guid actorId,
        IReadOnlyList<ProductionWorkItemPlan> items, CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return 0;
        }

        using var db = factory.CreateForTenant(tenantId);
        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            db.ProductionPieces.Add(new ProductionPiece
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JobOrderId = jobOrderId,
                UniqueKey = item.UniqueKey,
                SpecGroupNumber = item.SpecGroupNumber,
                SerialNumber = item.SerialNumber,
                Position = item.Position,
                BuildType = item.BuildType,
                SpecName = item.SpecName,
                GlassRef = item.GlassRef,
                Stage = item.Stage,
                Status = item.Status,
                WidthMm = item.WidthMm,
                HeightMm = item.HeightMm,
                Qty = item.Qty,
                QtyDone = 0,
                UnitSqm = item.UnitSqm,
                TotalSqm = item.TotalSqm,
                TotalLm = item.TotalLm,
                Treatment = item.Treatment,
                PredecessorStage = item.PredecessorStage,
                ChargeTemplateKey = item.ChargeTemplateKey,
                ChargeMetricQty = item.ChargeMetricQty,
                ChargeMetricUnit = item.ChargeMetricUnit,
                PolishSubTypeLabel = item.PolishSubTypeLabel,
                IsFoc = false,
                IsCancelled = false,
                CancelReason = "",
                Notes = item.Notes,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId
            });
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return items.Count;
    }

    public async Task<bool> TryUpdateAsync(
        ProductionPiece piece, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(piece.TenantId);
        var stored = await db.ProductionPieces
            .FirstOrDefaultAsync(p => p.Id == piece.Id, ct).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        stored.Status = piece.Status;
        stored.QtyDone = piece.QtyDone;
        stored.CompletedAt = piece.CompletedAt;   // Phase 26a (WPF WorkItem)
        stored.CompletedBy = piece.CompletedBy;
        stored.ReplacementSequence = piece.ReplacementSequence;
        stored.IsFoc = piece.IsFoc;
        stored.IsCancelled = piece.IsCancelled;
        stored.CancelledAt = piece.CancelledAt;
        stored.CancelledBy = piece.CancelledBy;
        stored.CancelReason = piece.CancelReason;
        stored.Notes = piece.Notes;
        stored.UpdatedAt = piece.UpdatedAt;
        stored.UpdatedBy = piece.UpdatedBy;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        piece.UpdatedAt = stored.UpdatedAt;
        return true;
    }

    public async Task AppendEventAsync(
        Guid tenantId, ProductionEvent entry, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        db.ProductionEvents.Add(entry);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProductionEvent>> ListEventsAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProductionEvents.AsNoTracking()
            .Where(e => e.ProductionPieceId == pieceId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProductionEvent>> ListQcEventsAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProductionEvents.AsNoTracking()
            .Where(e => e.ProductionPieceId == pieceId
                        && (e.EventType == ProductionEntryTypes.QcPassed
                            || e.EventType == ProductionEntryTypes.QcFailed
                            || e.EventType == ProductionEntryTypes.Rework))
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }
}
