using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.Production;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Infrastructure.Persistence;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 9c storage — the WPF MaterialReservationService persistence
//  port: deterministic-key idempotency (partial UNIQUE index), the
//  availability sum over ACTIVE holds, RLS-scoped contexts.
// ═══════════════════════════════════════════════════════════════════════

public sealed class ReservationStore(SaasDbContextFactory factory) : IReservationStore
{
    public async Task<JobOrder?> FindJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.JobOrders.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobOrderId, ct).ConfigureAwait(false);
    }

    public async Task<MaterialReservation?> FindAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.MaterialReservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<MaterialReservation?> FindByKeyAsync(
        Guid tenantId, string reservationKey, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.MaterialReservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReservationKey == reservationKey, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MaterialReservation>> ListByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.MaterialReservations.AsNoTracking()
            .Where(r => r.JobOrderId == jobOrderId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<int?> GetSheetBalanceAsync(
        Guid tenantId, Guid glassSheetId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.GlassSheets.AsNoTracking()
            .Where(g => g.Id == glassSheetId)
            .Select(g => (int?)(g.Stock - g.Used))
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<GlassSheet?> FindSheetAsync(
        Guid tenantId, Guid glassSheetId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.GlassSheets.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == glassSheetId, ct).ConfigureAwait(false);
    }

    public async Task<int> SumActiveHoldsAsync(
        Guid tenantId, Guid glassSheetId, string? excludeKey, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var query = db.MaterialReservations.AsNoTracking()
            .Where(r => r.GlassSheetId == glassSheetId
                        && (r.Status == ReservationStatuses.Reserved
                            || r.Status == ReservationStatuses.Allocated));
        if (!string.IsNullOrEmpty(excludeKey))
        {
            query = query.Where(r => r.ReservationKey != excludeKey);
        }

        return await query.SumAsync(r => (int?)r.ReservedQty, ct) ?? 0;
    }

    public async Task<bool> TryInsertAsync(
        MaterialReservation reservation, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(reservation.TenantId);
        db.MaterialReservations.Add(reservation);
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return false;
        }
    }

    public async Task<bool> TryUpdateAsync(
        MaterialReservation reservation, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(reservation.TenantId);
        var stored = await db.MaterialReservations
            .FirstOrDefaultAsync(r => r.Id == reservation.Id, ct)
            .ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        stored.Status = reservation.Status;
        stored.Quantity = reservation.Quantity;
        stored.Notes = reservation.Notes;
        stored.ReleasedAt = reservation.ReleasedAt;
        stored.ReleaseReason = reservation.ReleaseReason;
        stored.UpdatedAt = reservation.UpdatedAt;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        reservation.UpdatedAt = stored.UpdatedAt;
        return true;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
        || ex.InnerException?.Message.Contains(
            "duplicate key value violates unique constraint",
            StringComparison.OrdinalIgnoreCase) == true;
}
