// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/OptimizationBenchmarkTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// CALIBRATION BENCHMARK — pins the engine against the owner's REAL DGU
// job data (Spec 1: climaguard low-e + clear panes; Spec 2: obscure
// clear ramly panes). Floor settings: 6mm glass → LM/RM/TM/BM = 15mm,
// minimum breakout 15mm, kerf 3mm, rotation enabled (adaptive 0/90 mix).
//
// Reference results (owner's tool): Low-E 17 sheets @ 82.77%, Clear
// 16 sheets @ ~85%, Ramly 1 sheet on 2500×1875.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class OptimizationBenchmarkTests
    {
        private const int StockW = 3210;
        private const int StockH = 2250;
        private const int RamlyStockW = 2500;
        private const int RamlyStockH = 1875;

        private const string RefClear = "6mm clear ft glass";
        private const string RefLowE = "6mm climaguard hp neutral 70 low e ft glass";
        private const string RefRamly = "6mm obscure clear ramly ft glass";

        // Spec 1 — BIG group (rows W×H, qty per the job sheet).
        private static readonly (string Ref, int W, int H, int Qty)[] BigGroup =
        {
            ("11", 1050, 2127, 2), ("9", 1054, 2130, 2), ("10", 1055, 2130, 2),
            ("12", 800, 1798, 2), ("1", 905, 2536, 2), ("2", 1054, 1986, 2),
            ("4", 854, 1981, 2), ("7", 1003, 1857, 2), ("3", 856, 1970, 2),
            ("8", 844, 1457, 2), ("13", 650, 1382, 2), ("14", 650, 1372, 2),
            ("2", 605, 1471, 2), ("5", 553, 1448, 2), ("SA", 499, 1430, 2),
            ("S", 509, 1421, 2),
            ("R17", 1815, 725, 1), ("R8", 1515, 530, 1),
            ("R10", 2013, 828, 1), ("R10", 2513, 535, 1)
        };

        // Spec 1 — A group (all qty 2).
        private static readonly (string Ref, int W, int H)[] AGroup =
        {
            ("A2", 1055, 1985), ("A11", 1046, 2130), ("A9", 1050, 2130),
            ("A10", 1055, 2130), ("A7", 968, 1889), ("A4", 850, 1970),
            ("A3", 837, 1984), ("A1", 838, 2532), ("A14", 657, 1350),
            ("A8", 935, 1418), ("A13", 652, 1350), ("A5", 654, 1479),
            ("A6", 655, 1474), ("A12", 800, 1805)
        };

        // Spec 2 — SMALL group, ramly outers (all qty 1).
        private static readonly (string Ref, int W, int H)[] SmallGroup =
        {
            ("A7", 525, 380), ("A11", 725, 430), ("A13", 720, 430),
            ("A14", 720, 430), ("A18", 725, 430), ("A19", 725, 430),
            ("8", 520, 375), ("12", 725, 430), ("14", 710, 430),
            ("15", 730, 430), ("19", 720, 430), ("20", 720, 430)
        };

        private static MarginProfile SixMmMargins => MarginProfile.DefaultFor(6);

        private static List<StockSize> StockPool(params (int W, int H)[] sizes) =>
            sizes.Select(s => new StockSize { WidthMm = s.W, HeightMm = s.H }).ToList();

        private static List<CutPiece> Expand(
            IEnumerable<(string Ref, int W, int H, int Qty)> rows, string glassRef)
        {
            var list = new List<CutPiece>();
            foreach (var (rowRef, w, h, qty) in rows)
            {
                for (int i = 0; i < qty; i++)
                {
                    list.Add(new CutPiece
                    {
                        GlassRef = glassRef,
                        PieceLabel = qty > 1 ? $"{rowRef}-{i + 1}" : rowRef,
                        WidthMm = w,
                        HeightMm = h
                    });
                }
            }
            return list;
        }

        private static List<CutPiece> ClearPieces()
        {
            var pieces = Expand(BigGroup, RefClear);
            pieces.AddRange(Expand(AGroup.Select(t => (t.Ref, t.W, t.H, 2)), RefClear));
            return pieces;
        }

        private static List<CutPiece> RamlyPieces() =>
            Expand(SmallGroup.Select(t => (t.Ref, t.W, t.H, 1)), RefRamly);

        private static List<CutPiece> LowEPieces()
        {
            var pieces = ClearPieces();
            pieces.AddRange(RamlyPieces().Select(p => new CutPiece
            {
                GlassRef = RefLowE,
                PieceLabel = p.PieceLabel,
                WidthMm = p.WidthMm,
                HeightMm = p.HeightMm
            }));
            return pieces;
        }

        private static double Utilization(OptimizationResult r)
        {
            double glass = r.Sheets.Sum(s => s.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm));
            double stock = r.Sheets.Sum(s => (long)s.Stock.WidthMm * s.Stock.HeightMm);
            return stock == 0 ? 0 : glass / stock * 100.0;
        }

        // ── floor margin spec ───────────────────────────────────────────

        [Fact]
        public void Margins_DefaultFor6mm_MatchFloorSpec()
        {
            var m = SixMmMargins;
            Assert.Equal(15, m.LeftMm);
            Assert.Equal(15, m.RightMm);
            Assert.Equal(15, m.TopMm);
            Assert.Equal(15, m.BottomMm);
            Assert.Equal(15, m.MinBreakoutMm);
            Assert.Equal(3, m.KerfMm);
        }

        // ── benchmark: Low-E + clear panes = 17 sheets @ 82.77% ────────

        [Fact]
        public void Benchmark_LowE_MatchesReference_17Sheets_82Pct()
        {
            var result = GuillotinePacker.PackQuick(
                LowEPieces(), StockPool((StockW, StockH)), SixMmMargins, RotationPolicy.Free90);

            Assert.Empty(result.Unplaced);
            Assert.True(result.SheetsUsed <= 17, "Reference: Low-E fits in 17 sheets.");
            Assert.True(Utilization(result) >= 82.0, "Reference utilization ≈ 82.77%.");
            Assert.Empty(GuillotinePacker.Verify(result, SixMmMargins));
        }

        // ── benchmark: clear panes ──────────────────────────────────────

        [Fact]
        public void Benchmark_Clear_FitsWithin17Sheets()
        {
            // Owner's mature tool reaches 16 sheets (~85%) on this set —
            // the QUICK stage is pinned at ≤17 (within one sheet); the
            // OPTIMIZE levels continue chasing the last sheet.
            var result = GuillotinePacker.PackQuick(
                ClearPieces(), StockPool((StockW, StockH)), SixMmMargins, RotationPolicy.Free90);

            Assert.Empty(result.Unplaced);
            Assert.True(result.SheetsUsed <= 17, "Clear must stay within 17 sheets (reference 16).");
            Assert.True(Utilization(result) >= 79.0, "Clear utilization floor.");
            Assert.Empty(GuillotinePacker.Verify(result, SixMmMargins));
        }

        // ── benchmark: ramly outers = 1 sheet on 2500×1875 ────────────

        [Fact]
        public void Benchmark_Ramly_FitsSingleSmallestSheet()
        {
            var result = GuillotinePacker.PackQuick(
                RamlyPieces(),
                StockPool((RamlyStockW, RamlyStockH), (StockW, StockH)),
                SixMmMargins, RotationPolicy.Free90);

            Assert.Empty(result.Unplaced);
            Assert.Equal(1, result.SheetsUsed);
            Assert.Equal(RamlyStockW, result.Sheets[0].Stock.WidthMm);
            Assert.Equal(RamlyStockH, result.Sheets[0].Stock.HeightMm);
            Assert.True(Utilization(result) >= 70.0, "Reference ≈ 74–77% on 2500×1875.");
            Assert.Empty(GuillotinePacker.Verify(result, SixMmMargins));
        }

        // ── rotation policy behaviour ───────────────────────────────────

        [Fact]
        public void Benchmark_AdaptiveRotation_UsesBothOrientations()
        {
            var result = GuillotinePacker.PackQuick(
                LowEPieces(), StockPool((StockW, StockH)), SixMmMargins, RotationPolicy.Free90);

            Assert.Contains(result.Sheets.SelectMany(s => s.Pieces), p => p.Rotated);
            Assert.Contains(result.Sheets.SelectMany(s => s.Pieces), p => !p.Rotated);
        }

        [Fact]
        public void Benchmark_LockedRotation_ReportsUnplacedWithoutFailing()
        {
            var result = GuillotinePacker.PackQuick(
                LowEPieces(), StockPool((StockW, StockH)), SixMmMargins, RotationPolicy.Locked);

            // 905×2536 / 838×2532 pieces cannot fit unrotated on 3210×2250.
            Assert.NotEmpty(result.Unplaced);
            Assert.Empty(GuillotinePacker.Verify(result, SixMmMargins));
        }

        // ── OPTIMIZE stage must never regress QUICK ────────────────────

        [Fact]
        public void Benchmark_OptimizeStage_NeverRegressesQuick()
        {
            var quick = GuillotinePacker.PackQuick(
                LowEPieces(), StockPool((StockW, StockH)), SixMmMargins, RotationPolicy.Free90);
            var optimized = GuillotinePacker.PackOptimize(
                LowEPieces(), StockPool((StockW, StockH)), SixMmMargins, RotationPolicy.Free90, 3);

            Assert.Empty(optimized.Unplaced);
            Assert.True(optimized.SheetsUsed <= quick.SheetsUsed,
                "Multi-start optimization may equal or beat QUICK, never lose to it.");
            Assert.Empty(GuillotinePacker.Verify(optimized, SixMmMargins));
        }

        [Fact]
        public void Benchmark_Clear_OptimizeStage_StaysWithinQuickSheets()
        {
            var quick = GuillotinePacker.PackQuick(
                ClearPieces(), StockPool((StockW, StockH)), SixMmMargins, RotationPolicy.Free90);
            var optimized = GuillotinePacker.PackOptimize(
                ClearPieces(), StockPool((StockW, StockH)), SixMmMargins, RotationPolicy.Free90, 3);

            Assert.Empty(optimized.Unplaced);
            Assert.True(optimized.SheetsUsed <= quick.SheetsUsed);
            Assert.Empty(GuillotinePacker.Verify(optimized, SixMmMargins));
        }

        // ── determinism ─────────────────────────────────────────────────

        [Fact]
        public void Benchmark_LowE_Run_IsDeterministic()
        {
            var a = GuillotinePacker.PackQuick(
                LowEPieces(), StockPool((StockW, StockH)), SixMmMargins, RotationPolicy.Free90);
            var b = GuillotinePacker.PackQuick(
                LowEPieces(), StockPool((StockW, StockH)), SixMmMargins, RotationPolicy.Free90);

            Assert.Equal(a.SheetsUsed, b.SheetsUsed);
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
    }
}
