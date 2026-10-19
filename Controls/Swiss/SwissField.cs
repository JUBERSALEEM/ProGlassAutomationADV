// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Swiss/SwissField.cs
// PROJECT: ProGlassAutomation
// MODULE: Swiss — the underlined field
//
// ── UNDERLINE ONLY, NOT A BOX ────────────────────────────────────────
// The Swiss input is transparent with a 1px bottom rule that becomes 2px
// on focus. Not a bordered rectangle.
//
// A box would be decoration, and worse, it would be a box the user has
// to look THROUGH to find the text — the input is a line you write on,
// and the surrounding furniture is precisely what this style exists to
// remove.
//
// ── FOCUS IS THE RULE GETTING HEAVIER, NOT A GLOW ────────────────────
// No glow ring, no soft halo, no colour wash. The rule goes from 1px to
// 2px and the ink goes to full strength. That is a shape change, which
// means it survives a greyscale print and a colour-blind reader, and
// it is also why this field is far easier to focus than it looks: there
// is no low-contrast decoration anywhere near it to be mistaken for the
// state.
//
// ── NO PLACEHOLDER DOING A LABEL'S JOB ───────────────────────────────
// The label sits above in small caps. A placeholder that vanishes on the
// first keystroke takes the field's only label away from the person
// halfway through typing, which is an accessibility problem this control
// simply declines to have.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services;
using System.ComponentModel;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme.Swiss;
using ProGlassAutomation.Services.Theme;
using System.Windows.Controls;
using System.Windows;

namespace ProGlassAutomation.Controls.Swiss
{
    /// <summary>A Swiss field: transparent, with a rule that gets heavier.</summary>
    public class SwissField : ContentControl
    {
        /// <summary>
        /// The field's label, shown ABOVE the rule in small caps. Kept as
        /// a real control rather than a placeholder so it cannot vanish
        /// mid-typing.
        /// </summary>
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(
                nameof(Label), typeof(string),
                typeof(SwissField),
                new FrameworkPropertyMetadata(null, OnSwissChanged));

        public string? Label
        {
            get => (string?)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        /// <summary>Current interaction state, readable by tests.</summary>
        public static readonly DependencyProperty SwissStateProperty =
            DependencyProperty.Register(
                nameof(SwissState), typeof(SoftInteractionState),
                typeof(SwissField),
                new PropertyMetadata(
                    SoftInteractionState.Rest, OnSwissStateChanged));

        public SoftInteractionState SwissState
        {
            get => (SoftInteractionState)GetValue(SwissStateProperty);
            set => SetValue(SwissStateProperty, value);
        }

        private readonly SwissSurfaceParts _parts = new SwissSurfaceParts();
        private bool _bound;
        private bool _wired;

        public SwissField()
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Center;
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

        private static void OnSwissChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SwissField f) f.Repaint();
        }

        private static void OnSwissStateChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SwissField f) f.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());

            // Focus belongs to the HOSTED control — the field is a shell
            // around a TextBox or a ComboBox — so the field tracks the
            // focus scope rather than an IsFocused flag of its own.
            DependencyPropertyDescriptor.FromProperty(
                IsKeyboardFocusWithinProperty, typeof(Control))
                .AddValueChanged(this, (_, _) => Repaint());
        }

        /// <summary>Recomputes and applies the rule.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var profile = SwissPalette.For(dark);

            if (profile is null)
            {
                _parts.ApplyPlain(
                    SwissSurface.Token("WindowBackgroundBrush"),
                    SwissSurface.Token("PrimaryForegroundBrush"));
                return;
            }

            // A focused field is SELECTED, which is what puts the rule at
            // 2px. Reusing the selection weight rather than inventing a
            // second focus weight is the point: there is exactly one
            // heavier-line weight in this style and focus is one of the
            // things that earns it.
            bool focused = IsKeyboardFocusWithin;

            var stack = new SwissRecipe(profile, dark).Resolve(
                SoftDepthTier.Well,
                focused ? SoftInteractionState.Focused
                        : ResolveState(),
                interactive: true,
                selected: focused);

            _parts.Apply(stack);
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            return SwissState;
        }
    }
}