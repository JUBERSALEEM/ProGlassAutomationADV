using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.MasterData;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// Master-data flow against live PostgreSQL: the real EF store (query
/// filters + RLS via the tenant-bound factory), the WPF upsert semantics
/// end to end, the single-default rule, the projects→clients FK
/// (ON DELETE SET NULL) and the audit before/after payloads.
/// </summary>
[Collection("database")]
public sealed class MasterDataFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly MasterDataService _sut;

    public MasterDataFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        _sut = new MasterDataService(
            new MasterDataStore(factory),
            new AuditWriter(factory, NullLogger<AuditWriter>.Instance));
    }

    private static CompanyRequest Company(string name) => new(
        name, "100123456700003", "Dubai, UAE", "Juber", "+971500000001",
        "info@example.com", "AE12 3456 7890", "Gulf Bank", "",
        CompanyLogoLayouts.LeftPortrait, 24, 28, false);

    [DatabaseFact]
    public async Task Full_master_data_flow_with_tenant_isolation()
    {
        var tenantA = _fixture.TenantA;
        var tenantB = _fixture.TenantB;

        // ── create ───────────────────────────────────────────────────
        var company = await _sut.SaveCompanyAsync(
            tenantA, null, Company($"Integration {Guid.NewGuid():N}"[..60]),
            "127.0.0.1", CancellationToken.None);
        Assert.True(company.Success, company.Message);
        Assert.Equal(CompanyLogoLayouts.LeftPortrait, company.Value!.LogoLayout);
        Assert.Equal(24m, company.Value.LogoWidthMm);

        var client = await _sut.SaveClientAsync(
            tenantA, null,
            new ClientRequest($"Client {Guid.NewGuid():N}"[..40], "100987654300003",
                "EMR-1", "Ahmed", "Fatima", "Dubai", "Khaled", "+97142220000"),
            null, CancellationToken.None);
        Assert.True(client.Success, client.Message);

        var project = await _sut.SaveProjectAsync(
            tenantA, null,
            new ProjectRequest("SZR Phase 2", "428", "Tower cladding",
                "Dubai Marina", "LPO-1", client.Value!.Id),
            null, CancellationToken.None);
        Assert.True(project.Success, project.Message);

        var salesman = await _sut.SaveSalesmanAsync(
            tenantA, null, new SalesmanRequest($"Sales {Guid.NewGuid():N}"[..30], "Facade"),
            null, CancellationToken.None);
        Assert.True(salesman.Success, salesman.Message);

        // ── upsert: same key updates instead of duplicating ─────────
        var again = await _sut.SaveSalesmanAsync(
            tenantA, null, new SalesmanRequest(salesman.Value!.Name, ""),
            null, CancellationToken.None);
        Assert.True(again.Success);
        Assert.Equal(2, again.Value!.UsageCount);
        Assert.Equal("Facade", again.Value.Department); // blank never erases

        var salesmanList = await _sut.ListSalesmenAsync(
            tenantA, null, 1, 50, CancellationToken.None);
        Assert.Single(salesmanList.Items); // still one row

        // ── tenant isolation: tenant B sees none of it ───────────────
        var strangerCompany = await _sut.GetCompanyAsync(
            tenantB, company.Value!.Id, CancellationToken.None);
        var strangerProjects = await _sut.ListProjectsAsync(
            tenantB, null, null, null, null, CancellationToken.None);
        Assert.Equal(MasterDataErrorCode.NotFound, strangerCompany.ErrorCode);
        Assert.DoesNotContain(project.Value!.Id,
            strangerProjects.Items.Select(p => p.Id));

        // ── audit trail with before/after payloads ──────────────────
        await using (var db = _fixture.CreateContext(tenantA))
        {
            var events = await db.AuditLogs
                .Where(a => a.EntityId == salesman.Value.Id)
                .ToListAsync();
            Assert.Contains(events, e => e.Action == "masterdata.salesman_created");
            Assert.Contains(events, e => e.Action == "masterdata.salesman_updated");
            var updated = events.First(e => e.Action == "masterdata.salesman_updated");
            Assert.NotNull(updated.AfterJson);
            Assert.Contains("Department", updated.AfterJson);
        }

        // ── delete ───────────────────────────────────────────────────
        var deleted = await _sut.DeleteProjectAsync(
            tenantA, project.Value.Id, null, null, CancellationToken.None);
        Assert.True(deleted.Success);
        var missing = await _sut.GetProjectAsync(
            tenantA, project.Value.Id, CancellationToken.None);
        Assert.Equal(MasterDataErrorCode.NotFound, missing.ErrorCode);
    }

    [DatabaseFact]
    public async Task Setting_a_default_company_clears_the_previous_default()
    {
        var tenant = _fixture.TenantA;

        var firstRequest = Company($"First {Guid.NewGuid():N}"[..60]);
        var first = await _sut.SaveCompanyAsync(
            tenant, null, firstRequest, null, CancellationToken.None);
        // make it the default through an edit
        var firstDefault = await _sut.UpdateCompanyAsync(
            tenant, first.Value!.Id, null, firstRequest with { IsDefault = true },
            null, CancellationToken.None);
        Assert.True(firstDefault.Success, firstDefault.Message);

        var secondRequest = Company($"Second {Guid.NewGuid():N}"[..60]);
        var second = await _sut.SaveCompanyAsync(
            tenant, null, secondRequest, null, CancellationToken.None);
        var secondDefault = await _sut.UpdateCompanyAsync(
            tenant, second.Value!.Id, null, secondRequest with { IsDefault = true },
            null, CancellationToken.None);
        Assert.True(secondDefault.Success, secondDefault.Message);

        await using var db = _fixture.CreateContext(tenant);
        var defaults = await db.Companies
            .Where(c => c.Name.StartsWith("First ") || c.Name.StartsWith("Second "))
            .Where(c => c.IsDefault)
            .ToListAsync();
        var single = Assert.Single(defaults);
        Assert.Equal(second.Value.Id, single.Id);
    }

    [DatabaseFact]
    public async Task Deleting_a_client_detaches_but_keeps_their_projects()
    {
        var tenant = _fixture.TenantA;
        var client = await _sut.SaveClientAsync(
            tenant, null,
            new ClientRequest($"Bye {Guid.NewGuid():N}"[..30], "100555555500003",
                "", "", "", "", "", ""),
            null, CancellationToken.None);
        Assert.True(client.Success, client.Message);
        var project = await _sut.SaveProjectAsync(
            tenant, null,
            new ProjectRequest($"Orphan {Guid.NewGuid():N}"[..30], "", "", "", "",
                client.Value!.Id),
            null, CancellationToken.None);
        Assert.True(project.Success, project.Message);

        var deleted = await _sut.DeleteClientAsync(
            tenant, client.Value.Id, null, null, CancellationToken.None);
        Assert.True(deleted.Success);

        // FK is ON DELETE SET NULL: the project survives, unassigned — the
        // WPF behavior (masterdata.db rows never touched invoices).
        var survivor = await _sut.GetProjectAsync(
            tenant, project.Value!.Id, CancellationToken.None);
        Assert.True(survivor.Success);
        Assert.Null(survivor.Value!.CustomerId);
    }

    [DatabaseFact]
    public async Task Renaming_a_company_onto_a_taken_key_is_refused_by_the_service()
    {
        var tenant = _fixture.TenantB;
        var request = Company($"Keep {Guid.NewGuid():N}"[..60]);
        var keeper = await _sut.SaveCompanyAsync(
            tenant, null, request, null, CancellationToken.None);
        Assert.True(keeper.Success, keeper.Message);

        var other = Company($"Other {Guid.NewGuid():N}"[..60]);
        var otherCompany = await _sut.SaveCompanyAsync(
            tenant, null, other, null, CancellationToken.None);
        Assert.True(otherCompany.Success, otherCompany.Message);

        // Rename "Other" onto "Keep"'s exact name+TRN.
        var result = await _sut.UpdateCompanyAsync(
            tenant, otherCompany.Value!.Id, null,
            Company(keeper.Value!.Name), null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(MasterDataErrorCode.RenameConflict, result.ErrorCode);
    }

    [DatabaseFact]
    public async Task updated_at_advances_on_master_data_updates()
    {
        var tenant = _fixture.TenantA;
        var request = Company($"Touch {Guid.NewGuid():N}"[..60]);
        var created = await _sut.SaveCompanyAsync(
            tenant, null, request, null, CancellationToken.None);
        Assert.True(created.Success, created.Message);

        await using (var db = _fixture.CreateContext(tenant))
        {
            var before = await db.Companies.AsNoTracking()
                .SingleAsync(c => c.Id == created.Value!.Id);
            var beforeUpdated = before.UpdatedAt;
            var beforeCreatedAt = before.CreatedAt;

            var sameKey = Company(request.Name); // same natural key → update
            await _sut.SaveCompanyAsync(tenant, null, sameKey, null, CancellationToken.None);

            var after = await db.Companies.AsNoTracking()
                .SingleAsync(c => c.Id == created.Value.Id);
            Assert.True(after.UpdatedAt > beforeUpdated,
                $"updated_at must advance ({beforeUpdated:O} → {after.UpdatedAt:O})");
            Assert.Equal(beforeCreatedAt, after.CreatedAt); // creation is immutable
            Assert.Equal(2, after.UsageCount);
        }
    }
}
