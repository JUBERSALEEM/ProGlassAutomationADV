// ═══════════════════════════════════════════════════════════════════════
//  Phase 25b — the grid candidate families, golden tests:
//   • RunSheetFill — sheet-at-a-time fill, windowed scanning still
//     places every piece
//   • RunWallSeed — 3-across tall walls (maxside ≥ 2050, minside
//     800–1200) seed exactly three wall panes per sheet
//   • RunMaxRects — kerf-inflated non-guillotine BSSF stays inside
//     Verify (margins, kerf, no overlap)
//   • RunBestSplit / RunBestSplitBeam — per-sheet split selection and
//     the beam; the empty-winner contract (a stalled beam reports
//     everything unplaced rather than losing pieces silently)
//   • RunCapacityStrips — columns and rows: every sheet carries its
//     own cut sequence with stages 1..3, gap-separation holds
//   • PackSmart — capacity-preserving placement, breakout honoured,
//     deterministic
//   • the QUICK portfolio integration (shelf + strips + pattern beam)
//     and the 25b reconciliation (the grid path sets the Verified
//     flags through RecordValidation)
// Pure domain — no database, no filesystem.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Optimization;

namespace ProGlass.Saas.Domain.Tests.Optimization;

public sealed class CandidateFamiliesTests
{
    private static readonly StockSize[] Racks =
    {
        new(3210, 2250, 6, "Clear"),
        new(2250, 3210, 6, "Clear")
    };

    private static MarginProfile House => MarginProfile.DefaultFor(6);

    private static CutPiece P(int w, int h, string label = "") =>
        new("JOB-1", "A-01", "6mm Clear", "Glass", "Clear", 6, w, h, true, label);

    private static List<CutPiece> MixedBatch() => new()
    {
        P(2200, 1100, "01"), P(2100, 900, "02"), P(2000, 850, "03"),
        P(1800, 800, "04"), P(1700, 750, "05"), P(1600, 700, "06"),
        P(1500, 650, "07"), P(1400, 600, "08"), P(1300, 550, "09"),
        P(1200, 500, "10"), P(1100, 450, "11"), P(1000, 400, "12"),
        P(900, 350, "13"), P(800, 300, "14"), P(700, 250, "15"),
        P(600, 200, "16"), P(500, 180, "17"), P(450, 160, "18"),
        P(400, 140, "19"), P(350, 120, "20"), P(300, 110, "21"),
        P(250, 100, "22"), P(2200, 400, "23"), P(1100, 1100, "24")
    };

    private static string LayoutSignature(OptimizationResult result) =>
        string.Join("|", result.Sheets.SelectMany(s => s.Pieces.Select(p => p.Piece.PieceLabel)));

