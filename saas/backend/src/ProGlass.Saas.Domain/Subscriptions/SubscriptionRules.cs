namespace ProGlass.Saas.Domain.Subscriptions;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 13 — Subscriptions (the WPF Licensing module's SaaS analogue).
//
//  WPF ground truth (Services/Licensing/, read 2026-09-27):
//    • LicensePayload.LicenseEdition — Trial "30-day, single-activation,
//      all features"; Basic "single user, inquiry + proforma only";
//      Business "up to 5 users, full ERP without production
//      optimisation"; Factory "unlimited users, includes cutting-
//      optimisation engine"; Enterprise "unlimited + multi-site +
//      custom integrations".
//    • LicenseService.AutoTrialDays = 30 — the automatic evaluation
//      window issued on first run (AutoTrialManager).
//    • LicenseVerifier.ExpiryWarningDays = 14 — ExpiringSoon status.
//    • LicenseVerifier.GracePeriod = 7 days past expiry — the app keeps
//      running in READ-ONLY mode (data preserved, writes blocked), then
//      refuses outright.
//    • LicenseVerificationResult.ReadOnly = GracePeriod || TrialExpired.
//    • ReadOnlyGate.Demand — the service-layer write gate; denial
//      audit-logged BEFORE the throw; user-safe message: "The app is in
//      read-only mode. … Your data is preserved. Activate a full licence
//      in Settings → License to keep creating and editing records."
//
//  The SaaS replaces the signed per-machine license with a tenant
//  subscription (plans/features/subscriptions tables, Phase 3). The
//  STATUS vocabulary is the DB CHECK (0001): trialing/active/past_due/
//  suspended/cancelled — a machine is provided in the same style as the
//  WPF StatusTransitions tables.
// ═══════════════════════════════════════════════════════════════════════

public static class SubscriptionStatuses
{
    public const string Trialing = "trialing";
    public const string Active = "active";
    public const string PastDue = "past_due";
    public const string Suspended = "suspended";
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Trialing, Active, PastDue, Suspended, Cancelled
    };
}

