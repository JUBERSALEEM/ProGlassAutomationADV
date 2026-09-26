using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.ViewModels.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

/// <summary>
/// Oracle tests for PHASE 5a: search family (WHICH engines run) and effort
/// (HOW HARD they search) are separate axes. The first test is the one that
/// matters — splitting them must not move the default run.
/// </summary>
public sealed class StrategyEffortTests
{
    private static StockSize[] Stocks() => new[]
    {
        new StockSize { WidthMm = 1210, HeightMm = 810, ThicknessMm = 6, MaxSheets = 0 },
        new StockSize { WidthMm = 1000, HeightMm = 700, ThicknessMm = 6, MaxSheets = 0 }
    };

    private static readonly MarginProfile Margins = new()
    {
        ThicknessMm = 6,
        LeftMm = 10, RightMm = 10, TopMm = 10, BottomMm = 10,
        KerfMm = 3,
        MinBreakoutMm = 15
    };

    private static List<CutPiece> Job(int count)
    {
        var pieces = new List<CutPiece>();
        for (int i = 0; i < count; i++)
            pieces.Add(new CutPiece
            {
                WidthMm = 380 + (i * 47 % 460),
                HeightMm = 290 + (i * 61 % 340),
                ThicknessMm = 6,
                PieceLabel = "p" + i.ToString("00"),
                GlassRef = "clear",
                AllowRotate = true
            });
        return pieces;
    }

    private static string Signature(OptimizationResult r) => string.Join("|",
        r.Sheets.Select(s => s.SheetNo + ":" + s.Pieces.Count + ":" + s.Stock.WidthMm)
            .Concat(new[] { "u" + r.Unplaced.Count }));

    [Fact]
    public void Splitting_the_axes_does_not_move_the_default_run()
    {
        // The promise: maxStage = 0 (the default every existing caller uses)
        // means "effort decides both", so stage == effort and every gate in
        // PackOptimize evaluates exactly as it did before the split.
        var pieces = Job(40);

        var plain = GuillotinePacker.PackOptimize(pieces, Stocks(), Margins, RotationPolicy.Free90, 3);
        var uncapped = GuillotinePacker.PackOptimize(pieces, Stocks(), Margins, RotationPolicy.Free90, 3, maxStage: 0);
        var asWas = GuillotinePacker.PackOptimize(pieces, Stocks(), Margins, RotationPolicy.Free90, 3, maxStage: 3);

        Assert.Equal(Signature(plain), Signature(uncapped));
        Assert.Equal(Signature(plain), Signature(asWas));
    }

    [Fact]
    public void Stage_caps_the_portfolio_whatever_the_effort()
    {
        // Stage 1 is the quick portfolio. Effort 5 used to mean stage 5 as
        // well; now it means "search the quick portfolio very hard", which is
        // the same set of engines effort 1 ran.
        var pieces = Job(40);

        var quickFamily = GuillotinePacker.PackOptimize(pieces, Stocks(), Margins, RotationPolicy.Free90,
            effortLevel: 5, maxStage: 1);
        var effortOne = GuillotinePacker.PackOptimize(pieces, Stocks(), Margins, RotationPolicy.Free90,
            effortLevel: 1);

        Assert.Equal(Signature(effortOne), Signature(quickFamily));
    }

    [Fact]
    public void Effort_still_searches_harder_inside_one_stage()
    {
        // The other half of the split: with the portfolio pinned, more effort
        // must still be able to find a better plan. It adds candidates and
        // Better() takes the minimum, so it can never come out worse.
        var pieces = Job(40);

        var shallow = GuillotinePacker.PackOptimize(pieces, Stocks(), Margins, RotationPolicy.Free90,
            effortLevel: 2, maxStage: 3);
        var deep = GuillotinePacker.PackOptimize(pieces, Stocks(), Margins, RotationPolicy.Free90,
            effortLevel: 5, maxStage: 3);

        Assert.True(deep.SheetsUsed <= shallow.SheetsUsed,
            "deeper search inside the same family must not need more sheets");
        Assert.True(deep.Unplaced.Count <= shallow.Unplaced.Count);
    }

    [Fact]
    public void A_wider_family_can_only_add_candidates()
    {
        var pieces = Job(40);

        var narrow = GuillotinePacker.PackOptimize(pieces, Stocks(), Margins, RotationPolicy.Free90,
            effortLevel: 4, maxStage: 3);
        var wide = GuillotinePacker.PackOptimize(pieces, Stocks(), Margins, RotationPolicy.Free90,
            effortLevel: 4, maxStage: 5);

        Assert.True(wide.SheetsUsed <= narrow.SheetsUsed);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(wide, pieces, Margins));
    }

    [Fact]
    public void Sessions_saved_before_the_split_restore_to_the_family_their_effort_implied()
    {
        // Migration: an old session has no family, and back then the family
        // WAS the effort level. Restoring it that way makes the session run as
        // it did when it was saved. Out-of-range falls back to stage 3.
        Assert.Equal(OptimizationViewModel.Stage1StrategyLabel, OptimizationViewModel.LabelForStage(1));
        Assert.Equal(OptimizationViewModel.Stage2StrategyLabel, OptimizationViewModel.LabelForStage(2));
        Assert.Equal(OptimizationViewModel.Stage3StrategyLabel, OptimizationViewModel.LabelForStage(3));
        Assert.Equal(OptimizationViewModel.Stage4StrategyLabel, OptimizationViewModel.LabelForStage(4));
        Assert.Equal(OptimizationViewModel.Stage5StrategyLabel, OptimizationViewModel.LabelForStage(5));
        Assert.Equal(OptimizationViewModel.Stage3StrategyLabel, OptimizationViewModel.LabelForStage(0));
        Assert.Equal(OptimizationViewModel.Stage3StrategyLabel, OptimizationViewModel.LabelForStage(9));
    }
}
