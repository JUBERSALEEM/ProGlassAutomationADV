// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClayProgress.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — bar and ring progress
//
// Two shapes, one idea: progress is a WELL with something filling it.
// The track is concave (Well tier, no drop), and the fill rides just
// inside the track's radius so the two read as one moulded object
// rather than a bar laid on top of a groove.
//
// ── WHY THE FILL IS NOT A CLAY SURFACE ───────────────────────────────
// The fill has no drop and no inner rolls. At 8–14px tall a three-shadow
// stack cannot resolve, and a fill that tries reads as a lit tube. The
// fill is therefore a FLAT saturated accent — the one place in this
// family where flatness is the correct choice.
//
// ── ARC MATH ──────────────────────────────────────────────────────────
// StrokeDashArray is in units of the ellipse's own circumference, so
// the dash length has to be recomputed from the actual rendered size
// rather than hard-coded. A hard-coded dash silently mis-draws the moment
// the ring is resized, which is the single most common bug in hand-rolled
// WPF rings.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Claymorphism
{
    /// <summary>Clay progress as a concave track with a flat accent fill.</summary>
    public class ClayProgressBar : Control
    {
        /// <summary>Track corner radius.</summary>
        public static readonly DependencyProperty ClayCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ClayCornerRadius), typeof(CornerRadius),
                typeof(ClayProgressBar),
                new FrameworkPropertyMetadata(
                    new CornerRadius(10), OnClayChanged));

        public CornerRadius ClayCornerRadius
        {
            get => (CornerRadius)GetValue(ClayCornerRadiusProperty);
            set => SetValue(ClayCornerRadiusProperty, value);
        }

        /// <summary>Completion, 0..1. Clamped — a raw ratio is easy to
        /// hand a control a value of 1.4 from a divide that rounded
        /// differently, and an unclamped fill overflows the track.</summary>
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value), typeof(double),
                typeof(ClayProgressBar),
                new FrameworkPropertyMetadata(0.0, OnClayChanged));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>Fill colour. Transparent uses the accent.</summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClayProgressBar),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        private readonly ClaySurfaceParts _parts = new ClaySurfaceParts();
        private bool _bound;
        private bool _wired;

        public ClayProgressBar()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            Focusable = false;
            Height = 14;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (!_bound)
            {
                _parts.Bind(this);
                _bound = true;
                Wire();
            }
            Repaint();
        }

        private static void OnClayChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ClayProgressBar b) { b.Repaint(); b.ApplyValue(); }
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
        }

        /// <summary>Recomputes the track recipe.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;
            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var fill = ResolveFill();

            // A progress track is never interactive and never hovers —
            // it is a readout, not a control. Resting is the honest tier.
            ClaySurfaceParts.Paint(
                _parts, dark, SoftDepthTier.Well, SoftInteractionState.Rest,
                ClaySurface.TopLeft(ClayCornerRadius),
                small: true,
                ResolveTrack(dark, fill));

            if (Template?.FindName("ClayProgressFillBrush", this)
                is SolidColorBrush b)
            {
                b.Color = fill;
            }
        }

        private Color ResolveTrack(bool dark, Color fill)
        {
            // The track is the fill, sunk. Deriving it from the fill
            // rather than the page keeps the groove in the same family
            // as the thing that fills it.
            return ClaySurface.Shade(fill, dark ? 0.55 : 0.16);
        }

        private Color ResolveFill()
        {
            var baseFill = ClayFill;
            if (baseFill.A == 0)
            {
                var token = Application.Current?.TryFindResource(
                    "AccentBrush") as SolidColorBrush;
                baseFill = token?.Color ?? Colors.Gray;
            }
            return baseFill;
        }

        /// <summary>Pushes the clamped ratio onto the fill host.</summary>
        private void ApplyValue()
        {
            var host = Template?.FindName("ClayProgressFillHost", this)
                as FrameworkElement;
            if (host is null) return;

            double ratio = double.IsNaN(Value) ? 0 : Math.Clamp(Value, 0, 1);
            // Never collapse to zero WIDTH: a fill of exactly 0 with a
            // rounded corner still paints a sliver on some rasterisers.
            host.Visibility = ratio > 0.001 ? Visibility.Visible
                                             : Visibility.Collapsed;
            host.Width = Math.Max(0, ActualWidth * ratio);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            var s = base.ArrangeOverride(arrangeBounds);
            ApplyValue();
            return s;
        }
    }

    /// <summary>
    /// Clay progress as an arc ring.
    /// <para>
    /// THE ONE SURFACE IN THIS FAMILY THAT IS NOT A MOULD. Every other
    /// clay control resolves through ClaySurfaceParts, which paints the
    /// Well as gradient insets inside the element's own rounded
    /// rectangle. A ring is a STROKE, and a linear gradient cannot follow
    /// a circular path in WPF without hand-built Path geometry -- which
    /// would cost more than the ring earns on screen. So the track is a
    /// flat sunk circle carrying an explicit 1px edge for the same 3:1
    /// boundary every other surface gets. It is a deliberate deviation,
    /// not an oversight, and it is the only one.
    /// </para>
    /// </summary>
    public class ClayProgressRing : Control
    {
        /// <summary>Ring thickness.</summary>
        public static readonly DependencyProperty ClayThicknessProperty =
            DependencyProperty.Register(
                nameof(ClayThickness), typeof(double),
                typeof(ClayProgressRing),
                new FrameworkPropertyMetadata(12.0, OnClayChanged));

        public double ClayThickness
        {
            get => (double)GetValue(ClayThicknessProperty);
            set => SetValue(ClayThicknessProperty, value);
        }

        /// <summary>Completion, 0..1.</summary>
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value), typeof(double),
                typeof(ClayProgressRing),
                new FrameworkPropertyMetadata(0.0, OnClayChanged));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>Stroke colour. Transparent uses the accent.</summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClayProgressRing),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        /// <summary>Centre readout. Optional.</summary>
        public static readonly DependencyProperty ClayCenterTextProperty =
            DependencyProperty.Register(
                nameof(ClayCenterText), typeof(string),
                typeof(ClayProgressRing),
                new FrameworkPropertyMetadata(string.Empty, OnClayChanged));

        public string ClayCenterText
        {
            get => (string)GetValue(ClayCenterTextProperty);
            set => SetValue(ClayCenterTextProperty, value);
        }

        private bool _wired;

        public ClayProgressRing()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            Focusable = false;
            Width = 96;
            Height = 96;
        }

        private static void OnClayChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ClayProgressRing r) r.ApplyArc();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => ApplyArc());
            SizeChanged += (_, _) => ApplyArc();
        }

        /// <summary>
        /// Recomputes the dash from the ellipse's ACTUAL circumference.
        /// </summary>
        public void ApplyArc()
        {
            if (Template?.FindName("ClayRingArc", this) is not Ellipse arc)
                return;

            Wire();

            double stroke = Math.Max(1, ClayThickness);
            double diameter = Math.Min(ActualWidth, ActualHeight);
            if (diameter <= 0)
            {
                // Not laid out yet. Setting a dash computed from a zero
                // size is what produces a ring that renders as a dot on
                // the first frame and corrects itself a frame later.
                arc.StrokeDashArray = null;
                return;
            }

            double radius = (diameter - stroke) / 2;
            if (radius <= 0)
            {
                arc.StrokeDashArray = null;
                return;
            }

            double circumference = 2 * Math.PI * radius;
            double ratio = double.IsNaN(Value) ? 0 : Math.Clamp(Value, 0, 1);
            double dash = circumference * ratio;

            arc.StrokeDashArray = new DoubleCollection { dash, circumference - dash };
            arc.StrokeDashOffset = -circumference * 0.25;

            if (Template?.FindName("ClayRingBrush", this) is SolidColorBrush b)
            {
                var token = Application.Current?.TryFindResource(
                    "AccentBrush") as SolidColorBrush;
                var fill = ClayFill;
                if (fill.A == 0) fill = token?.Color ?? Colors.Gray;
                b.Color = fill;
            }
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            var s = base.ArrangeOverride(arrangeBounds);
            ApplyArc();
            return s;
        }
    }
}
