// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/SheetStore/GlassSheetLowStockTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Sheet store — the low-stock threshold has one home
//
// WHY THIS EXISTS
// ───────────────
// "Low stock" was declared twice with two values:
//
//     Agent/Tools/Read/GetInventoryTool.cs        LowStockThreshold = 2
//     Services/Dashboard/Core/DashboardMetricsService.cs        = 5
//
// Both read the same GlassSheet balance, so a sheet type holding 3 or 4 sheets
// was counted as low stock on the Overview dashboard and reported as fine by
// the AI agent — two answers to one question, from one dataset, at one moment.
// No exception, nothing in the log, nothing to notice until someone reconciles
// a reorder list against the screen.
//
// Both now read GlassSheetLowStock.Threshold. The first test pins the value;
// the second is the part that actually holds, because it fails if anyone
// reintroduces a local number.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using ProGlassAutomation.Models.SheetStore;
using Xunit;

namespace ProGlassAutomation.Tests.SheetStore
{
    public sealed class GlassSheetLowStockTests
    {
        /// <summary>
        /// The two files that decide whether a glass sheet is low stock. If a
        /// third one appears it must read the canonical threshold too, and be
        /// added here — that is the point of the list.
        /// </summary>
        private static readonly string[] Consumers =
        {
            "Agent/Tools/Read/GetInventoryTool.cs",
            "Services/Dashboard/Core/DashboardMetricsService.cs"
        };

        [Fact]
        public void Threshold_is_five()
        {
            // 5, not 2. The dashboard is the number an operator has been acting
            // on and its own comment records that 5 "matches the threshold the
            // Overview dashboard already used"; unifying on 2 would have
            // silently shrunk a count someone already trusts. Asserted so that
            // changing it is a deliberate edit to a test, not a quiet constant
            // bump in one of two files.
            Assert.Equal(5, GlassSheetLowStock.Threshold);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(4, true)]
        [InlineData(5, true)]     // at the threshold is already low
        [InlineData(6, false)]
        [InlineData(100, false)]
        public void IsLow_is_inclusive_of_the_threshold(int balance, bool expected)
        {
            // `<=`, matching both call sites this replaced. A sheet holding
            // exactly the threshold counts as low, because the next job takes
            // it under.
            Assert.Equal(expected, GlassSheetLowStock.IsLow(balance));
        }

        [Fact]
        public void Every_consumer_reads_the_canonical_threshold()
        {
            var offenders = Consumers
                .Select(rel => new { rel, text = Read(rel) })
                .Where(f => !f.text.Contains(
                    "ProGlassAutomation.Models.SheetStore.GlassSheetLowStock.Threshold",
                    StringComparison.Ordinal))
                .Select(f => f.rel)
                .ToList();

            Assert.True(
                offenders.Count == 0,
                "These files decide whether a glass sheet is low stock without " +
                "reading GlassSheetLowStock.Threshold. A local number here is how " +
                "the dashboard and the agent came to disagree in the first " +
                "place:\n  " + string.Join("\n  ", offenders));
        }

        [Fact]
        public void No_file_declares_its_own_low_stock_threshold()
        {
            // Catches a NEW consumer that this test's Consumers list does not
            // know about yet. Deliberately narrow: it looks for a constant
            // named *LowStockThreshold* assigned a literal, which is the exact
            // shape of the original defect, rather than trying to guess what a
            // threshold might be called.
            var root = Root();
            var offenders = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
                .Where(f => !f.StartsWith("Tests/", StringComparison.Ordinal))
                .Where(f => !f.Contains("/obj/", StringComparison.Ordinal))
                .Where(f => f != "Models/SheetStore/GlassSheetLowStock.cs")
                .Where(f => Read(f).Split('\n').Any(line =>
                {
                    var t = line.TrimStart();
                    if (t.StartsWith("//", StringComparison.Ordinal)) return false;
                    return t.Contains("LowStockThreshold =", StringComparison.Ordinal)
                        && !t.Contains("GlassSheetLowStock.Threshold", StringComparison.Ordinal);
                }))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            Assert.True(
                offenders.Count == 0,
                "These files assign their own low-stock threshold instead of " +
                "reading GlassSheetLowStock.Threshold:\n  " +
                string.Join("\n  ", offenders));
        }

        private static string Read(string relative)
        {
            var path = Path.Combine(
                Root(), relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"expected source file not found: {relative}");
            return File.ReadAllText(path);
        }

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }
}
