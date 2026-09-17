// ══════════════════════════════════════════════════════════════════════
//  FILE: Tests/Agent/OptimizationSmartPlacementTests.cs
//
//  Holds the smart placement policy (GuillotinePacker.SmartPlacement.cs) to
//  three separate promises:
//
//   1. NOTHING LEGACY CHANGED. The split maths was extracted from Commit into
//      SplitRemainder. Every existing caller passes minBreakout = 0, and at 0
//      the function must reproduce the old inline code exactly — same
//      rectangles, same sizes, same ORDER, because the ledger order feeds the
//      deterministic tie-breaks downstream. Tests 1-5 pin that literally.
//
//   2. THE NEW POLICY IS VALID. A plan is only an improvement if the oracle
//      accepts it: no overlaps, kerf respected, inside the margin zone.
//
//   3. THE NEW POLICY CANNOT REGRESS A RESULT. It is a candidate, not a
//      replacement, and Better() keeps the minimum — so PackOptimize with smart
//      placement enabled must never use more sheets, or leave more unplaced,
//      than PackQuick on the same input.
//
//  There is no dotnet SDK in this workspace, so these run on the developer's
//  machine. The pure-function tests are written out longhand on purpose: they
//  are the ones that would catch a silently altered split.
// ══════════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class OptimizationSmartPlacementTests
    {
        // ── fixtures ────────────────────────────────────────────────────
        private static CutPiece Piece(int w, int h, string glass = "6mm clear") =>
            new CutPiece { JobRef = "J1", GlassRef = glass, WidthMm = w, HeightMm = h };

        private static StockSize Stock(int w, int h, int maxSheets = 0) =>
            new StockSize { WidthMm = w, HeightMm = h, ThicknessMm = 6, MaxSheets = maxSheets };

        private static MarginProfile Margins(int breakout = 15, int kerf = 3) =>
            new MarginProfile
            {
                ThicknessMm = 6,
                LeftMm = 15,
                RightMm = 15,
                TopMm = 15,
                BottomMm = 15,
                KerfMm = kerf,
                MinBreakoutMm = breakout
            };

        /// <summary>3210 × 2250 rack, the benchmark's stock. Usable = 3180 × 2220.</summary>
        private static StockSize Rack() => Stock(3210, 2250);

        /// <summary>A realistic mixed job: repeats and one-offs, all well inside a rack.</summary>
        private static List<CutPiece> MixedJob()
        {
            var pieces = new List<CutPiece>();
            for (int i = 0; i < 6; i++) pieces.Add(Piece(1200, 800));
            for (int i = 0; i < 4; i++) pieces.Add(Piece(900, 600));
            for (int i = 0; i < 3; i++) pieces.Add(Piece(1500, 400));
            pieces.Add(Piece(2000, 1000));
            pieces.Add(Piece(750, 1100));
            pieces.Add(Piece(480, 320));
            return pieces;
        }

        private static List<GuillotinePacker.FreeRect> Split(
            int hostW, int hostH, int pieceW, int pieceH, int kerf, int breakout, SplitMode mode)
        {
            var output = new List<GuillotinePacker.FreeRect>();
            GuillotinePacker.SplitRemainder(
                new GuillotinePacker.FreeRect(0, 0, hostW, hostH),
                pieceW, pieceH, kerf, breakout, mode, output);
            return output;
        }

        // ══ 1. the extraction changed nothing ════════════════════════════

        [Fact]
        public void Horizontal_first_keeps_both_remainders_in_the_ledger_order()
        {
            // Host 1000×800, piece 400×300 at the origin, kerf 3.
            // rightW = 1000-400-3 = 597; bottomH = 800-300-3 = 497.
            // Horizontal-first keeps the FULL WIDTH below and the piece's own
            // height to the right — and adds the bottom rect FIRST, which is the
            // order the free-rect ledger has always had.
            var rects = Split(1000, 800, 400, 300, 3, 0, SplitMode.HorizontalFirst);

            Assert.Equal(2, rects.Count);
            Assert.Equal(0, rects[0].X);
            Assert.Equal(303, rects[0].Y);
            Assert.Equal(1000, rects[0].W);
            Assert.Equal(497, rects[0].H);
            Assert.Equal(403, rects[1].X);
            Assert.Equal(0, rects[1].Y);
            Assert.Equal(597, rects[1].W);
            Assert.Equal(300, rects[1].H);
        }

        [Fact]
        public void Vertical_first_mirrors_it_and_keeps_the_full_height_on_the_right()
        {
            var rects = Split(1000, 800, 400, 300, 3, 0, SplitMode.VerticalFirst);

            Assert.Equal(2, rects.Count);
            Assert.Equal(403, rects[0].X);
            Assert.Equal(0, rects[0].Y);
            Assert.Equal(597, rects[0].W);
            Assert.Equal(800, rects[0].H);   // full host height
            Assert.Equal(0, rects[1].X);
            Assert.Equal(303, rects[1].Y);
            Assert.Equal(400, rects[1].W);   // the piece's own width
            Assert.Equal(497, rects[1].H);
        }

        [Fact]
        public void Longer_leftover_follows_the_longer_remainder()
        {
            // rightW = 1000-200 = 800, bottomH = 400-300 = 100 → vertical.
            var vertical = Split(1000, 400, 200, 300, 0, 0, SplitMode.LongerLeftover);
            Assert.Equal(800, vertical[0].W);
            Assert.Equal(400, vertical[0].H);

            // rightW = 300-200 = 100, bottomH = 900-300 = 600 → horizontal.
            var horizontal = Split(300, 900, 200, 300, 0, 0, SplitMode.LongerLeftover);
            Assert.Equal(300, horizontal[0].W);
            Assert.Equal(600, horizontal[0].H);
        }

        [Fact]
        public void A_remainder_of_exactly_zero_is_never_added()
        {
            // Piece exactly fills the host width: rightW = 1000-997-3 = 0.
            var rects = Split(1000, 800, 997, 300, 3, 0, SplitMode.HorizontalFirst);

            Assert.Single(rects);
            Assert.Equal(1000, rects[0].W);
            Assert.Equal(497, rects[0].H);
        }

        [Fact]
        public void A_zero_breakout_keeps_every_positive_remainder_which_is_the_legacy_rule()
        {
            // 4 mm of leftover is useless stock, but keeping it is exactly what
            // the engine has always done — and every benchmark pin is measured
            // through that behaviour. This is the test that says the extraction
            // did not quietly start enforcing something.
            Assert.True(GuillotinePacker.Keepable(4, 500, 0));
            Assert.True(GuillotinePacker.Keepable(1, 1, 0));
            Assert.True(GuillotinePacker.Keepable(1, 1, -5));

            var rects = Split(1000, 800, 993, 300, 3, 0, SplitMode.HorizontalFirst);
            Assert.Equal(2, rects.Count);          // the 4 mm strip is still there
            Assert.Equal(4, rects[1].W);
        }

        // ══ 2. what the new rule does ════════════════════════════════════

        [Fact]
        public void A_remainder_below_the_minimum_breakout_becomes_trim_waste_not_stock()
        {
            // Same geometry as the test above, but the floor's 15 mm minimum is
            // now honoured: rightW = 1000-993-3 = 4 is below it, so it is not
            // offered as stock. Nothing can be placed into a strip the saw
            // cannot cut out.
            var rects = Split(1000, 800, 993, 300, 3, 15, SplitMode.HorizontalFirst);

            Assert.Single(rects);
            Assert.Equal(1000, rects[0].W);
            Assert.Equal(497, rects[0].H);
        }

        [Fact]
        public void Keepable_needs_both_dimensions_to_clear_the_minimum()
        {
            // A 900 × 4 strip is 3600 mm² of glass and still uncuttable: the
            // check is on the narrow dimension, not on area.
            Assert.False(GuillotinePacker.Keepable(900, 4, 15));
            Assert.False(GuillotinePacker.Keepable(4, 900, 15));
            Assert.True(GuillotinePacker.Keepable(900, 15, 15));
            Assert.True(GuillotinePacker.Keepable(15, 15, 15));
        }

        [Fact]
        public void The_audit_flags_a_thin_offcut_and_clears_a_compliant_one()
        {
            var margins = Margins(breakout: 15, kerf: 3);

            // Two pieces 10 mm apart: one kerf (3) is blade, so 7 mm of glass has
            // to be broken out — below the 15 mm minimum.
            var thin = Result(
                Sheet(1, Rack(), 15, 15, 3180, 2220,
                    Placed(15, 15, 500, 400), Placed(15, 425, 500, 400)));
            Assert.Single(GuillotinePacker.AuditBreakout(thin, margins));

            // Adjacent: the gap is exactly the kerf, nothing is broken out.
            var adjacent = Result(
                Sheet(1, Rack(), 15, 15, 3180, 2220,
                    Placed(15, 15, 500, 400), Placed(15, 418, 500, 400)));
            Assert.Empty(GuillotinePacker.AuditBreakout(adjacent, margins));

            // A 40 mm gap leaves a 37 mm offcut — waste, but cuttable waste.
            var compliant = Result(
                Sheet(1, Rack(), 15, 15, 3180, 2220,
                    Placed(15, 15, 500, 400), Placed(15, 455, 500, 400)));
            Assert.Empty(GuillotinePacker.AuditBreakout(compliant, margins));
        }

        [Fact]
        public void The_audit_stays_quiet_when_no_minimum_breakout_is_configured()
        {
            var margins = Margins(breakout: 0, kerf: 3);
            var thin = Result(
                Sheet(1, Rack(), 15, 15, 3180, 2220,
                    Placed(15, 15, 500, 400), Placed(15, 425, 500, 400)));

            Assert.Empty(GuillotinePacker.AuditBreakout(thin, margins));
        }

        // ══ 3. the policy produces valid, deterministic plans ════════════

        [Fact]
        public void Smart_placement_satisfies_the_oracle()
        {
            var margins = Margins();
            var result = GuillotinePacker.PackSmart(
                MixedJob(), new List<StockSize> { Rack() }, margins, RotationPolicy.Free90);

            Assert.NotEmpty(result.Sheets);
            Assert.Empty(GuillotinePacker.Verify(result, margins));
        }

        [Fact]
        public void Smart_placement_places_every_piece_of_a_job_that_fits()
        {
            var result = GuillotinePacker.PackSmart(
                MixedJob(), new List<StockSize> { Rack() }, Margins(), RotationPolicy.Free90);

            Assert.Empty(result.Unplaced);
            Assert.Equal(MixedJob().Count, result.Sheets.Sum(s => s.Pieces.Count));
        }

        [Fact]
        public void Smart_placement_is_deterministic()
        {
            var pieces = MixedJob();
            var stocks = new List<StockSize> { Rack() };
            var margins = Margins();

            var a = GuillotinePacker.PackSmart(pieces, stocks, margins, RotationPolicy.Free90);
            var b = GuillotinePacker.PackSmart(pieces, stocks, margins, RotationPolicy.Free90);

            Assert.Equal(a.SheetsUsed, b.SheetsUsed);
            for (int i = 0; i < a.Sheets.Count; i++)
            {
                Assert.Equal(a.Sheets[i].Pieces.Count, b.Sheets[i].Pieces.Count);
                for (int j = 0; j < a.Sheets[i].Pieces.Count; j++)
                {
                    Assert.Equal(a.Sheets[i].Pieces[j].X, b.Sheets[i].Pieces[j].X);
                    Assert.Equal(a.Sheets[i].Pieces[j].Y, b.Sheets[i].Pieces[j].Y);
                    Assert.Equal(a.Sheets[i].Pieces[j].Rotated, b.Sheets[i].Pieces[j].Rotated);
                }
            }
        }

        [Fact]
        public void Smart_placement_leaves_no_offcut_below_the_minimum_breakout()
        {
            var margins = Margins(breakout: 15);
            var result = GuillotinePacker.PackSmart(
                MixedJob(), new List<StockSize> { Rack() }, margins, RotationPolicy.Free90);

            // It never offers sub-breakout stock, so it cannot ask the floor to
            // break out a sliver.
            Assert.Empty(GuillotinePacker.AuditBreakout(result, margins));
        }

        [Fact]
        public void Smart_placement_reports_which_strategy_produced_the_plan()
        {
            var result = GuillotinePacker.PackSmart(
                MixedJob(), new List<StockSize> { Rack() }, Margins(), RotationPolicy.Free90);

            Assert.Equal(GuillotinePacker.SmartStrategyLabel, result.Strategy);
        }

        [Fact]
        public void Legacy_results_carry_no_strategy_so_nothing_reading_them_changes()
        {
            var quick = GuillotinePacker.PackQuick(
                MixedJob(), new List<StockSize> { Rack() }, Margins(), RotationPolicy.Free90);

            Assert.Equal("", quick.Strategy);
        }

        [Fact]
        public void Smart_placement_reports_unplaced_rather_than_failing_when_a_piece_is_too_big()
        {
            var pieces = new List<CutPiece> { Piece(5000, 4000), Piece(600, 400) };

            var result = GuillotinePacker.PackSmart(
                pieces, new List<StockSize> { Rack() }, Margins(), RotationPolicy.Free90);

            Assert.Single(result.Unplaced);
            Assert.Equal(5000, result.Unplaced[0].WidthMm);
            // The piece that does fit is still cut — one impossible part must not
            // take the rest of the job with it.
            Assert.Equal(1, result.Sheets.Sum(s => s.Pieces.Count));
            Assert.Equal(600, result.Sheets[0].Pieces[0].WidthMm);
        }

        [Fact]
        public void Smart_placement_respects_the_stock_cap()
        {
            // Four large pieces, but the rack is capped at one sheet.
            var pieces = new List<CutPiece>
            {
                Piece(2000, 1500), Piece(2000, 1500), Piece(2000, 1500), Piece(2000, 1500)
            };

            var result = GuillotinePacker.PackSmart(
                pieces, new List<StockSize> { Stock(3210, 2250, maxSheets: 1) },
                Margins(), RotationPolicy.Free90);

            Assert.Equal(1, result.SheetsUsed);
            Assert.NotEmpty(result.Unplaced);
        }

        [Fact]
        public void Smart_placement_with_locked_rotation_refuses_to_rotate()
        {
            // 3000 × 900 fits a 3180 × 2220 usable rack one way only.
            var pieces = new List<CutPiece> { Piece(3000, 900) };
            var margins = Margins();

            var locked = GuillotinePacker.PackSmart(
                pieces, new List<StockSize> { Rack() }, margins, RotationPolicy.Locked);
            Assert.Single(locked.Sheets);
            Assert.False(locked.Sheets[0].Pieces[0].Rotated);
            Assert.Empty(GuillotinePacker.Verify(locked, margins));
        }

        // ══ 4. it cannot regress a result ════════════════════════════════

        [Fact]
        public void Adding_smart_placement_never_costs_a_sheet()
        {
            var pieces = MixedJob();
            var stocks = new List<StockSize> { Rack() };
            var margins = Margins();

            var quick = GuillotinePacker.PackQuick(pieces, stocks, margins, RotationPolicy.Free90);
            var optimized = GuillotinePacker.PackOptimize(pieces, stocks, margins, RotationPolicy.Free90, 3);

            // Better() keeps the minimum, so a new candidate can only win by
            // being better. If this ever fails, a candidate was allowed to
            // replace the best instead of competing with it.
            Assert.True(optimized.Unplaced.Count <= quick.Unplaced.Count,
                $"unplaced grew: {quick.Unplaced.Count} -> {optimized.Unplaced.Count}");
            Assert.True(optimized.SheetsUsed <= quick.SheetsUsed,
                $"sheets grew: {quick.SheetsUsed} -> {optimized.SheetsUsed}");
            Assert.Empty(GuillotinePacker.Verify(optimized, margins));
        }

        [Fact]
        public void Optimize_stays_deterministic_with_the_smart_candidates_enabled()
        {
            var pieces = MixedJob();
            var stocks = new List<StockSize> { Rack() };
            var margins = Margins();

            var a = GuillotinePacker.PackOptimize(pieces, stocks, margins, RotationPolicy.Free90, 3);
            var b = GuillotinePacker.PackOptimize(pieces, stocks, margins, RotationPolicy.Free90, 3);

            Assert.Equal(a.SheetsUsed, b.SheetsUsed);
            Assert.Equal(a.Strategy, b.Strategy);
            for (int i = 0; i < a.Sheets.Count; i++)
            {
                Assert.Equal(a.Sheets[i].Pieces.Count, b.Sheets[i].Pieces.Count);
                for (int j = 0; j < a.Sheets[i].Pieces.Count; j++)
                {
                    Assert.Equal(a.Sheets[i].Pieces[j].X, b.Sheets[i].Pieces[j].X);
                    Assert.Equal(a.Sheets[i].Pieces[j].Y, b.Sheets[i].Pieces[j].Y);
                }
            }
        }

        [Fact]
        public void Effort_one_never_runs_the_smart_candidates()
        {
            // Level 1 must mirror PackQuick exactly — that is the contract
            // Benchmark_OptimizeStage_NeverRegressesQuick is built on, and the
            // smart candidates are gated behind effort >= 2.
            var pieces = MixedJob();
            var stocks = new List<StockSize> { Rack() };
            var margins = Margins();

            var quick = GuillotinePacker.PackQuick(pieces, stocks, margins, RotationPolicy.Free90);
            var level1 = GuillotinePacker.PackOptimize(pieces, stocks, margins, RotationPolicy.Free90, 1);

            Assert.Equal(quick.SheetsUsed, level1.SheetsUsed);
            Assert.Equal("", level1.Strategy);
        }

        // ── result builders for the audit tests ─────────────────────────
        private static PlacedPiece Placed(int x, int y, int w, int h) =>
            new PlacedPiece { Piece = Piece(w, h), X = x, Y = y, WidthMm = w, HeightMm = h };

        private static SheetLayout Sheet(int no, StockSize stock, int ux, int uy, int uw, int uh,
            params PlacedPiece[] pieces)
        {
            var layout = new SheetLayout
            {
                SheetNo = no,
                Stock = stock,
                UsableX = ux,
                UsableY = uy,
                UsableWidth = uw,
                UsableHeight = uh
            };
            layout.Pieces.AddRange(pieces);
            return layout;
        }

        private static OptimizationResult Result(SheetLayout sheet)
        {
            var result = new OptimizationResult { Rotation = RotationPolicy.Locked };
            result.Sheets.Add(sheet);
            return result;
        }
    }
}
