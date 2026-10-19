// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClayMenuRow.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the menu / nav row
//
// The row that fills a dropdown, a side panel and a settings list. One
// control for all three, because they differ only in width — and a
// "navigation rail" that is a different control from a "menu item" is
// how two halves of an app end up disagreeing about hover height.
//
// ── SELECTION IS NOT THE ONLY SIGNAL ─────────────────────────────────
// A selected row is tinted AND marked. The caller supplies the glyph;
// this control always draws a leading marker column so the selected row
// has a non-colour cue, which is what 1.4.1 asks for.
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
    /// <summary>A claymorphic menu or navigation row.</summary>
    public class ClayMenuRow : Button
    {
        /// <summary>Corner radius.</summary>
        public static readonly DependencyProperty ClayCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ClayCornerRadius), typeof(CornerRadius),
                typeof(ClayMenuRow),
                new FrameworkPropertyMetadata(
                    new CornerRadius(18), OnClayChanged));

        public CornerRadius ClayCornerRadius
        {
            get => (CornerRadius)GetValue(ClayCornerRadiusProperty);
            set => SetValue(ClayCornerRadiusProperty, value);
        }

        /// <summary>Surface colour. Transparent uses the ramp's surface.</summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClayMenuRow),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        /// <summary>Leading glyph. Optional.</summary>
        public static readonly DependencyProperty ClayRowGlyphProperty =
            DependencyProperty.Register(
                nameof(ClayRowGlyph), typeof(string),
                typeof(ClayMenuRow),
                new FrameworkPropertyMetadata(string.Empty, OnClayChanged));

        public string ClayRowGlyph
        {
            get => (string)GetValue(ClayRowGlyphProperty);
            set => SetValue(ClayRowGlyphProperty, value);
        }

        /// <summary>
        /// True when this row is the current one. Lifts the row a tier
        /// AND shows the marker, so selection survives greyscale.
        /// </summary>
        public static readonly DependencyProperty IsSelectedRowProperty =
            DependencyProperty.Register(
                nameof(IsSelectedRow), typeof(bool),
                typeof(ClayMenuRow),
                new FrameworkPropertyMetadata(false, OnClayChanged));

        public bool IsSelectedRow
        {
            get => (bool)GetValue(IsSelectedRowProperty);
            set => SetValue(IsSelectedRowProperty, value);
        }

        private readonly ClaySurfaceParts _parts = new ClaySurfaceParts();
        private bool _bound;
        private bool _wired;

        public ClayMenuRow()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
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
            if (d is ClayMenuRow r) r.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());

            DependencyPropertyDescriptor.FromProperty(
                IsPressedProperty, typeof(ButtonBase))
                .AddValueChanged(this, (_, _) => Repaint());
            DependencyPropertyDescriptor.FromProperty(
                IsKeyboardFocusedProperty, typeof(KeyboardNavigation))
                .AddValueChanged(this, (_, _) => Repaint());
            DependencyPropertyDescriptor.FromProperty(
                IsMouseOverProperty, typeof(ButtonBase))
                .AddValueChanged(this, (_, _) => Repaint());
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (IsPressed) return SoftInteractionState.Pressed;
            if (IsKeyboardFocused) return SoftInteractionState.Focused;
            if (IsMouseOver) return SoftInteractionState.Hover;
            return SoftInteractionState.Rest;
        }

        /// <summary>Recomputes and applies the whole recipe.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;
            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            // A selected row is RAISED; an unselected one is a plain
            // resting surface. One tier apart is enough, and the marker
            // carries the rest of the signal.
            var depth = IsSelectedRow
                ? SoftDepthTier.Raised
                : SoftDepthTier.Resting;

            ClaySurfaceParts.Paint(
                _parts, dark, depth, ResolveState(),
                ClaySurface.TopLeft(ClayCornerRadius),
                small: true,
                ResolveFill());
        }

        private Color ResolveFill()
        {
            var baseFill = ClayFill;
            if (baseFill.A == 0)
            {
                var token = Application.Current?.TryFindResource(
                    "SurfaceSecondaryBrush") as SolidColorBrush;
                baseFill = token?.Color ?? Colors.White;
            }
            if (!IsEnabled) return ClaySurface.Shade(baseFill, 0.55);
            if (IsSelectedRow) return ClaySurface.Lighten(baseFill, 0.10);
            return baseFill;
        }
    }
}
