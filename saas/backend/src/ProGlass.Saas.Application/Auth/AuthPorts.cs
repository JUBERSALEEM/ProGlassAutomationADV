namespace ProGlass.Saas.Application.Auth;

/// <summary>
/// Data port for user lookups and credential/lockout state changes.
/// Implementations own their tenant-scoped database access; every method
/// takes the tenant id EXPLICITLY (never ambient) so the login flow —
/// which runs before any authenticated context exists — works identically
/// to the post-login flows.
/// </summary>
public interface IUserStore
{
    /// <summary>Resolves a tenant by its login slug (the tenant-scoped
    /// login form asks for company code + username).</summary>
    Task<Guid?> FindTenantIdBySlugAsync(string tenantSlug, CancellationToken ct);

    Task<LoginUser?> FindLoginAsync(Guid tenantId, string usernameOrEmail, CancellationToken ct);

    Task<LoginUser?> FindByIdAsync(Guid tenantId, Guid userId, CancellationToken ct);

    /// <summary>Persists login state changes. The POLICY (WPF lockout
    /// semantics: increment, lock at 5, reset counter when the window
    /// triggers, clear on success) lives in AuthService so it is unit-
    /// tested in one place; this method is purely mechanical persistence.</summary>
    Task SaveLoginStateAsync(
        Guid tenantId, Guid userId,
        int failedAttemptCount, DateTime? lockedUntilUtc, DateTime? lastLoginAtUtc,
        CancellationToken ct);

    /// <summary>Updates the stored hash (registration, password change)
    /// and clears must_reset_password when requested.</summary>
    Task UpdatePasswordHashAsync(
        Guid tenantId, Guid userId, string passwordHash,
        bool clearMustResetPassword, CancellationToken ct);

    Task<bool> IsSlugTakenAsync(string tenantSlug, CancellationToken ct);
}

/// <summary>
/// Refresh token persistence. Tokens are stored HASHED (SHA-256 of the
/// secret); the raw secret exists only in the HTTP response and the user's
/// device. The refresh token string itself carries the tenant id
/// ("v1.{tenantId}.{secret}") because refresh_tokens is row-level-security
/// scoped — the tenant must be known before the row can be found.
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>Creates a new token row. <paramref name="absoluteExpiresAtUtc"/>
    /// is the lineage deadline: the FIRST token of a session gets
    /// now + AbsoluteRefreshTokenDays and every rotation passes the
    /// ORIGINAL value through unchanged (never extended) — that is what
    /// makes the lifetime absolute rather than sliding.</summary>
    Task CreateAsync(Guid tenantId, Guid userId, string tokenHash,
        DateTime expiresAtUtc, DateTime absoluteExpiresAtUtc, string? createdIp,
        CancellationToken ct);

    /// <summary>Active (not revoked, not expired) token by hash.</summary>
    Task<RefreshTokenRecord?> FindActiveByHashAsync(
        Guid tenantId, string tokenHash, CancellationToken ct);

    /// <summary>Any token by hash, including revoked — for reuse detection.</summary>
    Task<RefreshTokenRecord?> FindAnyByHashAsync(
        Guid tenantId, string tokenHash, CancellationToken ct);

    Task RevokeAsync(Guid tenantId, Guid id, string replacedByTokenHash, CancellationToken ct);

    /// <summary>Revokes every active token for a user (reuse detection,
    /// password change, admin force-logout). Returns the count revoked.</summary>
    Task<int> RevokeAllForUserAsync(Guid tenantId, Guid userId, CancellationToken ct);

    Task<IReadOnlyList<RefreshTokenRecord>> ListForUserAsync(
        Guid tenantId, Guid userId, CancellationToken ct);
}

/// <summary>
/// Creates a tenant and its Owner user. This runs BEFORE any tenant
/// context exists and writes to the platform table `tenants` (which the
/// runtime role cannot INSERT), so implementations use the elevated
/// platform connection (see Infrastructure/TenantProvisioner).
/// </summary>
public interface ITenantProvisioner
{
    Task<ProvisionedTenant> ProvisionAsync(
        string tenantName, string tenantSlug, string ownerUsername,
        string ownerDisplayName, string ownerEmail, string ownerPasswordHash,
        string? ip, CancellationToken ct);
}

/// <summary>Issues JWT access tokens. Claims: sub (user), tid (tenant),
/// role, preferred_username — the same claims TenantContextMiddleware reads.</summary>
public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) IssueAccessToken(LoginUser user);
}

/// <summary>
/// Audit port. Events with a tenant id are written to saas.audit_logs;
/// events without one (e.g. a login attempt against an unknown tenant
/// slug) cannot pass row-level security and are logged to the application
/// log instead — never silently dropped.
/// </summary>
public interface IAuditWriter
{
    Task WriteAsync(Guid? tenantId, Guid? userId, string action,
        string entityType, Guid? entityId, string? ip, string? correlationId,
        CancellationToken ct);

    /// <summary>Same event, with a before/after payload (the WPF Activity
    /// log's field-level change trail — stored in audit_logs.before_json /
    /// after_json). Pass null for either side when not applicable
    /// (creation has no "before", deletion no "after").</summary>
    Task WriteAsync(Guid? tenantId, Guid? userId, string action,
        string entityType, Guid? entityId, string? ip, string? correlationId,
        string? beforeJson, string? afterJson, CancellationToken ct);
}
