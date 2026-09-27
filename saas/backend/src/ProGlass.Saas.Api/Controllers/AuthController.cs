using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Auth;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// Authentication endpoints (API design §1). All failures use the shared
/// error envelope { code, message } with codes like
/// "auth.invalid_credentials" — clients branch on the CODE, never on the
/// message text. Lockouts answer 423 with a Retry-After header.
///
/// Register/login/refresh/logout are anonymous (logout accepts an expired
/// access token — the refresh token in the body identifies the session);
/// sessions and change-password require a valid access token.
///
/// Deferred (§58, documented in WPF_TO_SAAS_MAPPING): email verification,
/// email-based password reset, MFA endpoints. The trial subscription that
/// the design attaches to register ships in Phase 13 (billing).
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ClientIp, ct);
        return result.Success
            ? Ok(result.Value)
            : Fail(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ClientIp, ct);
        return result.Success
            ? Ok(result.Value)
            : Fail(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await auth.RefreshAsync(request.RefreshToken ?? "", ClientIp, ct);
        return result.Success
            ? Ok(result.Value)
            : Fail(result);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest? request, CancellationToken ct)
    {
        await auth.LogoutAsync(request?.RefreshToken ?? "", ClientIp, ct);
        return NoContent();
    }

    [HttpGet("sessions")]
    [Authorize]
    public async Task<IActionResult> Sessions(CancellationToken ct)
    {
        var sessions = await auth.ListSessionsAsync(
            RequiredTenantId, RequiredUserId, ct);
        return Ok(sessions);
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    [Authorize]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
    {
        var result = await auth.RevokeSessionAsync(
            RequiredTenantId, RequiredUserId, sessionId, ClientIp, ct);
        return result.Success ? NoContent() : Fail(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var result = await auth.ChangePasswordAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? NoContent() : Fail(result);
    }

    // ── helpers ────────────────────────────────────────────────────────

    public sealed record RefreshRequest(string? RefreshToken);
    public sealed record LogoutRequest(string? RefreshToken);

    private string? ClientIp =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

    private Guid RequiredTenantId =>
        Guid.TryParse(User.FindFirstValue("tid"), out var tid)
            ? tid
            : throw new InvalidOperationException(
                "Authenticated principal is missing the tid claim.");

    private Guid RequiredUserId =>
        Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),
            out var sub)
            ? sub
            : throw new InvalidOperationException(
                "Authenticated principal is missing the sub claim.");

    private IActionResult Fail<T>(AuthResult<T> result)
    {
        var (status, code) = Map(result.ErrorCode);
        if (status == StatusCodes.Status423Locked && result.RetryAfterSeconds is int retry)
            Response.Headers["Retry-After"] = retry.ToString();
        return StatusCode(status, new
        {
            code,
            message = result.Message
        });
    }

    /// <summary>Error code → (HTTP status, envelope code). The vocabulary
    /// follows API design §0: 401 never says WHICH part of the credential
    /// was wrong; 404 never reveals cross-tenant existence.</summary>
    private static (int status, string code) Map(AuthErrorCode error) => error switch
    {
        AuthErrorCode.ValidationFailed => (400, "auth.validation_failed"),
        AuthErrorCode.WeakPassword => (400, "auth.weak_password"),
        AuthErrorCode.CurrentPasswordWrong => (400, "auth.current_password_wrong"),
        AuthErrorCode.InvalidCredentials => (401, "auth.invalid_credentials"),
        AuthErrorCode.TokenInvalid => (401, "auth.token_invalid"),
        AuthErrorCode.TokenExpired => (401, "auth.token_expired"),
        AuthErrorCode.TokenReused => (401, "auth.token_reused"),
        AuthErrorCode.AccountInactive => (403, "auth.account_inactive"),
        AuthErrorCode.AccountLocked => (423, "auth.account_locked"),
        AuthErrorCode.SlugTaken => (409, "auth.slug_taken"),
        AuthErrorCode.SessionNotFound => (404, "auth.session_not_found"),
        AuthErrorCode.UserNotFound => (404, "auth.user_not_found"),
        AuthErrorCode.TenantNotFound => (404, "auth.tenant_not_found"),
        _ => (400, "auth.error")
    };
}
