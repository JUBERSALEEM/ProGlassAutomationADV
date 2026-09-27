using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Subscriptions;

namespace ProGlass.Saas.Application.Subscriptions;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 13 — Subscriptions (WPF Services/Licensing → SaaS). The tenant
//  reads its plan/features/usage and manages its subscription; the
//  platform side (suspend/reactivate/grant/overrides) lives behind
//  ISubscriptionAdminStore on the elevated connection — isolated for the
//  future payment provider (mapping §1.11), not HTTP-exposed yet.
// ═══════════════════════════════════════════════════════════════════════

public enum SubscriptionErrorCode
{
    None = 0,
    ValidationFailed,
    NotFound,
    Conflict,
    PlanNotAvailable,
    LimitReached
}

public sealed record SubscriptionResult<T>(
    bool Success, T? Value, SubscriptionErrorCode ErrorCode, string Message)
{
    public static SubscriptionResult<T> Ok(T value) =>
        new(true, value, SubscriptionErrorCode.None, "");

    public static SubscriptionResult<T> Fail(
        SubscriptionErrorCode code, string message) =>
        new(false, default, code, message);
}

// ── DTOs ────────────────────────────────────────────────────────────

public sealed record FeatureDto(
    string Key, bool IsEnabled, int? LimitValue, bool FromTenantOverride);

public sealed record SubscriptionDto(
    Guid Id,
    string PlanCode,
    string PlanName,
    string Status,
    SubscriptionAccess Access,
    bool ReadOnly,
    bool HardBlocked,
    string StatusMessage,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int DaysRemaining,
    int ActiveUsers,
    int? UserLimit,
    IReadOnlyList<FeatureDto> Features);

public sealed record ChangePlanRequest(string PlanCode);

// ── Ports ───────────────────────────────────────────────────────────

public interface ISubscriptionStore
{
    /// <summary>The tenant's CURRENT subscription (latest created) with
    /// plan + resolved features, or null when none exists (a tenant
    /// provisioned before Phase 13).</summary>
    Task<ResolvedSubscription?> GetCurrentAsync(
        Guid tenantId, CancellationToken ct);

    Task<Plan?> FindPlanByCodeAsync(
        string planCode, CancellationToken ct);

    Task<int> CountActiveUsersAsync(Guid tenantId, CancellationToken ct);

    /// <summary>Inserts or replaces the tenant's current subscription
    /// (status machine-guarded by the caller).</summary>
    Task<bool> TrySaveAsync(
        Subscription subscription, string? expectedStatus, CancellationToken ct);

    Task<IReadOnlyList<Plan>> ListPlansAsync(CancellationToken ct);
}

/// <summary>The resolved current subscription — everything the gate and
/// the DTO need in one read.</summary>
public sealed record ResolvedSubscription(
    Subscription Subscription,
    Plan Plan,
    IReadOnlyDictionary<string, (bool Enabled, int? Limit)> TenantOverrides,
    IReadOnlyDictionary<string, int?> PlanFeatures);

/// <summary>
/// Platform-side operations on the ELEVATED connection (the WPF
/// LicenseGenerator tool's SaaS successor — isolated behind this port
/// for the future payment provider; NOT HTTP-exposed).
/// </summary>
public interface ISubscriptionAdminStore
{
    Task<bool> SuspendAsync(Guid tenantId, string reason, CancellationToken ct);
    Task<bool> ReactivateAsync(Guid tenantId, CancellationToken ct);

    /// <summary>Grants a plan (status active, period from today for the
    /// plan's billing period; the LicenseGenerator "issue a license"
    /// analogue).</summary>
    Task<bool> GrantPlanAsync(
        Guid tenantId, string planCode, int periodDays, CancellationToken ct);

    /// <summary>Sets a tenant feature override (the LicensePlanCatalog
    /// admin knob).</summary>
    Task<bool> SetOverrideAsync(
        Guid tenantId, string featureKey, bool enabled, int? limit,
        CancellationToken ct);
}

// ── Service ─────────────────────────────────────────────────────────

