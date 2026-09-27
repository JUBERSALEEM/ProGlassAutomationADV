// ═══════════════════════════════════════════════════════════════════════
//  Phase 25a — the OPTIMIZE chassis golden tests:
//   • the six Sort orderings + ShuffleDeterministic (reproducible,
//     a permutation, seed-sensitive)
//   • the Better() comparison chain — unplaced/sheets dominance and the
//     four plate-pattern axes (FewestCuts, MinimiseStockChanges,
//     RemnantFirst, BalancedSpread) on hand-built plans
//   • the adaptive governor (SearchTier bands, budgets, PlanForQuick,
//     RecommendEffort) and ScaleProfile bands
//   • the oracles: Verify (overlap/kerf/margin/rotation), AuditBreakout,
//     ValidateResult (double placement, missing piece), VerifyComprehensive
//   • cut-sequence derivation: a stamped guillotine layout derives; a
//     pinwheel does not (and says so by leaving the list empty)
//   • PackOptimize: stage 1 IS PackQuick (the NeverRegressesQuick
//     contract), stage 3 never regresses sheet count, deterministic,
//     validation-clean, and the industrial-threshold note
// Pure domain — no database, no filesystem.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Optimization;

namespace ProGlass.Saas.Domain.Tests.Optimization;

public sealed class OptimizeChassisTests
{
    private static readonly StockSize Rack = new(1000, 1000, 6, "Clear");

    private static MarginProfile Zero => new(6, 0, 0, 0, 0, 0, 0);

    private static CutPiece Piece(int w, int h, string label = "") =>
        new("JOB-1", "A-01", "6mm Clear", "Glass", "Clear", 6, w, h, true, label);

    private static PlacedPiece At(CutPiece piece, int x, int y, int? w = null, int? h = null) =>
        new(piece, x, y, w ?? piece.WidthMm, h ?? piece.HeightMm, rotated: false);

    private static SheetLayout Sheet(params PlacedPiece[] pieces) => Sheet(1, pieces);

    private static SheetLayout Sheet(int sheetNo, params PlacedPiece[] pieces)
    {
        var sheet = new SheetLayout
        {
            SheetNo = sheetNo,
            Stock = Rack,
            UsableX = 0,
            UsableY = 0,
            UsableWidth = 1000,
            UsableHeight = 1000
        };
        foreach (var p in pieces) sheet.Pieces.Add(p);
        return sheet;
    }

    // ── Sort + ShuffleDeterministic ─────────────────────────────────

    [Fact]
    public void Sort_orders_by_each_key()
    {
        var pieces = new List<CutPiece>
        {
            Piece(600, 400, "a"),   // area 240k, max 600, min 400, per 1000
            Piece(500, 500, "b"),   // area 250k, max 500, min 500, per 1000
            Piece(300, 900, "c"),   // area 270k, max 900, min 300, per 1200
            Piece(700, 300, "d")    // area 210k, max 700, min 300, per 1000
        };

        Assert.Equal("c", GuillotinePacker.Sort(pieces, GuillotinePacker.OrderBy.Area)[0].PieceLabel);
        Assert.Equal("d", GuillotinePacker.Sort(pieces, GuillotinePacker.OrderBy.MaxSide)[0].PieceLabel);
        Assert.Equal("b", GuillotinePacker.Sort(pieces, GuillotinePacker.OrderBy.MinSide)[0].PieceLabel);
        Assert.Equal("c", GuillotinePacker.Sort(pieces, GuillotinePacker.OrderBy.Height)[0].PieceLabel);
        Assert.Equal("d", GuillotinePacker.Sort(pieces, GuillotinePacker.OrderBy.Width)[0].PieceLabel);
        // Perimeter 1200 (c) ties 1000 (a, b, d) — area breaks it: c first.
        Assert.Equal("c", GuillotinePacker.Sort(pieces, GuillotinePacker.OrderBy.Perimeter)[0].PieceLabel);
        // The input list is never reordered by sorting (pure function).
        Assert.Equal("a", pieces[0].PieceLabel);
    }

