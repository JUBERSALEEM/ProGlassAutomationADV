using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.Subscriptions;
using ProGlass.Saas.Domain.Subscriptions;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// The subscription core (Phase 13) against live PostgreSQL: the trial
/// seeded at registration (AutoTrialManager port), the WPF access states
/// (GracePeriod read-only / Expired hard block), plan changes with the
/// edition-tier feature resolution, cancellation, the platform admin
/// operations and tenant isolation.
/// </summary>
[Collection("database")]
public sealed class SubscriptionFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly SubscriptionService _sut;
    private readonly ISubscriptionStore _store;
    private readonly SubscriptionAdminStore _admin;
    private readonly TenantProvisioner _provisioner;

    public SubscriptionFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        _store = new SubscriptionStore(factory);
        _sut = new SubscriptionService(_store, audit);
        _admin = new SubscriptionAdminStore(fixture.OwnerDb);
        _provisioner = new TenantProvisioner(
            fixture.OwnerDb, NullLogger<TenantProvisioner>.Instance);
    }

    /// <summary>Inserts a subscription directly (owner connection) so a
    /// test can dial the period/status exactly.</summary>
    private async Task<Guid> SeedSubscriptionAsync(
        Guid tenantId, string status, DateOnly periodEnd,
        DateTime? trialEndsAt = null)
    {
        var id = Guid.NewGuid();
        using var connection = new Npgsql.NpgsqlConnection(_fixture.OwnerDb);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO saas.subscriptions
              (id, tenant_id, plan_id, status, period_start, period_end,
               trial_ends_at, created_at, updated_at)
            SELECT @id, @tid, p.id, @status, CURRENT_DATE - 30, @end,
                   @trial, now(), now()
            FROM saas.plans p WHERE p.code = @plan
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("tid", tenantId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("end", periodEnd);
        command.Parameters.AddWithValue(
            "trial", trialEndsAt ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("plan",
            status == "trialing" ? "trial" : "factory");
        await command.ExecuteNonQueryAsync();
        return id;
    }

    // ── registration seeds the trial (AutoTrialManager) ─────────────

    [Fact]
    public async Task Registration_Seeds_30DayTrial_WithAllFeatures()
    {
        var provisioned = await _provisioner.ProvisionAsync(
            $"Trial Co {Guid.NewGuid():N}", $"trial-{Guid.NewGuid():N}",
            "owner", "Owner", "owner@example.com", "hash", null,
            CancellationToken.None);
        var tenantId = provisioned.TenantId;

        var current = await _sut.GetCurrentAsync(tenantId, CancellationToken.None);
        Assert.Equal("trialing", current.Status);
        Assert.Equal(SubscriptionAccess.Trialing, current.Access);
        Assert.Equal("trial", current.PlanCode);
        Assert.Equal(30, current.DaysRemaining);
        Assert.False(current.ReadOnly);

        // The WPF trial edition: ALL features on.
        Assert.Contains(current.Features, f =>
            f.Key == ModuleFeatureMap.Optimization && f.IsEnabled);
        Assert.Contains(current.Features, f =>
            f.Key == ModuleFeatureMap.TaxInvoices && f.IsEnabled);
    }

    // ── access states ───────────────────────────────────────────────

    [Fact]
    public async Task PaidExpiry_GracePeriod_ReadOnly_ThenExpired_HardBlocked()
    {
        var tenant = _fixture.CreateTenant("sub-grace");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 2 days past a paid period end → GracePeriod (read-only).
        await SeedSubscriptionAsync(tenant, "active", today.AddDays(-2));
        var grace = await _sut.GetCurrentAsync(tenant, CancellationToken.None);
        Assert.Equal(SubscriptionAccess.GracePeriod, grace.Access);
        Assert.True(grace.ReadOnly);
        Assert.False(grace.HardBlocked);
        Assert.Contains("7-day grace period", grace.StatusMessage);

        // 10 days past → Expired (hard block, reads refused).
        var tenant2 = _fixture.CreateTenant("sub-expired");
        await SeedSubscriptionAsync(tenant2, "active", today.AddDays(-10));
        var expired = await _sut.GetCurrentAsync(tenant2, CancellationToken.None);
        Assert.Equal(SubscriptionAccess.Expired, expired.Access);
        Assert.True(expired.HardBlocked);
    }

    [Fact]
    public async Task TrialExpiry_IsReadOnly_WithTheWpfMessage()
    {
        var tenant = _fixture.CreateTenant("sub-trialexp");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedSubscriptionAsync(
            tenant, "trialing", today.AddDays(-1), DateTime.UtcNow.AddDays(-1));

        var current = await _sut.GetCurrentAsync(tenant, CancellationToken.None);
        Assert.Equal(SubscriptionAccess.TrialExpired, current.Access);
        Assert.True(current.ReadOnly);
        Assert.Equal("the 30-day trial has ended", current.StatusMessage);
    }

    [Fact]
    public async Task NoSubscription_ReadsAsHardBlocked()
    {
        var tenant = _fixture.CreateTenant("sub-none");
        var current = await _sut.GetCurrentAsync(tenant, CancellationToken.None);
        Assert.True(current.HardBlocked);
        Assert.Equal(0, current.UserLimit);
        Assert.Empty(current.Features);
        Assert.Contains("No subscription", current.PlanName);
    }

    // ── plan change + feature resolution ────────────────────────────

    [Fact]
    public async Task ChangePlan_ToBasic_UnlocksInquiryProformaOnly()
    {
        var tenant = _fixture.CreateTenant("sub-basic");
        await SeedSubscriptionAsync(
            tenant, "trialing",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10));

        var changed = await _sut.ChangePlanAsync(tenant, Guid.NewGuid(),
            new ChangePlanRequest("basic"), null, CancellationToken.None);
        Assert.True(changed.Success, changed.Message);

        var current = changed.Value!;
        Assert.Equal("basic", current.PlanCode);
        Assert.Equal(SubscriptionAccess.Active, current.Access);
        Assert.Equal(1, current.UserLimit);

        // The WPF Basic tier: "single user, inquiry + proforma only".
        var on = current.Features.Where(f => f.IsEnabled)
            .Select(f => f.Key).ToHashSet();
        Assert.Contains("inquiries", on);
        Assert.Contains("proformas", on);
        Assert.DoesNotContain("tax_invoices", on);
        Assert.DoesNotContain("optimization", on);
        Assert.DoesNotContain("sheet_store", on);
    }

    [Fact]
    public async Task ChangePlan_SeatGuard_RefusesDowngradeBelowSeats()
    {
        var tenant = _fixture.CreateTenant("sub-seats");

        // Two active users via the owner connection.
        using (var connection = new Npgsql.NpgsqlConnection(_fixture.OwnerDb))
        {
            await connection.OpenAsync();
            for (var i = 0; i < 2; i++)
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO saas.users
                      (id, tenant_id, username, display_name, password_hash,
                       role, is_active, created_at)
                    VALUES (@id, @tid, @u, @u, 'x', 'Viewer', true, now())
                    """;
                command.Parameters.AddWithValue("id", Guid.NewGuid());
                command.Parameters.AddWithValue("tid", tenant);
                command.Parameters.AddWithValue(
                    "u", $"seat{i}-{tenant:N}");
                await command.ExecuteNonQueryAsync();
            }
        }

        var refused = await _sut.ChangePlanAsync(tenant, Guid.NewGuid(),
            new ChangePlanRequest("basic"), null, CancellationToken.None);
        Assert.False(refused.Success);
        Assert.Equal(SubscriptionErrorCode.LimitReached, refused.ErrorCode);
        Assert.Contains("2 active users", refused.Message);
    }

    [Fact]
    public async Task ChangePlan_UnknownPlan_Refused()
    {
        var tenant = _fixture.CreateTenant("sub-unknown");
        var result = await _sut.ChangePlanAsync(tenant, Guid.NewGuid(),
            new ChangePlanRequest("platinum"), null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(SubscriptionErrorCode.PlanNotAvailable, result.ErrorCode);
    }

    // ── cancellation ────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_RecordsReason_AndStaysReadOnly()
    {
        var tenant = _fixture.CreateTenant("sub-cancel");
        await SeedSubscriptionAsync(
            tenant, "active",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20));

        var cancelled = await _sut.CancelAsync(tenant, Guid.NewGuid(),
            "  ", null, CancellationToken.None);   // blank → default
        Assert.True(cancelled.Success, cancelled.Message);
        Assert.Equal("cancelled", cancelled.Value!.Status);
        Assert.True(cancelled.Value.ReadOnly);
        Assert.False(cancelled.Value.HardBlocked);  // data preserved

        // Second cancel is machine-refused (cancelled is terminal).
        var again = await _sut.CancelAsync(tenant, Guid.NewGuid(),
            null, null, CancellationToken.None);
        Assert.False(again.Success);
        Assert.Equal(SubscriptionErrorCode.Conflict, again.ErrorCode);
    }

    // ── platform admin (LicenseGenerator successor) ─────────────────

    [Fact]
    public async Task Admin_Suspend_Reactivate_And_Override()
    {
        var tenant = _fixture.CreateTenant("sub-admin");
        await SeedSubscriptionAsync(
            tenant, "active",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30));

        Assert.True(await _admin.SuspendAsync(tenant, "chargeback", CancellationToken.None));
        var suspended = await _sut.GetCurrentAsync(tenant, CancellationToken.None);
        Assert.Equal(SubscriptionAccess.Suspended, suspended.Access);
        Assert.True(suspended.HardBlocked);

        Assert.True(await _admin.ReactivateAsync(tenant, CancellationToken.None));
        var active = await _sut.GetCurrentAsync(tenant, CancellationToken.None);
        Assert.Equal(SubscriptionAccess.Active, active.Access);

        // The LicensePlanCatalog knob: a tenant override switches the
        // optimization feature ON for a business plan.
        var business = await _sut.ChangePlanAsync(tenant, Guid.NewGuid(),
            new ChangePlanRequest("business"), null, CancellationToken.None);
        Assert.True(business.Success);
        Assert.DoesNotContain(business.Value!.Features,
            f => f.Key == ModuleFeatureMap.Optimization && f.IsEnabled);

        Assert.True(await _admin.SetOverrideAsync(
            tenant, ModuleFeatureMap.Optimization, true, null,
            CancellationToken.None));
        var overridden = await _sut.GetCurrentAsync(tenant, CancellationToken.None);
        var feature = Assert.Single(overridden.Features,
            f => f.Key == ModuleFeatureMap.Optimization);
        Assert.True(feature.IsEnabled);
        Assert.True(feature.FromTenantOverride);
    }

    [Fact]
    public async Task Admin_GrantPlan_IssuesAnActiveSubscription()
    {
        var tenant = _fixture.CreateTenant("sub-grant");
        Assert.True(await _admin.GrantPlanAsync(
            tenant, "factory", 365, CancellationToken.None));

        var current = await _sut.GetCurrentAsync(tenant, CancellationToken.None);
        Assert.Equal("factory", current.PlanCode);
        Assert.Equal("active", current.Status);
        Assert.Equal(SubscriptionAccess.Active, current.Access);
        Assert.Null(current.UserLimit);   // unlimited users
    }

    // ── tenant isolation ────────────────────────────────────────────

    [Fact]
    public async Task TenantIsolation_SubscriptionsArePerTenant()
    {
        var a = _fixture.CreateTenant("sub-iso-a");
        var b = _fixture.CreateTenant("sub-iso-b");
        await SeedSubscriptionAsync(
            a, "active", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30));

        var currentA = await _sut.GetCurrentAsync(a, CancellationToken.None);
        var currentB = await _sut.GetCurrentAsync(b, CancellationToken.None);
        Assert.Equal("active", currentA.Status);
        Assert.NotEqual(currentA.Id, currentB.Id);
        Assert.True(currentB.HardBlocked);   // B has no subscription
    }
}
