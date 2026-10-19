// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClayToggleSwitch.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the switch
//
// Two clay objects in one control: a track the thumb rides, and the
// thumb itself. The thumb keeps the Raised tier in BOTH positions —
// sliding it must not flatten it, or it stops reading as something you
// can grab.
//
// ── STATE IS POSITION, NOT COLOUR ────────────────────────────────────
// The track's fill changes with state, but that is never the signal on
// its own: the thumb's X POSITION is the primary cue. Position is a
// non-colour cue, so this control satisfies WCAG 1.4.1 by geometry rather
// than by leaning on a hue shift nobody with reduced colour perception
// can see.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Input;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Claymorphism
{
    /// <summary>A claymorphic toggle switch.</summary>
    public class ClayToggleSwitch : CheckBox
    {
        /// <summary>Track corner radius.</summary>
        public static readonly DependencyProperty ClayCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ClayCornerRadius), typeof(CornerRadius),
                typeof(ClayToggleSwitch),
                new FrameworkPropertyMetadata(
                    new CornerRadius(16), OnClayChanged));

        public CornerRadius ClayCornerRadius
        {
            get => (CornerRadius)GetValue(ClayCornerRadiusProperty);
            set => SetValue(ClayCornerRadiusProperty, value);
        }

        /// <summary>Track colour. Transparent means "use the accent".</summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClayToggleSwitch),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        private readonly ClaySurfaceParts _parts = new ClaySurfaceParts();
        private bool _bound;
        private bool _wired;

        public ClayToggleSwitch()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
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
            if (d is ClayToggleSwitch t) t.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());

            DependencyPropertyDescriptor.FromProperty(
                IsCheckedProperty, typeof(ToggleButton))
                .AddValueChanged(this, (_, _) => Repaint());
            DependencyPropertyDescriptor.FromProperty(
                IsKeyboardFocusedProperty, typeof(KeyboardNavigation))
                .AddValueChanged(this, (_, _) => Repaint());
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (IsKeyboardFocused) return SoftInteractionState.Focused;
            if (IsMouseOver) return SoftInteractionState.Hover;
            return SoftInteractionState.Rest;
        }

        /// <summary>Recomputes and applies the whole recipe.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;
            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            // The track is a Resting-tier object: it sits ON the page and
            // the thumb sits on the track. Two tiers, nested, is the
            // whole visual grammar of a clay switch.
            ClaySurfaceParts.Paint(
                _parts, dark, SoftDepthTier.Resting, ResolveState(),
                ClaySurface.TopLeft(ClayCornerRadius),
                small: true,
                ResolveFill(dark));
        }

        private Color ResolveFill(bool dark)
        {
            var baseFill = ClayFill;
            if (baseFill.A == 0)
            {
                var token = Application.Current?.TryFindResource(
                    "AccentBrush") as SolidColorBrush;
                baseFill = token?.Color ?? Colors.Gray;
            }
            if (!IsEnabled) return ClaySurface.Shade(baseFill, 0.55);
            if (IsChecked == false) return ClaySurface.Shade(baseFill, 0.28);
            return baseFill;
        }
    }
}
