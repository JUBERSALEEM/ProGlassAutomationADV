using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ProGlassAutomation.Controls.MiniBarChart
{
    /// <summary>
    /// Lightweight bar chart (mini vertical bars) for use inside stat
    /// cards. No external NuGet dependencies. Draws simple rounded bars
    /// on a Canvas.
    ///
    /// Base class is inferred from the XAML root element — do NOT
    /// specify one here (causes CS0263 partial class mismatch).
    ///
    /// Usage:
    ///     <local:MiniBarChartControl Height="60"
    ///                                Values="30,45,25,50,40,60,55"
    ///                                BarBrush="{DynamicResource AccentBrush}"
    ///                                HighlightIndex="4"
    ///                                HighlightBrush="{DynamicResource AccentDarkBrush}"/>
    ///
    /// HighlightIndex draws that specific bar in HighlightBrush color
    /// (used to emphasise "today" or "peak" value).
    /// -1 disables highlighting.
    /// </summary>
    public partial class MiniBarChartControl
    {
        public MiniBarChartControl()
        {
            InitializeComponent();
        }

        // ─── DependencyProperties ─────────────────────────────────────────

        public object Values
        {
            get => GetValue(ValuesProperty);
            set => SetValue(ValuesProperty, value);
        }

        public static readonly DependencyProperty ValuesProperty =
            DependencyProperty.Register(
                nameof(Values),
                typeof(object),
                typeof(MiniBarChartControl),
                new PropertyMetadata(null, OnAnyChanged));

        public Brush BarBrush
        {
            get => (Brush)GetValue(BarBrushProperty);
            set => SetValue(BarBrushProperty, value);
        }

        public static readonly DependencyProperty BarBrushProperty =
            DependencyProperty.Register(
                nameof(BarBrush),
                typeof(Brush),
                typeof(MiniBarChartControl),
                new PropertyMetadata(Brushes.SteelBlue, OnAnyChanged));

        public Brush HighlightBrush
        {
            get => (Brush)GetValue(HighlightBrushProperty);
            set => SetValue(HighlightBrushProperty, value);
        }

        public static readonly DependencyProperty HighlightBrushProperty =
            DependencyProperty.Register(
                nameof(HighlightBrush),
                typeof(Brush),
                typeof(MiniBarChartControl),
                new PropertyMetadata(Brushes.OrangeRed, OnAnyChanged));

        /// <summary>Index of bar to draw in HighlightBrush. -1 = none. Default -1.</summary>
        public int HighlightIndex
        {
            get => (int)GetValue(HighlightIndexProperty);
            set => SetValue(HighlightIndexProperty, value);
        }

        public static readonly DependencyProperty HighlightIndexProperty =
            DependencyProperty.Register(
                nameof(HighlightIndex),
                typeof(int),
                typeof(MiniBarChartControl),
                new PropertyMetadata(-1, OnAnyChanged));

        /// <summary>Corner radius for bar tops. Default 3.</summary>
        public double BarCornerRadius
        {
            get => (double)GetValue(BarCornerRadiusProperty);
            set => SetValue(BarCornerRadiusProperty, value);
        }

        public static readonly DependencyProperty BarCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(BarCornerRadius),
                typeof(double),
                typeof(MiniBarChartControl),
                new PropertyMetadata(3.0, OnAnyChanged));

        /// <summary>Gap between bars in pixels. Default 3.</summary>
        public double BarGap
        {
            get => (double)GetValue(BarGapProperty);
            set => SetValue(BarGapProperty, value);
        }

        public static readonly DependencyProperty BarGapProperty =
            DependencyProperty.Register(
                nameof(BarGap),
                typeof(double),
                typeof(MiniBarChartControl),
                new PropertyMetadata(3.0, OnAnyChanged));

        // ─── Redraw triggers ──────────────────────────────────────────────

        private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MiniBarChartControl c) c.Redraw();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Redraw();
        }

        // ─── Drawing ──────────────────────────────────────────────────────

        private void Redraw()
        {
            if (BarsCanvas == null) return;

            BarsCanvas.Children.Clear();

            double w = ActualWidth;
            double h = ActualHeight;

            if (w < 4 || h < 4) return;

            var values = ParseValues(Values);
            if (values == null || values.Count == 0) return;

            double max = values.Max();
            if (max < 0.0001) max = 1;

            int count = values.Count;
            double totalGap = BarGap * (count - 1);
            double barWidth = (w - totalGap) / count;
            if (barWidth < 1) barWidth = 1;

            for (int i = 0; i < count; i++)
            {
                double val = values[i];
                double barH = (val / max) * h;
                if (barH < 2) barH = 2;   // minimum visible bar

                double x = i * (barWidth + BarGap);
                double y = h - barH;

                var rect = new Rectangle
                {
                    Width = barWidth,
                    Height = barH,
                    RadiusX = BarCornerRadius,
                    RadiusY = BarCornerRadius,
                    Fill = (i == HighlightIndex && HighlightBrush != null)
                        ? HighlightBrush
                        : BarBrush
                };

                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                BarsCanvas.Children.Add(rect);
            }
        }

        private static List<double> ParseValues(object raw)
        {
            if (raw == null) return null;

            if (raw is IEnumerable<double> doubles)
                return doubles.ToList();

            if (raw is IEnumerable<decimal> decimals)
                return decimals.Select(d => (double)d).ToList();

            if (raw is IEnumerable<int> ints)
                return ints.Select(i => (double)i).ToList();

            if (raw is string s)
            {
                var parts = s.Split(new[] { ',', ';', '|', ' ' },
                                    StringSplitOptions.RemoveEmptyEntries);
                var list = new List<double>();
                foreach (var p in parts)
                {
                    if (double.TryParse(p.Trim(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double v))
                    {
                        list.Add(v);
                    }
                }
                return list;
            }

            return null;
        }
    }
}