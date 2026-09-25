using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

public sealed class FillFirstPlateRegressionTests
{
    // Synthetic fixture only. Not a customer cutting list.
    // One 800×700 pane plus four 580×380 panes: the shelf parks the large
    // pane alone, while fill-first can put the four smaller panes on sheet 1.
    private static List<CutPiece> ConcentrationJob(bool allowRotate)
    {
        var pieces = new List<CutPiece>
        {
            Pane(800, 700, "A", allowRotate)
        };
        for (int i = 0; i < 4; i++)
            pieces.Add(Pane(580, 380, "B" + i.ToString(), allowRotate));
        pieces.Add(Pane(380, 380, "C0", allowRotate));
        pieces.Add(Pane(380, 380, "C1", allowRotate));
        return pieces;
    }

    private static CutPiece Pane(int w, int h, string label, bool allowRotate) => new()
    {
        WidthMm = w,
        HeightMm = h,
        ThicknessMm = 6,
        PieceLabel = label,
        GlassRef = "clear",
        AllowRotate = allowRotate
    };

    private static StockSize[] Stock() => new[]
    {
        new StockSize { WidthMm = 1210, HeightMm = 810, ThicknessMm = 6, MaxSheets = 0 }
    };

    private static readonly MarginProfile Margins = new()
    {
        ThicknessMm = 6,
        LeftMm = 10,
        RightMm = 10,
        TopMm = 10,
        BottomMm = 10,
        KerfMm = 3,
        MinBreakoutMm = 15
    };

    private static long Leading(OptimizationResult result)
    {
        if (result.Sheets.Count == 0) return 0;
        return result.Sheets[0].Pieces.Sum(p => (long)p.WidthMm * p.HeightMm);
    }

    private static string Signature(OptimizationResult result)
    {
        var text = new StringBuilder();
        text.Append(result.Strategy).Append('|').Append(result.Sheets.Count)
            .Append('|').Append(result.Unplaced.Count).Append('|')
            .Append(string.Join("\n", result.ScaleNotes));
        foreach (var sheet in result.Sheets)
        {
            text.Append("|S").Append(sheet.SheetNo);
            foreach (var piece in sheet.Pieces
                         .OrderBy(p => p.Y).ThenBy(p => p.X).ThenBy(p => p.Piece.PieceLabel))
            {
                text.Append(':').Append(piece.Piece.PieceLabel)
                    .Append('@').Append(piece.X).Append(',').Append(piece.Y)
                    .Append(',').Append(piece.WidthMm).Append('x').Append(piece.HeightMm);
            }
        }
        return text.ToString();
    }

    [Fact]
    public void Fill_windows_widen_without_dropping_the_lower_stage()
    {
        int[] previous = Array.Empty<int>();
        for (int effort = 1; effort <= 5; effort++)
        {
            int[] windows = GuillotinePacker.FillFirstWindows(effort);
            Assert.Equal(effort, windows.Length);
            Assert.Equal(previous, windows.Take(previous.Length).ToArray());
            previous = windows;
        }
        Assert.Equal(new[] { 8, 12, 16, 24, 32 }, GuillotinePacker.FillFirstWindows(5));
    }

    [Fact]
    public void Yield_is_the_default_and_matches_the_current_plan()
    {
        var pieces = ConcentrationJob(allowRotate: true);
        var baseline = GuillotinePacker.PackQuick(pieces, Stock(), Margins, RotationPolicy.Free90);
        var yield = GuillotinePacker.PackQuick(pieces, Stock(), Margins, RotationPolicy.Free90,
            platePattern: PlatePattern.Yield);
        Assert.Equal(Signature(baseline), Signature(yield));
        Assert.DoesNotContain("Plate pattern:", string.Join(" ", yield.ScaleNotes));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fill_first_plate_is_legal_and_does_not_replace_yield_unless_selected(bool allowRotate)
    {
        var pieces = ConcentrationJob(allowRotate);
        var rotation = allowRotate ? RotationPolicy.Free90 : RotationPolicy.Locked;
        var yield = GuillotinePacker.PackQuick(pieces, Stock(), Margins, rotation);
        var fill = GuillotinePacker.PackQuick(pieces, Stock(), Margins, rotation,
            platePattern: PlatePattern.FillFirstPlate);

        Assert.Empty(GuillotinePacker.VerifyComprehensive(fill, pieces, Margins));
        Assert.Empty(GuillotinePacker.AuditBreakout(fill, Margins));
        Assert.True(fill.Unplaced.Count <= yield.Unplaced.Count);
        Assert.True(fill.Sheets.Count <= yield.Sheets.Count);
        if (fill.Sheets.Count == yield.Sheets.Count)
            Assert.True(Leading(fill) >= Leading(yield));
        Assert.Contains("Plate pattern: fill first plate", string.Join(" ", fill.ScaleNotes));
        // The default call is still the yield plan, even when fill is fuller.
        Assert.Equal(Signature(yield), Signature(
            GuillotinePacker.PackQuick(pieces, Stock(), Margins, rotation, platePattern: PlatePattern.Yield)));
    }

    [Fact]
    public void Fill_first_plate_concentrates_the_leading_sheet()
    {
        var pieces = ConcentrationJob(allowRotate: true);
        var fill = GuillotinePacker.PackFillFirstPlate(pieces, Stock(), Margins, RotationPolicy.Free90, 1);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(fill, pieces, Margins));
        Assert.Empty(fill.Unplaced);
        Assert.Equal(2, fill.Sheets.Count);
        Assert.True(Leading(fill) >= 4L * 580 * 380);
        Assert.StartsWith("FILL FIRST PLATE", fill.Strategy);
    }

    [Fact]
    public void Higher_fill_effort_keeps_the_lower_effort_plan()
    {
        var pieces = Enumerable.Range(0, 12).Select(i => Pane(
            400 + (i * 37 % 500),
            300 + (i * 53 % 400),
            "p" + i.ToString("00"),
            allowRotate: true)).ToList();
        var low = GuillotinePacker.PackFillFirstPlate(pieces, Stock(), Margins, RotationPolicy.Free90, 1);
        var high = GuillotinePacker.PackFillFirstPlate(pieces, Stock(), Margins, RotationPolicy.Free90, 5);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(low, pieces, Margins));
        Assert.Empty(GuillotinePacker.VerifyComprehensive(high, pieces, Margins));
        Assert.True(high.Unplaced.Count <= low.Unplaced.Count);
        if (high.Unplaced.Count == low.Unplaced.Count)
            Assert.True(high.Sheets.Count <= low.Sheets.Count);
        if (high.Unplaced.Count == low.Unplaced.Count && high.Sheets.Count == low.Sheets.Count)
            Assert.True(Leading(high) >= Leading(low));
    }
}
