using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.MasterData;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Infrastructure.Persistence;

/// <summary>
/// IMasterDataStore implementation over short-lived tenant-bound contexts
/// (ISaasDbContextFactory — same pattern as the auth stores). MECHANICAL
/// ONLY: lookup keys, ordering (usage DESC, last_used DESC — WPF list
/// order) and paging live here as query mechanics; the upsert POLICY is
/// MasterDataService. Every read is tenant-scoped twice (EF query filter
/// + RLS GUC); case-insensitive matching uses LOWER() on both sides,
/// exactly like the WPF SQLite queries.
/// </summary>
public sealed class MasterDataStore : IMasterDataStore
{
    private readonly ISaasDbContextFactory _factory;

    public MasterDataStore(ISaasDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    // ══════════════════════════════ COMPANIES ══════════════════════

    public async Task<Company?> FindCompanyByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<Company?> FindCompanyByKeyAsync(
        Guid tenantId, string name, string trn, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower()
                                      && c.Trn.ToLower() == trn.ToLower(), ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> IsCompanyKeyTakenAsync(
        Guid tenantId, string name, string trn, Guid exceptCompanyId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.Companies.AsNoTracking()
            .AnyAsync(c => c.Id != exceptCompanyId
                           && c.Name.ToLower() == name.ToLower()
                           && c.Trn.ToLower() == trn.ToLower(), ct)
            .ConfigureAwait(false);
    }

    public async Task InsertCompanyAsync(
        Company company, bool clearOtherDefaults, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(company.TenantId);
        await using var transaction = await db.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);
        db.Companies.Add(company);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        if (clearOtherDefaults)
            await ClearOtherDefaults(db, company.Id, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateCompanyAsync(
        Company company, bool clearOtherDefaults, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(company.TenantId);
        await using var transaction = await db.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);
        db.Companies.Update(company); // detached update — full-row UPDATE
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        if (clearOtherDefaults)
            await ClearOtherDefaults(db, company.Id, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Single-default rule (WPF Company Center): every other
    /// default flag dies in the same transaction as the save.</summary>
    private static async Task ClearOtherDefaults(
        SaasDbContext db, Guid keepCompanyId, CancellationToken ct)
    {
        await db.Companies
            .Where(c => c.IsDefault && c.Id != keepCompanyId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.IsDefault, false), ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> DeleteCompanyAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.Companies
            .Where(c => c.Id == id)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false) > 0;
    }

    public async Task<(IReadOnlyList<Company> Items, int TotalCount)> ListCompaniesAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        var query = db.Companies.AsNoTracking().OrderByUsage();
        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);
        return (items, total);
    }

    public async Task<IReadOnlyList<Company>> SearchCompaniesAsync(
        Guid tenantId, string query, int limit, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        var q = query.ToLower();
        return await db.Companies.AsNoTracking()
            .Where(c => c.Name.ToLower().Contains(q))
            .OrderByDescending(c => c.UsageCount)
            .Take(limit)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    // ══════════════════════════════ CLIENTS ════════════════════════

    public async Task<Client?> FindClientByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<Client?> FindClientByKeyAsync(
        Guid tenantId, string customerName, string customerTrn, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerName.ToLower() == customerName.ToLower()
                                      && c.CustomerTrn.ToLower() == customerTrn.ToLower(), ct)
            .ConfigureAwait(false);
    }

    public async Task InsertClientAsync(Client client, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(client.TenantId);
        db.Clients.Add(client);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateClientAsync(Client client, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(client.TenantId);
        db.Clients.Update(client);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> DeleteClientAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.Clients
            .Where(c => c.Id == id)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false) > 0;
    }

    public async Task<(IReadOnlyList<Client> Items, int TotalCount)> ListClientsAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        var query = db.Clients.AsNoTracking().OrderByUsage();
        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);
        return (items, total);
    }

    public async Task<IReadOnlyList<Client>> SearchClientsAsync(
        Guid tenantId, string query, int limit, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        var q = query.ToLower();
        return await db.Clients.AsNoTracking()
            .Where(c => c.CustomerName.ToLower().Contains(q))
            .OrderByDescending(c => c.UsageCount)
            .Take(limit)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    // ══════════════════════════════ PROJECTS ═══════════════════════

    public async Task<Project?> FindProjectByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<Project?> FindProjectByKeyAsync(
        Guid tenantId, string identifierName, Guid? customerId, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        var name = identifierName.ToLower();
        return await db.Projects.AsNoTracking()
            .Where(p => p.IdentifierName.ToLower() == name
                        && (customerId == null ? p.CustomerId == null
                                                : p.CustomerId == customerId))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task InsertProjectAsync(Project project, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(project.TenantId);
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateProjectAsync(Project project, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(project.TenantId);
        db.Projects.Update(project);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> DeleteProjectAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.Projects
            .Where(p => p.Id == id)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false) > 0;
    }

    public async Task<(IReadOnlyList<Project> Items, int TotalCount)> ListProjectsAsync(
        Guid tenantId, Guid? customerId, int page, int pageSize, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        var query = db.Projects.AsNoTracking();
        query = customerId is null
            ? query.Where(p => p.CustomerId == null)
            : query.Where(p => p.CustomerId == customerId);
        var ordered = query.OrderByUsage();
        var total = await ordered.CountAsync(ct).ConfigureAwait(false);
        var items = await ordered
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);
        return (items, total);
    }

    public async Task<IReadOnlyList<Project>> SearchProjectsAsync(
        Guid tenantId, string query, int limit, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        var q = query.ToLower();
        return await db.Projects.AsNoTracking()
            .Where(p => p.IdentifierName.ToLower().Contains(q))
            .OrderByDescending(p => p.UsageCount)
            .Take(limit)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    // ══════════════════════════════ SALESMEN ═══════════════════════

    public async Task<Salesman?> FindSalesmanByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.Salesmen.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<Salesman?> FindSalesmanByKeyAsync(
        Guid tenantId, string name, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        var n = name.ToLower();
        return await db.Salesmen.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name.ToLower() == n, ct)
            .ConfigureAwait(false);
    }

    public async Task InsertSalesmanAsync(Salesman salesman, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(salesman.TenantId);
        db.Salesmen.Add(salesman);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateSalesmanAsync(Salesman salesman, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(salesman.TenantId);
        db.Salesmen.Update(salesman);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> DeleteSalesmanAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        return await db.Salesmen
            .Where(s => s.Id == id)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false) > 0;
    }

    public async Task<(IReadOnlyList<Salesman> Items, int TotalCount)> ListSalesmenAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        var query = db.Salesmen.AsNoTracking().OrderByUsage();
        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);
        return (items, total);
    }

    public async Task<IReadOnlyList<Salesman>> SearchSalesmenAsync(
        Guid tenantId, string query, int limit, CancellationToken ct)
    {
        using var db = _factory.CreateForTenant(tenantId);
        var q = query.ToLower();
        return await db.Salesmen.AsNoTracking()
            .Where(s => s.Name.ToLower().Contains(q))
            .OrderByDescending(s => s.UsageCount)
            .Take(limit)
            .ToListAsync(ct).ConfigureAwait(false);
    }
}

/// <summary>The canonical WPF list order: usage_count DESC, then
/// last_used_at DESC. NULLS sort LAST (COALESCE floor) — matches the
/// SQLite behavior the WPF lists rely on.</summary>
internal static class MasterDataOrdering
{
    public static IOrderedQueryable<Company> OrderByUsage(
        this IQueryable<Company> query) =>
        query.OrderByDescending(c => c.UsageCount)
            .ThenByDescending(c => c.LastUsedAt ?? DateTime.MinValue);

    public static IOrderedQueryable<Client> OrderByUsage(
        this IQueryable<Client> query) =>
        query.OrderByDescending(c => c.UsageCount)
            .ThenByDescending(c => c.LastUsedAt ?? DateTime.MinValue);

    public static IOrderedQueryable<Project> OrderByUsage(
        this IQueryable<Project> query) =>
        query.OrderByDescending(p => p.UsageCount)
            .ThenByDescending(p => p.LastUsedAt ?? DateTime.MinValue);

    public static IOrderedQueryable<Salesman> OrderByUsage(
        this IQueryable<Salesman> query) =>
        query.OrderByDescending(s => s.UsageCount)
            .ThenByDescending(s => s.LastUsedAt ?? DateTime.MinValue);
}
