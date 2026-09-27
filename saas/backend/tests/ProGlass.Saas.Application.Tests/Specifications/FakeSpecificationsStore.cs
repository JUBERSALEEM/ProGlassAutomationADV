using ProGlass.Saas.Application.Specifications;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Application.Tests.Specifications;

/// <summary>
/// In-memory ISpecificationsStore mirroring the schema contracts: the
/// (tenant, glass_type, spec_name) natural key, smart-sort lists, and
/// cascade deletes. The ledger reconcile is a no-op here — the real
/// set-based derivation is covered by the integration tests against
/// live PostgreSQL.
/// </summary>
public sealed class FakeSpecificationsStore : ISpecificationsStore
{
    private readonly List<Specification> _specs = new();
    private readonly List<SpecificationUsage> _usages = new();

    public IReadOnlyList<Specification> Specs => _specs;

    public int ReconcileCalls { get; private set; }

    public Task<Specification?> FindByKeyAsync(
        Guid tenantId, string glassType, string specName, CancellationToken ct) =>
        Task.FromResult(_specs.FirstOrDefault(s =>
            s.TenantId == tenantId && s.GlassType == glassType && s.SpecName == specName));

    public Task<Specification?> FindByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct) =>
        Task.FromResult(_specs.FirstOrDefault(s => s.TenantId == tenantId && s.Id == id));

    public Task InsertAsync(Specification spec, CancellationToken ct)
    {
        _specs.Add(spec);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Specification spec, Guid actorId, CancellationToken ct)
    {
        var stored = _specs.First(s => s.Id == spec.Id);
        stored.SpecName = spec.SpecName;
        stored.GlassType = spec.GlassType;
        stored.RatePerSqm = spec.RatePerSqm;
        stored.CostBasisPerSqm = spec.CostBasisPerSqm;
        stored.ParamsJson = spec.ParamsJson;
        stored.Category = spec.Category;
        stored.Color = spec.Color;
        stored.ThicknessMm = spec.ThicknessMm;
        stored.SheetWidthMm = spec.SheetWidthMm;
        stored.SheetHeightMm = spec.SheetHeightMm;
        stored.SheetId = spec.SheetId;
        stored.PanesJson = spec.PanesJson;
        stored.IsActive = spec.IsActive;
        stored.UpdatedAt = DateTime.UtcNow;
        stored.UpdatedBy = actorId;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var spec = _specs.FirstOrDefault(s => s.TenantId == tenantId && s.Id == id);
        if (spec is null)
        {
            return Task.FromResult(false);
        }

        _specs.Remove(spec);
        _usages.RemoveAll(u => u.SpecId == id); // FK cascade
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<Specification>> ListAsync(
        Guid tenantId, string? glassType, CancellationToken ct)
    {
        var query = _specs.Where(s => s.TenantId == tenantId);
        if (glassType is not null)
        {
            query = query.Where(s => s.GlassType == glassType);
        }

        IReadOnlyList<Specification> sorted = query
            .OrderByDescending(s => s.UsageCount)
            .ThenByDescending(s => s.LastUsedAt)
            .ToList();
        return Task.FromResult(sorted);
    }

    public Task<IReadOnlyList<SpecificationUsage>> GetUsageHistoryAsync(
        Guid tenantId, Guid specId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SpecificationUsage>>(
            _usages.Where(u => u.TenantId == tenantId && u.SpecId == specId)
                .OrderByDescending(u => u.UsedAt)
                .ToList());

    public Task<IReadOnlyList<SpecificationUsage>> GetRecentUsageAsync(
        Guid tenantId, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SpecificationUsage>>(
            _usages.Where(u => u.TenantId == tenantId)
                .OrderByDescending(u => u.UsedAt)
                .Take(limit)
                .ToList());

    public Task<(int UsageRows, int Purged, int Healed)> ReconcileUsageAsync(
        Guid tenantId, CancellationToken ct)
    {
        ReconcileCalls++;
        return Task.FromResult((_usages.Count, 0, 0));
    }
}
