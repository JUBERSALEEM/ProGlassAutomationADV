namespace ProGlass.Saas.Application.Auth;

/// <summary>
/// Authentication policy. Lockout values are the WPF constants, verbatim
/// (Services/Audit/AuditService.Part2.cs, read 2026-09-27):
/// MaxFailedAttemptsBeforeLock = 5, LockoutMinutes = 5 — consecutive
/// failures persist ACROSS restarts, the counter resets when the lockout
/// window triggers, and a successful sign-in clears all lockout state.
/// </summary>
public sealed class AuthOptions
{
    /// <summary>Consecutive failed attempts allowed before a lockout window
    /// (WPF constant).</summary>
    public int MaxFailedAttemptsBeforeLock { get; set; } = 5;

    /// <summary>Length of the lockout window after the threshold is hit
    /// (WPF constant).</summary>
    public int LockoutMinutes { get; set; } = 5;

    /// <summary>Access token lifetime in minutes (short-lived; the refresh
    /// token carries the session).</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh token sliding lifetime in days.</summary>
    public int RefreshTokenDays { get; set; } = 30;

    /// <summary>Refresh token absolute lifetime in days — after this the
    /// user must re-authenticate regardless of activity.</summary>
    public int AbsoluteRefreshTokenDays { get; set; } = 60;

    /// <summary>Entropy of the refresh token secret, in bytes.</summary>
    public int RefreshTokenSecretBytes { get; set; } = 32;
}
