using System.Security.Cryptography;
using System.Text;
using ProGlass.Saas.Application.Security;

namespace ProGlass.Saas.Application.Auth;

/// <summary>
/// Authentication use-cases: register, login (with the WPF lockout
/// semantics), refresh-token rotation with reuse detection, logout,
/// password change and session management.
///
/// Pure orchestration — no EF, no HTTP, no clock hidden state; every
/// dependency is a port so the whole service is unit-testable with fakes
/// (ProGlass.Saas.Application.Tests) and integration-testable against
/// live PostgreSQL (ProGlass.Saas.IntegrationTests.AuthFlowTests).
/// </summary>
public sealed class AuthService
{
    private readonly IUserStore _users;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly ITenantProvisioner _provisioner;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditWriter _audit;
    private readonly AuthOptions _options;

    public AuthService(
        IUserStore users,
        IRefreshTokenStore refreshTokens,
        ITenantProvisioner provisioner,
        ITokenService tokens,
        IPasswordHasher hasher,
        IAuditWriter audit,
        AuthOptions options)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _refreshTokens = refreshTokens ?? throw new ArgumentNullException(nameof(refreshTokens));
        _provisioner = provisioner ?? throw new ArgumentNullException(nameof(provisioner));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    // ── Register ─────────────────────────────────────────────────────

    public async Task<AuthResult<RegisterResult>> RegisterAsync(
        RegisterRequest request, string? ip, CancellationToken ct)
    {
        var error = ValidateRegister(request);
        if (error != null)
            return AuthResult<RegisterResult>.Fail(AuthErrorCode.ValidationFailed, error);

        var (accepted, passwordError) = PasswordPolicy.ValidateNewPassword(
            request.Password, request.Username, request.DisplayName);
        if (!accepted)
            return AuthResult<RegisterResult>.Fail(AuthErrorCode.WeakPassword, passwordError);

        var slug = request.TenantSlug.Trim().ToLowerInvariant();
        if (await _users.IsSlugTakenAsync(slug, ct))
            return AuthResult<RegisterResult>.Fail(
                AuthErrorCode.SlugTaken,
                $"The company code \"{slug}\" is already taken. Choose another.");

        var provisioned = await _provisioner.ProvisionAsync(
            request.TenantName.Trim(), slug,
            request.Username.Trim(), request.DisplayName.Trim(),
            request.Email.Trim().ToLowerInvariant(),
            _hasher.Hash(request.Password), ip, ct).ConfigureAwait(false);

        var user = await _users.FindByIdAsync(
            provisioned.TenantId, provisioned.UserId, ct).ConfigureAwait(false);
        if (user is null) // provisioner just created it — a miss here is a bug
            return AuthResult<RegisterResult>.Fail(
                AuthErrorCode.UserNotFound, "Provisioning failed — tenant was not created.");

        await _audit.WriteAsync(provisioned.TenantId, provisioned.UserId,
            "auth.registered", "tenants", provisioned.TenantId, ip, null, ct);

        var tokens = await IssueTokensAsync(user, ip, ct).ConfigureAwait(false);
        return AuthResult<RegisterResult>.Ok(new RegisterResult(
            provisioned.TenantId, provisioned.UserId, slug, tokens));
    }

    // ── Login ────────────────────────────────────────────────────────

