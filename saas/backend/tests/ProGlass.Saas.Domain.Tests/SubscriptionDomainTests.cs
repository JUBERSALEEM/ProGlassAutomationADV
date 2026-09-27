using ProGlass.Saas.Domain.Subscriptions;

namespace ProGlass.Saas.Domain.Tests;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 13 subscription goldens — the WPF Licensing constants and the
//  ReadOnlyGate semantics (Services/Licensing, read 2026-09-27):
//  AutoTrialDays 30, ExpiryWarningDays 14, GracePeriod 7 days,
//  ReadOnly = GracePeriod || TrialExpired, "Your data is preserved".
// ═══════════════════════════════════════════════════════════════════════

public class SubscriptionDomainTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 9, 27);

    // ── constants (WPF verbatim) ────────────────────────────────────

    [Fact]
    public void Constants_MatchTheWpfLicensingModule()
    {
        Assert.Equal(30, SubscriptionGate.TrialDays);          // AutoTrialDays
        Assert.Equal(14, SubscriptionGate.ExpiryWarningDays);  // ExpiryWarningDays
        Assert.Equal(7, SubscriptionGate.GraceDays);           // GracePeriod
    }

    // ── Resolve (the WPF LicenseStatus precedence, SaaS shape) ──────

    [Fact]
    public void Active_WithLongPeriod_IsActive()
    {
        Assert.Equal(SubscriptionAccess.Active,
            SubscriptionGate.Resolve("active", Today.AddDays(60), Now));
    }

    [Theory]
    [InlineData(14)]
    [InlineData(7)]
    [InlineData(1)]
    [InlineData(0)]       // expires today: not yet expired → banner
    public void Active_ExpiringWithin14Days_IsExpiringSoon(int daysLeft)
    {
        Assert.Equal(SubscriptionAccess.ExpiringSoon,
            SubscriptionGate.Resolve("active", Today.AddDays(daysLeft), Now));
    }

    [Fact]
    public void Active_ExpiringIn15Days_IsStillActive()
    {
        Assert.Equal(SubscriptionAccess.Active,
            SubscriptionGate.Resolve("active", Today.AddDays(15), Now));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]       // last grace day
    public void Active_PastExpiry_WithinGrace_IsGracePeriodReadOnly(int daysPast)
    {
        var access = SubscriptionGate.Resolve(
            "active", Today.AddDays(-daysPast), Now);
        Assert.Equal(SubscriptionAccess.GracePeriod, access);
        Assert.True(SubscriptionGate.IsReadOnly(access));   // WPF ReadOnly
        Assert.True(SubscriptionGate.CanRead(access));     // data preserved
    }

    [Fact]
    public void Active_PastGrace_IsExpiredHardBlocked()
    {
        var access = SubscriptionGate.Resolve("active", Today.AddDays(-8), Now);
        Assert.Equal(SubscriptionAccess.Expired, access);
        Assert.True(SubscriptionGate.IsHardBlocked(access));
        Assert.False(SubscriptionGate.CanRead(access));     // app refuses to start
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    public void Trial_PastEnd_IsTrialExpiredReadOnly(int daysPast)
    {
        // WPF: a trial past expiry is TrialExpired (read-only, recovery =
        // a brand-new activation) — there is no paid grace window.
        var access = SubscriptionGate.Resolve(
            "trialing", Today.AddDays(-daysPast), Now);
        Assert.Equal(SubscriptionAccess.TrialExpired, access);
        Assert.True(SubscriptionGate.IsReadOnly(access));
        Assert.True(SubscriptionGate.CanRead(access));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(20)]
    [InlineData(15)]
    public void Trial_Running_IsTrialing(int daysLeft)
    {
        Assert.Equal(SubscriptionAccess.Trialing,
            SubscriptionGate.Resolve("trialing", Today.AddDays(daysLeft), Now));
    }

    [Fact]
    public void Trial_LastDay_IsStillTrialing()
    {
        // Day 0 of a trial is the boundary: not yet expired.
        Assert.Equal(SubscriptionAccess.Trialing,
            SubscriptionGate.Resolve("trialing", Today, Now));
    }

    [Fact]
    public void Suspended_IsHardBlocked_RegardlessOfPeriod()
    {
        var access = SubscriptionGate.Resolve(
            "suspended", Today.AddDays(60), Now);
        Assert.Equal(SubscriptionAccess.Suspended, access);
        Assert.False(SubscriptionGate.CanRead(access));
    }

    [Fact]
    public void Cancelled_IsReadOnly_Recoverable()
    {
        var access = SubscriptionGate.Resolve(
            "cancelled", Today.AddDays(-90), Now);
        Assert.Equal(SubscriptionAccess.Cancelled, access);
        Assert.True(SubscriptionGate.IsReadOnly(access));
        Assert.True(SubscriptionGate.CanRead(access));
    }

    [Fact]
    public void PastDue_InPeriod_BehavesLikeActive()
    {
        Assert.Equal(SubscriptionAccess.Active,
            SubscriptionGate.Resolve("past_due", Today.AddDays(60), Now));
        Assert.Equal(SubscriptionAccess.GracePeriod,
            SubscriptionGate.Resolve("past_due", Today.AddDays(-2), Now));
    }

    // ── the WPF messaging ───────────────────────────────────────────

    [Fact]
    public void ReadOnlyMessage_UsesTheWpfSentences()
    {
        var msg = SubscriptionGate.ReadOnlyMessage(
            SubscriptionAccess.TrialExpired);
        Assert.Contains("read-only mode", msg);
        Assert.Contains("the 30-day trial has ended", msg);
        Assert.Contains("Your data is preserved", msg);
        Assert.Contains("Settings → Subscription", msg);
    }

    [Fact]
    public void StatusText_GracePeriod_IsTheWpfSentence()
    {
        Assert.Equal(
            "the paid plan expired but is still in its 7-day grace period",
            SubscriptionGate.StatusText(SubscriptionAccess.GracePeriod));
    }

    // ── status machine ──────────────────────────────────────────────

    [Fact]
    public void Trialing_CanActivate_Or_Cancel()
    {
        Assert.True(SubscriptionStateMachine.CanTransition("trialing", "active"));
        Assert.True(SubscriptionStateMachine.CanTransition("trialing", "past_due"));
        Assert.True(SubscriptionStateMachine.CanTransition("trialing", "cancelled"));
        Assert.False(SubscriptionStateMachine.CanTransition("trialing", "suspended"));
    }

    [Fact]
    public void Active_CanDegrade_Suspend_Or_Cancel()
    {
        Assert.True(SubscriptionStateMachine.CanTransition("active", "past_due"));
        Assert.True(SubscriptionStateMachine.CanTransition("active", "suspended"));
        Assert.True(SubscriptionStateMachine.CanTransition("active", "cancelled"));
        Assert.True(SubscriptionStateMachine.CanTransition("active", "active"));
        Assert.False(SubscriptionStateMachine.CanTransition("active", "trialing"));
    }

    [Fact]
    public void PastDue_RecoversToActive()
    {
        Assert.True(SubscriptionStateMachine.CanTransition("past_due", "active"));
        Assert.True(SubscriptionStateMachine.CanTransition("past_due", "suspended"));
        Assert.True(SubscriptionStateMachine.CanTransition("past_due", "cancelled"));
    }

    [Fact]
    public void Suspended_Recovers_Or_Ends()
    {
        Assert.True(SubscriptionStateMachine.CanTransition("suspended", "active"));
        Assert.True(SubscriptionStateMachine.CanTransition("suspended", "cancelled"));
        Assert.True(SubscriptionStateMachine.CanTransition("suspended", "suspended"));
        Assert.False(SubscriptionStateMachine.CanTransition("suspended", "trialing"));
    }

    [Fact]
    public void Cancelled_IsTerminal()
    {
        foreach (var next in SubscriptionStatuses.All)
        {
            Assert.Equal(next == "cancelled",
                SubscriptionStateMachine.CanTransition("cancelled", next));
        }
    }

    [Fact]
    public void Reason_MentionsBothStatuses()
    {
        var reason = SubscriptionStateMachine.Reason("active", "trialing");
        Assert.Contains("active", reason);
        Assert.Contains("trialing", reason);
    }

    // ── module feature map (the WPF edition tiers) ──────────────────

    [Theory]
    [InlineData("/api/v1/fiscal/tax-invoices", "tax_invoices")]
    [InlineData("/api/v1/fiscal/vat-report", "tax_invoices")]
    [InlineData("/api/v1/proforma-invoices/42/spec-groups", "proformas")]
    [InlineData("/api/v1/stock/sheets", "sheet_store")]
    [InlineData("/api/v1/optimization/run", "optimization")]
    [InlineData("/api/v1/job-orders", "job_orders")]
    [InlineData("/api/v1/production/release", "production")]
    [InlineData("/api/v1/reservations/reserve", "production")]
    [InlineData("/api/v1/delivery-notes", "delivery")]
    [InlineData("/api/v1/inquiries/7/convert", "inquiries")]
    public void FeatureFor_MapsModuleRoutes(string path, string feature)
    {
        Assert.Equal(feature, ModuleFeatureMap.FeatureFor(path));
    }

    [Theory]
    [InlineData("/api/v1/auth/login")]
    [InlineData("/api/v1/subscriptions/current")]
    [InlineData("/api/v1/masterdata/companies")]
    [InlineData("/health")]
    [InlineData("/api/v1/audit")]
    public void FeatureFor_UngatedRoutes_ReturnNull(string path)
    {
        Assert.Null(ModuleFeatureMap.FeatureFor(path));
    }

    // ── feature resolution (plan ∪ tenant overrides) ────────────────

    [Fact]
    public void Resolution_PlanFeatures_AreEnabled()
    {
        var features = FeatureResolution.Resolve(
            new Dictionary<string, int?>
            {
                ["proformas"] = null,
                ["users"] = 5
            },
            new Dictionary<string, (bool, int?)>());
        Assert.True(FeatureResolution.IsEnabled(features, "proformas"));
        Assert.True(FeatureResolution.IsEnabled(features, "users"));
        Assert.False(FeatureResolution.IsEnabled(features, "optimization"));
    }

    [Fact]
    public void Resolution_TenantOverride_Wins()
    {
        var features = FeatureResolution.Resolve(
            new Dictionary<string, int?> { ["users"] = 5, ["reports"] = null },
            new Dictionary<string, (bool, int?)>
            {
                ["users"] = (true, 10),       // limit lifted
                ["reports"] = (false, null)   // switched off
            });
        Assert.True(features["users"].FromTenantOverride);
        Assert.Equal(10, features["users"].LimitValue);
        Assert.False(FeatureResolution.IsEnabled(features, "reports"));
    }

    [Fact]
    public void Resolution_MissingFeature_IsDisabled()
    {
        // Basic's "inquiry + proforma only" — everything else is absent.
        var features = FeatureResolution.Resolve(
            new Dictionary<string, int?>
            {
                ["users"] = 1, ["inquiries"] = null, ["proformas"] = null
            },
            new Dictionary<string, (bool, int?)>());
        Assert.False(FeatureResolution.IsEnabled(features, "tax_invoices"));
        Assert.False(FeatureResolution.IsEnabled(features, "optimization"));
        Assert.False(FeatureResolution.IsEnabled(features, "sheet_store"));
    }

    [Fact]
    public void WithinLimit_NullLimitIsUnlimited()
    {
        var features = FeatureResolution.Resolve(
            new Dictionary<string, int?> { ["users"] = null },
            new Dictionary<string, (bool, int?)>());
        Assert.True(FeatureResolution.WithinLimit(features, "users", 1_000_000));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]   // 5 seats used on a 5-seat plan = full
    public void WithinLimit_CountsAgainstTheLimit(int used, bool expected)
    {
        var features = FeatureResolution.Resolve(
            new Dictionary<string, int?> { ["users"] = 5 },
            new Dictionary<string, (bool, int?)>());
        Assert.Equal(expected,
            FeatureResolution.WithinLimit(features, "users", used));
    }
}
