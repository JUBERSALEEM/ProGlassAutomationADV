using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Application.Subscriptions;
using ProGlass.Saas.Domain.Subscriptions;

namespace ProGlass.Saas.Api.Middleware;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 13 — the ReadOnlyGate's HTTP enforcement (mapping §1.11):
//  "Middleware blocking writes for suspended/expired tenants, same
//  explicit messaging."
//
//  WPF semantics (Services/Licensing/ReadOnlyGate.cs):
//    • READS ALWAYS PASS — "Your data is preserved"; the customer keeps
//      their data in read-only mode.
//    • Writes in read-only mode are REFUSED with the user-safe message
//      (ReadOnlyModeException.UserMessage) and the denial is AUDIT-
//      LOGGED before the refusal.
//    • The recovery path (activating a licence in Settings) stays
//      available — the SaaS equivalent (choosing a plan in Settings →
//      Subscription) must therefore remain writable.
//  Plus the SaaS-specific states: suspended/expired-past-grace tenants
//  are blocked entirely (the WPF "app refuses to start" states), and a
//  plan's feature flags gate the module routes (the WPF edition tiers:
//  Basic = "inquiry + proforma only").
//
//  Scoped services (store, audit) are METHOD-injected: middleware is
//  constructed once per pipeline, InvokeAsync resolves per request.
// ═══════════════════════════════════════════════════════════════════════

public sealed class SubscriptionGateMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> ReadVerbs = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "OPTIONS"
    };

    // Auth must always work (an expired tenant still logs in — WPF: the
    // app opens read-only, it does not lock the user out).
    private const string AuthRoute = "/api/v1/auth";

    // The recovery path — subscription management itself stays writable
    // in read-only mode (WPF: the licence settings page stays usable).
    private const string SubscriptionRoute = "/api/v1/subscriptions";

    public async Task InvokeAsync(
        HttpContext context, ISubscriptionStore store, IAuditWriter audit)
    {
        var path = context.Request.Path.Value ?? "";

        // Unauthenticated + auth routes: no subscription check (login,
        // refresh, register are how a blocked tenant recovers).
        if (path.MatchesModule(AuthRoute)
            || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var tid = Guid.TryParse(
            context.User.FindFirst("tid")?.Value, out var t) ? t : Guid.Empty;
        if (tid == Guid.Empty)
        {
            await next(context);
            return;
        }

        var current = await store.GetCurrentAsync(tid, context.RequestAborted);
        if (current is null)
        {
            await next(context);
            return;
        }

        var access = SubscriptionGate.Resolve(
            current.Subscription.Status,
            current.Subscription.Status == SubscriptionStatuses.Trialing
            && current.Subscription.TrialEndsAt.HasValue
                ? DateOnly.FromDateTime(current.Subscription.TrialEndsAt.Value)
                : current.Subscription.PeriodEnd,
            DateTime.UtcNow);

        // 1) Module feature gate — the plan decides which modules exist
        //    (WPF edition tiers). Applies to reads AND writes.
        var featureKey = ModuleFeatureMap.FeatureFor(path);
        if (featureKey is not null)
        {
            var features = FeatureResolution.Resolve(
                current.PlanFeatures, current.TenantOverrides);
            if (!FeatureResolution.IsEnabled(features, featureKey))
            {
                await DenyAsync(context, audit, tenantId: tid, 403,
                    "subscription.feature_disabled",
                    $"The '{current.Plan.Code}' plan does not include this " +
                    "module. Upgrade your plan in Settings → Subscription to " +
                    "unlock it.");
                return;
            }
        }

        // 2) Read-only gate — reads pass ("Your data is preserved"),
        //    writes refuse with the WPF messaging.
        if (!ReadVerbs.Contains(context.Request.Method)
            && !path.MatchesModule(SubscriptionRoute)
            && SubscriptionGate.IsReadOnly(access))
        {
            await DenyAsync(context, audit, tid, 403,
                "subscription.read_only",
                SubscriptionGate.ReadOnlyMessage(access));
            return;
        }

        // 3) Hard block — suspended or past-grace-expiry: no data at all
        //    (the WPF "app refuses to start" analogue).
        if (!SubscriptionGate.CanRead(access))
        {
            await DenyAsync(context, audit, tid, 403, "subscription.blocked",
                "Subscription: " + SubscriptionGate.StatusText(access) +
                ". Contact the platform administrator to restore access.");
            return;
        }

        await next(context);
    }

    /// <summary>The ReadOnlyGate pattern: audit-log the denial BEFORE
    /// refusing (a denial that was never recorded is a compliance
    /// hole), then return the error envelope (API design §0).</summary>
    private static async Task DenyAsync(
        HttpContext context, IAuditWriter audit, Guid tenantId,
        int statusCode, string code, string message)
    {
        var actor = Guid.TryParse(
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value, out var a) ? a : null;

        try
        {
            await audit.WriteAsync(tenantId, actor,
                "subscription.gate_denied", "http",
                null, context.Connection.RemoteIpAddress?.ToString(), null,
                null, JsonSerializer.Serialize(new
                {
                    method = context.Request.Method,
                    path = context.Request.Path.Value,
                    code
                }), context.RequestAborted);
        }
        catch
        {
            // The WPF gate swallows audit-write failures the same way
            // (ReadOnlyGate: "audit-write failed" warning, still throws).
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new { code, message }),
            context.RequestAborted);
    }
}

internal static class SubscriptionPathExtensions
{
    /// <summary>"/api/v1/auth" matches "/api/v1/auth/…" (and the bare
    /// route itself).</summary>
    public static bool MatchesModule(this string path, string prefix) =>
        path == prefix
        || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
}
