// ═══════════════════════════════════════════════════════════════════════
// FILE: Licensing/ProGlass.Licensing.Tests/LicensePlanCatalogTests.cs
// PROJECT: ProGlass.Licensing.Tests
// MODULE: Tests for the global license-plan catalog — the single source
//         of truth shared by the generator tool and the customer app.
// ═══════════════════════════════════════════════════════════════════════

using System;
using Xunit;

namespace ProGlass.Licensing.Tests;

public sealed class LicensePlanCatalogTests
{
    [Fact]
    public void All_exposes_the_five_sellable_plans_in_ui_order()
    {
        Assert.Equal(5, LicensePlanCatalog.All.Count);
        Assert.Equal("7 Days", LicensePlanCatalog.All[0].Name);
        Assert.Equal("30 Days", LicensePlanCatalog.All[1].Name);
        Assert.Equal("6 Months", LicensePlanCatalog.All[2].Name);
        Assert.Equal("1 Year", LicensePlanCatalog.All[3].Name);
        Assert.Equal("2 Years", LicensePlanCatalog.All[4].Name);
    }

    [Theory]
    [InlineData(7, "7 Days")]
    [InlineData(30, "30 Days")]
    [InlineData(182, "6 Months")]
    [InlineData(365, "1 Year")]
    [InlineData(730, "2 Years")]
    public void FromDays_maps_exact_plan_windows(int days, string name)
    {
        Assert.Equal(name, LicensePlanCatalog.FromDays(days).Name);
    }

    [Theory]
    [InlineData(40, "30 Days")]
    [InlineData(200, "6 Months")]
    [InlineData(366, "1 Year")]
    public void FromDays_maps_nearest_plan_for_odd_windows(int days, string name)
    {
        Assert.Equal(name, LicensePlanCatalog.FromDays(days).Name);
    }

    [Fact]
    public void FromValidityWindow_maps_signed_windows_to_plans()
    {
        DateTime issued = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal("1 Year", LicensePlanCatalog
            .FromValidityWindow(issued, issued.AddDays(365))!.Name);
        Assert.Equal("7 Days", LicensePlanCatalog
            .FromValidityWindow(issued, issued.AddDays(7))!.Name);
        Assert.Equal("2 Years", LicensePlanCatalog
            .FromValidityWindow(issued, issued.AddDays(730))!.Name);
    }

    [Fact]
    public void FromValidityWindow_returns_null_for_open_ended_or_non_plan_windows()
    {
        DateTime issued = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Null(LicensePlanCatalog.FromValidityWindow(issued, null));
        Assert.Null(LicensePlanCatalog.FromValidityWindow(issued, issued.AddDays(900)));
        Assert.Null(LicensePlanCatalog.FromValidityWindow(issued, issued.AddDays(-5)));
    }

    [Fact]
    public void DurationLabel_is_human_readable()
    {
        Assert.Equal("7 days", LicensePlanCatalog.DurationLabel(7));
        Assert.Equal("6 months", LicensePlanCatalog.DurationLabel(182));
        Assert.Equal("1 year", LicensePlanCatalog.DurationLabel(365));
        Assert.Equal("2 years", LicensePlanCatalog.DurationLabel(730));
    }
}
