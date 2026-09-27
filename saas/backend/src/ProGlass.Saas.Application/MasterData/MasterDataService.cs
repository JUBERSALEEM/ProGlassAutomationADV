using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Application.MasterData;

/// <summary>
/// Master-data use cases — the WPF MasterDataRepository semantics ported
/// to a multi-tenant server (source of truth:
/// Services/MasterData/Repository/MasterDataRepository.cs, read
/// 2026-09-27):
///
///   • Upserts match by natural key, case-insensitive (companies:
///     name+TRN, clients: name+TRN, projects: identifier+customer,
///     salesmen: name); a match updates + increments usage_count + stamps
///     last_used_at; a miss inserts with usage_count = 1.
///   • Company edits by id may RENAME — refused when another row already
///     owns the new name+TRN (WPF audit-fix F-1b).
///   • Company invoice auto-save (preserve-on-blank) never erases stored
///     profile values and never touches IsDefault (WPF F-2). Exposed for
///     the Phase 7 proforma save; not an API endpoint.
///   • Single default company: setting IsDefault clears every other row.
///   • Salesman department is only overwritten when the new value is
///     non-blank (WPF preserve-on-blank quirk, verbatim).
///   • List order: usage_count DESC, last_used_at DESC. Autocomplete
///     search: case-insensitive contains on the name, top 20.
///
/// Differences from WPF (documented in WPF_TO_SAAS_MAPPING §1.10):
///   • UTC timestamps instead of local time (ADR-007).
///   • DELETE answers 404 for a missing id (REST + §0 "never reveal
///     cross-tenant existence"); WPF silently no-ops.
///   • Projects reference the customer by id (schema normalization).
///   • WPF's client-side ReadOnlyGate/OperationGuard become API
///     permission policies on the controllers ([HasPermission]).
/// </summary>
public sealed class MasterDataService
{
    /// <summary>WPF SEARCH_LIMIT (autocomplete), verbatim.</summary>
    public const int SearchLimit = 20;

    /// <summary>API design §0: pageSize max 200.</summary>
    public const int MaxPageSize = 200;

    private const int DefaultPageSize = 50;

    private static readonly JsonSerializerOptions AuditJson = new()
    {
        WriteIndented = false
    };

    private readonly IMasterDataStore _store;
    private readonly IAuditWriter _audit;

    public MasterDataService(IMasterDataStore store, IAuditWriter audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    // ════════════════════════════════ COMPANIES ══════════════════════

    /// <summary>POST — natural-key upsert with editor (latest-data-wins)
    /// semantics, the WPF "save company" path.</summary>
    public async Task<MasterDataResult<CompanyDto>> SaveCompanyAsync(
        Guid tenantId, Guid? actorUserId, CompanyRequest request,
        string? ip, CancellationToken ct)
    {
        var error = ValidateCompany(request);
        if (error != null)
            return MasterDataResult<CompanyDto>.Fail(
                MasterDataErrorCode.ValidationFailed, error);

        var name = Trim(request.Name);
        var trn = Trim(request.Trn);
        var existing = await _store.FindCompanyByKeyAsync(tenantId, name, trn, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            var before = CompanySnapshot(existing);
            existing.Name = name; // WPF: adopts the incoming (trimmed) spelling
            existing.Trn = trn;
            ApplyCompanyFields(existing, request);
            Touch(existing);
            await _store.UpdateCompanyAsync(existing, existing.IsDefault, ct)
                .ConfigureAwait(false);
            await AuditAsync(tenantId, actorUserId, "masterdata.company_updated",
                "companies", existing.Id, before, CompanySnapshot(existing), ip, ct)
                .ConfigureAwait(false);
            return MasterDataResult<CompanyDto>.Ok(ToDto(existing));
        }

        var company = NewCompany(tenantId, request);
        await _store.InsertCompanyAsync(company, company.IsDefault, ct)
            .ConfigureAwait(false);
        await AuditAsync(tenantId, actorUserId, "masterdata.company_created",
            "companies", company.Id, null, CompanySnapshot(company), ip, ct)
            .ConfigureAwait(false);
        return MasterDataResult<CompanyDto>.Ok(ToDto(company));
    }

    /// <summary>PUT — Company Center edit by id: renaming allowed, refused
    /// when the new name+TRN belongs to another row (WPF F-1/F-1b).</summary>
    public async Task<MasterDataResult<CompanyDto>> UpdateCompanyAsync(
        Guid tenantId, Guid id, Guid? actorUserId, CompanyRequest request,
        string? ip, CancellationToken ct)
    {
        var error = ValidateCompany(request);
        if (error != null)
            return MasterDataResult<CompanyDto>.Fail(
                MasterDataErrorCode.ValidationFailed, error);

        var existing = await _store.FindCompanyByIdAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        if (existing is null)
            return MasterDataResult<CompanyDto>.Fail(
                MasterDataErrorCode.NotFound, "Company not found.");

        var name = Trim(request.Name);
        var trn = Trim(request.Trn);
        var keyChanged =
            !string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existing.Trn, trn, StringComparison.OrdinalIgnoreCase);
        if (keyChanged
            && await _store.IsCompanyKeyTakenAsync(tenantId, name, trn, id, ct)
                .ConfigureAwait(false))
        {
            return MasterDataResult<CompanyDto>.Fail(
                MasterDataErrorCode.RenameConflict,
                $"Cannot rename \"{existing.Name}\": another saved company already " +
                $"uses the name + TRN \"{name}\" / \"{trn}\".");
        }

        var before = CompanySnapshot(existing);
        existing.Name = name;
        existing.Trn = trn;
        ApplyCompanyFields(existing, request);
        Touch(existing);
        await _store.UpdateCompanyAsync(existing, existing.IsDefault, ct)
            .ConfigureAwait(false);
        await AuditAsync(tenantId, actorUserId, "masterdata.company_updated",
            "companies", existing.Id, before, CompanySnapshot(existing), ip, ct)
            .ConfigureAwait(false);
        return MasterDataResult<CompanyDto>.Ok(ToDto(existing));
    }

