using ProGlass.Saas.Domain.Subscriptions;

namespace ProGlass.Saas.Application.Subscriptions;

// ═══════════════════════════════════════════════════════════════════════
// Phase 18 — plan limit enforcement (architecture doc §6: "Limits enforced
// in application services (users, invoices/month, storage GB, optimization
// jobs/month, reports/month) — hard-coded nowhere").
//
// The MECHANISM only: values come from the catalog (plan_features /
// tenant_features), NULL = unlimited, and NOTHING is seeded except the
// WPF-derived 'users' limits — an operator sets the rest via the admin
// port (SetOverrideAsync / plan catalog), which is the deliberate
// business-decision point. The WPF has no such limits (single-machine
// desktop licensing); this is a SaaS platform concern per the design docs.
//
// The users limit is enforced by the seat guard in SubscriptionService
// (Phase 13). This guard covers the per-period counters: tax invoices
// ('tax_invoices'), statements ('reports') and optimization runs
// ('optimization') — counted since the CURRENT BILLING PERIOD start.
// Storage (GB) has no counter yet: no blobs are stored (the attachments
// module is deferred — documented).
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Resolves a feature's effective per-period limit for a tenant
/// (tenant override wins over the plan; a missing or disabled feature has
/// no limit here — the module feature gate in the middleware 403s those
/// routes before a service runs).</summary>
public sealed class PlanLimitGuard(ISubscriptionStore store)
{
    public sealed record LimitCheck(DateOnly PeriodStart, int? Limit);

    /// <summary>Null check = no subscription (those tenants are hard-blocked
    /// by the gate; defensively no limit). Limit null = unlimited.</summary>
    public async Task<LimitCheck?> ResolveAsync(
        Guid tenantId, string featureKey, CancellationToken ct)
    {
        var current = await store.GetCurrentAsync(tenantId, ct);
        if (current is null)
        {
            return null;
        }

        var features = FeatureResolution.Resolve(
            current.PlanFeatures, current.TenantOverrides);
        var limit = features.TryGetValue(featureKey, out var f) && f.IsEnabled
            ? f.LimitValue
            : null;
        return new LimitCheck(current.Subscription.PeriodStart, limit);
    }
}
