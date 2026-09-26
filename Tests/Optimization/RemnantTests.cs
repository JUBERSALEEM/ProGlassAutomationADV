using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

/// <summary>
/// Oracle tests for PHASE 2 part 1 (remnant registration). Two claims:
/// the rectangles are real — they account for the usable area the pieces do
/// not use — and reading them changes nothing about the plan.
/// </summary>
public sealed class RemnantTests
{
    private static StockSize[] Stock() => new[]
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

    private static List<CutPiece> Job(int count)
    {
        var pieces = new List<CutPiece>();
        for (int i = 0; i < count; i++)
            pieces.Add(new CutPiece
            {
                WidthMm = 400 + (i * 37 % 500),
                HeightMm = 300 + (i * 53 % 380),
                ThicknessMm = 6,
                PieceLabel = "p" + i.ToString("00"),
                GlassRef = "clear",
                AllowRotate = true
            });
        return pieces;
    }

    [Fact]
    public void Leftovers_are_derived_at_the_end_of_a_run()
    {
        var result = GuillotinePacker.PackQuick(Job(14), Stock(), Margins, RotationPolicy.Free90);

        Assert.NotEmpty(result.Sheets);
        Assert.True(result.Sheets.Sum(s => s.Leftovers.Count) > 0,
            "a plan that wastes glass must say where the waste is");
    }

    [Fact]
    public void Leftovers_and_pieces_account_for_the_whole_usable_area()
    {
        // The conservation oracle. Every millimetre of the usable rectangle is
        // either a pane, a leftover, or the kerf a cut consumed — nothing may
        // be invented and nothing may vanish.
        var result = GuillotinePacker.PackQuick(Job(14), Stock(), Margins, RotationPolicy.Free90);

        foreach (var sheet in result.Sheets)
        {
            double usable = sheet.UsableWidth * sheet.UsableHeight;
            double used = sheet.Pieces.Sum(p => p.WidthMm * p.HeightMm);
            double left = sheet.Leftovers.Sum(r => r.WidthMm * r.HeightMm);

            // Kerf is the only sanctioned loss, bounded by the length of the
            // cuts actually made.
            double kerfLoss = sheet.Cuts.Sum(c =>
                (c.Axis == CutAxis.Vertical ? c.FromH : c.FromW) * Margins.KerfMm);

            Assert.True(used + left <= usable + 1.0,
                "leftovers claim more area than the sheet has");
            Assert.True(used + left >= usable - kerfLoss - 1.0,
                "area went missing: it is neither a piece, a leftover, nor kerf");
        }
    }

    [Fact]
    public void No_leftover_swallows_a_piece()
    {
        // A leftover is what is left AFTER the panes come off. If one contains
        // a pane, the replay of the cut sequence is wrong.
        var result = GuillotinePacker.PackQuick(Job(14), Stock(), Margins, RotationPolicy.Free90);

        foreach (var sheet in result.Sheets)
            foreach (var remnant in sheet.Leftovers)
                foreach (var piece in sheet.Pieces)
                    Assert.False(
                        piece.X >= remnant.X && piece.Y >= remnant.Y
                        && piece.X + piece.WidthMm <= remnant.X + remnant.WidthMm
                        && piece.Y + piece.HeightMm <= remnant.Y + remnant.HeightMm,
                        "a leftover rectangle contains a placed pane");
    }

    [Fact]
    public void Usable_means_both_sides_clear_the_minimum()
    {
        var result = GuillotinePacker.PackQuick(Job(14), Stock(), Margins, RotationPolicy.Free90);

        foreach (var sheet in result.Sheets)
            foreach (var remnant in sheet.Leftovers)
                Assert.Equal(remnant.MinSideMm >= Margins.MinBreakoutMm, remnant.IsUsable);
    }

    [Fact]
    public void Usable_remnants_are_offered_biggest_first_and_waste_is_not()
    {
        var result = GuillotinePacker.PackQuick(Job(14), Stock(), Margins, RotationPolicy.Free90);

        var offered = GuillotinePacker.UsableRemnants(result);

        foreach (var r in offered)
            Assert.True(r.IsUsable);

        for (int i = 1; i < offered.Count; i++)
            Assert.True(offered[i - 1].AreaM2 >= offered[i].AreaM2,
                "the most useful offcut should be first on the list");
    }

    [Fact]
    public void Deriving_leftovers_twice_does_not_double_count()
    {
        // The report and the export can both ask. Idempotent by construction.
        var result = GuillotinePacker.PackQuick(Job(14), Stock(), Margins, RotationPolicy.Free90);
        int first = result.Sheets.Sum(s => s.Leftovers.Count);

        GuillotinePacker.DeriveLeftovers(result, Margins.MinBreakoutMm, Margins.KerfMm);

        Assert.Equal(first, result.Sheets.Sum(s => s.Leftovers.Count));
    }

    [Fact]
    public void Reading_leftovers_does_not_move_a_piece()
    {
        // The promise that makes this safe: derivation reads the plan, so the
        // benchmark is untouched. Same input, same placements, with and
        // without a second derivation.
        var pieces = Job(14);
        var before = GuillotinePacker.PackQuick(pieces, Stock(), Margins, RotationPolicy.Free90);
        var signature = string.Join("|", before.Sheets.SelectMany(s => s.Pieces)
            .Select(p => p.X.ToString("0.##") + "," + p.Y.ToString("0.##")
                       + "," + p.WidthMm.ToString("0.##") + "," + p.HeightMm.ToString("0.##")));

        var after = GuillotinePacker.PackQuick(Job(14), Stock(), Margins, RotationPolicy.Free90);
        GuillotinePacker.DeriveLeftovers(after, Margins.MinBreakoutMm, Margins.KerfMm);

        var again = string.Join("|", after.Sheets.SelectMany(s => s.Pieces)
            .Select(p => p.X.ToString("0.##") + "," + p.Y.ToString("0.##")
                       + "," + p.WidthMm.ToString("0.##") + "," + p.HeightMm.ToString("0.##")));

        Assert.Equal(signature, again);
    }
}
