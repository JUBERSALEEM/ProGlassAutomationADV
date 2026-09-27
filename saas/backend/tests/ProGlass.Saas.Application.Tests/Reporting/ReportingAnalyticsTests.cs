// ═══════════════════════════════════════════════════════════════════════
//  Phase 20 — ReportingAnalytics (the WPF ReportingAnalyticsBuilder
//  port) golden tests: Pareto mix (top-10 + Other tail, share fallbacks),
//  delivery ageing buckets, physical (no 0.5 m² floor) area, throughput
//  weeks. Pure domain — no database.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Application.Reporting;

namespace ProGlass.Saas.Application.Tests.Reporting;

public sealed class ReportingAnalyticsTests
{
    private static DeliveryReportRow Row(
        string spec = "6mm Clear", string customer = "Acme", string colour = "",
        string glassType = "SGU", int w1 = 1000, int h1 = 2000, int total = 2,
        int delivered = 0, DateTime? ordered = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), 1, 1, spec, customer, "P", "GR-1",
        colour, glassType, w1, h1, 0, 0, total, delivered, ordered ?? default);

    // ── Pareto: rank, share, Other tail ─────────────────────────────

    [Fact]
    public void Mix_Ranks_By_Sqm_And_Folds_Tail_Into_Other()
    {
        var rows = new List<DeliveryReportRow>
        {
            Row(spec: "Big", w1: 2000, h1: 2000),   // 4 m² × 2 = 8
            Row(spec: "Big", w1: 2000, h1: 2000),
            Row(spec: "Small", w1: 500, h1: 500),   // 0.25 m² × 2 = 0.5 (physical, no floor)
        };

        // BuildJoAnalytics is the public path; assert through it.
        var analytics = ReportingAnalytics.BuildJoAnalytics(
            new List<JoReportRow>(), rows);

        Assert.Equal(2, analytics.TopSpecifications.Count);
        var big = analytics.TopSpecifications.Single(m => m.Label == "Big");
        var small = analytics.TopSpecifications.Single(m => m.Label == "Small");
        Assert.Equal(1, big.Rank);
        Assert.Equal(2, small.Rank);
        // Physical area (no 0.5 m² billable floor): 0.25 m² per panel.
        Assert.Equal(0.5m, small.Sqm);
        Assert.Equal(8m, big.Sqm);
        // Shares by sqm: 8 / 8.5 = 94.1; 0.5 / 8.5 = 5.9.
        Assert.Equal(94.1m, big.SharePct);
        Assert.Equal(5.9m, small.SharePct);
    }

    [Fact]
    public void Mix_Other_Bucket_Sums_Tail_And_Reconciles()
    {
        var rows = Enumerable.Range(0, 12).Select(i => Row(spec: $"Spec-{i}"))
            .ToList<DeliveryReportRow>();

        var analytics = ReportingAnalytics.BuildJoAnalytics(new List<JoReportRow>(), rows);

        // TopN = 10 + one Other row.
        Assert.Equal(11, analytics.TopSpecifications.Count);
        var other = analytics.TopSpecifications.Single(m => m.IsOtherBucket);
        Assert.Equal("Other (2 more)", other.Label);
        // All 12 rows have identical 2 m² × 2 = 4 m², so every share is
        // equal: top rows 8.3% each, Other 2 × 8.3 = 16.7%.
        Assert.Equal(16.7m, other.SharePct);
        // The table still sums to 100% (± rounding).
        var totalShare = analytics.TopSpecifications.Sum(m => m.SharePct);
        Assert.InRange(totalShare, 99.5m, 100.5m);
    }

    [Fact]
    public void Colour_Mix_Uses_OuterColour_With_Unspecified_Fallback()
    {
        var rows = new List<DeliveryReportRow>
        {
            Row(colour: "Bronze"), Row(colour: "Bronze"), Row(colour: "")
        };

        var analytics = ReportingAnalytics.BuildJoAnalytics(new List<JoReportRow>(), rows);

        var bronze = analytics.TopColours.Single(m => m.Label == "Bronze");
        var unspec = analytics.TopColours.Single(m => m.Label == "Unspecified");
        Assert.Equal(2, bronze.Qty);
        Assert.Equal(1, unspec.Qty);
        Assert.Equal("Bronze", analytics.DominantColour);
    }

    // ── Ageing ───────────────────────────────────────────────────────

    [Fact]
    public void Ageing_Buckets_And_Critical_Flag()
    {
        var now = new DateTime(2026, 9, 27);
        var rows = new List<DeliveryReportRow>
        {
            Row(ordered: now.AddDays(-3)),                    // 0–7
            Row(ordered: now.AddDays(-10)),                   // 8–14
            Row(ordered: now.AddDays(-20)),                   // 15–30
            Row(ordered: now.AddDays(-45), total: 3),         // over 30 — critical
        };

        var analytics = ReportingAnalytics.BuildDeliveryAnalytics(rows, now);

        Assert.Equal(4, analytics.PendingQty);
        Assert.Equal(4, analytics.TotalRows);
        Assert.Equal(4, analytics.NotStartedRows);
        Assert.Equal(0, analytics.CompletedRows);

        var over30 = analytics.PendingAgeing.Single(b => b.IsCritical);
        Assert.Equal("Over 30 days", over30.Label);
        Assert.Equal(1, over30.Count);
        Assert.Equal(3, over30.Qty);
        // Critical pending qty = the over-30 bucket's qty only.
        Assert.Equal(3, analytics.CriticalPendingQty);
        // Shares by qty: 1/4 = 25% for the first three, 3/4 = 75%.
        Assert.Equal(25m, analytics.PendingAgeing[0].SharePct);
        Assert.Equal(75m, over30.SharePct);
    }

    [Fact]
    public void Completed_Rows_Age_Out_Of_The_Pending_Buckets()
    {
        var now = new DateTime(2026, 9, 27);
        var rows = new List<DeliveryReportRow>
        {
            Row(ordered: now.AddDays(-45), delivered: 2),     // fully delivered
        };

        var analytics = ReportingAnalytics.BuildDeliveryAnalytics(rows, now);

        Assert.Equal(1, analytics.CompletedRows);
        Assert.Equal(0, analytics.PendingQty);
        Assert.Equal(0, analytics.CriticalPendingQty);
        Assert.All(analytics.PendingAgeing, b => Assert.Equal(0, b.Qty));
    }

    [Fact]
    public void Undated_Rows_Age_As_Zero_Not_Critical()
    {
        var analytics = ReportingAnalytics.BuildDeliveryAnalytics(
            new List<DeliveryReportRow> { Row(ordered: default) },
            new DateTime(2026, 9, 27));

        Assert.Equal(0, analytics.CriticalPendingQty);
        Assert.Equal(2, analytics.PendingAgeing[0].Qty); // 0–7 bucket
    }

    // ── Physical area (SqmOf) ────────────────────────────────────────

    [Fact]
    public void Sqm_Is_Physical_No_Minimum_Floor_And_W2_Falls_Back()
    {
        // 300×300 = 0.09 m² — under the 0.5 billable floor, but the
        // analytics area is physical.
        var rows = new List<DeliveryReportRow> { Row(w1: 300, h1: 300, total: 1) };
        var analytics = ReportingAnalytics.BuildDeliveryAnalytics(rows);
        Assert.Equal(0.09m, analytics.TopSpecifications.Sum(m => m.Sqm));

        // W2/H2 bounding pair used when W1/H1 are absent (shaped glass).
        var shaped = new List<DeliveryReportRow>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), 1, 1, "Shaped", "Acme", "P", "GR",
                "", "SGU", 0, 0, 1200, 800, 2, 0, default)
        };
        var shapedAnalytics = ReportingAnalytics.BuildDeliveryAnalytics(shaped);
        Assert.Equal(1.92m, shapedAnalytics.TopSpecifications.Sum(m => m.Sqm));
    }

    // ── Throughput ───────────────────────────────────────────────────

    [Fact]
    public void Throughput_Groups_By_Iso_Week_Starting_Monday()
    {
        // 2026-09-27 is a Sunday; 2026-09-21 is its Monday.
        var rows = new List<JoReportRow>
        {
            new(Guid.NewGuid(), "JOB-1", Guid.NewGuid(), "Acme",
                new DateTime(2026, 9, 27), 2, 4m, 100m),
            new(Guid.NewGuid(), "JOB-2", Guid.NewGuid(), "Acme",
                new DateTime(2026, 9, 25), 1, 2m, 50m),
            new(Guid.NewGuid(), "JOB-3", Guid.NewGuid(), "Beta",
                new DateTime(2026, 9, 15), 1, 2m, 50m),   // previous week
        };

        var analytics = ReportingAnalytics.BuildJoAnalytics(rows);

        Assert.Equal(2, analytics.Throughput.Count);
        Assert.Equal(new DateTime(2026, 9, 21), analytics.Throughput[0].WeekStart);
        Assert.Equal(2, analytics.Throughput[0].Count);
        Assert.Equal(6m, analytics.Throughput[0].Sqm);
        Assert.Equal(new DateTime(2026, 9, 14), analytics.Throughput[1].WeekStart);
    }

    // ── Job order mix ────────────────────────────────────────────────

    [Fact]
    public void Jo_Mix_Totals_And_Top_Customers_By_Value()
    {
        var rows = new List<JoReportRow>
        {
            new(Guid.NewGuid(), "JOB-1", Guid.NewGuid(), "Acme",
                new DateTime(2026, 9, 27), 10, 20m, 1_000m),
            new(Guid.NewGuid(), "JOB-2", Guid.NewGuid(), "Beta",
                new DateTime(2026, 9, 27), 5, 10m, 500m),
        };

        var analytics = ReportingAnalytics.BuildJoAnalytics(rows);

        Assert.Equal(2, analytics.TotalJobOrders);
        Assert.Equal(15, analytics.TotalQty);
        Assert.Equal(30m, analytics.TotalSqm);
        Assert.Equal(1_500m, analytics.TotalValue);

        var acme = analytics.TopCustomers.Single(m => m.Label == "Acme");
        Assert.Equal(1, acme.Rank);
        Assert.Equal(66.7m, acme.SharePct);   // 1000/1500
    }
}
