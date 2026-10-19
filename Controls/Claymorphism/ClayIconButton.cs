// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClayIconButton.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the square icon button
//
// An icon button is a button that is not allowed to be oblong. The
// whole difference from ClayButton is geometric: the width tracks the
// height, so the radius stays a squircle instead of stretching into a
// lozenge as soon as the label is longer than the glyph.
//
// ClayIconGlyph is a string, not an ImageSource, because this design
// system draws its icons as glyph runs — forcing an image here would
// make every icon a second asset to keep in step with the font.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Input;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Claymorphism
{
    /// <summary>A claymorphic square icon button.</summary>
    public class ClayIconButton : Button
    {
        /// <summary>Corner radius.</summary>
        public static readonly DependencyProperty ClayCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ClayCornerRadius), typeof(CornerRadius),
                typeof(ClayIconButton),
                new FrameworkPropertyMetadata(
                    new CornerRadius(16), OnClayChanged));

        public CornerRadius ClayCornerRadius
        {
            get => (CornerRadius)GetValue(ClayCornerRadiusProperty);
            set => SetValue(ClayCornerRadiusProperty, value);
        }

        /// <summary>Surface colour. Transparent uses the ramp's surface.</summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClayIconButton),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        /// <summary>Label colour. Transparent derives from the ink token.</summary>
        public static readonly DependencyProperty ClayLabelProperty =
            DependencyProperty.Register(
                nameof(ClayLabel), typeof(Color),
                typeof(ClayIconButton),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayLabel
        {
            get => (Color)GetValue(ClayLabelProperty);
            set => SetValue(ClayLabelProperty, value);
        }

        /// <summary>The glyph rendered at the centre of the button.</summary>
        public static readonly DependencyProperty ClayIconGlyphProperty =
            DependencyProperty.Register(
                nameof(ClayIconGlyph), typeof(string),
                typeof(ClayIconButton),
                new FrameworkPropertyMetadata(string.Empty, OnClayChanged));

        public string ClayIconGlyph
        {
            get => (string)GetValue(ClayIconGlyphProperty);
            set => SetValue(ClayIconGlyphProperty, value);
        }

        private readonly ClaySurfaceParts _parts = new ClaySurfaceParts();
        private readonly ClayPressHost _press = new ClayPressHost();
        private bool _bound;
        private bool _wired;

        public ClayIconButton()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            // Square. Width tracks Height so the clay geometry holds.
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var size = base.MeasureOverride(constraint);
            // Only widen when there is room; forcing a width in an
            // unbounded panel would defeat virtualisation.
            if (!double.IsInfinity(constraint.Width))
            {
                size.Width = size.Height;
            }
            return size;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (!_bound)
            {
                _parts.Bind(this);
                _press.Bind(this, "ClayIconPressScale", "ClayIconPressTranslate");
                _bound = true;
                Wire();
            }
            Repaint();
        }

        private static void OnClayChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ClayIconButton b) b.Repaint();
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
            var state = ResolveState();
            ClaySurfaceParts.Paint(
                _parts, dark, SoftDepthTier.Raised, state,
                ClaySurface.TopLeft(ClayCornerRadius),
                small: true,
                ResolveFill(dark));
            ApplyLabel();
        }

        private Color ResolveFill(bool dark)
        {
            var baseFill = ClayFill;
            if (baseFill.A == 0)
            {
                var token = Application.Current?.TryFindResource(
                    "SurfaceSecondaryBrush") as SolidColorBrush;
                baseFill = token?.Color ?? Colors.White;
            }
            if (!IsEnabled) return ClaySurface.Shade(baseFill, 0.55);
            return baseFill;
        }

        /// <summary>Resolves the ink and republishes it on the glyph brush.</summary>
        private void ApplyLabel()
        {
            var ink = ClayLabel;
            if (ink.A == 0)
            {
                var token = Application.Current?.TryFindResource(
                    "TextPrimaryBrush") as SolidColorBrush;
                ink = token?.Color ?? Colors.Black;
            }
            if (!IsEnabled) ink = ClaySurface.Shade(ink, 0.35);

            // The brush is looked up by key in the template's own
            // resource dictionary and mutated in place; the presenter
            // already holds this instance through its StaticResource, so
            // the glyph recolours without the template being rebuilt.
            if (Template?.FindName("ClayIconBrush", this) is SolidColorBrush b)
            {
                b.Color = ink;
            }
        }
    }
}
