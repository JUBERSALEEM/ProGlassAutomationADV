namespace ProGlass.Saas.Domain.Tenancy;

/// <summary>
/// The authenticated request's tenant identity.
///
/// CRITICAL RULE (master prompt §24): the tenant is derived from the
/// authenticated principal — NEVER from a client-supplied tenant id in a
/// body, query string or header. The auth middleware (Phase 4) is the only
/// writer of this context; everything downstream only reads it.
/// </summary>
public interface ITenantContext
{
    /// <summary>Current tenant. Guid.Empty means "no tenant" — and the EF
    /// query filters + RLS both fail closed in that case (no rows).</summary>
    Guid TenantId { get; }

    /// <summary>Authenticated user, when known.</summary>
    Guid? UserId { get; }

    /// <summary>Authenticated username, when known.</summary>
    string Username { get; }
}

/// <summary>
/// Mutable <see cref="ITenantContext"/> holder. Registered scoped; the auth
/// middleware sets it once per request after validating the JWT. Integration
/// tests construct it directly. An unset context sees nothing (fail closed).
/// </summary>
public sealed class AmbientTenantContext : ITenantContext
{
    public Guid TenantId { get; private set; } = Guid.Empty;
    public Guid? UserId { get; private set; }
    public string Username { get; private set; } = "";

    /// <summary>Throws when the id is empty — a request must never run
    /// "on no tenant" without the caller having decided that explicitly
    /// (platform/admin endpoints do not use this context at all).</summary>
    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be Guid.Empty.", nameof(tenantId));
        TenantId = tenantId;
    }

    public void SetUser(Guid? userId, string username)
    {
        UserId = userId;
        Username = username ?? "";
    }

    /// <summary>Clears the identity. After this, queries see no rows.</summary>
    public void Clear()
    {
        TenantId = Guid.Empty;
        UserId = null;
        Username = "";
    }
}
