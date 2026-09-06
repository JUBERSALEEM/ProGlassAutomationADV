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
//   3. The proforma model is strictly three-way: BUY SHEET = USED SHEET +
//      WASTAGE (leftover). Wastage is DERIVED from cutting, never a
//      purchased "wastage sheet", and there is no offcut concept.
//
//   4. The Step 6 WASTAGE CHARGE is read from the matching Step 6 wastage
//      row's SOURCE (outer / inner / lam / middle), its SQM, SHEET QTY and
//      rate, then cross-checked — with explicit warnings when bought sheets
//      are less than the sheets needed (a Step 6 SHEET QTY mistake).
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
            // outer/inner pane identity in the glass type.
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
        public void Sheet_model_is_buy_used_and_wastage_leftover_only()
        {
            string vm = Read(
                "ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs");

            // BUY = USED + WASTAGE (leftover). No separate purchased
            // "wastage sheet" and no offcut.
            Assert.Contains("BuySheetPrice", vm, StringComparison.Ordinal);
            Assert.Contains("UsedSheetPrice", vm, StringComparison.Ordinal);
            Assert.Contains("WastageValue", vm, StringComparison.Ordinal);
            Assert.Contains("public decimal TotalBuyPrice", vm, StringComparison.Ordinal);
            Assert.Contains("public decimal TotalUsedPrice", vm, StringComparison.Ordinal);
            Assert.Contains("public decimal TotalWastageValue", vm, StringComparison.Ordinal);
            Assert.Contains("public decimal TotalStep6Charge", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("Offcut", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("offcut", vm, StringComparison.Ordinal);

            string view = Read(
                "Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml");
            Assert.Contains("BUY SHEET PRICE", view, StringComparison.Ordinal);
            Assert.Contains("USED SHEET PRICE", view, StringComparison.Ordinal);
            Assert.Contains("WASTAGE VALUE", view, StringComparison.Ordinal);
            Assert.Contains("WASTAGE (LEFTOVER)", view, StringComparison.Ordinal);
            Assert.Contains("STEP 6 WASTAGE CHARGE", view, StringComparison.Ordinal);
            Assert.DoesNotContain("OFFCUT", view, StringComparison.Ordinal);
            Assert.DoesNotContain("Offcut", view, StringComparison.Ordinal);
            Assert.Contains("OVERALL REPORT", view, StringComparison.Ordinal);
        }

        [Fact]
        public void Wastage_charge_is_read_from_the_step6_source_per_pane()
        {
            string vm = Read(
                "ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs");

            // The charge is resolved from the Step 6 wastage rows, matched
            // to the exact pane side (outer/inner/lam/middle) and its source
            // SQM, SHEET QTY and rate.
            Assert.Contains("WastagesSection?.WastageRows", vm, StringComparison.Ordinal);
            Assert.Contains("AvailableSources", vm, StringComparison.Ordinal);
            Assert.Contains("\"Outer (W1/H1)\"", vm, StringComparison.Ordinal);
            Assert.Contains("\"Inner (W2/H2)\"", vm, StringComparison.Ordinal);
            Assert.Contains("\"Lam Inner (W2/H2)\"", vm, StringComparison.Ordinal);
            Assert.Contains("\"Lam Outer (W2/H2)\"", vm, StringComparison.Ordinal);
            Assert.Contains("ResultAmount", vm, StringComparison.Ordinal);
            Assert.Contains("SpecNumber", vm, StringComparison.Ordinal);

            // Split across a row's checked sources by SQM share (the same
            // allocation Step 9 AppendWastageCostRows uses).
            Assert.Contains("src.SqmValue", vm, StringComparison.Ordinal);
            Assert.Contains("row.TotalSqm", vm, StringComparison.Ordinal);

            // The source identity (pane + colour + W1/H1) is surfaced so the
            // operator can see exactly which outer/inner pane was charged.
            Assert.Contains("src.DisplayLabel", vm, StringComparison.Ordinal);
            Assert.Contains("SheetQuantity", vm, StringComparison.Ordinal);
        }

        [Fact]
        public void Cross_check_warns_when_bought_sheets_are_short()
        {
            string vm = Read(
                "ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs");

            // Validation states, not blind results.
            Assert.Contains("HasShortfall", vm, StringComparison.Ordinal);
            Assert.Contains("HasChargeWithoutWastage", vm, StringComparison.Ordinal);
            Assert.Contains("HasWastageWithoutCharge", vm, StringComparison.Ordinal);
            Assert.Contains("public bool IsHealthy", vm, StringComparison.Ordinal);
            Assert.Contains("public string StatusLabel", vm, StringComparison.Ordinal);
            Assert.Contains("ShortfallMessages", vm, StringComparison.Ordinal);
            Assert.Contains("ReviewMessages", vm, StringComparison.Ordinal);

            // The shortfall message names the Step 6 SHEET QTY mistake.
            Assert.Contains("bought", vm, StringComparison.Ordinal);
            Assert.Contains("less than the", vm, StringComparison.Ordinal);
            Assert.Contains("Step 6 SHEET QTY", vm, StringComparison.Ordinal);

            string view = Read(
                "Views/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityView.xaml");
            Assert.Contains("SHORTFALL", view, StringComparison.Ordinal);
            Assert.Contains("REVIEW", view, StringComparison.Ordinal);
            Assert.Contains("STATUS", view, StringComparison.Ordinal);
            Assert.Contains("CROSS-CHECK", view, StringComparison.Ordinal);
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