    private static void AssertValidPlan(
        OptimizationResult result, IReadOnlyList<CutPiece> pieces, MarginProfile margins)
    {
        Assert.Equal(0, result.Unplaced.Count);
        Assert.Equal(pieces.Count,
            result.Sheets.Sum(s => s.Pieces.Count) + result.Unplaced.Count);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(result, pieces, margins));
    }

    // ── RunSheetFill ────────────────────────────────────────────────

    [Fact]
    public void SheetFill_fills_sheet_at_a_time_and_stays_valid()
    {
        var pieces = MixedBatch();

        var result = GuillotinePacker.RunSheetFill(
            pieces, Racks, House, RotationPolicy.Free90, SplitMode.HorizontalFirst);

        Assert.Equal("SHEET-FILL", result.Strategy);
        AssertValidPlan(result, pieces, House);
        // Deterministic: the same job produces the same plan.
        var again = GuillotinePacker.RunSheetFill(
            pieces, Racks, House, RotationPolicy.Free90, SplitMode.HorizontalFirst);
        Assert.Equal(LayoutSignature(result), LayoutSignature(again));
    }

    [Fact]
    public void SheetFill_window_limits_scanning_not_quantity()
    {
        // A candidate window of 2 must never drop pieces: the window
        // bounds the per-placement scan, never the input.
        var pieces = Enumerable.Range(1, 8)
            .Select(i => P(400 + i * 10, 300 + i * 10, $"p{i:00}"))
            .ToList<CutPiece>();

        var result = GuillotinePacker.RunSheetFill(
            pieces, Racks, House, RotationPolicy.Free90, SplitMode.HorizontalFirst,
            candidateWindowSize: 2);

        Assert.Equal(0, result.Unplaced.Count);
        Assert.Equal(pieces.Count, result.Sheets.Sum(s => s.Pieces.Count));
    }

    // ── RunWallSeed ─────────────────────────────────────────────────

    [Fact]
    public void WallSeed_seeds_exactly_three_walls_per_sheet()
    {
        // 2100×1000: maxside 2100 ≥ 2050, minside 1000 within 800–1200 —
        // a wall pane. Six of them seed two sheets of three across.
        var pieces = Enumerable.Range(1, 6)
            .Select(i => P(2100, 1000, $"w{i}"))
            .ToList<CutPiece>();

        var result = GuillotinePacker.RunWallSeed(
            pieces, Racks, House, RotationPolicy.Free90, SplitMode.HorizontalFirst);

        Assert.Equal("WALL-SEED", result.Strategy);
        AssertValidPlan(result, pieces, House);
        Assert.Equal(2, result.Sheets.Count);
        Assert.All(result.Sheets, s => Assert.Equal(3, s.Pieces.Count));
    }

    [Fact]
    public void WallSeed_ignores_panes_outside_the_wall_band()
    {
        // 2100×1300: minside 1300 > 1200 — NOT a wall. Six of them must
        // not be force-fed three-across (which would not fit anyway);
        // they pack through the ordinary leftover/new-sheet paths.
        var pieces = Enumerable.Range(1, 6)
            .Select(i => P(2100, 1300, $"b{i}"))
            .ToList<CutPiece>();

        var result = GuillotinePacker.RunWallSeed(
            pieces, Racks, House, RotationPolicy.Free90, SplitMode.HorizontalFirst);

        Assert.Equal("WALL-SEED", result.Strategy);
        AssertValidPlan(result, pieces, House);
        // No sheet holds a trio of these panes (3 × 1300 = 3900 > 2220
        // usable height; 3 × 2100 + kerf > 3180 usable width).
        Assert.All(result.Sheets, s => Assert.True(s.Pieces.Count <= 2));
    }

    // ── RunMaxRects ─────────────────────────────────────────────────

    [Fact]
    public void MaxRects_respects_kerf_and_margins()
    {
        var pieces = MixedBatch();

        var result = GuillotinePacker.RunMaxRects(
            GuillotinePacker.Sort(pieces, GuillotinePacker.OrderBy.Area),
            Racks, House, RotationPolicy.Free90);

        Assert.Equal("MAXRECTS-BSSF", result.Strategy);
        AssertValidPlan(result, pieces, House);
    }

    // ── RunBestSplit + the beam ─────────────────────────────────────

    [Fact]
    public void BestSplit_selects_per_sheet_and_stays_valid()
    {
        var pieces = MixedBatch();

        var result = GuillotinePacker.RunBestSplit(
            pieces, Racks, House, RotationPolicy.Free90);

        Assert.Equal("BEST-SPLIT", result.Strategy);
        AssertValidPlan(result, pieces, House);

        var remainderAware = GuillotinePacker.RunBestSplit(
            pieces, Racks, House, RotationPolicy.Free90, remainderAware: true);
        Assert.Equal("BEST-SPLIT", remainderAware.Strategy);
        AssertValidPlan(remainderAware, pieces, House);
    }

    [Fact]
    public void BestSplitBeam_explores_without_losing_pieces()
    {
        var pieces = MixedBatch();

        var result = GuillotinePacker.RunBestSplitBeam(
            pieces, Racks, House, RotationPolicy.Free90, beam: 4);

        Assert.Equal("BEST-SPLIT-BEAM", result.Strategy);
        AssertValidPlan(result, pieces, House);

        var again = GuillotinePacker.RunBestSplitBeam(
            pieces, Racks, House, RotationPolicy.Free90, beam: 4);
        Assert.Equal(LayoutSignature(result), LayoutSignature(again));
    }

    [Fact]
    public void BestSplitBeam_stalled_beam_reports_everything_unplaced()
    {
        // One capped sheet, two pieces that only fit one per sheet: the
        // beam opens a sheet for the first, then every expansion is
        // refused by the cap. The WPF contract: no winner means ALL
        // pieces come back unplaced — nothing is silently lost, and the
        // degenerate candidate loses the Better() comparison on the
        // unplaced axis.
        var capped = new StockSize(600, 400, 6, "Clear", MaxSheets: 1);
        var pieces = new List<CutPiece> { P(550, 350, "A"), P(550, 350, "B") };

        var result = GuillotinePacker.RunBestSplitBeam(
            pieces, new[] { capped }, House, RotationPolicy.Free90, beam: 4);

        Assert.Equal("BEST-SPLIT-BEAM", result.Strategy);
        Assert.Equal(0, result.Sheets.Count);
        Assert.Equal(2, result.Unplaced.Count);
    }

    // ── RunCapacityStrips ───────────────────────────────────────────

    [Fact]
    public void CapacityStrips_columns_place_cut_and_stay_valid()
    {
        var pieces = new List<CutPiece>
        {
            P(2200, 1100, "a1"), P(2200, 1100, "a2"),
            P(1000, 500, "b1"), P(1000, 500, "b2"),
            P(2200, 400, "c1")
        };

        var result = GuillotinePacker.RunCapacityStrips(
            pieces, Racks, House, RotationPolicy.Free90, transpose: false,
            CancellationToken.None);

        Assert.Equal("CAPACITY STRIPS (columns)", result.Strategy);
        AssertValidPlan(result, pieces, House);
        // PHASE 0: the strip path records its own cuts — no sheet
        // without a cut sequence, stages bounded by the 3-stage strip
        // grammar (separator 1, in-strip 2, sliver 3).
        Assert.All(result.Sheets, s => Assert.NotEmpty(s.Cuts));
        Assert.All(result.Sheets, s => Assert.All(s.Cuts,
            c => Assert.InRange(c.Stage, 1, 3)));
    }

    [Fact]
    public void CapacityStrips_rows_transpose_the_same_batch()
    {
        var pieces = new List<CutPiece>
        {
            P(2200, 1100, "a1"), P(2200, 1100, "a2"),
            P(1000, 500, "b1"), P(1000, 500, "b2"),
            P(2200, 400, "c1")
        };

        var result = GuillotinePacker.RunCapacityStrips(
            pieces, Racks, House, RotationPolicy.Free90, transpose: true,
            CancellationToken.None);

        Assert.Equal("CAPACITY STRIPS (rows)", result.Strategy);
        AssertValidPlan(result, pieces, House);
        Assert.All(result.Sheets, s => Assert.NotEmpty(s.Cuts));
    }

    [Fact]
    public void CapacityStrips_separates_strips_by_a_cuttable_gap()
    {
        // House margins: MinBreakout 15, kerf 3 → gap 21 between strips,
        // so the offcut a separator actually cuts out is 18 mm — above
        // the breakout floor. Two strips on one sheet must therefore sit
        // at least (width + gap) apart on the strip axis.
        var pieces = new List<CutPiece>
        {
            P(2200, 1100, "a1"), P(2200, 1100, "a2"), P(2200, 1100, "a3")
        };

        var result = GuillotinePacker.RunCapacityStrips(
            pieces, Racks, House, RotationPolicy.Free90, transpose: false,
            CancellationToken.None);

        AssertValidPlan(result, pieces, House);
        int gap = House.MinBreakoutMm + 2 * House.KerfMm;
        foreach (var sheet in result.Sheets)
        {
            var columns = sheet.Pieces
                .GroupBy(p => p.X)
                .OrderBy(g => g.Key)
                .ToList();
            for (int i = 1; i < columns.Count; i++)
            {
                int previousWidth = columns[i - 1].Max(p => p.WidthMm);
                Assert.True(columns[i].Key - (columns[i - 1].Key + previousWidth) >= gap,
                    $"strip gap {columns[i].Key - (columns[i - 1].Key + previousWidth)} < {gap}");
            }
        }
    }

    // ── PackSmart ───────────────────────────────────────────────────

    [Fact]
    public void SmartPlacement_is_valid_deterministic_and_breakout_clean()
    {
        var pieces = MixedBatch();

        var result = GuillotinePacker.PackSmart(
            pieces, Racks, House, RotationPolicy.Free90);

        Assert.Equal("SMART PLACEMENT", result.Strategy);
        AssertValidPlan(result, pieces, House);
        // The point of the policy: no offcut thinner than the floor's
        // minimum breakout is left cuttable (AuditBreakout is empty).
        Assert.Empty(GuillotinePacker.AuditBreakout(result, House));

        var again = GuillotinePacker.PackSmart(
            pieces, Racks, House, RotationPolicy.Free90);
        Assert.Equal(LayoutSignature(result), LayoutSignature(again));
    }

    [Fact]
    public void SmartPlacement_windowed_scan_keeps_every_piece()
    {
        var pieces = Enumerable.Range(1, 8)
            .Select(i => P(400 + i * 10, 300 + i * 10, $"p{i:00}"))
            .ToList<CutPiece>();

        var result = GuillotinePacker.PackSmart(
            pieces, Racks, House, RotationPolicy.Free90,
            candidateWindowSize: 2);

        Assert.Equal(0, result.Unplaced.Count);
        Assert.Equal(pieces.Count, result.Sheets.Sum(s => s.Pieces.Count));
    }

    // ── the QUICK portfolio + the grid wiring ───────────────────────

    [Fact]
    public void Quick_portfolio_places_cuts_and_validates()
    {
        var pieces = MixedBatch();

        var quick = GuillotinePacker.PackQuick(pieces, Racks, House, RotationPolicy.Free90);

        Assert.Equal(0, quick.Unplaced.Count);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(quick, pieces, House));
        // Every sheet carries a cut sequence — the shelf records cuts in
        // Commit, the strips record their own, the beam records through
        // Commit, and anything else falls to the derivation net.
        Assert.All(quick.Sheets, s => Assert.NotEmpty(s.Cuts));
        // The scope note names what is still pending.
        Assert.Contains(quick.ScaleNotes, n => n.StartsWith("SaaS port (Phase 25b)"));
    }

    [Fact]
    public void Optimize_grid_sets_the_verified_flags()
    {
        // 25b reconciliation: the grid validates through CheckCandidate,
        // which used to leave ConservationVerified/GeometryVerified
        // false — cutting reports read those flags and showed "Not
        // verified" for clean OPTIMIZE plans. RecordValidation derives
        // them (WPF verbatim).
        var pieces = MixedBatch();

        var optimized = GuillotinePacker.PackOptimize(
            pieces, Racks, House, RotationPolicy.Free90, effortLevel: 3);

        Assert.Empty(GuillotinePacker.VerifyComprehensive(optimized, pieces, House));
        Assert.True(optimized.ConservationVerified);
        Assert.True(optimized.GeometryVerified);
        Assert.True(optimized.ConservationChecked);
        Assert.True(optimized.GeometryChecked);
    }

    [Fact]
    public void Optimize_effort2_runs_the_advanced_families_validly()
    {
        // Stage 2 adds SheetFill/WallSeed/BestSplit/SmartPlacement to
        // the grid; the winner is still one of the named candidates and
        // the plan still validates.
        var pieces = MixedBatch();

        var optimized = GuillotinePacker.PackOptimize(
            pieces, Racks, House, RotationPolicy.Free90, effortLevel: 2);

        Assert.Equal(0, optimized.Unplaced.Count);
        Assert.Empty(GuillotinePacker.VerifyComprehensive(optimized, pieces, House));
        Assert.True(
            optimized.Strategy.StartsWith("QUICK SHELF") ||
            optimized.Strategy.StartsWith("GRID") ||
            optimized.Strategy.StartsWith("CAPACITY STRIPS") ||
            optimized.Strategy.StartsWith("PATTERN BEAM") ||
            optimized.Strategy.StartsWith("SHEET-FILL") ||
            optimized.Strategy.StartsWith("WALL-SEED") ||
            optimized.Strategy.StartsWith("BEST-SPLIT") ||
            optimized.Strategy.StartsWith("SMART PLACEMENT") ||
            optimized.Strategy.StartsWith("MAXRECTS-BSSF"),
            $"unexpected strategy: {optimized.Strategy}");
    }
}
