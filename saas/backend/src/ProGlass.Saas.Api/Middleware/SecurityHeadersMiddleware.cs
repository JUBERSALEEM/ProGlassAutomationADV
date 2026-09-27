using Microsoft.AspNetCore.Http;

namespace ProGlass.Saas.Api.Middleware;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 15 — production hardening middleware:
//    • SecurityHeadersMiddleware — the standard web hardening headers
//      (the WPF's attack surface was a desktop app; the API needs the
//      web equivalents).
//    • GlobalExceptionHandlerMiddleware — every unhandled exception
//      becomes the shared { code, message } envelope (API design §0)
//      with NO internals leaked; the details go to the log only.
//    • LoginRateLimitMiddleware — a fixed-window limiter on the auth
//      endpoints (the web analogue of the WPF's per-user lockout: the
//      WPF locks an account after N failures; the API ALSO throttles
//      per IP so an attacker cannot spray unlimited usernames).
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Sets the hardening response headers on every reply.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // API-only deployment: no iframe embedding of any kind.
        context.Response.Headers["X-Frame-Options"] = "DENY";
        // No MIME sniffing beyond the declared content type.
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        // The API is addressed by its own origin; never leak where a
        // client came from.
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        // An API serves JSON; scripts/styles are never executed from it.
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'none'; frame-ancestors 'none'";
        // The API never proxies arbitrary content.
        context.Response.Headers["X-Permitted-Cross-Domain-Policies"] =
            "none";
        // TLS terminates at the reverse proxy; keep the connection
        // hint private when it does.
        context.Response.Headers["Cross-Origin-Opener-Policy"] =
            "same-origin";

        await next(context);
    }
}
