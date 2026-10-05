// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Dashboard/DashboardModernizationTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Dashboard modernization (Aug 2026)
//
// Hermetic source-file tests that lock the "modern analytics app" pass on
// the dashboards. They do NOT boot WPF or touch a DB — they read the
// XAML / theme / chart source relative to the project root and assert the
// upgrade is present, so a future edit that drops the modern look fails.
//
// What is locked:
//   1. TrendChartControl exposes SeriesEndBrush, and ResolveSeriesBrush()
//      builds a top→bottom LinearGradientBrush from the primary to the end
//      brush (the modern gradient-bar fill).
//   2. Every dashboard trend chart declares both series brushes.
//   3. The Overview dashboard uses its scoped Indigo Studio gradient hero card
//      with the white-on-indigo Factory pulse number.
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
        [InlineData("Views/Dashboard/Optimization/OptimizationDashboardView.xaml")]
        [InlineData("Views/Dashboard/Production/ProductionDashboardView.xaml")]
        public void All_dashboard_charts_declare_both_series_brushes(string rel)
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root, rel));

            // Palette can be page-scoped (Indigo Overview) or shared (Pg).
            // Check each chart, not a count that could match unrelated elements.
            var charts = Regex.Matches(src, @"<\w+:TrendChartControl\b[^>]*>");
            Assert.NotEmpty(charts.Cast<Match>());
            foreach (Match chart in charts)
            {
                Assert.Contains("SeriesBrush=", chart.Value);
                Assert.Contains("SeriesEndBrush=", chart.Value);
            }
        }

        [Fact]
        public void Overview_uses_the_scoped_indigo_gradient_hero_card()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/Dashboard/Overview/OverviewDashboardView.xaml"));

            Assert.Contains("IndigoStudioResources.xaml", src);
            Assert.Contains("Background=\"{DynamicResource Ov.Hero}\"", src);
            Assert.Contains("Dashboard.FactoryPulse", src);
            Assert.Contains("Foreground=\"{DynamicResource Ov.OnAccent}\"", src);
            Assert.Contains("Foreground=\"{DynamicResource Ov.OnAccentMuted}\"", src);
            Assert.Contains("SeriesKind=\"Area\"", src);
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

        [Fact]
        public void Overview_keeps_all_four_complete_kpi_collections()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/Dashboard/Overview/OverviewDashboardView.xaml"));
            foreach (string collection in new[] { "SalesTiles", "ProductionTiles", "InventoryTiles", "DeliveryTiles" })
                Assert.Contains("ItemsSource=\"{Binding " + collection + "}\"", src);
            var resources = File.ReadAllText(Path.Combine(root,
                "Views/Dashboard/Overview/IndigoStudioResources.xaml"));
            Assert.Contains("<ItemsPanelTemplate><StackPanel/></ItemsPanelTemplate>", resources);
            Assert.DoesNotContain("MaxHeight=", resources);
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
            Assert.Contains("MaxHeight=\"440\"", src);
            Assert.Contains("CanContentScroll=\"True\"", src);
        }

        /// <summary>
        /// The Statement dashboard now matches the other modules: it exposes a
        /// KpiCards collection (same KpiCardModel shape) and renders through
        /// the shared Ds.KpiTileTemplate instead of hand-written scalar cards.
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
            Assert.Contains("Ds.KpiTileTemplate", view);
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
