// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Swiss/SwissButton.cs
// PROJECT: ProGlassAutomation
// MODULE: Swiss — the inverting control
//
// ── STATE IS AN INVERSION, NOT A TINT ────────────────────────────────
// This is the whole control. At rest it is ink on paper; hovered or
// pressed it is paper on ink. Nothing between.
//
// The tempting Swiss implementation is to darken the fill by 8% on
// hover. That is wrong on two counts. It is a tint, and a tint is a
// decoration; and it spends a state change that costs the reader
// nothing. An inversion is the highest-contrast state change available
// in the entire engine — roughly 20:1 against maybe 1.2:1 for the tint —
// and it is free to produce, because there is no shadow to recompose.
//
// ── AND THE GEOMETRY MUST NOT MOVE ───────────────────────────────────
// No lift, no scale, no translate, at any state. The grid is a rational
// system and interaction is not permitted to disturb its geometry: a
// button that rises on hover has stopped sitting on the baseline grid,
// which is the one thing the whole composition is built on.
//
// The only movement this style permits is a directional arrow glyph
// sliding along its own baseline, because a glyph moving horizontally
// is a TYPOGRAPHIC element rather than a spatial one. It is exposed as
// a flag and left to the host, because a button control does not know
// whether it has an arrow.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme.Swiss;

namespace ProGlassAutomation.Controls.Swiss
{
    /// <summary>A Swiss button whose state is a full colour inversion.</summary>
    public class SwissButton : Button
    {
        #region ── Dependency properties ──

        /// <summary>
        /// Corner radius, exposed but ALWAYS overridden to zero. It
        /// exists so a host can bind the family token to it and discover
        /// the override at the call site rather than wondering why its
        /// 4px is having no effect.
        /// </summary>
        public static readonly DependencyProperty SwissCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(SwissCornerRadius), typeof(CornerRadius),
                typeof(SwissButton),
                new PropertyMetadata(new CornerRadius(0)));

        public CornerRadius SwissCornerRadius
        {
            get => (CornerRadius)GetValue(SwissCornerRadiusProperty);
            set => SetValue(SwissCornerRadiusProperty, value);
        }

        /// <summary>
        /// Whether this button carries a directional arrow. Swiss
        /// buttons are expected to state their direction as typography;
        /// the host decides whether it applies.
        /// </summary>
        public static readonly DependencyProperty SwissDirectionalProperty =
            DependencyProperty.Register(
                nameof(SwissDirectional), typeof(bool),
                typeof(SwissButton),
                new PropertyMetadata(false, OnSwissChanged));

        public bool SwissDirectional
        {
            get => (bool)GetValue(SwissDirectionalProperty);
            set => SetValue(SwissDirectionalProperty, value);
        }

        #endregion

        private readonly SwissSurfaceParts _parts = new SwissSurfaceParts();
        private bool _bound;
        private bool _wired;

        public SwissButton()
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

        private static void OnSwissChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SwissButton b) b.Repaint();
        }

        /// <summary>
        /// Precedence matters and is not arbitrary. A disabled button
        /// that is still hovered must read as disabled, or the inversion
        /// would fight the dimming; and a button clicked out of must not
        /// flicker back through hover on its way to rest.
        /// </summary>
        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (IsPressed) return SoftInteractionState.Pressed;
            if (IsKeyboardFocused) return SoftInteractionState.Focused;
            if (IsMouseOver) return SoftInteractionState.Hover;
            return SoftInteractionState.Rest;
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());

            // These are DPs with no routed event, so the repaint is hung
            // off the properties directly.
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

        /// <summary>Recomputes and applies the construction.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            SwissSurfaceParts.Paint(
                _parts, dark, SoftDepthTier.Raised, ResolveState(),
                interactive: true, selected: false,
                SwissSurface.Token("WindowBackgroundBrush"),
                SwissSurface.Token("PrimaryForegroundBrush"));

            UpdateLabel();
            UpdateArrow();
        }

        /// <summary>
        /// Publishes the ink colour. The label and the ground have already
        /// swapped by the time this runs, so the label must follow — an
        /// inverted button with unchanged label text is black on black,
        /// which is the one way this state machine can make the app
        /// UNREADABLE while technically inverting it.
        /// <para>
        /// Written to the inherited <c>Foreground</c> rather than to a
        /// named brush in the template. The template's ContentPresenter
        /// binds <c>TextElement.Foreground</c> to it, so the whole label
        /// follows in one assignment — and no host that swaps its own
        /// ContentPresenter out can accidentally get stranded with the
        /// previous state's colour.
        /// </para>
        /// </summary>
        private void UpdateLabel()
        {
            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var profile = SwissPalette.For(dark);
            if (profile is null) return;

            var stack = new SwissRecipe(profile, dark).Resolve(
                SoftDepthTier.Raised, ResolveState(),
                interactive: true, selected: false);

            // Inverted: ink sits on the inverted fill. At rest: ink on
            // paper. Both branches read "which colour is the text",
            // which is the only question a foreground answers.
            Foreground = Frozen(stack.Inverted
                ? stack.Contrast.Fill
                : stack.Contrast.Ink);
        }

        private static SolidColorBrush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>
        /// Shows the arrow only when the host asked for one. Swiss
        /// buttons are expected to state direction as typography, but
        /// that is the host's decision — a control cannot know whether it
        /// navigates anywhere.
        /// </summary>
        private void UpdateArrow()
        {
            if (Template?.FindName("PART_SwissArrow", this)
                is FrameworkElement arrow)
            {
                arrow.Visibility = SwissDirectional
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }
    }
}