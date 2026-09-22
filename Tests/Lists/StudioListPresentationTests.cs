using System;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ProGlassAutomation.Views.Dashboard.Overview;
using ProGlassAutomation.Views.Shared.Lists;
using Xunit;

namespace ProGlassAutomation.Tests.Lists
{
    // Native STA tests; NOT executed in the Linux/no-dotnet editing environment.
    public sealed class StudioListPresentationTests
    {
        [Fact]
        public void Dossier_view_model_tag_is_not_rendered_as_icon_text()
        {
            var converter = new StudioListTagGlyphConverter();
            Assert.Equal("", converter.Convert(new object(), typeof(string), null!, CultureInfo.InvariantCulture));
            Assert.Equal("\uE81C", converter.Convert("\uE81C", typeof(string), null!, CultureInfo.InvariantCulture));
        }

        [Theory]
        [InlineData(double.NaN, 0.0)]
        [InlineData(double.PositiveInfinity, 0.0)]
        [InlineData(-1.0, 0.0)]
        [InlineData(840.0, 840.0)]
        public void Viewport_extent_binding_never_assigns_invalid_width(double input, double expected)
        {
            var converter = new StudioListExtentConverter();
            Assert.Equal(expected, converter.Convert(input, typeof(double), null!, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void Short_host_reserves_room_for_the_register_without_hiding_toolbar_controls()
        {
            var converter = new StudioListToolbarHeightConverter();
            Assert.Equal(240.0, converter.Convert(400.0, typeof(double), null!, CultureInfo.InvariantCulture));
            Assert.Equal(360.0, converter.Convert(1000.0, typeof(double), null!, CultureInfo.InvariantCulture));
            Assert.Equal(360.0, converter.Convert(double.NaN, typeof(double), null!, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void Gutter_tracks_system_scrollbar_width()
        {
            var converter = new StudioListGutterConverter();
            Assert.Equal(new Thickness(0, 0, 23, 0), converter.Convert(23.0, typeof(Thickness), null!, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void Semantic_pills_keep_distinct_soft_fill_and_readable_ink() => OnSta(() =>
        {
            var palette = new IndigoStatusBrushConverter
            {
                Accent = Brushes.Purple, AccentSoft = Brushes.Lavender,
                Danger = Brushes.DarkRed, DangerSoft = Brushes.MistyRose,
                Success = Brushes.DarkGreen, SuccessSoft = Brushes.Honeydew,
                Warning = Brushes.SaddleBrown, WarningSoft = Brushes.Cornsilk,
                Neutral = Brushes.DimGray, NeutralSoft = Brushes.WhiteSmoke
            };
            var converter = new StudioListBrushConverter { Palette = palette };
            Assert.Same(Brushes.MistyRose, converter.Convert("DangerBrush", typeof(Brush), "soft", CultureInfo.InvariantCulture));
            Assert.Same(Brushes.DarkRed, converter.Convert("DangerBrush", typeof(Brush), null!, CultureInfo.InvariantCulture));
            Assert.Same(Brushes.DarkRed, converter.Convert("TintRedBrush", typeof(Brush), "ink", CultureInfo.InvariantCulture));
            Assert.Same(Brushes.Purple, converter.Convert("AccentDarkBrush", typeof(Brush), null!, CultureInfo.InvariantCulture));
            Assert.Same(Brushes.Honeydew, converter.Convert("TintGreenBrush", typeof(Brush), null!, CultureInfo.InvariantCulture));
        });

        [Theory]
        [InlineData(600.0)]
        [InlineData(1500.0)]
        public void Shared_horizontal_viewport_keeps_list_height_finite_and_recycles(double width) => OnSta(() =>
        {
            var viewport = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = false
            };
            var grid = new Grid { MinWidth = 1120 };
            var converter = new StudioListExtentConverter();
            grid.SetBinding(FrameworkElement.WidthProperty, new Binding(nameof(ScrollViewer.ViewportWidth)) { Source = viewport, Converter = converter });
            grid.SetBinding(FrameworkElement.HeightProperty, new Binding(nameof(ScrollViewer.ViewportHeight)) { Source = viewport, Converter = converter });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new Border { Height = 44 });
            var list = new ListBox { ItemsSource = Enumerable.Range(0, 500), Padding = new Thickness(0) };
            ScrollViewer.SetCanContentScroll(list, true);
            ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Visible);
            VirtualizingPanel.SetIsVirtualizing(list, true);
            VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
            VirtualizingPanel.SetScrollUnit(list, ScrollUnit.Pixel);
            Grid.SetRow(list, 1);
            grid.Children.Add(list);
            viewport.Content = grid;
            for (int i = 0; i < 3; i++)
            {
                viewport.Measure(new Size(width, 420));
                viewport.Arrange(new Rect(0, 0, width, 420));
                viewport.UpdateLayout();
            }
            Assert.True(double.IsFinite(list.ActualHeight));
            Assert.InRange(list.ActualHeight, 1, 420);
            Assert.True(grid.ActualWidth >= 1120);
            Assert.Equal(500, list.Items.Count); // no collection cap or replacement data
            int realized = Enumerable.Range(0, 500).Count(i => list.ItemContainerGenerator.ContainerFromIndex(i) != null);
            Assert.InRange(realized, 1, 499);
        });

        private static void OnSta(Action test)
        {
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try { test(); }
                catch (Exception ex) { error = ex; }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF test exceeded its test timeout.");
            if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}
