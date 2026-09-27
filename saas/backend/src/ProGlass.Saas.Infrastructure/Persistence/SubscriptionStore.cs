using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProGlass.Saas.Application.Subscriptions;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Subscriptions;

namespace ProGlass.Saas.Infrastructure.Persistence;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 13 storage — tenant-side reads over the RLS-scoped context,
//  platform-side operations (suspend/reactivate/grant/override) on the
//  ELEVATED connection (the LicenseGenerator tool's successor; isolated
//  behind ISubscriptionAdminStore for the future payment provider).
// ═══════════════════════════════════════════════════════════════════════

public sealed class SubscriptionStore(SaasDbContextFactory factory)
    : ISubscriptionStore
{
    public async Task<ResolvedSubscription?> GetCurrentAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var sub = await db.Subscriptions.AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (sub is null)
        {
            return null;
        }

        var planFeatures = await db.PlanFeatures.AsNoTracking()
            .Include(pf => pf.Feature)
            .Where(pf => pf.PlanId == sub.PlanId)
            .ToDictionaryAsync(
                pf => pf.Feature.Key,
                pf => pf.LimitValue,
                StringComparer.OrdinalIgnoreCase, ct).ConfigureAwait(false);

        var overrides = await db.TenantFeatures.AsNoTracking()
            .Include(tf => tf.Feature)
            .Where(tf => tf.TenantId == tenantId)
            .ToDictionaryAsync(
                tf => tf.Feature.Key,
                tf => (tf.IsEnabled, tf.LimitValue),
                StringComparer.OrdinalIgnoreCase, ct).ConfigureAwait(false);

        return new ResolvedSubscription(sub, sub.Plan, overrides, planFeatures);
    }

    public async Task<Plan?> FindPlanByCodeAsync(
        string planCode, CancellationToken ct)
    {
        using var db = factory.CreatePlatform();
        return await db.Plans.AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Code == planCode, ct).ConfigureAwait(false);
    }

    public async Task<int> CountActiveUsersAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.Users.AsNoTracking()
            .CountAsync(u => u.TenantId == tenantId && u.IsActive, ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> TrySaveAsync(
        Subscription subscription, string? expectedStatus, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(subscription.TenantId);
        var stored = await db.Subscriptions
            .FirstOrDefaultAsync(
                s => s.Id == subscription.Id, ct).ConfigureAwait(false);
        if (stored is null)
        {
            if (expectedStatus is not null)
            {
                // A transition was requested against a subscription that
                // does not exist (or was replaced) — refuse.
                return false;
            }

            db.Subscriptions.Add(subscription);
        }
        else
        {
            if (expectedStatus is not null
                && !string.Equals(stored.Status, expectedStatus,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            db.Entry(stored).CurrentValues.SetValues(subscription);
        }

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<Plan>> ListPlansAsync(
        CancellationToken ct)
    {
        using var db = factory.CreatePlatform();
        return await db.Plans.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price).ThenBy(p => p.Code)
            .ToListAsync(ct).ConfigureAwait(false);
    }
}

/// <summary>
/// Platform-side subscription operations on the elevated connection —
/// the SaaS successor of the WPF LicenseGenerator tool. Not exposed over
/// HTTP (no platform-admin auth surface yet); the future payment
/// provider calls this port.
/// </summary>
public sealed class SubscriptionAdminStore : ISubscriptionAdminStore
{
    private readonly string _connectionString;

    public SubscriptionAdminStore(string platformConnectionString)
    {
        _connectionString = platformConnectionString;
    }

    public async Task<bool> SuspendAsync(
        Guid tenantId, string reason, CancellationToken ct)
    {
        return await ExecuteAsync(
            """
            UPDATE saas.subscriptions SET status = 'suspended',
              cancel_reason = @reason, updated_at = now()
            WHERE id = (SELECT id FROM saas.subscriptions
                        WHERE tenant_id = @tid
                        ORDER BY created_at DESC LIMIT 1)
              AND status NOT IN ('cancelled', 'suspended')
            """,
            ("tid", tenantId), ("reason", reason ?? "(no reason given)"), ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> ReactivateAsync(
        Guid tenantId, CancellationToken ct)
    {
        return await ExecuteAsync(
            """
            UPDATE saas.subscriptions SET status = 'active',
              updated_at = now()
            WHERE id = (SELECT id FROM saas.subscriptions
                        WHERE tenant_id = @tid
                        ORDER BY created_at DESC LIMIT 1)
              AND status = 'suspended'
            """,
            ("tid", tenantId), ct).ConfigureAwait(false);
    }

    public async Task<bool> GrantPlanAsync(
        Guid tenantId, string planCode, int periodDays, CancellationToken ct)
    {
        return await ExecuteAsync(
            """
            INSERT INTO saas.subscriptions
              (id, tenant_id, plan_id, status, period_start, period_end,
               created_at, updated_at)
            SELECT gen_random_uuid(), @tid, p.id, 'active',
                   CURRENT_DATE, CURRENT_DATE + @days::int, now(), now()
            FROM saas.plans p
            WHERE p.code = @plan
              AND NOT EXISTS (SELECT 1 FROM saas.subscriptions s
                              WHERE s.tenant_id = @tid
                                AND s.status = 'active')
            """,
            ("tid", tenantId), ("plan", planCode), ("days", periodDays), ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> SetOverrideAsync(
        Guid tenantId, string featureKey, bool enabled, int? limit,
        CancellationToken ct)
    {
        return await ExecuteAsync(
            """
            INSERT INTO saas.tenant_features
              (tenant_id, feature_id, is_enabled, limit_value)
            SELECT @tid, f.id, @enabled, @lim
            FROM saas.features f WHERE f.key = @fkey
            ON CONFLICT (tenant_id, feature_id)
            DO UPDATE SET is_enabled = EXCLUDED.is_enabled,
                          limit_value = EXCLUDED.limit_value
            """,
            ("tid", tenantId), ("fkey", featureKey),
            ("enabled", enabled), ("lim", (object?)limit ?? DBNull.Value), ct)
            .ConfigureAwait(false);
    }

    private async Task<bool> ExecuteAsync(
        string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync().ConfigureAwait(false) > 0;
    }
}
