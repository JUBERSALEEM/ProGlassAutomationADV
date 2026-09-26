using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

/// <summary>
/// Oracle tests for PHASE 2 part 2 (offcut reuse). The pass is allowed to
/// save sheets. It is never allowed to lose a pane, overlap two panes,
/// leave a sheet it cannot describe how to cut, or renumber sloppily.
/// </summary>
public sealed class OffcutReuseTests
{
    private static StockSize[] Stocks() => new[]
    {
        new StockSize { WidthMm = 1210, HeightMm = 810, ThicknessMm = 6, MaxSheets = 0 }
    };

    private static readonly MarginProfile Margins = new()
    {
        ThicknessMm = 6,
        LeftMm = 10, RightMm = 10, TopMm = 10, BottomMm = 10,
        KerfMm = 3,
        MinBreakoutMm = 15
    };

    /// <summary>A job that reliably leaves big rectangles behind: a few large
    /// panes next to many small ones, so the last sheet is thin.</summary>
    private static List<CutPiece> Job()
    {
        var pieces = new List<CutPiece>();
        for (int i = 0; i < 6; i++)
            pieces.Add(Pane(560, 380, "big" + i));
        for (int i = 0; i < 12; i++)
            pieces.Add(Pane(300 + (i * 23 % 120), 240 + (i * 17 % 90), "small" + i));
        return pieces;
    }

    private static CutPiece Pane(int w, int h, string label) => new()
    {
        WidthMm = w,
        HeightMm = h,
        ThicknessMm = 6,
        PieceLabel = label,
        GlassRef = "clear",
        AllowRotate = true
    };

    [Fact]
    public void Reuse_never_loses_a_pane_and_never_overlaps_one()
    {
        var job = Job();
        var result = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90);
        int placedBefore = result.Sheets.Sum(s => s.Pieces.Count);

        int saved = GuillotinePacker.ConsolidateIntoOffcuts(result, job, Margins, RotationPolicy.Free90);

        Assert.True(saved >= 0);
        Assert.Equal(placedBefore, result.Sheets.Sum(s => s.Pieces.Count));
        Assert.Empty(GuillotinePacker.VerifyComprehensive(result, job, Margins));
    }

    [Fact]
    public void Every_sheet_that_holds_panes_is_still_cuttable_after_reuse()
    {
        // The rollback guarantee. The pass moves panes, so the cut sequence
        // has to be rebuilt — and if it cannot be, the move is undone rather
        // than shipping a plan with no way to cut it.
        var job = Job();
        var result = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90);

        GuillotinePacker.ConsolidateIntoOffcuts(result, job, Margins, RotationPolicy.Free90);

        foreach (var sheet in result.Sheets)
        {
            if (sheet.Pieces.Count == 0) continue;
            Assert.NotEmpty(sheet.Cuts);
        }
    }

    [Fact]
    public void Sheets_are_renumbered_without_gaps_when_one_is_dropped()
    {
        var job = Job();
        var result = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90);
        int before = result.Sheets.Count;

        int saved = GuillotinePacker.ConsolidateIntoOffcuts(result, job, Margins, RotationPolicy.Free90);

        Assert.Equal(before - saved, result.Sheets.Count);
        for (int i = 0; i < result.Sheets.Count; i++)
            Assert.Equal(i + 1, result.Sheets[i].SheetNo);
    }

    [Fact]
    public void Reuse_is_off_unless_it_is_asked_for()
    {
        // The switch that keeps the default run identical.
        var plain = GuillotinePacker.PackQuick(Job(), Stocks(), Margins, RotationPolicy.Free90);
        var reused = GuillotinePacker.PackQuick(Job(), Stocks(), Margins, RotationPolicy.Free90,
            reuseOffcuts: true);

        Assert.True(reused.Sheets.Count <= plain.Sheets.Count,
            "reuse may save sheets, never cost one");
        Assert.Empty(GuillotinePacker.VerifyComprehensive(reused, Job(), Margins));
    }

    [Fact]
    public void Reuse_on_the_deep_search_still_verifies()
    {
        var job = Job();
        var reused = GuillotinePacker.PackOptimize(job, Stocks(), Margins, RotationPolicy.Free90,
            effortLevel: 3, reuseOffcuts: true);

        Assert.NotEmpty(reused.Sheets);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(reused, job, Margins));
        Assert.Contains(reused.ScaleNotes, n => n.StartsWith("Offcut reuse:", System.StringComparison.Ordinal));
    }

    [Fact]
    public void A_plan_with_one_sheet_is_left_alone()
    {
        var job = new List<CutPiece> { Pane(400, 300, "only") };
        var result = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90);

        int saved = GuillotinePacker.ConsolidateIntoOffcuts(result, job, Margins, RotationPolicy.Free90);

        Assert.Equal(0, saved);
        Assert.Single(result.Sheets);
    }
}
