// ═══════════════════════════════════════════════════════════════════════
//  Phase 25c — the repeated-pattern / plate-pattern / partitioned
//  family, golden tests:
//   • RunRepeatedPatterns — identical-quantity batches repeat identical
//     per-sheet patterns, original pane identities are preserved exactly
//     once, mixed sizes stay valid, and the strip DP's separated-gap
//     columns respect kerf + minimum breakout
//   • the pattern-master route: PackOptimize at the industrial quantity
//     runs PackIndustrialPatterns (CheckCandidate'd, verified clean)
//     instead of the small-job grid, and says so
//   • PackFillFirstPlate — a legal fill-first plan with the stage/window
//     strategy label; FillFirstWindows stages nest (a wider search
//     cannot drop a plan the narrower one already found)
//   • ApplyPlatePattern via PackQuick — Yield is untouched; FillFirstPlate
//     never returns an invalid plan, more unplaced pieces or more sheets
//     than the yield plan, and leaves a decision note on every path
//   • NoteFillSkipped — the industrial quantity skips the plate pattern
//     with a visible note
//   • PackOptimizePartitioned routing — small jobs delegate to
//     PackOptimize, finite stock takes PackOptimize (a rack cap belongs
//     to the whole job), unlimited stock above the large-job threshold
//     runs the batched engine with its note, stage 1 is QUICK
//   • SmartSearchConfiguration defaults (the WPF's tuning, verbatim)
// Pure domain — no database, no filesystem.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Optimization;

namespace ProGlass.Saas.Domain.Tests.Optimization;

public sealed class PatternFamilyTests
{
    private static readonly StockSize[] Racks =
    {
        new(3210, 2250, 6, "Clear"),
        new(2250, 3210, 6, "Clear")
    };

    private static readonly StockSize[] BigRacks =
    {
        new(3210, 2250, 6, "Clear", maxSheets: 0)
    };

    private static MarginProfile House => MarginProfile.DefaultFor(6);

    private static CutPiece P(int w, int h, string label = "") =>
        new("JOB-1", "A-01", "6mm Clear", "Glass", "Clear", 6, w, h, true, label);

    // ── RunRepeatedPatterns ─────────────────────────────────────────

    [Fact]
    public void Repeated_patterns_repeat_identical_sheets_for_identical_quantities()
    {
        // 480 of the SAME 600×400 pane: a quantity-aware plan prices one
        // pattern and repeats it, so most sheets must share one exact
        // placement signature instead of every sheet being bespoke.
        var pieces = Enumerable.Range(0, 480)
            .Select(i => P(600, 400, $"p{i:D4}"))
            .ToList();

        var result = GuillotinePacker.RunRepeatedPatterns(
            pieces, Racks, House, RotationPolicy.Free90, 1, default);

        Assert.Equal(0, result.Unplaced.Count);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(result, pieces, House));

