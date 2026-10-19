// ════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/OptimizationScaleTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// SCALE HARDENING — pins the guarantees added for very large jobs
// (~1 lakh / 100,000 parts). Three things are locked down here:
//
//   1. RESULT IDENTITY. Below OptimizationSearchGovernor.FullSearchPieceLimit the search is
//      untouched, so every calibrated benchmark figure still holds. The
//      engine changes at scale are pruning and allocation removal, not
//      heuristics: a sheet is skipped only when its exact free-rect
//      bounds prove no orientation of the piece can fit anywhere on it.
//
//   2. TERMINATION. Large inputs must finish, must stay verifiable, and
//      must never be worse than QUICK (OPTIMIZE always contains the
//      QUICK candidate, and Better() keeps the minimum).
//
//   3. HONESTY + CONTROL. Any size-driven reduction in search breadth is
//      reported on the result, progress is emitted per pass, and a
//      cancellation token stops the run promptly instead of leaving the
//      UI spinning.
// ════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class OptimizationScaleTests
    {
        private const int StockW = 3210;
        private const int StockH = 2250;

        private static readonly MarginProfile Margins = new MarginProfile
        {
            ThicknessMm = 6,
            LeftMm = 15,
            RightMm = 15,
            TopMm = 15,
            BottomMm = 15,
            KerfMm = 3,
            MinBreakoutMm = 15
        };

        private static readonly StockSize Stock = new StockSize
        {
            WidthMm = StockW,
            HeightMm = StockH,
            ThicknessMm = 6,
            Color = "6mm",
            Label = "3210 × 2250",
            MaxSheets = 0
        };

        private static List<StockSize> Stocks => new List<StockSize> { Stock };

        // ── 1. the scale policy itself ─────────────────────────────────

        [Fact]
        public void ScaleProfile_AtFullSearchThreshold_UsesFullSearchPolicy()
        {
            var p = ScaleProfile.For(OptimizationSearchGovernor.FullSearchPieceLimit, 5);

            Assert.Equal(ScaleTier.Standard, p.Tier);
            Assert.False(p.IsReduced);
            Assert.Equal(6, p.ShuffleCap);
            Assert.True(p.AllowBeam);
            Assert.Equal(6, p.MaxRectsOrderings);
            Assert.Equal(int.MaxValue, p.ConsolidationSweeps);
        }

        [Fact]
        public void ScaleProfile_JustAboveFullSearchThreshold_UsesNarrowedPolicy()
        {
            var p = ScaleProfile.For(OptimizationSearchGovernor.FullSearchPieceLimit + 1, 5);

            Assert.Equal(ScaleTier.Large, p.Tier);
            Assert.True(p.IsReduced);
            Assert.Equal(2, p.ShuffleCap);
            Assert.False(p.AllowBeam);
            Assert.Equal(3, p.MaxRectsOrderings);
            Assert.Equal(4, p.ConsolidationSweeps);
        }

        [Fact]
        public void ScaleProfile_JustAboveNarrowedThreshold_UsesIndustrialPolicy()
        {
            var p = ScaleProfile.For(OptimizationSearchGovernor.NarrowedSearchPieceLimit + 1, 5);

            Assert.Equal(ScaleTier.Huge, p.Tier);
            Assert.True(p.IsReduced);
            Assert.Equal(3, p.ShuffleCap);
            Assert.False(p.AllowBeam);
            Assert.Equal(3, p.MaxRectsOrderings);
            Assert.Equal(1, p.ConsolidationSweeps);
        }

        [Fact]
        public void ScaleProfile_NarrowedThresholdBoundary_IsStillLargeNotIndustrial()
        {
            Assert.Equal(ScaleTier.Large, ScaleProfile.For(OptimizationSearchGovernor.NarrowedSearchPieceLimit, 5).Tier);
        }

        [Fact]
        public void ScaleProfile_TierNeverDependsOnRequestedEffort()
        {
            // Determinism: the same job must always get the same budget,
            // otherwise a re-run could silently produce a different plan.
            for (int effort = 1; effort <= 5; effort++)
            {
                Assert.Equal(ScaleTier.Standard, ScaleProfile.For(500, effort).Tier);
                Assert.Equal(ScaleTier.Large, ScaleProfile.For(5000, effort).Tier);
                Assert.Equal(ScaleTier.Huge, ScaleProfile.For(50000, effort).Tier);
            }
        }

        [Fact]
        public void ScaleProfile_Summary_StatesThePartCount()
        {
            var reduced = ScaleProfile.For(OptimizationSearchGovernor.FullSearchPieceLimit + 1, 5);
            Assert.Contains((OptimizationSearchGovernor.FullSearchPieceLimit + 1).ToString(), reduced.Summary);

            var standard = ScaleProfile.For(10, 5);
            Assert.False(standard.IsReduced);
        }

        // ── 2. small inputs are untouched ──────────────────────────────

        [Fact]
        public void StandardSizeRun_ReportsNoScaleNotes()
        {
            var pieces = LargeJob(120);

            var result = GuillotinePacker.PackOptimize(
                pieces, Stocks, Margins, RotationPolicy.Free90, 5);

            Assert.Empty(result.ScaleNotes);
            Assert.Empty(GuillotinePacker.Verify(result, Margins));
        }

        [Fact]
        public void StandardSizeRun_IsBitIdenticalAcrossRepeatedRuns()
        {
            var pieces = LargeJob(200);

            var a = GuillotinePacker.PackOptimize(pieces, Stocks, Margins, RotationPolicy.Free90, 5);
            var b = GuillotinePacker.PackOptimize(pieces, Stocks, Margins, RotationPolicy.Free90, 5);

            Assert.Equal(Signature(a), Signature(b));
        }

        // ── 3. large inputs terminate, stay valid, stay deterministic ──

        [Fact]
        public void LargeRun_TerminatesAndVerifies()
        {
            // Above FullSearchPieceLimit, so the reduced search is exercised.
            var pieces = LargeJob(OptimizationSearchGovernor.FullSearchPieceLimit + 400);

            var sw = Stopwatch.StartNew();
            var result = GuillotinePacker.PackOptimize(
                pieces, Stocks, Margins, RotationPolicy.Free90, 5);
            sw.Stop();

            // A generous ceiling whose only job is to fail loudly if the
            // engine ever regresses to the pre-fix O(parts x sheets^2)
            // consolidation or starts allocating per placement again.
            Assert.True(sw.Elapsed.TotalSeconds < 240,
                $"OPTIMIZE at {pieces.Count} parts took {sw.Elapsed.TotalSeconds:F1}s — expected well under 240s.");

            Assert.NotEmpty(result.Sheets);
            Assert.Equal(pieces.Count, PlacedCount(result) + result.Unplaced.Count);
            Assert.Empty(GuillotinePacker.Verify(result, Margins));
        }

        [Fact]
        public void LargeRun_ReportsTheReductionItApplied()
        {
            var pieces = LargeJob(OptimizationSearchGovernor.FullSearchPieceLimit + 100);

            var result = GuillotinePacker.PackOptimize(
                pieces, Stocks, Margins, RotationPolicy.Free90, 5);

            Assert.NotEmpty(result.ScaleNotes);
            Assert.Contains(pieces.Count.ToString(), result.ScaleNotes[0]);
        }

        [Fact]
        public void LargeRun_IsDeterministic()
        {
            var pieces = LargeJob(OptimizationSearchGovernor.FullSearchPieceLimit + 100);

            var a = GuillotinePacker.PackOptimize(pieces, Stocks, Margins, RotationPolicy.Free90, 4);
            var b = GuillotinePacker.PackOptimize(pieces, Stocks, Margins, RotationPolicy.Free90, 4);

            Assert.Equal(Signature(a), Signature(b));
        }

        [Fact]
        public void LargeRun_OptimizeIsNeverWorseThanQuick()
        {
            // OPTIMIZE always evaluates the QUICK candidate (OrderBy.Area /
            // HorizontalFirst / no shuffle survives every shuffle cap) and
            // Better() keeps the minimum, so this must hold at every tier.
            var pieces = LargeJob(OptimizationSearchGovernor.FullSearchPieceLimit + 100);

            var quick = GuillotinePacker.PackQuick(
                pieces, Stocks, Margins, RotationPolicy.Free90, SplitMode.HorizontalFirst);
            var optimized = GuillotinePacker.PackOptimize(
                pieces, Stocks, Margins, RotationPolicy.Free90, 5);

            Assert.True(optimized.Sheets.Count <= quick.Sheets.Count,
                $"OPTIMIZE used {optimized.Sheets.Count} sheets vs QUICK {quick.Sheets.Count}.");
        }

        [Fact]
        public void LargeRun_QuickAlsoTerminates()
        {
            var pieces = LargeJob(OptimizationSearchGovernor.FullSearchPieceLimit + 100);

            var result = GuillotinePacker.PackQuick(
                pieces, Stocks, Margins, RotationPolicy.Free90, SplitMode.HorizontalFirst);

            Assert.NotEmpty(result.Sheets);
            Assert.Empty(GuillotinePacker.Verify(result, Margins));
        }

        // ── 4. cancellation and progress ───────────────────────────────

        [Fact]
        public void AlreadyCancelledToken_StopsOptimizeImmediately()
        {
            var pieces = LargeJob(500);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var sw = Stopwatch.StartNew();
            Assert.ThrowsAny<OperationCanceledException>(() =>
                GuillotinePacker.PackOptimize(
                    pieces, Stocks, Margins, RotationPolicy.Free90, 5, cts.Token));
            sw.Stop();

            Assert.True(sw.Elapsed.TotalSeconds < 10,
                $"Cancellation took {sw.Elapsed.TotalSeconds:F1}s; the engine checks the token per part.");
        }

        [Fact]
        public void AlreadyCancelledToken_StopsQuickToo()
        {
            var pieces = LargeJob(500);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAny<OperationCanceledException>(() =>
                GuillotinePacker.PackQuick(
                    pieces, Stocks, Margins, RotationPolicy.Free90,
                    SplitMode.HorizontalFirst, cts.Token));
        }

        [Fact]
        public void CancellationMidRun_UnwindsOutOfTheEngine()
        {
            // The sink is synchronous on purpose: Progress<T> would post to a
            // synchronization context and make the assertion race the run.
            var pieces = LargeJob(OptimizationSearchGovernor.FullSearchPieceLimit + 100);
            using var cts = new CancellationTokenSource();
            var cancelAfterTwoReports = new CancelAfterN(cts, 2);

            var sw = Stopwatch.StartNew();
            Assert.ThrowsAny<OperationCanceledException>(() =>
                GuillotinePacker.PackOptimize(
                    pieces, Stocks, Margins, RotationPolicy.Free90, 5,
                    cts.Token, cancelAfterTwoReports));
            sw.Stop();

            Assert.True(cts.IsCancellationRequested);
            Assert.True(sw.Elapsed.TotalSeconds < 120,
                $"A mid-run cancel took {sw.Elapsed.TotalSeconds:F1}s to surface.");
        }

        [Fact]
        public void Progress_IsReportedPerPassAndEndsWithDone()
        {
            var pieces = LargeJob(400);
            var collector = new ProgressCollector();

            var result = GuillotinePacker.PackOptimize(
                pieces, Stocks, Margins, RotationPolicy.Free90, 3,
                CancellationToken.None, collector);

            Assert.NotEmpty(collector.Reports);

            var passes = collector.Reports.Where(r => r.Stage == "pass").ToList();
            Assert.NotEmpty(passes);
            Assert.All(passes, p => Assert.True(p.Total > 0));
            Assert.Equal(passes.Count, passes.Max(p => p.Completed) + 1);

            var last = collector.Reports.Last();
            Assert.Equal("done", last.Stage);
            Assert.Equal(1.0, last.Fraction, 6);
            Assert.Equal(result.Sheets.Count.ToString(),
                last.Message.Split(' ').First());
        }

        [Fact]
        public void Progress_FractionNeverExceedsOne()
        {
            var pieces = LargeJob(300);
            var collector = new ProgressCollector();

            GuillotinePacker.PackOptimize(
                pieces, Stocks, Margins, RotationPolicy.Free90, 2,
                CancellationToken.None, collector);

            Assert.All(collector.Reports, r =>
            {
                Assert.InRange(r.Fraction, 0.0, 1.0);
                Assert.False(string.IsNullOrWhiteSpace(r.Stage));
            });
        }

        [Fact]
        public void EmptyInput_ReturnsEmptyResultWithoutThrowing()
        {
            var result = GuillotinePacker.PackOptimize(
                new List<CutPiece>(), Stocks, Margins, RotationPolicy.Free90, 5);

            Assert.Empty(result.Sheets);
            Assert.Empty(result.Unplaced);
            Assert.Empty(result.ScaleNotes);
        }

        [Fact]
        public void NullArguments_AreRejectedWithClearMessages()
        {
            Assert.ThrowsAny<ArgumentException>(() => GuillotinePacker.PackOptimize(
                null, Stocks, Margins, RotationPolicy.Free90, 5));
            Assert.ThrowsAny<ArgumentException>(() => GuillotinePacker.PackOptimize(
                LargeJob(5), new List<StockSize>(), Margins, RotationPolicy.Free90, 5));
            Assert.ThrowsAny<ArgumentException>(() => GuillotinePacker.PackOptimize(
                LargeJob(5), Stocks, null, RotationPolicy.Free90, 5));
        }

        // ── helpers ────────────────────────────────────────────────────

        private static int PlacedCount(OptimizationResult r) =>
            r.Sheets.Sum(s => s.Pieces.Count);

        /// <summary>Exact layout fingerprint: every sheet's stock and every
        /// piece's position, size and rotation. Any drift fails the test.</summary>
        private static string Signature(OptimizationResult r)
        {
            var sb = new StringBuilder();
            foreach (var s in r.Sheets)
            {
                sb.Append(s.Stock.WidthMm).Append('x').Append(s.Stock.HeightMm).Append(':');
                foreach (var p in s.Pieces)
                    sb.Append(p.Piece.PieceLabel).Append('@')
                      .Append(p.X).Append(',').Append(p.Y).Append(',')
                      .Append(p.WidthMm).Append('x').Append(p.HeightMm).Append(',')
                      .Append(p.Rotated ? 'R' : 'N').Append(';');
                sb.Append('|');
            }
            sb.Append("#");
            foreach (var u in r.Unplaced) sb.Append(u.PieceLabel).Append(',');
            return sb.ToString();
        }

        /// <summary>
        /// Deterministic job generator. Uses a local xorshift rather than
        /// System.Random so the fixture cannot shift between runtimes — these
        /// tests assert exact coordinates.
        /// </summary>
        private static List<CutPiece> LargeJob(int count, int seed = 20260916)
        {
            var rnd = new XorShift(seed);
            var list = new List<CutPiece>(count);
            for (int i = 0; i < count; i++)
            {
                int bucket = rnd.Next(100);
                int w, h;
                if (bucket < 30) { w = rnd.Next(300, 900); h = rnd.Next(300, 900); }
                else if (bucket < 60) { w = rnd.Next(600, 1500); h = rnd.Next(400, 1100); }
                else if (bucket < 85) { w = rnd.Next(1200, 2000); h = rnd.Next(600, 1200); }
                else { w = rnd.Next(1800, 2090); h = rnd.Next(900, 1200); }

                list.Add(new CutPiece
                {
                    JobRef = "SCALE",
                    GlassRef = "6mm clear ft glass",
                    SpecLabel = "S1",
                    Position = (i + 1).ToString(),
                    Color = "6mm",
                    ThicknessMm = 6,
                    WidthMm = w,
                    HeightMm = h,
                    AllowRotate = rnd.Next(100) < 85,
                    PieceLabel = $"P{i:D6}_{w}x{h}"
                });
            }
            return list;
        }

        private sealed class XorShift
        {
            private uint _s;
            public XorShift(int seed) { _s = unchecked((uint)seed); if (_s == 0) _s = 0x9E3779B9; }
            public int Next(int maxExclusive) => (int)(Step() % (uint)maxExclusive);
            public int Next(int minInclusive, int maxExclusive) =>
                minInclusive + Next(maxExclusive - minInclusive);
            private uint Step()
            {
                unchecked
                {
                    _s ^= _s << 13;
                    _s ^= _s >> 17;
                    _s ^= _s << 5;
                }
                return _s;
            }
        }

        /// <summary>Synchronous progress sink — Progress&lt;T&gt; would post to a
        /// synchronization context and make assertions race.</summary>
        private sealed class ProgressCollector : IProgress<OptimizeProgress>
        {
            public List<OptimizeProgress> Reports { get; } = new List<OptimizeProgress>();
            public void Report(OptimizeProgress value)
            {
                lock (Reports) Reports.Add(value);
            }
        }

        /// <summary>Cancels after the Nth report, to prove a mid-run cancel
        /// unwinds through the engine instead of being ignored.</summary>
        private sealed class CancelAfterN : IProgress<OptimizeProgress>
        {
            private readonly CancellationTokenSource _cts;
            private readonly int _n;
            private int _count;
            public CancelAfterN(CancellationTokenSource cts, int n) { _cts = cts; _n = n; }
            public void Report(OptimizeProgress value)
            {
                if (++_count >= _n && !_cts.IsCancellationRequested) _cts.Cancel();
            }
        }
    }
}
