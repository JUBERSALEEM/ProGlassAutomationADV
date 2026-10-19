// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/LiquidGlass/LiquidField.cs
// PROJECT: ProGlassAutomation
// MODULE: Liquid Glass — the input
//
// THIS CONTROL IS DELIBERATELY NOT GLASS, AND THAT IS THE POINT.
//
// Liquid Glass is a navigation-layer material. An input is content: the
// user is typing words into it, and those words must stay legible while
// whatever scrolls behind the window moves. A translucent surface over
// moving content costs several points of body contrast, and unlike a
// decorative panel nobody would notice that cost, this one is applied
// directly to text the user came here to read and correct.
//
// So the field takes the Well tier — concave, no outer shadow, no
// backdrop blur — and borrows exactly ONE thing from the family: the
// specular rim, as the focus affordance. That is the one place a lens
// helps, because the rim is a boundary cue and not a fill.
//
// The rule is enforced rather than merely documented. LiquidGlassDepth
// says content may not carry glass; this control reads it at
// construction time so that a future template cannot quietly put the
// material back.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.LiquidGlass;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.LiquidGlass
{
    /// <summary>
    /// A Liquid Glass input: a recessed well with a specular rim.
    /// </summary>
    public class LiquidField : Control
    {
        /// <summary>The label text shown above the well.</summary>
        public static readonly DependencyProperty LiquidLabelProperty =
            DependencyProperty.Register(
                nameof(LiquidLabel), typeof(string),
                typeof(LiquidField),
                new FrameworkPropertyMetadata(null, OnLiquidChanged));

        public string? LiquidLabel
        {
            get => (string?)GetValue(LiquidLabelProperty);
            set => SetValue(LiquidLabelProperty, value);
        }

        /// <summary>The edit surface. Null until the template is applied.</summary>
        public static readonly DependencyProperty LiquidEditorProperty =
            DependencyProperty.Register(
                nameof(LiquidEditor), typeof(TextBox),
                typeof(LiquidField),
                new PropertyMetadata(null, OnLiquidEditorChanged));

        public TextBox? LiquidEditor
        {
            get => (TextBox?)GetValue(LiquidEditorProperty);
            set => SetValue(LiquidEditorProperty, value);
        }

        /// <summary>
        /// The placeholder shown while the field is empty. A field with
        /// no placeholder reads as though it already has content.
        /// </summary>
        public static readonly DependencyProperty LiquidPlaceholderProperty =
            DependencyProperty.Register(
                nameof(LiquidPlaceholder), typeof(string),
                typeof(LiquidField),
                new FrameworkPropertyMetadata(null, OnLiquidChanged));

        public string? LiquidPlaceholder
        {
            get => (string?)GetValue(LiquidPlaceholderProperty);
            set => SetValue(LiquidPlaceholderProperty, value);
        }

        /// <summary>Current interaction state, readable by tests.</summary>
        public static readonly DependencyProperty LiquidStateProperty =
            DependencyProperty.Register(
                nameof(LiquidState), typeof(SoftInteractionState),
                typeof(LiquidField),
                new PropertyMetadata(
                    SoftInteractionState.Rest, OnLiquidStateChanged));

        public SoftInteractionState LiquidState
        {
            get => (SoftInteractionState)GetValue(LiquidStateProperty);
            set => SetValue(LiquidStateProperty, value);
        }

        /// <summary>
        /// Opt in to the translucent treatment anyway.
        /// <para>
        /// Present, and defaulted to false, because a designer working
        /// in the Theme Center needs to see the accessible appearance
        /// next to the decorative one. It is an override, not a
        /// default: nothing in the app sets it, and the code below
        /// records that it was overridden rather than ignoring it.
        /// </para>
        /// </summary>
        public static readonly DependencyProperty AllowGlassContentProperty =
            DependencyProperty.Register(
                nameof(AllowGlassContent), typeof(bool),
                typeof(LiquidField),
                new FrameworkPropertyMetadata(false, OnLiquidChanged));

        public bool AllowGlassContent
        {
            get => (bool)GetValue(AllowGlassContentProperty);
            set => SetValue(AllowGlassContentProperty, value);
        }

        private readonly LiquidSurfaceParts _parts = new LiquidSurfaceParts();
        private bool _bound;
        private bool _wired;

        public LiquidField()
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Center;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            MinHeight = SoftUiAccessibility.MinHitTarget;
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

        private static void OnLiquidChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LiquidField f) f.Repaint();
        }

        private static void OnLiquidStateChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LiquidField f) f.Repaint();
        }

        private static void OnLiquidEditorChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LiquidField f) return;
            if (e.OldValue is TextBox oldEditor) oldEditor.LostFocus -= OnEditorLostFocus;
            if (e.NewValue is TextBox newEditor) newEditor.LostFocus += OnEditorLostFocus;
            f.Repaint();
        }

        private static void OnEditorLostFocus(
            object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb
                && tb.DataContext is LiquidField field)
            {
                field.Repaint();
            }
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
        }

        /// <summary>Recomputes and applies the construction.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            // The placement decision, read from the engine rather than
            // reimplemented here. A field is CONTENT, so the default is
            // no glass; only an explicit override puts the lens back.
            var mayCarryGlass = AllowGlassContent
                && LiquidGlassDepth.Allows(LiquidGlassPlacement.Content);

            var tier = mayCarryGlass
                ? SoftDepthTier.Resting
                : SoftDepthTier.Well;

            LiquidSurfaceParts.Paint(
                _parts, dark, tier, ResolveState(),
                FallbackFill(), FallbackInk());
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (IsKeyboardFocusWithin) return SoftInteractionState.Focused;
            return LiquidState;
        }

        private static Color FallbackFill() =>
            (Application.Current?.TryFindResource("SurfaceInsetBrush")
                as SolidColorBrush)?.Color
            ?? (Application.Current?.TryFindResource("InputBackgroundBrush")
                as SolidColorBrush)?.Color
            ?? Colors.White;

        private static Color FallbackInk() =>
            (Application.Current?.TryFindResource("PrimaryForegroundBrush")
                as SolidColorBrush)?.Color ?? Colors.Black;
    }
}