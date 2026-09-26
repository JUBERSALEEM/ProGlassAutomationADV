using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

/// <summary>
/// Oracle tests for PHASE E1 (instrumentation). Two claims: the numbers are
/// TRUE, and measuring a plan does not change it.
/// </summary>
public sealed class InstrumentationTests
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
                WidthMm = 380 + (i * 47 % 430),
                HeightMm = 280 + (i * 61 % 320),
                ThicknessMm = 6,
                PieceLabel = "p" + i.ToString("00"),
                GlassRef = "clear",
                AllowRotate = true
            });
        return pieces;
    }

    private static string Signature(OptimizationResult r) => string.Join("|",
        r.Sheets.Select(s => s.SheetNo + ":" + string.Join(",", s.Pieces
            .Select(p => p.X + "x" + p.Y + "x" + p.WidthMm + "x" + p.HeightMm))));

    // ── E1.2 the bound ──────────────────────────────────────────────────

    [Theory]
    [InlineData(5)]
    [InlineData(25)]
    [InlineData(60)]
    public void The_bound_is_never_higher_than_what_the_plan_actually_used(int count)
    {
        // The property that makes a bound worth anything. A bound ABOVE the
        // real sheet count is not a bound, it is a lie that claims headroom
        // which does not exist.
        var job = Job(count);
        var result = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90);

        Assert.True(result.SheetsLowerBound > 0, "a job with glass to cut needs at least one plate");
        Assert.True(result.SheetsLowerBound <= result.SheetsUsed,
            "the bound claimed more sheets than the plan used, so it is not a lower bound");
        Assert.True(result.OptimalityGapSheets >= 0);
    }

    [Fact]
    public void The_bound_holds_on_the_deep_search_too()
    {
        var job = Job(45);
        var result = GuillotinePacker.PackOptimize(job, Stocks(), Margins, RotationPolicy.Free90, effortLevel: 4);

        Assert.True(result.SheetsLowerBound <= result.SheetsUsed);
        Assert.True(result.OptimalityGapPercent >= 0);
    }

    [Fact]
    public void A_job_that_fits_on_one_plate_is_bounded_at_one()
    {
        var job = new List<CutPiece>
        {
            new CutPiece { WidthMm = 300, HeightMm = 300, ThicknessMm = 6, PieceLabel = "a", GlassRef = "clear" }
        };
        var result = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90);

        Assert.Equal(1, result.SheetsUsed);
        Assert.Equal(1, result.SheetsLowerBound);
        Assert.Equal(0, result.OptimalityGapSheets);
    }

    [Fact]
    public void Panes_too_large_for_any_plate_do_not_inflate_the_bound()
    {
        // A bound that counted a pane which cannot be cut anywhere would claim
        // headroom that no engine could ever recover.
        var job = new List<CutPiece>
        {
            new CutPiece { WidthMm = 300, HeightMm = 300, ThicknessMm = 6, PieceLabel = "fits", GlassRef = "clear" },
            new CutPiece { WidthMm = 9000, HeightMm = 9000, ThicknessMm = 6, PieceLabel = "huge", GlassRef = "clear" }
        };
        var result = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90);

        Assert.Single(result.Unplaced);          // the huge one is left out
        Assert.Equal(1, result.SheetsUsed);
        Assert.Equal(1, result.SheetsLowerBound); // and ignored by the bound
    }

    // ── E1.1 cut reachability ───────────────────────────────────────────

    [Fact]
    public void Audit_flags_a_layout_no_guillotine_table_can_produce()
    {
        // The pinwheel: five panes that tile the usable area PERFECTLY. Zero
        // waste — the best number a utilisation metric can produce — and not
        // one straight cut can start it. Every vertical and horizontal line
        // that separates panes runs through the interior of another pane.
        // A yield-only engine would be delighted by this plan.
        var result = new OptimizationResult();
        var sheet = new SheetLayout
        {
            SheetNo = 1,
            Stock = Stocks()[0],
            UsableX = 0,
            UsableY = 0,
            UsableWidth = 300,
            UsableHeight = 300
        };
        AddPane(sheet, 0, 0, 100, 200);      // left, tall
        AddPane(sheet, 100, 0, 200, 100);    // bottom right, wide
        AddPane(sheet, 200, 100, 100, 200);  // right, tall
        AddPane(sheet, 0, 200, 200, 100);    // top left, wide
        AddPane(sheet, 100, 100, 100, 100);  // centre
        result.Sheets.Add(sheet);

        // Derivation is exactly what the engine attempts for such a sheet, and
        // it must fail — that failure is what makes the sheet uncuttable.
        Assert.False(GuillotinePacker.TryDeriveGuillotineCuts(sheet));
        Assert.Empty(sheet.Cuts);

        int uncuttable = OptimizationInstrumentation.AuditCutReachability(result);

        Assert.Equal(1, uncuttable);
        Assert.Equal(1, result.UncuttableSheetCount);
        Assert.Contains(result.ScaleNotes, n => n.Contains("CUT REACHABILITY", StringComparison.Ordinal));
    }

    [Fact]
    public void A_healthy_plan_reports_nothing_uncuttable()
    {
        var job = Job(30);
        var result = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90);

        Assert.Equal(0, result.UncuttableSheetCount);
        Assert.DoesNotContain(result.ScaleNotes, n => n.Contains("CUT REACHABILITY", StringComparison.Ordinal));
    }

    // ── measurement must not mutate ─────────────────────────────────────

    [Fact]
    public void Measuring_a_plan_leaves_the_plan_alone()
    {
        var job = Job(35);
        var result = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90);
        string before = Signature(result);

        OptimizationInstrumentation.Instrument(result, Stocks(), Margins, RotationPolicy.Free90);
        OptimizationInstrumentation.Instrument(result, Stocks(), Margins, RotationPolicy.Free90);

        Assert.Equal(before, Signature(result));
    }

    private static void AddPane(SheetLayout sheet, int x, int y, int w, int h)
    {
        sheet.Pieces.Add(new PlacedPiece
        {
            Piece = new CutPiece
            {
                WidthMm = w,
                HeightMm = h,
                ThicknessMm = 6,
                PieceLabel = x + "_" + y,
                GlassRef = "clear"
            },
            X = x,
            Y = y,
            WidthMm = w,
            HeightMm = h
        });
    }
}
