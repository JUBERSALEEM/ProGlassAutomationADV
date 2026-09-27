// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 20:
//   Services/Reporting/ReportingAnalyticsBuilder.cs (slice 1, 2026-09-02)
// Pure aggregation over an ALREADY-FILTERED row list — no queries here,
// so the aggregates can never drift from the detail table they summarise
// (the WPF cardinal rule). Static, synchronous, side-effect free.
//
// Documented difference: ColourOf in the WPF parses the specification
// STRING at report time (SpecPaneResolver). The SaaS schema carries the
// normalized spec_groups.outer_glass_color (saved with the Step 5 grid /
// imported from the WPF spec name), so the row brings the colour and this
// builder falls back to "Unspecified" exactly like the WPF's parser miss.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Pricing;

namespace ProGlass.Saas.Application.Reporting;

public static class ReportingAnalytics
{
    /// <summary>Pareto depth: top 10 + an "Other" tail (industry norm).</summary>
    public const int TopN = 10;

    /// <summary>Pending older than this is escalated.</summary>
    public const int CriticalAgeDays = 30;

    // ── JOB ORDER ─────────────────────────────────────────────────────

    public static JoAnalyticsDto BuildJoAnalytics(
        IReadOnlyList<JoReportRow>? jobOrders,
        IReadOnlyList<DeliveryReportRow>? itemRows = null)
    {
        var result = new JoAnalyticsDto();
        jobOrders ??= Array.Empty<JoReportRow>();

        result.TotalJobOrders = jobOrders.Count;
        result.TotalQty = jobOrders.Sum(j => j.TotalQty);
        result.TotalSqm = jobOrders.Sum(j => j.TotalSqm);
        result.TotalValue = jobOrders.Sum(j => j.GrandTotal);

        result.TopCustomers = BuildMix(
            jobOrders,
            j => Clean(j.CustomerName),
            j => 1,
            j => j.TotalQty,
            j => j.TotalSqm,
            j => j.GrandTotal,
            rankBy: MixRank.Value);

        result.Throughput = BuildThroughput(jobOrders);

        if (itemRows is { Count: > 0 })
        {
            result.TopSpecifications = BuildMix(
                itemRows,
                r => Clean(r.Specification),
                r => 1,
                r => r.TotalQty,
                r => SqmOf(r),
                r => 0m,
                rankBy: MixRank.Sqm);

            result.TopColours = BuildMix(
                itemRows,
                r => ColourOf(r),
                r => 1,
                r => r.TotalQty,
                r => SqmOf(r),
                r => 0m,
                rankBy: MixRank.Sqm);

            result.GlassTypeMix = BuildMix(
                itemRows,
                r => Clean(r.GlassType),
                r => 1,
                r => r.TotalQty,
                r => SqmOf(r),
                r => 0m,
                rankBy: MixRank.Sqm);
        }

        var topSpec = result.TopSpecifications.FirstOrDefault(m => !m.IsOtherBucket);
        if (topSpec != null)
        {
            result.DominantSpecification = topSpec.Label;
            result.TopSpecSharePct = topSpec.SharePct;
        }

        var topColour = result.TopColours.FirstOrDefault(m => !m.IsOtherBucket);
        if (topColour != null) result.DominantColour = topColour.Label;

        return result;
    }

    // ── DELIVERY ──────────────────────────────────────────────────────