    [Fact]
    public void Shuffle_is_reproducible_seed_sensitive_and_a_permutation()
    {
        var pieces = Enumerable.Range(0, 40)
            .Select(i => Piece(100 + i, 200 + i, $"p{i}"))
            .ToList();

        var first = GuillotinePacker.ShuffleDeterministic(pieces, 12345);
        var second = GuillotinePacker.ShuffleDeterministic(pieces, 12345);
        var other = GuillotinePacker.ShuffleDeterministic(pieces, 999);

        Assert.Equal(first.Select(p => p.PieceLabel), second.Select(p => p.PieceLabel));
        Assert.NotEqual(first.Select(p => p.PieceLabel), other.Select(p => p.PieceLabel));
        // A permutation: same multiset of labels, and the source is intact.
        Assert.Equal(
            pieces.Select(p => p.PieceLabel).OrderBy(l => l, StringComparer.Ordinal),
            first.Select(p => p.PieceLabel).OrderBy(l => l, StringComparer.Ordinal));
        Assert.Equal("p0", pieces[0].PieceLabel);
    }

    // ── the Better chain ────────────────────────────────────────────

    [Fact]
    public void Better_dominates_on_unplaced_then_sheets()
    {
        // More unplaced always loses, whatever the sheets.
        var unplacedMore = new OptimizationResult();
        unplacedMore.Unplaced.Add(Piece(100, 100));
        unplacedMore.Sheets.Add(Sheet(At(Piece(900, 900), 0, 0)));

        var unplacedNone = new OptimizationResult();
        unplacedNone.Sheets.Add(Sheet());
        unplacedNone.Sheets.Add(Sheet(At(Piece(900, 900), 0, 0)));

        var better = GuillotinePacker.BetterPublic(unplacedMore, unplacedNone);
        Assert.Same(unplacedNone, better);

        // Equal unplaced: fewer sheets wins even at lower utilization.
        var twoSheets = new OptimizationResult();
        twoSheets.Sheets.Add(Sheet(At(Piece(100, 100), 0, 0)));
        twoSheets.Sheets.Add(Sheet(At(Piece(100, 100), 0, 0)));

        var fewerSheets = new OptimizationResult();
        fewerSheets.Sheets.Add(Sheet(At(Piece(100, 100), 0, 0), At(Piece(100, 100), 200, 0)));

        Assert.Same(fewerSheets, GuillotinePacker.BetterPublic(twoSheets, fewerSheets));
    }

    [Fact]
    public void Better_fewest_cuts_prefers_the_plan_with_fewer_cuts()
    {
        // Plan A: two pieces side by side → ONE vertical cut separates
        // them (two disjoint rectangles are always one cut apart).
        var oneCut = new OptimizationResult();
        oneCut.Sheets.Add(Sheet(
            At(Piece(600, 1000, "a"), 0, 0),
            At(Piece(400, 1000, "b"), 600, 0)));

        // Plan B: three pieces needing a two-stage sequence — vertical
        // at 400 splits {c, e} | {d}, then horizontal at 500 splits
        // {c} / {e} → TWO cuts.
        var twoCuts = new OptimizationResult();
        twoCuts.Sheets.Add(Sheet(
            At(Piece(400, 500, "c"), 0, 0),
            At(Piece(600, 500, "d"), 400, 0),
            At(Piece(300, 500, "e"), 0, 500)));

        var fewest = GuillotinePacker.BetterPublic(oneCut, twoCuts, PlatePattern.FewestCuts);
        Assert.Same(oneCut, fewest);

        // Under Yield the axis does not fire and utilization decides:
        // oneCut = 1,000,000 mm² of glass (full), twoCuts = 650,000 —
        // same evidence, different verdict.
        var byYield = GuillotinePacker.BetterPublic(oneCut, twoCuts, PlatePattern.Yield);
        Assert.Same(oneCut, byYield);

        // And the axis itself is real: swap the glass so twoCuts carries
        // MORE glass while keeping its two cuts — FewestCuts still keeps
        // oneCut, Yield would not.
        var twoCutsDenser = new OptimizationResult();
        twoCutsDenser.Sheets.Add(Sheet(
            At(Piece(400, 500, "c"), 0, 0),
            At(Piece(600, 500, "d"), 400, 0),
            At(Piece(600, 500, "e"), 0, 500)));
        Assert.Same(oneCut,
            GuillotinePacker.BetterPublic(oneCut, twoCutsDenser, PlatePattern.FewestCuts));
        Assert.Same(twoCutsDenser,
            GuillotinePacker.BetterPublic(oneCut, twoCutsDenser, PlatePattern.Yield));
    }

