using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Domain.Tenancy;

namespace ProGlass.Saas.Infrastructure.Persistence;

/// <summary>
/// Creates short-lived <see cref="SaasDbContext"/> instances for the auth
/// data stores. The normal request pipeline uses the scoped
/// ITenantContext + AddDbContext path (Program.cs); login/registration
/// runs BEFORE any authenticated context exists, so the auth stores build
/// their own contexts bound to an EXPLICIT tenant id.
///
/// Registered as a singleton — it is immutable (just the connection
/// string); a context per operation means no scoped ITenantContext is ever
/// captured by a singleton (the AddDbContextFactory captive-dependency
/// trap — see ADR-011).
/// </summary>
public interface ISaasDbContextFactory
{
    /// <summary>Context whose query filter + RLS session GUC are bound to
    /// the given tenant. Throws on Guid.Empty (fail closed).</summary>
    SaasDbContext CreateForTenant(Guid tenantId);

    /// <summary>Context with NO tenant binding: platform tables only
    /// (tenants, plans, features — not row-secured, 0005_rls.sql). Tenant
    /// tables see NOTHING through it: the GUC stays unset, so RLS fails
    /// closed, and the EF query filter matches no row.</summary>
    SaasDbContext CreatePlatform();
}

public sealed class SaasDbContextFactory : ISaasDbContextFactory
{
    private readonly string _connectionString;

    public SaasDbContextFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public SaasDbContext CreateForTenant(Guid tenantId)
    {
        var ambient = new AmbientTenantContext();
        ambient.SetTenant(tenantId); // throws on Guid.Empty — fail closed
        return Build(ambient);
    }

    public SaasDbContext CreatePlatform()
    {
        // Unset context: TenantId == Guid.Empty. The interceptor skips the
        // GUC and the query filter matches nothing — both layers fail closed.
        return Build(new AmbientTenantContext());
    }

    private SaasDbContext Build(AmbientTenantContext ambient)
    {
        var options = new DbContextOptionsBuilder<SaasDbContext>()
            .UseNpgsql(_connectionString)
            .AddInterceptors(new TenantSessionInterceptor(ambient))
            .Options;
        return new SaasDbContext(options, ambient);
    }
}
