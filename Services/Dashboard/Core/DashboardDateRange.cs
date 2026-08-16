// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Dashboard/Core/DashboardDateRange.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Services.Dashboard.Core
//
// PURPOSE:
//   One definition of "the selected period" for the whole dashboard.
//
// WHY THIS EXISTS
//   Before this, every widget invented its own window: monthsBack = 6
//   here, limit = 5 there, "this month" computed inline in a ViewModel.
//   Two widgets on the same screen could therefore describe different
//   periods with no visible indication, and there was nothing for a
//   global range selector to bind to.
//
// PREVIOUS-PERIOD RULE
//   Comparisons use the immediately preceding window of the SAME LENGTH,
//   not "the same month last year". For a 30-day range the comparison is
//   the 30 days before it. This keeps "vs previous" meaningful for every
//   preset including Custom.
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Services.Dashboard.Core
{
    public enum DashboardRangeKind
    {
        Today,
        ThisWeek,
        ThisMonth,
        LastMonth,
        Quarter,
        SixMonths,
        ThisYear,
        AllTime,
        Custom
    }

    /// <summary>
    /// An inclusive-start, exclusive-end date window.
    ///
    /// End is exclusive so that a day, month or year boundary needs no
    /// "23:59:59" fudge: a record belongs to the window when
    /// Start &lt;= value &lt; End. Using an inclusive end with DateTime
    /// silently drops rows saved later in the final second.
    /// </summary>
    public sealed class DashboardDateRange
    {
        public DashboardRangeKind Kind { get; }

        /// <summary>Inclusive lower bound. DateTime.MinValue for AllTime.</summary>
        public DateTime Start { get; }

        /// <summary>Exclusive upper bound.</summary>
        public DateTime End { get; }

        public string Label { get; }

        private DashboardDateRange(DashboardRangeKind kind, DateTime start,
                                   DateTime end, string label)
        {
            Kind = kind;
            Start = start;
            End = end;
            Label = label;
        }

        /// <summary>True when no filtering should be applied.</summary>
        public bool IsAllTime => Kind == DashboardRangeKind.AllTime;

        /// <summary>Does this window contain the given timestamp?</summary>
        public bool Contains(DateTime value) =>
            IsAllTime || (value >= Start && value < End);

        /// <summary>
        /// The window of identical length immediately before this one,
        /// used for "vs previous period".
        ///
        /// AllTime has no meaningful predecessor, so it returns itself
        /// and callers detect that via HasPrevious.
        /// </summary>
        public DashboardDateRange Previous()
        {
            if (IsAllTime) return this;

            var span = End - Start;
            return new DashboardDateRange(
                Kind, Start - span, Start, "Previous " + Label);
        }

        public bool HasPrevious => !IsAllTime;

        // ═════════════════════════════════════════════════════════════
        //  FACTORIES
        //
        //  All take "now" explicitly rather than reading DateTime.Now
        //  internally, so the boundaries are testable and a single load
        //  cannot straddle midnight between two widgets.
        // ═════════════════════════════════════════════════════════════

        public static DashboardDateRange Today(DateTime now)
        {
            var d = now.Date;
            return new DashboardDateRange(
                DashboardRangeKind.Today, d, d.AddDays(1), "Today");
        }

        public static DashboardDateRange ThisWeek(DateTime now)
        {
            // Monday-start week. DayOfWeek puts Sunday at 0, so shift it
            // to 7 before subtracting or Sunday jumps forward a week.
            int dow = (int)now.DayOfWeek;
            if (dow == 0) dow = 7;
            var start = now.Date.AddDays(-(dow - 1));
            return new DashboardDateRange(
                DashboardRangeKind.ThisWeek, start, start.AddDays(7), "This week");
        }

        public static DashboardDateRange ThisMonth(DateTime now)
        {
            var start = new DateTime(now.Year, now.Month, 1);
            return new DashboardDateRange(
                DashboardRangeKind.ThisMonth, start, start.AddMonths(1), "This month");
        }

        public static DashboardDateRange LastMonth(DateTime now)
        {
            var start = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
            return new DashboardDateRange(
                DashboardRangeKind.LastMonth, start, start.AddMonths(1), "Last month");
        }

        public static DashboardDateRange Quarter(DateTime now)
        {
            var start = new DateTime(now.Year, now.Month, 1).AddMonths(-2);
            return new DashboardDateRange(
                DashboardRangeKind.Quarter, start,
                new DateTime(now.Year, now.Month, 1).AddMonths(1), "Last 3 months");
        }

        public static DashboardDateRange SixMonths(DateTime now)
        {
            var start = new DateTime(now.Year, now.Month, 1).AddMonths(-5);
            return new DashboardDateRange(
                DashboardRangeKind.SixMonths, start,
                new DateTime(now.Year, now.Month, 1).AddMonths(1), "Last 6 months");
        }

        public static DashboardDateRange ThisYear(DateTime now)
        {
            var start = new DateTime(now.Year, 1, 1);
            return new DashboardDateRange(
                DashboardRangeKind.ThisYear, start, start.AddYears(1), "This year");
        }

        public static DashboardDateRange AllTime() =>
            new(DashboardRangeKind.AllTime, DateTime.MinValue,
                DateTime.MaxValue, "All time");

        /// <summary>
        /// Custom window. The supplied end date is treated as inclusive
        /// for the user (they picked that day) and stored exclusively by
        /// adding one day, which is what every comparison here expects.
        /// Reversed inputs are swapped rather than producing an empty
        /// range the user cannot explain.
        /// </summary>
        public static DashboardDateRange Custom(DateTime from, DateTime to)
        {
            var a = from.Date;
            var b = to.Date;
            if (b < a) (a, b) = (b, a);
            return new DashboardDateRange(
                DashboardRangeKind.Custom, a, b.AddDays(1),
                $"{a:dd MMM yyyy} – {b:dd MMM yyyy}");
        }

        public static DashboardDateRange FromKind(DashboardRangeKind kind, DateTime now) =>
            kind switch
            {
                DashboardRangeKind.Today      => Today(now),
                DashboardRangeKind.ThisWeek   => ThisWeek(now),
                DashboardRangeKind.ThisMonth  => ThisMonth(now),
                DashboardRangeKind.LastMonth  => LastMonth(now),
                DashboardRangeKind.Quarter    => Quarter(now),
                DashboardRangeKind.SixMonths  => SixMonths(now),
                DashboardRangeKind.ThisYear   => ThisYear(now),
                _                             => AllTime()
            };

        public override string ToString() => Label;
    }
}