    public async Task<AuthResult<LoginResult>> LoginAsync(
        LoginRequest request, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TenantSlug)
            || string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password))
            return AuthResult<LoginResult>.Fail(
                AuthErrorCode.ValidationFailed, "Company code, username and password are required.");

        var slug = request.TenantSlug.Trim().ToLowerInvariant();
        var login = request.Username.Trim();

        // Tenant resolution from the login slug, then a tenant-scoped user
        // lookup. Failures are deliberately GENERIC — in a multi-tenant SaaS
        // the response must not disclose whether the company code, the
        // username or the password was wrong (documented difference from
        // the single-company WPF app, which named the field).
        var tenantId = await _users.FindTenantIdBySlugAsync(slug, ct).ConfigureAwait(false);
        if (tenantId is null)
        {
            await _audit.WriteAsync(null, null, "auth.login_failed",
                "tenants", null, ip, null, ct); // no tenant → application log (RLS)
            EqualizePasswordTiming(request.Password);
            return InvalidCredentials();
        }

        var user = await _users.FindLoginAsync(tenantId.Value, login, ct).ConfigureAwait(false);
        if (user is null)
        {
            await _audit.WriteAsync(tenantId, null, "auth.login_failed",
                "users", null, ip, null, ct);
            EqualizePasswordTiming(request.Password);
            return InvalidCredentials();
        }

        if (!user.IsActive)
            return AuthResult<LoginResult>.Fail(
                AuthErrorCode.AccountInactive,
                "This account is no longer active. Contact your administrator.");

        // WPF persisted lockout: reject while a window is open.
        var lockoutRemaining = RemainingLockoutSeconds(user.LockedUntilUtc);
        if (lockoutRemaining > 0)
            return AuthResult<LoginResult>.Fail(
                AuthErrorCode.AccountLocked,
                $"Account temporarily locked. Try again in {lockoutRemaining}s.",
                lockoutRemaining);

        if (!_hasher.Verify(user.PasswordHash, request.Password))
        {
            // WPF AuditService.Part1 semantics, verbatim: increment; at the
            // threshold, open the lockout window and reset the counter.
            var failures = user.FailedAttemptCount + 1;
            DateTime? lockedUntil = null;
            var locked = failures >= _options.MaxFailedAttemptsBeforeLock;
            if (locked)
            {
                lockedUntil = DateTime.UtcNow.AddMinutes(_options.LockoutMinutes);
                failures = 0;
            }
            await _users.SaveLoginStateAsync(
                tenantId.Value, user.UserId, failures, lockedUntil, null, ct)
                .ConfigureAwait(false);
            await _audit.WriteAsync(tenantId.Value, user.UserId, "auth.login_failed",
                "users", user.UserId, ip, null, ct);

            if (locked)
            {
                await _audit.WriteAsync(tenantId.Value, user.UserId, "auth.account_locked",
                    "users", user.UserId, ip, null, ct);
                var seconds = _options.LockoutMinutes * 60;
                return AuthResult<LoginResult>.Fail(
                    AuthErrorCode.AccountLocked,
                    $"Too many failed attempts. Account locked — try again in {seconds}s.",
                    seconds);
            }
            return InvalidCredentials();
        }

        await _users.SaveLoginStateAsync(
            tenantId.Value, user.UserId, 0, null, DateTime.UtcNow, ct)
            .ConfigureAwait(false);
        await _audit.WriteAsync(tenantId.Value, user.UserId, "auth.login_succeeded",
            "users", user.UserId, ip, null, ct);

        var tokens = await IssueTokensAsync(user, ip, ct).ConfigureAwait(false);
        return AuthResult<LoginResult>.Ok(new LoginResult(
            tenantId.Value, slug, user.UserId, user.Username, user.DisplayName,
            user.Role, user.MustResetPassword, tokens));
    }

    private static int RemainingLockoutSeconds(DateTime? lockedUntilUtc)
    {
        if (lockedUntilUtc is null) return 0;
        var remaining = (int)Math.Ceiling(
            (lockedUntilUtc.Value - DateTime.UtcNow).TotalSeconds);
        return Math.Max(0, remaining);
    }

    private static AuthResult<LoginResult> InvalidCredentials() =>
        AuthResult<LoginResult>.Fail(
            AuthErrorCode.InvalidCredentials,
            "Company code, username or password is incorrect.");

    /// <summary>
    /// Burns a hash verification when the user/tenant was not found so the
    /// response time does not reveal WHICH part of the credential was wrong.
    /// </summary>
    private void EqualizePasswordTiming(string password)
    {
        _timingDummyHash ??= _hasher.Hash("timing-equalization-dummy");
        _ = _hasher.Verify(_timingDummyHash, password);
    }

    private string? _timingDummyHash;

    // ── Refresh ──────────────────────────────────────────────────────

    public async Task<AuthResult<TokenPair>> RefreshAsync(
        string refreshToken, string? ip, CancellationToken ct)
    {
        if (!RefreshTokens.TryParse(refreshToken, out var tenantId, out var secret))
            return AuthResult<TokenPair>.Fail(
                AuthErrorCode.TokenInvalid, "The session token is malformed.");

        var hash = RefreshTokens.HashSecret(secret);
        var record = await _refreshTokens.FindAnyByHashAsync(tenantId, hash, ct)
            .ConfigureAwait(false);

        if (record is null)
            return AuthResult<TokenPair>.Fail(
                AuthErrorCode.TokenInvalid, "The session has ended. Please sign in again.");

        if (record.RevokedAtUtc is not null)
        {
            // REUSE DETECTED (a revoked token being replayed). Revoke the
            // user's entire session chain — the token was either stolen or
            // a client is misbehaving; either way, everything issued from
            // this lineage dies now.
            var revoked = await _refreshTokens.RevokeAllForUserAsync(
                tenantId, record.UserId, ct).ConfigureAwait(false);
            await _audit.WriteAsync(tenantId, record.UserId,
                "auth.refresh_reuse_detected", "users", record.UserId, ip, null, ct);
            return AuthResult<TokenPair>.Fail(
                AuthErrorCode.TokenReused,
                "This session token was already used. All sessions were revoked " +
                "for your protection — please sign in again.");
        }

        var now = DateTime.UtcNow;
        if (record.ExpiresAtUtc <= now)
        {
            await _refreshTokens.RevokeAsync(
                tenantId, record.Id, "", ct).ConfigureAwait(false);
            return AuthResult<TokenPair>.Fail(
                AuthErrorCode.TokenExpired, "The session expired. Please sign in again.");
        }

        // ABSOLUTE lifetime: the lineage deadline is stored per token and
        // inherited unchanged by every rotation (migration 0006), so no
        // matter how active the session is, it dies on schedule.
        if (record.AbsoluteExpiresAtUtc <= now)
        {
            await _refreshTokens.RevokeAsync(
                tenantId, record.Id, "", ct).ConfigureAwait(false);
            return AuthResult<TokenPair>.Fail(
                AuthErrorCode.TokenExpired,
                "The session reached its maximum age. Please sign in again.");
        }

        var user = await _users.FindByIdAsync(tenantId, record.UserId, ct).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            await _refreshTokens.RevokeAllForUserAsync(tenantId, record.UserId, ct)
                .ConfigureAwait(false);
            return AuthResult<TokenPair>.Fail(
                AuthErrorCode.AccountInactive, "This account is no longer active.");
        }

        // ROTATE: revoke the presented token and issue a fresh pair. The
        // sliding expiry is capped by the inherited absolute deadline, and
        // the absolute deadline itself is passed through UNCHANGED.
        var newSecret = RefreshTokens.NewSecret(_options.RefreshTokenSecretBytes);
        var newHash = RefreshTokens.HashSecret(newSecret);
        var newToken = RefreshTokens.Format(tenantId, newSecret);
        var newExpiry = now.AddDays(_options.RefreshTokenDays);
        if (newExpiry > record.AbsoluteExpiresAtUtc)
            newExpiry = record.AbsoluteExpiresAtUtc;

        await _refreshTokens.RevokeAsync(tenantId, record.Id, newHash, ct)
            .ConfigureAwait(false);
        await _refreshTokens.CreateAsync(
            tenantId, record.UserId, newHash, newExpiry,
            record.AbsoluteExpiresAtUtc, ip, ct).ConfigureAwait(false);
        await _audit.WriteAsync(tenantId, record.UserId,
            "auth.refresh_rotated", "users", record.UserId, ip, null, ct);

        var (accessToken, accessExpiry) = _tokens.IssueAccessToken(user);
        return AuthResult<TokenPair>.Ok(new TokenPair(
            accessToken, accessExpiry, newToken));
    }

    // ── Logout ───────────────────────────────────────────────────────

    public async Task<AuthResult<object>> LogoutAsync(
        string refreshToken, string? ip, CancellationToken ct)
    {
        if (RefreshTokens.TryParse(refreshToken, out var tokenTenant, out var secret))
        {
            var hash = RefreshTokens.HashSecret(secret);
            var record = await _refreshTokens.FindActiveByHashAsync(tokenTenant, hash, ct)
                .ConfigureAwait(false);
            if (record is not null)
            {
                await _refreshTokens.RevokeAsync(tokenTenant, record.Id, "", ct)
                    .ConfigureAwait(false);
                await _audit.WriteAsync(tokenTenant, record.UserId,
                    "auth.logged_out", "users", record.UserId, ip, null, ct);
            }
        }
        return AuthResult<object>.Ok(new object());
    }

    // ── Password change ──────────────────────────────────────────────

    public async Task<AuthResult<object>> ChangePasswordAsync(
        Guid tenantId, Guid userId,
        ChangePasswordRequest request, string? ip, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(tenantId, userId, ct).ConfigureAwait(false);
        if (user is null || !user.IsActive)
            return AuthResult<object>.Fail(
                AuthErrorCode.UserNotFound, "Account not found.");

        if (!_hasher.Verify(user.PasswordHash, request.CurrentPassword))
        {
            await _audit.WriteAsync(tenantId, userId,
                "auth.password_change_denied", "users", userId, ip, null, ct);
            return AuthResult<object>.Fail(
                AuthErrorCode.CurrentPasswordWrong, "The current password is incorrect.");
        }

        var (accepted, passwordError) = PasswordPolicy.ValidateNewPassword(
            request.NewPassword, user.Username, user.DisplayName);
        if (!accepted)
            return AuthResult<object>.Fail(AuthErrorCode.WeakPassword, passwordError);

        await _users.UpdatePasswordHashAsync(
            tenantId, userId, _hasher.Hash(request.NewPassword), true, ct)
            .ConfigureAwait(false);

        // Every other device must re-authenticate with the new password.
        await _refreshTokens.RevokeAllForUserAsync(tenantId, userId, ct).ConfigureAwait(false);
        await _audit.WriteAsync(tenantId, userId,
            "auth.password_changed", "users", userId, ip, null, ct);
        return AuthResult<object>.Ok(new object());
    }

    // ── Sessions ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(
        Guid tenantId, Guid userId, CancellationToken ct)
    {
        var records = await _refreshTokens.ListForUserAsync(tenantId, userId, ct)
            .ConfigureAwait(false);
        return records
            .Select(r => new SessionInfo(
                r.Id, r.CreatedAtUtc, r.ExpiresAtUtc, r.RevokedAtUtc, r.CreatedIp))
            .ToList();
    }

    public async Task<AuthResult<object>> RevokeSessionAsync(
        Guid tenantId, Guid userId, Guid sessionId, string? ip, CancellationToken ct)
    {
        var records = await _refreshTokens.ListForUserAsync(tenantId, userId, ct)
            .ConfigureAwait(false);
        if (records.All(r => r.Id != sessionId))
            return AuthResult<object>.Fail(
                AuthErrorCode.SessionNotFound, "Session not found.");

        await _refreshTokens.RevokeAsync(tenantId, sessionId, "", ct).ConfigureAwait(false);
        await _audit.WriteAsync(tenantId, userId,
            "auth.session_revoked", "refresh_tokens", sessionId, ip, null, ct);
        return AuthResult<object>.Ok(new object());
    }

    // ── Shared ───────────────────────────────────────────────────────

    private async Task<TokenPair> IssueTokensAsync(LoginUser user, string? ip, CancellationToken ct)
    {
        var (accessToken, expiresAt) = _tokens.IssueAccessToken(user);
        var secret = RefreshTokens.NewSecret(_options.RefreshTokenSecretBytes);
        var hash = RefreshTokens.HashSecret(secret);
        await _refreshTokens.CreateAsync(
            user.TenantId, user.UserId, hash,
            DateTime.UtcNow.AddDays(_options.RefreshTokenDays),
            DateTime.UtcNow.AddDays(_options.AbsoluteRefreshTokenDays),
            ip, ct).ConfigureAwait(false);
        return new TokenPair(accessToken, expiresAt, RefreshTokens.Format(user.TenantId, secret));
    }

    private static string? ValidateRegister(RegisterRequest request)
    {
        if (request is null) return "Request body is required.";
        var name = request.TenantName?.Trim() ?? "";
        if (name.Length is < 2 or > 200)
            return "Company name must be 2–200 characters.";
        var slug = request.TenantSlug?.Trim().ToLowerInvariant() ?? "";
        if (slug.Length is < 3 or > 40 || !System.Text.RegularExpressions.Regex.IsMatch(
                slug, @"^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$"))
            return "Company code must be 3–40 characters: lowercase letters, digits and hyphens " +
                   "(no leading/trailing hyphen).";
        var username = request.Username?.Trim() ?? "";
        if (username.Length is < 3 or > 60)
            return "Username must be 3–60 characters.";
        var display = request.DisplayName?.Trim() ?? "";
        if (display.Length is < 2 or > 120)
            return "Display name must be 2–120 characters.";
        var email = request.Email?.Trim() ?? "";
        if (email.Length == 0 || email.Length > 200 || !email.Contains('@') || email.Contains(' '))
            return "A valid email address is required for the owner account.";
        return null;
    }
}

/// <summary>
/// Refresh token wire format + hashing. The token string carries the
/// tenant id because the refresh_tokens table is RLS-scoped — the tenant
/// must be known before its row can be found (see AuthPorts).
/// Format: "v1.{tenantId as N}.{base64url(secret)}". Only the SHA-256 of
/// the secret is stored.
/// </summary>
public static class RefreshTokens
{
    public static string NewSecret(int bytes) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes));

    public static string Format(Guid tenantId, string secret) =>
        $"v1.{tenantId:N}.{secret}";

    public static bool TryParse(string? token, out Guid tenantId, out string secret)
    {
        tenantId = Guid.Empty;
        secret = "";
        if (string.IsNullOrEmpty(token)) return false;
        var parts = token.Split('.');
        if (parts.Length != 3 || parts[0] != "v1") return false;
        if (!Guid.TryParseExact(parts[1], "N", out tenantId)) return false;
        if (string.IsNullOrEmpty(parts[2])) return false;
        secret = parts[2];
        return true;
    }

    public static string HashSecret(string secret)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hash);
    }
}
