using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace ProGlass.Saas.Api.Middleware;

/// <summary>
/// A fixed-window rate limiter on the credential endpoints — the web
/// analogue of the WPF lockout (which locks a USERNAME after N
/// failures; the API throttles the IP as well so an attacker cannot
/// spray unlimited usernames, each under its own lockout budget).
///
/// Deliberately in-memory: single-instance deployments need no shared
/// store; a multi-instance deployment swaps this for the reverse
/// proxy's limiter (documented in SAAS_DEPLOYMENT.md).
/// </summary>
public sealed class LoginRateLimitMiddleware(RequestDelegate next)
{
    public const int MaxAttemptsPerWindow = 20;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private const string LoginPath = "/api/v1/auth/login";
    private const string RegisterPath = "/api/v1/auth/register";

    private sealed record WindowState(int Count, DateTime WindowStartUtc);

    private static readonly ConcurrentDictionary<string, WindowState> Windows = new();

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (path != LoginPath && path != RegisterPath)
        {
            await next(context);
            return;
        }

        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTime.UtcNow;
        var state = Windows.GetOrAdd(key, _ => new WindowState(0, now));

        if (now - state.WindowStartUtc >= Window)
        {
            state = new WindowState(0, now);
        }

        if (state.Count >= MaxAttemptsPerWindow)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            context.Response.Headers["Retry-After"] =
                ((int)(Window - (now - state.WindowStartUtc)).TotalSeconds)
                .ToString();
            await context.Response.WriteAsync(
                $$"""{"code":"auth.rate_limited","message":"Too many attempts from this address. Try again in a minute."}""");
            return;
        }

        Windows[key] = state with { Count = state.Count + 1 };
        await next(context);
    }
}
