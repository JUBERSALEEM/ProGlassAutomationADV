using System.Security.Claims;
using ProGlass.Saas.Domain.Tenancy;

namespace ProGlass.Saas.Api.Middleware;

/// <summary>
/// THE bridge between authentication and tenancy (ADR-009, master prompt
/// §24): after JwtBearer validates the access token, this middleware is
/// the ONLY writer of the request-scoped tenant context. It reads the
/// tenant id and user id from the authenticated PRINCIPAL — never from a
/// client-supplied body/query/header — and the whole data layer
/// (EF query filters + PostgreSQL RLS) then operates on exactly that
/// tenant. Unauthenticated requests keep the context unset and see
/// nothing (fail closed).
/// </summary>
public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AmbientTenantContext tenant)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantId = ParseClaim(context, "tid");
            var userId = ParseClaim(context,
                ClaimTypes.NameIdentifier, fallbackType: "sub");
            var username = context.User.FindFirstValue("preferred_username") ?? "";

            if (tenantId is null || userId is null)
            {
                // Authenticated but the token does not carry the claims the
                // tenancy layer needs — treat as unauthenticated (fail
                // closed) rather than running "on no tenant".
                await Results.Unauthorized().ExecuteAsync(context);
                return;
            }

            tenant.SetTenant(tenantId.Value);
            tenant.SetUser(userId, username);
        }

        await next(context);
    }

    private static Guid? ParseClaim(HttpContext context, string claimType,
        string? fallbackType = null)
    {
        var value = context.User.FindFirstValue(claimType)
                    ?? (fallbackType is null ? null : context.User.FindFirstValue(fallbackType));
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}
