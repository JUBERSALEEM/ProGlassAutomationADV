using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

public sealed class CapacityStripRegressionTests
{
    // Customer regression data belongs ONLY in tests. The production candidate
    // knows neither these dimensions, this material, nor the nine-sheet target.
    private static readonly (int W, int H)[] Job =
    {
        (1073,1198),(473,1168),(473,1158),(1068,1168),(1068,1188),(1078,1178),
        (1081,1183),(1068,1183),(1068,1178),(463,1188),(473,1188),(1073,1188),
        (1073,1173),(1078,1178),(1083,1178),(1073,1178),(1068,1168),(473,1178),
        (468,1188),(1073,1168),(1068,1158),(1068,1168),(1068,1178),(478,1153),
        (478,1153),(1068,1168),(1068,1168),(1068,1168),(1068,1168),(1068,1168),
        (1068,1168),(488,1168),(478,1178),(1068,1168),(1068,1168),(1068,1168),
        (1068,1168),(1068,1168),(1068,1168),(458,1178),(468,1178),(1068,1158),
        (1078,1178),(1038,1173),(983,1173),(1078,1173)
    };

    private static List<CutPiece> Sample(int scale = 1, bool transpose = false) => Job
        .Select((p, i) => new CutPiece
        {
            WidthMm = (transpose ? p.H : p.W) * scale,
            HeightMm = (transpose ? p.W : p.H) * scale,
            ThicknessMm = 6, Color = "any material", GlassRef = "not a special case",
            PieceLabel = "part-" + i, AllowRotate = true
        }).ToList();

    private static StockSize Stock(int scale = 1, int cap = 0) => new()
    { WidthMm = 3210 * scale, HeightMm = 2250 * scale, ThicknessMm = 6, MaxSheets = cap };

    private static MarginProfile Margins(int scale = 1) => new()
    {
        ThicknessMm = 6, LeftMm = 15 * scale, RightMm = 15 * scale,
        TopMm = 15 * scale, BottomMm = 15 * scale, KerfMm = 3 * scale,
        MinBreakoutMm = 15 * scale
    };

    private static void Valid(OptimizationResult result, IReadOnlyList<CutPiece> input, MarginProfile margins)
    {
        Assert.Empty(GuillotinePacker.VerifyComprehensive(result, input, margins));
        Assert.Empty(GuillotinePacker.AuditBreakout(result, margins));
        Assert.False(result.HasValidationFailure);
        var output = result.Sheets.SelectMany(s => s.Pieces).Select(p => p.Piece)
            .Concat(result.Unplaced).ToList();
        Assert.Equal(input.Count, output.Count);
        Assert.Equal(input.Count, output.Distinct().Count());
        Assert.True(input.All(output.Contains));
    }