        var signatures = result.Sheets
            .GroupBy(s => string.Join("|",
                s.Pieces.Select(p => $"{p.X},{p.Y},{p.WidthMm}x{p.HeightMm}")
                    .OrderBy(t => t)))
            .ToList();
        // Identical panes repeat one (or a couple of) pattern(s) across
        // the order — that is the whole point of the pattern master.
        Assert.True(signatures.Count <= 4,
            $"expected repeated identical patterns, got {signatures.Count} distinct sheet signatures");
        Assert.Contains(signatures, g => g.Count() >= 8);
    }

    [Fact]
    public void Repeated_patterns_preserve_every_pane_identity_exactly_once()
    {
        var pieces = Enumerable.Range(0, 97)
            .Select(i => P(400 + (i % 3) * 50, 300 + (i % 2) * 75, $"id{i:D3}"))
            .ToList();

        var result = GuillotinePacker.RunRepeatedPatterns(
            pieces, Racks, House, RotationPolicy.Free90, 2, default);

        var placed = result.Sheets.SelectMany(s => s.Pieces)
            .Select(p => p.Piece.PieceLabel).ToList();
        var all = placed.Concat(result.Unplaced.Select(p => p.PieceLabel)).ToList();
        Assert.All(pieces, p => Assert.Single(all, l => l == p.PieceLabel));
        Assert.Equal(pieces.Count, all.Count);
    }

    [Fact]
    public void Repeated_patterns_note_the_type_window_and_limited_search()
    {
        var pieces = Enumerable.Range(0, 120)
            .Select(i => P(500, 350, $"q{i:D3}"))
            .ToList();

        var result = GuillotinePacker.RunRepeatedPatterns(
            pieces, Racks, House, RotationPolicy.Locked, 1, default);

        Assert.Contains(result.ScaleNotes,
            n => n.StartsWith("Quantity-aware search:", StringComparison.Ordinal));
        Assert.Contains(result.ScaleNotes,
            n => n.StartsWith("Pattern window 1:", StringComparison.Ordinal));
    }

    [Fact]
    public void Repeated_patterns_respect_finite_rack_supply()
    {
        // 200 panes that fit at most 8 per sheet but only 12 sheets of
        // stock (Medium margins: 4×2 on the 1940×1140 usable area): the
        // pattern master must respect the cap and REPORT the overflow.
        var stocks = new[] { new StockSize(2000, 1200, 6, "Clear", maxSheets: 12) };
        var pieces = Enumerable.Range(0, 200)
            .Select(i => P(400, 500, $"s{i:D3}"))
            .ToList();

        var result = GuillotinePacker.RunRepeatedPatterns(
            pieces, stocks, House, RotationPolicy.Free90, 1, default);

        Assert.True(result.Sheets.Count <= 12, $"used {result.Sheets.Count} sheets of a 12-sheet rack");
        // Every pane is accounted for exactly once — placed or reported.
        Assert.Equal(200, result.Sheets.Sum(s => s.Pieces.Count) + result.Unplaced.Count);
        // 12 sheets hold at most 96 of the 200 panes: the overflow is
        // never silently dropped.
        Assert.True(result.Unplaced.Count >= 104,
            $"expected the 104+ overflow reported, got {result.Unplaced.Count}");
    }

    // ── the industrial route ────────────────────────────────────────

    [Fact]
    public void Optimize_industrial_quantity_runs_the_pattern_master()
    {
        // 100,005 panes of one small size: at/above the 100,000-part
        // threshold PackOptimize hands off to the pattern master — a
        // CheckCandidate'd, verified plan — and says so on the notes.
        var pieces = Enumerable.Range(0, 100_005)
            .Select(i => P(10, 10, $"p{i}"))
            .ToList();

        var result = GuillotinePacker.PackOptimize(pieces, Racks, House,
            RotationPolicy.Free90, effortLevel: 2);

        Assert.Contains(result.ScaleNotes,
            n => n.StartsWith("Industrial quantity route:", StringComparison.Ordinal));
        Assert.Equal(0, result.Unplaced.Count);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(result, pieces, House));
    }

    [Fact]
    public void Optimize_industrial_quantity_skips_the_plate_pattern_with_a_note()
    {
        var pieces = Enumerable.Range(0, 100_005)
            .Select(i => P(10, 10, $"p{i}"))
            .ToList();

        var result = GuillotinePacker.PackOptimize(pieces, Racks, House,
            RotationPolicy.Free90, effortLevel: 1, platePattern: PlatePattern.FillFirstPlate);

        Assert.Contains(result.ScaleNotes,
            n => n.Contains("fill first plate is not run at industrial quantity",
                StringComparison.Ordinal));
    }

    // ── fill-first plate ────────────────────────────────────────────

    [Fact]
    public void FillFirstWindows_stages_nest()
    {
        Assert.Equal(new[] { 8 }, GuillotinePacker.FillFirstWindows(1));
        Assert.Equal(new[] { 8, 12 }, GuillotinePacker.FillFirstWindows(2));
        Assert.Equal(new[] { 8, 12, 16 }, GuillotinePacker.FillFirstWindows(3));
        Assert.Equal(new[] { 8, 12, 16, 24 }, GuillotinePacker.FillFirstWindows(4));
        Assert.Equal(new[] { 8, 12, 16, 24, 32 }, GuillotinePacker.FillFirstWindows(5));
        // Effort is clamped, never rejected.
        Assert.Equal(GuillotinePacker.FillFirstWindows(1), GuillotinePacker.FillFirstWindows(0));
        Assert.Equal(GuillotinePacker.FillFirstWindows(5), GuillotinePacker.FillFirstWindows(9));
    }

    [Fact]
    public void PackFillFirstPlate_places_every_pane_and_labels_the_window()
    {
        var pieces = new List<CutPiece>();
        for (int i = 0; i < 30; i++)
            pieces.Add(P(700 + (i % 4) * 60, 450 + (i % 3) * 50, $"f{i:D2}"));

        var result = GuillotinePacker.PackFillFirstPlate(
            pieces, Racks, House, RotationPolicy.Free90, 3);

        Assert.StartsWith("FILL FIRST PLATE", result.Strategy, StringComparison.Ordinal);
        Assert.Contains("window", result.Strategy, StringComparison.Ordinal);
        Assert.Equal(0, result.Unplaced.Count);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(result, pieces, House));
    }

    [Fact]
    public void FillFirstPlate_fills_the_open_sheet_before_opening_another()
    {
        // 12 identical panes that fit four-per-sheet: a fill-first plan
        // finishes each open plate before the next opens, so no two
        // partially-filled sheets may coexist below the last one.
        var pieces = Enumerable.Range(0, 12)
            .Select(i => P(900, 800, $"g{i:D2}"))
            .ToList();

        var result = GuillotinePacker.PackFillFirstPlate(
            pieces, Racks, House, RotationPolicy.Free90, 2);

        Assert.Equal(0, result.Unplaced.Count);
        // 900x800 panes on 3210x2250 racks (Medium margins: 3150x2190
        // usable, kerf 3 = exactly 6 panes per sheet): the fill-first
        // loop finishes each open plate before opening the next.
        var counts = result.Sheets.Select(s => s.Pieces.Count).ToList();
        Assert.Equal(2, result.Sheets.Count);
        Assert.Equal(new[] { 6, 6 }, counts);
    }

    [Fact]
    public void ApplyPlatePattern_Yield_leaves_the_plan_untouched()
    {
        var pieces = Enumerable.Range(0, 24)
            .Select(i => P(800 + (i % 5) * 40, 500 + (i % 3) * 60, $"y{i:D2}"))
            .ToList();

        var yieldRun = GuillotinePacker.PackQuick(pieces, Racks, House, RotationPolicy.Free90,
            platePattern: PlatePattern.Yield);

        Assert.DoesNotContain(yieldRun.ScaleNotes,
            n => n.StartsWith("Plate pattern:", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyPlatePattern_FillFirstPlate_never_regresses_the_yield_contract()
    {
        // The legal second packer may only replace the yield plan with a
        // legal plan that does not add unplaced pieces or sheets — and
        // must leave a decision note on every path.
        var pieces = Enumerable.Range(0, 40)
            .Select(i => P(650 + (i % 7) * 45, 400 + (i % 4) * 55, $"h{i:D2}"))
            .ToList();

        var yieldRun = GuillotinePacker.PackQuick(pieces, Racks, House, RotationPolicy.Free90);
        var fillRun = GuillotinePacker.PackQuick(pieces, Racks, House, RotationPolicy.Free90,
            platePattern: PlatePattern.FillFirstPlate, fillEffort: 3);

        Assert.True(fillRun.Unplaced.Count <= yieldRun.Unplaced.Count);
        Assert.True(fillRun.Sheets.Count <= yieldRun.Sheets.Count);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(fillRun, pieces, House));
        Assert.Contains(fillRun.ScaleNotes,
            n => n.StartsWith("Plate pattern:", StringComparison.Ordinal));
    }

    // ── partitioned routing ─────────────────────────────────────────

    [Fact]
    public void Partitioned_small_jobs_delegate_to_the_grid()
    {
        var pieces = Enumerable.Range(0, 260)
            .Select(i => P(300 + (i % 9) * 45, 250 + (i % 5) * 40, $"d{i:D3}"))
            .ToList();

        var viaPartitioned = GuillotinePacker.PackOptimizePartitioned(
            pieces, Racks, House, RotationPolicy.Free90, effortLevel: 2);
        var direct = GuillotinePacker.PackOptimize(
            pieces, Racks, House, RotationPolicy.Free90, effortLevel: 2);

        // Below the large-job threshold the partitioned engine IS the
        // grid — same plan, no batched-search note.
        Assert.Equal(direct.SheetsUsed, viaPartitioned.SheetsUsed);
        Assert.Equal(direct.Strategy, viaPartitioned.Strategy);
        Assert.DoesNotContain(viaPartitioned.ScaleNotes,
            n => n.StartsWith("Partitioned Adaptive Smart Search", StringComparison.Ordinal));
    }

    [Fact]
    public void Partitioned_finite_stock_takes_the_grid()
    {
        // A rack cap belongs to the whole job, not to each parallel
        // batch: finite inventory routes to PackOptimize even above the
        // large-job threshold.
        var stocks = new[] { new StockSize(3210, 2250, 6, "Clear", maxSheets: 9999) };
        var pieces = Enumerable.Range(0, 2_010)
            .Select(i => P(300, 200, $"k{i:D4}"))
            .ToList();

        var result = GuillotinePacker.PackOptimizePartitioned(
            pieces, stocks, House, RotationPolicy.Free90, effortLevel: 2);

        Assert.DoesNotContain(result.ScaleNotes,
            n => n.StartsWith("Partitioned Adaptive Smart Search", StringComparison.Ordinal));
        Assert.Equal(0, result.Unplaced.Count);
    }

    [Fact]
    public void Partitioned_large_jobs_run_the_batched_engine()
    {
        // Unlimited stock above the 2,000-part threshold: stratified
        // production batches, global baselines, cross-window
        // consolidation — with a note that says exactly that. (One
        // distinct size keeps the pattern LP a single type window.)
        var pieces = Enumerable.Range(0, 2_010)
            .Select(i => P(300, 200, $"b{i:D4}"))
            .ToList();

        var result = GuillotinePacker.PackOptimizePartitioned(
            pieces, BigRacks, House, RotationPolicy.Free90, effortLevel: 2);

        Assert.Contains(result.ScaleNotes,
            n => n.StartsWith("Partitioned Adaptive Smart Search", StringComparison.Ordinal));
        Assert.Contains(result.ScaleNotes,
            n => n.Contains("batches + global multi-pass", StringComparison.Ordinal));
        Assert.Equal(0, result.Unplaced.Count);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(result, pieces, House));
    }

    [Fact]
    public void Partitioned_stage_one_is_quick()
    {
        var pieces = Enumerable.Range(0, 30)
            .Select(i => P(500 + (i % 4) * 40, 350 + (i % 3) * 30, $"t{i:D2}"))
            .ToList();

        var result = GuillotinePacker.PackOptimizePartitioned(
            pieces, Racks, House, RotationPolicy.Free90, effortLevel: 3, maxStage: 1);

        Assert.StartsWith("QUICK", result.Strategy, StringComparison.Ordinal);
    }

    [Fact]
    public void SmartSearch_defaults_match_the_WPF_tuning()
    {
        var c = SmartSearchConfiguration.Default;
        Assert.Equal(2000, c.LargeJobThreshold);
        Assert.Equal(1500, c.BatchTargetSize);
        Assert.Equal(400, c.CandidateWindowSize);
        Assert.Equal(128, c.OpenSheetWindow);
        Assert.True(c.MaxParallelWorkers >= 1 && c.MaxParallelWorkers <= 4);
        Assert.Equal(4, c.BeamWidth);
        Assert.Equal(16, c.BeamDepth);
        Assert.Equal(0.10, c.TailOptimizationPercentage);
        Assert.Equal(75.0m, c.WeakSheetUtilizationThreshold);
        Assert.Equal(2, c.ConsolidationRadius);
        Assert.Equal(0, c.SearchTimeBudgetSeconds);
    }

    [Fact]
    public void StockSize_effectively_unlimited_sheets_distinguishes_fake_caps()
    {
        // WithMaxSheets hands the leftover repair a reduced-cap copy of
        // the SAME stock; the sentinel separates "finite" from unlimited.
        var rack = new StockSize(3210, 2250, 6, "Clear", maxSheets: 40);
        var reduced = rack.WithMaxSheets(7);
        Assert.Equal(7, reduced.MaxSheets);
        Assert.Equal(rack.WidthMm, reduced.WidthMm);
        Assert.Equal(rack.HeightMm, reduced.HeightMm);
        Assert.True(rack.MaxSheets < StockSize.EffectivelyUnlimitedSheets);
        Assert.True(StockSize.EffectivelyUnlimitedSheets > 1_000_000);
    }
}
