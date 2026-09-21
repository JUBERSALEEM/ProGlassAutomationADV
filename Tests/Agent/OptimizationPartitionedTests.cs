// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/OptimizationPartitionedTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// SCALE TESTS FOR 10K, 25K, AND 50K+ PARTITIONED ADAPTIVE SMART SEARCH
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class OptimizationPartitionedTests
    {
        private const int StockW = 3210;
        private const int StockH = 2250;

        private static readonly MarginProfile Margins = new MarginProfile
        {
            ThicknessMm = 6,
            LeftMm = 15,
            RightMm = 15,
            TopMm = 15,
            BottomMm = 15,
            KerfMm = 3,
            MinBreakoutMm = 15
        };

        private static readonly StockSize Stock = new StockSize
        {
            WidthMm = StockW,
            HeightMm = StockH,
            ThicknessMm = 6,
            Color = "6mm",
            Label = "3210 × 2250",
            MaxSheets = 0
        };

        private static List<StockSize> Stocks => new List<StockSize> { Stock };

        private static List<CutPiece> GenerateJob(int count, int seed = 50000)
        {
            var rnd = new Random(seed);
            var list = new List<CutPiece>(count);
            for (int i = 0; i < count; i++)
            {
                int b = rnd.Next(100);
                int w, h;
                if (b < 35) { w = rnd.Next(400, 850); h = rnd.Next(300, 750); }
                else if (b < 70) { w = rnd.Next(700, 1400); h = rnd.Next(500, 1000); }
                else if (b < 90) { w = rnd.Next(1200, 1900); h = rnd.Next(700, 1100); }
                else { w = rnd.Next(1800, 2100); h = rnd.Next(800, 1100); }

                list.Add(new CutPiece
                {
                    JobRef = "PARTITION_TEST",
                    GlassRef = "6mm clear ft glass",
                    SpecLabel = "S1",
                    Position = (i + 1).ToString(),
                    Color = "6mm",
                    ThicknessMm = 6,
                    WidthMm = w,
                    HeightMm = h,
                    AllowRotate = true,
                    PieceLabel = $"P_{i + 1}"
                });
            }
            return list;
        }

        [Fact]
        public void PartitionedOptimizer_10K_Parts_TerminatesAndPassesOracle()
        {
            int count = 10000;
            var pieces = GenerateJob(count, seed: 10001);

            var sw = Stopwatch.StartNew();
            var result = GuillotinePacker.PackOptimizePartitioned(
                pieces, Stocks, Margins, RotationPolicy.Free90, effortLevel: 3);
            sw.Stop();

            Assert.True(result.Sheets.Count > 0);
            int placedCount = result.Sheets.Sum(s => s.Pieces.Count);
            Assert.Equal(count, placedCount + result.Unplaced.Count);

            // Oracle verification: kerf, margins, boundary, no overlaps
            var problems = GuillotinePacker.VerifyComprehensive(result, pieces, Margins);
            Assert.False(result.HasValidationFailure);
            Assert.Empty(result.Unplaced);
            Assert.Empty(problems);
        }

        [Fact]
        public void PartitionedOptimizer_25K_Parts_TerminatesAndConservesQuantity()
        {
            int count = 25000;
            var pieces = GenerateJob(count, seed: 25002);

            var result = GuillotinePacker.PackOptimizePartitioned(
                pieces, Stocks, Margins, RotationPolicy.Free90, effortLevel: 3);

            Assert.True(result.Sheets.Count > 0);
            int placedCount = result.Sheets.Sum(s => s.Pieces.Count);
            Assert.Equal(count, placedCount + result.Unplaced.Count);

            var problems = GuillotinePacker.VerifyComprehensive(result, pieces, Margins);
            Assert.False(result.HasValidationFailure);
            Assert.Empty(result.Unplaced);
            Assert.Empty(problems);
        }

        [Fact]
        public void PartitionedOptimizer_50K_Parts_TerminatesAndProducesNoOracleViolations()
        {
            int count = 50000;
            var pieces = GenerateJob(count, seed: 50003);

            var result = GuillotinePacker.PackOptimizePartitioned(
                pieces, Stocks, Margins, RotationPolicy.Free90, effortLevel: 3);

            Assert.True(result.Sheets.Count > 0);
            int placedCount = result.Sheets.Sum(s => s.Pieces.Count);
            Assert.Equal(count, placedCount + result.Unplaced.Count);

            var problems = GuillotinePacker.VerifyComprehensive(result, pieces, Margins);
            Assert.False(result.HasValidationFailure);
            Assert.Empty(result.Unplaced);
            Assert.Empty(problems);

            Assert.NotEmpty(result.ScaleNotes);
            Assert.Contains("Partitioned Adaptive Smart Search", result.ScaleNotes[0]);
        }

        [Fact]
        public void PartitionedOptimizer_CancellationMidRun_StopsGracefully()
        {
            var pieces = GenerateJob(10000, seed: 40004);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAny<OperationCanceledException>(() =>
                GuillotinePacker.PackOptimizePartitioned(
                    pieces, Stocks, Margins, RotationPolicy.Free90, 3, cts.Token));
        }
    }
}
