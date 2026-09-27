using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Application.Pricing;

/// <summary>
/// Persistence port for the per-tenant pricing/VAT configuration (schema
/// 0003 tenant_pricing_config + tenant_vat_config, one row each per
/// tenant — UNIQUE(tenant_id)). Mechanical only: validation and the
/// WPF-default fallback live in PricingService. Like the other stores,
/// every method takes the tenant id EXPLICITLY; implementations own
/// tenant-scoped access (query filter + RLS both fail closed).
/// </summary>
public interface IPricingConfigStore
{
    /// <summary>The tenant's rows, or nulls when none exist yet (a fresh
    /// tenant has no row until the first PUT).</summary>
    Task<(TenantPricingConfig? Pricing, TenantVatConfig? Vat)> GetAsync(
        Guid tenantId, CancellationToken ct);

    /// <summary>
    /// Insert-or-update both rows in ONE SaveChanges (the MasterDataStore
    /// atomicity rule: no crash window between the two writes). The
    /// entities arrive fully valued; the store stamps ids/tenant/audit
    /// columns.
    /// </summary>
    Task UpsertAsync(
        TenantPricingConfig pricing, TenantVatConfig vat, Guid actorId, CancellationToken ct);
}
