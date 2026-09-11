// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/OptimizationEngineTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// ORACLE TESTS for the glass cutting optimization engine (2026-09-12):
// every produced layout must pass Verify() — no overlaps, pieces inside
// the usable area (stock minus LM/RM/TM/BM), kerf respected between
// pieces, rotation only when allowed.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class OptimizationEngineTests
    {
        private static readonly StockSize Stock3210 = new()
        {
            WidthMm = 3210, HeightMm = 2250, ThicknessMm = 6, Color = "Clear",
            Label = "3210 × 2250"
        };

        private static MarginProfile Margins => MarginProfile.DefaultFor(6);

        private static CutPiece Piece(int w, int h, string reference, bool rotate = true) => new()
        {
            WidthMm = w, HeightMm = h, GlassRef = reference,
            PieceLabel = reference, AllowRotate = rotate, ThicknessMm = 6
        };

        // ══════════════════════════════════════════════════════════════
        //  BASIC PACKING + ORACLE
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Quick_pack_places_all_pieces_and_passes_the_oracle()
        {
            var pieces = Enumerable.Range(1, 8)
                .Select(i => Piece(800, 600, $"G-0{i}"))
                .ToList();

            var result = GuillotinePacker.PackQuick(
                pieces, new[] { Stock3210 }, Margins, RotationPolicy.Free90);

            Assert.True(result.Success);
            Assert.Empty(GuillotinePacker.Verify(result, Margins));
            Assert.Equal(8, result.Sheets.Sum(s => s.Pieces.Count));
            Assert.True(result.UtilizationPct > 0m);
        }

        [Fact]
        public void Pieces_stay_inside_the_margin_zone()
        {
            var result = GuillotinePacker.PackQuick(
                new[] { Piece(1200, 900, "G-01") },
                new[] { Stock3210 }, Margins, RotationPolicy.Locked);

            var placed = result.Sheets.Single().Pieces.Single();
            Assert.True(placed.X >= Margins.LeftMm);
            Assert.True(placed.Y >= Margins.TopMm);
            Assert.True(placed.X + placed.WidthMm <= 3210 - Margins.RightMm);
            Assert.True(placed.Y + placed.HeightMm <= 2250 - Margins.BottomMm);
        }

        [Fact]
        public void Kerf_forces_a_second_sheet_when_space_is_tight()
        {
            // Two 1000-wide pieces side by side need
            // 25 + 1000 + kerf(3) + 1000 + 25 = 2053mm of stock width.
            // At 2053 they share one sheet…
            var oneSheet = GuillotinePacker.PackQuick(
                new[] { Piece(1000, 1000, "A", rotate: false), Piece(1000, 1000, "B", rotate: false) },
                new[] { new StockSize { WidthMm = 2053, HeightMm = 1100 } },
                Margins, RotationPolicy.Locked);
            Assert.Equal(1, oneSheet.SheetsUsed);
            Assert.Empty(GuillotinePacker.Verify(oneSheet, Margins));

            // …and one millimetre less breaks the row — the kerf pushes
            // piece B onto its own sheet.
            var twoSheets = GuillotinePacker.PackQuick(
                new[] { Piece(1000, 1000, "A", rotate: false), Piece(1000, 1000, "B", rotate: false) },
                new[] { new StockSize { WidthMm = 2052, HeightMm = 1100 } },
                Margins, RotationPolicy.Locked);
            Assert.Equal(2, twoSheets.SheetsUsed);
            Assert.Empty(GuillotinePacker.Verify(twoSheets, Margins));
        }

        // ══════════════════════════════════════════════════════════════
        //  ROTATION POLICY
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Free90_rotates_a_piece_that_only_fits_turned()
        {
            // Usable = 3160 × 2200. Piece 2200×3000 fits ONLY rotated
            // (3000×2200).
            var rotated = GuillotinePacker.PackQuick(
                new[] { Piece(2200, 3000, "TALL") },
                new[] { Stock3210 }, Margins, RotationPolicy.Free90);

            Assert.True(rotated.Success);
            Assert.True(rotated.Sheets.Single().Pieces.Single().Rotated);

            var locked = GuillotinePacker.PackQuick(
                new[] { Piece(2200, 3000, "TALL") },
                new[] { Stock3210 }, Margins, RotationPolicy.Locked);

            Assert.False(locked.Success);
            Assert.Single(locked.Unplaced);
        }

        // ══════════════════════════════════════════════════════════════
        //  UTILIZATION + MULTI-STOCK
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Utilization_reports_used_over_stock_area()
        {
            var result = GuillotinePacker.PackQuick(
                new[]
                {
                    Piece(1500, 1000, "A"), Piece(1500, 1000, "B"),
                    Piece(1500, 1000, "C"), Piece(1500, 1000, "D")
                },
                new[] { Stock3210 }, Margins, RotationPolicy.Locked);

            Assert.Equal(1, result.SheetsUsed);
            // 4 × 1.5 m² on a 7.2225 m² sheet ≈ 83%.
            Assert.InRange(result.Sheets.Single().UtilizationPct, 80m, 86m);
            Assert.Empty(GuillotinePacker.Verify(result, Margins));
        }

        [Fact]
        public void Multi_stock_pool_picks_the_size_that_fits()
        {
            var small = new StockSize { WidthMm = 2000, HeightMm = 1500, Label = "small" };
            var large = new StockSize { WidthMm = 3210, HeightMm = 2250, Label = "large" };

            var result = GuillotinePacker.PackQuick(
                new[] { Piece(2500, 2000, "BIG", rotate: false) },
                new[] { small, large }, Margins, RotationPolicy.Locked);

            Assert.True(result.Success);
            Assert.Equal("large", result.Sheets.Single().Stock.Label);
        }

        [Fact]
        public void Stock_sheet_cap_is_respected()
        {
            var capped = new StockSize
            {
                WidthMm = 3210, HeightMm = 2250, MaxSheets = 1
            };

            // Four 1500×2000 pieces cannot share one 3210×2250 sheet —
            // with MaxSheets = 1 the extras are reported unplaced.
            var pieces = Enumerable.Range(1, 4)
                .Select(i => Piece(1500, 2000, $"P{i}", rotate: false))
                .ToList();

            var result = GuillotinePacker.PackQuick(
                pieces, new[] { capped }, Margins, RotationPolicy.Locked);

            Assert.Equal(1, result.SheetsUsed);
            Assert.True(result.Unplaced.Count > 0);
        }

        [Fact]
        public void Deterministic_runs_produce_identical_layouts()
        {
            var pieces = Enumerable.Range(1, 30)
                .Select(i => Piece(400 + (i % 5) * 130, 300 + (i % 3) * 170, $"G-{i}"))
                .ToList();

            var first = GuillotinePacker.PackQuick(pieces, new[] { Stock3210 }, Margins, RotationPolicy.Free90);
            var second = GuillotinePacker.PackQuick(pieces, new[] { Stock3210 }, Margins, RotationPolicy.Free90);

            Assert.Equal(first.SheetsUsed, second.SheetsUsed);
            for (int s = 0; s < first.Sheets.Count; s++)
            {
                Assert.Equal(
                    first.Sheets[s].Pieces.Select(p => (p.X, p.Y, p.WidthMm, p.HeightMm)),
                    second.Sheets[s].Pieces.Select(p => (p.X, p.Y, p.WidthMm, p.HeightMm)));
            }
        }
    }
}