    [Theory]
    [InlineData(0)] // direct QUICK entry point, not only OPTIMIZE effort 1
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Customer_job_fits_nine_valid_sheets_at_every_level(int effort)
    {
        var input = Sample(); var margins = Margins();
        var result = effort == 0
            ? GuillotinePacker.PackQuick(input, new[] { Stock() }, margins, RotationPolicy.Free90)
            : GuillotinePacker.PackOptimizePartitioned(input, new[] { Stock() }, margins, RotationPolicy.Free90, effort);
        Valid(result, input, margins);
        Assert.Empty(result.Unplaced);
        Assert.True(result.SheetsUsed <= 9);
        Assert.True(result.UtilizationPct >= 75.72m);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public void Pattern_generalizes_to_transposed_scaled_and_reordered_inputs(int scale, bool transpose)
    {
        var input = Sample(scale, transpose);
        input.Reverse();
        var margins = Margins(scale);
        var result = GuillotinePacker.PackQuick(input, new[] { Stock(scale) }, margins, RotationPolicy.Free90);
        Valid(result, input, margins);
        Assert.Empty(result.Unplaced);
        Assert.True(result.SheetsUsed <= 9);
    }

    [Fact]
    public void Mixed_jobs_preserve_safety_and_deeper_search_never_regresses_quick()
    {
        for (int seed = 0; seed < 24; seed++)
        {
            var random = new Random(seed);
            var input = Enumerable.Range(0, 35 + seed).Select(i => new CutPiece
            {
                WidthMm = random.Next(250, 1300), HeightMm = random.Next(200, 1250),
                ThicknessMm = 6, PieceLabel = i.ToString(), AllowRotate = i % 4 != 0
            }).ToList();
            var stocks = new[] { new StockSize { WidthMm = random.Next(2400, 3900),
                HeightMm = random.Next(2100, 3000), ThicknessMm = 6 } };
            var margins = Margins();
            var quick = GuillotinePacker.PackQuick(input, stocks, margins, RotationPolicy.Free90);
            var deeper = GuillotinePacker.PackOptimize(input, stocks, margins, RotationPolicy.Free90, 3);
            Valid(quick, input, margins); Valid(deeper, input, margins);
            Assert.Empty(quick.Unplaced); Assert.Empty(deeper.Unplaced);
            Assert.True(deeper.SheetsUsed <= quick.SheetsUsed);
            Assert.True(deeper.Sheets.SelectMany(s => s.Pieces).All(p => p.Piece.AllowRotate || !p.Rotated));
        }
    }

    [Fact]
    public void Rotation_locks_and_repeatability_survive_the_new_portfolio()
    {
        var input = Sample(); var stocks = new[] { Stock() }; var margins = Margins();
        var a = GuillotinePacker.PackQuick(input, stocks, margins, RotationPolicy.Locked);
        var b = GuillotinePacker.PackQuick(input, stocks, margins, RotationPolicy.Locked);
        Valid(a, input, margins); Valid(b, input, margins);
        Assert.True(a.Sheets.SelectMany(s => s.Pieces).All(p => !p.Rotated));
        string Shape(OptimizationResult r) => string.Join("|", r.Sheets.SelectMany(s => s.Pieces
            .Select(p => $"{s.SheetNo}:{p.Piece.PieceLabel}:{p.X}:{p.Y}:{p.WidthMm}:{p.HeightMm}")));
        Assert.Equal(Shape(a), Shape(b));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Finite_stock_returns_shortage_without_exceeding_caps(int effort)
    {
        var input = Sample(); var margins = Margins();
        var r = GuillotinePacker.PackOptimizePartitioned(input, new[] { Stock(cap: 8) }, margins, RotationPolicy.Free90, effort);
        Valid(r, input, margins);
        Assert.True(r.SheetsUsed <= 8);
        Assert.NotEmpty(r.Unplaced);
    }

    [Fact]
    public void Partition_threshold_does_not_multiply_a_finite_stock_cap()
    {
        var input = Enumerable.Range(0, 2100).Select(i => new CutPiece
        { WidthMm = 1000, HeightMm = 800, PieceLabel = i.ToString() }).ToList();
        var r = GuillotinePacker.PackOptimizePartitioned(input, new[] { Stock(cap: 2) }, Margins(), RotationPolicy.Free90, 2);
        Valid(r, input, Margins());
        Assert.True(r.SheetsUsed <= 2);
        Assert.NotEmpty(r.Unplaced);
    }

    [Fact]
    public void Mixed_stock_sizes_and_asymmetric_trims_preserve_caps_and_identities()
    {
        var input = Sample();
        var margins = new MarginProfile { LeftMm = 50, RightMm = 50, TopMm = 0,
            BottomMm = 50, ThicknessMm = 6, KerfMm = 3, MinBreakoutMm = 15 };
        var small = new StockSize { WidthMm = 1800, HeightMm = 1500, ThicknessMm = 6, MaxSheets = 2 };
        var large = Stock();
        var r = GuillotinePacker.PackQuick(input, new[] { large, small }, margins, RotationPolicy.Free90);
        Valid(r, input, margins);
        Assert.Empty(r.Unplaced);
        Assert.True(r.Sheets.Count(sheet => ReferenceEquals(sheet.Stock, small)) <= 2);
    }

    private sealed class ProgressLog : IProgress<OptimizeProgress>
    {
        public List<OptimizeProgress> Items { get; } = new();
        public void Report(OptimizeProgress value) => Items.Add(value);
    }

    [Fact]
    public void Quick_reports_done_only_after_its_whole_portfolio_finishes()
    {
        var log = new ProgressLog();
        GuillotinePacker.PackQuick(Sample(), new[] { Stock() }, Margins(), RotationPolicy.Free90, progress: log);
        Assert.Equal("done", log.Items.Last().Stage);
        Assert.Equal(1, log.Items.Count(p => p.Stage == "done"));
        Assert.True(log.Items.Zip(log.Items.Skip(1), (a, b) => a.Fraction <= b.Fraction).All(v => v));
    }

    [Fact]
    public void Breakout_failure_is_part_of_comprehensive_verification()
    {
        var a = new CutPiece { WidthMm = 100, HeightMm = 100, PieceLabel = "a" };
        var b = new CutPiece { WidthMm = 100, HeightMm = 100, PieceLabel = "b" };
        var result = new OptimizationResult { Rotation = RotationPolicy.Free90 };
        var sheet = new SheetLayout { Stock = Stock(), SheetNo = 1, UsableX = 15, UsableY = 15,
            UsableWidth = 3180, UsableHeight = 2220 };
        sheet.Pieces.Add(new PlacedPiece { Piece = a, X = 15, Y = 15, WidthMm = 100, HeightMm = 100 });
        sheet.Pieces.Add(new PlacedPiece { Piece = b, X = 123, Y = 15, WidthMm = 100, HeightMm = 100 });
        result.Sheets.Add(sheet);
        Assert.Empty(GuillotinePacker.Verify(result, Margins())); // kerf alone accepts an 8mm gap
        Assert.NotEmpty(GuillotinePacker.VerifyComprehensive(result, new[] { a, b }, Margins()));
    }

    [Fact]
    public void Oversized_parts_are_accounted_for_and_cancellation_is_observed()
    {
        var input = Sample();
        var oversized = new CutPiece { WidthMm = 9000, HeightMm = 9000, PieceLabel = "oversize" };
        input.Add(oversized);
        var r = GuillotinePacker.PackQuick(input, new[] { Stock() }, Margins(), RotationPolicy.Free90);
        Valid(r, input, Margins());
        Assert.Contains(oversized, r.Unplaced);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => GuillotinePacker.PackQuick(input,
            new[] { Stock() }, Margins(), RotationPolicy.Free90, cancellationToken: cts.Token));
    }
}
