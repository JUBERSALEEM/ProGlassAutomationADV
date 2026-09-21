using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

public sealed class RepeatedPatternRegressionTests
{
    private static readonly MarginProfile Margins = MarginProfile.DefaultFor(6);
    private static List<CutPiece> Panes(int count, int w = 600, int h = 400) => Enumerable.Range(0, count)
        .Select(i => new CutPiece { WidthMm = w, HeightMm = h, ThicknessMm = 6, PieceLabel = i.ToString(), AllowRotate = true }).ToList();
    private static StockSize Stock(int cap = 0) => new() { WidthMm = 1260, HeightMm = 860, ThicknessMm = 6, MaxSheets = cap };
    private static OptimizationResult Repeats(List<CutPiece> input, StockSize[] stocks, RotationPolicy rotation = RotationPolicy.Free90) =>
        (OptimizationResult)typeof(GuillotinePacker).GetMethod("RunRepeatedPatterns", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { input, stocks, Margins, rotation, 1, CancellationToken.None })!;
    private static void Valid(OptimizationResult r, List<CutPiece> input)
    {
        Assert.Empty(GuillotinePacker.VerifyComprehensive(r, input, Margins));
        var returned = r.Sheets.SelectMany(s => s.Pieces).Select(p => p.Piece).Concat(r.Unplaced).ToHashSet();
        Assert.Equal(input.Count, returned.Count);
        Assert.True(input.All(returned.Contains));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(40)]
    [InlineData(10000)]
    public void Exact_repeat_quantities_scale_without_rounding_or_overproduction(int quantity)
    {
        var input = Panes(quantity); var r = Repeats(input, new[] { Stock() });
        Valid(r, input); Assert.Empty(r.Unplaced);
        Assert.Equal((quantity + 3) / 4, r.SheetsUsed);
    }

    [Fact]
    public void Finite_inventory_and_multiple_racks_are_global_not_per_pattern()
    {
        var input = Panes(40); var stocks = new[] { Stock(3), Stock(5) };
        var r = Repeats(input, stocks); Valid(r, input);
        Assert.Equal(8, r.SheetsUsed); Assert.Equal(8, r.Unplaced.Count);
        Assert.Equal(3, r.Sheets.Count(s => ReferenceEquals(s.Stock, stocks[0])));
        Assert.Equal(5, r.Sheets.Count(s => ReferenceEquals(s.Stock, stocks[1])));
    }

    [Fact]
    public void Large_quantities_retain_piece_rotation_permissions()
    {
        var input = Enumerable.Range(0, 320).Select(i => new CutPiece { WidthMm = 800, HeightMm = 1200,
            ThicknessMm = 6, PieceLabel = i.ToString(), AllowRotate = i % 2 == 0 }).ToList();
        var r = Repeats(input, new[] { Stock() }); Valid(r, input);
        Assert.Equal(160, r.Unplaced.Count);
        Assert.True(r.Sheets.SelectMany(s => s.Pieces).All(p => p.Piece.AllowRotate && p.Rotated));
        var locked = Repeats(input, new[] { Stock() }, RotationPolicy.Locked); Valid(locked, input);
        Assert.Empty(locked.Sheets); Assert.Equal(input.Count, locked.Unplaced.Count);
    }

    [Fact]
    public void Material_windows_do_not_mix_colors_or_reset_rack_caps()
    {
        var input = Enumerable.Range(0, 16).Select(i => new CutPiece { WidthMm = 600, HeightMm = 400,
            ThicknessMm = 6, Color = i < 8 ? "Clear" : "Grey", PieceLabel = i.ToString() }).ToList();
        var r = Repeats(input, new[] { Stock(3) }); Valid(r, input);
        Assert.True(r.Sheets.All(s => s.Pieces.Select(p => p.Piece.Color).Distinct().Count() == 1));
        Assert.Equal(3, r.SheetsUsed); Assert.Equal(4, r.Unplaced.Count);
    }

