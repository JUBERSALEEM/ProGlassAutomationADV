using ProGlass.Saas.Application.MasterData;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Application.Tests;

/// <summary>
/// In-memory IMasterDataStore — mirrors the EF implementation's mechanics
/// (case-insensitive keys, WPF list order, usage-desc search, paging) so
/// the MasterDataService tests exercise the real upsert policy.
/// </summary>
public sealed class FakeMasterDataStore : IMasterDataStore
{
    public List<Company> Companies { get; } = new();
    public List<Client> Clients { get; } = new();
    public List<Project> Projects { get; } = new();
    public List<Salesman> Salesmen { get; } = new();

    // ── Companies ───────────────────────────────────────────────────

    public Task<Company?> FindCompanyByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Companies.FirstOrDefault(c =>
            c.TenantId == tenantId && c.Id == id));

    public Task<Company?> FindCompanyByKeyAsync(
        Guid tenantId, string name, string trn, CancellationToken ct)
        => Task.FromResult(Companies.FirstOrDefault(c =>
            c.TenantId == tenantId
            && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.Trn, trn, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> IsCompanyKeyTakenAsync(
        Guid tenantId, string name, string trn, Guid exceptCompanyId, CancellationToken ct)
        => Task.FromResult(Companies.Any(c =>
            c.TenantId == tenantId && c.Id != exceptCompanyId
            && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.Trn, trn, StringComparison.OrdinalIgnoreCase)));

    public Task InsertCompanyAsync(Company company, bool clearOtherDefaults, CancellationToken ct)
    {
        if (clearOtherDefaults)
            ClearOtherDefaults(company.TenantId, company.Id);
        Companies.Add(company);
        return Task.CompletedTask;
    }

    public Task UpdateCompanyAsync(Company company, bool clearOtherDefaults, CancellationToken ct)
    {
        if (clearOtherDefaults)
            ClearOtherDefaults(company.TenantId, company.Id);
        var index = Companies.FindIndex(c => c.Id == company.Id);
        Companies[index] = company;
        return Task.CompletedTask;
    }

    private void ClearOtherDefaults(Guid tenantId, Guid keepCompanyId)
    {
        foreach (var other in Companies.Where(c =>
                     c.TenantId == tenantId && c.Id != keepCompanyId && c.IsDefault))
            other.IsDefault = false;
    }

    public Task<bool> DeleteCompanyAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var company = Companies.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id);
        return Task.FromResult(Companies.Remove(company!));
    }

    public Task<(IReadOnlyList<Company> Items, int TotalCount)> ListCompaniesAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct)
    {
        var all = Companies.Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.UsageCount)
            .ThenByDescending(c => c.LastUsedAt ?? DateTime.MinValue)
            .ToList();
        return Task.FromResult<(IReadOnlyList<Company>, int)>(
            (all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), all.Count));
    }

    public Task<IReadOnlyList<Company>> SearchCompaniesAsync(
        Guid tenantId, string query, int limit, CancellationToken ct)
    {
        var q = query.ToLowerInvariant();
        return Task.FromResult<IReadOnlyList<Company>>(Companies
            .Where(c => c.TenantId == tenantId && c.Name.ToLowerInvariant().Contains(q))
            .OrderByDescending(c => c.UsageCount)
            .Take(limit)
            .ToList());
    }

    // ── Clients ─────────────────────────────────────────────────────

    public Task<Client?> FindClientByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Clients.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id));

    public Task<Client?> FindClientByKeyAsync(
        Guid tenantId, string customerName, string customerTrn, CancellationToken ct)
        => Task.FromResult(Clients.FirstOrDefault(c =>
            c.TenantId == tenantId
            && string.Equals(c.CustomerName, customerName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.CustomerTrn, customerTrn, StringComparison.OrdinalIgnoreCase)));

    public Task InsertClientAsync(Client client, CancellationToken ct)
    {
        Clients.Add(client);
        return Task.CompletedTask;
    }

    public Task UpdateClientAsync(Client client, CancellationToken ct)
    {
        var index = Clients.FindIndex(c => c.Id == client.Id);
        Clients[index] = client;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteClientAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var client = Clients.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id);
        return Task.FromResult(Clients.Remove(client!));
    }

    public Task<(IReadOnlyList<Client> Items, int TotalCount)> ListClientsAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct)
    {
        var all = Clients.Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.UsageCount)
            .ThenByDescending(c => c.LastUsedAt ?? DateTime.MinValue)
            .ToList();
        return Task.FromResult<(IReadOnlyList<Client>, int)>(
            (all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), all.Count));
    }

    public Task<IReadOnlyList<Client>> SearchClientsAsync(
        Guid tenantId, string query, int limit, CancellationToken ct)
    {
        var q = query.ToLowerInvariant();
        return Task.FromResult<IReadOnlyList<Client>>(Clients
            .Where(c => c.TenantId == tenantId && c.CustomerName.ToLowerInvariant().Contains(q))
            .OrderByDescending(c => c.UsageCount)
            .Take(limit)
            .ToList());
    }

    // ── Projects ────────────────────────────────────────────────────

    public Task<Project?> FindProjectByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Projects.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id));

    public Task<Project?> FindProjectByKeyAsync(
        Guid tenantId, string identifierName, Guid? customerId, CancellationToken ct)
        => Task.FromResult(Projects.FirstOrDefault(p =>
            p.TenantId == tenantId
            && string.Equals(p.IdentifierName, identifierName, StringComparison.OrdinalIgnoreCase)
            && p.CustomerId == customerId));

    public Task InsertProjectAsync(Project project, CancellationToken ct)
    {
        Projects.Add(project);
        return Task.CompletedTask;
    }

    public Task UpdateProjectAsync(Project project, CancellationToken ct)
    {
        var index = Projects.FindIndex(p => p.Id == project.Id);
        Projects[index] = project;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteProjectAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var project = Projects.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id);
        return Task.FromResult(Projects.Remove(project!));
    }

    public Task<(IReadOnlyList<Project> Items, int TotalCount)> ListProjectsAsync(
        Guid tenantId, Guid? customerId, int page, int pageSize, CancellationToken ct)
    {
        var all = Projects
            .Where(p => p.TenantId == tenantId && p.CustomerId == customerId)
            .OrderByDescending(p => p.UsageCount)
            .ThenByDescending(p => p.LastUsedAt ?? DateTime.MinValue)
            .ToList();
        return Task.FromResult<(IReadOnlyList<Project>, int)>(
            (all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), all.Count));
    }

    public Task<IReadOnlyList<Project>> SearchProjectsAsync(
        Guid tenantId, string query, int limit, CancellationToken ct)
    {
        var q = query.ToLowerInvariant();
        return Task.FromResult<IReadOnlyList<Project>>(Projects
            .Where(p => p.TenantId == tenantId
                        && p.IdentifierName.ToLowerInvariant().Contains(q))
            .OrderByDescending(p => p.UsageCount)
            .Take(limit)
            .ToList());
    }

    // ── Salesmen ────────────────────────────────────────────────────

    public Task<Salesman?> FindSalesmanByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Salesmen.FirstOrDefault(s => s.TenantId == tenantId && s.Id == id));

    public Task<Salesman?> FindSalesmanByKeyAsync(
        Guid tenantId, string name, CancellationToken ct)
        => Task.FromResult(Salesmen.FirstOrDefault(s =>
            s.TenantId == tenantId
            && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task InsertSalesmanAsync(Salesman salesman, CancellationToken ct)
    {
        Salesmen.Add(salesman);
        return Task.CompletedTask;
    }

    public Task UpdateSalesmanAsync(Salesman salesman, CancellationToken ct)
    {
        var index = Salesmen.FindIndex(s => s.Id == salesman.Id);
        Salesmen[index] = salesman;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteSalesmanAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var salesman = Salesmen.FirstOrDefault(s => s.TenantId == tenantId && s.Id == id);
        return Task.FromResult(Salesmen.Remove(salesman!));
    }

    public Task<(IReadOnlyList<Salesman> Items, int TotalCount)> ListSalesmenAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct)
    {
        var all = Salesmen.Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.UsageCount)
            .ThenByDescending(s => s.LastUsedAt ?? DateTime.MinValue)
            .ToList();
        return Task.FromResult<(IReadOnlyList<Salesman>, int)>(
            (all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), all.Count));
    }

    public Task<IReadOnlyList<Salesman>> SearchSalesmenAsync(
        Guid tenantId, string query, int limit, CancellationToken ct)
    {
        var q = query.ToLowerInvariant();
        return Task.FromResult<IReadOnlyList<Salesman>>(Salesmen
            .Where(s => s.TenantId == tenantId && s.Name.ToLowerInvariant().Contains(q))
            .OrderByDescending(s => s.UsageCount)
            .Take(limit)
            .ToList());
    }
}