    [Fact]
    public void Better_minimise_stock_changes_prefers_one_rack()
    {
        var singleRack = new OptimizationResult();
        singleRack.Sheets.Add(Sheet(At(Piece(100, 100, "a"), 0, 0)));
        singleRack.Sheets.Add(Sheet(At(Piece(100, 100, "b"), 0, 0)));

        var mixed = new OptimizationResult();
        mixed.Sheets.Add(Sheet(At(Piece(100, 100, "c"), 0, 0)));
        var otherStock = new SheetLayout
        {
            SheetNo = 2,
            Stock = new StockSize(1200, 1200, 6, "Clear"),
            UsableWidth = 1200, UsableHeight = 1200
        };
        otherStock.Pieces.Add(At(Piece(100, 100, "d"), 0, 0));
        mixed.Sheets.Add(otherStock);

        var winner = GuillotinePacker.BetterPublic(singleRack, mixed, PlatePattern.MinimiseStockChanges);
        Assert.Same(singleRack, winner);
    }

    [Fact]
    public void Better_remnant_first_prefers_the_bigger_single_leftover()
    {
        var bigRemnant = new OptimizationResult();
        bigRemnant.Sheets.Add(Sheet(At(Piece(100, 100, "a"), 0, 0)));   // leftover 990,000

        var slivers = new OptimizationResult();
        slivers.Sheets.Add(Sheet(
            At(Piece(500, 500, "b"), 0, 0),
            At(Piece(400, 400, "c"), 500, 0)));                          // leftover 590,000

        var winner = GuillotinePacker.BetterPublic(bigRemnant, slivers, PlatePattern.RemnantFirst);
        Assert.Same(bigRemnant, winner);
    }

    [Fact]
    public void Better_balanced_spread_prefers_even_loading()
    {
        var even = new OptimizationResult();
        even.Sheets.Add(Sheet(At(Piece(500, 500, "a"), 0, 0)));
        even.Sheets.Add(Sheet(At(Piece(500, 500, "b"), 0, 0)));

        var lopsided = new OptimizationResult();
        lopsided.Sheets.Add(Sheet(
            At(Piece(500, 500, "c"), 0, 0),
            At(Piece(400, 400, "d"), 500, 0)));
        lopsided.Sheets.Add(Sheet(At(Piece(100, 100, "e"), 0, 0)));

        var winner = GuillotinePacker.BetterPublic(even, lopsided, PlatePattern.BalancedSpread);
        Assert.Same(even, winner);   // spread 0 < spread (0.66 − 0.01)
    }

    // ── the governor + scale profile ────────────────────────────────

    [Fact]
    public void Governor_tiers_budgets_and_quick_plan()
    {
        // Tier bands (piece count): Full ≤ 2,000; Narrowed ≤ 15,000;
        // Minimum above.
        Assert.Equal(SearchTier.Full, OptimizationSearchGovernor.PlanForOptimize(2000, 3).Tier);
        Assert.Equal(SearchTier.Narrowed, OptimizationSearchGovernor.PlanForOptimize(2001, 3).Tier);
        Assert.Equal(SearchTier.Narrowed, OptimizationSearchGovernor.PlanForOptimize(15000, 3).Tier);
        Assert.Equal(SearchTier.Minimum, OptimizationSearchGovernor.PlanForOptimize(15001, 3).Tier);

        // Wall-clock budgets by tier and effort (WPF BudgetFor).
        Assert.Equal(TimeSpan.FromMinutes(8),
            OptimizationSearchGovernor.PlanForOptimize(100, 3).TimeBudget);
        Assert.Equal(TimeSpan.FromMinutes(15),
            OptimizationSearchGovernor.PlanForOptimize(100, 4).TimeBudget);
        Assert.Equal(TimeSpan.FromMinutes(25),
            OptimizationSearchGovernor.PlanForOptimize(100, 5).TimeBudget);
        Assert.Equal(TimeSpan.FromMinutes(20),
            OptimizationSearchGovernor.PlanForOptimize(5000, 5).TimeBudget);
        Assert.Equal(TimeSpan.FromMinutes(40),
            OptimizationSearchGovernor.PlanForOptimize(20000, 3).TimeBudget);

        // Shuffle caps by effort (Full tier).
        Assert.Equal(0, OptimizationSearchGovernor.PlanForOptimize(100, 3).ShuffleCap);
        Assert.Equal(3, OptimizationSearchGovernor.PlanForOptimize(100, 4).ShuffleCap);
        Assert.Equal(6, OptimizationSearchGovernor.PlanForOptimize(100, 5).ShuffleCap);

        // QUICK: consolidation 3 below 2,000 parts, 2 above, 5-minute cap.
        var small = OptimizationSearchGovernor.PlanForQuick(500);
        var large = OptimizationSearchGovernor.PlanForQuick(5000);
        Assert.Equal(3, small.MaxConsolidationSweeps);
        Assert.Equal(2, large.MaxConsolidationSweeps);
        Assert.Equal(TimeSpan.FromMinutes(5), small.TimeBudget);
        Assert.Equal(TimeSpan.FromMinutes(5), large.TimeBudget);

        // The narrowing escape hatch halves nothing silently: it must
        // record the reason the operator will read.
        var narrowed = new SearchPlan().WithReducedBreadth("because");
        Assert.Equal(SearchTier.Narrowed, narrowed.Tier);
        Assert.Equal(1, narrowed.ShuffleCap);
        Assert.Equal(400, narrowed.OpenSheetWindow);
        Assert.Contains("because", narrowed.Note);
    }

