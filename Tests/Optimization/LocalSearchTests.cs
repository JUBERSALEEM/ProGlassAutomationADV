using System;
using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

/// <summary>
/// Oracle tests for PHASE E2 (placement-level local search). The pass may save
/// sheets. It may never lose a pane, overlap two, break kerf, leave a sheet it
/// cannot describe how to cut, or renumber sloppily.
///
/// The kerf assertions are the ones that caught the bug in review: deriving
/// leftovers with kerf 0 measures a rectangle from the cut line instead of
/// from after the blade, so a pane placed by that measurement sits closer than
/// a kerf to its neighbour. VerifyComprehensive is what catches it.
/// </summary>
public sealed class LocalSearchTests
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

    /// <summary>A job that reliably leaves large rectangles behind.</summary>
    private static List<CutPiece> Job()
    {
        var pieces = new List<CutPiece>();
        for (int i = 0; i < 6; i++) pieces.Add(Pane(560, 380, "big" + i));
        for (int i = 0; i < 12; i++) pieces.Add(Pane(300 + (i * 23 % 120), 240 + (i * 17 % 90), "small" + i));
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

    private static string Signature(OptimizationResult r) => string.Join("|",
        r.Sheets.Select(s => string.Join(",", s.Pieces
            .Select(p => p.X + "x" + p.Y + "x" + p.WidthMm + "x" + p.HeightMm))));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Local_search_never_costs_a_sheet_or_a_pane(bool swaps)
    {
        var job = Job();
        var before = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90);
        var after = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90,
            localSearch: true, enableSwaps: swaps);

        Assert.True(after.SheetsUsed <= before.SheetsUsed,
            "local search returned more sheets than the plan it was given");
        Assert.Equal(before.Sheets.Sum(s => s.Pieces.Count), after.Sheets.Sum(s => s.Pieces.Count));
        Assert.Equal(before.Unplaced.Count, after.Unplaced.Count);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(after, job, Margins));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_sheet_stays_cuttable_after_local_search(bool swaps)
    {
        var job = Job();
        var result = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90,
            localSearch: true, enableSwaps: swaps);

        foreach (var sheet in result.Sheets)
        {
            if (sheet.Pieces.Count == 0) continue;
            Assert.NotEmpty(sheet.Cuts);
        }

        Assert.Equal(0, result.UncuttableSheetCount);
    }

    [Fact]
    public void Sheet_numbers_stay_contiguous_when_one_is_dropped()
    {
        var job = Job();
        var result = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90,
            localSearch: true);

        for (int i = 0; i < result.Sheets.Count; i++)
            Assert.Equal(i + 1, result.Sheets[i].SheetNo);
    }

    [Fact]
    public void Local_search_is_off_unless_it_is_asked_for()
    {
        // The switch that keeps the default run identical.
        var plain = GuillotinePacker.PackQuick(Job(), Stocks(), Margins, RotationPolicy.Free90);

        Assert.DoesNotContain(plain.ScaleNotes, n => n.StartsWith("Local search:", StringComparison.Ordinal));
        Assert.Equal(Signature(plain),
            Signature(GuillotinePacker.PackQuick(Job(), Stocks(), Margins, RotationPolicy.Free90,
                localSearch: false)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Local_search_is_deterministic(bool swaps)
    {
        // Without this, a sheet saved on one run and not the next is worse
        // than never saving one.
        var first = GuillotinePacker.PackQuick(Job(), Stocks(), Margins, RotationPolicy.Free90,
            localSearch: true, enableSwaps: swaps);
        var second = GuillotinePacker.PackQuick(Job(), Stocks(), Margins, RotationPolicy.Free90,
            localSearch: true, enableSwaps: swaps);

        Assert.Equal(Signature(first), Signature(second));
        Assert.Equal(first.SheetsUsed, second.SheetsUsed);
    }

    [Fact]
    public void Local_search_reports_what_it_did()
    {
        var result = GuillotinePacker.PackQuick(Job(), Stocks(), Margins, RotationPolicy.Free90,
            localSearch: true);

        Assert.Contains(result.ScaleNotes, n => n.StartsWith("Local search:", StringComparison.Ordinal));
    }

    [Fact]
    public void A_rolled_back_move_leaves_no_rectangles_behind()
    {
        // The staleness bug: a move that is undone must not leave its
        // rectangles in the leftovers, or the report and the DXF export draw
        // space that a pane is standing on.
        var job = Job();
        var result = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90,
            localSearch: true);

        // Leftovers are re-derived at the end of the pass, so every one of
        // them must be empty of panes. This is the invariant the rollback
        // clearing protects.
        foreach (var sheet in result.Sheets)
            foreach (var region in sheet.Leftovers)
                foreach (var piece in sheet.Pieces)
                    Assert.False(
                        piece.X >= region.X && piece.Y >= region.Y
                        && piece.X + piece.WidthMm <= region.X + region.WidthMm + 0.5
                        && piece.Y + piece.HeightMm <= region.Y + region.HeightMm + 0.5,
                        "a leftover rectangle contains a pane, so the rollback left stale space");
    }

    // ── E2.2 remainder-aware split choice ───────────────────────────────

    [Fact]
    public void Remainder_aware_off_is_the_comparison_the_loop_always_made()
    {
        // The whole safety argument for E2.2 in one assertion: with the switch
        // off the remainder term is 0 for every candidate, so the key collapses
        // to (area, count, -index) - the chain this loop used before. Default
        // and explicit-false must therefore be the same plan, not a similar one.
        var a = GuillotinePacker.RunBestSplit(Job(), Stocks(), Margins, RotationPolicy.Free90);
        var b = GuillotinePacker.RunBestSplit(Job(), Stocks(), Margins, RotationPolicy.Free90,
            remainderAware: false);

        Assert.Equal(Signature(a), Signature(b));
        Assert.Equal(a.SheetsUsed, b.SheetsUsed);
    }

    [Fact]
    public void Remainder_aware_on_is_deterministic_and_verifies()
    {
        // It is NOT asserted to be better. Whether preferring the split that
        // leaves the largest rectangle actually helps is a measurement, not a
        // claim - the census is how it gets measured. What must hold is that it
        // stays a valid plan and returns to it.
        var job = Job();
        var a = GuillotinePacker.RunBestSplit(job, Stocks(), Margins, RotationPolicy.Free90,
            remainderAware: true);
        var b = GuillotinePacker.RunBestSplit(Job(), Stocks(), Margins, RotationPolicy.Free90,
            remainderAware: true);

        Assert.Equal(Signature(a), Signature(b));
        Assert.Empty(GuillotinePacker.VerifyComprehensive(a, job, Margins));
    }

    [Fact]
    public void Remainder_aware_keeps_every_plate_cuttable()
    {
        var job = Job();
        var result = GuillotinePacker.RunBestSplit(job, Stocks(), Margins, RotationPolicy.Free90,
            remainderAware: true);

        foreach (var sheet in result.Sheets)
            if (sheet.Pieces.Count > 0)
                Assert.NotEmpty(sheet.Cuts);
    }

    [Fact]
    public void Local_search_on_a_single_sheet_does_nothing()
    {
        var job = new List<CutPiece> { Pane(400, 300, "only") };
        var result = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90,
            localSearch: true, enableSwaps: true);

        Assert.Single(result.Sheets);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(result, job, Margins));
    }
}
