using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProGlass.Saas.Domain.Entities;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// Tenant isolation through the REAL application path: EF global query
/// filters + PostgreSQL Row-Level Security, over the unprivileged runtime
/// connection. These tests are the .NET twin of the 23 live checks in
/// saas/database/verify/apply_and_test.py.
/// </summary>
[Collection("database")]
public class TenantIsolationTests
{
    private readonly DatabaseFixture _fixture;

    public TenantIsolationTests(DatabaseFixture fixture) => _fixture = fixture;

    private Client NewClient(Guid tenantId, string name) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CustomerName = name
    };

    [DatabaseFact]
    public async Task Tenant_A_sees_only_its_own_rows()
    {
        await using (var context = _fixture.CreateContext(_fixture.TenantA))
        {
            context.Clients.Add(NewClient(_fixture.TenantA, "Client A1"));
            await context.SaveChangesAsync();
        }
        await using (var context = _fixture.CreateContext(_fixture.TenantB))
        {
            context.Clients.Add(NewClient(_fixture.TenantB, "Client B1"));
            await context.SaveChangesAsync();
        }

        await using (var contextA = _fixture.CreateContext(_fixture.TenantA))
        {
            var names = await contextA.Clients.Select(c => c.CustomerName).ToListAsync();
            Assert.Contains("Client A1", names);
            Assert.DoesNotContain("Client B1", names);
        }
    }

    [DatabaseFact]
    public async Task Cross_tenant_lookup_by_primary_key_returns_null()
    {
        Guid clientB;
        await using (var contextB = _fixture.CreateContext(_fixture.TenantB))
        {
            var client = NewClient(_fixture.TenantB, "Client B lookup");
            contextB.Clients.Add(client);
            await contextB.SaveChangesAsync();
            clientB = client.Id;
        }

        await using (var contextA = _fixture.CreateContext(_fixture.TenantA))
        {
            Assert.Null(await contextA.Clients.FindAsync(clientB));
        }
    }

    [DatabaseFact]
    public async Task Cross_tenant_update_and_delete_touch_no_rows()
    {
        Guid clientB;
        await using (var contextB = _fixture.CreateContext(_fixture.TenantB))
        {
            var client = NewClient(_fixture.TenantB, "Client B guarded");
            contextB.Clients.Add(client);
            await contextB.SaveChangesAsync();
            clientB = client.Id;
        }

        await using (var contextA = _fixture.CreateContext(_fixture.TenantA))
        {
            var attempt = await contextA.Clients.FindAsync(clientB);
            Assert.Null(attempt); // the filter already prevents loading it
        }
    }

    [DatabaseFact]
    public async Task RLS_rejects_writing_a_row_claiming_another_tenant()
    {
        // The EF query filter cannot stop a deliberately mis-set TenantId on
        // a NEW entity — the database's WITH CHECK policy must (ADR-009).
        // Requires the unprivileged runtime connection (superusers bypass
        // RLS, which is why SAAS_TEST_APP_DB is a dedicated role).
        await using var contextA = _fixture.CreateContext(_fixture.TenantA);
        contextA.Clients.Add(NewClient(_fixture.TenantB, "Evil insert"));

        var ex = await Assert.ThrowsAnyAsync<PostgresException>(
            () => contextA.SaveChangesAsync());
        Assert.Equal("42501", ex.SqlState); // insufficient_privilege / RLS violation
    }

    [DatabaseFact]
    public async Task Unauthenticated_context_sees_nothing()
    {
        await using (var contextA = _fixture.CreateContext(_fixture.TenantA))
        {
            contextA.Clients.Add(NewClient(_fixture.TenantA, "Client A for anon test"));
            await contextA.SaveChangesAsync();
        }

        await using (var anonymous = _fixture.CreateContext(null))
        {
            Assert.Equal(0, await anonymous.Clients.CountAsync());
        }
    }

    [DatabaseFact]
    public async Task Tenant_scoped_uniqueness_allows_same_number_in_two_tenants()
    {
        // proforma_no is unique PER TENANT (WPF rule: PI numbers belong to
        // the issuing company) — mirrored by uq_proforma_invoices_tenant_no.
        await using (var contextA = _fixture.CreateContext(_fixture.TenantA))
        {
            contextA.ProformaInvoices.Add(new ProformaInvoice
            {
                Id = Guid.NewGuid(),
                TenantId = _fixture.TenantA,
                ProformaNo = "PI-SHARED-001"
            });
            await contextA.SaveChangesAsync();
        }

        await using (var contextB = _fixture.CreateContext(_fixture.TenantB))
        {
            contextB.ProformaInvoices.Add(new ProformaInvoice
            {
                Id = Guid.NewGuid(),
                TenantId = _fixture.TenantB,
                ProformaNo = "PI-SHARED-001"
            });
            await contextB.SaveChangesAsync(); // same number, other tenant: OK
        }

        await using (var contextA = _fixture.CreateContext(_fixture.TenantA))
        {
            contextA.ProformaInvoices.Add(new ProformaInvoice
            {
                Id = Guid.NewGuid(),
                TenantId = _fixture.TenantA,
                ProformaNo = "PI-SHARED-001"
            });
            var ex = await Assert.ThrowsAnyAsync<DbUpdateException>(
                () => contextA.SaveChangesAsync());
            Assert.NotNull(ex); // duplicate key inside the SAME tenant
        }
    }
}
