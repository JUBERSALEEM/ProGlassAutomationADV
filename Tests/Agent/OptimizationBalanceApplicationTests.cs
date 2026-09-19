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

        [Fact]
        public void The_packer_gate_reads_zero_as_unlimited_which_is_why_exhausted_racks_must_be_dropped()
        {
            // The single most dangerous line in this whole feature. If a rack
            // that has run out is written back as 0 instead of being removed,
            // this gate hands the engine infinite glass for exactly the rack
            // that has none.
            string packer = Read(Packer);
            Assert.Contains(
                ".Where(i => sheetsPerStock[i] < stocks[i].MaxSheets || stocks[i].MaxSheets == 0)",
                packer);

            // So the run ledger must drop, never zero.
            string run = Read(RunVm);
            string body = Slice(run, "private static List<(StockSize Stock, string Composition)> ApplyRunBalance(",
                "return result;");
            Assert.Contains("if (remaining <= 0)", body);
            Assert.Contains("continue;", body);
            Assert.DoesNotContain("WithMaxSheets(0)", body);
            Assert.DoesNotContain("WithMaxSheets(StockSize.UnlimitedSheets)", body);
        }

        // ══════════════════════════════════════════════════════════════
        //  H1 — the overflow that would have read a full rack as exhausted
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Duplicate_stock_balances_are_summed_in_long_and_clamped_not_in_int()
        {
            // Available defaults to 99999999. Summing ~22 rows at one size in
            // int passes int.MaxValue and wraps negative, and a negative cap
            // fails the packer gate on the first sheet — every piece would
            // come back unplaced from a rack that is full.
            string body = Slice(Read(RunVm),
                "private static List<StockSize> DistinctBySize(List<StockSize> stocks) =>",
                ".ToList();");

            Assert.Contains("long summed = 0;", body);
            Assert.Contains("summed >= int.MaxValue ? int.MaxValue : (int)summed", body);
            Assert.DoesNotContain("g.Sum(s => s.MaxSheets)", body);   // the int sum, gone

            // The run ledger accumulates in long for the same reason.
            string run = Read(RunVm);
            Assert.Contains(
                "new Dictionary<(int W, int H, int T, string Color), long>()", run);
            Assert.Contains("out long have", run);
        }

        [Fact]
        public void The_default_availability_never_enters_the_run_ledger()
        {
            // H2: 99999999 is finite, so a default rack would otherwise be
            // counted, deducted sheet by sheet, and fire a BALANCE warning on
            // a run where nobody ever typed a balance. That is noise on every
            // ordinary run, and noise is how the real warning gets ignored.
            string body = Slice(Read(RunVm),
                "var unlimitedKeys = new HashSet<(int W, int H, int T, string Color)>();",
                "int balanceConstrainedGroups = 0;");

            Assert.Contains("row.Stock.MaxSheets == StockSize.UnlimitedSheets", body);
            Assert.Contains("row.Stock.MaxSheets >= StockSize.EffectivelyUnlimitedSheets", body);
            Assert.Contains("unlimitedKeys.Add(BalanceKey(row.Stock));", body);

            // And DistinctBySize must not sum a no-limit rack either.
            string distinct = Slice(Read(RunVm),
                "private static List<StockSize> DistinctBySize(List<StockSize> stocks) =>",
                ".ToList();");
            Assert.Contains("st.MaxSheets >= StockSize.EffectivelyUnlimitedSheets", distinct);

            // One definition, and no hard-coded copies of the literal left
            // anywhere the default is produced — otherwise a future change to
            // the constant silently strands the ledger's "no balance was set"
            // recognition behind a stale literal.
            string stocks = Read(StocksVm);
            Assert.Contains("StockSize.EffectivelyUnlimitedSheets", stocks);
            Assert.DoesNotContain("\"99999999\"", stocks);

            string session = Read("ViewModels/Optimization/OptimizationViewModel.Session.cs");
            Assert.Contains("StockSize.EffectivelyUnlimitedSheets", session);
            Assert.DoesNotContain("\"99999999\"", session);
        }

        // ══════════════════════════════════════════════════════════════
        //  H3 — two glasses at one size are two racks, not one
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Stock_rows_are_merged_on_physical_glass_identity_not_on_size_alone()
        {
            // AddStep5Rack de-duplicates on (W, H, composition), so the pool
            // legitimately holds a 6mm and an 8mm rack at the same dimensions.
            // Merging on size alone summed them and claimed 20 sheets of a
            // glass that only has 10.
            string run = Read(RunVm);

            Assert.Contains("stocks.GroupBy(BalanceKey)", run);
            Assert.DoesNotContain("stocks.GroupBy(s => (s.WidthMm, s.HeightMm))", run);

            // The key is the same identity the ledger deducts against, so
            // merging and accounting cannot disagree about what a rack is.
            string key = Slice(run,
                "private static (int W, int H, int T, string Color) BalanceKey(StockSize s) =>",
                ";");
            Assert.Contains("s.ThicknessMm", key);
            Assert.Contains("s.Color", key);
        }

        [Fact]
        public void Racks_carry_their_thickness_so_the_dormant_oracle_can_wake()
        {
            // B3. VerifyComprehensive's piece-vs-stock thickness check is
            // guarded by Stock.ThicknessMm > 0, and the view model never set
            // it — so 8mm pieces on a 6mm rack passed the oracle that Phase 8
            // made the export gate.
            string packer = Read(Packer);
            Assert.Contains("p.Piece.ThicknessMm != sheet.Stock.ThicknessMm", packer);
            Assert.Contains("sheet.Stock.ThicknessMm > 0", packer);

            string margins = Read(MarginsVm);
            Assert.Contains("ParseThicknessMm(s.Composition)", margins);
            Assert.Contains("s.ThicknessMm > 0 ? s.ThicknessMm", margins);
        }

        // ══════════════════════════════════════════════════════════════
        //  B2 — the balance is per RUN, not per group
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Each_cutting_group_is_handed_what_is_left_not_what_the_run_started_with()
        {
            // RunCoreAsync used to pass the same untouched stockPool into
            // ResolveGroupStocks for every group, so a rack with Available 10
            // could be committed 10 sheets to 6mm clear AND 10 to 8mm clear.
            string run = Read(RunVm);

            Assert.Contains("var effectivePool = ApplyRunBalance(stockPool, balanceLedger,", run);
            Assert.Contains("ResolveGroupStocks(composition, effectivePool,", run);

            // The untouched pool must no longer be what groups are packed on.
            Assert.DoesNotContain("ResolveGroupStocks(composition, stockPool,\n                        AllowCrossCompositionStock, out string stockNote);", run);

            // And what a group actually cut is taken out, keyed off the rack
            // the packer really opened.
            Assert.Contains("result.Sheets.GroupBy(sh => BalanceKey(sh.Stock))", run);
            Assert.Contains("balanceLedger[used.Key] = have - used.Count();", run);
        }

        [Fact]
        public void A_group_starved_by_the_ledger_says_so_instead_of_blaming_missing_setup()
        {
            // H4: "no rack for this glass" sends an operator to Step 4; "the
            // rack ran out" sends them to the Sheet Store. Before this they
            // were the same message, so a starved group looked like a missing
            // sheet size and the operator went hunting for a rack that was
            // standing right there.
            string run = Read(RunVm);

            Assert.Contains("bool ranOutOfBalance = groupStocks.Count == 0 && poolReduced &&", run);
            Assert.Contains("rack balance exhausted earlier in this run", run);
            Assert.Contains("OracleLabel = ranOutOfBalance", run);

            // Both labels must still exist — the distinction is the point.
            Assert.Contains("NOT CUT", run);
            Assert.Contains("no rack for this glass", run);
        }

        [Fact]
        public void The_balance_note_names_the_groups_that_lost_out()
        {
            // H5: the ledger serves groups in cutting-list order, so which one
            // gets first call on a short rack is decided by list position. A
            // count alone could not tell an operator whether the group they
            // cared about was the one starved.
            string run = Read(RunVm);

            Assert.Contains("balanceConstrainedNames.Add(composition);", run);
            Assert.Contains("string.Join(\", \", balanceConstrainedNames.Take(4).ToArray())", run);
            Assert.Contains("BALANCE:", run);
        }

        // ══════════════════════════════════════════════════════════════
        //  Phase 10 — the balance has to reach the engine at all
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Every_consumer_of_sheet_store_inventory_filters_on_a_positive_balance()
        {
            // The cross-check that found Phase 10. Two of three consumers did
            // this and the Optimization module did not; pinning all three is
            // what stops one of them silently dropping the filter later.
            Assert.Contains(".Where(g => g.Balance > 0)", Read(ProductionBridge));
            Assert.Contains(".Where(g => g.Balance > 0)", Read(StockSelection));
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
        public void The_step5_feed_writes_a_real_balance_onto_the_racks_it_builds()
        {
            string live = Read(LiveRunVm);

            Assert.Contains("LookupStoreBalance(group.OuterGlassColor,", live);
            Assert.Contains("LookupStoreBalance(group.InnerGlassColor,", live);
            Assert.Contains("int? storeBalance = null)", live);
            Assert.Contains("if (storeBalance.HasValue)", live);
            Assert.Contains(
                "row.AvailableText = storeBalance.Value.ToString(CultureInfo.InvariantCulture);", live);
        }

        [Fact]
        public void A_balance_the_store_cannot_confirm_is_null_and_never_zero()
        {
            // 0 in MaxSheets is the UnlimitedSheets sentinel. Answering "the
            // store could not tell me" with 0 would hand the engine infinite
            // glass for a rack inventory could not price.
            string live = Read(LiveRunVm);
            Assert.Contains(": (int?)null;", live);

            // Behavioural half: the guards must answer before any database is
            // touched, so these cannot hang or throw in a test run.
            Assert.False(Services.ProformaInvoice.SheetStorePricingService
                .TryGetStockBalance(null, 6, 3210, 2250, out int b1));
            Assert.Equal(0, b1);

            Assert.False(Services.ProformaInvoice.SheetStorePricingService
                .TryGetStockBalance("   ", 0, 3210, 2250, out int b2));
            Assert.Equal(0, b2);

            Assert.False(Services.ProformaInvoice.SheetStorePricingService
                .TryGetStockBalance("clear", 6, 0, 0, out int b3));
            Assert.Equal(0, b3);

            Assert.False(Services.ProformaInvoice.SheetStorePricingService
                .TryResolveStockSheet(null, 6, out int w, out int h, out int b4));
            Assert.Equal(0, w);
            Assert.Equal(0, h);
            Assert.Equal(0, b4);
        }

        [Fact]
        public void The_jobs_tab_applies_a_balance_only_when_inventory_can_attribute_it()
        {
            // Phase 11. Step4Sheets carries no colour and inventory is keyed by
            // colour, so a size-only lookup would merge a Clear and a Low-E
            // rack at the same dimensions and hand the run a balance belonging
            // to glass it is not cutting. A wrong balance is worse than no
            // balance: it refuses groups that do have stock.
            string svc = Read(SheetStoreSvc);
            string body = Slice(svc,
                "public static bool TryGetUnambiguousStockBalance(",
                "catch (Exception ex)");
            Assert.Contains(".Distinct().ToList()", body);
            Assert.Contains("if (colours.Count != 1) return false;", body);

            string stocks = Read(StocksVm);
            Assert.Contains(
                ".TryGetUnambiguousStockBalance(ParseThicknessMm(comp), w, h, out int storeBalance))", stocks);

            // And applying it is never silent — the operator has to be able to
            // tell which Available figures came from inventory and which are
            // the unlimited default waiting to be typed over.
            Assert.Contains("balancesUnknown", stocks);
            Assert.Contains("could not be ", stocks);
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

        [Fact]
        public void Per_piece_rotation_lock_reaches_the_engine()
        {
            // Finding 8e: the packer always honoured CutPiece.AllowRotate, but
            // TryBuildInputs wrote the global SETTINGS choice for every row,
            // so the flag had no input path at all.
            string margins = Read(MarginsVm);
            Assert.Contains("AllowRotate = RotationEnabled && !p.RotationLocked", margins);

            // And it survives save-and-load, or a locked pane gets rotated
            // anyway the moment a session is reopened.
            string session = Read("ViewModels/Optimization/OptimizationViewModel.Session.cs");
            Assert.Contains("RotationLocked = p.RotationLocked", session);
            Assert.Contains("restored.RotationLocked = p.RotationLocked;", session);
        }
    }
}
