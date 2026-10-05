using System;
using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

/// <summary>
/// Oracle tests for PHASE E3 (search structure).
///
/// E3.1 is the one item in E1-E3 whose safety cannot be argued from the
/// source alone: parallel evaluation is only correct if RunPass keeps no
/// mutable state between calls, and that was established by reading it, not
/// by running it. These tests are what turn that reasoning into a fact —
/// determinism across repeated runs is the signal a race would break.
/// </summary>
public sealed class SearchStructureTests
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

    // ── E3.1 parallel evaluation ────────────────────────────────────────

    [Fact]
    public void Parallel_evaluation_is_off_unless_asked_for()
    {
        var plain = GuillotinePacker.PackOptimize(Job(40), Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: 3);
        var explicitOff = GuillotinePacker.PackOptimize(Job(40), Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: 3, parallelEval: false);

        Assert.Equal(Signature(plain), Signature(explicitOff));
    }

    [Fact]
    public void Parallel_evaluation_is_deterministic()
    {
        // The race detector. If RunPass shares any mutable state, or the fold
        // is order-dependent, two runs of the same input will disagree.
        var a = GuillotinePacker.PackOptimize(Job(40), Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: 3, parallelEval: true);
        var b = GuillotinePacker.PackOptimize(Job(40), Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: 3, parallelEval: true);

        Assert.Equal(Signature(a), Signature(b));
        Assert.Equal(a.SheetsUsed, b.SheetsUsed);
    }

    [Fact]
    public void Parallel_evaluation_never_loses_a_pane_and_never_costs_a_sheet()
    {
        // The parallel path finishes every candidate rather than stopping at
        // the time budget, so it sees a superset of what the sequential loop
        // sees. A minimum over a superset can only be better or equal. If this
        // ever fails, the results array is dropping candidates.
        var job = Job(40);
        var sequential = GuillotinePacker.PackOptimize(job, Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: 3);
        var parallel = GuillotinePacker.PackOptimize(Job(40), Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: 3, parallelEval: true);

        Assert.True(parallel.SheetsUsed <= sequential.SheetsUsed);
        Assert.Equal(sequential.Sheets.Sum(s => s.Pieces.Count),
                     parallel.Sheets.Sum(s => s.Pieces.Count));
        Assert.Empty(GuillotinePacker.VerifyComprehensive(parallel, job, Margins));
    }

    // ── E3.3 wider beam ─────────────────────────────────────────────────

    [Fact]
    public void A_wider_beam_adds_a_candidate_and_can_never_cost_a_sheet()
    {
        // Better() takes the minimum, so an extra candidate cannot make the
        // plan worse - only slower. Effort 5 only, so nothing on the default
        // path pays for it.
        var job = Job(40);
        var narrow = GuillotinePacker.PackOptimize(job, Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: 5);
        var wide = GuillotinePacker.PackOptimize(Job(40), Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: 5, wideBeam: true);

        Assert.True(wide.SheetsUsed <= narrow.SheetsUsed);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(wide, job, Margins));
    }

    [Fact]
    public void The_wide_beam_does_not_apply_below_the_top_effort()
    {
        // Effort 4 must be untouched by a switch that is documented as
        // effort-5-only, or the default path is no longer the default path.
        var plain = GuillotinePacker.PackOptimize(Job(40), Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: 4);
        var wide = GuillotinePacker.PackOptimize(Job(40), Stocks(), Margins,
            RotationPolicy.Free90, effortLevel: 4, wideBeam: true);

        Assert.Equal(Signature(plain), Signature(wide));
    }
}
