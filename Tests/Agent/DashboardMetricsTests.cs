// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/DashboardMetricsTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// WHAT THESE COVER
//   The pure logic in the new dashboard service layer: date-range
//   boundaries, previous-period derivation, and the delta maths that
//   drives every "vs previous" indicator.
//
//   Deliberately NOT covered here: the EF queries themselves. Those need
//   a database, and asserting against a live SQLite file would test the
//   data rather than the code. The queries are kept trivial (project,
//   filter, group) precisely so the logic worth testing lives in the
//   range and delta types, which are pure and fully testable.
//
// WHY THE BOUNDARY TESTS MATTER
//   Every one of these is a bug I would otherwise have shipped blind:
//   an inclusive end date silently drops rows saved in the final second
//   of a period, and .NET's DayOfWeek puts Sunday at 0, which throws a
//   Monday-start week a full week backwards unless it is corrected.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Dashboard.Core;
using System;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class DashboardMetricsTests
    {
        // ── Week boundaries ──────────────────────────────────────────────

        [Fact]
        public void ThisWeek_OnSunday_StaysInTheWeekThatJustEnded()
        {
            // Sunday 16 Aug 2026. DayOfWeek == 0 here; without the shift
            // to 7 the start would jump forward instead of back to Monday.
            var sunday = new DateTime(2026, 8, 16, 22, 42, 0);
            var range = DashboardDateRange.ThisWeek(sunday);

            Assert.Equal(new DateTime(2026, 8, 10), range.Start);
            Assert.Equal(new DateTime(2026, 8, 17), range.End);
            Assert.True(range.Contains(sunday));
        }

        [Fact]
        public void ThisWeek_OnMonday_StartsThatSameDay()
        {
            var monday = new DateTime(2026, 8, 17, 0, 1, 0);
            var range = DashboardDateRange.ThisWeek(monday);

            Assert.Equal(new DateTime(2026, 8, 17), range.Start);
            Assert.True(range.Contains(monday));
        }

        [Fact]
        public void ThisWeek_SpansTheYearBoundary()
        {
            // 1 Jan 2026 is a Thursday, so its week starts in December.
            var range = DashboardDateRange.ThisWeek(new DateTime(2026, 1, 1));
            Assert.Equal(new DateTime(2025, 12, 29), range.Start);
        }

        // ── Exclusive end ────────────────────────────────────────────────

        [Fact]
        public void Range_EndIsExclusive_SoLastInstantOfPeriodStillCounts()
        {
            var range = DashboardDateRange.ThisMonth(new DateTime(2026, 8, 16));

            // The very end of the last day must be inside the month.
            var lastInstant = new DateTime(2026, 8, 31, 23, 59, 59);
            Assert.True(range.Contains(lastInstant));

            // Midnight on the 1st of next month must be outside it.
            Assert.False(range.Contains(new DateTime(2026, 9, 1, 0, 0, 0)));
        }

        [Fact]
        public void ThisMonth_RollsOverDecemberCorrectly()
        {
            var range = DashboardDateRange.ThisMonth(new DateTime(2026, 12, 31));
            Assert.Equal(new DateTime(2026, 12, 1), range.Start);
            Assert.Equal(new DateTime(2027, 1, 1), range.End);
        }

        // ── Custom ranges ────────────────────────────────────────────────

        [Fact]
        public void Custom_TreatsTheChosenEndDayAsIncluded()
        {
            // A user picking 1st–5th expects the 5th to be in the results.
            var range = DashboardDateRange.Custom(
                new DateTime(2026, 8, 1), new DateTime(2026, 8, 5));

            Assert.True(range.Contains(new DateTime(2026, 8, 5, 18, 0, 0)));
            Assert.Equal(new DateTime(2026, 8, 6), range.End);
        }

        [Fact]
        public void Custom_SwapsReversedDates_RatherThanReturningNothing()
        {
            var range = DashboardDateRange.Custom(
                new DateTime(2026, 8, 20), new DateTime(2026, 8, 10));

            Assert.Equal(new DateTime(2026, 8, 10), range.Start);
            Assert.True(range.Contains(new DateTime(2026, 8, 15)));
        }

        // ── Previous period ──────────────────────────────────────────────

        [Fact]
        public void Previous_IsTheSameLengthImmediatelyBefore()
        {
            var range = DashboardDateRange.ThisMonth(new DateTime(2026, 8, 16));
            var prev = range.Previous();

            Assert.Equal(new DateTime(2026, 7, 1), prev.Start);
            Assert.Equal(new DateTime(2026, 8, 1), prev.End);
            Assert.Equal(range.End - range.Start, prev.End - prev.Start);
        }

        [Fact]
        public void Previous_AndCurrent_DoNotOverlap()
        {
            var range = DashboardDateRange.ThisWeek(new DateTime(2026, 8, 16));
            var prev = range.Previous();

            Assert.True(prev.End <= range.Start);
            Assert.False(prev.Contains(range.Start));
        }

        [Fact]
        public void AllTime_HasNoPreviousPeriod()
        {
            var range = DashboardDateRange.AllTime();
            Assert.False(range.HasPrevious);
            Assert.True(range.IsAllTime);
            Assert.True(range.Contains(DateTime.MinValue.AddDays(1)));
            Assert.True(range.Contains(DateTime.MaxValue.AddDays(-1)));
        }

        // ── Delta maths ──────────────────────────────────────────────────

        [Fact]
        public void Delta_FromZeroPrevious_ReportsNoPercentage()
        {
            // Going from nothing to something is not "+100%", it is
            // undefined. Inventing a number here would be a fabricated
            // business figure.
            var d = new MetricDelta { Current = 500m, Previous = 0m };
            Assert.Null(d.ChangePercent);
            Assert.True(d.HasComparison);   // there IS current activity
        }

        [Fact]
        public void Delta_BothZero_HasNothingToCompare()
        {
            var d = new MetricDelta { Current = 0m, Previous = 0m };
            Assert.Null(d.ChangePercent);
            Assert.False(d.HasComparison);
        }

        [Fact]
        public void Delta_ComputesPercentageChange()
        {
            var d = new MetricDelta { Current = 110m, Previous = 100m };
            Assert.NotNull(d.ChangePercent);
            Assert.Equal(10d, d.ChangePercent!.Value, 3);
            Assert.True(d.IsUp);
        }

        [Fact]
        public void Delta_HandlesADecrease()
        {
            var d = new MetricDelta { Current = 75m, Previous = 100m };
            Assert.Equal(-25d, d.ChangePercent!.Value, 3);
            Assert.False(d.IsUp);
        }

        [Fact]
        public void Delta_UsesAbsolutePrevious_SoNegativeBaselinesKeepTheirSign()
        {
            // Moving from -100 to -50 is an improvement. Dividing by a raw
            // negative would flip the sign and report it as a decline.
            var d = new MetricDelta { Current = -50m, Previous = -100m };
            Assert.Equal(50d, d.ChangePercent!.Value, 3);
            Assert.True(d.IsUp);
        }

        // ── Filter service ───────────────────────────────────────────────

        [Fact]
        public void Filter_DefaultsToAllTime_SoExistingTotalsAreUnchanged()
        {
            var svc = new DashboardFilterService();
            Assert.True(svc.CurrentRange.IsAllTime);
        }

        [Fact]
        public void Filter_NotifiesSubscribersOnChange()
        {
            var svc = new DashboardFilterService();
            DashboardDateRange? seen = null;

            using (svc.Subscribe(r => seen = r))
            {
                svc.SetRange(DashboardRangeKind.ThisMonth);
            }

            Assert.NotNull(seen);
            Assert.Equal(DashboardRangeKind.ThisMonth, seen!.Kind);
        }

        [Fact]
        public void Filter_DisposingUnsubscribes()
        {
            var svc = new DashboardFilterService();
            int hits = 0;

            var sub = svc.Subscribe(_ => hits++);
            svc.SetRange(DashboardRangeKind.ThisMonth);
            sub.Dispose();
            svc.SetRange(DashboardRangeKind.ThisYear);

            Assert.Equal(1, hits);
        }

        [Fact]
        public void Filter_SelectingTheSameRangeTwiceDoesNotReNotify()
        {
            // Re-broadcasting would reload every widget for no reason.
            var svc = new DashboardFilterService();
            int hits = 0;

            using var sub = svc.Subscribe(_ => hits++);
            svc.SetRange(DashboardRangeKind.ThisMonth);
            svc.SetRange(DashboardRangeKind.ThisMonth);

            Assert.Equal(1, hits);
        }

        [Fact]
        public void Filter_DisposingTwiceIsSafe()
        {
            var svc = new DashboardFilterService();
            var sub = svc.Subscribe(_ => { });
            sub.Dispose();
            sub.Dispose();   // must not throw or detach someone else
        }
    }
}