    /// <summary>
    /// The invoice auto-save path (WPF SaveOrUpdateCompanyAsync with
    /// preserveUnsetProfileFields: true) — internal, used by the Phase 7
    /// proforma save. Lookup order is id-first (an edit may have renamed
    /// the company), then natural key. Blank profile fields NEVER erase
    /// stored values; IsDefault is never touched here.
    /// </summary>
    public async Task<CompanyDto> UpsertCompanyFromInvoiceAsync(
        Guid tenantId, Guid? companyId, CompanyRequest request, CancellationToken ct)
    {
        var name = Trim(request.Name);
        var trn = Trim(request.Trn);

        var existing = companyId is { } byId
            ? await _store.FindCompanyByIdAsync(tenantId, byId, ct).ConfigureAwait(false)
            : null;
        existing ??= await _store.FindCompanyByKeyAsync(tenantId, name, trn, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var company = new Company
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                Trn = trn,
                Location = Trim(request.Location),
                Representative = Trim(request.Representative),
                RepresentativeMobile = Trim(request.RepresentativeMobile),
                Email = Trim(request.Email),
                AccountNumber = Trim(request.AccountNumber),
                BankName = Trim(request.BankName),
                LogoStorageKey = Trim(request.LogoStorageKey),
                LogoLayout = CompanyLogoLayouts.IsValid(request.LogoLayout)
                    ? request.LogoLayout
                    : CompanyLogoLayouts.None,
                LogoWidthMm = request.LogoWidthMm > 0 ? request.LogoWidthMm : 22m,
                LogoHeightMm = request.LogoHeightMm > 0 ? request.LogoHeightMm : 26m,
                UsageCount = 1,
                LastUsedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            await _store.InsertCompanyAsync(company, false, ct).ConfigureAwait(false);
            return ToDto(company);
        }

        // Preserve-on-blank (WPF F-2): non-blank values enrich, blanks
        // never erase. Branding layout/size only move when actually set.
        existing.Name = name.Length > 0 ? name : existing.Name;
        existing.Trn = trn.Length > 0 ? trn : existing.Trn;
        SetIfProvided(v => existing.Location = v, request.Location);
        SetIfProvided(v => existing.LogoStorageKey = v, request.LogoStorageKey);
        SetIfProvided(v => existing.Representative = v, request.Representative);
        SetIfProvided(v => existing.RepresentativeMobile = v, request.RepresentativeMobile);
        SetIfProvided(v => existing.Email = v, request.Email);
        SetIfProvided(v => existing.AccountNumber = v, request.AccountNumber);
        SetIfProvided(v => existing.BankName = v, request.BankName);
        if (CompanyLogoLayouts.IsValid(request.LogoLayout)
            && request.LogoLayout != CompanyLogoLayouts.None)
            existing.LogoLayout = request.LogoLayout;
        if (request.LogoWidthMm > 0)
            existing.LogoWidthMm = request.LogoWidthMm;
        if (request.LogoHeightMm > 0)
            existing.LogoHeightMm = request.LogoHeightMm;
        Touch(existing);
        await _store.UpdateCompanyAsync(existing, false, ct).ConfigureAwait(false);
        return ToDto(existing);
    }