    [Fact]
    public void RecommendEffort_picks_stages_3_4_5()
    {
        Assert.Equal(5, OptimizationSearchGovernor.RecommendEffort(50, 1, false).Stage);
        Assert.Equal(4, OptimizationSearchGovernor.RecommendEffort(1000, 2, false).Stage);
        Assert.Equal(3, OptimizationSearchGovernor.RecommendEffort(1000, 1, false).Stage);
        Assert.Equal(4, OptimizationSearchGovernor.RecommendEffort(1000, 1, true).Stage);
        Assert.Equal(3, OptimizationSearchGovernor.RecommendEffort(100000, 1, false).Stage);
        Assert.Equal(
            "one stock size in the normal shop range stays on stage 3, the calibrated default.",
            OptimizationSearchGovernor.RecommendEffort(1000, 1, false).Reason);
    }

    [Fact]
    public void ScaleProfile_bands_match_the_governor()
    {
        var standard = ScaleProfile.For(1500, 3);
        Assert.Equal(ScaleTier.Standard, standard.Tier);
        Assert.False(standard.IsReduced);
        Assert.True(standard.AllowBeam);
        Assert.True(standard.AllowQuadraticStrategies);

        var large = ScaleProfile.For(5000, 3);
        Assert.Equal(ScaleTier.Large, large.Tier);
        Assert.True(large.IsReduced);
        Assert.False(large.AllowBeam);
        Assert.Equal(4, large.ConsolidationSweeps);
        Assert.Contains("adaptive bounded", large.Summary);

        var huge = ScaleProfile.For(50000, 3);
        Assert.Equal(ScaleTier.Huge, huge.Tier);
        Assert.False(huge.AllowQuadraticStrategies);
    }

    // ── the oracles ─────────────────────────────────────────────────

    [Fact]
    public void Verify_flags_overlap_kerf_and_margin_breaches()
    {
        var overlapping = new OptimizationResult();
        overlapping.Sheets.Add(Sheet(
            At(Piece(500, 500, "a"), 0, 0),
            At(Piece(500, 500, "b"), 250, 250)));
        var problems = GuillotinePacker.Verify(overlapping, Zero);
        Assert.Contains(problems, p => p.Contains("overlaps"));

        var tight = new OptimizationResult();
        tight.Sheets.Add(Sheet(
            At(Piece(500, 500, "a"), 0, 0),
            At(Piece(500, 500, "b"), 502, 0)));   // 2mm gap < 3mm kerf
        problems = GuillotinePacker.Verify(tight, new MarginProfile(6, 0, 0, 0, 0, 3, 0));
        Assert.Contains(problems, p => p.Contains("breaks kerf"));

        var outOfBounds = new OptimizationResult();
        outOfBounds.Sheets.Add(Sheet(At(Piece(500, 500, "a"), 600, 600)));
        problems = GuillotinePacker.Verify(outOfBounds, Zero);
        Assert.Contains(problems, p => p.Contains("breaches the margin zone"));
    }