/// <summary>The subscription status machine (SaaS-specific — the WPF has
/// no subscription transitions; the shape mirrors the ported WPF
/// machines: forward chain, self-transitions, one recovery edge, one
/// terminal state).</summary>
public static class SubscriptionStateMachine
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Table =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [SubscriptionStatuses.Trialing] = new[]
            {
                SubscriptionStatuses.Trialing, SubscriptionStatuses.Active,
                SubscriptionStatuses.PastDue, SubscriptionStatuses.Cancelled
            },
            [SubscriptionStatuses.Active] = new[]
            {
                SubscriptionStatuses.Active, SubscriptionStatuses.PastDue,
                SubscriptionStatuses.Suspended, SubscriptionStatuses.Cancelled
            },
            [SubscriptionStatuses.PastDue] = new[]
            {
                SubscriptionStatuses.PastDue, SubscriptionStatuses.Active,
                SubscriptionStatuses.Suspended, SubscriptionStatuses.Cancelled
            },
            // Suspended recovers by payment (→ active) or ends cancelled.
            [SubscriptionStatuses.Suspended] = new[]
            {
                SubscriptionStatuses.Suspended, SubscriptionStatuses.Active,
                SubscriptionStatuses.Cancelled
            },
            [SubscriptionStatuses.Cancelled] = new[] { SubscriptionStatuses.Cancelled }
        };

    public static bool CanTransition(string? current, string next)
    {
        if (!Table.TryGetValue((current ?? "").Trim(), out var allowed))
        {
            return false;
        }

        return allowed.Contains(next.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public static string Reason(string? current, string next) =>
        $"Subscription status change from '{(current ?? "").Trim()}' to " +
        $"'{next.Trim()}' is not allowed.";
}

/// <summary>The resolved access state — the SaaS shape of the WPF
/// LicenseStatus, reduced to what a multi-tenant API needs: is the
/// tenant writable, and if not, why (the exact WPF messaging).</summary>
public enum SubscriptionAccess
{
    /// <summary>Valid, expires in more than 14 days (WPF Valid).</summary>
    Active = 0,

    /// <summary>Trial window still running (WPF Trial badge).</summary>
    Trialing = 1,

    /// <summary>Expires within 14 days — renewal banner (WPF
    /// ExpiringSoon / LicenseVerifier.ExpiryWarningDays).</summary>
    ExpiringSoon = 2,

    /// <summary>Past expiry but inside the 7-day grace window — READ-ONLY
    /// (WPF GracePeriod: "the paid licence expired but is still in its
    /// 7-day grace period").</summary>
    GracePeriod = 3,

    /// <summary>Trial window ended — READ-ONLY (WPF TrialExpired: "the
    /// 30-day trial has ended").</summary>
    TrialExpired = 4,

    /// <summary>Past expiry with no grace left — no access at all (WPF
    /// Expired: the app refuses to start).</summary>
    Expired = 5,

    /// <summary>Suspended by the platform (billing) or cancelled — no
    /// access (no WPF equivalent; SaaS billing state).</summary>
    Suspended = 6,

    /// <summary>Cancelled subscription — read-only, recoverable by
    /// re-subscribing.</summary>
    Cancelled = 7
}

/// <summary>
/// Pure subscription gating rules — the WPF licensing constants and the
/// ReadOnlyGate semantics, ported onto the subscription tables.
/// </summary>
public static class SubscriptionGate
{
    /// <summary>WPF LicenseService.AutoTrialDays — the automatic
    /// evaluation window.</summary>
    public const int TrialDays = 30;

    /// <summary>WPF LicenseVerifier.ExpiryWarningDays — ExpiringSoon
    /// threshold.</summary>
    public const int ExpiryWarningDays = 14;

    /// <summary>WPF LicenseVerifier.GracePeriod — 7 days past expiry in
    /// read-only mode.</summary>
    public const int GraceDays = 7;

    /// <summary>
    /// Resolves the access state (WPF precedence: the FIRST applicable
    /// status wins, LicensePayload doc comment).
    /// </summary>
    /// <param name="status">DB status (trialing/active/past_due/
    /// suspended/cancelled).</param>
    /// <param name="periodEnd">The paid period end (or the trial end for
    /// a trialing subscription).</param>
    /// <param name="now">UTC now.</param>
    public static SubscriptionAccess Resolve(
        string? status, DateOnly? periodEnd, DateTime now)
    {
        var s = (status ?? "").Trim();
        if (string.Equals(s, SubscriptionStatuses.Suspended,
                StringComparison.OrdinalIgnoreCase))
        {
            return SubscriptionAccess.Suspended;
        }

        if (string.Equals(s, SubscriptionStatuses.Cancelled,
                StringComparison.OrdinalIgnoreCase))
        {
            return SubscriptionAccess.Cancelled;
        }

        var today = DateOnly.FromDateTime(now);
        if (periodEnd.HasValue)
        {
            if (today > periodEnd.Value)
            {
                var pastDueDays = today.DayNumber - periodEnd.Value.DayNumber;
                var isTrial = string.Equals(s, SubscriptionStatuses.Trialing,
                    StringComparison.OrdinalIgnoreCase);
                if (pastDueDays > GraceDays)
                {
                    // WPF Expired: past expiry, no grace left — for a
                    // TRIAL there is no grace at all (the trial's
                    // recovery path is a brand-new activation), matching
                    // LicenseService's TrialExpired/Expired split.
                    return isTrial
                        ? SubscriptionAccess.TrialExpired
                        : SubscriptionAccess.Expired;
                }

                return isTrial
                    ? SubscriptionAccess.TrialExpired
                    : SubscriptionAccess.GracePeriod;
            }

            var daysLeft = periodEnd.Value.DayNumber - today.DayNumber;
            if (daysLeft <= ExpiryWarningDays)
            {
                return string.Equals(s, SubscriptionStatuses.Trialing,
                    StringComparison.OrdinalIgnoreCase)
                    ? SubscriptionAccess.Trialing
                    : SubscriptionAccess.ExpiringSoon;
            }
        }

        return string.Equals(s, SubscriptionStatuses.Trialing,
            StringComparison.OrdinalIgnoreCase)
            ? SubscriptionAccess.Trialing
            : SubscriptionAccess.Active;
    }

    /// <summary>WPF LicenseVerificationResult.ReadOnly — true when the
    /// tenant keeps READ access but every write is blocked (GracePeriod
    /// or TrialExpired).</summary>
    public static bool IsReadOnly(SubscriptionAccess access) =>
        access is SubscriptionAccess.GracePeriod
            or SubscriptionAccess.TrialExpired
            or SubscriptionAccess.Cancelled;

    /// <summary>Writes are blocked entirely (no read-only grace):
    /// suspended, or past the grace window.</summary>
    public static bool IsHardBlocked(SubscriptionAccess access) =>
        access is SubscriptionAccess.Suspended
            or SubscriptionAccess.Expired;

    /// <summary>Reads (GET) are allowed unless hard-blocked — the WPF
    /// read-only promise: "Your data is preserved."</summary>
    public static bool CanRead(SubscriptionAccess access) =>
        !IsHardBlocked(access);

    /// <summary>
    /// The WPF ReadOnlyModeException.UserMessage, adapted to the
    /// subscription vocabulary (same sentences, "licence" → plan).
    /// </summary>
    public static string ReadOnlyMessage(SubscriptionAccess access) =>
        "The workspace is in read-only mode.\n\n" +
        "Reason: " + StatusText(access) + "\n\n" +
        "Your data is preserved. Choose a plan in Settings → Subscription " +
        "to keep creating and editing records.";

    /// <summary>WPF ReadOnlyModeException.StatusText, verbatim shape.</summary>
    public static string StatusText(SubscriptionAccess s) => s switch
    {
        SubscriptionAccess.TrialExpired =>
            "the 30-day trial has ended",
        SubscriptionAccess.GracePeriod =>
            "the paid plan expired but is still in its 7-day grace period",
        SubscriptionAccess.Cancelled =>
            "the subscription was cancelled",
        SubscriptionAccess.Suspended =>
            "the subscription is suspended (billing)",
        SubscriptionAccess.Expired =>
            "the subscription expired and its grace period has ended",
        SubscriptionAccess.ExpiringSoon =>
            "the subscription expires soon",
        _ => "the current plan does not permit writes"
    };
}

/// <summary>
/// The module → feature-key map (the WPF edition doc comments made
/// concrete): which subscription feature gates which API module.
/// Routes not listed have no feature requirement.
/// </summary>
public static class ModuleFeatureMap
{
    // feature keys (seeded in migration 0017 from the LicenseEdition
    // tier doc comments)
    public const string Users = "users";
    public const string Inquiries = "inquiries";
    public const string Proformas = "proformas";
    public const string JobOrders = "job_orders";
    public const string Delivery = "delivery";
    public const string TaxInvoices = "tax_invoices";
    public const string SheetStore = "sheet_store";
    public const string Production = "production";
    public const string Optimization = "optimization";
    public const string Reports = "reports";
    public const string MultiSite = "multi_site";
    public const string CustomIntegrations = "custom_integrations";

    /// <summary>API route prefix → feature key. The FIRST matching
    /// prefix wins (longest first). No entry → no feature gate.</summary>
    public static readonly IReadOnlyList<(string Prefix, string Feature)> Routes =
    new[]
    {
        ("api/v1/inquiries", Inquiries),
        ("api/v1/proforma-invoices", Proformas),
        ("api/v1/job-orders", JobOrders),
        ("api/v1/production", Production),
        ("api/v1/reservations", Production),
        ("api/v1/delivery-notes", Delivery),
        ("api/v1/fiscal", TaxInvoices),
        ("api/v1/stock", SheetStore),
        ("api/v1/optimization", Optimization),
        ("api/v1/pricing", Reports)
    };

    /// <summary>Resolves the feature key a path belongs to (null → no
    /// gate). Pure string match — the API layer passes the path.</summary>
    public static string? FeatureFor(string? path)
    {
        var p = (path ?? "").TrimStart('/');
        foreach (var (prefix, feature) in Routes)
        {
            if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return feature;
            }
        }

        return null;
    }
}

