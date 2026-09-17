// ══════════════════════════════════════════════════════════════════════
//  FILE: Tests/Agent/OptimizationScaleSafetyTests.cs
//
//  A 30,000-part job took the application down. The cause was arithmetic, not
//  a bad pointer:
//
//   • RunBestSplit calls FillOneSheet THREE times per sheet — one trial per
//     split rule — and each trial copies the whole remaining part list and
//     re-scans every remaining part for every placement. At 30,000 parts over
//     ~1,000 sheets that is on the order of 1.35 billion placement attempts
//     for that one strategy.
//   • RunSheetFill and RunWallSeed are the same shape and each runs once per
//     split rule, so the effort-2+ block adds roughly 4 billion more.
//   • NONE of them accepted a CancellationToken, and Advanced.cs and
//     BestSplit.cs contained zero cancellation checks. Cancel was a no-op for
//     the entire quadratic phase, so the only way out of a hung run was to kill
//     the process — which is what "the app crashed" means here.
//   • ScaleProfile's own Summary told the operator "only the minimum search was
//     run", while the code still ran all six quadratic strategies. The message
//     was not a lie about intent, but it was not true either.
//
//  These tests pin the fix. Two kinds:
//
//   BEHAVIOURAL — the scale bands, and that a cancelled token stops a run
//   before it produces anything.
//
//   STRUCTURAL — cancellation inside a long-running strategy is timing
//   dependent, and a test that asserts "it cancelled within N seconds" is a
//   flaky test that will be disabled within a month. So the guarantee that
//   every strategy accepts a token and checks it in its hot loops is asserted
//   against the source instead. That is deliberate, and it is the same reason
//   the XAML guarantees elsewhere in this suite are structural.
//
//  No dotnet SDK in this workspace: these run on the developer's machine.
// ══════════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class OptimizationScaleSafetyTests
    {
        private static string Root()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Services", "Optimization")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }

        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));

        private const string Packer = "Services/Optimization/GuillotinePacker.cs";
        private const string Advanced = "Services/Optimization/GuillotinePacker.Advanced.cs";
        private const string BestSplit = "Services/Optimization/GuillotinePacker.BestSplit.cs";
        private const string Smart = "Services/Optimization/GuillotinePacker.SmartPlacement.cs";
        private const string RunVm = "ViewModels/Optimization/OptimizationViewModel.Run.cs";

        private static CutPiece Piece(int w, int h) =>
            new CutPiece { JobRef = "J1", GlassRef = "6mm clear", WidthMm = w, HeightMm = h };

        private static MarginProfile Margins() => MarginProfile.DefaultFor(6);

        private static List<StockSize> Stocks() =>
            new List<StockSize> { new StockSize { WidthMm = 3210, HeightMm = 2250, ThicknessMm = 6 } };

        /// <summary>A job big enough that the quadratic strategies are the slow
        /// part, small enough that the test finishes in seconds either way.</summary>
        private static List<CutPiece> Job(int parts)
        {
            var list = new List<CutPiece>(parts);
            for (int i = 0; i < parts; i++)
                list.Add(Piece(600 + (i % 7) * 50, 400 + (i % 5) * 60));
            return list;
        }

        // ══ 1. the scale bands decide what may run ════════════════════════

        [Theory]
        [InlineData(500, ScaleTier.Standard, true)]
        [InlineData(2000, ScaleTier.Standard, true)]     // threshold is inclusive
        [InlineData(2001, ScaleTier.Large, true)]
        [InlineData(20000, ScaleTier.Large, true)]
        [InlineData(20001, ScaleTier.Huge, false)]
        [InlineData(30000, ScaleTier.Huge, false)]       // the job that took the app down
        public void The_quadratic_strategies_are_dropped_only_above_the_huge_threshold(
            int parts, ScaleTier expectedTier, bool expectedAllowed)
        {
            var scale = ScaleProfile.For(parts, 5);

            Assert.Equal(expectedTier, scale.Tier);
            Assert.Equal(expectedAllowed, scale.AllowQuadraticStrategies);
        }

        [Fact]
        public void The_benchmark_band_still_gets_the_full_search()
        {
            // Every calibration number the owner measured lives in the Standard
            // band. If this ever changes, the benchmark moves with it — so it is
            // pinned here rather than discovered later.
            var scale = ScaleProfile.For(500, 5);

            Assert.Equal(ScaleTier.Standard, scale.Tier);
            Assert.True(scale.AllowQuadraticStrategies);
            Assert.True(scale.AllowBeam);
            Assert.Equal(int.MaxValue, scale.ShuffleCap);
            Assert.Equal(int.MaxValue, scale.ConsolidationSweeps);
            Assert.False(scale.IsReduced);
        }

        [Fact]
        public void The_huge_band_tells_the_operator_what_it_refused_to_run()
        {
            // The summary already claimed "only the minimum search was run" while
            // six quadratic strategies still executed. A reduction that is
            // reported but not applied is worse than one that is not reported.
            string summary = ScaleProfile.For(30000, 5).Summary;

            Assert.Contains("quadratic", summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("best-split", summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Split the job", summary, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void The_engine_gates_the_quadratic_block_on_that_switch()
        {
            string source = Read(Packer);

            Assert.Contains("if (effortLevel >= 2 && scale.AllowQuadraticStrategies)", source);
        }

        // ══ 2. cancellation reaches every strategy ════════════════════════

        [Theory]
        [InlineData(Advanced, 5)]     // RunSheetFill x2, RunWallSeed x2, RunMaxRects x1
        [InlineData(BestSplit, 5)]    // FillOneSheet, RunBestSplit x2, beam x2
        [InlineData(Packer, 10)]      // combo loop, extras, consolidation donor loop, PackSmart wiring
        [InlineData(Smart, 2)]
        public void Each_engine_file_checks_the_token_in_its_hot_loops(string file, int minimum)
        {
            // Was ZERO in Advanced.cs and BestSplit.cs — the two files holding
            // every sheet-at-a-time strategy, which is exactly where a large run
            // spends all of its time.
            int checks = Regex.Matches(Read(file), "ThrowIfCancellationRequested").Count;

            Assert.True(checks >= minimum,
                $"{Path.GetFileName(file)} has {checks} cancellation checks, expected at least {minimum}.");
        }

        [Fact]
        public void Every_reachable_strategy_accepts_a_cancellation_token()
        {
            var missing = new List<string>();

            foreach (string file in new[] { Packer, Advanced, BestSplit, Smart })
            {
                string source = Read(file);
                foreach (Match m in Regex.Matches(source,
                    @"(?:private|public|internal) static [\w<>\(\), \.]+? (Run\w+|Pack\w+|ConsolidateAll|FillOneSheet)\(([^;{]*?)\)\s*\n?\s*\{",
                    RegexOptions.Singleline))
                {
                    string name = m.Groups[1].Value;
                    // RunSkyline is unreachable on purpose: GuillotinePacker.Smart.cs
                    // documents that nothing calls ImproveByRepack, and that it was
                    // measured to beat the shipped candidate pool zero times.
                    if (name == "RunSkyline") continue;

                    if (!m.Groups[2].Value.Contains("CancellationToken"))
                        missing.Add(Path.GetFileName(file) + ":" + name);
                }
            }

            Assert.True(missing.Count == 0,
                "strategies that cannot be cancelled: " + string.Join(", ", missing));
        }

        [Fact]
        public void A_cancelled_run_throws_before_producing_anything()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAny<OperationCanceledException>(() =>
                GuillotinePacker.PackOptimize(Job(200), Stocks(), Margins(),
                    RotationPolicy.Free90, 5, cts.Token));
        }

        [Fact]
        public void Cancelling_mid_run_surfaces_promptly_and_yields_no_plan()
        {
            // Cancelled from the progress sink on the LAST ordering pass, i.e.
            // immediately before the engine enters the strategies that used to be
            // uncancellable. Whatever it does next must observe the token.
            using var cts = new CancellationTokenSource();
            var sink = new CancelOnLastPass(cts, expectedPasses: 12);   // effort 3 = 4 orderings x 3 splits
            var sw = Stopwatch.StartNew();

            Assert.ThrowsAny<OperationCanceledException>(() =>
                GuillotinePacker.PackOptimize(Job(600), Stocks(), Margins(),
                    RotationPolicy.Free90, 3, cts.Token, sink));

            sw.Stop();
            Assert.True(sink.Cancelled, "the sink never saw the final pass report");
            Assert.True(sw.Elapsed.TotalSeconds < 60,
                $"Cancellation took {sw.Elapsed.TotalSeconds:F1}s to surface from the quadratic phase.");
        }

        [Fact]
        public void Quick_and_optimize_still_agree_after_the_scale_gating()
        {
            // Gating removes candidates; Better() keeps the minimum. Neither may
            // turn into a worse plan than the single QUICK pass.
            var pieces = Job(400);
            var quick = GuillotinePacker.PackQuick(pieces, Stocks(), Margins(), RotationPolicy.Free90);
            var optimized = GuillotinePacker.PackOptimize(pieces, Stocks(), Margins(), RotationPolicy.Free90, 3);

            Assert.True(optimized.SheetsUsed <= quick.SheetsUsed,
                $"sheets grew: {quick.SheetsUsed} -> {optimized.SheetsUsed}");
            Assert.True(optimized.Unplaced.Count <= quick.Unplaced.Count);
            Assert.Empty(GuillotinePacker.Verify(optimized, Margins()));
        }

        // ══ 3. the UI must not drown in a 30,000-row list ═════════════════

        [Fact]
        public void The_unplaced_list_is_capped_and_says_it_is_a_sample()
        {
            // One string per unplaced part, into a bound ObservableCollection, on
            // the dispatcher, one CollectionChanged each. At 30,000 parts that is
            // a frozen window — and the list is not the number the operator
            // needs, since ResultUnplaced already carries the true total.
            string source = Read(RunVm);

            Assert.Contains("const int unplacedRowCap = 500;", source);
            Assert.Contains("if (unplacedListed >= unplacedRowCap) continue;", source);
            Assert.Contains("more. The UNPLACED total above is the full count.", source);
            Assert.Contains("HasUnplaced = unplacedSeen > 0;", source);
        }

        // ── progress sink that cancels on the final ordering pass ─────────
        private sealed class CancelOnLastPass : IProgress<OptimizeProgress>
        {
            private readonly CancellationTokenSource _cts;
            private readonly int _expected;

            public CancelOnLastPass(CancellationTokenSource cts, int expectedPasses)
            {
                _cts = cts;
                _expected = expectedPasses;
            }

            public bool Cancelled { get; private set; }

            public void Report(OptimizeProgress value)
            {
                if (Cancelled || value.Stage != "pass") return;
                if (value.Completed + 1 < _expected) return;

                Cancelled = true;
                _cts.Cancel();
            }
        }
    }
}
