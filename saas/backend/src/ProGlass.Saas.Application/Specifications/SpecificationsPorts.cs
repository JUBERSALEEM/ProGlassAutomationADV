using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Application.Specifications;

/// <summary>
/// Persistence port for the specifications registry (schema 0003 + 0008:
/// specifications, specification_usages). MECHANICAL ONLY — the WPF
/// policies (dedup by GlassType+SpecName, preserve-on-blank catalogue
/// refresh, ledger-derived counts) live in SpecificationsService. Every
/// method takes the tenant id EXPLICITLY; implementations own
/// tenant-scoped access (query filter + RLS both fail closed).
/// </summary>
public interface ISpecificationsStore
{
    /// <summary>Natural key (WPF SaveOrUpdateAsync dedup).</summary>
    Task<Specification?> FindByKeyAsync(
        Guid tenantId, string glassType, string specName, CancellationToken ct);

    Task<Specification?> FindByIdAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task InsertAsync(Specification spec, CancellationToken ct);

    /// <summary>Tracked full-field copy + audit stamps, one SaveChanges.</summary>
    Task UpdateAsync(Specification spec, Guid actorId, CancellationToken ct);

    Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>WPF GetAllAsync smart sort: most-used first, then most
    /// recent. Optional exact glass-type filter.</summary>
    Task<IReadOnlyList<Specification>> ListAsync(
        Guid tenantId, string? glassType, CancellationToken ct);

    Task<IReadOnlyList<SpecificationUsage>> GetUsageHistoryAsync(
        Guid tenantId, Guid specId, CancellationToken ct);

    Task<IReadOnlyList<SpecificationUsage>> GetRecentUsageAsync(
        Guid tenantId, int limit, CancellationToken ct);

    // Both usage reads load the Proforma nav (the invoice number is part
    // of the read model).

    /// <summary>
    /// THE LEDGER (SpecificationLedgerService ground truth, translated to
    /// set-based SQL over the normalized schema): one pass, one
    /// transaction —
    ///   1. PURGE usage rows whose (spec, proforma) pair no longer exists
    ///      in spec_groups (orphans from deleted/edited invoices);
    ///   2. INSERT the missing verified uses (one row per distinct
    ///      (spec_id, proforma_id) — "2 PIs × 2 specs = 4 uses");
    ///   3. HEAL the catalogue columns: usage_count = verified count,
    ///      last_used_at = most recent use, last_proforma_no = the
    ///      invoice of that use. PricePerSqm is NEVER touched (estimator-
    ///      owned; per-invoice quoted rates live on the usage rows).
    /// Returns (kept rows, purged count, healed specs).
    /// </summary>
    Task<(int UsageRows, int Purged, int Healed)> ReconcileUsageAsync(
        Guid tenantId, CancellationToken ct);
}
