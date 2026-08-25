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
