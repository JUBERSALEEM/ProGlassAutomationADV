// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClayField.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the pressed-in text field
//
// A field is CONCAVE. Every other clay object is pressed OUT of the
// page; a field is pressed INTO it. That is why this resolves at the
// Well tier, where the insets are deepest and there is no outer drop at
// all — concavity has no mass.
//
// The one thing a hand-rolled clay field must not lose is the caret.
// PART_ContentHost is not optional: WPF draws text AND blinks the caret
// through that part, and a template without it renders an empty box that
// still takes focus. It is in the template for that reason alone.
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
    /// <summary>A claymorphic text field. Concave by design.</summary>
    public class ClayField : TextBox
    {
        /// <summary>Corner radius. Clay exaggerates roundness.</summary>
        public static readonly DependencyProperty ClayCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ClayCornerRadius), typeof(CornerRadius),
                typeof(ClayField),
                new FrameworkPropertyMetadata(
                    new CornerRadius(22), OnClayChanged));

        public CornerRadius ClayCornerRadius
        {
            get => (CornerRadius)GetValue(ClayCornerRadiusProperty);
            set => SetValue(ClayCornerRadiusProperty, value);
        }

        /// <summary>
        /// The well's own colour. Transparent means "use the ramp's
        /// surface", so a caller cannot accidentally paint clay with an
        /// off-family colour and break the single-light-source rule.
        /// </summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClayField),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        /// <summary>Placeholder shown while the field is empty.</summary>
        public static readonly DependencyProperty ClayPlaceholderProperty =
            DependencyProperty.Register(
                nameof(ClayPlaceholder), typeof(string),
                typeof(ClayField),
                new FrameworkPropertyMetadata(string.Empty, OnClayChanged));

        public string ClayPlaceholder
        {
            get => (string)GetValue(ClayPlaceholderProperty);
            set => SetValue(ClayPlaceholderProperty, value);
        }

        /// <summary>
        /// Placeholder colour. Deliberately dimmer than real text but NOT
        /// at contrast-lawyer levels — a placeholder that fails 1.4.3 is
        /// worse than one that is merely soft, because it looks like a
        /// label the user is meant to read.
        /// </summary>
        public static readonly DependencyProperty ClayPlaceholderBrushProperty =
            DependencyProperty.Register(
                nameof(ClayPlaceholderBrush), typeof(Brush),
                typeof(ClayField),
                new FrameworkPropertyMetadata(null, OnClayChanged));

        public Brush ClayPlaceholderBrush
        {
            get => (Brush)GetValue(ClayPlaceholderBrushProperty);
            set => SetValue(ClayPlaceholderBrushProperty, value);
        }

        private readonly ClaySurfaceParts _parts = new ClaySurfaceParts();
        private bool _bound;
        private bool _wired;

        public ClayField()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            // The field owns its own background: the well IS the fill.
            Background = Brushes.Transparent;
            BorderThickness = new Thickness(0);
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
            if (d is ClayField f) f.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());

            // A field repaints on IsKeyboardFocused too: the focused well
            // is a Raised tier, so leaving the focus ring to XAML alone
            // would show a ring around a flat well.
            DependencyPropertyDescriptor.FromProperty(
                IsKeyboardFocusedProperty, typeof(KeyboardNavigation))
                .AddValueChanged(this, (_, _) => Repaint());
            DependencyPropertyDescriptor.FromProperty(
                IsMouseOverProperty, typeof(UIElement))
                .AddValueChanged(this, (_, _) => Repaint());
        }

        /// <summary>
        /// A focused field is PROMOTED, not restyled. The well lifts to
        /// Raised so there is depth under the focus ring rather than a
        /// flat outline around a hole.
        /// </summary>
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

            // Well is the base; focus promotes it through the same
            // SoftDepthRules the rest of the family uses, so the ring
            // always sits on a surface with visible mass.
            var depth = IsKeyboardFocused
                ? SoftDepthRules.Promote(SoftDepthTier.Well)
                : SoftDepthTier.Well;

            ClaySurfaceParts.Paint(
                _parts, dark, depth, state,
                ClaySurface.TopLeft(ClayCornerRadius),
                small: false,
                ResolveFill(dark));
        }

        private Color ResolveFill(bool dark)
        {
            var baseFill = ClayFill;
            if (baseFill.A == 0)
            {
                var token = Application.Current?.TryFindResource(
                    "SurfacePrimaryBrush") as SolidColorBrush;
                baseFill = token?.Color ?? Colors.White;
            }
            if (!IsEnabled)
            {
                return ClaySurface.Shade(baseFill, 0.35);
            }
            return baseFill;
        }
    }
}
