using System.Data;
using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.Production;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Infrastructure.Persistence;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 26b storage — the WPF production.db cutting/cost tables:
//    • material_allocations (the allocation ledger; deterministic
//      AllocationId UNIQUE per tenant, consumption cascade)
//    • production_actual_costs (deterministic CostId UNIQUE per tenant)
//    • production_replacements (+ the replacement work item in the same
//      transaction)
//    • cutting_sessions / cutting_sheets / cutting_placed_pieces (the
//      verified layout, one serializable transaction per group)
//  RLS-scoped contexts per tenant; serializable transactions wrap the
//  multi-row flows exactly as the WPF does.
// ═══════════════════════════════════════════════════════════════════════

public sealed class CuttingCostStore(SaasDbContextFactory factory) : ICuttingCostStore
{
    // ── allocations ───────────────────────────────────────────────────

    public async Task<MaterialAllocation?> FindAllocationAsync(
        Guid tenantId, string allocationId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.MaterialAllocations.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AllocationId == allocationId, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MaterialAllocation>> ListAllocationsByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var rows = await db.MaterialAllocations.AsNoTracking()
            .Where(a => a.JobOrderId == jobOrderId)
            .ToListAsync(ct).ConfigureAwait(false);
        return rows
            .OrderBy(a => a.SheetNo)
            .ThenBy(a => a.PlacedPieceLabel, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<MaterialAllocation>> ListAllocationsByReservationAsync(
        Guid tenantId, string reservationKey, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.MaterialAllocations.AsNoTracking()
            .Where(a => a.ReservationKey == reservationKey)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> TryUpdateAllocationAsync(
        MaterialAllocation allocation, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(allocation.TenantId);
        var stored = await db.MaterialAllocations
            .FirstOrDefaultAsync(a => a.Id == allocation.Id, ct).ConfigureAwait(false);
        if (stored is null) return false;
        stored.Status = allocation.Status;
        stored.ConsumedAt = allocation.ConsumedAt;
        stored.SpecGroupNumber = allocation.SpecGroupNumber;
        stored.SerialNumber = allocation.SerialNumber;
        stored.Notes = allocation.Notes;
        stored.Quantity = allocation.Quantity;
        stored.UtilizationPct = allocation.UtilizationPct;
        stored.WastagePct = allocation.WastagePct;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<int> InsertAllocationAsync(
        Guid tenantId, MaterialAllocation allocation, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        db.MaterialAllocations.Add(allocation);
        try
        {
            return await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // UNIQUE race — the caller re-reads by id (WPF race pattern).
            return 0;
        }
    }

    // ── actual costs ─────────────────────────────────────────────────

    public async Task<ActualCost?> FindCostAsync(
        Guid tenantId, string costId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ActualCosts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CostId == costId, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ActualCost>> ListCostsByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var rows = await db.ActualCosts.AsNoTracking()
            .Where(c => c.JobOrderId == jobOrderId)
            .ToListAsync(ct).ConfigureAwait(false);
        return rows
            .OrderBy(c => c.SpecGroupNumber)
            .ThenBy(c => c.SerialNumber)
            .ThenBy(c => ActualCostTypes.WpfCode(c.CostType))
            .ToList();
    }

    public async Task<bool> TryUpdateCostAsync(ActualCost cost, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(cost.TenantId);
        var stored = await db.ActualCosts
            .FirstOrDefaultAsync(c => c.Id == cost.Id, ct).ConfigureAwait(false);
        if (stored is null) return false;
        stored.Quantity = cost.Quantity;
        stored.UnitCost = cost.UnitCost;
        stored.TotalCost = cost.TotalCost;
        stored.SellingValue = cost.SellingValue;
        stored.IsFoc = cost.IsFoc;
        stored.Notes = cost.Notes;
        stored.StockSheetId = cost.StockSheetId;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<int> InsertCostAsync(
        Guid tenantId, ActualCost cost, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        db.ActualCosts.Add(cost);
        try
        {
            return await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            return 0;
        }
    }

    // ── replacements ─────────────────────────────────────────────────

    public async Task<IReadOnlyList<ProductionReplacement>> ListReplacementsByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProductionReplacements.AsNoTracking()
            .Where(r => r.JobOrderId == jobOrderId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProductionReplacement>> ListReplacementsByPieceAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProductionReplacements.AsNoTracking()
            .Where(r => r.OriginalPieceId == pieceId)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<ProductionReplacement?> FindReplacementAsync(
        Guid tenantId, string replacementId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProductionReplacements.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReplacementId == replacementId, ct)
            .ConfigureAwait(false);
    }

    public async Task<(ProductionReplacement? Existing, ProductionReplacement? Created)>
        InsertReplacementWithPieceAsync(
            Guid tenantId, ProductionReplacement replacement,
            ProductionPiece replacementPiece, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var existing = await db.ProductionReplacements.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReplacementId == replacement.ReplacementId, ct)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return (existing, null);
        }

        // The replacement work item is idempotent on its UniqueKey (WPF
        // checks before creating).
        var existingPiece = await db.ProductionPieces.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UniqueKey == replacementPiece.UniqueKey, ct)
            .ConfigureAwait(false);
        if (existingPiece is not null)
        {
            replacement.ReplacementPieceId = existingPiece.Id;
        }
        else
        {
            db.ProductionPieces.Add(replacementPiece);
            replacement.ReplacementPieceId = replacementPiece.Id;
        }
        db.ProductionReplacements.Add(replacement);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return (null, replacement);
    }

    // ── cutting sessions ─────────────────────────────────────────────

    public async Task<CuttingSession?> FindCuttingSessionAsync(
        Guid tenantId, string cuttingSessionId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.CuttingSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.CuttingSessionId == cuttingSessionId, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CuttingSession>> ListCuttingSessionsByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var rows = await db.CuttingSessions.AsNoTracking()
            .Where(s => s.JobOrderId == jobOrderId)
            .ToListAsync(ct).ConfigureAwait(false);
        return rows.OrderByDescending(s => s.CreatedAt).ToList();
    }

    public async Task<bool> TryUpdateCuttingSessionAsync(
        CuttingSession session, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(session.TenantId);
        var stored = await db.CuttingSessions
            .FirstOrDefaultAsync(x => x.Id == session.Id, ct).ConfigureAwait(false);
        if (stored is null) return false;
        stored.Status = session.Status;
        stored.CompletedAt = session.CompletedAt;
        stored.SheetsUsed = session.SheetsUsed;
        stored.TotalPieces = session.TotalPieces;
        stored.UtilizationPct = session.UtilizationPct;
        stored.WastagePct = session.WastagePct;
        stored.IsVerified = session.IsVerified;
        stored.VerificationResult = session.VerificationResult;
        stored.OptimizationSessionId = session.OptimizationSessionId;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<int> InsertCuttingSessionAsync(
        Guid tenantId, CuttingSession session, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var exists = await db.CuttingSessions.AsNoTracking()
            .AnyAsync(s => s.CuttingSessionId == session.CuttingSessionId, ct)
            .ConfigureAwait(false);
        if (exists) return 0;
        db.CuttingSessions.Add(session);
        try
        {
            return await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            return 0;
        }
    }

    // ── cutting sheets + placed pieces ───────────────────────────────

    public async Task<IReadOnlyList<CuttingSheet>> ListCuttingSheetsBySessionAsync(
        Guid tenantId, Guid cuttingSessionId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var rows = await db.CuttingSheets.AsNoTracking()
            .Where(s => s.CuttingSessionId == cuttingSessionId)
            .ToListAsync(ct).ConfigureAwait(false);
        return rows.OrderBy(s => s.SheetNo).ToList();
    }

    public async Task<IReadOnlyList<CuttingPlacedPiece>> ListPlacedPiecesBySessionAsync(
        Guid tenantId, Guid cuttingSessionId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var rows = await db.CuttingPlacedPieces.AsNoTracking()
            .Where(p => p.CuttingSessionId == cuttingSessionId)
            .ToListAsync(ct).ConfigureAwait(false);
        return rows.OrderBy(p => p.CutSequence).ToList();
    }

    public async Task<CuttingPlacedPiece?> FindPlacedPieceAsync(
        Guid tenantId, string placedPieceId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.CuttingPlacedPieces.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlacedPieceId == placedPieceId, ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> TryUpdatePlacedPieceAsync(
        CuttingPlacedPiece piece, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(piece.TenantId);
        var stored = await db.CuttingPlacedPieces
            .FirstOrDefaultAsync(p => p.Id == piece.Id, ct).ConfigureAwait(false);
        if (stored is null) return false;
        stored.Status = piece.Status;
        stored.Notes = piece.Notes;
        stored.AllocationId = piece.AllocationId;
        stored.ReservationKey = piece.ReservationKey;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<int> PersistCuttingLayoutAsync(
        Guid tenantId,
        IReadOnlyList<CuttingSheet> sheets,
        IReadOnlyList<CuttingPlacedPiece> pieces,
        IReadOnlyList<MaterialAllocation> allocations,
        CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var sheetIds = sheets.Select(s => s.CuttingSheetId).ToList();
        var existingSheetIds = (await db.CuttingSheets.AsNoTracking()
            .Where(s => sheetIds.Contains(s.CuttingSheetId))
            .Select(s => s.CuttingSheetId)
            .ToListAsync(ct).ConfigureAwait(false))
            .ToHashSet(StringComparer.Ordinal);

        var placedIds = pieces.Select(p => p.PlacedPieceId).ToList();
        var existingPlacedIds = (await db.CuttingPlacedPieces.AsNoTracking()
            .Where(p => placedIds.Contains(p.PlacedPieceId))
            .Select(p => p.PlacedPieceId)
            .ToListAsync(ct).ConfigureAwait(false))
            .ToHashSet(StringComparer.Ordinal);

        var allocationIds = allocations
            .Where(a => !string.IsNullOrWhiteSpace(a.AllocationId))
            .Select(a => a.AllocationId).ToList();
        var existingAllocationIds = (await db.MaterialAllocations.AsNoTracking()
            .Where(a => allocationIds.Contains(a.AllocationId))
            .Select(a => a.AllocationId)
            .ToListAsync(ct).ConfigureAwait(false))
            .ToHashSet(StringComparer.Ordinal);

        // Idempotent re-runs keep existing rows; the WPF's post-save
        // spec enrichment is applied directly on insert in the SaaS.
        var sheetIdMap = existingSheetIds.Count > 0
            ? (await db.CuttingSheets.AsNoTracking()
                .Where(s => sheetIds.Contains(s.CuttingSheetId))
                .ToDictionaryAsync(s => s.CuttingSheetId, s => s.Id, ct)
                .ConfigureAwait(false))
            : new Dictionary<string, Guid>(StringComparer.Ordinal);

        int written = 0;
        foreach (var sheet in sheets)
        {
            if (existingSheetIds.Contains(sheet.CuttingSheetId))
            {
                // Re-link the pieces to the existing sheet row.
                sheetIdMap[sheet.CuttingSheetId] = sheetIdMap.GetValueOrDefault(
                    sheet.CuttingSheetId, sheet.Id);
                continue;
            }
            db.CuttingSheets.Add(sheet);
            sheetIdMap[sheet.CuttingSheetId] = sheet.Id;
            written++;
        }

        foreach (var piece in pieces)
        {
            if (sheetIdMap.TryGetValue(
                    sheets.FirstOrDefault(s => s.Id == piece.CuttingSheetId)
                        ?.CuttingSheetId ?? "",
                    out var mappedSheetId))
            {
                piece.CuttingSheetId = mappedSheetId;
            }
            if (existingPlacedIds.Contains(piece.PlacedPieceId)) continue;
            db.CuttingPlacedPieces.Add(piece);
            written++;
        }

        foreach (var allocation in allocations)
        {
            if (existingAllocationIds.Contains(allocation.AllocationId)) continue;
            db.MaterialAllocations.Add(allocation);
            written++;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return written;
    }

    // ── reservations + placed pieces the workflow needs ──────────────

    public async Task<MaterialReservation?> FindReservationByKeyAsync(
        Guid tenantId, string reservationKey, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.MaterialReservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReservationKey == reservationKey, ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> TryUpdateReservationAsync(
        MaterialReservation reservation, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(reservation.TenantId);
        var stored = await db.MaterialReservations
            .FirstOrDefaultAsync(r => r.Id == reservation.Id, ct).ConfigureAwait(false);
        if (stored is null) return false;
        stored.Status = reservation.Status;
        stored.UpdatedAt = reservation.UpdatedAt;
        stored.ReleasedAt = reservation.ReleasedAt;
        stored.ReleaseReason = reservation.ReleaseReason;
        stored.OptimizationSessionId = reservation.OptimizationSessionId;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<CuttingPlacedPiece>> ListPlacedPiecesByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.CuttingPlacedPieces.AsNoTracking()
            .Where(p => p.JobOrderId == jobOrderId)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> ListProductionPieceJobIdsAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProductionPieces.AsNoTracking()
            .Where(p => p.JobOrderId != null)
            .Select(p => p.JobOrderId!.Value)
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);
    }

    // ── consistency scanner reads (read-only, whole tenant) ──────────

    public async Task<IReadOnlyList<MaterialReservation>> ListAllReservationsAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.MaterialReservations.AsNoTracking()
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MaterialAllocation>> ListAllAllocationsAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.MaterialAllocations.AsNoTracking()
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GlassSheet>> ListAllGlassSheetsAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.GlassSheets.AsNoTracking()
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<(Guid SheetId, string Action,
        string DocumentReference, int QuantityChanged)>> ListSheetHistoryAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var rows = await db.StockTransactions.AsNoTracking()
            .Where(t => t.ItemType == "sheet" && t.GlassSheetId != null)
            .ToListAsync(ct).ConfigureAwait(false);
        return rows
            .Select(t => (t.GlassSheetId!.Value, t.TransactionType,
                $"{t.ReferenceType} {t.Notes}".Trim(), (int)t.QuantityDelta))
            .ToList();
    }

}