public sealed class SubscriptionService(
    ISubscriptionStore store, IAuditWriter audit)
{
    private const int MonthlyPeriodDays = 30;

    /// <summary>GET /api/v1/subscriptions/current — the tenant's plan,
    /// resolved access state (WPF LicenseStatus shape), features and
    /// seat usage. A tenant with NO subscription reads as an
    /// EXPIRED-style hard block (never invented access).</summary>
    public async Task<SubscriptionDto> GetCurrentAsync(
        Guid tenantId, CancellationToken ct)
    {
        var users = await store.CountActiveUsersAsync(tenantId, ct);
        var current = await store.GetCurrentAsync(tenantId, ct);
        if (current is null)
        {
            // Pre-Phase-13 tenant without a subscription row: the WPF
            // analogue is NotActivated — no writes, data preserved.
            return new SubscriptionDto(
                Guid.Empty, "none", "No subscription", "none",
                SubscriptionAccess.Expired, ReadOnly: true,
                HardBlocked: true,
                StatusMessage:
                    "No subscription exists for this workspace. Contact " +
                    "the platform administrator to activate a plan.",
                PeriodStart: DateOnly.FromDateTime(DateTime.UtcNow),
                PeriodEnd: DateOnly.FromDateTime(DateTime.UtcNow),
                DaysRemaining: 0, ActiveUsers: users, UserLimit: 0,
                Features: Array.Empty<FeatureDto>());
        }

        return BuildDto(current, users);
    }

    private static SubscriptionDto BuildDto(
        ResolvedSubscription current, int users)
    {
        var now = DateTime.UtcNow;
        var access = SubscriptionGate.Resolve(
            current.Subscription.Status,
            EffectivePeriodEnd(current.Subscription), now);
        var features = FeatureResolution.Resolve(
            current.PlanFeatures, current.TenantOverrides);
        var userLimit = features.TryGetValue(
            ModuleFeatureMap.Users, out var uf) ? uf.LimitValue : null;
        var today = DateOnly.FromDateTime(now);
        var end = EffectivePeriodEnd(current.Subscription) ?? today;

        return new SubscriptionDto(
            current.Subscription.Id,
            current.Plan.Code, current.Plan.Name,
            current.Subscription.Status, access,
            SubscriptionGate.IsReadOnly(access),
            SubscriptionGate.IsHardBlocked(access),
            SubscriptionGate.StatusText(access),
            current.Subscription.PeriodStart, end,
            Math.Max(0, end.DayNumber - today.DayNumber),
            users, userLimit,
            features.Values
                .Select(f => new FeatureDto(
                    f.Key, f.IsEnabled, f.LimitValue, f.FromTenantOverride))
                .OrderBy(f => f.Key, StringComparer.Ordinal)
                .ToList());
    }

    /// <summary>A trialing subscription's effective end is its trial end
    /// (TrialEndsAt), every other status uses the paid period end.</summary>
    private static DateOnly? EffectivePeriodEnd(Subscription s) =>
        string.Equals(s.Status, SubscriptionStatuses.Trialing,
            StringComparison.OrdinalIgnoreCase)
            ? (s.TrialEndsAt.HasValue
                ? DateOnly.FromDateTime(s.TrialEndsAt.Value) : (DateOnly?)s.PeriodEnd)
            : s.PeriodEnd;

    /// <summary>GET /api/v1/subscriptions/plans — the public catalog
    /// (the WPF edition tiers).</summary>
    public async Task<IReadOnlyList<Plan>> ListPlansAsync(
        CancellationToken ct) =>
        await store.ListPlansAsync(ct);

    /// <summary>POST /current/change-plan — switch the tenant's plan
    /// (Settings.Manage). Basic's "inquiry + proforma only" tiering is
    /// enforced by the API gate, not by refusing the switch.</summary>
    public async Task<SubscriptionResult<SubscriptionDto>> ChangePlanAsync(
        Guid tenantId, Guid actorId, ChangePlanRequest request,
        string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PlanCode))
        {
            return SubscriptionResult<SubscriptionDto>.Fail(
                SubscriptionErrorCode.ValidationFailed,
                "A plan code is required.");
        }

        var plan = await store.FindPlanByCodeAsync(
            request.PlanCode.Trim(), ct);
        if (plan is null || !plan.IsActive)
        {
            return SubscriptionResult<SubscriptionDto>.Fail(
                SubscriptionErrorCode.PlanNotAvailable,
                $"Plan '{request.PlanCode}' is not available.");
        }

        var users = await store.CountActiveUsersAsync(tenantId, ct);
        var current = await store.GetCurrentAsync(tenantId, ct);

        // Seat guard: switching below the current seat count is refused
        // (users would be locked out mid-work).
        if (plan.Code == "basic" && users > 1)
        {
            return SubscriptionResult<SubscriptionDto>.Fail(
                SubscriptionErrorCode.LimitReached,
                $"The Basic plan allows a single user; this workspace has " +
                $"{users} active users. Deactivate users before downgrading.");
        }

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var periodDays = plan.BillingPeriod == "yearly" ? 365 : MonthlyPeriodDays;
        var sub = new Subscription
        {
            Id = current?.Subscription.Id ?? Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = plan.Id,
            Status = SubscriptionStatuses.Active,
            PeriodStart = today,
            PeriodEnd = today.AddDays(periodDays),
            TrialEndsAt = current?.Subscription.TrialEndsAt,
            CancelReason = current?.Subscription.CancelReason,
            CreatedAt = current?.Subscription.CreatedAt ?? now,
            UpdatedAt = now,
            CreatedBy = current?.Subscription.CreatedBy ?? actorId,
            UpdatedBy = actorId
        };

        if (!await store.TrySaveAsync(sub, current?.Subscription.Status, ct))
        {
            return SubscriptionResult<SubscriptionDto>.Fail(
                SubscriptionErrorCode.Conflict,
                "The subscription changed concurrently — reload and retry.");
        }

        await audit.WriteAsync(tenantId, actorId, "subscription.plan_changed",
            "subscriptions", sub.Id, ip, null, null,
            $"{{\"plan\":\"{plan.Code}\"}}", ct);
        return SubscriptionResult<SubscriptionDto>.Ok(
            await GetCurrentAsync(tenantId, ct));
    }

    /// <summary>POST /current/cancel — the WPF soft-close pattern:
    /// reason recorded ("(no reason given)" default), the row survives,
    /// reads stay available (Cancelled = read-only, recoverable).</summary>
    public async Task<SubscriptionResult<SubscriptionDto>> CancelAsync(
        Guid tenantId, Guid actorId, string? reason, string? ip,
        CancellationToken ct)
    {
        var current = await store.GetCurrentAsync(tenantId, ct);
        if (current is null)
        {
            return SubscriptionResult<SubscriptionDto>.Fail(
                SubscriptionErrorCode.NotFound,
                "No subscription exists for this workspace.");
        }

        if (!SubscriptionStateMachine.CanTransition(
                current.Subscription.Status, SubscriptionStatuses.Cancelled))
        {
            return SubscriptionResult<SubscriptionDto>.Fail(
                SubscriptionErrorCode.Conflict,
                SubscriptionStateMachine.Reason(
                    current.Subscription.Status,
                    SubscriptionStatuses.Cancelled));
        }

        var sub = current.Subscription;
        sub.Status = SubscriptionStatuses.Cancelled;
        sub.CancelReason = string.IsNullOrWhiteSpace(reason)
            ? "(no reason given)"
            : reason.Trim();
        sub.UpdatedAt = DateTime.UtcNow;
        sub.UpdatedBy = actorId;
        if (!await store.TrySaveAsync(sub, null, ct))
        {
            return SubscriptionResult<SubscriptionDto>.Fail(
                SubscriptionErrorCode.Conflict,
                "The subscription changed concurrently — reload and retry.");
        }

        await audit.WriteAsync(tenantId, actorId, "subscription.cancelled",
            "subscriptions", sub.Id, ip, null, null,
            $"{{\"reason\":\"{sub.CancelReason}\"}}", ct);
        return SubscriptionResult<SubscriptionDto>.Ok(
            await GetCurrentAsync(tenantId, ct));
    }
}
