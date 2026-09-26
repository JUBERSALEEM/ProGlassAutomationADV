using System;
using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

/// <summary>
/// Oracle tests for PHASE 0 (cut-sequence capture) and PHASE 1
/// (cut-complexity patterns).
///
/// The load-bearing claim these protect: CutPolicy.Free — the default — must
/// produce exactly the plan the engine produced before cut capture existed.
/// Everything else in the phase is additive, so the first test here is a
/// regression lock on that identity, not a behavioural assertion.
/// </summary>
public sealed class CutPatternTests
{
    private static CutPiece Pane(int w, int h, string label, bool allowRotate = true) => new()
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

    /// <summary>A job varied enough to need several strips per sheet.</summary>
    private static List<CutPiece> Job()
    {
        var pieces = new List<CutPiece>();
        for (int i = 0; i < 14; i++)
            pieces.Add(Pane(400 + (i * 37 % 500), 300 + (i * 53 % 380), "p" + i.ToString("00")));
        return pieces;
    }

    private static string Signature(OptimizationResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(result.Sheets.Count).Append('|');
        foreach (var sheet in result.Sheets)
        {
            sb.Append(sheet.Stock.WidthMm).Append('x').Append(sheet.Stock.HeightMm).Append(':');
            foreach (var p in sheet.Pieces)
                sb.Append(p.X).Append(',').Append(p.Y).Append(',')
                  .Append(p.WidthMm).Append(',').Append(p.HeightMm)
                  .Append(p.Rotated ? ",R" : ",N").Append(';');
            sb.Append('|');
        }
        return sb.ToString();
    }

