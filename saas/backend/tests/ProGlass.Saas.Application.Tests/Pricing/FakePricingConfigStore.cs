using ProGlass.Saas.Application.Pricing;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Application.Tests.Pricing;

/// <summary>
/// In-memory IPricingConfigStore: one (pricing, vat) pair per tenant,
/// mirroring the UNIQUE(tenant_id) schema constraint and the store's
/// insert-or-update semantics (both rows always move together).
/// </summary>
public sealed class FakePricingConfigStore : IPricingConfigStore
{
    private readonly Dictionary<Guid, (TenantPricingConfig Pricing, TenantVatConfig Vat)> _rows = new();

    public IReadOnlyList<TenantPricingConfig> SavedPricing =>
        _rows.Values.Select(r => r.Pricing).ToList();

    public int UpsertCalls { get; private set; }

    public Task<(TenantPricingConfig? Pricing, TenantVatConfig? Vat)> GetAsync(
        Guid tenantId, CancellationToken ct)
    {
        _rows.TryGetValue(tenantId, out var row);
        return Task.FromResult<(TenantPricingConfig?, TenantVatConfig?)>(
            row == default ? (null, null) : (row.Pricing, row.Vat));
    }

    public Task UpsertAsync(
        TenantPricingConfig pricing, TenantVatConfig vat, Guid actorId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pricing);
        ArgumentNullException.ThrowIfNull(vat);

        UpsertCalls++;

        var now = DateTime.UtcNow;
        if (_rows.TryGetValue(pricing.TenantId, out var existing))
        {
            // Update in place — id/created audit trail survive, values
            // and updated stamps move (as the EF store does).
            existing.Pricing.YieldFactorDefault = pricing.YieldFactorDefault;
            existing.Pricing.MarginDefault = pricing.MarginDefault;
            existing.Pricing.ThicknessOptions = pricing.ThicknessOptions;
            existing.Pricing.TreatmentOptions = pricing.TreatmentOptions;
            existing.Pricing.AspWidths = pricing.AspWidths;
            existing.Pricing.InterlayerTypes = pricing.InterlayerTypes;
            existing.Pricing.InterlayerThicknesses = pricing.InterlayerThicknesses;
            existing.Pricing.PolishOptions = pricing.PolishOptions;
            existing.Pricing.PanelExtraOptions = pricing.PanelExtraOptions;
            existing.Pricing.UpdatedAt = now;
            existing.Pricing.UpdatedBy = actorId;

            existing.Vat.VatRate = vat.VatRate;
            existing.Vat.Currency = vat.Currency;
            existing.Vat.UpdatedAt = now;
            existing.Vat.UpdatedBy = actorId;
        }
        else
        {
            pricing.Id = pricing.Id == Guid.Empty ? Guid.NewGuid() : pricing.Id;
            pricing.CreatedAt = now;
            pricing.CreatedBy = actorId;
            pricing.UpdatedAt = now;
            pricing.UpdatedBy = actorId;

            vat.Id = vat.Id == Guid.Empty ? Guid.NewGuid() : vat.Id;
            vat.CreatedAt = now;
            vat.CreatedBy = actorId;
            vat.UpdatedAt = now;
            vat.UpdatedBy = actorId;

            _rows[pricing.TenantId] = (pricing, vat);
        }

        return Task.CompletedTask;
    }
}
