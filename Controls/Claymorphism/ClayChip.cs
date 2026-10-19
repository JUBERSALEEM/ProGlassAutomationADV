// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClayChip.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the label chip
//
// A chip is the documented EXCEPTION in this family. Small semantic
// pills (Feature / Bug / Design / Research) keep an ink fill and do NOT
// pose as cards — they sit ON a clay surface and must read as a
// different class of object.
//
// The temptation is to give a chip the full three-shadow mould. Resist
// it. At 24px tall the insets have nowhere to resolve and the result is
// a muddy smudge, so this control resolves at small:true, where the
// recipe collapses the inner rolls to keep the silhouette clean.
//
// ── WHY THE INK FILL ─────────────────────────────────────────────────
// A pastelled chip on a pastelled card is invisible. Chips therefore
// keep their saturated ink fill and carry their meaning as TEXT, which
// is the one cue that survives both low contrast and a greyscale print.
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
    /// <summary>A claymorphic label chip.</summary>
    public class ClayChip : ContentControl
    {
        /// <summary>Corner radius. Chips are pill-shaped by default.</summary>
        public static readonly DependencyProperty ClayCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ClayCornerRadius), typeof(CornerRadius),
                typeof(ClayChip),
                new FrameworkPropertyMetadata(
                    new CornerRadius(14), OnClayChanged));

        public CornerRadius ClayCornerRadius
        {
            get => (CornerRadius)GetValue(ClayCornerRadiusProperty);
            set => SetValue(ClayCornerRadiusProperty, value);
        }

        /// <summary>
        /// The chip's ink fill. Unlike a surface this does NOT fall back
        /// to the ramp — a chip is meant to be the saturated accent.
        /// </summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClayChip),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        /// <summary>Text colour. Falls back to the accent-foreground ink.</summary>
        public static readonly DependencyProperty ClayLabelProperty =
            DependencyProperty.Register(
                nameof(ClayLabel), typeof(Color),
                typeof(ClayChip),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayLabel
        {
            get => (Color)GetValue(ClayLabelProperty);
            set => SetValue(ClayLabelProperty, value);
        }

        private readonly ClaySurfaceParts _parts = new ClaySurfaceParts();
        private bool _bound;
        private bool _wired;

        public ClayChip()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            Focusable = false;
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
            if (d is ClayChip c) c.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
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
            var state = ResolveState();
            ClaySurfaceParts.Paint(
                _parts, dark, SoftDepthTier.Resting, state,
                ClaySurface.TopLeft(ClayCornerRadius),
                small: true,
                ResolveFill());
            ApplyLabel();
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
            if (!IsEnabled) return ClaySurface.Shade(baseFill, 0.55);
            if (IsMouseOver) return ClaySurface.Lighten(baseFill, 0.08);
            return baseFill;
        }

        private void ApplyLabel()
        {
            var ink = ClayLabel;
            if (ink.A == 0)
            {
                var token = Application.Current?.TryFindResource(
                    "AccentForegroundBrush") as SolidColorBrush;
                ink = token?.Color ?? Colors.White;
            }
            if (!IsEnabled) ink = ClaySurface.Shade(ink, 0.35);
            if (Template?.FindName("ClayChipBrush", this) is SolidColorBrush b)
            {
                b.Color = ink;
            }
        }
    }
}
