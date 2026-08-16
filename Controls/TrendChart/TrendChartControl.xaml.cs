// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/TrendChart/TrendChartControl.xaml.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Controls.TrendChart
//
// A dashboard trend chart drawn with WPF primitives.
//
// SUPPORTS
//   Bar · Line · Area, a value axis with gridlines, a category axis,
//   hover crosshair + tooltip, a grow-in animation, and an empty state.
//
// DATA CONTRACT
//   Points takes any IEnumerable of objects exposing Label (string) and
//   Value (decimal/double/int). That matches both existing shapes —
//   Services/Dashboard/Core.TimeBucket and Models/Dashboard.ChartBarModel
//   (via RawValue) — so no ViewModel has to reshape its data to use this.
//
// PERFORMANCE
//   Redraw() rebuilds the visual tree, so it must not run per frame.
//   It is called only on data change, size change or series-type change.
//   Hover does NOT redraw: it moves two existing shapes, which is why
//   the crosshair stays smooth on a large window.
//
// WHY NOT A CHART LIBRARY
//   A NuGet package cannot be restored or verified in this environment,
//   so adding one would be an unverifiable dependency in a build I
//   cannot run. Controls/Sparkline and Controls/MiniBarChart already
//   draw with PathGeometry in code-behind; this follows that idiom.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ProGlassAutomation.Controls.TrendChart
{
    public enum TrendSeriesKind
    {
        Bar,
        Line,
        Area
    }

    public partial class TrendChartControl : UserControl
    {
        // One resolved data point, independent of the source type.
        private sealed class Pt
        {
            public string Label = string.Empty;
            public double Value;
            public double X;        // centre of the point in plot space
            public double Y;        // mapped Y for the value
            public FrameworkElement? Visual;
        }

        private readonly List<Pt> _points = new();

        // Reused hover visuals — moved rather than recreated, so hovering
        // never rebuilds the tree.
        private Line? _crosshair;
        private Ellipse? _marker;

        private double _plotW;
        private double _plotH;
        private int _hoverIndex = -1;

        private const double AxisGutter = 42;   // room for value labels
        private const double TopPad = 10;   // headroom above the tallest point

        // Readable axis bounds, ascending. Includes half-steps so the
        // upper bound tracks the data instead of over-reaching.
        private static readonly double[] NiceSteps =
            { 1, 1.5, 2, 2.5, 3, 4, 5, 7.5, 10 };

        public TrendChartControl()
        {
            InitializeComponent();
        }

        // ═════════════════════════════════════════════════════════════
        //  DEPENDENCY PROPERTIES
        // ═════════════════════════════════════════════════════════════

        public object Points
        {
            get => GetValue(PointsProperty);
            set => SetValue(PointsProperty, value);
        }

        public static readonly DependencyProperty PointsProperty =
            DependencyProperty.Register(
                nameof(Points), typeof(object), typeof(TrendChartControl),
                new PropertyMetadata(null, OnDataChanged));

        public TrendSeriesKind SeriesKind
        {
            get => (TrendSeriesKind)GetValue(SeriesKindProperty);
            set => SetValue(SeriesKindProperty, value);
        }

        public static readonly DependencyProperty SeriesKindProperty =
            DependencyProperty.Register(
                nameof(SeriesKind), typeof(TrendSeriesKind), typeof(TrendChartControl),
                new PropertyMetadata(TrendSeriesKind.Bar, OnDataChanged));

        public Brush SeriesBrush
        {
            get => (Brush)GetValue(SeriesBrushProperty);
            set => SetValue(SeriesBrushProperty, value);
        }

        public static readonly DependencyProperty SeriesBrushProperty =
            DependencyProperty.Register(
                nameof(SeriesBrush), typeof(Brush), typeof(TrendChartControl),
                new PropertyMetadata(null, OnDataChanged));

        public Brush GridBrush
        {
            get => (Brush)GetValue(GridBrushProperty);
            set => SetValue(GridBrushProperty, value);
        }

        public static readonly DependencyProperty GridBrushProperty =
            DependencyProperty.Register(
                nameof(GridBrush), typeof(Brush), typeof(TrendChartControl),
                new PropertyMetadata(null, OnDataChanged));

        public Brush LabelBrush
        {
            get => (Brush)GetValue(LabelBrushProperty);
            set => SetValue(LabelBrushProperty, value);
        }

        public static readonly DependencyProperty LabelBrushProperty =
            DependencyProperty.Register(
                nameof(LabelBrush), typeof(Brush), typeof(TrendChartControl),
                new PropertyMetadata(null, OnDataChanged));

        /// <summary>Prefix for tooltip and axis values, e.g. "AED ".</summary>
        public string ValuePrefix
        {
            get => (string)GetValue(ValuePrefixProperty);
            set => SetValue(ValuePrefixProperty, value);
        }

        public static readonly DependencyProperty ValuePrefixProperty =
            DependencyProperty.Register(
                nameof(ValuePrefix), typeof(string), typeof(TrendChartControl),
                new PropertyMetadata(string.Empty));

        /// <summary>Message shown when there is nothing to plot.</summary>
        public string EmptyMessage
        {
            get => (string)GetValue(EmptyMessageProperty);
            set => SetValue(EmptyMessageProperty, value);
        }

        public static readonly DependencyProperty EmptyMessageProperty =
            DependencyProperty.Register(
                nameof(EmptyMessage), typeof(string), typeof(TrendChartControl),
                new PropertyMetadata("No data available for this period.",
                                     OnEmptyMessageChanged));

        /// <summary>Animate on data change. Off for reduced-motion setups.</summary>
        public bool Animate
        {
            get => (bool)GetValue(AnimateProperty);
            set => SetValue(AnimateProperty, value);
        }

        public static readonly DependencyProperty AnimateProperty =
            DependencyProperty.Register(
                nameof(Animate), typeof(bool), typeof(TrendChartControl),
                new PropertyMetadata(true));

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => (d as TrendChartControl)?.Redraw();

        private static void OnEmptyMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TrendChartControl c && c.EmptyText != null)
                c.EmptyText.Text = e.NewValue as string ?? string.Empty;
        }

        // ═════════════════════════════════════════════════════════════
        //  DATA RESOLUTION
        //
        //  Reflection once per point, not per frame. This lets the
        //  control accept TimeBucket, ChartBarModel or any future shape
        //  without those types taking a dependency on the control.
        // ═════════════════════════════════════════════════════════════

        private static bool TryNumber(object? o, out double value)
        {
            value = 0;
            switch (o)
            {
                case null: return false;
                case double dd: value = dd; return true;
                case decimal m: value = (double)m; return true;
                case int i: value = i; return true;
                case long l: value = l; return true;
                case float f: value = f; return true;
                default:
                    return double.TryParse(Convert.ToString(o, CultureInfo.InvariantCulture),
                                           NumberStyles.Any, CultureInfo.InvariantCulture,
                                           out value);
            }
        }

        private List<Pt> Resolve(object? raw)
        {
            var list = new List<Pt>();
            if (raw is not IEnumerable seq || raw is string) return list;

            foreach (var item in seq)
            {
                if (item == null) continue;

                var t = item.GetType();
                string label =
                    t.GetProperty("Label", BindingFlags.Public | BindingFlags.Instance)?
                     .GetValue(item) as string ?? string.Empty;

                // Value first, then RawValue — ChartBarModel uses the latter.
                object? v = t.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)?
                             .GetValue(item)
                         ?? t.GetProperty("RawValue", BindingFlags.Public | BindingFlags.Instance)?
                             .GetValue(item);

                if (!TryNumber(v, out double val)) val = 0;

                list.Add(new Pt { Label = label, Value = val });
            }
            return list;
        }

        // ═════════════════════════════════════════════════════════════
        //  RENDER
        // ═════════════════════════════════════════════════════════════

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

        /// <summary>
        /// True when animation should run.
        ///
        /// ui-ux-pro-max lists "no reduced-motion" as an animation
        /// anti-pattern. Windows exposes the user's preference through
        /// SystemParameters.ClientAreaAnimation, so an explicit
        /// Animate="True" still yields to someone who has turned
        /// animations off system-wide.
        /// </summary>
        private bool ShouldAnimate =>
            Animate && SystemParameters.ClientAreaAnimation;

        private Brush Res(string key, Brush fallback) =>
            TryFindResource(key) as Brush ?? fallback;

        private void Redraw()
        {
            if (GridLayer == null || SeriesLayer == null) return;   // pre-InitializeComponent

            GridLayer.Children.Clear();
            SeriesLayer.Children.Clear();
            HoverLayer.Children.Clear();
            AxisLayer.Children.Clear();
            _crosshair = null;
            _marker = null;
            _hoverIndex = -1;
            HoverPopup.IsOpen = false;

            _points.Clear();
            _points.AddRange(Resolve(Points));

            _plotW = PlotHost?.ActualWidth ?? 0;
            _plotH = PlotHost?.ActualHeight ?? 0;

            // A chart with every value at zero is NOT the same as a chart
            // with no data, but neither can be drawn meaningfully, so both
            // show the empty state rather than a misleading flat baseline.
            bool hasData = _points.Count > 0 && _points.Any(p => Math.Abs(p.Value) > 0.0000001);

            if (EmptyState != null)
                EmptyState.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;

            if (!hasData || _plotW < 20 || _plotH < 20) return;

            var series = SeriesBrush ?? Res("AccentBrush", Brushes.SteelBlue);
            var grid = GridBrush ?? Res("DividerBrush", Brushes.LightGray);
            var label = LabelBrush ?? Res("MutedForegroundBrush", Brushes.Gray);

            double max = _points.Max(p => p.Value);
            double min = Math.Min(0, _points.Min(p => p.Value));

            // "Nice" upper bound so gridline labels are round numbers
            // rather than 1,237.4 — a readability requirement, and it
            // also stops the tallest bar touching the top edge.
            double niceMax = NiceCeiling(max);
            if (Math.Abs(niceMax - min) < 0.0000001) niceMax = min + 1;

            double plotTop = TopPad;
            double plotBottom = _plotH;
            double drawH = plotBottom - plotTop;
            double left = AxisGutter;
            double drawW = _plotW - left;
            if (drawW < 10 || drawH < 10) return;

            double MapY(double v) =>
                plotBottom - ((v - min) / (niceMax - min)) * drawH;

            // ── Gridlines + value labels ─────────────────────────────
            const int GridLines = 4;
            for (int g = 0; g <= GridLines; g++)
            {
                double val = min + (niceMax - min) * g / GridLines;
                double y = MapY(val);

                var ln = new Line
                {
                    X1 = left,
                    X2 = _plotW,
                    Y1 = y,
                    Y2 = y,
                    Stroke = grid,
                    StrokeThickness = 1,
                    // A dash on every line but the baseline: the zero line
                    // is structural, the rest are reference marks.
                    StrokeDashArray = g == 0 ? null : new DoubleCollection { 3, 4 },
                    Opacity = g == 0 ? 0.85 : 0.45,
                    SnapsToDevicePixels = true
                };
                GridLayer.Children.Add(ln);

                var tb = new TextBlock
                {
                    Text = Abbreviate(val),
                    FontSize = 9,
                    Foreground = label,
                    TextAlignment = TextAlignment.Right,
                    Width = AxisGutter - 6
                };
                Canvas.SetLeft(tb, 0);
                Canvas.SetTop(tb, y - 7);
                GridLayer.Children.Add(tb);
            }

            // ── X positions ──────────────────────────────────────────
            int n = _points.Count;
            double slot = drawW / n;

            for (int i = 0; i < n; i++)
            {
                _points[i].X = left + slot * i + slot / 2;
                _points[i].Y = MapY(_points[i].Value);
            }

            if (SeriesKind == TrendSeriesKind.Bar)
                DrawBars(series, slot, plotBottom);
            else
                DrawLine(series, SeriesKind == TrendSeriesKind.Area);

            // ── Category axis ────────────────────────────────────────
            foreach (var p in _points)
            {
                var tb = new TextBlock
                {
                    Text = p.Label,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = label,
                    TextAlignment = TextAlignment.Center,
                    Width = slot
                };
                Canvas.SetLeft(tb, p.X - slot / 2);
                Canvas.SetTop(tb, 2);
                AxisLayer.Children.Add(tb);
            }
        }

        private void DrawBars(Brush series, double slot, double baseline)
        {
            // Bar width leaves a gap either side; capped so a 3-point
            // series does not render slabs.
            double barW = Math.Min(38, Math.Max(6, slot * 0.55));

            foreach (var p in _points)
            {
                double h = Math.Max(1, baseline - p.Y);

                var r = new Rectangle
                {
                    Width = barW,
                    Height = h,
                    Fill = series,
                    RadiusX = 4,
                    RadiusY = 4,
                    SnapsToDevicePixels = true
                };

                Canvas.SetLeft(r, p.X - barW / 2);
                Canvas.SetTop(r, p.Y);
                SeriesLayer.Children.Add(r);
                p.Visual = r;

                if (ShouldAnimate)
                {
                    // Grow from the baseline. Animating Height alone would
                    // grow downward, so Top is animated in step.
                    r.BeginAnimation(HeightProperty, new DoubleAnimation
                    {
                        From = 0,
                        To = h,
                        Duration = TimeSpan.FromMilliseconds(420),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    });

                    var top = new DoubleAnimation
                    {
                        From = baseline,
                        To = p.Y,
                        Duration = TimeSpan.FromMilliseconds(420),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    r.BeginAnimation(Canvas.TopProperty, top);
                }
            }
        }

        private void DrawLine(Brush series, bool fillArea)
        {
            var figure = new PathFigure { StartPoint = new Point(_points[0].X, _points[0].Y) };
            for (int i = 1; i < _points.Count; i++)
                figure.Segments.Add(new LineSegment(new Point(_points[i].X, _points[i].Y), true));

            var geo = new PathGeometry();
            geo.Figures.Add(figure);

            if (fillArea)
            {
                double baseY = _plotH;
                var af = new PathFigure { StartPoint = new Point(_points[0].X, baseY) };
                af.Segments.Add(new LineSegment(new Point(_points[0].X, _points[0].Y), true));
                for (int i = 1; i < _points.Count; i++)
                    af.Segments.Add(new LineSegment(new Point(_points[i].X, _points[i].Y), true));
                af.Segments.Add(new LineSegment(new Point(_points[^1].X, baseY), true));
                af.IsClosed = true;

                var ageo = new PathGeometry();
                ageo.Figures.Add(af);

                SeriesLayer.Children.Add(new Path
                {
                    Data = ageo,
                    Fill = series,
                    Opacity = 0.18,
                    IsHitTestVisible = false
                });
            }

            var line = new Path
            {
                Data = geo,
                Stroke = series,
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            SeriesLayer.Children.Add(line);

            // Point dots give the hover target something visible to snap to.
            foreach (var p in _points)
            {
                var dot = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = series
                };
                Canvas.SetLeft(dot, p.X - 3);
                Canvas.SetTop(dot, p.Y - 3);
                SeriesLayer.Children.Add(dot);
                p.Visual = dot;
            }

            if (ShouldAnimate)
            {
                SeriesLayer.BeginAnimation(OpacityProperty, new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(380)
                });
            }
        }

        // ═════════════════════════════════════════════════════════════
        //  HOVER
        //
        //  Moves two existing shapes. No Redraw(), no new visuals per
        //  mouse move — that is what keeps this smooth.
        // ═════════════════════════════════════════════════════════════

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_points.Count == 0 || _plotW <= 0)
            {
                HidePopup();
                return;
            }

            var pos = e.GetPosition(PlotHost);

            // Nearest point on X. A hit test against the bar itself would
            // lose the tooltip in the gaps between bars.
            int best = 0;
            double bestDx = double.MaxValue;
            for (int i = 0; i < _points.Count; i++)
            {
                double dx = Math.Abs(_points[i].X - pos.X);
                if (dx < bestDx) { bestDx = dx; best = i; }
            }

            if (best == _hoverIndex && HoverPopup.IsOpen) return;
            _hoverIndex = best;
            ShowIndicator(_points[best]);
        }

        /// <summary>
        /// Draw the crosshair, marker and tooltip for one point.
        /// Called by BOTH the mouse and keyboard paths so the two can
        /// never present different state for the same selection.
        /// </summary>
        private void ShowIndicator(Pt p)
        {
            var accent = SeriesBrush ?? Res("AccentBrush", Brushes.SteelBlue);

            if (_crosshair == null)
            {
                _crosshair = new Line
                {
                    Stroke = accent,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 3 },
                    Opacity = 0.6
                };
                HoverLayer.Children.Add(_crosshair);
            }
            _crosshair.X1 = _crosshair.X2 = p.X;
            _crosshair.Y1 = 0;
            _crosshair.Y2 = _plotH;

            if (_marker == null)
            {
                _marker = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = accent,
                    Stroke = Res("DashGlassSurfaceBrush", Brushes.White),
                    StrokeThickness = 2
                };
                HoverLayer.Children.Add(_marker);
            }
            Canvas.SetLeft(_marker, p.X - 5);
            Canvas.SetTop(_marker, p.Y - 5);

            TipLabel.Text = p.Label;
            TipValue.Text = ValuePrefix + p.Value.ToString("N2", CultureInfo.CurrentCulture);

            // Announce the focused value to screen readers. Without this
            // a keyboard user gets a visual tooltip and silence.
            AutomationProperties.SetName(
                this, $"Trend chart. {p.Label}: {ValuePrefix}{p.Value:N2}");

            // Offset so the cursor never covers the tooltip.
            HoverPopup.HorizontalOffset = p.X + 14;
            HoverPopup.VerticalOffset = Math.Max(0, p.Y - 12);
            HoverPopup.IsOpen = true;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e) => HidePopup();

        // ═════════════════════════════════════════════════════════════
        //  KEYBOARD ACCESS
        //
        //  ui-ux-pro-max, priority 1 (Accessibility) and the "Compact
        //  Control Semantics" guideline: an interactive element must be
        //  keyboard operable with visible focus, and the only route to
        //  its data must not be hover. Left/Right walk the series and
        //  surface the same tooltip the mouse shows; Home/End jump to
        //  the ends; Escape dismisses.
        // ═════════════════════════════════════════════════════════════

        private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (FocusRing != null) FocusRing.Visibility = Visibility.Visible;

            // Land on the first point so a keyboard user immediately sees
            // a value rather than an empty focused rectangle.
            if (_points.Count > 0 && _hoverIndex < 0) SelectIndex(0);
        }

        private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (FocusRing != null) FocusRing.Visibility = Visibility.Collapsed;
            HidePopup();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_points.Count == 0) return;

            int next = _hoverIndex;
            switch (e.Key)
            {
                case Key.Left:  next = Math.Max(0, (_hoverIndex < 0 ? 0 : _hoverIndex) - 1); break;
                case Key.Right: next = Math.Min(_points.Count - 1, _hoverIndex + 1); break;
                case Key.Home:  next = 0; break;
                case Key.End:   next = _points.Count - 1; break;
                case Key.Escape: HidePopup(); e.Handled = true; return;
                default: return;
            }

            SelectIndex(next);
            e.Handled = true;   // stop Left/Right also moving focus away
        }

        /// <summary>
        /// Shared by mouse and keyboard so both paths show identical
        /// state. Duplicating the logic is how the two drift apart.
        /// </summary>
        private void SelectIndex(int index)
        {
            if (index < 0 || index >= _points.Count) return;
            _hoverIndex = index;
            ShowIndicator(_points[index]);
        }

        private void HidePopup()
        {
            HoverPopup.IsOpen = false;
            _hoverIndex = -1;
            if (_crosshair != null) _crosshair.Y1 = _crosshair.Y2 = 0;
            if (_marker != null) Canvas.SetTop(_marker, -100);
        }

        // ═════════════════════════════════════════════════════════════
        //  AXIS HELPERS
        // ═════════════════════════════════════════════════════════════

        /// <summary>
        /// Round a maximum up to a readable bound (1/2/5 x power of ten),
        /// so gridlines land on numbers a person would choose.
        /// </summary>
        private static double NiceCeiling(double v)
        {
            if (v <= 0) return 1;

            double mag = Math.Pow(10, Math.Floor(Math.Log10(v)));
            double norm = v / mag;

            // A coarse 1/2/5/10 ladder wastes a lot of plot height: a max
            // of 245,820 would round to 500,000 and squash every bar into
            // the bottom half (51% of the area empty). This finer ladder
            // keeps the bound close to the data -- the same case now
            // rounds to 250,000, wasting 2% -- while still landing on
            // numbers a person would choose for an axis.
            foreach (double step in NiceSteps)
                if (norm <= step)
                    return step * mag;

            return 10 * mag;
        }

        /// <summary>Compact axis labels: 1.2M, 45.0K, 250.</summary>
        private static string Abbreviate(double v)
        {
            double a = Math.Abs(v);
            if (a >= 1_000_000) return (v / 1_000_000).ToString("0.#") + "M";
            if (a >= 1_000) return (v / 1_000).ToString("0.#") + "K";
            return v.ToString("0.#", CultureInfo.CurrentCulture);
        }
    }
}
