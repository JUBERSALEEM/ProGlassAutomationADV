using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ProGlassAutomation.Controls.Sparkline
{
    /// <summary>
    /// Lightweight sparkline (mini line chart) drawn with pure WPF Polyline
    /// primitives. No external NuGet dependencies. Used in stat cards to
    /// show trends alongside a single big number.
    ///
    /// Base class is inferred from the XAML root element — do NOT
    /// specify one here (causes CS0263 partial class mismatch).
    ///
    /// Usage:
    ///     <local:SparklineControl Height="40"
    ///                             Values="12,15,10,18,22,20,25"
    ///                             LineBrush="{DynamicResource AccentBrush}"
    ///                             AreaBrush="{DynamicResource AccentBrush}"/>
    ///
    /// Values can be set as:
    ///   - Comma-separated string ("1,2,3,4,5") — auto-parsed to double[]
    ///   - IEnumerable&lt;double&gt; via code
    ///
    /// Redraws on: SizeChanged, Values change, LineBrush/AreaBrush change.
    /// </summary>
    public partial class SparklineControl
    {
        public SparklineControl()
        {
            InitializeComponent();
        }

        // ─── DependencyProperties ─────────────────────────────────────────

        /// <summary>
        /// Data points. Can be a comma-separated string or an
        /// IEnumerable&lt;double&gt;. Auto-triggers redraw on change.
        /// </summary>
        public object Values
        {
            get => GetValue(ValuesProperty);
            set => SetValue(ValuesProperty, value);
        }

        public static readonly DependencyProperty ValuesProperty =
            DependencyProperty.Register(
                nameof(Values),
                typeof(object),
                typeof(SparklineControl),
                new PropertyMetadata(null, OnValuesChanged));

        /// <summary>Stroke color of the line.</summary>
        public Brush LineBrush
        {
            get => (Brush)GetValue(LineBrushProperty);
            set => SetValue(LineBrushProperty, value);
        }

        public static readonly DependencyProperty LineBrushProperty =
            DependencyProperty.Register(
                nameof(LineBrush),
                typeof(Brush),
                typeof(SparklineControl),
                new PropertyMetadata(Brushes.SteelBlue, OnBrushChanged));

        /// <summary>Fill color under the line (drawn at 25% opacity).</summary>
        public Brush AreaBrush
        {
            get => (Brush)GetValue(AreaBrushProperty);
            set => SetValue(AreaBrushProperty, value);
        }

        public static readonly DependencyProperty AreaBrushProperty =
            DependencyProperty.Register(
                nameof(AreaBrush),
                typeof(Brush),
                typeof(SparklineControl),
                new PropertyMetadata(Brushes.SteelBlue, OnBrushChanged));

        /// <summary>Line thickness in pixels. Default 2.0</summary>
        public double LineThickness
        {
            get => (double)GetValue(LineThicknessProperty);
            set => SetValue(LineThicknessProperty, value);
        }

        public static readonly DependencyProperty LineThicknessProperty =
            DependencyProperty.Register(
                nameof(LineThickness),
                typeof(double),
                typeof(SparklineControl),
                new PropertyMetadata(2.0, OnBrushChanged));

        /// <summary>
        /// When true, draws the filled area under the line.
        /// When false, only the line stroke is drawn. Default true.
        /// </summary>
        public bool ShowArea
        {
            get => (bool)GetValue(ShowAreaProperty);
            set => SetValue(ShowAreaProperty, value);
        }

        public static readonly DependencyProperty ShowAreaProperty =
            DependencyProperty.Register(
                nameof(ShowArea),
                typeof(bool),
                typeof(SparklineControl),
                new PropertyMetadata(true, OnBrushChanged));

        // ─── Redraw triggers ──────────────────────────────────────────────

        private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SparklineControl s) s.Redraw();
        }

        private static void OnBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SparklineControl s) s.Redraw();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Redraw();
        }

        // ─── Drawing ──────────────────────────────────────────────────────

        private void Redraw()
        {
            double w = ActualWidth;
            double h = ActualHeight;

            if (w < 4 || h < 4 || LinePath == null) return;

            var points = ParseValues(Values);
            if (points == null || points.Count < 2)
            {
                LinePath.Data = null;
                AreaPath.Data = null;
                return;
            }

            // Padding so line doesn't touch top/bottom edges
            double pad = LineThickness + 1;
            double drawH = h - (pad * 2);
            if (drawH < 1) drawH = 1;

            double min = points.Min();
            double max = points.Max();
            double range = max - min;
            if (range < 0.0001) range = 1;   // avoid divide-by-zero on flat data

            double stepX = points.Count == 1
                ? 0
                : w / (points.Count - 1);

            var pathFigure = new PathFigure
            {
                StartPoint = new Point(0, MapY(points[0], min, range, pad, drawH))
            };

            for (int i = 1; i < points.Count; i++)
            {
                double x = i * stepX;
                double y = MapY(points[i], min, range, pad, drawH);
                pathFigure.Segments.Add(new LineSegment(new Point(x, y), true));
            }

            var lineGeometry = new PathGeometry();
            lineGeometry.Figures.Add(pathFigure);
            LinePath.Data = lineGeometry;

            // Filled area under line (closed shape down to bottom)
            if (ShowArea)
            {
                var areaFigure = new PathFigure
                {
                    StartPoint = new Point(0, h)
                };
                areaFigure.Segments.Add(new LineSegment(
                    new Point(0, MapY(points[0], min, range, pad, drawH)), true));
                for (int i = 1; i < points.Count; i++)
                {
                    double x = i * stepX;
                    double y = MapY(points[i], min, range, pad, drawH);
                    areaFigure.Segments.Add(new LineSegment(new Point(x, y), true));
                }
                areaFigure.Segments.Add(new LineSegment(new Point(w, h), true));
                areaFigure.IsClosed = true;

                var areaGeometry = new PathGeometry();
                areaGeometry.Figures.Add(areaFigure);
                AreaPath.Data = areaGeometry;
            }
            else
            {
                AreaPath.Data = null;
            }
        }

        private static double MapY(double value, double min, double range, double pad, double drawH)
        {
            // Invert Y (WPF Y grows downward, chart high values should be at top)
            double normalized = (value - min) / range;
            return pad + (1 - normalized) * drawH;
        }

        private static List<double> ParseValues(object raw)
        {
            if (raw == null) return null;

            // Already a numeric collection
            if (raw is IEnumerable<double> doubles)
                return doubles.ToList();

            if (raw is IEnumerable<decimal> decimals)
                return decimals.Select(d => (double)d).ToList();

            if (raw is IEnumerable<int> ints)
                return ints.Select(i => (double)i).ToList();

            // Comma-separated string
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