    /// <summary>
    /// Builds delivery ageing and outstanding-work analytics.
    /// <paramref name="asOf"/> is injected so the ageing buckets are
    /// deterministic and testable, and so a re-run for a past period ages
    /// against that period rather than today.
    /// </summary>
    public static DeliveryAnalyticsDto BuildDeliveryAnalytics(
        IReadOnlyList<DeliveryReportRow>? rows,
        DateTime? asOf = null)
    {
        var result = new DeliveryAnalyticsDto();
        rows ??= Array.Empty<DeliveryReportRow>();
        DateTime now = asOf ?? DateTime.Now;

        result.TotalRows = rows.Count;
        result.OrderedQty = rows.Sum(r => r.TotalQty);
        result.DeliveredQty = rows.Sum(r => r.DeliveredQty);
        result.PendingQty = rows.Sum(r => Math.Max(0, r.BalanceQty));

        result.NotStartedRows = rows.Count(r => r.DeliveredQty <= 0 && r.TotalQty > 0);
        result.CompletedRows = rows.Count(r => r.TotalQty > 0 && r.BalanceQty <= 0);
        result.PartialRows = rows.Count(r => r.DeliveredQty > 0 && r.BalanceQty > 0);

        var pending = rows.Where(r => r.BalanceQty > 0).ToList();

        result.PendingAgeing = BuildAgeing(pending, now);
        result.CriticalPendingQty = result.PendingAgeing
            .Where(b => b.IsCritical)
            .Sum(b => b.Qty);

        result.PendingByCustomer = BuildMix(
            pending,
            r => Clean(r.CustomerName),
            r => 1,
            r => r.BalanceQty,
            r => SqmOf(r, r.BalanceQty),
            r => 0m,
            rankBy: MixRank.Qty);

        result.PendingBySpecification = BuildMix(
            pending,
            r => Clean(r.Specification),
            r => 1,
            r => r.BalanceQty,
            r => SqmOf(r, r.BalanceQty),
            r => 0m,
            rankBy: MixRank.Qty);

        var delivered = rows.Where(r => r.DeliveredQty > 0).ToList();
        result.DeliveredBySpecification = BuildMix(
            delivered,
            r => Clean(r.Specification),
            r => 1,
            r => r.DeliveredQty,
            r => SqmOf(r, r.DeliveredQty),
            r => 0m,
            rankBy: MixRank.Qty);

        return result;
    }

    // ── SHARED ────────────────────────────────────────────────────────

    private enum MixRank { Sqm, Qty, Value, Count }

    /// <summary>
    /// Generic Pareto builder: group, rank, take the top N, and fold the
    /// tail into a single "Other" row so the table still sums to 100%.
    /// </summary>
    private static List<MixRowDto> BuildMix<T>(
        IReadOnlyList<T> source,
        Func<T, string> keySelector,
        Func<T, int> countSelector,
        Func<T, int> qtySelector,
        Func<T, decimal> sqmSelector,
        Func<T, decimal> valueSelector,
        MixRank rankBy)
    {
        var rows = new List<MixRowDto>();
        if (source == null || source.Count == 0) return rows;

        var groups = source
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Select(g => new MixRowDto
            {
                Label = g.Key,
                Count = g.Sum(countSelector),
                Qty = g.Sum(qtySelector),
                Sqm = Math.Round(g.Sum(sqmSelector), 2),
                Value = g.Sum(valueSelector)
            })
            .ToList();

        if (groups.Count == 0) return rows;

        // Denominator matches the ranking measure, so "share" always means
        // share of the thing the table is sorted by.
        decimal total = rankBy switch
        {
            MixRank.Sqm => groups.Sum(g => g.Sqm),
            MixRank.Qty => groups.Sum(g => g.Qty),
            MixRank.Value => groups.Sum(g => g.Value),
            _ => groups.Sum(g => g.Count)
        };

        // Glass area can legitimately be zero (missing dimensions) while
        // piece counts are not. Fall back so a table is never blank.
        if (total <= 0m && rankBy == MixRank.Sqm)
        {
            rankBy = MixRank.Qty;
            total = groups.Sum(g => g.Qty);
        }
        if (total <= 0m)
        {
            rankBy = MixRank.Count;
            total = groups.Sum(g => g.Count);
        }
        if (total <= 0m) return rows;

        decimal Measure(MixRowDto m) => rankBy switch
        {
            MixRank.Sqm => m.Sqm,
            MixRank.Qty => m.Qty,
            MixRank.Value => m.Value,
            _ => m.Count
        };

        var ordered = groups.OrderByDescending(Measure).ToList();
        var top = ordered.Take(TopN).ToList();
        var tail = ordered.Skip(TopN).ToList();

        int rank = 1;
        foreach (var g in top)
        {
            g.Rank = rank++;
            g.SharePct = Math.Round(Measure(g) / total * 100m, 1);
            rows.Add(g);
        }

        if (tail.Count > 0)
        {
            var other = new MixRowDto
            {
                Rank = rank,
                Label = $"Other ({tail.Count} more)",
                Count = tail.Sum(g => g.Count),
                Qty = tail.Sum(g => g.Qty),
                Sqm = Math.Round(tail.Sum(g => g.Sqm), 2),
                Value = tail.Sum(g => g.Value),
                IsOtherBucket = true
            };
            other.SharePct = Math.Round(Measure(other) / total * 100m, 1);
            rows.Add(other);
        }

        return rows;
    }