    [Fact]
    public void AuditBreakout_flags_offcuts_below_the_minimum()
    {
        var margins = new MarginProfile(6, 0, 0, 0, 0, 3, 50);   // kerf 3, min breakout 50

        var plan = new OptimizationResult();
        plan.Sheets.Add(Sheet(
            At(Piece(500, 500, "a"), 0, 0),
            At(Piece(500, 500, "b"), 510, 0)));   // 10mm gap − 3 kerf = 7mm offcut < 50

        var problems = GuillotinePacker.AuditBreakout(plan, margins);
        var msg = Assert.Single(problems);
        Assert.Contains("below the 50 mm minimum breakout", msg);

        // A 60mm gap − 3 kerf = 57mm is fine.
        var ok = new OptimizationResult();
        ok.Sheets.Add(Sheet(
            At(Piece(500, 500, "a"), 0, 0),
            At(Piece(500, 500, "b"), 563, 0)));
        Assert.Empty(GuillotinePacker.AuditBreakout(ok, margins));
    }

    [Fact]
    public void ValidateResult_catches_double_placement_and_missing_pieces()
    {
        var shared = Piece(500, 500, "a");
        var plan = new OptimizationResult();
        plan.Sheets.Add(Sheet(At(shared, 0, 0), At(shared, 500, 0)));

        var problems = GuillotinePacker.ValidateResult(plan, new[] { shared });
        Assert.Contains(problems, p => p.Contains("placed more than once"));

        var missing = Piece(300, 300, "ghost");
        problems = GuillotinePacker.ValidateResult(plan, new[] { shared, missing });
        Assert.Contains(problems, p => p.Contains("Piece missing from plan"));
    }

    [Fact]
    public void VerifyComprehensive_checks_quantity_and_stock_caps()
    {
        var piece = Piece(500, 500, "a");
        var plan = new OptimizationResult();
        plan.Sheets.Add(Sheet(At(piece, 0, 0)));

        // Input had TWO pieces but only one is placed and none unplaced.
        var problems = GuillotinePacker.VerifyComprehensive(
            plan, new[] { piece, Piece(400, 400, "b") }, Zero);
        Assert.Contains(problems, p => p.Contains("Quantity conservation violation"));

        // Stock cap: a rack that allows only one sheet, used twice.
        var capped = new StockSize(1000, 1000, 6, "Clear", maxSheets: 1);
        var cappedPlan = new OptimizationResult();
        for (int i = 0; i < 2; i++)
        {
            var s = new SheetLayout
            {
                SheetNo = i + 1, Stock = capped,
                UsableWidth = 1000, UsableHeight = 1000
            };
            s.Pieces.Add(At(Piece(500, 500, $"p{i}"), 0, 0));
            cappedPlan.Sheets.Add(s);
        }
        problems = GuillotinePacker.VerifyComprehensive(
            cappedPlan, cappedPlan.Sheets.SelectMany(s => s.Pieces.Select(p => p.Piece)).ToList(), Zero);
        Assert.Contains(problems, p => p.Contains("Stock cap violated"));
    }

    // ── cut-sequence derivation ─────────────────────────────────────

    [Fact]
    public void Cut_derivation_succeeds_for_stamped_guillotine_layouts()
    {
        var sheet = Sheet(
            At(Piece(600, 1000, "a"), 0, 0),
            At(Piece(400, 1000, "b"), 600, 0));
        Assert.Empty(sheet.Cuts);

        Assert.True(GuillotinePacker.TryDeriveGuillotineCuts(sheet));
        var cut = Assert.Single(sheet.Cuts);
        Assert.Equal(CutAxis.Vertical, cut.Axis);
        Assert.Equal(600, cut.AtMm);

        // Idempotent: deriving again keeps the same sequence.
        Assert.True(GuillotinePacker.TryDeriveGuillotineCuts(sheet));
        Assert.Single(sheet.Cuts);
    }

    [Fact]
    public void Cut_derivation_rejects_a_pinwheel()
    {
        // The classic non-guillotine windmill: every full-width and
        // full-height line crosses one of the four pieces.
        var sheet = Sheet(
            At(Piece(700, 300, "top"), 0, 0),
            At(Piece(300, 700, "right"), 700, 0),
            At(Piece(700, 300, "bottom"), 300, 700),
            At(Piece(300, 700, "left"), 0, 300));
        Assert.Empty(sheet.Cuts);

        Assert.False(GuillotinePacker.TryDeriveGuillotineCuts(sheet));
        Assert.Empty(sheet.Cuts);   // never invents cuts

        var plan = new OptimizationResult();
        plan.Sheets.Add(sheet);
        Assert.False(GuillotinePacker.AllSheetsCuttable(plan));

        var cuttable = new OptimizationResult();
        cuttable.Sheets.Add(Sheet(At(Piece(600, 1000, "a"), 0, 0),
                                  At(Piece(400, 1000, "b"), 600, 0)));
        Assert.True(GuillotinePacker.AllSheetsCuttable(cuttable));
    }