    private static string CutSignature(OptimizationResult result)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var sheet in result.Sheets)
        {
            sb.Append(sheet.SheetNo).Append(':');
            foreach (var c in sheet.Cuts)
                sb.Append(c.Seq).Append(c.Axis == CutAxis.Vertical ? "V" : "H")
                  .Append('@').Append(c.AtMm).Append('(')
                  .Append(c.FromX).Append(',').Append(c.FromY).Append(',')
                  .Append(c.FromW).Append(',').Append(c.FromH).Append(')')
                  .Append("s").Append(c.Stage).Append(c.IsTrim ? "T" : "-").Append(';');
            sb.Append('|');
        }
        return sb.ToString();
    }

    // ── PHASE 1 regression lock ──────────────────────────────────────────

    [Fact]
    public void Free_policy_is_identical_to_passing_no_policy()
    {
        var pieces = Job();

        var baseline = GuillotinePacker.PackQuick(pieces, Stock(), Margins, RotationPolicy.Free90);
        var withFree = GuillotinePacker.PackQuick(pieces, Stock(), Margins, RotationPolicy.Free90,
            policy: CutPolicy.Free);

        Assert.Equal(Signature(baseline), Signature(withFree));
        Assert.Equal(CutSignature(baseline), CutSignature(withFree));
        Assert.Empty(baseline.Unplaced);
    }

    [Fact]
    public void Free_policy_places_every_piece_and_passes_the_geometry_oracle()
    {
        var pieces = Job();
        var plan = GuillotinePacker.PackQuick(pieces, Stock(), Margins, RotationPolicy.Free90);

        Assert.Empty(GuillotinePacker.VerifyComprehensive(plan, pieces, Margins));
        Assert.Empty(plan.Unplaced);
    }

    // ── PHASE 0: the cut sequence itself ─────────────────────────────────

    [Fact]
    public void Every_placed_sheet_records_a_cut_sequence()
    {
        var pieces = Job();
        var plan = GuillotinePacker.PackQuick(pieces, Stock(), Margins, RotationPolicy.Free90);

        Assert.NotEmpty(plan.Sheets);
        foreach (var sheet in plan.Sheets)
        {
            // A piece that exactly fills the usable sheet is the one honest
            // exception: it needs no cut to be freed.
            long usable = (long)sheet.UsableWidth * sheet.UsableHeight;
            long used = sheet.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm);
            if (used == usable) continue;

            // Otherwise a sheet holding pieces must be cuttable — otherwise
            // the plan could not be produced on a table.
            Assert.NotEmpty(sheet.Cuts);
            for (int i = 0; i < sheet.Cuts.Count; i++)
                Assert.Equal(i + 1, sheet.Cuts[i].Seq);
        }
    }

    [Fact]
    public void Cut_sequence_is_deterministic()
    {
        var pieces = Job();

        var a = GuillotinePacker.PackQuick(pieces, Stock(), Margins, RotationPolicy.Free90);
        var b = GuillotinePacker.PackQuick(pieces, Stock(), Margins, RotationPolicy.Free90);

        Assert.Equal(CutSignature(a), CutSignature(b));
    }

    [Fact]
    public void Default_plan_passes_the_cut_pattern_oracle()
    {
        var pieces = Job();
        var plan = GuillotinePacker.PackQuick(pieces, Stock(), Margins, RotationPolicy.Free90);

        Assert.Empty(GuillotinePacker.ValidateCutPattern(plan, CutPolicy.Free));
        Assert.Empty(plan.ValidationProblems);
    }

    // ── PHASE 1: staged patterns ─────────────────────────────────────────

    [Theory]
    [InlineData(CutComplexity.FreeGuillotine)]
    [InlineData(CutComplexity.SimpleFirstX)]
    [InlineData(CutComplexity.SimpleFirstY)]
    [InlineData(CutComplexity.Simple)]
    [InlineData(CutComplexity.ThreeStage)]
    [InlineData(CutComplexity.ComplexTrims)]
    public void Every_complexity_level_produces_a_plan_that_passes_its_own_oracle(
        CutComplexity complexity)
    {
        var pieces = Job();
        var policy = CutPolicy.For(complexity);

        var plan = GuillotinePacker.PackOptimize(pieces, Stock(), Margins, RotationPolicy.Free90,
            effortLevel: 2, policy: policy);

        // Geometry and conservation are non-negotiable at every level: a
        // pattern that cannot be cut is not a pattern.
        Assert.Empty(GuillotinePacker.VerifyComprehensive(plan, pieces, Margins));
        Assert.Empty(plan.Unplaced);

        // And the pattern the operator asked for is the pattern they get.
        var problems = GuillotinePacker.ValidateCutPattern(plan, policy);
        Assert.Empty(problems);
    }

    [Fact]
    public void Simple_pattern_never_cuts_deeper_than_two_stages()
    {
        var pieces = Job();
        var policy = CutPolicy.For(CutComplexity.Simple);

        var plan = GuillotinePacker.PackOptimize(pieces, Stock(), Margins, RotationPolicy.Free90,
            effortLevel: 2, policy: policy);

        Assert.All(plan.Sheets.SelectMany(s => s.Cuts), cut => Assert.True(cut.Stage <= 2));
    }

    [Fact]
    public void Three_stage_pattern_never_cuts_deeper_than_three_stages()
    {
        var pieces = Job();
        var policy = CutPolicy.For(CutComplexity.ThreeStage);

        var plan = GuillotinePacker.PackOptimize(pieces, Stock(), Margins, RotationPolicy.Free90,
            effortLevel: 2, policy: policy);

        Assert.All(plan.Sheets.SelectMany(s => s.Cuts), cut => Assert.True(cut.Stage <= 3));
    }

    [Fact]
    public void First_x_pattern_cuts_horizontally_first_on_every_sheet()
    {
        var pieces = Job();
        var policy = CutPolicy.For(CutComplexity.SimpleFirstX);

        var plan = GuillotinePacker.PackOptimize(pieces, Stock(), Margins, RotationPolicy.Free90,
            effortLevel: 2, policy: policy);

        // A sheet whose every piece spans the sheet height has no stage-1 cut
        // at all (its strip IS the sheet), which is legal — so this asserts
        // that no stage-1 cut is ever vertical, not that one must exist.
        Assert.NotEmpty(plan.Sheets);
        Assert.NotEmpty(plan.Sheets.SelectMany(s => s.Cuts));
        var firstCuts = plan.Sheets.SelectMany(s => s.Cuts).Where(c => c.Stage == 1).ToList();
        Assert.All(firstCuts, cut => Assert.Equal(CutAxis.Horizontal, cut.Axis));
    }

    [Fact]
    public void First_y_pattern_cuts_vertically_first_on_every_sheet()
    {
        var pieces = Job();
        var policy = CutPolicy.For(CutComplexity.SimpleFirstY);

        var plan = GuillotinePacker.PackOptimize(pieces, Stock(), Margins, RotationPolicy.Free90,
            effortLevel: 2, policy: policy);

        Assert.NotEmpty(plan.Sheets);
        Assert.NotEmpty(plan.Sheets.SelectMany(s => s.Cuts));
        var firstCuts = plan.Sheets.SelectMany(s => s.Cuts).Where(c => c.Stage == 1).ToList();
        Assert.All(firstCuts, cut => Assert.Equal(CutAxis.Vertical, cut.Axis));
    }

    [Fact]
    public void Staged_pattern_never_costs_pieces()
    {
        // Staged cutting trades yield for simplicity, so it may well cost a
        // sheet — the sheet count is NOT pinned here for that reason. What it
        // must never cost is PIECES: every pattern still has to place the whole
        // order, because a pattern that cannot be cut is not a pattern.
        var pieces = Job();

        var staged = GuillotinePacker.PackOptimize(pieces, Stock(), Margins, RotationPolicy.Free90,
            effortLevel: 2, policy: CutPolicy.For(CutComplexity.Simple));
        var free = GuillotinePacker.PackOptimize(pieces, Stock(), Margins, RotationPolicy.Free90,
            effortLevel: 2, policy: CutPolicy.Free);

        Assert.Empty(staged.Unplaced);
        Assert.Equal(free.Unplaced.Count, staged.Unplaced.Count);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(staged, pieces, Margins));
    }

    // ── Reconstruction of sheets that were stamped, not split ─────────────

    [Fact]
    public void Cut_reconstruction_recovers_a_simple_guillotine_layout()
    {
        // Two pieces side by side across the usable sheet: exactly one cut.
        var sheet = new SheetLayout
        {
            SheetNo = 1, UsableX = 0, UsableY = 0, UsableWidth = 1000, UsableHeight = 800
        };
        sheet.Pieces.Add(At(Pane(400, 800, "a"), 0, 0, 400, 800));
        sheet.Pieces.Add(At(Pane(600, 800, "b"), 400, 0, 600, 800));

        Assert.True(GuillotinePacker.TryDeriveGuillotineCuts(sheet));

        Assert.Single(sheet.Cuts);
        Assert.Equal(CutAxis.Vertical, sheet.Cuts[0].Axis);
        Assert.Equal(400, sheet.Cuts[0].AtMm);
        Assert.Equal(1, sheet.Cuts[0].Stage);
        Assert.Equal(1, sheet.Cuts[0].Seq);
    }

    [Fact]
    public void Cut_reconstruction_walks_a_two_stage_layout_in_execution_order()
    {
        // Sheet cut into two columns at x = 500, then each column cut once:
        // the column cut must be recorded BEFORE the cuts inside the columns.
        var sheet = new SheetLayout
        {
            SheetNo = 1, UsableX = 0, UsableY = 0, UsableWidth = 1000, UsableHeight = 1000
        };
        sheet.Pieces.Add(At(Pane(500, 400, "a"), 0, 0, 500, 400));
        sheet.Pieces.Add(At(Pane(500, 600, "b"), 0, 400, 500, 600));
        sheet.Pieces.Add(At(Pane(500, 700, "c"), 500, 0, 500, 700));
        sheet.Pieces.Add(At(Pane(500, 300, "d"), 500, 700, 500, 300));

        Assert.True(GuillotinePacker.TryDeriveGuillotineCuts(sheet));
        Assert.Equal(3, sheet.Cuts.Count);
        Assert.Equal(1, sheet.Cuts[0].Seq);
        Assert.Equal(1, sheet.Cuts[0].Stage);
        Assert.Equal(CutAxis.Vertical, sheet.Cuts[0].Axis);
        Assert.Equal(500, sheet.Cuts[0].AtMm);
        Assert.All(sheet.Cuts, cut => Assert.True(cut.Stage <= 2));
        Assert.Empty(GuillotinePacker.ValidateCutPattern(
            new OptimizationResult { Sheets = { sheet } }, CutPolicy.For(CutComplexity.Simple)));
    }

    [Fact]
    public void Cut_reconstruction_refuses_a_non_guillotine_layout()
    {
        // A pinwheel: every candidate cut line crosses a piece, so there is no
        // guillotine sequence. The sheet must be left EMPTY rather than given
        // invented cuts — a plan that cannot be cut on a guillotine table has
        // to be visible, not papered over.
        var sheet = new SheetLayout
        {
            SheetNo = 1, UsableX = 0, UsableY = 0, UsableWidth = 1000, UsableHeight = 1000
        };
        sheet.Pieces.Add(At(Pane(600, 400, "a"), 0, 0, 600, 400));
        sheet.Pieces.Add(At(Pane(400, 600, "b"), 600, 0, 400, 600));
        sheet.Pieces.Add(At(Pane(600, 400, "c"), 400, 600, 600, 400));
        sheet.Pieces.Add(At(Pane(400, 600, "d"), 0, 400, 400, 600));

        Assert.False(GuillotinePacker.TryDeriveGuillotineCuts(sheet));
        Assert.Empty(sheet.Cuts);
    }

    private static PlacedPiece At(CutPiece piece, int x, int y, int w, int h) => new()
    {
        Piece = piece,
        X = x,
        Y = y,
        WidthMm = w,
        HeightMm = h
    };

    // ── The policy object itself ─────────────────────────────────────────

    [Fact]
    public void Policy_resolution_matches_the_declared_levels()
    {
        Assert.False(CutPolicy.Free.IsDepthConstrained);
        Assert.False(CutPolicy.For(CutComplexity.ComplexTrims).IsDepthConstrained);
        Assert.True(CutPolicy.For(CutComplexity.ComplexTrims).AllowTrimCuts);

        foreach (var level in new[]
        {
            CutComplexity.SimpleFirstX, CutComplexity.SimpleFirstY,
            CutComplexity.Simple, CutComplexity.ThreeStage
        })
        {
            var policy = CutPolicy.For(level);
            Assert.True(policy.IsDepthConstrained);
            Assert.False(policy.AllowTrimCuts);
            Assert.True(policy.MaxStages >= 2);
        }

        Assert.Equal(2, CutPolicy.For(CutComplexity.Simple).MaxStages);
        Assert.Equal(3, CutPolicy.For(CutComplexity.ThreeStage).MaxStages);
        Assert.True(CutPolicy.For(CutComplexity.SimpleFirstX).ForceFirstCutHorizontal);
        Assert.False(CutPolicy.For(CutComplexity.SimpleFirstX).ForceFirstCutVertical);
        Assert.True(CutPolicy.For(CutComplexity.SimpleFirstY).ForceFirstCutVertical);
        Assert.False(CutPolicy.For(CutComplexity.Simple).ForceFirstCutHorizontal);
    }

    [Fact]
    public void Trim_cuts_are_counted_per_sheet()
    {
        var pieces = Job();
        var plan = GuillotinePacker.PackQuick(pieces, Stock(), Margins, RotationPolicy.Free90);

        foreach (var sheet in plan.Sheets)
        {
            int counted = sheet.Cuts.Count(c => c.IsTrim);
            Assert.Equal(counted, sheet.TrimCutCount);
        }
    }
}