/// <summary>
/// Feature resolution: the plan's features ∪ tenant overrides (an
/// override wins — that is the override's purpose, the LicensePlanCatalog
/// admin knob).
/// </summary>
public static class FeatureResolution
{
    public sealed record EffectiveFeature(
        string Key, bool IsEnabled, int? LimitValue, bool FromTenantOverride);

    public static IReadOnlyDictionary<string, EffectiveFeature> Resolve(
        IReadOnlyDictionary<string, int?> planFeatures,
        IReadOnlyDictionary<string, (bool Enabled, int? Limit)> tenantOverrides)
    {
        var result = new Dictionary<string, EffectiveFeature>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var (key, limit) in planFeatures)
        {
            result[key] = new EffectiveFeature(key, true, limit, false);
        }

        foreach (var (key, value) in tenantOverrides)
        {
            result[key] = new EffectiveFeature(
                key, value.Enabled, value.Limit, true);
        }

        return result;
    }

    /// <summary>True when the feature is enabled for the tenant (a
    /// missing feature is DISABLED — Basic's "inquiry + proforma only"
    /// means everything else is simply absent).</summary>
    public static bool IsEnabled(
        IReadOnlyDictionary<string, EffectiveFeature> features, string key) =>
        features.TryGetValue(key, out var f) && f.IsEnabled;

    /// <summary>A used count against a limit (null limit = unlimited,
    /// WPF Factory/Enterprise "unlimited users").</summary>
    public static bool WithinLimit(
        IReadOnlyDictionary<string, EffectiveFeature> features,
        string key, int used) =>
        !features.TryGetValue(key, out var f)
            || !f.IsEnabled
            || f.LimitValue is not int limit
            || used < limit;
}
