using ProGlassAutomation.Models.Dashboard;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ProGlassAutomation.Controls.DonutChart
{
    /// <summary>
    /// Shared ring / donut chart for the 7 module dashboards.
    /// WPF primitives only — no LiveCharts / NuGet.
    /// Accent words resolve through theme brushes so Dark theme works.
    /// </summary>
    public partial class DonutChartControl
    {
        public DonutChartControl()
        {
            InitializeComponent();
        }

        public IEnumerable Slices
        {
            get => (IEnumerable)GetValue(SlicesProperty);
            set => SetValue(SlicesProperty, value);
        }

        public static readonly DependencyProperty SlicesProperty =
            DependencyProperty.Register(
                nameof(Slices), typeof(IEnumerable),
                typeof(DonutChartControl),
                new PropertyMetadata(null, OnAnyChanged));

        public string HoleLabel
        {
            get => (string)GetValue(HoleLabelProperty);
            set => SetValue(HoleLabelProperty, value);
        }

        public static readonly DependencyProperty HoleLabelProperty =
            DependencyProperty.Register(
                nameof(HoleLabel), typeof(string),
                typeof(DonutChartControl),
                new PropertyMetadata("TOTAL", OnAnyChanged));

        public string EmptyMessage
        {
            get => (string)GetValue(EmptyMessageProperty);
            set => SetValue(EmptyMessageProperty, value);
        }

        public static readonly DependencyProperty EmptyMessageProperty =
            DependencyProperty.Register(
                nameof(EmptyMessage), typeof(string),
                typeof(DonutChartControl),
                new PropertyMetadata("No data", OnAnyChanged));

        public double Thickness
        {
            get => (double)GetValue(ThicknessProperty);
            set => SetValue(ThicknessProperty, value);
        }

        public static readonly DependencyProperty ThicknessProperty =
            DependencyProperty.Register(
                nameof(Thickness), typeof(double),
                typeof(DonutChartControl),
                new PropertyMetadata(18.0, OnAnyChanged));

        private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DonutChartControl c) c.Redraw();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

        private void Redraw()
        {
            if (SliceLayer == null || PlotHost == null) return;

            SliceLayer.Children.Clear();
            LegendHost.Items.Clear();

            var slices = Normalize(Slices);
            double total = slices.Sum(s => (double)s.RawValue);
            bool any = total > 0.0001;

            EmptyState.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
            EmptyText.Text = string.IsNullOrWhiteSpace(EmptyMessage) ? "No data" : EmptyMessage;
            CenterValue.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            CenterLabel.Visibility = any ? Visibility.Visible : Visibility.Collapsed;

            CenterLabel.Text = HoleLabel ?? "";
            CenterValue.Text = any
                ? (total >= 1000 ? total.ToString("N0") : total.ToString("0.#"))
                : "";

            if (!any) return;

            double size = Math.Min(PlotHost.ActualWidth, PlotHost.ActualHeight);
            if (size < 40) return;

            double cx = PlotHost.ActualWidth / 2.0;
            double cy = PlotHost.ActualHeight / 2.0;
            double outer = (size / 2.0) - 4;
            double inner = Math.Max(8, outer - Math.Max(10, Thickness));

            double angle = -90;
            foreach (var slice in slices)
            {
                if (slice.RawValue <= 0) continue;
                double sweep = (double)slice.RawValue / total * 360.0;
                var brush = ResolveBrush(slice);
                SliceLayer.Children.Add(BuildArc(cx, cy, inner, outer, angle, sweep, brush));
                LegendHost.Items.Add(BuildLegend(slice, brush));
                angle += sweep;
            }
        }

        private static List<ChartSliceModel> Normalize(IEnumerable? raw)
        {
            var list = new List<ChartSliceModel>();
            if (raw == null) return list;
            foreach (var item in raw)
            {
                if (item is ChartSliceModel slice)
                    list.Add(slice);
            }
            return list;
        }

        private Brush ResolveBrush(ChartSliceModel slice)
        {
            if (!string.IsNullOrWhiteSpace(slice.SliceColor))
            {
                try
                {
                    return (Brush)new BrushConverter().ConvertFromString(slice.SliceColor)!;
                }
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DonutChartControl.xaml] ResolveBrush: fall through to accent: " + ex.Message); }
            }

            string key = slice.Accent switch
            {
                "Green" => "SuccessBrush",
                "Blue" => "InfoBrush",
                "Amber" => "WarningBrush",
                "Red" => "DangerBrush",
                _ => "AccentBrush"
            };

            if (TryFindResource(key) is Brush themed)
                return themed;
            return Brushes.SteelBlue;
        }

        private static Path BuildArc(
            double cx, double cy, double inner, double outer,
            double startDeg, double sweepDeg, Brush fill)
        {
            var geo = new StreamGeometry { FillRule = FillRule.Nonzero };
            using (var ctx = geo.Open())
            {
                Point outerStart = Polar(cx, cy, outer, startDeg);
                Point outerEnd = Polar(cx, cy, outer, startDeg + sweepDeg);
                Point innerEnd = Polar(cx, cy, inner, startDeg + sweepDeg);
                Point innerStart = Polar(cx, cy, inner, startDeg);
                bool large = sweepDeg > 180;

                ctx.BeginFigure(outerStart, true, true);
                ctx.ArcTo(outerEnd, new Size(outer, outer), 0, large,
                    SweepDirection.Clockwise, true, false);
                ctx.LineTo(innerEnd, true, false);
                ctx.ArcTo(innerStart, new Size(inner, inner), 0, large,
                    SweepDirection.Counterclockwise, true, false);
            }
            geo.Freeze();

            return new Path
            {
                Data = geo,
                Fill = fill,
                SnapsToDevicePixels = true
            };
        }

        private static Point Polar(double cx, double cy, double r, double deg)
        {
            double rad = deg * Math.PI / 180.0;
            return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
        }

        private FrameworkElement BuildLegend(ChartSliceModel slice, Brush brush)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };
            row.Children.Add(new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(2),
                Background = brush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            var texts = new StackPanel();
            texts.Children.Add(new TextBlock
            {
                Text = slice.Label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)TryFindResource("PrimaryForegroundBrush")
                    ?? Brushes.Black
            });
            texts.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(slice.ValueDisplay)
                    ? slice.RawValue.ToString("0")
                    : slice.ValueDisplay,
                FontSize = 10,
                Foreground = (Brush)TryFindResource("SecondaryForegroundBrush")
                    ?? Brushes.Gray
            });
            row.Children.Add(texts);
            return row;
        }
    }
}
