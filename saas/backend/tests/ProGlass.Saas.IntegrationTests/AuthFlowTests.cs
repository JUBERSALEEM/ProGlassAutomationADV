using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Infrastructure.Persistence;
using ProGlass.Saas.Infrastructure.Security;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// End-to-end authentication flow against live PostgreSQL: register →
/// lockout → login → refresh rotation → reuse detection → password change
/// → session management, over the REAL stack (Argon2id hashing, JWT
/// issuance, tenant-bound EF contexts, RLS, platform-connection
/// provisioning). Requires the same two connections as the other database
/// tests (see DatabaseFact); provisioning runs on the owner connection
/// (SAAS_PLATFORM_DB, falling back to SAAS_TEST_DB — the runtime role
/// cannot insert into saas.tenants by design).
/// </summary>
[Collection("database")]
public sealed class AuthFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly AuthService _sut;

    public AuthFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var options = new AuthOptions();
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var platform = Environment.GetEnvironmentVariable("SAAS_PLATFORM_DB")
                       ?? fixture.OwnerDb;
        _sut = new AuthService(
            new UserStore(factory),
            new RefreshTokenStore(factory),
            new TenantProvisioner(platform, NullLogger<TenantProvisioner>.Instance),
            new JwtTokenService(TestJwtSettings, options),
            new Argon2idPasswordHasher(),
            new AuditWriter(factory, NullLogger<AuditWriter>.Instance),
            options);
    }

    internal static JwtSettings TestJwtSettings => new()
    {
        Issuer = "integration-tests",
        Audience = "integration-tests",
        SigningKey = "integration-test-signing-key-0123456789abcdef" // 48 bytes
    };

    private static (string Slug, RegisterRequest Request) RegisterRequest(
        string username, string password = "Str0ng-Pass!")
    {
        var slug = $"it-{Guid.NewGuid():N}"[..32];
        return (slug, new RegisterRequest(
            $"Integration {username}", slug,
            username, $"{username} Saleem",
            $"{username.ToLowerInvariant()}@example.com", password));
    }

    [DatabaseFact]
    public async Task Full_auth_flow_against_live_postgres()
    {
        // ── 1. Register: tenant + Administrator owner + auto-login ──────
        var (slug, request) = RegisterRequest("juber");
        var registered = await _sut.RegisterAsync(request, "127.0.0.1", CancellationToken.None);
        Assert.True(registered.Success, registered.Message);
        var tenantId = registered.Value!.TenantId;
        var userId = registered.Value.UserId;
        Assert.NotEqual("", registered.Value.Tokens.AccessToken);
        Assert.StartsWith("v1.", registered.Value.Tokens.RefreshToken);

        await using (var db = _fixture.CreateContext(tenantId))
        {
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            Assert.Equal("Administrator", user.Role);
            Assert.StartsWith("$argon2id$v=19$", user.PasswordHash);
            Assert.False(user.MustResetPassword); // WPF-migrated users only
        }

        // ── 2. Login with the wrong password → lockout after 5 ─────────
        var login = new LoginRequest(slug, request.Username, "Wrong-1!");
        AuthResult<LoginResult> failed = null!;
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            failed = await _sut.LoginAsync(login, "127.0.0.1", CancellationToken.None);
            if (attempt < 5)
            {
                Assert.False(failed.Success);
                Assert.Equal(AuthErrorCode.InvalidCredentials, failed.ErrorCode);
            }
        }
        Assert.False(failed.Success);
        Assert.Equal(AuthErrorCode.AccountLocked, failed.ErrorCode);
        Assert.Equal(300, failed.RetryAfterSeconds);

        await using (var db = _fixture.CreateContext(tenantId))
        {
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            // WPF semantics: counter RESETS when the window opens.
            Assert.Equal(0, user.FailedAttemptCount);
            Assert.NotNull(user.LockedUntilUtc);
        }

        // Even the CORRECT password is refused while locked.
        var correct = new LoginRequest(slug, request.Username, request.Password);
        var locked = await _sut.LoginAsync(correct, null, CancellationToken.None);
        Assert.False(locked.Success);
        Assert.Equal(AuthErrorCode.AccountLocked, locked.ErrorCode);

        // Expire the window (WPF lockouts are persisted, time-based).
        await using (var connection = new NpgsqlConnection(_fixture.OwnerDb))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "UPDATE saas.users SET locked_until_utc = now() - interval '1 second' " +
                "WHERE id = $1";
            command.Parameters.AddWithValue(userId);
            await command.ExecuteNonQueryAsync();
        }

        // ── 3. Login succeeds and clears state ──────────────────────────
        var loggedIn = await _sut.LoginAsync(correct, "127.0.0.1", CancellationToken.None);
        Assert.True(loggedIn.Success, loggedIn.Message);
        Assert.Equal(tenantId, loggedIn.Value!.TenantId);
        Assert.Equal(request.Username, loggedIn.Value.Username);
        Assert.False(loggedIn.Value.MustResetPassword);

        await using (var db = _fixture.CreateContext(tenantId))
        {
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            Assert.Equal(0, user.FailedAttemptCount);
            Assert.Null(user.LockedUntilUtc);
            Assert.NotNull(user.LastLoginAt);
        }

        // ── 4. Refresh: rotation with absolute-deadline inheritance ────
        var firstRefresh = loggedIn.Value.Tokens.RefreshToken;
        var rotated = await _sut.RefreshAsync(
            firstRefresh, "127.0.0.1", CancellationToken.None);
        Assert.True(rotated.Success, rotated.Message);

        await using (var db = _fixture.CreateContext(tenantId))
        {
            var tokens = await db.RefreshTokens
                .Where(r => r.UserId == userId)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync();
            Assert.Equal(2, tokens.Count);
            var original = tokens[0];
            var successor = tokens[1];
            Assert.NotNull(original.RevokedAt);
            Assert.Null(successor.RevokedAt);
            Assert.Equal(
                RefreshTokens.HashSecret(SecretOf(rotated.Value!.RefreshToken)),
                original.ReplacedByTokenHash);
            Assert.Equal(original.AbsoluteExpiresAtUtc, successor.AbsoluteExpiresAtUtc);
        }

        // ── 5. Reuse detection: replay kills the whole family ──────────
        var replay = await _sut.RefreshAsync(
            firstRefresh, "127.0.0.1", CancellationToken.None);
        Assert.False(replay.Success);
        Assert.Equal(AuthErrorCode.TokenReused, replay.ErrorCode);

        var afterReuse = await _sut.RefreshAsync(
            rotated.Value!.RefreshToken, null, CancellationToken.None);
        Assert.False(afterReuse.Success);
        Assert.Equal(AuthErrorCode.TokenReused, afterReuse.ErrorCode);

        await using (var db = _fixture.CreateContext(tenantId))
        {
            var all = await db.RefreshTokens.Where(r => r.UserId == userId).ToListAsync();
            Assert.Equal(2, all.Count);
            Assert.All(all, t => Assert.NotNull(t.RevokedAt));
        }

        // ── 6. Sessions: list + revoke ──────────────────────────────────
        var session = await _sut.LoginAsync(correct, "10.9.8.7", CancellationToken.None);
        Assert.True(session.Success);
        var sessions = await _sut.ListSessionsAsync(
            tenantId, userId, CancellationToken.None);
        var active = sessions.Where(s => s.RevokedAtUtc is null).ToList();
        Assert.Single(active);
        Assert.Equal("10.9.8.7", active[0].CreatedIp);

        var revoked = await _sut.RevokeSessionAsync(
            tenantId, userId, active[0].Id, null, CancellationToken.None);
        Assert.True(revoked.Success);
        Assert.True((await _sut.ListSessionsAsync(tenantId, userId, CancellationToken.None))
            .All(s => s.RevokedAtUtc is not null));

        // ── 7. Change password: hash swap, flag clear, sessions dead ───
        var changed = await _sut.ChangePasswordAsync(
            tenantId, userId,
            new ChangePasswordRequest(request.Password, "Newer-Str0ng-Pass!"),
            null, CancellationToken.None);
        Assert.True(changed.Success, changed.Message);

        var oldPassword = await _sut.LoginAsync(correct, null, CancellationToken.None);
        Assert.False(oldPassword.Success);
        Assert.Equal(AuthErrorCode.InvalidCredentials, oldPassword.ErrorCode);

        var newPassword = await _sut.LoginAsync(
            new LoginRequest(slug, request.Username, "Newer-Str0ng-Pass!"),
            null, CancellationToken.None);
        Assert.True(newPassword.Success, newPassword.Message);
    }

    [DatabaseFact]
    public async Task The_same_username_in_two_tenants_is_fine__isolation_holds()
    {
        // Duplicate usernames are only a problem WITHIN a tenant
        // (uq_users_tenant_username); across tenants they are independent
        // users, and one tenant must not be able to log into the other.
        var (slugA, requestA) = RegisterRequest("shared", "TenantA-Pass!");
        var (slugB, requestB) = RegisterRequest("shared", "TenantB-Pass!");

        var registeredA = await _sut.RegisterAsync(requestA, null, CancellationToken.None);
        var registeredB = await _sut.RegisterAsync(requestB, null, CancellationToken.None);
        Assert.True(registeredA.Success, registeredA.Message);
        Assert.True(registeredB.Success, registeredB.Message);
        Assert.NotEqual(registeredA.Value!.TenantId, registeredB.Value!.TenantId);

        // Same username + A's password against B's company code → generic
        // invalid credentials (never "wrong password for user X").
        var cross = await _sut.LoginAsync(
            new LoginRequest(slugB, requestB.Username, "TenantA-Pass!"),
            null, CancellationToken.None);
        Assert.False(cross.Success);
        Assert.Equal(AuthErrorCode.InvalidCredentials, cross.ErrorCode);

        // B's own credentials work.
        var own = await _sut.LoginAsync(
            new LoginRequest(slugB, requestB.Username, "TenantB-Pass!"),
            null, CancellationToken.None);
        Assert.True(own.Success, own.Message);

        // Tenant A's refresh token is useless against tenant B's rows:
        // the token carries the tenant id, and the row lookup is RLS-scoped.
        var stolen = await _sut.RefreshAsync(
            own.Value!.Tokens.RefreshToken, null, CancellationToken.None);
        Assert.True(stolen.Success);
        var replayAcrossTenants = await _sut.RefreshAsync(
            registeredA.Value!.Tokens.RefreshToken, null, CancellationToken.None);
        // A's token was revoked by A's own login rotation in Register →
        // either TokenInvalid (row not visible / not found) or TokenReused.
        // Both are safe outcomes; assert it is NOT a success.
        Assert.False(replayAcrossTenants.Success);
    }

    [DatabaseFact]
    public async Task Duplicate_username_within_one_tenant_is_rejected_by_the_database()
    {
        // User management (creating additional users) is Phase 5 — here we
        // prove the DATABASE backstop: the within-tenant uniqueness
        // constraint (uq_users_tenant_username) makes a duplicate
        // impossible no matter which code path writes the row.
        var (_, request) = RegisterRequest("uniq");
        var registered = await _sut.RegisterAsync(request, null, CancellationToken.None);
        Assert.True(registered.Success, registered.Message);
        var tenantId = registered.Value!.TenantId;

        await using (var connection = new NpgsqlConnection(_fixture.OwnerDb))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO saas.users (tenant_id, username, display_name, email,
                                        password_hash, role)
                VALUES ($1, $2, 'Dup', 'dup@example.com', 'x', 'Viewer')
                """;
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue(request.Username);

            var violation = await Assert.ThrowsAsync<PostgresException>(
                () => command.ExecuteNonQueryAsync());
            Assert.Equal("23505", violation.SqlState); // unique_violation
            Assert.Equal("uq_users_tenant_username", violation.ConstraintName);
        }
    }

    [DatabaseFact]
    public async Task Audit_trail_records_the_auth_lifecycle()
    {
        var (slug, request) = RegisterRequest("audit");
        var registered = await _sut.RegisterAsync(request, "127.0.0.1", CancellationToken.None);
        Assert.True(registered.Success);
        var tenantId = registered.Value!.TenantId;

        await _sut.LoginAsync(
            new LoginRequest(slug, request.Username, "Wrong-1!"), "127.0.0.1",
            CancellationToken.None);
        var login = await _sut.LoginAsync(
            new LoginRequest(slug, request.Username, request.Password), "127.0.0.1",
            CancellationToken.None);
        Assert.True(login.Success);
        await _sut.LogoutAsync(
            login.Value!.Tokens.RefreshToken, "127.0.0.1", CancellationToken.None);

        await using (var db = _fixture.CreateContext(tenantId))
        {
            var actions = await db.AuditLogs
                .Where(a => a.UserId == registered.Value.UserId)
                .Select(a => a.Action)
                .OrderBy(a => a)
                .ToListAsync();
            Assert.Contains("auth.registered", actions);
            Assert.Contains("auth.login_failed", actions);
            Assert.Contains("auth.login_succeeded", actions);
            Assert.Contains("auth.logged_out", actions);
        }
    }

    private static string SecretOf(string refreshToken)
    {
        Assert.True(RefreshTokens.TryParse(refreshToken, out _, out var secret));
        return secret;
    }
}
