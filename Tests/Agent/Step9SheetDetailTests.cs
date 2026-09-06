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
//   3. A proper sheet-price reconciliation is produced per row and as an
//      overall report: TOTAL sheet price, USED sheet price, WASTAGE sheet
//      price, the offcut value and their comparison.
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
        public void Sheet_price_reconciliation_is_computed_and_reported()
        {
            string vm = Read(
                "ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs");

            // Per-row figures: total (purchase), used, offcut and wastage.
            Assert.Contains("public decimal UsedPrice", vm, StringComparison.Ordinal);
            Assert.Contains("public decimal OffcutPrice", vm, StringComparison.Ordinal);
            Assert.Contains("PurchaseCost = purchaseCostForThisRow,      // TOTAL sheet price", vm,
                StringComparison.Ordinal);
            Assert.Contains("UsedPrice = usedPriceForThisRow,            // USED sheet price", vm,
                StringComparison.Ordinal);
            Assert.Contains("OffcutPrice = offcutPriceForThisRow,        // OFFCUT value", vm,
                StringComparison.Ordinal);

            // Job-level totals + comparison.
            Assert.Contains("public decimal TotalUsedPrice", vm, StringComparison.Ordinal);
            Assert.Contains("public decimal TotalOffcutPrice", vm, StringComparison.Ordinal);
            Assert.Contains("public decimal NetWastageVsOffcut => WastageCharge - TotalOffcutPrice;", vm,
                StringComparison.Ordinal);
            Assert.Contains("public string NetWastageVsOffcutLabel", vm, StringComparison.Ordinal);

            string view = Read(
                "Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml");
            Assert.Contains("TOTAL SHEET PRICE", view, StringComparison.Ordinal);
            Assert.Contains("USED SHEET PRICE", view, StringComparison.Ordinal);
            Assert.Contains("WASTAGE SHEET PRICE", view, StringComparison.Ordinal);
            Assert.Contains("OFFCUT VALUE", view, StringComparison.Ordinal);
            Assert.Contains("NetWastageVsOffcutDisplay", view, StringComparison.Ordinal);
            Assert.Contains("OVERALL REPORT", view, StringComparison.Ordinal);
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
