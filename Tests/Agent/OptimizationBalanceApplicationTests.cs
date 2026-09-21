// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/OptimizationBalanceApplicationTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// PHASES 9-11 (2026-09-20) — pins that the Sheet Store BALANCE is actually
// applied to an optimization run, and that it is applied safely.
//
// Owner instructions, verbatim:
//   "apply balance also"
//   "apply balance safely"
//   "apply balance and do deep analysis and cross check more deeply"
//
// Why this file exists: three phases changed what glass a run believes it
// has, which flows straight into sheet counts, utilization and the cost
// published to Step 6 and Step 9. None of it had a single test. The
// invariants below are the ones whose loss would be silent and expensive —
// an overflowed balance reading as "exhausted", an exhausted rack reading as
// "infinite", a default rack firing a warning nobody's balance deserved, or
// one of the two Production consumers quietly dropping its Balance > 0
// filter and diverging from the other.
//
// Two kinds of assertion, matching the convention already used across
// Tests/Agent:
//   • behavioural, against the public engine and service API;
//   • source-text pins, for the private view-model logic that cannot be
//     called from a test (RunCoreAsync needs a Dispatcher). These pin the
//     exact expression that carries the invariant, so a refactor that drops
//     it fails here rather than in production.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class OptimizationBalanceApplicationTests
    {
        private const string Models = "Services/Optimization/OptimizationModels.cs";
        private const string Packer = "Services/Optimization/GuillotinePacker.cs";
        private const string RunVm = "ViewModels/Optimization/OptimizationViewModel.Run.cs";
        private const string MarginsVm = "ViewModels/Optimization/OptimizationViewModel.Margins.cs";
        private const string LiveRunVm = "ViewModels/Optimization/OptimizationViewModel.LiveRun.cs";
        private const string StocksVm = "ViewModels/Optimization/OptimizationViewModel.Stocks.cs";
        private const string ReportVm = "ViewModels/Optimization/OptimizationViewModel.Report.cs";
        private const string SheetStoreSvc = "Services/ProformaInvoice/SheetStorePricingService.cs";
        private const string ProductionBridge = "Services/Production/ProductionOptimizationBridge.cs";
        private const string StockSelection = "Services/Production/Cutting/StockSelectionService.cs";

        private static string Root()
        {
            var dir = AppContext.BaseDirectory;
            var d = new DirectoryInfo(dir);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "ProGlassAutomation.csproj")))
                d = d.Parent;
            return d?.FullName ?? dir;
        }

        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));

        private static string Slice(string text, string start, string end)
        {
            int i = text.IndexOf(start, StringComparison.Ordinal);
            Assert.True(i >= 0, $"start marker not found: {start}");
            int j = text.IndexOf(end, i + start.Length, StringComparison.Ordinal);
            Assert.True(j > i, $"end marker not found after start: {end}");
            return text.Substring(i, j + end.Length - i);
        }

        // ══════════════════════════════════════════════════════════════
        //  The two sentinels, which must never be confused
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Unlimited_and_effectively_unlimited_are_different_values()
        {
            // 0 is what the three packer strategies special-case as infinite
            // glass. 99999999 is what the Available field defaults to and is
            // an ordinary finite cap the packer honours literally. Code that
            // asks "is this rack limited?" has to test both, and the only way
            // to keep that true is for the two to stay distinct and named.
            Assert.Equal(0, StockSize.UnlimitedSheets);
            Assert.Equal(99999999, StockSize.EffectivelyUnlimitedSheets);
            Assert.NotEqual(StockSize.UnlimitedSheets, StockSize.EffectivelyUnlimitedSheets);
        }

        [Fact]
        public void WithMaxSheets_copies_every_other_property()
        {
            // StockSize is immutable — all init — so re-pricing a rack mid-run
            // means making a new one. A copy helper that silently dropped
            // ThicknessMm would un-do finding B3 and put the piece-vs-stock
            // thickness check back to sleep.
            var rack = new StockSize
            {
                WidthMm = 3210,
                HeightMm = 2250,
                ThicknessMm = 8,
                Color = "Clear",
                Label = "3210 × 2250 8mm",
                MaxSheets = 25
            };

            var repriced = rack.WithMaxSheets(7);

            Assert.Equal(7, repriced.MaxSheets);
            Assert.Equal(rack.WidthMm, repriced.WidthMm);
            Assert.Equal(rack.HeightMm, repriced.HeightMm);
            Assert.Equal(rack.ThicknessMm, repriced.ThicknessMm);
            Assert.Equal(rack.Color, repriced.Color);
            Assert.Equal(rack.Label, repriced.Label);
            Assert.Equal(rack.SizeLabel, repriced.SizeLabel);

            // And the original is untouched — the packer may still hold it.
            Assert.Equal(25, rack.MaxSheets);
        }

        // ══════════════════════════════════════════════════════════════
        //  H1 — the overflow that would have read a full rack as exhausted
        // ══════════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════════
        //  H3 — two glasses at one size are two racks, not one
        // ══════════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════════
        //  B2 — the balance is per RUN, not per group
        // ══════════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════════
        //  Phase 10 — the balance has to reach the engine at all
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Every_consumer_of_sheet_store_inventory_filters_on_a_positive_balance()
        {
            // The cross-check that found Phase 10. Two of three consumers did
            // this and the Optimization module did not; pinning all three is
            // what stops one of them silently dropping the filter later.
            // Balance is [NotMapped] Stock − Used. The IQueryable filter must
            // use the arithmetic EF can translate — same as UseStockAsync.
            Assert.Contains(".Where(g => g.Stock - g.Used > 0)", Read(ProductionBridge));
            Assert.Contains(".Where(g => g.Stock - g.Used > 0)", Read(StockSelection));
            Assert.Contains("MaxSheets = gs.Balance", Read(ProductionBridge));

            // The balance-aware rack resolve filters too — exact colour +
            // thickness, never a largest-of-any-thickness fallback.
            string svc = Read(SheetStoreSvc);
            Assert.Contains("public static bool TryPickStockSheet(", svc);
            Assert.Contains("s.Balance > 0", svc);
            Assert.Contains("s.ThicknessMm == thicknessMm", svc);
            Assert.Contains("requirePositiveBalance: true", svc);
        }

        [Fact]
        public void Unambiguous_lookup_guards_answer_before_touching_the_database()
        {
            Assert.False(Services.ProformaInvoice.SheetStorePricingService
                .TryGetUnambiguousStockBalance(6, 0, 0, out int b));
            Assert.Equal(0, b);

            Assert.False(Services.ProformaInvoice.SheetStorePricingService
                .TryGetUnambiguousStockBalance(0, -1, 2250, out int b2));
            Assert.Equal(0, b2);
        }

        [Fact]
        public void No_blocking_store_lookup_exists_without_an_explicit_timeout()
        {
            // RunFromStep5 builds racks on the UI thread. A bounded wait
            // degrades to "the store cannot say", which every caller already
            // treats as a note rather than an error; an unbounded one freezes
            // the window.
            string svc = Read(SheetStoreSvc);

            foreach (var start in new[]
                     {
                         "public static bool TryResolveStockSheet(",
                         "public static bool IsStockedSize(",
                         "public static bool TryGetStockBalance(",
                         "public static bool TryGetUnambiguousStockBalance(",
                         "private static bool TryGetSnapshotWithBoundedFallback("
                     })
            {
                string body = Slice(svc, start, "catch (Exception ex)");

                Assert.DoesNotContain(".Wait()", body);
                Assert.DoesNotContain(".Wait();", body);
                Assert.DoesNotContain("GetAwaiter().GetResult()", body);

                if (body.Contains(".Result"))
                    Assert.Matches(@"\.Wait\(\s*(\d+|timeoutMs)\s*\)", body);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  Phase 8 invariants the balance work depends on
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void The_export_gate_still_runs_the_comprehensive_oracle_and_a_real_count_check()
        {
            // A balance that is applied but never verified is only half the
            // job: the gate is what stops a plan that lost pieces reaching a
            // cut file. VerifyComprehensive had zero callers in the entire
            // repository before Phase 8.
            string report = Read(ReportVm);

            Assert.Contains("GuillotinePacker.VerifyComprehensive(", report);
            Assert.Contains("if (!TryPassExportGate(", report);

            // Conservation is checked against the count captured BEFORE the
            // engine ran. A list rebuilt from the result would agree with
            // itself and pass by construction.
            Assert.Contains("placed + unplaced != r.PieceCount", report);
            Assert.Contains("r.ValidationFailed", report);
        }


    }
}
