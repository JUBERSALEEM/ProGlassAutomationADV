using Npgsql;
using ProGlass.Saas.Domain.Tenancy;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>Shared per-run state: fresh tenant ids so tests are re-runnable
/// against a persistent database without cleanup steps.</summary>
public sealed class DatabaseFixture
{
    public string OwnerDb { get; }
    public string AppDb { get; }
    public Guid TenantA { get; } = Guid.NewGuid();
    public Guid TenantB { get; } = Guid.NewGuid();

    public DatabaseFixture()
    {
        OwnerDb = Environment.GetEnvironmentVariable("SAAS_TEST_DB") ?? "";
        AppDb = Environment.GetEnvironmentVariable("SAAS_TEST_APP_DB") ?? "";
        if (string.IsNullOrEmpty(OwnerDb) || string.IsNullOrEmpty(AppDb))
            throw new InvalidOperationException("SAAS_TEST_DB/SAAS_TEST_APP_DB missing");

        // Apply (or verify) the full schema — same runner, same files the
        // Python harness verified against live PostgreSQL.
        new SqlMigrationRunner(OwnerDb).ApplyAsync().GetAwaiter().GetResult();

        CreateTenants();
    }

    private void CreateTenants()
    {
        using var connection = new NpgsqlConnection(OwnerDb);
        connection.Open();
        foreach (var (tenantId, label) in new[]
                 { (TenantA, "A"), (TenantB, "B") })
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO saas.tenants (id, name, slug)
                VALUES ($1, $2, $3) ON CONFLICT (id) DO NOTHING
                """;
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue($"Integration Tenant {label}");
            command.Parameters.AddWithValue($"it-{tenantId:N}");
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// A fresh tenant row for tests that own a whole document set (pricing
    /// config, proforma invoices) — one row per tenant means sharing
    /// TenantA would make outcomes depend on test order. Inserted through
    /// the owner connection, like TenantA/TenantB.
    /// </summary>
    public Guid CreateTenant(string label)
    {
        var tenantId = Guid.NewGuid();
        using var connection = new NpgsqlConnection(OwnerDb);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO saas.tenants (id, name, slug)
            VALUES ($1, $2, $3)
            """;
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue($"{label} {tenantId:N}"[..60]);
        command.Parameters.AddWithValue($"t-{tenantId:N}"[..40]);
        command.ExecuteNonQuery();
        return tenantId;
    }

    /// <summary>
    /// A DbContext bound to a tenant through the REAL runtime path:
    /// AmbientTenantContext + TenantSessionInterceptor (RLS GUC) over the
    /// unprivileged application connection. tenantId == null simulates an
    /// unauthenticated request — it must see nothing.
    /// </summary>
    public SaasDbContext CreateContext(Guid? tenantId)
    {
        var ambient = new AmbientTenantContext();
        if (tenantId is not null)
        {
            ambient.SetTenant(tenantId.Value);
            ambient.SetUser(Guid.NewGuid(), "integration-test");
        }

        var options = new DbContextOptionsBuilder<SaasDbContext>()
            .UseNpgsql(AppDb)
            .AddInterceptors(new TenantSessionInterceptor(ambient))
            .Options;

        return new SaasDbContext(options, ambient);
    }
}

[CollectionDefinition("database")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