    public async Task<MasterDataResult<CompanyDto>> GetCompanyAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var company = await _store.FindCompanyByIdAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        return company is null
            ? MasterDataResult<CompanyDto>.Fail(
                MasterDataErrorCode.NotFound, "Company not found.")
            : MasterDataResult<CompanyDto>.Ok(ToDto(company));
    }

    public async Task<PagedResult<CompanyDto>> ListCompaniesAsync(
        Guid tenantId, string? search, int? page, int? pageSize, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var hits = await _store.SearchCompaniesAsync(
                tenantId, search.Trim(), SearchLimit, ct).ConfigureAwait(false);
            return new PagedResult<CompanyDto>(
                hits.Select(ToDto).ToList(), 1, hits.Count, hits.Count);
        }
        var (items, total) = await _store.ListCompaniesAsync(
            tenantId, NormalizePage(page), NormalizePageSize(pageSize), ct)
            .ConfigureAwait(false);
        return new PagedResult<CompanyDto>(
            items.Select(ToDto).ToList(),
            NormalizePage(page), NormalizePageSize(pageSize), total);
    }

    public async Task<MasterDataResult<object>> DeleteCompanyAsync(
        Guid tenantId, Guid id, Guid? actorUserId, string? ip, CancellationToken ct)
    {
        var existing = await _store.FindCompanyByIdAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        if (existing is null)
            return MasterDataResult<object>.Fail(
                MasterDataErrorCode.NotFound, "Company not found.");

        var deleted = await _store.DeleteCompanyAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        if (deleted)
            await AuditAsync(tenantId, actorUserId, "masterdata.company_deleted",
                "companies", id, CompanySnapshot(existing), null, ip, ct)
                .ConfigureAwait(false);
        return MasterDataResult<object>.Ok(new object());
    }

    // ════════════════════════════════ CLIENTS ════════════════════════

    /// <summary>POST — natural-key upsert (name+TRN). The key fields of an
    /// existing row are immutable (WPF: only non-key fields are
    /// overwritten); a different TRN is a different client.</summary>
    public async Task<MasterDataResult<ClientDto>> SaveClientAsync(
        Guid tenantId, Guid? actorUserId, ClientRequest request,
        string? ip, CancellationToken ct)
    {
        var error = ValidateClient(request);
        if (error != null)
            return MasterDataResult<ClientDto>.Fail(
                MasterDataErrorCode.ValidationFailed, error);

        var name = Trim(request.CustomerName);
        var trn = Trim(request.CustomerTrn);
        var existing = await _store.FindClientByKeyAsync(tenantId, name, trn, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            var before = ClientSnapshot(existing);
            existing.ReferenceCode = Trim(request.ReferenceCode);
            existing.SalesRepresentative = Trim(request.SalesRepresentative);
            existing.DocumentPreparedBy = Trim(request.DocumentPreparedBy);
            existing.BillingAddress = Trim(request.BillingAddress);
            existing.AttentionExecutiveName = Trim(request.AttentionExecutiveName);
            existing.Phone = Trim(request.Phone);
            Touch(existing);
            await _store.UpdateClientAsync(existing, ct).ConfigureAwait(false);
            await AuditAsync(tenantId, actorUserId, "masterdata.client_updated",
                "clients", existing.Id, before, ClientSnapshot(existing), ip, ct)
                .ConfigureAwait(false);
            return MasterDataResult<ClientDto>.Ok(ToDto(existing));
        }

        var client = new Client
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerName = name,
            CustomerTrn = trn,
            ReferenceCode = Trim(request.ReferenceCode),
            SalesRepresentative = Trim(request.SalesRepresentative),
            DocumentPreparedBy = Trim(request.DocumentPreparedBy),
            BillingAddress = Trim(request.BillingAddress),
            AttentionExecutiveName = Trim(request.AttentionExecutiveName),
            Phone = Trim(request.Phone),
            UsageCount = 1,
            LastUsedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        await _store.InsertClientAsync(client, ct).ConfigureAwait(false);
        await AuditAsync(tenantId, actorUserId, "masterdata.client_created",
            "clients", client.Id, null, ClientSnapshot(client), ip, ct)
            .ConfigureAwait(false);
        return MasterDataResult<ClientDto>.Ok(ToDto(client));
    }

    /// <summary>PUT — edit by id. Key (name+TRN) stays immutable (WPF has
    /// no client rename path); non-key fields take the latest data.</summary>
    public async Task<MasterDataResult<ClientDto>> UpdateClientAsync(
        Guid tenantId, Guid id, Guid? actorUserId, ClientRequest request,
        string? ip, CancellationToken ct)
    {
        var error = ValidateClient(request);
        if (error != null)
            return MasterDataResult<ClientDto>.Fail(
                MasterDataErrorCode.ValidationFailed, error);

        var existing = await _store.FindClientByIdAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        if (existing is null)
            return MasterDataResult<ClientDto>.Fail(
                MasterDataErrorCode.NotFound, "Client not found.");

        var before = ClientSnapshot(existing);
        existing.ReferenceCode = Trim(request.ReferenceCode);
        existing.SalesRepresentative = Trim(request.SalesRepresentative);
        existing.DocumentPreparedBy = Trim(request.DocumentPreparedBy);
        existing.BillingAddress = Trim(request.BillingAddress);
        existing.AttentionExecutiveName = Trim(request.AttentionExecutiveName);
        existing.Phone = Trim(request.Phone);
        Touch(existing);
        await _store.UpdateClientAsync(existing, ct).ConfigureAwait(false);
        await AuditAsync(tenantId, actorUserId, "masterdata.client_updated",
            "clients", existing.Id, before, ClientSnapshot(existing), ip, ct)
            .ConfigureAwait(false);
        return MasterDataResult<ClientDto>.Ok(ToDto(existing));
    }

    public async Task<MasterDataResult<ClientDto>> GetClientAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var client = await _store.FindClientByIdAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        return client is null
            ? MasterDataResult<ClientDto>.Fail(
                MasterDataErrorCode.NotFound, "Client not found.")
            : MasterDataResult<ClientDto>.Ok(ToDto(client));
    }

    public async Task<PagedResult<ClientDto>> ListClientsAsync(
        Guid tenantId, string? search, int? page, int? pageSize, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var hits = await _store.SearchClientsAsync(
                tenantId, search.Trim(), SearchLimit, ct).ConfigureAwait(false);
            return new PagedResult<ClientDto>(
                hits.Select(ToDto).ToList(), 1, hits.Count, hits.Count);
        }
        var (items, total) = await _store.ListClientsAsync(
            tenantId, NormalizePage(page), NormalizePageSize(pageSize), ct)
            .ConfigureAwait(false);
        return new PagedResult<ClientDto>(
            items.Select(ToDto).ToList(),
            NormalizePage(page), NormalizePageSize(pageSize), total);
    }

    public async Task<MasterDataResult<object>> DeleteClientAsync(
        Guid tenantId, Guid id, Guid? actorUserId, string? ip, CancellationToken ct)
    {
        var existing = await _store.FindClientByIdAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        if (existing is null)
            return MasterDataResult<object>.Fail(
                MasterDataErrorCode.NotFound, "Client not found.");

        var deleted = await _store.DeleteClientAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        if (deleted)
            await AuditAsync(tenantId, actorUserId, "masterdata.client_deleted",
                "clients", id, ClientSnapshot(existing), null, ip, ct)
                .ConfigureAwait(false);
        return MasterDataResult<object>.Ok(new object());
    }

    // ════════════════════════════════ PROJECTS ═══════════════════════

    /// <summary>POST — natural-key upsert (identifier + customer). Same
    /// project name for a different customer is a different project
    /// (WPF rule; the customer reference is an id in the SaaS schema).</summary>
    public async Task<MasterDataResult<ProjectDto>> SaveProjectAsync(
        Guid tenantId, Guid? actorUserId, ProjectRequest request,
        string? ip, CancellationToken ct)
    {
        var error = ValidateProject(request);
        if (error != null)
            return MasterDataResult<ProjectDto>.Fail(
                MasterDataErrorCode.ValidationFailed, error);

        var identifier = Trim(request.IdentifierName);
        var existing = await _store.FindProjectByKeyAsync(
            tenantId, identifier, request.CustomerId, ct).ConfigureAwait(false);

        if (existing is not null)
        {
            var before = ProjectSnapshot(existing);
            ApplyProjectFields(existing, request);
            Touch(existing);
            await _store.UpdateProjectAsync(existing, ct).ConfigureAwait(false);
            await AuditAsync(tenantId, actorUserId, "masterdata.project_updated",
                "projects", existing.Id, before, ProjectSnapshot(existing), ip, ct)
                .ConfigureAwait(false);
            return MasterDataResult<ProjectDto>.Ok(ToDto(existing));
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IdentifierName = identifier,
            CustomerId = request.CustomerId,
            ContractNo = Trim(request.ContractNo),
            Reference = Trim(request.Reference),
            SiteDeliveryLocation = Trim(request.SiteDeliveryLocation),
            LpoNumber = Trim(request.LpoNumber),
            UsageCount = 1,
            LastUsedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        await _store.InsertProjectAsync(project, ct).ConfigureAwait(false);
        await AuditAsync(tenantId, actorUserId, "masterdata.project_created",
            "projects", project.Id, null, ProjectSnapshot(project), ip, ct)
            .ConfigureAwait(false);
        return MasterDataResult<ProjectDto>.Ok(ToDto(project));
    }

    /// <summary>PUT — edit by id; the key (identifier + customer) is
    /// immutable (WPF has no project rename path).</summary>
    public async Task<MasterDataResult<ProjectDto>> UpdateProjectAsync(
        Guid tenantId, Guid id, Guid? actorUserId, ProjectRequest request,
        string? ip, CancellationToken ct)
    {
        var error = ValidateProject(request);
        if (error != null)
            return MasterDataResult<ProjectDto>.Fail(
                MasterDataErrorCode.ValidationFailed, error);

        var existing = await _store.FindProjectByIdAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        if (existing is null)
            return MasterDataResult<ProjectDto>.Fail(
                MasterDataErrorCode.NotFound, "Project not found.");

        var before = ProjectSnapshot(existing);
        ApplyProjectFields(existing, request);
        Touch(existing);
        await _store.UpdateProjectAsync(existing, ct).ConfigureAwait(false);
        await AuditAsync(tenantId, actorUserId, "masterdata.project_updated",
            "projects", existing.Id, before, ProjectSnapshot(existing), ip, ct)
            .ConfigureAwait(false);
        return MasterDataResult<ProjectDto>.Ok(ToDto(existing));
    }

    public async Task<MasterDataResult<ProjectDto>> GetProjectAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var project = await _store.FindProjectByIdAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        return project is null
            ? MasterDataResult<ProjectDto>.Fail(
                MasterDataErrorCode.NotFound, "Project not found.")
            : MasterDataResult<ProjectDto>.Ok(ToDto(project));
    }

    public async Task<PagedResult<ProjectDto>> ListProjectsAsync(
        Guid tenantId, Guid? customerId, string? search,
        int? page, int? pageSize, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var hits = await _store.SearchProjectsAsync(
                tenantId, search.Trim(), SearchLimit, ct).ConfigureAwait(false);
            return new PagedResult<ProjectDto>(
                hits.Select(ToDto).ToList(), 1, hits.Count, hits.Count);
        }
        var (items, total) = await _store.ListProjectsAsync(
            tenantId, customerId, NormalizePage(page), NormalizePageSize(pageSize), ct)
            .ConfigureAwait(false);
        return new PagedResult<ProjectDto>(
            items.Select(ToDto).ToList(),
            NormalizePage(page), NormalizePageSize(pageSize), total);
    }

    public async Task<MasterDataResult<object>> DeleteProjectAsync(
        Guid tenantId, Guid id, Guid? actorUserId, string? ip, CancellationToken ct)
    {
        var existing = await _store.FindProjectByIdAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        if (existing is null)
            return MasterDataResult<object>.Fail(
                MasterDataErrorCode.NotFound, "Project not found.");

        var deleted = await _store.DeleteProjectAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        if (deleted)
            await AuditAsync(tenantId, actorUserId, "masterdata.project_deleted",
                "projects", id, ProjectSnapshot(existing), null, ip, ct)
                .ConfigureAwait(false);
        return MasterDataResult<object>.Ok(new object());
    }

    // ════════════════════════════════ SALESMEN ═══════════════════════

    /// <summary>POST — natural-key upsert by name. Department is only
    /// overwritten when the new value is non-blank (WPF quirk, verbatim
    /// — blank never erases a stored department).</summary>
    public async Task<MasterDataResult<SalesmanDto>> SaveSalesmanAsync(
        Guid tenantId, Guid? actorUserId, SalesmanRequest request,
        string? ip, CancellationToken ct)
    {
        var error = ValidateSalesman(request);
        if (error != null)
            return MasterDataResult<SalesmanDto>.Fail(
                MasterDataErrorCode.ValidationFailed, error);

        var name = Trim(request.Name);
        var existing = await _store.FindSalesmanByKeyAsync(tenantId, name, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            var before = SalesmanSnapshot(existing);
            if (!string.IsNullOrWhiteSpace(request.Department))
                existing.Department = Trim(request.Department);
            Touch(existing);
            await _store.UpdateSalesmanAsync(existing, ct).ConfigureAwait(false);
            await AuditAsync(tenantId, actorUserId, "masterdata.salesman_updated",
                "salesmen", existing.Id, before, SalesmanSnapshot(existing), ip, ct)
                .ConfigureAwait(false);
            return MasterDataResult<SalesmanDto>.Ok(ToDto(existing));
        }

        var salesman = new Salesman
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Department = Trim(request.Department),
            UsageCount = 1,
            LastUsedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        await _store.InsertSalesmanAsync(salesman, ct).ConfigureAwait(false);
        await AuditAsync(tenantId, actorUserId, "masterdata.salesman_created",
            "salesmen", salesman.Id, null, SalesmanSnapshot(salesman), ip, ct)
            .ConfigureAwait(false);
        return MasterDataResult<SalesmanDto>.Ok(ToDto(salesman));
    }

    /// <summary>PUT — edit by id; department updated only when non-blank
    /// (the WPF save path has no id-based salesman edit; the preserve
    /// rule is the closest verbatim behavior).</summary>
    public async Task<MasterDataResult<SalesmanDto>> UpdateSalesmanAsync(
        Guid tenantId, Guid id, Guid? actorUserId, SalesmanRequest request,
        string? ip, CancellationToken ct)
    {
        var error = ValidateSalesman(request);
        if (error != null)
            return MasterDataResult<SalesmanDto>.Fail(
                MasterDataErrorCode.ValidationFailed, error);

        var existing = await _store.FindSalesmanByIdAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        if (existing is null)
            return MasterDataResult<SalesmanDto>.Fail(
                MasterDataErrorCode.NotFound, "Salesman not found.");

        var before = SalesmanSnapshot(existing);
        if (!string.IsNullOrWhiteSpace(request.Department))
            existing.Department = Trim(request.Department);
        Touch(existing);
        await _store.UpdateSalesmanAsync(existing, ct).ConfigureAwait(false);
        await AuditAsync(tenantId, actorUserId, "masterdata.salesman_updated",
            "salesmen", existing.Id, before, SalesmanSnapshot(existing), ip, ct)
            .ConfigureAwait(false);
        return MasterDataResult<SalesmanDto>.Ok(ToDto(existing));
    }

    public async Task<MasterDataResult<SalesmanDto>> GetSalesmanAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var salesman = await _store.FindSalesmanByIdAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        return salesman is null
            ? MasterDataResult<SalesmanDto>.Fail(
                MasterDataErrorCode.NotFound, "Salesman not found.")
            : MasterDataResult<SalesmanDto>.Ok(ToDto(salesman));
    }

    public async Task<PagedResult<SalesmanDto>> ListSalesmenAsync(
        Guid tenantId, string? search, int? page, int? pageSize, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var hits = await _store.SearchSalesmenAsync(
                tenantId, search.Trim(), SearchLimit, ct).ConfigureAwait(false);
            return new PagedResult<SalesmanDto>(
                hits.Select(ToDto).ToList(), 1, hits.Count, hits.Count);
        }
        var (items, total) = await _store.ListSalesmenAsync(
            tenantId, NormalizePage(page), NormalizePageSize(pageSize), ct)
            .ConfigureAwait(false);
        return new PagedResult<SalesmanDto>(
            items.Select(ToDto).ToList(),
            NormalizePage(page), NormalizePageSize(pageSize), total);
    }

    public async Task<MasterDataResult<object>> DeleteSalesmanAsync(
        Guid tenantId, Guid id, Guid? actorUserId, string? ip, CancellationToken ct)
    {
        var existing = await _store.FindSalesmanByIdAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        if (existing is null)
            return MasterDataResult<object>.Fail(
                MasterDataErrorCode.NotFound, "Salesman not found.");

        var deleted = await _store.DeleteSalesmanAsync(tenantId, id, ct)
            .ConfigureAwait(false);
        if (deleted)
            await AuditAsync(tenantId, actorUserId, "masterdata.salesman_deleted",
                "salesmen", id, SalesmanSnapshot(existing), null, ip, ct)
                .ConfigureAwait(false);
        return MasterDataResult<object>.Ok(new object());
    }

    // ═════════════════════ shared helpers ════════════════════════════

    /// <summary>WPF editor semantics for the non-key company fields:
    /// latest data wins; blank clears; logo sizes fall back to the
    /// industrial defaults (22 × 26) when the operator leaves them
    /// blank so a 0 mm box can never reach the renderer.</summary>
    private static void ApplyCompanyFields(Company company, CompanyRequest request)
    {
        company.Location = Trim(request.Location);
        company.Representative = Trim(request.Representative);
        company.RepresentativeMobile = Trim(request.RepresentativeMobile);
        company.Email = Trim(request.Email);
        company.AccountNumber = Trim(request.AccountNumber);
        company.BankName = Trim(request.BankName);
        company.LogoStorageKey = Trim(request.LogoStorageKey);
        company.LogoLayout = CompanyLogoLayouts.IsValid(request.LogoLayout)
            ? request.LogoLayout
            : CompanyLogoLayouts.None;
        company.LogoWidthMm = request.LogoWidthMm > 0 ? request.LogoWidthMm : 22m;
        company.LogoHeightMm = request.LogoHeightMm > 0 ? request.LogoHeightMm : 26m;
        company.IsDefault = request.IsDefault;
    }

    private static Company NewCompany(Guid tenantId, CompanyRequest request) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = Trim(request.Name),
        Trn = Trim(request.Trn),
        Location = Trim(request.Location),
        Representative = Trim(request.Representative),
        RepresentativeMobile = Trim(request.RepresentativeMobile),
        Email = Trim(request.Email),
        AccountNumber = Trim(request.AccountNumber),
        BankName = Trim(request.BankName),
        LogoStorageKey = Trim(request.LogoStorageKey),
        LogoLayout = CompanyLogoLayouts.IsValid(request.LogoLayout)
            ? request.LogoLayout
            : CompanyLogoLayouts.None,
        LogoWidthMm = request.LogoWidthMm > 0 ? request.LogoWidthMm : 22m,
        LogoHeightMm = request.LogoHeightMm > 0 ? request.LogoHeightMm : 26m,
        IsDefault = request.IsDefault,
        UsageCount = 1,
        LastUsedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };

    private static void ApplyProjectFields(Project project, ProjectRequest request)
    {
        project.ContractNo = Trim(request.ContractNo);
        project.Reference = Trim(request.Reference);
        project.SiteDeliveryLocation = Trim(request.SiteDeliveryLocation);
        project.LpoNumber = Trim(request.LpoNumber);
    }

    /// <summary>Every upsert of an existing row counts as a use (WPF).</summary>
    private static void Touch(Company entity)
    {
        entity.UsageCount += 1;
        entity.LastUsedAt = DateTime.UtcNow;
    }

    private static void Touch(Client entity)
    {
        entity.UsageCount += 1;
        entity.LastUsedAt = DateTime.UtcNow;
    }

    private static void Touch(Project entity)
    {
        entity.UsageCount += 1;
        entity.LastUsedAt = DateTime.UtcNow;
    }

    private static void Touch(Salesman entity)
    {
        entity.UsageCount += 1;
        entity.LastUsedAt = DateTime.UtcNow;
    }

    /// <summary>Trim, treating null as "" (JSON can send null for any
    /// field; validation already bounded the length).</summary>
    private static string Trim(string? value) => value?.Trim() ?? "";

    private static void SetIfProvided(Action<string> set, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) set(value.Trim());
    }

    private static int NormalizePage(int? page) => page is > 0 ? page.Value : 1;
    private static int NormalizePageSize(int? pageSize) =>
        pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;

    private async Task AuditAsync(
        Guid tenantId, Guid? actorUserId, string action, string entityType,
        Guid entityId, object? before, object? after, string? ip, CancellationToken ct)
        => await _audit.WriteAsync(tenantId, actorUserId, action, entityType,
            entityId, ip, null,
            before is null ? null : JsonSerializer.Serialize(before, AuditJson),
            after is null ? null : JsonSerializer.Serialize(after, AuditJson),
            ct).ConfigureAwait(false);

    // ── snapshots (audit before/after payload — the WPF field-diff trail) ─

    private static object CompanySnapshot(Company c) => new
    {
        c.Name, c.Trn, c.Location, c.Representative, c.RepresentativeMobile,
        c.Email, c.AccountNumber, c.BankName, c.LogoStorageKey,
        c.LogoLayout, c.LogoWidthMm, c.LogoHeightMm, c.IsDefault
    };

    private static object ClientSnapshot(Client c) => new
    {
        c.CustomerName, c.CustomerTrn, c.ReferenceCode, c.SalesRepresentative,
        c.DocumentPreparedBy, c.BillingAddress, c.AttentionExecutiveName, c.Phone
    };

    private static object ProjectSnapshot(Project p) => new
    {
        p.IdentifierName, p.CustomerId, p.ContractNo, p.Reference,
        p.SiteDeliveryLocation, p.LpoNumber
    };

    private static object SalesmanSnapshot(Salesman s) => new { s.Name, s.Department };

    // ── DTO mapping ─────────────────────────────────────────────────

    private static CompanyDto ToDto(Company c) => new(
        c.Id, c.Name, c.Trn, c.Location, c.Representative, c.RepresentativeMobile,
        c.Email, c.AccountNumber, c.BankName, c.LogoStorageKey,
        c.LogoLayout, c.LogoWidthMm, c.LogoHeightMm,
        c.IsDefault, c.UsageCount, c.LastUsedAt, c.CreatedAt);

    private static ClientDto ToDto(Client c) => new(
        c.Id, c.CustomerName, c.CustomerTrn, c.ReferenceCode, c.SalesRepresentative,
        c.DocumentPreparedBy, c.BillingAddress, c.AttentionExecutiveName, c.Phone,
        c.UsageCount, c.LastUsedAt, c.CreatedAt);

    private static ProjectDto ToDto(Project p) => new(
        p.Id, p.IdentifierName, p.ContractNo, p.Reference,
        p.SiteDeliveryLocation, p.LpoNumber, p.CustomerId,
        p.UsageCount, p.LastUsedAt, p.CreatedAt);

    private static SalesmanDto ToDto(Salesman s) => new(
        s.Id, s.Name, s.Department, s.UsageCount, s.LastUsedAt, s.CreatedAt);

    // ── validation (WPF data annotations, ported verbatim) ───────────

    private static string? ValidateCompany(CompanyRequest? request)
    {
        if (request is null) return "Request body is required.";
        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 200)
            return "Company name is required (max 200 characters).";
        if (Trim(request.Trn).Length > 20)
            return "TRN must be at most 20 characters.";
        if (Trim(request.Location).Length > 300)
            return "Location must be at most 300 characters.";
        if (Trim(request.Representative).Length > 120)
            return "Representative must be at most 120 characters.";
        if (Trim(request.RepresentativeMobile).Length > 30)
            return "Representative mobile must be at most 30 characters.";
        if (Trim(request.Email).Length > 120)
            return "Email must be at most 120 characters.";
        if (Trim(request.AccountNumber).Length > 80)
            return "Account number must be at most 80 characters.";
        if (Trim(request.BankName).Length > 120)
            return "Bank name must be at most 120 characters.";
        if (Trim(request.LogoStorageKey).Length > 500)
            return "Logo storage key must be at most 500 characters.";
        if (!CompanyLogoLayouts.IsValid(request.LogoLayout))
            return "Logo layout must be one of: None, LeftPortrait, TopPanel.";
        return null;
    }

    private static string? ValidateClient(ClientRequest? request)
    {
        if (request is null) return "Request body is required.";
        var name = request.CustomerName?.Trim() ?? "";
        if (name.Length is < 1 or > 200)
            return "Customer name is required (max 200 characters).";
        if (Trim(request.CustomerTrn).Length > 20)
            return "Customer TRN must be at most 20 characters.";
        if (Trim(request.ReferenceCode).Length > 100)
            return "Reference code must be at most 100 characters.";
        if (Trim(request.SalesRepresentative).Length > 200)
            return "Sales representative must be at most 200 characters.";
        if (Trim(request.DocumentPreparedBy).Length > 200)
            return "Document prepared by must be at most 200 characters.";
        if (Trim(request.BillingAddress).Length > 500)
            return "Billing address must be at most 500 characters.";
        if (Trim(request.AttentionExecutiveName).Length > 200)
            return "Attention name must be at most 200 characters.";
        if (Trim(request.Phone).Length > 50)
            return "Phone must be at most 50 characters.";
        return null;
    }

    private static string? ValidateProject(ProjectRequest? request)
    {
        if (request is null) return "Request body is required.";
        var identifier = request.IdentifierName?.Trim() ?? "";
        if (identifier.Length is < 1 or > 200)
            return "Project identifier is required (max 200 characters).";
        if (Trim(request.ContractNo).Length > 100)
            return "Contract number must be at most 100 characters.";
        if (Trim(request.Reference).Length > 300)
            return "Project reference must be at most 300 characters.";
        if (Trim(request.SiteDeliveryLocation).Length > 300)
            return "Site delivery location must be at most 300 characters.";
        if (Trim(request.LpoNumber).Length > 100)
            return "LPO number must be at most 100 characters.";
        return null;
    }

    private static string? ValidateSalesman(SalesmanRequest? request)
    {
        if (request is null) return "Request body is required.";
        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 200)
            return "Salesman name is required (max 200 characters).";
        if (Trim(request.Department).Length > 100)
            return "Department must be at most 100 characters.";
        return null;
    }
}
