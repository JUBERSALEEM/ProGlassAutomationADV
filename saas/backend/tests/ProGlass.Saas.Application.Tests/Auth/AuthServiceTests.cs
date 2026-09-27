using ProGlass.Saas.Application.Auth;
using Xunit;

namespace ProGlass.Saas.Application.Tests;

/// <summary>
/// AuthService orchestration tests against in-memory fakes: WPF lockout
/// semantics (persisted, counter reset on lock, cleared on success),
/// generic login errors, refresh rotation + reuse detection, absolute
/// lifetime inheritance, password change and session management.
/// </summary>
public sealed class AuthServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly FakeUserStore _users = new();
    private readonly FakeRefreshTokenStore _refreshTokens = new();
    private readonly FakeTenantProvisioner _provisioner;
    private readonly FakeTokenService _tokens = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeAuditWriter _audit = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _provisioner = new FakeTenantProvisioner(_users);
        _sut = new AuthService(
            _users, _refreshTokens, _provisioner, _tokens, _hasher, _audit,
            new AuthOptions());
        _users.SlugToTenant["acme"] = TenantId;
        _users.TakenSlugs.Add("acme");
    }

    private FakeUserStore.Row SeedUser(
        string username = "juber", string password = "Str0ng-Pass!",
        string role = "Administrator", bool isActive = true,
        bool mustReset = false, int failedCount = 0, DateTime? lockedUntil = null) =>
        SeedUser(Guid.NewGuid(), username, password, role, isActive, mustReset,
            failedCount, lockedUntil);

    private FakeUserStore.Row SeedUser(
        Guid userId, string username, string password, string role, bool isActive,
        bool mustReset, int failedCount, DateTime? lockedUntil)
    {
        var row = new FakeUserStore.Row
        {
            TenantId = TenantId,
            UserId = userId,
            Username = username,
            DisplayName = username + " Saleem",
            Email = username.ToLowerInvariant() + "@example.com",
            Role = role,
            PasswordHash = _hasher.Hash(password),
            IsActive = isActive,
            MustResetPassword = mustReset,
            FailedAttemptCount = failedCount,
            LockedUntilUtc = lockedUntil
        };
        _users.Users[userId] = row;
        return row;
    }

    private static LoginRequest Login(
        string slug = "acme", string username = "juber",
        string password = "Str0ng-Pass!") => new(slug, username, password);

    // ── Register ──────────────────────────────────────────────────

    [Fact]
    public async Task Register_creates_tenant_owner_and_tokens()
    {
        var request = new RegisterRequest(
            "Acme Glass", "acme-new", "juber", "Juber Saleem",
            "Juber@Example.com", "Str0ng-Pass!");

        var result = await _sut.RegisterAsync(request, "127.0.0.1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("acme-new", result.Value!.TenantSlug);
        Assert.NotEqual(Guid.Empty, result.Value.TenantId);
        Assert.NotEqual(Guid.Empty, result.Value.UserId);
        Assert.NotEqual("", result.Value.Tokens.AccessToken);
        Assert.NotEqual("", result.Value.Tokens.RefreshToken);

        // Slug + email normalized before provisioning; owner gets the
        // Administrator role (WPF catalog has no "owner" role).
        var provisioned = Assert.Single(_provisioner.Calls);
        Assert.Equal("acme-new", provisioned.Slug);
        Assert.Equal("juber@example.com", provisioned.Email);
        var owner = _users.Users[result.Value.UserId];
        Assert.Equal("Administrator", owner.Role);

        Assert.True(_audit.Has("auth.registered"));
        Assert.NotEmpty(_refreshTokens.Tokens);
    }

    [Fact]
    public async Task Register_rejects_weak_passwords_without_provisioning()
    {
        var request = new RegisterRequest(
            "Acme Glass", "acme-new", "juber", "Juber Saleem",
            "juber@example.com", "password");

        var result = await _sut.RegisterAsync(request, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthErrorCode.WeakPassword, result.ErrorCode);
        Assert.Empty(_provisioner.Calls);
        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task Register_rejects_taken_slug()
    {
        var request = new RegisterRequest(
            "Acme Glass", "ACME", "juber", "Juber Saleem",
            "juber@example.com", "Str0ng-Pass!"); // slug differs only by case

        var result = await _sut.RegisterAsync(request, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthErrorCode.SlugTaken, result.ErrorCode);
        Assert.Empty(_provisioner.Calls);
    }

    [Theory]
    [InlineData("Acme Glass", "ab", "juber", "Juber Saleem", "juber@example.com")]        // slug too short
    [InlineData("Acme Glass", "acme-new", "ju", "Juber Saleem", "juber@example.com")]     // username too short
    [InlineData("A", "acme-new", "juber", "Juber Saleem", "juber@example.com")]           // tenant name too short
    [InlineData("Acme Glass", "acme-new", "juber", "Juber Saleem", "not-an-email")]       // email
    public async Task Register_validates_input(
        string tenantName, string slug, string username, string display, string email)
    {
        var request = new RegisterRequest(
            tenantName, slug, username, display, email, "Str0ng-Pass!");

        var result = await _sut.RegisterAsync(request, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Empty(_provisioner.Calls);
    }

    // ── Login: the happy path ─────────────────────────────────────

    [Fact]
    public async Task Login_succeeds_and_clears_lockout_state()
    {
        var row = SeedUser(failedCount: 3);

        var result = await _sut.LoginAsync(Login(), "127.0.0.1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("juber", result.Value!.Username);
        Assert.Equal("Administrator", result.Value.Role);
        Assert.NotEqual("", result.Value.Tokens.AccessToken);
        Assert.Equal(0, row.FailedAttemptCount);
        Assert.Null(row.LockedUntilUtc);
        Assert.NotNull(row.LastLoginAt);
        Assert.True(_audit.Has("auth.login_succeeded"));
    }

    [Fact]
    public async Task Login_accepts_email_as_identifier()
    {
        SeedUser();
        var result = await _sut.LoginAsync(
            Login(username: "JUBER@EXAMPLE.COM"), null, CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Login_reports_must_reset_password_flag()
    {
        SeedUser(mustReset: true);
        var result = await _sut.LoginAsync(Login(), null, CancellationToken.None);
        Assert.True(result.Success);
        Assert.True(result.Value!.MustResetPassword);
    }

    // ── Login: generic errors (no field disclosure) ───────────────

    [Fact]
    public async Task Login_unknown_tenant_and_unknown_user_return_the_same_generic_error()
    {
        var wrongTenant = await _sut.LoginAsync(
            Login(slug: "no-such"), null, CancellationToken.None);
        var wrongUser = await _sut.LoginAsync(
            Login(username: "nobody"), null, CancellationToken.None);

        Assert.False(wrongTenant.Success);
        Assert.False(wrongUser.Success);
        Assert.Equal(AuthErrorCode.InvalidCredentials, wrongTenant.ErrorCode);
        Assert.Equal(AuthErrorCode.InvalidCredentials, wrongUser.ErrorCode);
        // IDENTICAL text — the response must not reveal which part was wrong.
        Assert.Equal(wrongTenant.Message, wrongUser.Message);
        Assert.True(_audit.Has("auth.login_failed"));
    }

    [Fact]
    public async Task Login_unknown_principal_still_performs_a_hash_verify()
    {
        // Timing equalization: the unknown-tenant path burns a dummy
        // Argon2 verify so a timing attack cannot distinguish "no such
        // company code" from "wrong password".
        await _sut.LoginAsync(Login(slug: "no-such"), null, CancellationToken.None);
        Assert.Equal(1, _hasher.VerifyCalls);
    }

    [Fact]
    public async Task Login_rejects_inactive_account()
    {
        SeedUser(isActive: false);
        var result = await _sut.LoginAsync(Login(), null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(AuthErrorCode.AccountInactive, result.ErrorCode);
    }

    // ── Login: WPF lockout semantics, verbatim ────────────────────
    // (Services/Audit/AuditService.Part1.cs: 5 attempts → 5-minute window,
    //  counter RESETS when the window opens, success clears everything.)

    [Fact]
    public async Task Login_wrong_password_increments_the_counter()
    {
        var row = SeedUser(failedCount: 0);

        var result = await _sut.LoginAsync(
            Login(password: "Wrong-1!"), null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthErrorCode.InvalidCredentials, result.ErrorCode);
        Assert.Equal(1, row.FailedAttemptCount);
        Assert.Null(row.LockedUntilUtc);
        Assert.True(_audit.Has("auth.login_failed"));
        Assert.False(_audit.Has("auth.account_locked"));
    }

    [Fact]
    public async Task Login_fifth_failure_locks_for_five_minutes_and_resets_the_counter()
    {
        var row = SeedUser(failedCount: 4); // one away from the threshold

        var result = await _sut.LoginAsync(
            Login(password: "Wrong-1!"), null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthErrorCode.AccountLocked, result.ErrorCode);
        Assert.Equal(5 * 60, result.RetryAfterSeconds);

        // WPF: the counter RESETS when the lockout window opens.
        Assert.Equal(0, row.FailedAttemptCount);
        Assert.NotNull(row.LockedUntilUtc);
        Assert.True(row.LockedUntilUtc > DateTime.UtcNow.AddMinutes(4),
            $"lock window should end ~5 minutes out, got {row.LockedUntilUtc:O}");
        Assert.True(row.LockedUntilUtc < DateTime.UtcNow.AddMinutes(6),
            $"lock window should end ~5 minutes out, got {row.LockedUntilUtc:O}");
        Assert.True(_audit.Has("auth.account_locked"));
    }

    [Fact]
    public async Task Login_while_locked_is_rejected_even_with_the_correct_password()
    {
        SeedUser(lockedUntil: DateTime.UtcNow.AddMinutes(4));

        var result = await _sut.LoginAsync(Login(), null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthErrorCode.AccountLocked, result.ErrorCode);
        Assert.True(result.RetryAfterSeconds!.Value > 0);
        Assert.False(_audit.Has("auth.login_succeeded"));
    }

    [Fact]
    public async Task Login_after_the_window_expires_succeeds()
    {
        var row = SeedUser(lockedUntil: DateTime.UtcNow.AddSeconds(-1));

        var result = await _sut.LoginAsync(Login(), null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, row.FailedAttemptCount);
        Assert.Null(row.LockedUntilUtc);
    }

    // ── Refresh: rotation ─────────────────────────────────────────

    [Fact]
    public async Task Refresh_rotates_and_inherits_the_absolute_deadline()
    {
        SeedUser();
        var login = await _sut.LoginAsync(Login(), null, CancellationToken.None);
        var firstToken = login.Value!.Tokens.RefreshToken;
        var firstRecord = _refreshTokens.Tokens.Single(t => t.RevokedAtUtc is null);

        var refreshed = await _sut.RefreshAsync(firstToken, null, CancellationToken.None);

        Assert.True(refreshed.Success);
        Assert.NotEqual(firstToken, refreshed.Value!.RefreshToken);

        // Old token revoked, linked to its successor by hash.
        var oldRecord = _refreshTokens.Tokens.First(t => t.Id == firstRecord.Id);
        Assert.NotNull(oldRecord.RevokedAtUtc);
        Assert.Equal(
            RefreshTokens.HashSecret(SecretOf(refreshed.Value.RefreshToken)),
            oldRecord.ReplacedByTokenHash);

        // The new token INHERITS the absolute deadline unchanged, and its
        // sliding expiry is capped by it.
        var newRecord = _refreshTokens.Tokens.Single(t => t.RevokedAtUtc is null);
        Assert.Equal(firstRecord.AbsoluteExpiresAtUtc, newRecord.AbsoluteExpiresAtUtc);
        Assert.True(newRecord.ExpiresAtUtc <= newRecord.AbsoluteExpiresAtUtc);
        Assert.True(_audit.Has("auth.refresh_rotated"));
    }

    [Fact]
    public async Task Refresh_reuse_of_a_revoked_token_kills_the_whole_session_family()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, "juber", "Str0ng-Pass!", "Administrator", true, false, 0, null);
        var login = await _sut.LoginAsync(Login(), null, CancellationToken.None);
        var oldToken = login.Value!.Tokens.RefreshToken;

        var rotated = await _sut.RefreshAsync(oldToken, null, CancellationToken.None);
        Assert.True(rotated.Success);
        var newToken = rotated.Value!.RefreshToken;

        // Replay the OLD (revoked) token → reuse detected, EVERYTHING dies.
        var replay = await _sut.RefreshAsync(oldToken, null, CancellationToken.None);
        Assert.False(replay.Success);
        Assert.Equal(AuthErrorCode.TokenReused, replay.ErrorCode);
        Assert.True(_audit.Has("auth.refresh_reuse_detected"));

        Assert.All(_refreshTokens.Tokens, t => Assert.NotNull(t.RevokedAtUtc));

        // The successor is dead too — the thief and the victim both lose.
        var after = await _sut.RefreshAsync(newToken, null, CancellationToken.None);
        Assert.False(after.Success);
        Assert.Equal(AuthErrorCode.TokenReused, after.ErrorCode);
    }

    [Fact]
    public async Task Refresh_expired_sliding_window_is_rejected()
    {
        SeedUser();
        var login = await _sut.LoginAsync(Login(), null, CancellationToken.None);
        var token = login.Value!.Tokens.RefreshToken;
        AgeActiveToken(token, expiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

        var result = await _sut.RefreshAsync(token, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthErrorCode.TokenExpired, result.ErrorCode);
    }

    [Fact]
    public async Task Refresh_past_the_absolute_deadline_is_rejected_even_when_sliding_is_valid()
    {
        // Regression: the absolute deadline must bind even while the
        // sliding window is still open (migration 0006 stores it per token
        // and rotations inherit it — a per-token created-at check can
        // NEVER fire here because sliding expires first).
        SeedUser();
        var login = await _sut.LoginAsync(Login(), null, CancellationToken.None);
        var token = login.Value!.Tokens.RefreshToken;
        AgeActiveToken(token,
            expiresAtUtc: DateTime.UtcNow.AddDays(10),
            absoluteExpiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

        var result = await _sut.RefreshAsync(token, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthErrorCode.TokenExpired, result.ErrorCode);
        Assert.Contains("maximum age", result.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("v1.notsaguid.secret")]
    [InlineData("v2.11111111111111111111111111111111.c2VjcmV0")]
    public async Task Refresh_malformed_tokens_are_rejected(string token)
    {
        var result = await _sut.RefreshAsync(token, null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(AuthErrorCode.TokenInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Refresh_unknown_hash_is_rejected()
    {
        // Well-formed but never issued (or already deleted).
        var token = RefreshTokens.Format(TenantId, Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        var result = await _sut.RefreshAsync(token, null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(AuthErrorCode.TokenInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Refresh_for_a_deactivated_account_revokes_everything()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, "juber", "Str0ng-Pass!", "Administrator", true, false, 0, null);
        var login = await _sut.LoginAsync(Login(), null, CancellationToken.None);
        var token = login.Value!.Tokens.RefreshToken;

        _users.Users[userId].IsActive = false;

        var result = await _sut.RefreshAsync(token, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthErrorCode.AccountInactive, result.ErrorCode);
        Assert.All(_refreshTokens.Tokens, t => Assert.NotNull(t.RevokedAtUtc));
    }

    // ── Logout ────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_revokes_the_presented_token()
    {
        SeedUser();
        var login = await _sut.LoginAsync(Login(), null, CancellationToken.None);
        var token = login.Value!.Tokens.RefreshToken;

        await _sut.LogoutAsync(token, "127.0.0.1", CancellationToken.None);

        Assert.True(_refreshTokens.Tokens.All(t => t.RevokedAtUtc is not null));
        Assert.True(_audit.Has("auth.logged_out"));
    }

    [Fact]
    public async Task Logout_with_a_garbage_token_is_still_a_success()
    {
        await _sut.LogoutAsync("garbage", null, CancellationToken.None);
        Assert.False(_audit.Has("auth.logged_out")); // nothing to revoke
    }

    // ── Change password ───────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_updates_hash_clears_flag_and_revokes_sessions()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, "juber", "Str0ng-Pass!", "Administrator", true, true, 0, null);
        var login = await _sut.LoginAsync(Login(), null, CancellationToken.None);
        // A second session (another device).
        await _sut.LoginAsync(Login(), null, CancellationToken.None);
        Assert.Equal(2, _refreshTokens.Tokens.Count(t => t.RevokedAtUtc is null));

        var result = await _sut.ChangePasswordAsync(
            TenantId, userId,
            new ChangePasswordRequest("Str0ng-Pass!", "Newer-Str0ng-Pass!"),
            null, CancellationToken.None);

        Assert.True(result.Success);
        var row = _users.Users[userId];
        Assert.Equal(_hasher.Hash("Newer-Str0ng-Pass!"), row.PasswordHash);
        Assert.False(row.MustResetPassword);
        Assert.All(_refreshTokens.Tokens, t => Assert.NotNull(t.RevokedAtUtc));
        Assert.True(_audit.Has("auth.password_changed"));
    }

    [Fact]
    public async Task ChangePassword_with_wrong_current_password_is_denied()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, "juber", "Str0ng-Pass!", "Administrator", true, false, 0, null);
        var originalHash = _users.Users[userId].PasswordHash;

        var result = await _sut.ChangePasswordAsync(
            TenantId, userId,
            new ChangePasswordRequest("Wrong-Current-1!", "Newer-Str0ng-Pass!"),
            null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthErrorCode.CurrentPasswordWrong, result.ErrorCode);
        Assert.Equal(originalHash, _users.Users[userId].PasswordHash);
        Assert.True(_audit.Has("auth.password_change_denied"));
    }

    [Fact]
    public async Task ChangePassword_enforces_the_policy_on_the_new_password()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, "juber", "Str0ng-Pass!", "Administrator", true, false, 0, null);

        var result = await _sut.ChangePasswordAsync(
            TenantId, userId,
            new ChangePasswordRequest("Str0ng-Pass!", "password"),
            null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthErrorCode.WeakPassword, result.ErrorCode);
    }

    // ── Sessions ──────────────────────────────────────────────────

    [Fact]
    public async Task Sessions_lists_token_metadata_newest_first()
    {
        SeedUser();
        await _sut.LoginAsync(Login(), "10.0.0.1", CancellationToken.None);
        await _sut.LoginAsync(Login(), "10.0.0.2", CancellationToken.None);
        var userId = _users.Users.Single().Key;

        var sessions = await _sut.ListSessionsAsync(
            TenantId, userId, CancellationToken.None);

        Assert.Equal(2, sessions.Count);
        Assert.Contains("10.0.0.1", sessions.Select(s => s.CreatedIp));
        Assert.Contains("10.0.0.2", sessions.Select(s => s.CreatedIp));
        Assert.True(sessions[0].CreatedAtUtc >= sessions[1].CreatedAtUtc);
    }

    [Fact]
    public async Task RevokeSession_kills_exactly_that_session()
    {
        SeedUser();
        var first = (await _sut.LoginAsync(Login(), null, CancellationToken.None))
            .Value!.Tokens.RefreshToken;
        var second = (await _sut.LoginAsync(Login(), null, CancellationToken.None))
            .Value!.Tokens.RefreshToken;
        var userId = _users.Users.Single().Key;
        var target = _refreshTokens.Tokens.First(t =>
            t.TokenHash == RefreshTokens.HashSecret(SecretOf(first)));

        var result = await _sut.RevokeSessionAsync(
            TenantId, userId, target.Id, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(_refreshTokens.Tokens.First(t => t.Id == target.Id).RevokedAtUtc);
        Assert.True(_refreshTokens.Tokens
            .First(t => t.TokenHash == RefreshTokens.HashSecret(SecretOf(second)))
            .RevokedAtUtc is null);
        Assert.True(_audit.Has("auth.session_revoked"));
    }

    [Fact]
    public async Task RevokeSession_unknown_id_returns_not_found()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, "juber", "Str0ng-Pass!", "Administrator", true, false, 0, null);

        var result = await _sut.RevokeSessionAsync(
            TenantId, userId, Guid.NewGuid(), null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthErrorCode.SessionNotFound, result.ErrorCode);
    }

    // ── RefreshTokens helper ──────────────────────────────────────

    [Fact]
    public void Refresh_token_format_round_trips()
    {
        var tenantId = Guid.NewGuid();
        var secret = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var token = RefreshTokens.Format(tenantId, secret);

        Assert.True(RefreshTokens.TryParse(token, out var parsedTenant, out var parsedSecret));
        Assert.Equal(tenantId, parsedTenant);
        Assert.Equal(secret, parsedSecret);
    }

    [Fact]
    public void HashSecret_is_deterministic_and_hex()
    {
        Assert.Equal(
            RefreshTokens.HashSecret("abc"),
            RefreshTokens.HashSecret("abc"));
        Assert.NotEqual(RefreshTokens.HashSecret("abc"), RefreshTokens.HashSecret("abd"));
        Assert.Matches("^[0-9A-F]{64}$", RefreshTokens.HashSecret("abc"));
    }

    // ── helpers ───────────────────────────────────────────────────

    private static string SecretOf(string refreshToken)
    {
        Assert.True(RefreshTokens.TryParse(refreshToken, out _, out var secret));
        return secret;
    }

    /// <summary>Re-times an ACTIVE token row (simulates the passage of
    /// time) — tests craft expired states directly instead of sleeping.</summary>
    private void AgeActiveToken(
        string refreshToken, DateTime? expiresAtUtc = null,
        DateTime? absoluteExpiresAtUtc = null)
    {
        var hash = RefreshTokens.HashSecret(SecretOf(refreshToken));
        var index = _refreshTokens.Tokens.FindIndex(
            t => t.TokenHash == hash && t.RevokedAtUtc is null);
        Assert.True(index >= 0, "token to age must exist and be active");
        var record = _refreshTokens.Tokens[index];
        _refreshTokens.Tokens[index] = record with
        {
            ExpiresAtUtc = expiresAtUtc ?? record.ExpiresAtUtc,
            AbsoluteExpiresAtUtc = absoluteExpiresAtUtc ?? record.AbsoluteExpiresAtUtc
        };
    }
}
