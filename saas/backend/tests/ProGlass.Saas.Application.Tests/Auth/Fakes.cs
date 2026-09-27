using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Application.Security;

namespace ProGlass.Saas.Application.Tests;

/// <summary>
/// In-memory fakes for every auth port. They implement the SAME contract
/// semantics as the Infrastructure implementations (tenant-scoped lookups,
/// mechanical state writes, revocation bookkeeping) so the AuthService
/// tests exercise the orchestration logic — lockout policy, rotation,
/// reuse detection, absolute lifetimes — without a database. Live
/// PostgreSQL coverage lives in ProGlass.Saas.IntegrationTests.
/// </summary>
public sealed class FakeUserStore : IUserStore
{
    public sealed class Row
    {
        public required Guid TenantId { get; init; }
        public required Guid UserId { get; init; }
        public required string Username { get; init; }
        public required string DisplayName { get; init; }
        public string Email { get; init; } = "";
        public string Role { get; init; } = "Administrator";
        public required string PasswordHash { get; set; }
        public bool IsActive { get; set; } = true;
        public bool MustResetPassword { get; set; }
        public int FailedAttemptCount { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public Dictionary<Guid, Row> Users { get; } = new();
    public Dictionary<string, Guid> SlugToTenant { get; } = new(StringComparer.Ordinal);
    public HashSet<string> TakenSlugs { get; } = new(StringComparer.Ordinal);

    public Task<Guid?> FindTenantIdBySlugAsync(string tenantSlug, CancellationToken ct)
        => Task.FromResult<Guid?>(SlugToTenant.TryGetValue(
            tenantSlug.Trim().ToLowerInvariant(), out var id) ? id : null);

    public Task<LoginUser?> FindLoginAsync(
        Guid tenantId, string usernameOrEmail, CancellationToken ct)
    {
        var login = usernameOrEmail.Trim();
        var email = login.ToLowerInvariant();
        var row = Users.Values.FirstOrDefault(r =>
            r.TenantId == tenantId &&
            (r.Username == login || (Email(r) != null && Email(r) == email)));
        return Task.FromResult(row is null ? null : Snapshot(row));
    }

    public Task<LoginUser?> FindByIdAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        var row = Users.GetValueOrDefault(userId);
        return Task.FromResult(row is null || row.TenantId != tenantId
            ? null
            : Snapshot(row));
    }

    public Task SaveLoginStateAsync(
        Guid tenantId, Guid userId,
        int failedAttemptCount, DateTime? lockedUntilUtc, DateTime? lastLoginAtUtc,
        CancellationToken ct)
    {
        var row = Users[userId];
        row.FailedAttemptCount = failedAttemptCount;
        row.LockedUntilUtc = lockedUntilUtc;
        if (lastLoginAtUtc is not null)
            row.LastLoginAt = lastLoginAtUtc;
        return Task.CompletedTask;
    }

    public Task UpdatePasswordHashAsync(
        Guid tenantId, Guid userId, string passwordHash,
        bool clearMustResetPassword, CancellationToken ct)
    {
        var row = Users[userId];
        row.PasswordHash = passwordHash;
        if (clearMustResetPassword)
            row.MustResetPassword = false;
        return Task.CompletedTask;
    }

    public Task<bool> IsSlugTakenAsync(string tenantSlug, CancellationToken ct)
        => Task.FromResult(TakenSlugs.Contains(tenantSlug.Trim().ToLowerInvariant()));

    private static string? Email(Row row) => string.IsNullOrEmpty(row.Email) ? null : row.Email;

    public static LoginUser Snapshot(Row row) => new(
        row.TenantId, row.UserId, row.Username, row.DisplayName,
        row.Email, row.Role, row.PasswordHash,
        row.IsActive, row.MustResetPassword,
        row.FailedAttemptCount, row.LockedUntilUtc, row.LastLoginAt);
}

public sealed class FakeRefreshTokenStore : IRefreshTokenStore
{
    public List<RefreshTokenRecord> Tokens { get; } = new();

    public Task CreateAsync(
        Guid tenantId, Guid userId, string tokenHash,
        DateTime expiresAtUtc, DateTime absoluteExpiresAtUtc, string? createdIp,
        CancellationToken ct)
    {
        Tokens.Add(new RefreshTokenRecord(
            Guid.NewGuid(), tenantId, userId, tokenHash,
            DateTime.UtcNow, expiresAtUtc, absoluteExpiresAtUtc,
            null, null, createdIp));
        return Task.CompletedTask;
    }

