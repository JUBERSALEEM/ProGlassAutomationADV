using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Application.MasterData;

/// <summary>
/// Persistence port for the four master-data entities — the SaaS twin of
/// the WPF IMasterDataRepository (one interface for all four on purpose:
/// same upsert/search/list pattern, one store — the WPF file says exactly
/// this). Like the auth stores, every method takes the tenant id
/// EXPLICITLY; implementations own tenant-scoped access (query filter +
/// RLS both fail closed).
///
/// MECHANICAL ONLY: the WPF upsert POLICY (lookup order, rename-collision
/// refusal, preserve-on-blank, single default, usage counting) lives in
/// MasterDataService so it is unit-tested in one place. List/search
/// methods apply the canonical WPF order (usage_count DESC, last_used_at
/// DESC) as part of the query contract.
/// </summary>
public interface IMasterDataStore
{
    // ── Companies ───────────────────────────────────────────────────

    Task<Company?> FindCompanyByIdAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>Natural key: name + TRN, case-insensitive (WPF).</summary>
    Task<Company?> FindCompanyByKeyAsync(
        Guid tenantId, string name, string trn, CancellationToken ct);

    /// <summary>Another company (except <paramref name="exceptCompanyId"/>)
    /// already owns the name+TRN pair — the rename-collision guard.</summary>
    Task<bool> IsCompanyKeyTakenAsync(
        Guid tenantId, string name, string trn, Guid exceptCompanyId, CancellationToken ct);

    /// <summary>Inserts the company. <paramref name="clearOtherDefaults"/>
    /// applies the single-default rule in the SAME transaction (the WPF
    /// SaveChanges was one atomic unit — a crash between "clear others"
    /// and "save this" must not be able to leave two defaults).</summary>
    Task InsertCompanyAsync(Company company, bool clearOtherDefaults, CancellationToken ct);

    /// <summary>Updates the company (detached, full-row). Same
    /// single-default atomicity as the insert.</summary>
    Task UpdateCompanyAsync(Company company, bool clearOtherDefaults, CancellationToken ct);
    Task<bool> DeleteCompanyAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<(IReadOnlyList<Company> Items, int TotalCount)> ListCompaniesAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct);

    /// <summary>Case-insensitive contains on the name (autocomplete).</summary>
    Task<IReadOnlyList<Company>> SearchCompaniesAsync(
        Guid tenantId, string query, int limit, CancellationToken ct);

    // ── Clients ─────────────────────────────────────────────────────

    Task<Client?> FindClientByIdAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>Natural key: customer name + TRN, case-insensitive (WPF).</summary>
    Task<Client?> FindClientByKeyAsync(
        Guid tenantId, string customerName, string customerTrn, CancellationToken ct);

    Task InsertClientAsync(Client client, CancellationToken ct);
    Task UpdateClientAsync(Client client, CancellationToken ct);
    Task<bool> DeleteClientAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<(IReadOnlyList<Client> Items, int TotalCount)> ListClientsAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<Client>> SearchClientsAsync(
        Guid tenantId, string query, int limit, CancellationToken ct);

    // ── Projects ────────────────────────────────────────────────────

    Task<Project?> FindProjectByIdAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>Natural key: identifier name + customer, case-insensitive
    /// (WPF: ProjectIdentifierName + CustomerName; the SaaS schema links
    /// the customer by id — same rule, normalized reference).</summary>
    Task<Project?> FindProjectByKeyAsync(
        Guid tenantId, string identifierName, Guid? customerId, CancellationToken ct);

    Task InsertProjectAsync(Project project, CancellationToken ct);
    Task UpdateProjectAsync(Project project, CancellationToken ct);
    Task<bool> DeleteProjectAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<(IReadOnlyList<Project> Items, int TotalCount)> ListProjectsAsync(
        Guid tenantId, Guid? customerId, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<Project>> SearchProjectsAsync(
        Guid tenantId, string query, int limit, CancellationToken ct);

    // ── Salesmen ────────────────────────────────────────────────────

    Task<Salesman?> FindSalesmanByIdAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>Natural key: name, case-insensitive (WPF).</summary>
    Task<Salesman?> FindSalesmanByKeyAsync(
        Guid tenantId, string name, CancellationToken ct);

    Task InsertSalesmanAsync(Salesman salesman, CancellationToken ct);
    Task UpdateSalesmanAsync(Salesman salesman, CancellationToken ct);
    Task<bool> DeleteSalesmanAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<(IReadOnlyList<Salesman> Items, int TotalCount)> ListSalesmenAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<Salesman>> SearchSalesmenAsync(
        Guid tenantId, string query, int limit, CancellationToken ct);
}
