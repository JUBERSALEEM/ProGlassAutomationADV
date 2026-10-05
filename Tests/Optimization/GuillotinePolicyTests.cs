using System;
using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

/// <summary>
/// Oracle tests for PHASE E4.1 — the guillotine feasibility policy.
///
/// This is the only item in E1-E4 that is allowed to change the plan, and
/// that is the point of it. Finding F1: RunMaxRects is documented as
/// non-guillotine, it runs by default at effort 3 and deeper, and nothing
/// checked the consequence — TryDeriveGuillotineCuts leaves a sheet with an
/// EMPTY cut list when no sequence of straight cuts reaches its panes, and the
/// call site discarded that result. So a plan could ship with pieces on it and
/// no way to cut them out.
///
/// These tests pin the two things the policy promises: that the detector
/// actually refuses such a layout, and that turning the policy on never
/// returns one. They deliberately do NOT claim the policy uses fewer sheets —
/// it usually uses the same and can use more, because an uncuttable plan that
/// saved a sheet is now refused. That is the trade, not a regression.
/// </summary>
public sealed class GuillotinePolicyTests
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

    /// <summary>
    /// The pinwheel: five panes tiling 300x300 with ZERO waste — the best
    /// number a utilisation metric can produce — and not one straight cut can
    /// start it. Every line that separates two panes runs through the interior
    /// of a third. This is the layout the policy exists to refuse.
    /// </summary>
    private static OptimizationResult Pinwheel()
    {
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
        return result;
    }

    private static List<CutPiece> Job(int count, int step)
    {
        var pieces = new List<CutPiece>();
        for (int i = 0; i < count; i++)
            pieces.Add(new CutPiece
            {
                WidthMm = 380 + (i * step % 430),
                HeightMm = 280 + (i * step % 320),
                ThicknessMm = 6,
                PieceLabel = "p" + i.ToString("00"),
                GlassRef = "clear",
                AllowRotate = true
            });
        return pieces;
    }

    private static IEnumerable<object[]> Battery()
    {
        // count, step, effort — deliberately varied so the pool has to work,
        // including sizes where MaxRects is most likely to win the comparison.
        yield return new object[] { 12, 47, 3 };
        yield return new object[] { 40, 47, 3 };
        yield return new object[] { 40, 91, 5 };
        yield return new object[] { 90, 53, 5 };
    }

    private static string Signature(OptimizationResult r) => string.Join("|",
        r.Sheets.Select(s => s.SheetNo + ":" + string.Join(",", s.Pieces
            .Select(p => p.X + "x" + p.Y + "x" + p.WidthMm + "x" + p.HeightMm))));

    // ── the detector ────────────────────────────────────────────────────

    [Fact]
    public void The_detector_refuses_a_layout_no_straight_cut_can_reach()
    {
        var pinwheel = Pinwheel();

        Assert.False(GuillotinePacker.AllSheetsCuttable(pinwheel));
    }

    [Fact]
    public void The_detector_accepts_layouts_that_are_cuttable()
    {
        // Two panes side by side: one vertical cut separates them. If the
        // detector refused ordinary work it would refuse everything.
        var result = new OptimizationResult();
        var sheet = new SheetLayout
        {
            SheetNo = 1,
            Stock = Stocks()[0],
            UsableX = 0, UsableY = 0, UsableWidth = 300, UsableHeight = 300
        };
        AddPane(sheet, 0, 0, 150, 300);
        AddPane(sheet, 150, 0, 150, 300);
        result.Sheets.Add(sheet);

        Assert.True(GuillotinePacker.AllSheetsCuttable(result));
    }

    [Fact]
    public void Empty_sheets_are_trivially_admissible()
    {
        // A sheet with nothing on it has no cut sequence and needs none. A
        // detector that called that a failure would reject every plan.
        var result = new OptimizationResult();
        result.Sheets.Add(new SheetLayout { SheetNo = 1, Stock = Stocks()[0] });

        Assert.True(GuillotinePacker.AllSheetsCuttable(result));
    }

    [Fact]
    public void The_detector_agrees_with_derivation_on_every_generator_output()
    {
        // The contract is "no sheet is left uncuttable". Pinned here against
        // the generator whose output the policy exists to police, so the
        // helper and the raw derivation can never drift apart.
        for (int step = 31; step <= 97; step += 33)
        {
            var result = GuillotinePacker.RunMaxRects(Job(40, step), Stocks(), Margins,
                RotationPolicy.Free90);

            bool expected = result.Sheets.All(s =>
                s.Pieces.Count == 0
                || s.Cuts.Count > 0
                || GuillotinePacker.TryDeriveGuillotineCuts(s));

            Assert.Equal(expected, GuillotinePacker.AllSheetsCuttable(result));
        }
    }

    // ── the policy ──────────────────────────────────────────────────────

    [Fact]
    public void The_policy_is_off_unless_asked_for()
    {
        var plain = GuillotinePacker.PackOptimize(Job(40, 47), Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: 3);
        var explicitOff = GuillotinePacker.PackOptimize(Job(40, 47), Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: 3, requireGuillotine: false);

        Assert.Equal(Signature(plain), Signature(explicitOff));
    }

    [Fact]
    public void The_policy_is_never_mentioned_when_it_was_not_asked_for()
    {
        // A note the operator did not ask for is noise, and noise on the
        // default path is how people stop reading the notes that matter.
        var plain = GuillotinePacker.PackOptimize(Job(40, 47), Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: 5);

        Assert.DoesNotContain(plain.ScaleNotes,
            n => n.Contains("GUILLOTINE POLICY", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Battery))]
    public void With_the_policy_on_no_sheet_ever_ships_without_a_cut_sequence(int count, int step, int effort)
    {
        // The promise. Asserted against the raw derivation rather than
        // AllSheetsCuttable, so the test cannot pass by agreeing with itself.
        var result = GuillotinePacker.PackOptimize(Job(count, step), Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: effort, requireGuillotine: true);

        foreach (var sheet in result.Sheets)
        {
            if (sheet.Pieces.Count == 0) continue;
            Assert.True(sheet.Cuts.Count > 0 || GuillotinePacker.TryDeriveGuillotineCuts(sheet),
                "sheet " + sheet.SheetNo + " holds panes but no straight cut reaches them");
        }

        Assert.Equal(0, OptimizationInstrumentation.AuditCutReachability(result));
    }

    [Theory]
    [MemberData(nameof(Battery))]
    public void The_policy_never_loses_a_pane(int count, int step, int effort)
    {
        // Refusing a plan must not smuggle a pane out of the job. The fallback
        // is the PackQuick seed, which places everything that fits.
        var job = Job(count, step);
        var plain = GuillotinePacker.PackOptimize(job, Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: effort);
        var policy = GuillotinePacker.PackOptimize(Job(count, step), Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: effort, requireGuillotine: true);

        Assert.Equal(plain.Sheets.Sum(s => s.Pieces.Count) + plain.Unplaced.Count,
                     policy.Sheets.Sum(s => s.Pieces.Count) + policy.Unplaced.Count);
    }

    [Theory]
    [MemberData(nameof(Battery))]
    public void The_policy_is_deterministic(int count, int step, int effort)
    {
        var a = GuillotinePacker.PackOptimize(Job(count, step), Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: effort, requireGuillotine: true);
        var b = GuillotinePacker.PackOptimize(Job(count, step), Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: effort, requireGuillotine: true);

        Assert.Equal(Signature(a), Signature(b));
    }
}
