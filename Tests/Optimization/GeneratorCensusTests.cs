using System;
using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization;

/// <summary>
/// PHASE E1.3 (candidate census) and E1.4 (baseline capture).
///
/// The census exists because of what BestSplit.cs:8-13 already recorded: on
/// the benchmark, four generators produced the SAME plan. That was measured
/// once, by hand, in 2026-09. This makes it measurable on demand, so the
/// question "is this generator earning its place in the pool?" has an answer
/// that does not depend on somebody remembering to check.
///
/// These tests assert the properties that must hold for every generator —
/// validity, bound, determinism — and one cross-check of the audit against a
/// hand count. They deliberately do NOT assert which generator wins: that is
/// what the census is for, and hard-coding a winner would defeat it. To read
/// the census, run these with detailed output and compare the results.
/// </summary>
public sealed class GeneratorCensusTests
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
        r.Sheets.Select(s => string.Join(",", s.Pieces
            .Select(p => p.X + "x" + p.Y + "x" + p.WidthMm + "x" + p.HeightMm))));

    /// <summary>
    /// Every generator in the pool, called on its own. Visibility was widened
    /// from private to internal for exactly this; nothing else changed.
    /// </summary>
    private static IEnumerable<(string Name, Func<List<CutPiece>, OptimizationResult> Build, bool PoolIdentical)> Generators()
    {
        // Not pool-identical: the pool feeds each ordering through Sort()
        // first and sweeps all three splits. Called raw here it is a different
        // candidate, so it must not be compared against the pool's result.
        yield return ("RunPass/HorizontalFirst",
            job => GuillotinePacker.RunPass(job, Stocks(), Margins, RotationPolicy.Free90,
                SplitMode.HorizontalFirst), false);
        yield return ("RunBestSplit",
            job => GuillotinePacker.RunBestSplit(job, Stocks(), Margins, RotationPolicy.Free90), true);
        yield return ("RunBestSplitBeam/4",
            job => GuillotinePacker.RunBestSplitBeam(job, Stocks(), Margins, RotationPolicy.Free90, 4), true);
        yield return ("RunSheetFill",
            job => GuillotinePacker.RunSheetFill(job, Stocks(), Margins, RotationPolicy.Free90,
                SplitMode.HorizontalFirst), true);
        yield return ("RunWallSeed",
            job => GuillotinePacker.RunWallSeed(job, Stocks(), Margins, RotationPolicy.Free90,
                SplitMode.HorizontalFirst), true);
        yield return ("RunMaxRects",
            job => GuillotinePacker.RunMaxRects(job, Stocks(), Margins, RotationPolicy.Free90), true);
        yield return ("RunCapacityStrips",
            job => GuillotinePacker.RunCapacityStrips(job, Stocks(), Margins, RotationPolicy.Free90,
                false, default), true);
        // Not pool-identical: the pool passes the scale profile's sweep count
        // and a windowed candidate size, so this call is a different candidate.
        yield return ("PackSmart",
            job => GuillotinePacker.PackSmart(job, Stocks(), Margins, RotationPolicy.Free90,
                SplitMode.HorizontalFirst, 1, default), false);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(40)]
    public void Every_generator_produces_a_plan_that_verifies(int count)
    {
        // Conservation and geometry, per generator. A generator that loses a
        // pane or overlaps two must not stay in the pool.
        foreach (var (name, build, _) in Generators())
        {
            var job = Job(count);
            var result = build(job);

            var problems = GuillotinePacker.VerifyComprehensive(result, job, Margins);
            Assert.True(problems.Count == 0,
                name + " produced an invalid plan: " + string.Join("; ", problems));
        }
    }

    [Fact]
    public void Every_generator_respects_the_optimality_bound()
    {
        foreach (var (name, build, _) in Generators())
        {
            var job = Job(40);
            var result = build(job);
            OptimizationInstrumentation.MeasureOptimality(result, Stocks(), Margins, RotationPolicy.Free90);

            Assert.True(result.SheetsLowerBound <= result.SheetsUsed,
                name + ": bound " + result.SheetsLowerBound + " exceeded the " + result.SheetsUsed
                + " sheets it actually used");
        }
    }

    [Fact]
    public void Every_generator_is_deterministic()
    {
        // E1.4. A baseline is only meaningful if the engine returns to it: same
        // input must give the same plan, twice, every time. Without this no
        // before/after comparison of any later phase means anything.
        foreach (var (name, build, _) in Generators())
        {
            var first = build(Job(40));
            var second = build(Job(40));

            Assert.Equal(Signature(first), Signature(second));
            Assert.Equal(first.SheetsUsed, second.SheetsUsed);
        }
    }

    [Fact]
    public void The_reachability_audit_agrees_with_a_hand_count()
    {
        // Cross-check of the audit against counting by hand, on the generator
        // most likely to produce an uncuttable layout. This asserts the audit
        // is CORRECT, not that MaxRects always fails to derive — that depends
        // on the job, and hard-coding it would make the test lie later.
        var job = Job(40);
        var result = GuillotinePacker.RunMaxRects(job, Stocks(), Margins, RotationPolicy.Free90);

        // Exactly what the engine does for a sheet with no sequence.
        foreach (var sheet in result.Sheets)
            if (sheet.Cuts.Count == 0)
                GuillotinePacker.TryDeriveGuillotineCuts(sheet);

        int byHand = result.Sheets.Count(s => s.Pieces.Count > 0 && s.Cuts.Count == 0);
        int audited = OptimizationInstrumentation.AuditCutReachability(result);

        Assert.Equal(byHand, audited);
        Assert.Equal(byHand, result.UncuttableSheetCount);
    }

    [Fact]
    public void The_full_pool_never_loses_to_its_own_generators()
    {
        // A sanity check on the streaming minimum: whatever the pool returns
        // must be at least as good as the best single generator inside it,
        // measured on sheets. If this ever fails, Better() is broken.
        var job = Job(40);
        var pooled = GuillotinePacker.PackOptimize(job, Stocks(), Margins, RotationPolicy.Free90,
            effortLevel: 3);

        // Only generators the pool actually calls with these exact parameters.
        // Comparing against a differently-parameterised call would be comparing
        // against a candidate that was never in the pool, and the assertion
        // would fail for a reason that has nothing to do with Better().
        int bestSingle = int.MaxValue;
        foreach (var (name, build, poolIdentical) in Generators())
        {
            if (!poolIdentical) continue;
            var single = build(Job(40));
            if (single.Unplaced.Count == 0 && single.SheetsUsed < bestSingle)
                bestSingle = single.SheetsUsed;
        }

        Assert.True(pooled.SheetsUsed <= bestSingle,
            "the pool returned " + pooled.SheetsUsed + " sheets but a single generator reached "
            + bestSingle);
    }
}
