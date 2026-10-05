// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/JobProfitabilityNavigationTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the Job Profitability row-navigation contract introduced with the
// full-screen detail page:
//
//   • SINGLE click on a row  → opens the existing 420px detail sidebar
//                              (JobDetailPanel bound to JobDetail VM).
//   • DOUBLE click on a row  → opens the full-screen detail page
//                              (JobFullDetailView), the same pattern the
//                              Step 9 Profit & Loss module uses for its
//                              full-screen spec report (STATE 2), with a
//                              BACK TO JOBS affordance.
//
// Both surfaces must read the SAME JobDetail VM so the figures always
// agree. These are source-level contract tests (the sandbox cannot build
// WPF), so they assert on the committed sources rather than the running UI.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class JobProfitabilityNavigationTests
    {
        [Fact]
        public void View_model_exposes_distinct_single_and_double_click_commands()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "ViewModels/JobProfitability/JobProfitabilityViewModel.cs"));

            // Single-click sidebar path is preserved.
            Assert.Contains("SelectRowCommand", src);

            // New double-click full-page path + back affordance.
            Assert.Contains("OpenFullDetailCommand", src);
            Assert.Contains("CloseFullDetailCommand", src);
            Assert.Contains("IsFullDetailOpen", src);
            Assert.Contains("HasFullDetail", src);

            // Double-click loads the SAME JobDetail VM the sidebar uses.
            Assert.Contains("JobDetail.LoadJobDetail(row.ProformaId)", src);
        }

        [Fact]
        public void Row_supports_single_click_sidebar_and_double_click_full_page()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/JobProfitability/JobProfitabilityView.xaml"));

            // Single click still opens the sidebar.
            Assert.Contains("MouseAction=\"LeftClick\"", src);
            Assert.Contains("DataContext.SelectRowCommand", src);

            // Double click opens the full-screen detail.
            Assert.Contains("MouseAction=\"LeftDoubleClick\"", src);
            Assert.Contains("DataContext.OpenFullDetailCommand", src);

            // Back affordance to the jobs list.
            Assert.Contains("← BACK TO JOBS", src);
            Assert.Contains("CloseFullDetailCommand", src);

            // Both layers are present: the existing sidebar panel and the
            // full-screen detail page.
            Assert.Contains("JobDetailPanel", src);
            Assert.Contains("JobFullDetailView", src);
        }

        [Fact]
        public void Full_detail_page_reads_the_same_job_detail_view_model()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/JobProfitability/JobProfitabilityView.xaml"));

            // The full page and the sidebar bind the SAME VM so the numbers
            // can never disagree.
            Assert.Contains("DataContext=\"{Binding JobDetail}\"", src);
        }

        [Fact]
        public void Double_click_gesture_is_hosted_on_a_control_not_a_border()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/JobProfitability/JobProfitabilityView.xaml"));

            // Border is not a Control and has no MouseDoubleClick class
            // handler; the proven pattern in every other GodView here is to
            // attach the double-click gesture to a Control. The row template
            // wraps its Border in a ContentControl and puts the InputBindings
            // there.
            Assert.Contains("<ContentControl.InputBindings>", src);
            Assert.Contains("MouseAction=\"LeftDoubleClick\"", src);
        }

        [Fact]
        public void Missing_target_note_binds_the_bool_flag_not_the_visibility_enum()
        {
            var root = LocateProjectRoot();

            foreach (var rel in new[]
            {
                "Views/JobProfitability/JobDetailPanel.xaml",
                "Views/JobProfitability/JobFullDetailView.xaml"
            })
            {
                var src = File.ReadAllText(Path.Combine(root, rel));

                // The "target data missing" note must invert a real bool.
                Assert.Contains(
                    "HasTargetData, Converter={StaticResource InverseBoolToVisibilityConverter}",
                    src);

                // Feeding the Visibility enum (TargetPanelVisibility) through
                // the inverse BOOL converter always fell through to Visible,
                // so the note was shown even when target data existed.
                Assert.DoesNotContain(
                    "TargetPanelVisibility, Converter={StaticResource InverseBoolToVisibilityConverter}",
                    src);
            }
        }

        [Fact]
        public void Detail_load_guard_prevents_duplicate_reads_for_the_same_invoice()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "ViewModels/JobProfitability/JobDetailViewModel.cs"));

            // A double-click fires two single-click toggles plus the
            // full-detail command; without a guard that starts three identical
            // DB reads for the same invoice.
            Assert.Contains("_loadingProformaId", src);
            Assert.Contains(
                "string.Equals(_loadingProformaId, proformaId, StringComparison.Ordinal)",
                src);
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
                "Could not locate the ProGlassAutomation project root.");
        }
    }
}
