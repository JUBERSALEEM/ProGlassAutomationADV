using System;
using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

/// <summary>
/// Oracle tests for the cut-sequence audit (E4.2).
///
/// The engine records a cut list for every sheet and exports it to the table.
/// Nothing checked that the list, replayed, actually produces the panes the
/// plan says it does. A sheet can sail through VerifyComprehensive — pieces
/// in bounds, no overlaps, kerf respected — while carrying a cut list that
/// leaves two panes fused together or runs a blade straight through a third.
/// The screen and the CNC would disagree, and nobody would know until glass
/// was ruined.
///
/// The audit reports; it does not reject. VerifyComprehensive is an acceptance
/// predicate in more than one place, so a check this strict is deliberately
/// kept out of it until there is a decision to make it one.
/// </summary>
public sealed class CutSequenceAuditTests
{
    private const int Plate = 300;      // usable square, 0,0 origin
    private const int Kerf = 3;

    private static StockSize[] Stocks() => new[]
    {
        new StockSize { WidthMm = 1210, HeightMm = 810, ThicknessMm = 6, MaxSheets = 0 }
    };

    private static readonly MarginProfile Margins = new()
    {
        ThicknessMm = 6,
        LeftMm = 10, RightMm = 10, TopMm = 10, BottomMm = 10,
        KerfMm = Kerf,
        MinBreakoutMm = 15
    };

    private static SheetLayout Sheet()
    {
        return new SheetLayout
        {
            SheetNo = 1,
            Stock = Stocks()[0],
            UsableX = 0,
            UsableY = 0,
            UsableWidth = Plate,
            UsableHeight = Plate
        };
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

    /// <summary>A vertical blade at <paramref name="atMm"/> spanning the
    /// whole plate, which is the shape the packer emits.</summary>
    private static CutStep VerticalCut(int atMm) => new CutStep
    {
        Seq = 1,
        Axis = CutAxis.Vertical,
        AtMm = atMm,
        FromX = 0, FromY = 0, FromW = Plate, FromH = Plate,
        Stage = 1
    };

    private static OptimizationResult PlanOf(SheetLayout sheet)
    {
        var result = new OptimizationResult();
        result.Sheets.Add(sheet);
        return result;
    }

    // ── the three cases that matter ─────────────────────────────────────

    [Fact]
    public void A_sequence_that_separates_every_pane_is_accepted()
    {
        // Two panes side by side, one blade between them. Replay leaves each
        // in a cell of its own. If this failed, the audit would be useless —
        // it would reject ordinary work.
        var sheet = Sheet();
        AddPane(sheet, 0, 0, 150, 300);
        AddPane(sheet, 150, 0, 150, 300);
        sheet.Cuts.Add(VerticalCut(150));

        var result = PlanOf(sheet);

        Assert.Equal(0, GuillotinePacker.AuditCutConsistency(result, Kerf));
        Assert.Equal(0, result.InconsistentCutSheetCount);
    }

    [Fact]
    public void A_sequence_that_leaves_two_panes_fused_is_caught()
    {
        // Two panes with no blade between them. The single recorded cut is on
        // the far side of both, so replay leaves them sharing one cell — they
        // are never separated from each other and the plan cannot be produced
        // as drawn.
        var sheet = Sheet();
        AddPane(sheet, 0, 0, 100, 300);
        AddPane(sheet, 100, 0, 100, 300);
        sheet.Cuts.Add(VerticalCut(200));

        var result = PlanOf(sheet);

        Assert.Equal(1, GuillotinePacker.AuditCutConsistency(result, Kerf));
        Assert.Contains(result.ScaleNotes, n => n.Contains("CUT SEQUENCE", StringComparison.Ordinal));
    }

    [Fact]
    public void A_blade_running_through_a_pane_is_caught()
    {
        // One pane straddling the blade. Replay puts it in no cell at all,
        // because the cut line crosses it. This is the case that ruins glass.
        var sheet = Sheet();
        AddPane(sheet, 0, 0, 200, 300);
        sheet.Cuts.Add(VerticalCut(100));

        var result = PlanOf(sheet);

        Assert.Equal(1, GuillotinePacker.AuditCutConsistency(result, Kerf));
    }

    // ── scope of the check ──────────────────────────────────────────────

    [Fact]
    public void A_sheet_with_no_sequence_at_all_is_left_to_the_reachability_audit()
    {
        // E1.1 already reports "no cut sequence could be derived". Counting
        // those here too would double-report every MaxRects sheet and make
        // neither number readable.
        var sheet = Sheet();
        AddPane(sheet, 0, 0, 100, 100);
        var result = PlanOf(sheet);

        Assert.Equal(0, GuillotinePacker.AuditCutConsistency(result, Kerf));
    }

    [Fact]
    public void An_empty_sheet_is_ignored()
    {
        var result = PlanOf(Sheet());

        Assert.Equal(0, GuillotinePacker.AuditCutConsistency(result, Kerf));
    }

    // ── wiring ──────────────────────────────────────────────────────────

    [Fact]
    public void The_audit_runs_on_every_finished_plan_and_is_idempotent()
    {
        // Proves the plumbing without asserting what the number should be.
        // Whether real plans score zero is a measurement for the first build,
        // not something to guess at here — and a wrong guess baked into an
        // assertion is a test that fails for the wrong reason.
        var job = new List<CutPiece>();
        for (int i = 0; i < 25; i++)
            job.Add(new CutPiece
            {
                WidthMm = 380 + (i * 47 % 430),
                HeightMm = 280 + (i * 61 % 320),
                ThicknessMm = 6,
                PieceLabel = "p" + i.ToString("00"),
                GlassRef = "clear",
                AllowRotate = true
            });

        var result = GuillotinePacker.PackQuick(job, Stocks(), Margins, RotationPolicy.Free90);

        // PackQuick instruments its own result, so the count is already there.
        // Re-running must reproduce it exactly, not add to it.
        int again = GuillotinePacker.AuditCutConsistency(result, Margins.KerfMm);

        Assert.Equal(again, result.InconsistentCutSheetCount);
        Assert.True(again >= 0);
    }
}
