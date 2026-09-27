namespace ProGlass.Saas.Application.Auth;

/// <summary>Every expected auth failure has a code — clients map codes,
/// never parse messages (API design §0 error contract).</summary>
public enum AuthErrorCode
{
    None = 0,
    ValidationFailed,
    InvalidCredentials,
    AccountInactive,
    AccountLocked,
    TenantNotFound,
    SlugTaken,
    WeakPassword,
    CurrentPasswordWrong,
    TokenInvalid,
    TokenExpired,
    TokenReused,
    SessionNotFound,
    UserNotFound,
}

/// <summary>Typed result: either a value or a code + operator-readable
/// message (which is safe to return in API responses). RetryAfterSeconds
/// is set for lockout failures so the API can answer 423 with a
/// Retry-After header (API design §1).</summary>
public sealed record AuthResult<T>(
    bool Success, T? Value, AuthErrorCode ErrorCode, string Message,
    int? RetryAfterSeconds = null)
{
    public static AuthResult<T> Ok(T value) =>
        new(true, value, AuthErrorCode.None, "");

    public static AuthResult<T> Fail(AuthErrorCode code, string message,
        int? retryAfterSeconds = null) =>
        new(false, default, code, message, retryAfterSeconds);
}

// ── Requests ───────────────────────────────────────────────────────────

public sealed record RegisterRequest(
    string TenantName, string TenantSlug, string Username,
    string DisplayName, string Email, string Password);

public sealed record LoginRequest(string TenantSlug, string Username, string Password);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// ── Results ────────────────────────────────────────────────────────────

public sealed record TokenPair(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken);

public sealed record RegisterResult(Guid TenantId, Guid UserId, string TenantSlug, TokenPair Tokens);

public sealed record LoginResult(
    Guid TenantId, string TenantSlug, Guid UserId, string Username,
    string DisplayName, string Role, bool MustResetPassword, TokenPair Tokens);

public sealed record SessionInfo(
    Guid Id, DateTime CreatedAtUtc, DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc, string? CreatedIp);

// ── Store record shapes (pure data — no EF entities cross this boundary) ─

public sealed record LoginUser(
    Guid TenantId, Guid UserId, string Username, string DisplayName,
    string Email, string Role, string PasswordHash,
    bool IsActive, bool MustResetPassword,
    int FailedAttemptCount, DateTime? LockedUntilUtc, DateTime? LastLoginAtUtc);

public sealed record RefreshTokenRecord(
    Guid Id, Guid TenantId, Guid UserId, string TokenHash,
    DateTime CreatedAtUtc, DateTime ExpiresAtUtc, DateTime AbsoluteExpiresAtUtc,
    DateTime? RevokedAtUtc, string? ReplacedByTokenHash, string? CreatedIp);

public sealed record ProvisionedTenant(Guid TenantId, Guid UserId);
