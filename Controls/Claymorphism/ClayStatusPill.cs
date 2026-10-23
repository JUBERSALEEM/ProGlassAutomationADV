// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClayStatusPill.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the chip
//
// ── WHY THE INSET IS DROPPED HERE ────────────────────────────────────
// Inset softness is proportional to the corner radius, and a pill's
// radius is its own half-height — so on a 28px chip the inset is asked
// to resolve inside about 14px of travel. It cannot. The roll collapses
// into a hard rim and the chip reads as a smudge rather than a clay
// object. Under roughly 24px tall the inset is dropped and the outer
// drop alone carries the form.
//
// This is not a special case bolted on; it is the same radius-proportional
// rule the rest of the recipe obeys, hitting its limit. `ClaySmall`
// exposes it so the rule is visible at the call site rather than
// inferred.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;

namespace ProGlassAutomation.Controls.Claymorphism
{
    /// <summary>A fully-round clay chip: status, tag, counter.</summary>
    public class ClayStatusPill : ContentControl
    {
        #region ── Dependency properties ──

        /// <summary>
        /// The pill's own colour. A clay status chip carries its STATUS
        /// hue — that is the point of it being a clay object rather than
        /// a tinted rectangle: the colour is load-bearing, not
        /// decorative.
        /// </summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClayStatusPill),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        /// <summary>Label colour. Transparent derives it from the fill.</summary>
        public static readonly DependencyProperty ClayLabelProperty =
            DependencyProperty.Register(
                nameof(ClayLabel), typeof(Color),
                typeof(ClayStatusPill),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayLabel
        {
            get => (Color)GetValue(ClayLabelProperty);
            set => SetValue(ClayLabelProperty, value);
        }

        /// <summary>
        /// Minimum height. Drives the fully-round radius — a pill is a
        /// stadium, never a rounded rectangle, so the radius must track
        /// whatever height the content ends up at.
        /// </summary>
        public static readonly DependencyProperty ClayMinHeightProperty =
            DependencyProperty.Register(
                nameof(ClayMinHeight), typeof(double),
                typeof(ClayStatusPill),
                new FrameworkPropertyMetadata(28.0, OnClayChanged));

        public double ClayMinHeight
        {
            get => (double)GetValue(ClayMinHeightProperty);
            set => SetValue(ClayMinHeightProperty, value);
        }

        #endregion

        private readonly ClaySurfaceParts _parts = new ClaySurfaceParts();
        private bool _bound;
        private bool _wired;

        public ClayStatusPill()
        {
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            SizeChanged += (_, _) => Repaint();
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
            if (d is ClayStatusPill p) p.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
        }

        /// <summary>Recomputes and applies the pill.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            // A stadium: the radius is half the ACTUAL height, so the pill
            // stays fully round whatever the content does to it.
            double height = ActualHeight > 0 ? ActualHeight : ClayMinHeight;
            double radius = height / 2.0;

            // The template's corner radius follows the measured height, so
            // the gradient insets are clipped to the same stadium.
            if (Template?.FindName("PART_ClayDropHost", this)
                is Border host)
            {
                var r = new CornerRadius(radius);
                host.CornerRadius = r;
                if (Template.FindName("PART_ClayHighlight", this)
                    is Border hi) hi.CornerRadius = r;
                if (Template.FindName("PART_ClayShade", this)
                    is Border sh) sh.CornerRadius = r;
                if (Template.FindName("PART_ClayEdge", this)
                    is Border ed) ed.CornerRadius = r;
            }

            ClaySurfaceParts.Paint(
                _parts, dark,
                SoftDepthTier.Resting,
                IsEnabled
                    ? SoftInteractionState.Rest
                    : SoftInteractionState.Disabled,
                radius,
                small: true,
                ResolveFill(dark));

            UpdateLabel(dark);
        }

        private Color ResolveFill(bool dark)
        {
            if (ClayFill.A > 0) return ClayFill;
            var brush = Application.Current?.TryFindResource(
                "AccentBrush") as SolidColorBrush;
            return brush?.Color ?? Colors.Gray;
        }

        private void UpdateLabel(bool dark)
        {
            var text = ClayLabel;
            if (text.A == 0)
            {
                var token = Application.Current?.TryFindResource(
                    "AccentForegroundBrush") as SolidColorBrush;
                text = token?.Color ?? Colors.White;
            }
            if (Template?.FindName("ClayLabelBrush", this)
                is SolidColorBrush brush)
            {
                brush.Color = text;
            }
        }
    }
}