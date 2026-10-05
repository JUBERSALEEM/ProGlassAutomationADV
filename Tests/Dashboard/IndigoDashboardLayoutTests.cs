using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Views.Dashboard;
using Xunit;

namespace ProGlassAutomation.Tests.Dashboard
{
    // Native WPF tests. Not substituted with a browser/HTML approximation.
    public sealed class IndigoDashboardLayoutTests
    {
        [Fact]
        public void Five_kpis_remain_reachable_at_wide_and_narrow_widths() => OnSta(() =>
        {
            var panel = new IndigoDashboardPanel { MaxColumns = 4, MinimumColumnWidth = 210, Gap = 16 };
            for (int i = 0; i < 5; i++) panel.Children.Add(new Border { MinHeight = 80 });
            Layout(panel, 1040);
            Assert.Equal(176, panel.DesiredSize.Height, 3);
            Assert.Equal(96, VisualTreeHelper.GetOffset(panel.Children[4]).Y, 3);
            Layout(panel, 360);
            Assert.Equal(464, panel.DesiredSize.Height, 3);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(i * 96, VisualTreeHelper.GetOffset(panel.Children[i]).Y, 3);
                Assert.Equal(360, panel.Children[i].RenderSize.Width, 3);
            }
            Layout(panel, 1040);
            Assert.Equal(176, panel.DesiredSize.Height, 3);
        });

        [Fact]
        public void Weighted_cards_stack_before_secondary_card_is_squeezed() => OnSta(() =>
        {
            var panel = new IndigoDashboardPanel { MinimumColumnWidth = 340, FirstColumnWeight = 1.45, Gap = 18 };
            panel.Children.Add(new Border { MinHeight = 120 });
            panel.Children.Add(new Border { MinHeight = 240 });
            Layout(panel, 850); // below 340 * 2.45 + 18 = 851
            Assert.Equal(378, panel.DesiredSize.Height, 3);
            Assert.Equal(138, VisualTreeHelper.GetOffset(panel.Children[1]).Y, 3);
            Layout(panel, 1000);
            Assert.Equal(240, panel.DesiredSize.Height, 3);
            Assert.True(panel.Children[1].RenderSize.Width >= 340);
        });

        [Theory]
        [InlineData(4.0, 560.0, 140.0)]
        [InlineData(0.25, 140.0, 560.0)]
        public void Unbounded_measure_respects_both_weighted_column_minimums(
            double weight, double firstWidth, double secondWidth) => OnSta(() =>
        {
            var panel = new IndigoDashboardPanel
            {
                MaxColumns = 2, MinimumColumnWidth = 140, FirstColumnWeight = weight, Gap = 18,
            };
            panel.Children.Add(new Border { MinHeight = 80 });
            panel.Children.Add(new Border { MinHeight = 80 });
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.Equal(718, panel.DesiredSize.Width, 3);
            panel.Arrange(new Rect(new Point(), panel.DesiredSize));
            Assert.Equal(firstWidth, panel.Children[0].RenderSize.Width, 3);
            Assert.Equal(secondWidth, panel.Children[1].RenderSize.Width, 3);
        });

        [Fact]
        public void Collapsed_items_do_not_reserve_holes_and_empty_panel_is_safe() => OnSta(() =>
        {
            var panel = new IndigoDashboardPanel { MaxColumns = 4, MinimumColumnWidth = 210, Gap = 16 };
            Layout(panel, 1040);
            Assert.Equal(0, panel.DesiredSize.Height);
            for (int i = 0; i < 5; i++) panel.Children.Add(new Border { MinHeight = 80 });
            panel.Children[1].Visibility = Visibility.Collapsed;
            Layout(panel, 1040);
            Assert.Equal(80, panel.DesiredSize.Height);
            Assert.Equal(5, panel.Children.Count); // no data/container removed
        });

        [Fact]
        public void Invalid_layout_parameters_are_rejected() => OnSta(() =>
        {
            var panel = new IndigoDashboardPanel();
            Assert.Throws<ArgumentException>(() => panel.MaxColumns = 0);
            Assert.Throws<ArgumentException>(() => panel.MinimumColumnWidth = double.NaN);
            Assert.Throws<ArgumentException>(() => panel.Gap = -1);
            Assert.Throws<ArgumentException>(() => panel.FirstColumnWeight = double.PositiveInfinity);
        });

        [Fact]
        public void Shared_resource_dictionary_loads_without_installing_an_app_theme() => OnSta(() =>
        {
            var resources = new ResourceDictionary();
            string assembly = typeof(IndigoDashboardPanel).Assembly.GetName().Name!;
            resources.Source = new Uri($"pack://application:,,,/{assembly};component/Views/Dashboard/IndigoDashboardResources.xaml", UriKind.Absolute);
            Assert.IsType<Style>(resources["Ds.CardStyle"]);
            Assert.IsType<DataTemplate>(resources["Ds.KpiTileTemplate"]);
            Assert.IsType<ItemsPanelTemplate>(resources["Ds.KpiBandPanel"]);
            Assert.IsType<SolidColorBrush>(resources["PrimaryForegroundBrush"]);
        });

        private static void Layout(IndigoDashboardPanel panel, double width)
        {
            panel.Measure(new Size(width, double.PositiveInfinity));
            panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
        }

        private static void OnSta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { failure = ex; }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF test thread timed out");
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