    public Task<RefreshTokenRecord?> FindActiveByHashAsync(
        Guid tenantId, string tokenHash, CancellationToken ct)
        => Task.FromResult(Tokens.FirstOrDefault(t =>
            t.TenantId == tenantId && t.TokenHash == tokenHash &&
            t.RevokedAtUtc is null && t.ExpiresAtUtc > DateTime.UtcNow));

    public Task<RefreshTokenRecord?> FindAnyByHashAsync(
        Guid tenantId, string tokenHash, CancellationToken ct)
        => Task.FromResult(Tokens.FirstOrDefault(t =>
            t.TenantId == tenantId && t.TokenHash == tokenHash));

    public Task RevokeAsync(
        Guid tenantId, Guid id, string replacedByTokenHash, CancellationToken ct)
    {
        var token = Tokens.FirstOrDefault(t => t.Id == id);
        if (token is not null)
        {
            var index = Tokens.IndexOf(token);
            Tokens[index] = token with
            {
                RevokedAtUtc = DateTime.UtcNow,
                ReplacedByTokenHash = string.IsNullOrEmpty(replacedByTokenHash)
                    ? null
                    : replacedByTokenHash
            };
        }
        return Task.CompletedTask;
    }

    public Task<int> RevokeAllForUserAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        var count = 0;
        for (var i = 0; i < Tokens.Count; i++)
        {
            if (Tokens[i].TenantId == tenantId && Tokens[i].UserId == userId
                && Tokens[i].RevokedAtUtc is null)
            {
                Tokens[i] = Tokens[i] with { RevokedAtUtc = DateTime.UtcNow };
                count++;
            }
        }
        return Task.FromResult(count);
    }

    public Task<IReadOnlyList<RefreshTokenRecord>> ListForUserAsync(
        Guid tenantId, Guid userId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<RefreshTokenRecord>>(Tokens
            .Where(t => t.TenantId == tenantId && t.UserId == userId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToList());
}

/// <summary>
/// Provisions into the paired FakeUserStore — the REAL TenantProvisioner
/// writes tenant + user rows, and AuthService then reads the user back;
/// the fake mirrors that so the register flow is exercised end to end.
/// </summary>
public sealed class FakeTenantProvisioner : ITenantProvisioner
{
    private readonly FakeUserStore _users;

    public FakeTenantProvisioner(FakeUserStore users) => _users = users;

    public List<(string TenantName, string Slug, string Username, string Email)> Calls { get; } = new();

    public Task<ProvisionedTenant> ProvisionAsync(
        string tenantName, string tenantSlug,
        string ownerUsername, string ownerDisplayName,
        string ownerEmail, string ownerPasswordHash,
        string? ip, CancellationToken ct)
    {
        Calls.Add((tenantName, tenantSlug, ownerUsername, ownerEmail));

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _users.SlugToTenant[tenantSlug] = tenantId;
        _users.TakenSlugs.Add(tenantSlug);
        _users.Users[userId] = new FakeUserStore.Row
        {
            TenantId = tenantId,
            UserId = userId,
            Username = ownerUsername,
            DisplayName = ownerDisplayName,
            Email = ownerEmail,
            Role = "Administrator",
            PasswordHash = ownerPasswordHash
        };
        return Task.FromResult(new ProvisionedTenant(tenantId, userId));
    }
}

public sealed class FakeTokenService : ITokenService
{
    public int Calls { get; private set; }

    public (string Token, DateTime ExpiresAtUtc) IssueAccessToken(LoginUser user)
    {
        Calls++;
        return ($"access:{user.UserId:N}", DateTime.UtcNow.AddMinutes(15));
    }
}

/// <summary>Instant deterministic "hashing" (unit tests only — Argon2
/// round-trips are covered in IntegrationTests, which reference the real
/// Argon2idPasswordHasher). Counts Verify calls so tests can assert the
/// timing-equalization dummy verify runs on unknown-tenant/user paths.</summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public int VerifyCalls { get; private set; }

    public string Hash(string password) => "fake$" + password;

    public bool Verify(string storedHash, string password)
    {
        VerifyCalls++;
        return storedHash == "fake$" + password;
    }
}

public sealed class FakeAuditWriter : IAuditWriter
{
    public sealed record Event(
        Guid? TenantId, Guid? UserId, string Action,
        string EntityType, Guid? EntityId, string? Ip);

    public List<Event> Events { get; } = new();

    public Task WriteAsync(
        Guid? tenantId, Guid? userId, string action,
        string entityType, Guid? entityId, string? ip, string? correlationId,
        CancellationToken ct)
    {
        Events.Add(new Event(tenantId, userId, action, entityType, entityId, ip));
        return Task.CompletedTask;
    }

    public bool Has(string action) => Events.Any(e => e.Action == action);
}
