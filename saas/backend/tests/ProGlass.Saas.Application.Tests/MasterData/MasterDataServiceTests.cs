using ProGlass.Saas.Application.MasterData;
using Xunit;

namespace ProGlass.Saas.Application.Tests;

/// <summary>
/// MasterDataService tests — the WPF MasterDataRepository semantics,
/// unit-locked (upsert keys, rename collision, preserve-on-blank, single
/// default, usage counting, ordering, search limit).
/// </summary>
public sealed class MasterDataServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    private readonly FakeMasterDataStore _store = new();
    private readonly FakeAuditWriter _audit = new();
    private readonly MasterDataService _sut;

    public MasterDataServiceTests()
    {
        _sut = new MasterDataService(_store, _audit);
    }

    private static CompanyRequest Company(
        string name = "Apex Glazing LLC", string trn = "100123456700003",
        string location = "Dubai, UAE", string layout = CompanyLogoLayouts.None,
        decimal width = 0, decimal height = 0, bool isDefault = false,
        string representative = "Juber", string email = "info@apex.example") => new(
        name, trn, location, representative, "+971500000001", email,
        "AE12 3456 7890", "Gulf Bank", "logos/apex.png", layout, width, height,
        isDefault);

    private static ClientRequest Client(
        string name = "Emirates Towers LLC", string trn = "100987654300003") => new(
        name, trn, "EMR-001", "Ahmed Al Mansouri", "Fatima",
        "Sheikh Zayed Road, Dubai", "Mr. Khaled", "+97142220000");

    private static ProjectRequest Project(
        string identifier = "SZR Tower Phase 2", Guid? customerId = null) => new(
        identifier, "428 SZR", "TOWER CLADDING PACKAGE", "Dubai Marina", "LPO-2680",
        customerId);

    // ── Companies ─────────────────────────────────────────────────

    [Fact]
    public async Task Company_save_inserts_with_defaults_and_audits_creation()
    {
        var result = await _sut.SaveCompanyAsync(
            Tenant, Actor, Company(width: 30, height: 40), "127.0.0.1",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Apex Glazing LLC", result.Value!.Name);
        var stored = _store.Companies.Single();
        Assert.Equal(1, stored.UsageCount);
        Assert.NotNull(stored.LastUsedAt);
        Assert.Equal(30m, stored.LogoWidthMm);   // operator intent kept
        Assert.Equal(40m, stored.LogoHeightMm);
        Assert.True(_audit.Has("masterdata.company_created"));
    }

    [Fact]
    public async Task Company_save_fills_industrial_logo_defaults_when_blank()
    {
        var result = await _sut.SaveCompanyAsync(
            Tenant, Actor, Company(width: 0, height: 0), null, CancellationToken.None);

        var stored = _store.Companies.Single();
        Assert.Equal(22m, stored.LogoWidthMm);   // WPF industrial defaults
        Assert.Equal(26m, stored.LogoHeightMm);
    }

    [Fact]
    public async Task Company_save_matches_the_natural_key_case_insensitively()
    {
        await _sut.SaveCompanyAsync(Tenant, Actor, Company(), null, CancellationToken.None);

        // Same name+TRN in different case → same record, usage grows.
        var again = await _sut.SaveCompanyAsync(
            Tenant, Actor, Company(name: "APEX GLAZING llc", trn: "100123456700003",
                location: "Sharjah, UAE"),
            null, CancellationToken.None);

        Assert.True(again.Success);
        Assert.Single(_store.Companies);
        Assert.Equal(2, _store.Companies[0].UsageCount);
        Assert.Equal("Sharjah, UAE", _store.Companies[0].Location); // latest wins
        Assert.True(_audit.Has("masterdata.company_updated"));
    }

    [Fact]
    public async Task Company_same_name_with_a_different_TRN_is_a_new_company()
    {
        await _sut.SaveCompanyAsync(
            Tenant, Actor, Company(), null, CancellationToken.None);
        await _sut.SaveCompanyAsync(
            Tenant, Actor, Company(trn: "100999999900003"), null, CancellationToken.None);

        Assert.Equal(2, _store.Companies.Count);
    }

    [Fact]
    public async Task Company_save_as_default_clears_other_defaults()
    {
        await _sut.SaveCompanyAsync(
            Tenant, Actor, Company(name: "First LLC", isDefault: true),
            null, CancellationToken.None);
        await _sut.SaveCompanyAsync(
            Tenant, Actor, Company(name: "Second LLC", trn: "100222222200003",
                isDefault: true),
            null, CancellationToken.None);

        Assert.Equal(2, _store.Companies.Count);
        Assert.False(_store.Companies[0].IsDefault); // single-default rule
        Assert.True(_store.Companies[1].IsDefault);
    }

    [Fact]
    public async Task Company_update_can_rename_when_no_other_row_owns_the_key()
    {
        var created = (await _sut.SaveCompanyAsync(
            Tenant, Actor, Company(), null, CancellationToken.None)).Value!;

        var renamed = await _sut.UpdateCompanyAsync(
            Tenant, created.Id, Actor, Company(name: "Apex Glazing Group LLC"),
            null, CancellationToken.None);

        Assert.True(renamed.Success);
        Assert.Equal("Apex Glazing Group LLC", _store.Companies[0].Name);
    }

    [Fact]
    public async Task Company_rename_onto_another_rows_key_is_refused()
    {
        await _sut.SaveCompanyAsync(
            Tenant, Actor, Company(name: "Apex LLC", trn: "100111111100003"),
            null, CancellationToken.None);
        var other = (await _sut.SaveCompanyAsync(
            Tenant, Actor, Company(name: "Gulf LLC", trn: "100222222200003"),
            null, CancellationToken.None)).Value!;

        // Rename Gulf LLC onto Apex's name+TRN → refused (WPF F-1b).
        var result = await _sut.UpdateCompanyAsync(
            Tenant, other.Id, Actor,
            Company(name: "Apex LLC", trn: "100111111100003"),
            null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(MasterDataErrorCode.RenameConflict, result.ErrorCode);
        Assert.Equal("Gulf LLC", _store.Companies[1].Name); // untouched
    }

    [Fact]
    public async Task Company_update_of_a_missing_id_is_not_found()
    {
        var result = await _sut.UpdateCompanyAsync(
            Tenant, Guid.NewGuid(), Actor, Company(), null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(MasterDataErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Company_invoice_autosave_never_erases_profile_values()
    {
        var created = (await _sut.SaveCompanyAsync(
            Tenant, Actor, Company(), null, CancellationToken.None)).Value!;
        var beforeDefault = _store.Companies[0].IsDefault;

        // Invoice header carries blanks — nothing stored may be erased.
        var enriched = await _sut.UpsertCompanyFromInvoiceAsync(
            Tenant, created.Id,
            new CompanyRequest("Apex Glazing LLC", "100123456700003",
                "", "", "", "", "", "", "", CompanyLogoLayouts.None, 0, 0, false),
            CancellationToken.None);

        Assert.Equal("Dubai, UAE", _store.Companies[0].Location);
        Assert.Equal("Juber", _store.Companies[0].Representative);
        Assert.Equal("info@apex.example", _store.Companies[0].Email);
        Assert.Equal(2, _store.Companies[0].UsageCount);
        Assert.Equal(beforeDefault, _store.Companies[0].IsDefault); // untouched
        Assert.Equal(enriched.Id, created.Id);
    }

    [Fact]
    public async Task Company_invoice_autosave_enriches_with_non_blank_values()
    {
        await _sut.SaveCompanyAsync(
            Tenant, Actor, Company(), null, CancellationToken.None);

        await _sut.UpsertCompanyFromInvoiceAsync(
            Tenant, null,
            new CompanyRequest("Apex Glazing LLC", "100123456700003",
                "Abu Dhabi, UAE", "", "", "", "", "", "",
                CompanyLogoLayouts.TopPanel, 40, 50, false),
            CancellationToken.None);

        Assert.Single(_store.Companies);
        Assert.Equal("Abu Dhabi, UAE", _store.Companies[0].Location);
        Assert.Equal(CompanyLogoLayouts.TopPanel, _store.Companies[0].LogoLayout);
        Assert.Equal(40m, _store.Companies[0].LogoWidthMm);
        Assert.Equal(50m, _store.Companies[0].LogoHeightMm);
        Assert.Equal(2, _store.Companies[0].UsageCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bogus")]
    public async Task Company_save_rejects_unknown_logo_layouts(string layout)
    {
        var result = await _sut.SaveCompanyAsync(
            Tenant, Actor, Company(layout: layout), null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(MasterDataErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Company_save_rejects_overlong_names()
    {
        var result = await _sut.SaveCompanyAsync(
            Tenant, Actor, Company(name: new string('X', 201)),
            null, CancellationToken.None);
        Assert.Equal(MasterDataErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Company_delete_missing_is_not_found_and_existing_is_deleted()
    {
        var missing = await _sut.DeleteCompanyAsync(
            Tenant, Guid.NewGuid(), Actor, null, CancellationToken.None);
        Assert.Equal(MasterDataErrorCode.NotFound, missing.ErrorCode);

        var created = (await _sut.SaveCompanyAsync(
            Tenant, Actor, Company(), null, CancellationToken.None)).Value!;
        var deleted = await _sut.DeleteCompanyAsync(
            Tenant, created.Id, Actor, null, CancellationToken.None);
        Assert.True(deleted.Success);
        Assert.Empty(_store.Companies);
        Assert.True(_audit.Has("masterdata.company_deleted"));
    }

    // ── Clients ───────────────────────────────────────────────────

    [Fact]
    public async Task Client_save_updates_non_key_fields_and_increments_usage()
    {
        await _sut.SaveClientAsync(
            Tenant, Actor, Client(), null, CancellationToken.None);
        var again = await _sut.SaveClientAsync(
            Tenant, Actor,
            Client(name: "EMIRATES TOWERS llc", trn: "100987654300003"),
            null, CancellationToken.None);

        Assert.True(again.Success);
        Assert.Single(_store.Clients);
        Assert.Equal(2, _store.Clients[0].UsageCount);
        Assert.Equal("Fatima", _store.Clients[0].DocumentPreparedBy);
        Assert.True(_audit.Has("masterdata.client_updated"));
    }

    [Fact]
    public async Task Client_key_fields_are_immutable_on_update_by_id()
    {
        var created = (await _sut.SaveClientAsync(
            Tenant, Actor, Client(), null, CancellationToken.None)).Value!;

        var updated = await _sut.UpdateClientAsync(
            Tenant, created.Id, Actor,
            Client(name: "Renamed LLC", trn: "100000000000003"),
            null, CancellationToken.None);

        Assert.True(updated.Success);
        Assert.Equal("Emirates Towers LLC", _store.Clients[0].CustomerName);
        Assert.Equal("100987654300003", _store.Clients[0].CustomerTrn);
    }

    [Fact]
    public async Task Client_different_trn_is_a_different_client()
    {
        await _sut.SaveClientAsync(Tenant, Actor, Client(), null, CancellationToken.None);
        await _sut.SaveClientAsync(
            Tenant, Actor, Client(trn: "100555555500003"), null, CancellationToken.None);
        Assert.Equal(2, _store.Clients.Count);
    }

    [Fact]
    public async Task Client_update_audits_before_and_after_payloads()
    {
        var created = (await _sut.SaveClientAsync(
            Tenant, Actor, Client(), null, CancellationToken.None)).Value!;
        await _sut.UpdateClientAsync(
            Tenant, created.Id, Actor,
            new ClientRequest("Emirates Towers LLC", "100987654300003",
                "EMR-002", "Sara", "Omar", "", "", ""),
            null, CancellationToken.None);

        var update = _audit.Events.Single(e => e.Action == "masterdata.client_updated");
        Assert.NotNull(update.BeforeJson);
        Assert.NotNull(update.AfterJson);
        Assert.Contains("EMR-001", update.BeforeJson);
        Assert.Contains("EMR-002", update.AfterJson);
    }

    // ── Projects ──────────────────────────────────────────────────

    [Fact]
    public async Task Project_key_is_identifier_plus_customer()
    {
        var customerA = (await _sut.SaveClientAsync(
            Tenant, Actor, Client(), null, CancellationToken.None)).Value!;
        var customerB = (await _sut.SaveClientAsync(
            Tenant, Actor, Client(name: "Other Client LLC", trn: "100777777700003"),
            null, CancellationToken.None)).Value!;

        var first = await _sut.SaveProjectAsync(
            Tenant, Actor, Project(customerId: customerA.Id), null, CancellationToken.None);
        var sameForB = await _sut.SaveProjectAsync(
            Tenant, Actor, Project(customerId: customerB.Id), null, CancellationToken.None);
        var again = await _sut.SaveProjectAsync(
            Tenant, Actor, Project(customerId: customerA.Id), null, CancellationToken.None);

        Assert.True(first.Success);
        Assert.True(sameForB.Success);
        Assert.True(again.Success);
        Assert.Equal(2, _store.Projects.Count); // same name, different customer → 2 rows
        Assert.Equal(2, _store.Projects
            .Single(p => p.CustomerId == customerA.Id).UsageCount);
    }

    [Fact]
    public async Task Project_list_can_be_scoped_to_one_customer()
    {
        var customer = (await _sut.SaveClientAsync(
            Tenant, Actor, Client(), null, CancellationToken.None)).Value!;
        await _sut.SaveProjectAsync(
            Tenant, Actor, Project(identifier: "Alpha", customerId: customer.Id),
            null, CancellationToken.None);
        await _sut.SaveProjectAsync(
            Tenant, Actor, Project(identifier: "Beta", customerId: null),
            null, CancellationToken.None);

        var scoped = await _sut.ListProjectsAsync(
            Tenant, customer.Id, null, null, null, CancellationToken.None);
        var all = await _sut.ListProjectsAsync(
            Tenant, null, null, null, null, CancellationToken.None);

        Assert.Single(scoped.Items);
        Assert.Equal("Alpha", scoped.Items[0].IdentifierName);
        Assert.Equal(2, all.Items.Count);
    }

    // ── Salesmen ──────────────────────────────────────────────────

    [Fact]
    public async Task Salesman_blank_department_never_erases_a_stored_one()
    {
        await _sut.SaveSalesmanAsync(
            Tenant, Actor, new SalesmanRequest("Ahmed Al Mansouri", "Facade Sales"),
            null, CancellationToken.None);

        var again = await _sut.SaveSalesmanAsync(
            Tenant, Actor, new SalesmanRequest("ahmed al mansouri", ""),
            null, CancellationToken.None);

        Assert.True(again.Success);
        Assert.Single(_store.Salesmen);
        Assert.Equal("Facade Sales", _store.Salesmen[0].Department); // preserved
        Assert.Equal(2, _store.Salesmen[0].UsageCount);
    }

    [Fact]
    public async Task Salesman_non_blank_department_wins()
    {
        await _sut.SaveSalesmanAsync(
            Tenant, Actor, new SalesmanRequest("Ahmed", "Retail"),
            null, CancellationToken.None);
        await _sut.SaveSalesmanAsync(
            Tenant, Actor, new SalesmanRequest("Ahmed", "Projects"),
            null, CancellationToken.None);
        Assert.Equal("Projects", _store.Salesmen[0].Department);
    }

    // ── List ordering, search, paging ─────────────────────────────

    [Fact]
    public async Task Lists_order_by_usage_then_recency()
    {
        var first = (await _sut.SaveSalesmanAsync(
            Tenant, Actor, new SalesmanRequest("A", ""), null, CancellationToken.None)).Value!;
        var second = (await _sut.SaveSalesmanAsync(
            Tenant, Actor, new SalesmanRequest("B", ""), null, CancellationToken.None)).Value!;
        await _sut.SaveSalesmanAsync(
            Tenant, Actor, new SalesmanRequest("B", ""), null, CancellationToken.None);

        var list = await _sut.ListSalesmenAsync(
            Tenant, null, 1, 50, CancellationToken.None);

        // B used twice → first; A once → second.
        Assert.Equal(second.Id, list.Items[0].Id);
        Assert.Equal(first.Id, list.Items[1].Id);
        Assert.Equal(2, list.TotalCount);
    }

    [Fact]
    public async Task Search_is_case_insensitive_contains_and_caps_at_the_WPF_limit()
    {
        for (var i = 0; i < MasterDataService.SearchLimit + 5; i++)
            await _sut.SaveClientAsync(
                Tenant, Actor, Client(name: $"Emirates Client {i:00} LLC"),
                null, CancellationToken.None);

        var hits = await _sut.ListClientsAsync(
            Tenant, "EMIRATES CLIENT", null, null, CancellationToken.None);

        Assert.Equal(MasterDataService.SearchLimit, hits.Items.Count);
        Assert.Equal(MasterDataService.SearchLimit, hits.TotalCount);
        Assert.All(hits.Items, dto =>
            Assert.Contains("Emirates Client", dto.CustomerName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_cap_is_not_a_total_cap_for_plain_lists()
    {
        for (var i = 0; i < 25; i++)
            await _sut.SaveClientAsync(
                Tenant, Actor, Client(name: $"Client {i:00} LLC", trn: $"100{i:011d}"),
                null, CancellationToken.None);

        var page1 = await _sut.ListClientsAsync(Tenant, null, 1, 20, CancellationToken.None);
        var page2 = await _sut.ListClientsAsync(Tenant, null, 2, 20, CancellationToken.None);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);
    }

    [Fact]
    public async Task Everything_is_scoped_by_the_explicit_tenant()
    {
        var otherTenant = Guid.NewGuid();
        await _sut.SaveClientAsync(
            Tenant, Actor, Client(), null, CancellationToken.None);

        var stranger = await _sut.GetClientAsync(
            otherTenant, _store.Clients[0].Id, CancellationToken.None);
        var strangerList = await _sut.ListClientsAsync(
            otherTenant, null, null, null, CancellationToken.None);

        Assert.Equal(MasterDataErrorCode.NotFound, stranger.ErrorCode);
        Assert.Empty(strangerList.Items);
    }
}
