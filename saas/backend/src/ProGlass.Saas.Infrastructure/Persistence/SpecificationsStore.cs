using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.Specifications;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Infrastructure.Persistence;

/// <summary>
/// ISpecificationsStore implementation over short-lived tenant-bound
/// contexts (ISaasDbContextFactory — same pattern as the other stores).
/// MECHANICAL ONLY: the apply/ledger POLICY lives in
/// SpecificationsService. The reconcile is set-based SQL in ONE
/// transaction — the WPF ledger walked saved JSON snapshots with a
/// debounce; the normalized schema makes the same ground truth a
/// three-statement pass.
/// </summary>
public sealed class SpecificationsStore : ISpecificationsStore
{
    private readonly ISaasDbContextFactory _factory;

    public SpecificationsStore(ISaasDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    // ── library ────────────────────────────────────────────────────

    public async Task<Specification?> FindByKeyAsync(
        Guid tenantId, string glassType, string specName, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.Specifications.AsNoTracking()
            .FirstOrDefaultAsync(s => s.GlassType == glassType && s.SpecName == specName, ct)
            .ConfigureAwait(false);
    }

    public async Task<Specification?> FindByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.Specifications.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task InsertAsync(Specification spec, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(spec.TenantId);
        db.Specifications.Add(spec);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(
        Specification spec, Guid actorId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(spec.TenantId);
        var tracked = await db.Specifications
            .FirstAsync(s => s.Id == spec.Id, ct)
            .ConfigureAwait(false);

        // LIBRARY fields only — UsageCount / LastUsedAt / LastProformaNo
        // are ledger-owned (ReconcileUsageAsync), never APPLY-owned.
        tracked.SpecName = spec.SpecName;
        tracked.GlassType = spec.GlassType;
        tracked.RatePerSqm = spec.RatePerSqm;
        tracked.CostBasisPerSqm = spec.CostBasisPerSqm;
        tracked.ParamsJson = spec.ParamsJson;
        tracked.Category = spec.Category;
        tracked.Color = spec.Color;
        tracked.ThicknessMm = spec.ThicknessMm;
        tracked.SheetWidthMm = spec.SheetWidthMm;
        tracked.SheetHeightMm = spec.SheetHeightMm;
        tracked.SheetId = spec.SheetId;
        tracked.PanesJson = spec.PanesJson;
        tracked.IsActive = spec.IsActive;
        tracked.UpdatedAt = DateTime.UtcNow;
        tracked.UpdatedBy = actorId;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        // Usage rows die with the spec (FK ON DELETE CASCADE).
        using var db = _factory.CreateForTenant(tenantId);
        return await db.Specifications
            .Where(s => s.Id == id)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false) > 0;
    }

    public async Task<IReadOnlyList<Specification>> ListAsync(
        Guid tenantId, string? glassType, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        var query = db.Specifications.AsNoTracking().AsQueryable();
        if (glassType is not null)
        {
            query = query.Where(s => s.GlassType == glassType);
        }

        // WPF GetAllAsync smart sort: most-used first, then most recent.
        return await query
            .OrderByDescending(s => s.UsageCount)
            .ThenByDescending(s => s.LastUsedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    // ── usage trail ────────────────────────────────────────────────

    public async Task<IReadOnlyList<SpecificationUsage>> GetUsageHistoryAsync(
        Guid tenantId, Guid specId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.SpecificationUsages.AsNoTracking()
            .Include(u => u.Proforma)
            .Where(u => u.SpecId == specId)
            .OrderByDescending(u => u.UsedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SpecificationUsage>> GetRecentUsageAsync(
        Guid tenantId, int limit, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.SpecificationUsages.AsNoTracking()
            .Include(u => u.Proforma)
            .OrderByDescending(u => u.UsedAt)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    // ── the ledger (set-based, one transaction) ────────────────────

    public async Task<(int UsageRows, int Purged, int Healed)> ReconcileUsageAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        await using var transaction = await db.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);

        // 1. PURGE: usage rows whose (spec, invoice) pair no longer exists
        //    among the invoice's spec_groups. Derived data: re-derivable
        //    at any time, so deleting them loses no fact — it removes
        //    fiction (WPF ledger: same purge, walked from snapshots).
        var purged = await db.Database.ExecuteSqlAsync($"""
            DELETE FROM saas.specification_usages u
            WHERE u.tenant_id = {tenantId}
              AND NOT EXISTS (
                SELECT 1 FROM saas.spec_groups g
                WHERE g.spec_id = u.spec_id
                  AND g.proforma_id = u.proforma_id)
            """, ct).ConfigureAwait(false);

        // 2. INSERT the missing verified uses: one row per distinct
        //    (spec_id, proforma_id) pair carried by a saved invoice's
        //    Step 5 groups. Names come from the saved invoice row —
        //    authoritative, not from the draft-time context Step 4 saw.
        //    used_at = the invoice's updated_at (when the use was saved).
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO saas.specification_usages
                (tenant_id, spec_id, proforma_id, spec_name, glass_type,
                 company_name, customer_name, project_name,
                 price_per_sqm, sheet_width_mm, sheet_height_mm, used_at)
            SELECT DISTINCT
                g.tenant_id, g.spec_id, g.proforma_id,
                g.spec_name, g.glass_type,
                COALESCE(c.name, ''), COALESCE(cl.customer_name, ''),
                COALESCE(p.identifier_name, ''),
                g.rate_per_sqm,
                s.sheet_width_mm, s.sheet_height_mm,
                pi.updated_at
            FROM saas.spec_groups g
            JOIN saas.proforma_invoices pi ON pi.id = g.proforma_id
            LEFT JOIN saas.companies c ON c.id = pi.company_id
            LEFT JOIN saas.clients cl ON cl.id = pi.client_id
            LEFT JOIN saas.projects p ON p.id = pi.project_id
            JOIN saas.specifications s ON s.id = g.spec_id
            WHERE g.tenant_id = {tenantId}
              AND g.spec_id IS NOT NULL
              AND NOT EXISTS (
                SELECT 1 FROM saas.specification_usages u
                WHERE u.spec_id = g.spec_id AND u.proforma_id = g.proforma_id)
            """, ct).ConfigureAwait(false);

        // 3a. HEAL specs WITH verified uses: count, most recent use, and
        //     the invoice of that use. rate_per_sqm is NEVER touched
        //     (estimator-owned; per-invoice quoted rates live on the usage
        //     rows, which is what makes the price bands truthful).
        //     DISTINCT ON carries the LATEST use's invoice per spec.
        await db.Database.ExecuteSqlAsync($"""
            UPDATE saas.specifications s
            SET usage_count = agg.cnt,
                last_used_at = agg.last_used,
                last_proforma_no = agg.last_proforma_no
            FROM (
                SELECT DISTINCT ON (u.spec_id)
                    u.spec_id,
                    COUNT(*) OVER (PARTITION BY u.spec_id) AS cnt,
                    MAX(u.used_at) OVER (PARTITION BY u.spec_id) AS last_used,
                    pi.proforma_no AS last_proforma_no
                FROM saas.specification_usages u
                JOIN saas.proforma_invoices pi ON pi.id = u.proforma_id
                WHERE u.tenant_id = {tenantId}
                ORDER BY u.spec_id, u.used_at DESC
            ) AS agg
            WHERE s.id = agg.spec_id
            """, ct).ConfigureAwait(false);

        // 3b. Specs that lost their last verified use go back to zero
        //     (an APPLY-pressed draft that was never saved, or an edited
        //     invoice that dropped the group — fiction, removed).
        await db.Database.ExecuteSqlAsync($"""
            UPDATE saas.specifications s
            SET usage_count = 0
            WHERE s.tenant_id = {tenantId}
              AND s.usage_count <> 0
              AND NOT EXISTS (
                SELECT 1 FROM saas.specification_usages u
                WHERE u.spec_id = s.id)
            """, ct).ConfigureAwait(false);

        await transaction.CommitAsync(ct).ConfigureAwait(false);

        // Kept rows + how many catalogue rows were healed (counts moved).
        var usageRows = await db.SpecificationUsages.AsNoTracking()
            .CountAsync(u => u.TenantId == tenantId, ct).ConfigureAwait(false);
        // "Healed" = catalogue rows now carrying verified uses.
        var healed = await db.Specifications.AsNoTracking()
            .CountAsync(s => s.TenantId == tenantId && s.UsageCount > 0, ct)
            .ConfigureAwait(false);

        return (usageRows, purged, healed);
    }
}
