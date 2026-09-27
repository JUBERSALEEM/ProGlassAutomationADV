using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.Pricing;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Infrastructure.Persistence;

/// <summary>
/// IPricingConfigStore implementation over short-lived tenant-bound
/// contexts (ISaasDbContextFactory — same pattern as the other stores).
/// MECHANICAL ONLY: validation and the WPF-default fallback live in
/// PricingService. Reads are tenant-scoped twice (EF query filter + RLS
/// GUC). The upsert writes BOTH rows in one SaveChanges so a crash can
/// never leave pricing saved without its VAT half.
/// </summary>
public sealed class PricingConfigStore : IPricingConfigStore
{
    private readonly ISaasDbContextFactory _factory;

    public PricingConfigStore(ISaasDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<(TenantPricingConfig? Pricing, TenantVatConfig? Vat)> GetAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);

        // No tracking: pure read-back (the Phase 5 lesson — a tracked
        // entity would go stale if the same request wrote first).
        var pricing = await db.TenantPricingConfigs.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        var vat = await db.TenantVatConfigs.AsNoTracking()
            .FirstOrDefaultAsync(v => v.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        return (pricing, vat);
    }

    public async Task UpsertAsync(
        TenantPricingConfig pricing, TenantVatConfig vat, Guid actorId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pricing);
        ArgumentNullException.ThrowIfNull(vat);

        using var db = _factory.CreateForTenant(pricing.TenantId);
        await using var transaction = await db.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);

        var now = DateTime.UtcNow;

        // Pricing row: update in place when it exists, else insert.
        var existingPricing = await db.TenantPricingConfigs
            .FirstOrDefaultAsync(p => p.TenantId == pricing.TenantId, ct)
            .ConfigureAwait(false);

        if (existingPricing is null)
        {
            pricing.Id = pricing.Id == Guid.Empty ? Guid.NewGuid() : pricing.Id;
            pricing.CreatedAt = now;
            pricing.CreatedBy = actorId;
            pricing.UpdatedAt = now;
            pricing.UpdatedBy = actorId;
            db.TenantPricingConfigs.Add(pricing);
        }
        else
        {
            existingPricing.YieldFactorDefault = pricing.YieldFactorDefault;
            existingPricing.MarginDefault = pricing.MarginDefault;
            existingPricing.ThicknessOptions = pricing.ThicknessOptions;
            existingPricing.TreatmentOptions = pricing.TreatmentOptions;
            existingPricing.AspWidths = pricing.AspWidths;
            existingPricing.InterlayerTypes = pricing.InterlayerTypes;
            existingPricing.InterlayerThicknesses = pricing.InterlayerThicknesses;
            existingPricing.PolishOptions = pricing.PolishOptions;
            existingPricing.PanelExtraOptions = pricing.PanelExtraOptions;
            existingPricing.UpdatedAt = now;
            existingPricing.UpdatedBy = actorId;
        }

        // VAT row: same treatment (UNIQUE(tenant_id) — one row per tenant).
        var existingVat = await db.TenantVatConfigs
            .FirstOrDefaultAsync(v => v.TenantId == vat.TenantId, ct)
            .ConfigureAwait(false);

        if (existingVat is null)
        {
            vat.Id = vat.Id == Guid.Empty ? Guid.NewGuid() : vat.Id;
            vat.CreatedAt = now;
            vat.CreatedBy = actorId;
            vat.UpdatedAt = now;
            vat.UpdatedBy = actorId;
            db.TenantVatConfigs.Add(vat);
        }
        else
        {
            existingVat.VatRate = vat.VatRate;
            existingVat.Currency = vat.Currency;
            existingVat.UpdatedAt = now;
            existingVat.UpdatedBy = actorId;
        }

        // ONE SaveChanges for both rows — the MasterDataStore atomicity
        // rule (no crash window between pricing and VAT writes).
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }
}