    // ── PackOptimize ────────────────────────────────────────────────

    private static List<CutPiece> CalibrationBatch() => new()
    {
        Piece(2200, 1100, "01"), Piece(2100, 900, "02"), Piece(2000, 850, "03"),
        Piece(1800, 800, "04"), Piece(1700, 750, "05"), Piece(1600, 700, "06"),
        Piece(1500, 650, "07"), Piece(1400, 600, "08"), Piece(1300, 550, "09"),
        Piece(1200, 500, "10"), Piece(1100, 450, "11"), Piece(1000, 400, "12"),
        Piece(900, 350, "13"), Piece(800, 300, "14"), Piece(700, 250, "15"),
        Piece(600, 200, "16"), Piece(500, 180, "17"), Piece(450, 160, "18"),
        Piece(400, 140, "19"), Piece(350, 120, "20"), Piece(300, 110, "21"),
        Piece(250, 100, "22"), Piece(2200, 400, "23"), Piece(1100, 1100, "24")
    };

    private static readonly StockSize[] Racks =
    {
        new(3210, 2250, 6, "Clear"),
        new(2250, 3210, 6, "Clear")
    };

    private static bool IsKnownCandidate(string strategy) =>
        strategy.StartsWith("QUICK SHELF") ||
        strategy.StartsWith("GRID") ||
        strategy.StartsWith("CAPACITY STRIPS") ||
        strategy.StartsWith("PATTERN BEAM") ||
        strategy.StartsWith("SHEET-FILL") ||
        strategy.StartsWith("WALL-SEED") ||
        strategy.StartsWith("BEST-SPLIT") ||
        strategy.StartsWith("SMART PLACEMENT") ||
        strategy.StartsWith("MAXRECTS-BSSF");

