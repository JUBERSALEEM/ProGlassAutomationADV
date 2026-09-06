// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/Step9ComparisonReportTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Locks the 2026-09-06 Step 9 "GLASS / SPEC COMPARISON" report contract:
//
//   1. The six-section report stays CLOSED by default. Recalculation must
//      preserve an existing selection but must NEVER auto-select the first
//      spec row — the report opens only when the user clicks a per-group
//      comparison row.
//
//   2. The report is bounded (MaxHeight) so it scrolls inside its own
//      viewer instead of stretching the whole page, and it no longer shows
//      the raw "Source rows" count — the identity section keeps the spec
//      (build / glass code / dimensions summary) and its SQM, which are the
//      figures the estimator actually needs here.
//
//   3. The tab carries a "GO TO STEP 6 · OTHER CHARGES" button wired back
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
        public void Comparison_report_stays_closed_until_a_row_is_clicked()
        {
            // Recalculation preserves a live selection but must not fall
            // back to the first spec row — no auto-open by default.
            string target = Read(
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.TargetPricing.cs");

            Assert.Contains("SelectedTargetSpec != null", target,
                StringComparison.Ordinal);
            Assert.DoesNotContain("TargetSpecsView[0]", target,
                StringComparison.Ordinal);

            // The empty "No specification selected" state must remain mounted
            // so a closed report reads clearly instead of vanishing.
            string view = Read(
                "Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml");
            Assert.Contains("No specification selected", view,
                StringComparison.Ordinal);
            Assert.Contains("HasSelectedTargetSpec", view,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Comparison_report_is_bounded_and_shows_no_source_row_count()
        {
            string view = Read(
                "Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml");

            // Bounded: the inspector caps its height and scrolls internally.
            Assert.Contains("MaxHeight=\"600\"", view,
                StringComparison.Ordinal);

            // No raw source-row summary in the identity section. The spec
            // identity (build / glass code / dimensions) and its SQM stay.
            Assert.DoesNotContain("SourceRowsSummary", view,
                StringComparison.Ordinal);
            Assert.DoesNotContain("Source rows", view,
                StringComparison.Ordinal);
            Assert.Contains("GlassTypeCode", view,
                StringComparison.Ordinal);
            Assert.Contains("DimensionsSummary", view,
                StringComparison.Ordinal);
            Assert.Contains("AreaSqmDisplay", view,
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
