// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/Step9ComparisonReportTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Locks the 2026-09-06 Step 9 "GLASS / SPEC COMPARISON" report contract:
//
//   1. The spec detail stays CLOSED by default. Recalculation must
//      preserve an existing selection but must NEVER auto-select the first
//      spec row — the detail opens only when the user clicks a per-group
//      comparison row.
//
//   2. The detail opens as a FULL-SCREEN module view (the whole tab
//      content, with a "BACK TO COMPARISON" return), not a 392 px right
//      sidebar. It keeps the six sections under the page scroller.
//
//   3. The detail shows the spec identity (build, dimensions summary) and
//      its SQM/price figures — no raw "Source rows" count and no "Glass
//      code" row.
//
//   4. The tab carries a "GO TO STEP 6 · OTHER CHARGES" button wired back
//      to the wizard (the report repeatedly references Step 6 U-insert /
//      wastage rows).
//
// These are source-pinning tests (the repo's Agent convention).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class Step9ComparisonReportTests
    {
        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(LocateProjectRoot(),
                relative.Replace('/', Path.DirectorySeparatorChar)));

        [Fact]
        public void Spec_detail_stays_closed_until_a_row_is_clicked()
        {
            // Recalculation preserves a live selection but must not fall
            // back to the first spec row — no auto-open by default.
            string target = Read(
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.TargetPricing.cs");

            Assert.Contains("SelectedTargetSpec != null", target,
                StringComparison.Ordinal);
            Assert.DoesNotContain("TargetSpecsView[0]", target,
                StringComparison.Ordinal);

            // The two states are toggled off the selection: the list is the
            // default, the detail only appears once a row is selected.
            string view = Read(
                "Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml");
            Assert.Contains("HasSelectedTargetSpec", view,
                StringComparison.Ordinal);
            Assert.Contains("InverseBoolToVisibilityConverter", view,
                StringComparison.Ordinal);
            Assert.Contains("BoolToVisibilityConverter", view,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Spec_detail_opens_as_a_full_screen_not_a_sidebar()
        {
            string view = Read(
                "Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml");

            // No right-sidebar column remains; the detail takes the full
            // module width and returns via an explicit back button.
            Assert.DoesNotContain("Width=\"392\"", view,
                StringComparison.Ordinal);
            Assert.Contains("BACK TO COMPARISON", view,
                StringComparison.Ordinal);
            Assert.Contains("Command=\"{Binding BackToComparisonCommand}\"", view,
                StringComparison.Ordinal);

            string vm = Read(
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.TargetPricing.cs");
            Assert.Contains("public ICommand BackToComparisonCommand { get; }", vm,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Spec_detail_shows_spec_and_sqm_but_no_glass_code_or_source_rows()
        {
            string view = Read(
                "Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml");

            // No raw source-row summary and no glass-code row: the detail
            // keeps the spec identity (build, dimensions summary) and its
            // SQM/price figures only.
            Assert.DoesNotContain("SourceRowsSummary", view,
                StringComparison.Ordinal);
            Assert.DoesNotContain("Source rows", view,
                StringComparison.Ordinal);
            Assert.DoesNotContain("Glass code", view,
                StringComparison.Ordinal);
            Assert.DoesNotContain("GlassTypeCode", view,
                StringComparison.Ordinal);

            Assert.Contains("DimensionsSummary", view,
                StringComparison.Ordinal);
            Assert.Contains("AreaSqmDisplay", view,
                StringComparison.Ordinal);
            Assert.Contains("BuildTypeLabel", view,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Comparison_tab_offers_a_jump_to_step_six()
        {
            string view = Read(
                "Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml");
            Assert.Contains("GO TO STEP 6 · OTHER CHARGES", view,
                StringComparison.Ordinal);
            Assert.Contains("Command=\"{Binding GoToStep6Command}\"", view,
                StringComparison.Ordinal);

            string vm = Read(
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.TargetPricing.cs");
            Assert.Contains("public ICommand GoToStep6Command { get; }", vm,
                StringComparison.Ordinal);
            Assert.Contains("NavigateToStep6Requested", vm,
                StringComparison.Ordinal);

            // The wizard shell wires the jump to its own step navigation.
            string shell = Read("ViewModels/ProformaInvoice/ProformaInvoiceViewModel.cs");
            Assert.Contains(
                "Step9VM.NavigateToStep6Requested = () => GoToStep(6);",
                shell, StringComparison.Ordinal);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "ViewModels", "ProformaInvoice", "Step9ProfitLoss",
                    "ProfitLossStepViewModel.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
