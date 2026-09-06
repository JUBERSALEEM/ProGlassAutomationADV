// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/Step9SheetDetailTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Locks the 2026-09-06 Step 9 "SHEET DETAIL" contract:
//
//   1. The sheet report stays CLOSED by default; clicking a sheet row opens
//      a FULL-SCREEN detail (whole tab content) with a "BACK TO SHEET LIST"
//      return — the same treatment the GLASS / SPEC COMPARISON tab got.
//
//   2. The GLASS TYPE column shows only the build family (SGU / DGU / LAM /
//      DGU+LAM) — never the outer/inner pane identity.
//
//   3. A proper three-way sheet-price reconciliation is produced per row and
//      as an overall report: BUY sheet price, USED sheet price and WASTAGE
//      sheet price — the wastage charge being READ FROM THE STEP 6 WASTAGE
//      SETUP (per pane side), never a free-hand guess.
//
//   4. There is deliberately NO offcut concept: glass bought but not used is
//      WASTAGE, full stop.
//
// These are source-pinning tests (the repo's Agent convention).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class Step9SheetDetailTests
    {
        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(LocateProjectRoot(),
                relative.Replace('/', Path.DirectorySeparatorChar)));

        [Fact]
        public void Sheet_detail_opens_full_screen_and_closes_back_to_the_list()
        {
            string vm = Read(
                "ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs");

            Assert.Contains("public ICommand SelectRowCommand { get; }", vm,
                StringComparison.Ordinal);
            Assert.Contains("public ICommand BackCommand { get; }", vm,
                StringComparison.Ordinal);
            Assert.Contains("public bool HasSelectedRow => SelectedRow != null;", vm,
                StringComparison.Ordinal);
            Assert.Contains("SelectedRow = null;", vm,
                StringComparison.Ordinal); // refresh closes any open detail

            string view = Read(
                "Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml");
            Assert.Contains("BACK TO SHEET LIST", view,
                StringComparison.Ordinal);
            Assert.Contains("Command=\"{Binding BackCommand}\"", view,
                StringComparison.Ordinal);
            Assert.Contains("SelectRowCommand", view,
                StringComparison.Ordinal);
            Assert.Contains("HasSelectedRow", view,
                StringComparison.Ordinal);
            Assert.DoesNotContain("Width=\"392\"", view,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Glass_type_column_shows_only_the_build_family()
        {
            string vm = Read(
                "ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs");

            // Short family labels only — never "LAMINATION" and never the
            // outer/inner pane identity.
            Assert.Contains("\"DGU+LAM\"", vm, StringComparison.Ordinal);
            Assert.Contains("if (dgu) return \"DGU\";", vm, StringComparison.Ordinal);
            Assert.Contains("if (lam) return \"LAM\";", vm, StringComparison.Ordinal);
            Assert.Contains("return \"SGU\";", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("LAMINATION", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("PaneNote", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("Outer pane", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("Inner pane", vm, StringComparison.Ordinal);

            string view = Read(
                "Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml");
            Assert.DoesNotContain("PaneNote", view, StringComparison.Ordinal);
            Assert.DoesNotContain("Outer pane", view, StringComparison.Ordinal);
            Assert.DoesNotContain("Inner pane", view, StringComparison.Ordinal);
        }

        [Fact]
        public void Sheet_price_reconciliation_is_the_three_way_buy_used_wastage_model()
        {
            string vm = Read(
                "ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs");

            // Per-row figures: BUY / USED / WASTAGE sheet prices only.
            Assert.Contains("BuySheetPrice", vm, StringComparison.Ordinal);
            Assert.Contains("UsedSheetPrice", vm, StringComparison.Ordinal);
            Assert.Contains("WastageSheetPrice", vm, StringComparison.Ordinal);
            Assert.Contains("BuySheets", vm, StringComparison.Ordinal);
            Assert.Contains("UsedSheets", vm, StringComparison.Ordinal);
            Assert.Contains("WastageSheets", vm, StringComparison.Ordinal);

            // Job-level totals + comparison.
            Assert.Contains("public decimal TotalBuyPrice", vm, StringComparison.Ordinal);
            Assert.Contains("public decimal TotalUsedPrice", vm, StringComparison.Ordinal);
            Assert.Contains("public decimal WastageSheetPrice", vm, StringComparison.Ordinal);
            Assert.Contains("public decimal UsedPriceSharePercent", vm, StringComparison.Ordinal);
            Assert.Contains("public decimal WastagePriceSharePercent", vm, StringComparison.Ordinal);

            // The offcut concept must NOT exist: bought-but-unused glass is
            // wastage, full stop.
            Assert.DoesNotContain("Offcut", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("offcut", vm, StringComparison.Ordinal);

            string view = Read(
                "Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml");
            Assert.Contains("BUY SHEET PRICE", view, StringComparison.Ordinal);
            Assert.Contains("USED SHEET PRICE", view, StringComparison.Ordinal);
            Assert.Contains("WASTAGE SHEET PRICE", view, StringComparison.Ordinal);
            Assert.DoesNotContain("OFFCUT", view, StringComparison.Ordinal);
            Assert.DoesNotContain("Offcut", view, StringComparison.Ordinal);
            Assert.Contains("OVERALL REPORT", view, StringComparison.Ordinal);
        }

        [Fact]
        public void Wastage_charge_is_read_from_the_step6_setup_per_pane_side()
        {
            string vm = Read(
                "ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs");

            // The wastage charge is resolved from the Step 6 wastage rows,
            // matched to the exact pane side — the same labels the canonical
            // Step 9 cost rows use (W1/H1 outer, W2/H2 inner/lam).
            Assert.Contains("WastagesSection?.WastageRows", vm, StringComparison.Ordinal);
            Assert.Contains("AvailableSources", vm, StringComparison.Ordinal);
            Assert.Contains("\"Outer (W1/H1)\"", vm, StringComparison.Ordinal);
            Assert.Contains("\"Inner (W2/H2)\"", vm, StringComparison.Ordinal);
            Assert.Contains("\"Lam Inner (W2/H2)\"", vm, StringComparison.Ordinal);
            Assert.Contains("\"Lam Outer (W2/H2)\"", vm, StringComparison.Ordinal);
            Assert.Contains("ResultAmount", vm, StringComparison.Ordinal);
            Assert.Contains("SpecNumber", vm, StringComparison.Ordinal);

            // A wastage row can cover several panes — the charge is split
            // across checked sources by SQM share so a multi-source row is
            // never double-counted (mirrors Step 9 AppendWastageCostRows).
            Assert.Contains("src.SqmValue", vm, StringComparison.Ordinal);
            Assert.Contains("rowTotalSqm", vm, StringComparison.Ordinal);

            // The view must say where the wastage charge comes from.
            string view = Read(
                "Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml");
            Assert.Contains("Step 6", view, StringComparison.Ordinal);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "ViewModels", "ProformaInvoice", "Step9ProfitLoss",
                    "SheetProfitabilityViewModel.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