    private static List<AgeingBucketDto> BuildAgeing(
        IReadOnlyList<DeliveryReportRow> pending, DateTime now)
    {
        var buckets = new List<AgeingBucketDto>
        {
            new() { Label = "0–7 days",   MinDays = 0,  MaxDays = 7 },
            new() { Label = "8–14 days",  MinDays = 8,  MaxDays = 14 },
            new() { Label = "15–30 days", MinDays = 15, MaxDays = 30 },
            new() { Label = "Over 30 days", MinDays = 31, MaxDays = int.MaxValue,
                    IsCritical = true },
        };

        foreach (var row in pending)
        {
            int age = AgeInDays(row, now);
            var bucket = buckets.First(b => age >= b.MinDays && age <= b.MaxDays);

            bucket.Count++;
            bucket.Qty += row.BalanceQty;
            bucket.Sqm += SqmOf(row, row.BalanceQty);
        }

        int totalQty = buckets.Sum(b => b.Qty);
        foreach (var b in buckets)
        {
            b.Sqm = Math.Round(b.Sqm, 2);
            b.SharePct = totalQty <= 0
                ? 0m
                : Math.Round(b.Qty / (decimal)totalQty * 100m, 1);
        }

        return buckets;
    }

    /// <summary>
    /// Age of an outstanding row, in days, measured from when the work was
    /// ORDERED (the master-DN creation date) rather than when part of it
    /// shipped — ageing must express how long the customer has been
    /// waiting. Rows with no usable date age as 0 (conservative: escalating
    /// work that may not be late is worse than under-reporting it).
    /// </summary>
    private static int AgeInDays(DeliveryReportRow row, DateTime now)
    {
        if (row.OrderedAt == default) return 0;

        int days = (int)(now.Date - row.OrderedAt.Date).TotalDays;
        return days < 0 ? 0 : days;
    }

    private static List<ThroughputPointDto> BuildThroughput(
        IReadOnlyList<JoReportRow> jobOrders)
    {
        var points = new List<ThroughputPointDto>();
        if (jobOrders.Count == 0) return points;

        return jobOrders
            .GroupBy(j => StartOfWeek(j.CreatedAt))
            .OrderBy(g => g.Key)
            .Select(g => new ThroughputPointDto
            {
                WeekStart = g.Key,
                Count = g.Count(),
                Sqm = Math.Round(g.Sum(j => j.TotalSqm), 2)
            })
            .ToList();
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    /// <summary>
    /// Area of one row's glass. Uses the FULL row quantity by default;
    /// pass an override to cost only the delivered or pending part.
    /// W2/H2 covers shaped glass (second pair = bounding size).
    /// Physical area — no 0.5 m² billable floor (WPF SqmOf).
    /// </summary>
    private static decimal SqmOf(DeliveryReportRow row, int? qtyOverride = null)
    {
        int w = row.W1 > 0 ? row.W1 : row.W2;
        int h = row.H1 > 0 ? row.H1 : row.H2;
        if (w <= 0 || h <= 0) return 0m;

        int qty = qtyOverride ?? row.TotalQty;
        if (qty <= 0) return 0m;

        return SqmCalculator.CalculatePhysicalPanelArea(w, h) * qty;
    }

    /// <summary>
    /// Outer-pane colour for the colour Pareto — from the normalized
    /// spec_groups.outer_glass_color the row carries (see file header).
    /// </summary>
    private static string ColourOf(DeliveryReportRow row)
    {
        var colour = row.OuterColour?.Trim();
        return string.IsNullOrWhiteSpace(colour) ? "Unspecified" : colour;
    }

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
}