    [Fact]
    public void More_than_one_type_window_preserves_all_input_identities()
    {
        var input = Enumerable.Range(0, 270).Select(i => new CutPiece { WidthMm = 250 + i % 135,
            HeightMm = 250, ThicknessMm = 6, PieceLabel = i.ToString() }).ToList();
        var r = Repeats(input, new[] { Stock() }); Valid(r, input); Assert.Empty(r.Unplaced);
    }

    [Fact]
    public void Invalid_and_oversized_panes_are_not_silently_discarded()
    {
        var input = Panes(8);
        input.Add(new CutPiece { WidthMm = 9000, HeightMm = 9000, PieceLabel = "oversize" });
        var r = Repeats(input, new[] { Stock() }); Valid(r, input);
        Assert.Equal(1, r.Unplaced.Count);
    }

    [Fact]
    public void Subset_patterns_cannot_introduce_a_sub_breakout_gap()
    {
        var input = Panes(17, 2, 2);
        var r = Repeats(input, new[] { new StockSize { WidthMm = 100, HeightMm = 100, ThicknessMm = 6 } });
        Valid(r, input); Assert.Empty(r.Unplaced);
        Assert.Empty(GuillotinePacker.AuditBreakout(r, Margins));
    }

    [Fact]
    public void Master_solution_matches_a_known_fractional_optimum()
    {
        var masterType = typeof(GuillotinePacker).GetNestedType("RepeatMaster", BindingFlags.NonPublic)!;
        var patternType = typeof(GuillotinePacker).GetNestedType("RepeatPattern", BindingFlags.NonPublic)!;
        object Pattern((int, int)[] counts)
        {
            var p = Activator.CreateInstance(patternType)!;
            patternType.GetField("Counts")!.SetValue(p, counts); return p;
        }
        foreach (int capacity in new[] { int.MaxValue, 2 })
        {
            var m = Activator.CreateInstance(masterType, new object[] { new[] { 6, 4 }, new[] { capacity },
                11d, CancellationToken.None, new Func<bool>(() => false), null!, 0 })!;
            masterType.GetMethod("Add")!.Invoke(m, new[] { Pattern(new[] { (0, 2), (1, 1) }), (object)0 });
            masterType.GetMethod("Add")!.Invoke(m, new[] { Pattern(new[] { (1, 2) }), (object)1 });
            masterType.GetMethod("Solve")!.Invoke(m, null);
            double objective = (double)masterType.GetProperty("Objective")!.GetValue(m)!;
            Assert.Equal(capacity == int.MaxValue ? 3.5d : 46d, objective, 6);
        }
    }

    [Theory]
    [InlineData(100000)]
    [InlineData(100001)]
    public void Industrial_dispatch_keeps_the_exact_repeat_count(int quantity)
    {
        var input = Panes(quantity);
        var r = GuillotinePacker.PackOptimizePartitioned(input, new[] { Stock() }, Margins, RotationPolicy.Free90, 1);
        Valid(r, input); Assert.Empty(r.Unplaced);
        Assert.Equal((quantity + 3) / 4, r.SheetsUsed);
    }

    private sealed class CancelRepeated : IProgress<OptimizeProgress>
    {
        public CancellationTokenSource Source { get; } = new();
        public void Report(OptimizeProgress progress) { if (progress.Stage == "repeat") Source.Cancel(); }
    }

    [Fact]
    public void Cancellation_at_the_quantity_planner_boundary_is_observed()
    {
        var progress = new CancelRepeated();
        using (progress.Source)
            Assert.Throws<OperationCanceledException>(() => GuillotinePacker.PackQuick(Panes(40), new[] { Stock() },
                Margins, RotationPolicy.Free90, cancellationToken: progress.Source.Token, progress: progress));
    }

    [Fact]
    public void Industrial_quick_route_is_cancellable_before_work_starts()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => GuillotinePacker.PackQuick(Panes(100000),
            new[] { Stock() }, Margins, RotationPolicy.Free90, cancellationToken: cts.Token));
    }
}