    [Fact]
    public void Optimize_stage1_is_exactly_quick()
    {
        var pieces = CalibrationBatch();
        var margins = MarginProfile.DefaultFor(6);

        var quick = GuillotinePacker.PackQuick(pieces, Racks, margins, RotationPolicy.Free90);
        var stage1 = GuillotinePacker.PackOptimize(pieces, Racks, margins, RotationPolicy.Free90,
            effortLevel: 1);

        // The NeverRegressesQuick contract: stage 1 mirrors PackQuick —
        // the same deterministic call, so the same winning candidate and
        // label (25b: QUICK's portfolio is shelf + capacity strips +
        // pattern beam, and any of them can win).
        Assert.Equal(quick.SheetsUsed, stage1.SheetsUsed);
        Assert.Equal(quick.Unplaced.Count, stage1.Unplaced.Count);
        Assert.Equal(quick.Strategy, stage1.Strategy);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(stage1, pieces, margins));
    }

    [Fact]
    public void Optimize_never_regresses_quick_and_stays_valid()
    {
        var pieces = CalibrationBatch();
        var margins = MarginProfile.DefaultFor(6);

        var quick = GuillotinePacker.PackQuick(pieces, Racks, margins, RotationPolicy.Free90);
        foreach (var effort in new[] { 2, 3, 4, 5 })
        {
            var optimized = GuillotinePacker.PackOptimize(pieces, Racks, margins,
                RotationPolicy.Free90, effortLevel: effort);

            Assert.True(optimized.SheetsUsed <= quick.SheetsUsed,
                $"effort {effort}: {optimized.SheetsUsed} > QUICK {quick.SheetsUsed}");
            Assert.Equal(0, optimized.Unplaced.Count);
            Assert.Empty(GuillotinePacker.VerifyComprehensive(optimized, pieces, margins));
            // Every sheet is cuttable — the E4.1 invariant, by construction
            // or by derivation.
            Assert.True(GuillotinePacker.AllSheetsCuttable(optimized));
            // QUICK seeds the comparison and a candidate only replaces
            // it by being strictly better — so the winner is always one
            // of the run's named candidates (25b widened the pool with
            // the advanced families; the WPF's E4.1 guillotine gate is
            // off by default, so a non-guillotine MAXRECTS winner is
            // legal and carries the derivation warning instead).
            Assert.True(IsKnownCandidate(optimized.Strategy),
                $"unexpected strategy: {optimized.Strategy}");
            Assert.True(
                GuillotinePacker.AllSheetsCuttable(optimized) ||
                optimized.ScaleNotes.Any(n => n.StartsWith("WARNING: sheet ")),
                "non-cuttable winner without the E4.1 warning note");
        }
    }

    [Fact]
    public void Optimize_is_deterministic()
    {
        var pieces = CalibrationBatch();
        var margins = MarginProfile.DefaultFor(6);

        var first = GuillotinePacker.PackOptimize(pieces, Racks, margins,
            RotationPolicy.Free90, effortLevel: 4);
        var second = GuillotinePacker.PackOptimize(pieces, Racks, margins,
            RotationPolicy.Free90, effortLevel: 4);

        Assert.Equal(first.SheetsUsed, second.SheetsUsed);
        Assert.Equal(first.Strategy, second.Strategy);
        Assert.Equal(
            first.Sheets.SelectMany(s => s.Pieces.Select(p => p.Piece.PieceLabel)),
            second.Sheets.SelectMany(s => s.Pieces.Select(p => p.Piece.PieceLabel)));
    }

    [Fact]
    public void Optimize_maxStage_overrides_effort()
    {
        // maxStage 2 with effort 5: the stage axis wins — 3 orderings,
        // no shuffles — and it still never regresses QUICK.
        var pieces = CalibrationBatch();
        var margins = MarginProfile.DefaultFor(6);
        var quick = GuillotinePacker.PackQuick(pieces, Racks, margins, RotationPolicy.Free90);

        var staged = GuillotinePacker.PackOptimize(pieces, Racks, margins,
            RotationPolicy.Free90, effortLevel: 5, maxStage: 2);

        Assert.True(staged.SheetsUsed <= quick.SheetsUsed);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(staged, pieces, margins));
    }

    [Fact]
    public void Optimize_parallel_evaluation_reaches_the_same_plan()
    {
        var pieces = CalibrationBatch();
        var margins = MarginProfile.DefaultFor(6);

        var sequential = GuillotinePacker.PackOptimize(pieces, Racks, margins,
            RotationPolicy.Free90, effortLevel: 3);
        var parallel = GuillotinePacker.PackOptimize(pieces, Racks, margins,
            RotationPolicy.Free90, effortLevel: 3, parallelEval: true);

        // E3.1: folding in candidate order reaches the same winner the
        // sequential loop would have reached (budget not binding).
        Assert.Equal(sequential.SheetsUsed, parallel.SheetsUsed);
        Assert.Equal(sequential.Strategy, parallel.Strategy);
        Assert.Equal(
            sequential.Sheets.SelectMany(s => s.Pieces.Select(p => p.Piece.PieceLabel)),
            parallel.Sheets.SelectMany(s => s.Pieces.Select(p => p.Piece.PieceLabel)));
    }

    [Fact]
    public void Optimize_industrial_threshold_note_is_visible()
    {
        // 100,005 pieces is at/above the 100,000-part threshold: the WPF
        // hands off to the pattern master. In this slice ONE bounded
        // shelf pass runs instead and SAYS SO — never silently. The
        // pieces are tiny so the batch stays linear-sized in sheets.
        var pieces = Enumerable.Range(0, 100_005)
            .Select(i => Piece(10, 10, $"p{i}"))
            .ToList<CutPiece>();
        var margins = MarginProfile.DefaultFor(6);

        var result = GuillotinePacker.PackOptimize(pieces, Racks, margins,
            RotationPolicy.Free90, effortLevel: 2);

        Assert.Contains(result.ScaleNotes,
            n => n.StartsWith("INDUSTRIAL PATH") && n.Contains("100,005"));
        Assert.Equal(0, result.Unplaced.Count);
    }

    [Fact]
    public void Quick_pass_now_consolidates_and_stays_valid()
    {
        var pieces = CalibrationBatch();
        var margins = MarginProfile.DefaultFor(6);

        var quick = GuillotinePacker.PackQuick(pieces, Racks, margins, RotationPolicy.Free90);

        Assert.Equal(0, quick.Unplaced.Count);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(quick, pieces, margins));
        // The scope note tells the operator exactly which WPF candidates
        // are still pending — no silent degradation.
        Assert.Contains(quick.ScaleNotes, n => n.StartsWith("SaaS port (Phase 25c)"));
        // Consolidation ran: every remaining sheet holds at least one
        // piece (no evacuated husks left behind).
        Assert.All(quick.Sheets, s => Assert.NotEmpty(s.Pieces));
    }
}
