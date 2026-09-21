using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

public sealed class PatternSearchRegressionTests
{
    // Generated fixtures only. The customer's second cutting list is NOT
    // embedded in production code, tests, or checked-in sample data.
    private static List<CutPiece> Job(int count, int seed)
    {
        var random = new Random(seed);
        return Enumerable.Range(0, count).Select(i => new CutPiece
        {
            WidthMm = random.Next(450, 1050),
            HeightMm = i % 3 == 0 ? random.Next(2600, 3000) : random.Next(900, 1750),
            ThicknessMm = 6, PieceLabel = i.ToString(), AllowRotate = true
        }).ToList();
    }

    private static StockSize Stock(int cap = 0) => new()
    { WidthMm = 3210, HeightMm = 2250, ThicknessMm = 6, MaxSheets = cap };

    private static readonly MarginProfile Margins = MarginProfile.DefaultFor(6);

    private static OptimizationResult Beam(List<CutPiece> input, StockSize[] stocks, int width) =>
        (OptimizationResult)typeof(GuillotinePacker).GetMethod("RunBestSplitBeam", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { input, stocks, Margins, RotationPolicy.Free90, width,
                CancellationToken.None, Type.Missing, int.MaxValue, 3, null! })!;

    [Fact]
    public void Quick_includes_the_valid_beam_four_and_eight_quality_floor()
    {
        int checkedCandidates = 0;
        for (int seed = 0; seed < 12; seed++)
        {
            var input = Job(30 + seed, seed);
            var stocks = new[] { Stock() };
            var quick = GuillotinePacker.PackQuick(input, stocks, Margins, RotationPolicy.Free90);
            Assert.Empty(GuillotinePacker.VerifyComprehensive(quick, input, Margins));
            foreach (int width in new[] { 4, 8 })
            {
                var candidate = Beam(input, stocks, width);
                if (GuillotinePacker.VerifyComprehensive(candidate, input, Margins).Count != 0) continue;
                checkedCandidates++;
                Assert.True(quick.Unplaced.Count <= candidate.Unplaced.Count);
                if (quick.Unplaced.Count == candidate.Unplaced.Count)
                    Assert.True(quick.SheetsUsed <= candidate.SheetsUsed);
            }
        }
        Assert.True(checkedCandidates > 0);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    [InlineData(4, 5)]
    [InlineData(5, 6)]
    public void Higher_levels_add_pattern_candidates_without_removing_the_prefix(int level, int count)
    {
        var schedule = typeof(GuillotinePacker).GetMethod("PatternCandidates", BindingFlags.NonPublic | BindingFlags.Static)!;
        var entries = ((System.Collections.IEnumerable)schedule.Invoke(null, new object[] { 1, level })!).Cast<object>().ToList();
        Assert.Equal(count, entries.Count);
        var widths = entries.Select(e => (int)e.GetType().GetField("Item1")!.GetValue(e)!).ToArray();
        Assert.Equal(new[] { 4, 8, 12, 16, 24, 32 }.Take(count).ToArray(), widths);
    }

    [Theory]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(400)]
    public void Whole_job_and_tail_windows_preserve_inventory_and_every_identity(int count)
    {
        var input = Job(count, count);
        var stocks = new[] { Stock(12), Stock(18) };
        var result = GuillotinePacker.PackQuick(input, stocks, Margins, RotationPolicy.Free90);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(result, input, Margins));
        Assert.False(result.HasValidationFailure);
        Assert.True(result.Sheets.Count(s => ReferenceEquals(s.Stock, stocks[0])) <= 12);
        Assert.True(result.Sheets.Count(s => ReferenceEquals(s.Stock, stocks[1])) <= 18);
        Assert.Equal(count, result.Sheets.Sum(s => s.Pieces.Count) + result.Unplaced.Count);
        Assert.True(result.Unplaced.Count > 0); // shortage cannot reset the tail's rack budgets
    }

    [Fact]
    public void Bounded_pattern_repair_does_not_mutate_the_retained_floor()
    {
        var input = Job(280, 22); var stocks = new[] { Stock() };
        var baseline = GuillotinePacker.PackQuick(input, stocks, Margins, RotationPolicy.Free90);
        string Signature(OptimizationResult r) => string.Join("|", r.Sheets.SelectMany(s => s.Pieces.Select(p =>
            $"{s.SheetNo}:{p.Piece.PieceLabel}:{p.X}:{p.Y}:{p.WidthMm}:{p.HeightMm}")));
        string before = Signature(baseline);
        var result = (OptimizationResult)typeof(GuillotinePacker)
            .GetMethod("ImprovePatterns", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { baseline, input, stocks, Margins, RotationPolicy.Free90,
                2, 5, CancellationToken.None, null! })!;
        Assert.Equal(before, Signature(baseline));
        Assert.Empty(GuillotinePacker.VerifyComprehensive(result, input, Margins));
        Assert.Empty(result.Unplaced);
        Assert.True(result.SheetsUsed <= baseline.SheetsUsed);
    }

    [Fact]
    public void Increasing_effort_retains_the_earlier_valid_result_on_generated_small_jobs()
    {
        for (int seed = 0; seed < 4; seed++)
        {
            var input = Job(38 + seed, seed); var stocks = new[] { Stock() };
            var previous = GuillotinePacker.PackQuick(input, stocks, Margins, RotationPolicy.Free90);
            for (int level = 2; level <= 5; level++)
            {
                var next = GuillotinePacker.PackOptimize(input, stocks, Margins, RotationPolicy.Free90, level);
                Assert.Empty(GuillotinePacker.VerifyComprehensive(next, input, Margins));
                Assert.Empty(next.Unplaced);
                Assert.True(next.SheetsUsed <= previous.SheetsUsed);
                previous = next;
            }
        }
    }

    [Fact]
    public void An_expired_optional_search_budget_keeps_the_complete_floor()
    {
        var input = Job(35, 9); var stocks = new[] { Stock() };
        var baseline = GuillotinePacker.PackQuick(input, stocks, Margins, RotationPolicy.Free90);
        var result = (OptimizationResult)typeof(GuillotinePacker)
            .GetMethod("ImprovePatterns", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { baseline, input, stocks, Margins, RotationPolicy.Free90,
                2, 5, CancellationToken.None, new Func<bool>(() => true) })!;
        Assert.True(ReferenceEquals(baseline, result));
        Assert.Empty(GuillotinePacker.VerifyComprehensive(result, input, Margins));
    }

    private sealed class CancelAtPattern : IProgress<OptimizeProgress>
    {
        public CancellationTokenSource Source { get; } = new();
        public void Report(OptimizeProgress value) { if (value.Stage == "pattern") Source.Cancel(); }
    }

    [Fact]
    public void Cancellation_at_the_new_quick_pattern_stage_is_not_swallowed()
    {
        var sink = new CancelAtPattern();
        using (sink.Source)
            Assert.Throws<OperationCanceledException>(() => GuillotinePacker.PackQuick(Job(35, 0),
                new[] { Stock() }, Margins, RotationPolicy.Free90,
                cancellationToken: sink.Source.Token, progress: sink));
    }
}
