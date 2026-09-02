// ═══════════════════════════════════════════════════════════════════════
//  FILE: Services/Reporting/ReportingAnalyticsBuilder.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02 — advanced reporting, slice 1
//
//  Pure aggregation over an ALREADY-FILTERED row list.
//
//  WHY PURE / WHY NO QUERIES HERE
//  ---------------------------------------------------------------------
//  The cardinal rule of a trustworthy report is that the detail table must
//  reconcile exactly to the KPI band above it. The reliable way to
//  guarantee that is to compute both from the SAME materialised rows. If
//  these aggregates ran their own queries they would eventually drift from
//  the detail -- a different WHERE clause, a different date boundary, a
//  missing status -- and a report whose totals disagree with its own rows
//  is worse than no report.
//
//  Everything here is therefore static, synchronous, and side-effect free,
//  which also makes it unit-testable without a database.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Reporting
{
    public static class ReportingAnalyticsBuilder
    {
        /// <summary>
        /// Pareto depth. Top 10 plus an "Other" tail is the industry norm:
        /// it keeps a printed page readable while still reconciling to
        /// 100%, which a bare "top 10" would not.
        /// </summary>
        public const int TopN = 10;

        /// <summary>Pending older than this is escalated.</summary>
        public const int CriticalAgeDays = 30;

        // ═══════════════════════════════════════════════════════════════
        //  JOB ORDER
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds Job Order mix analytics.
        ///
        /// <paramref name="jobOrders"/> supplies volume, value and
        /// throughput. <paramref name="itemRows"/> is optional and supplies
        /// spec/colour mix: a JoReportRow has no glass detail, so the
        /// per-item DeliveryReportRow set (which carries Specification and
        /// GlassType for the same job orders) is the correct source. When
        /// it is not supplied the spec/colour tables are simply empty
        /// rather than guessed at.
        /// </summary>
        public static JoAnalyticsSummary BuildJoAnalytics(
            IReadOnlyList<JoReportRow>? jobOrders,
            IReadOnlyList<DeliveryReportRow>? itemRows = null)
        {
            var result = new JoAnalyticsSummary();
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

        // ═══════════════════════════════════════════════════════════════
        //  DELIVERY
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds delivery ageing and outstanding-work analytics.
        ///
        /// <paramref name="asOf"/> is injected rather than read from
        /// DateTime.Now so the ageing buckets are deterministic and
        /// testable, and so a report re-run for a past period ages against
        /// that period rather than today.
        /// </summary>
        public static DeliveryAnalyticsSummary BuildDeliveryAnalytics(
            IReadOnlyList<DeliveryReportRow>? rows,
            DateTime? asOf = null)
        {
            var result = new DeliveryAnalyticsSummary();
            rows ??= Array.Empty<DeliveryReportRow>();
            DateTime now = asOf ?? DateTime.Now;

            result.TotalRows = rows.Count;
            result.OrderedQty = rows.Sum(r => r.TotalQty);
            result.DeliveredQty = rows.Sum(r => r.DeliveredQty);
            result.PendingQty = rows.Sum(r => Math.Max(0, r.BalanceQty));

            result.NotStartedRows = rows.Count(r => r.DeliveredQty <= 0 && r.TotalQty > 0);
            result.CompletedRows = rows.Count(r => r.TotalQty > 0 && r.BalanceQty <= 0);
            result.PartialRows = rows.Count(r =>
                r.DeliveredQty > 0 && r.BalanceQty > 0);

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

        // ═══════════════════════════════════════════════════════════════
        //  SHARED
        // ═══════════════════════════════════════════════════════════════

        private enum MixRank { Sqm, Qty, Value, Count }

        /// <summary>
        /// Generic Pareto builder: group, rank, take the top N, and fold
        /// the tail into a single "Other" row so the table still sums to
        /// 100%.
        /// </summary>
        private static List<MixRow> BuildMix<T>(
            IReadOnlyList<T> source,
            Func<T, string> keySelector,
            Func<T, int> countSelector,
            Func<T, int> qtySelector,
            Func<T, decimal> sqmSelector,
            Func<T, decimal> valueSelector,
            MixRank rankBy)
        {
            var rows = new List<MixRow>();
            if (source == null || source.Count == 0) return rows;

            var groups = source
                .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                .Select(g => new MixRow
                {
                    Label = g.Key,
                    Count = g.Sum(countSelector),
                    Qty = g.Sum(qtySelector),
                    Sqm = Math.Round(g.Sum(sqmSelector), 2),
                    Value = g.Sum(valueSelector)
                })
                .ToList();

            if (groups.Count == 0) return rows;

            // Denominator matches the ranking measure, so "share" always
            // means share of the thing the table is sorted by.
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

            decimal Measure(MixRow m) => rankBy switch
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
                var other = new MixRow
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

        private static List<AgeingBucket> BuildAgeing(
            IReadOnlyList<DeliveryReportRow> pending, DateTime now)
        {
            var buckets = new List<AgeingBucket>
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
        /// Age of an outstanding row, in days, measured from when the work
        /// was ORDERED (DeliveryReportRow.OrderedAt, the master-DN creation
        /// date) rather than when part of it shipped. Ageing must express
        /// how long the customer has been waiting; a dispatch date would
        /// reset the clock on every partial delivery and make chronically
        /// late rows look fresh.
        ///
        /// Rows with no usable date age as 0 and land in the youngest
        /// bucket. That is deliberately conservative: escalating work that
        /// may not be late is worse than under-reporting it, because a
        /// critical bucket nobody trusts gets ignored entirely.
        /// </summary>
        private static int AgeInDays(DeliveryReportRow row, DateTime now)
        {
            if (row.OrderedAt == default) return 0;

            int days = (int)(now.Date - row.OrderedAt.Date).TotalDays;
            return days < 0 ? 0 : days;
        }

        private static List<ThroughputPoint> BuildThroughput(
            IReadOnlyList<JoReportRow> jobOrders)
        {
            var points = new List<ThroughputPoint>();
            if (jobOrders.Count == 0) return points;

            return jobOrders
                .GroupBy(j => StartOfWeek(j.CreatedAt))
                .OrderBy(g => g.Key)
                .Select(g => new ThroughputPoint
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
        /// W2/H2 covers shaped glass, where the second pair is the
        /// bounding size.
        /// </summary>
        private static decimal SqmOf(DeliveryReportRow row, int? qtyOverride = null)
        {
            int w = row.W1 > 0 ? row.W1 : row.W2;
            int h = row.H1 > 0 ? row.H1 : row.H2;
            if (w <= 0 || h <= 0) return 0m;

            int qty = qtyOverride ?? row.TotalQty;
            if (qty <= 0) return 0m;

            return (w * (decimal)h) / 1_000_000m * qty;
        }

        /// <summary>
        /// Outer-pane colour for the colour Pareto.
        ///
        /// The raw specification string is too granular to group usefully:
        /// "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass" and its
        /// 10mm variant are different strings but the same COLOUR, and a
        /// colour table that splits them answers nothing.
        ///
        /// Uses SpecPaneResolver -- the same parser the Specifications
        /// Center uses -- so "which colour" means the same thing in both
        /// places. Deliberately NOT SpecificationIntelligenceService
        /// .DecomposeSpecification: that defaults OuterColor to "Clear"
        /// when it cannot determine one, which would silently report every
        /// unparsed row as Clear and corrupt the Pareto.
        /// </summary>
        private static string ColourOf(DeliveryReportRow row)
        {
            try
            {
                var panes = Specifications.SpecPaneResolver.Parse(row.Specification);
                var outer = panes.FirstOrDefault();
                if (outer != null && !string.IsNullOrWhiteSpace(outer.Color))
                    return outer.Color.Trim();
            }
            catch
            {
                // Best-effort: an unparsed row is grouped as Unspecified
                // rather than being attributed to a colour it may not have.
            }

            return "Unspecified";
        }

        private static string Clean(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
    }
}
