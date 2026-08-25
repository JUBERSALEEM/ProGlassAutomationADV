// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Dashboard/DashboardModernizationTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Dashboard modernization (Aug 2026)
//
// Hermetic source-file tests that lock the "modern analytics app" pass on
// the seven dashboards. They do NOT boot WPF or touch a DB — they read the
// XAML / theme / chart source relative to the project root and assert the
// upgrade is present, so a future edit that drops the modern look fails.
//
// What is locked:
//   1. TrendChartControl exposes SeriesEndBrush, and ResolveSeriesBrush()
//      builds a top→bottom LinearGradientBrush from the primary to the end
//      brush (the modern gradient-bar fill).
//   2. Every dashboard bar chart that uses SeriesBrush=AccentBrush now also
//      sets SeriesEndBrush=AccentDarkBrush (blue gradient bars).
//   3. The Overview dashboard uses the DashHeroBlueBrush gradient hero card
//      with the white-on-blue Factory pulse number.
//   4. DashHeroBlueBrush exists in BOTH Light and Dark themes.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Dashboard
{
    public class DashboardModernizationTests
    {
        private static readonly string[] DashboardViews =
        {
            "Views/Dashboard/Overview/OverviewDashboardView.xaml",
            "Views/Dashboard/ProformaInvoice/PiDashboardView.xaml",
            "Views/Dashboard/SheetStore/SheetStoreDashboardView.xaml",
            "Views/Dashboard/JobOrder/JoDashboard/JoDashboardView.xaml",
            "Views/Dashboard/DeliveryNote/DnDashboard/DnDashboardView.xaml",
            "Views/Dashboard/TaxInvoice/TiDashboard/TiDashboardView.xaml",
        };

        [Fact]
        public void TrendChart_exposes_the_gradient_capability()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Controls/TrendChart/TrendChartControl.xaml.cs"));

            Assert.Contains("SeriesEndBrush", src);
            Assert.Contains("SeriesEndBrushProperty", src);
            // ResolveSeriesBrush must build a top→bottom gradient when an
            // end brush is provided.
            Assert.Contains("ResolveSeriesBrush", src);
            Assert.Contains("LinearGradientBrush", src);
            Assert.Contains("StartPoint = new Point(0, 0)", src);
            Assert.Contains("EndPoint = new Point(0, 1)", src);
        }

        /// <summary>
        /// Every bar chart that uses the accent brush must also set an end
        /// brush, so the series renders as a blue gradient (the "modern app"
        /// look) instead of a flat single colour.
        /// </summary>
        [Theory]
        [InlineData("Views/Dashboard/Overview/OverviewDashboardView.xaml")]
        [InlineData("Views/Dashboard/ProformaInvoice/PiDashboardView.xaml")]
        [InlineData("Views/Dashboard/SheetStore/SheetStoreDashboardView.xaml")]
        [InlineData("Views/Dashboard/JobOrder/JoDashboard/JoDashboardView.xaml")]
        [InlineData("Views/Dashboard/DeliveryNote/DnDashboard/DnDashboardView.xaml")]
        [InlineData("Views/Dashboard/TaxInvoice/TiDashboard/TiDashboardView.xaml")]
        public void All_bar_charts_use_the_blue_gradient(string rel)
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root, rel));

            // Every SeriesBrush=AccentBrush must be paired with a
            // SeriesEndBrush=AccentDarkBrush.
            int primary = Regex.Matches(src, @"SeriesBrush=""\{DynamicResource AccentBrush\}""").Count;
            int end = Regex.Matches(src, @"SeriesEndBrush=""\{DynamicResource AccentDarkBrush\}""").Count;
            Assert.True(primary > 0,
                $"{rel}: expected at least one accent-brushed bar chart.");
            Assert.Equal(primary, end);
        }

        [Fact]
        public void Overview_uses_the_blue_gradient_hero_card()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/Dashboard/Overview/OverviewDashboardView.xaml"));

            Assert.Contains("DashHeroBlueBrush", src);
            Assert.Contains("FACTORY PULSE", src);
            // The hero number is white on the blue gradient.
            Assert.Contains("Foreground=\"White\"", src);
            Assert.Contains("Foreground=\"#CCFFFFFF\"", src);
        }

        [Fact]
        public void DashHeroBlueBrush_exists_in_both_themes()
        {
            var root = LocateProjectRoot();
            foreach (var theme in new[] { "Themes/LightTheme.xaml", "Themes/DarkTheme.xaml" })
            {
                var src = File.ReadAllText(Path.Combine(root, theme));
                Assert.Contains("x:Key=\"DashHeroBlueBrush\"", src);
                Assert.Contains("LinearGradientBrush", src);
            }
        }

        /// <summary>
        /// The Overview bottom section (Sales / Production / Sheet store) now
        /// renders its 4 KPI tiles as a 2×2 grid (Rows=2) so each tile is
        /// larger and the label/value are legible, instead of four thin tiles
        /// crammed into a single row.
        /// </summary>
        [Fact]
        public void Overview_kpi_band_uses_a_two_row_grid()
        {
            var root = LocateProjectRoot();
            var styles = File.ReadAllText(Path.Combine(root, "Styles/DashboardStyles.xaml"));
            Assert.Contains("<UniformGrid Rows=\"2\"/>", styles);
        }

        /// <summary>
        /// The Overview "Recent documents" feed must be a mouse-wheel scroller
        /// (bounded height + smooth scroll + smart bubbling) so the list
        /// scrolls under the pointer instead of growing forever or being
        /// swallowed by the outer page scroller.
        /// </summary>
        [Fact]
        public void Recent_documents_is_a_mouse_wheel_scroller()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/Dashboard/Overview/OverviewDashboardView.xaml"));

            Assert.Contains("Recent documents", src);
            Assert.Contains("SmartScrollBubblingBehavior.IsEnabled=\"True\"", src);
            Assert.Contains("SmoothScrollBehavior.IsEnabled=\"True\"", src);
            Assert.Contains("MaxHeight=\"320\"", src);
            Assert.Contains("CanContentScroll=\"True\"", src);
        }

        /// <summary>
        /// The Statement dashboard now matches the other modules: it exposes a
        /// KpiCards collection (same KpiCardModel shape) and renders through
        /// the shared SoftKpiCardTemplate instead of hand-written scalar cards.
        /// </summary>
        [Fact]
        public void Statement_matches_the_other_module_dashboards()
        {
            var root = LocateProjectRoot();
            var vm = File.ReadAllText(Path.Combine(root,
                "ViewModels/Dashboard/Statement/StDashboardViewModel.cs"));
            var view = File.ReadAllText(Path.Combine(root,
                "Views/Dashboard/Statement/StDashboardView.xaml"));

            Assert.Contains("KpiCards", vm);
            Assert.Contains("ObservableCollection<ProGlassAutomation.Models.Dashboard.KpiCardModel>", vm);
            Assert.Contains("SoftKpiCardTemplate", view);
            Assert.Contains("ItemsSource=\"{Binding KpiCards}\"", view);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName, "Views", "Settings",
                    "SettingsSectionStyles.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
