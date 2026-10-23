// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClaySlider.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the slider
//
// A concave track with a raised thumb riding it. Two clay objects,
// nested — the same grammar as the switch, which is why both resolve
// through the same recipe rather than each hand-rolling a shadow.
//
// ── WHY THIS IS NOT System.Windows.Controls.Slider ────────────────────
// Deriving from Slider would drag in its own chrome, its own keyboard
// handling and its own minimum, all of which would then need fighting.
// This is a Control: the template owns the geometry, and the thumb
// position is pushed from Value. It is a material, not a behaviour.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Claymorphism
{
    /// <summary>A claymorphic slider.</summary>
    public class ClaySlider : Control
    {
        /// <summary>Track corner radius.</summary>
        public static readonly DependencyProperty ClayCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ClayCornerRadius), typeof(CornerRadius),
                typeof(ClaySlider),
                new FrameworkPropertyMetadata(
                    new CornerRadius(9), OnClayChanged));

        public CornerRadius ClayCornerRadius
        {
            get => (CornerRadius)GetValue(ClayCornerRadiusProperty);
            set => SetValue(ClayCornerRadiusProperty, value);
        }

        /// <summary>Lowest selectable value.</summary>
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(
                nameof(Minimum), typeof(double),
                typeof(ClaySlider),
                new FrameworkPropertyMetadata(0.0, OnClayChanged));

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        /// <summary>Highest selectable value.</summary>
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(
                nameof(Maximum), typeof(double),
                typeof(ClaySlider),
                new FrameworkPropertyMetadata(100.0, OnClayChanged));

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        /// <summary>Current value.</summary>
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value), typeof(double),
                typeof(ClaySlider),
                new FrameworkPropertyMetadata(0.0, OnValueChanged));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>Accent used for the filled portion of the track.</summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClaySlider),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        private readonly ClaySurfaceParts _parts = new ClaySurfaceParts();
        private readonly ClaySurfaceParts _thumbParts = new ClaySurfaceParts();
        private bool _bound;
        private bool _wired;

        public ClaySlider()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            Focusable = true;
            Height = 34;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (!_bound)
            {
                _parts.Bind(this);
                // The thumb is a SECOND mould. It names its parts with
                // the same PART_Clay* convention inside the thumb's own
                // template scope, resolved through the thumb host.
                var thumb = Template?.FindName("ClaySliderThumb", this) as Control;
                if (thumb is not null) _thumbParts.Bind(thumb);
                _bound = true;
                Wire();
            }
            Repaint();
        }

        private static void OnClayChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ClaySlider s) s.Repaint();
        }

        private static void OnValueChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ClaySlider s) s.ApplyGeometry();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
        }

        /// <summary>Recomputes the track and thumb recipes.</summary>
        public void Repaint()
        {
            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var accent = ResolveAccent();

            if (_parts.Complete)
            {
                // Track is concave: a groove, not an object.
                ClaySurfaceParts.Paint(
                    _parts, dark, SoftDepthTier.Well, SoftInteractionState.Rest,
                    ClaySurface.TopLeft(ClayCornerRadius),
                    small: true,
                    ClaySurface.Shade(accent, dark ? 0.55 : 0.18));
            }
            if (_thumbParts.Complete)
            {
                var state = IsEnabled
                    ? SoftInteractionState.Rest
                    : SoftInteractionState.Disabled;
                // The thumb STAYS raised in every state. A thumb that
                // flattens on hover stops reading as draggable.
                ClaySurfaceParts.Paint(
                    _thumbParts, dark, SoftDepthTier.Raised, state,
                    14, small: true, accent);
            }

            if (Template?.FindName("ClaySliderFill", this) is Border f)
            {
                var brush = Application.Current?.TryFindResource(
                    "AccentBrush") as SolidColorBrush;
                f.Background = ClayVisualFactory.Frozen(
                    new SolidColorBrush(accent));
                _ = brush;
            }
            ApplyGeometry();
        }

        private Color ResolveAccent()
        {
            var fill = ClayFill;
            if (fill.A == 0)
            {
                var token = Application.Current?.TryFindResource(
                    "AccentBrush") as SolidColorBrush;
                fill = token?.Color ?? Colors.Gray;
            }
            return IsEnabled ? fill : ClaySurface.Shade(fill, 0.55);
        }

        /// <summary>
        /// Maps Value onto the thumb's horizontal offset. The travel is
        /// the track MINUS the thumb, not the track: letting the thumb
        /// centre reach both ends puts it half outside the groove.
        /// </summary>
        private void ApplyGeometry()
        {
            var thumb = Template?.FindName("ClaySliderThumb", this)
                as FrameworkElement;
            var fill = Template?.FindName("ClaySliderFill", this)
                as FrameworkElement;
            if (thumb is null) return;

            double min = Minimum;
            double max = Maximum;
            double span = max - min;
            double ratio = span <= 0 ? 0 : (Value - min) / span;
            ratio = double.IsNaN(ratio) ? 0 : Math.Clamp(ratio, 0, 1);

            double track = ActualWidth;
            double thumbWidth = thumb.ActualWidth > 0 ? thumb.ActualWidth : 28;
            double travel = Math.Max(0, track - thumbWidth);

            thumb.RenderTransform = new TranslateTransform
            {
                X = travel * ratio,
                Y = 0,
            };

            if (fill is not null)
            {
                fill.Width = Math.Max(0, thumbWidth * 0.5 + travel * ratio);
            }
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            var s = base.ArrangeOverride(arrangeBounds);
            ApplyGeometry();
            return s;
        }
    }
}
