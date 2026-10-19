// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Neumorphism/NeuButton.cs
// PROJECT: ProGlassAutomation
// MODULE: Neumorphism — the pressed object
//
// A button that dents into the page rather than changing colour.
//
// ── PRESS IS AN INVERSION, NOT A DIMMING ────────────────────────────
// The two outset shadows swing INSIDE the surface and swap ends. That
// is the one motion neumorphism gets right, because a physical dent
// reads as "pressed" with no colour change at all — and it is also the
// reason the two halves of the pair MUST derive from one depth value:
// an inset pair whose offsets disagree reads as two smudges, not as a
// dent.
//
// ── AND IT STILL MOVES THE FILL ──────────────────────────────────────
// The inversion alone is a shadow-only state change, which is the class
// of cue WCAG 1.4.1 excludes and which is invisible to anyone with
// reduced contrast sensitivity. So the fill also darkens by 10%. In a
// style with no colour boundary at all, that second signal is not a
// nicety — it is the only state cue some users will ever perceive.
//
// ── HOVER IS NOT SPENT ON THE INVERSION ──────────────────────────────
// The convex-to-concave transition is this style's most powerful piece
// of feedback, and it is therefore reserved for press alone. Hover
// promotes a depth tier — a slightly larger extrusion — and leaves the
// inversion alone. If everything dented on hover, nothing would read as
// pressed.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Neu;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Neumorphism
{
    /// <summary>A neumorphic button whose press is a real dent.</summary>
    public class NeuButton : Button
    {
        #region ── Dependency properties ──

        /// <summary>Corner radius. 12–20px; never balloon-round.</summary>
        public static readonly DependencyProperty NeuCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(NeuCornerRadius), typeof(CornerRadius),
                typeof(NeuButton),
                new FrameworkPropertyMetadata(
                    new CornerRadius(12), OnNeuChanged));

        public CornerRadius NeuCornerRadius
        {
            get => (CornerRadius)GetValue(NeuCornerRadiusProperty);
            set => SetValue(NeuCornerRadiusProperty, value);
        }

        /// <summary>
        /// True for a primary action. Primary CTAs get the ACCENT, and
        /// the accent is not applied to the whole object — an accent
        /// surface would destroy the monochrome calm the style depends
        /// on. It goes on the LABEL and a 1px ring, which is what makes
        /// a button findable against a page it shares a colour with.
        /// </summary>
        public static readonly DependencyProperty NeuPrimaryProperty =
            DependencyProperty.Register(
                nameof(NeuPrimary), typeof(bool),
                typeof(NeuButton),
                new FrameworkPropertyMetadata(false, OnNeuChanged));

        public bool NeuPrimary
        {
            get => (bool)GetValue(NeuPrimaryProperty);
            set => SetValue(NeuPrimaryProperty, value);
        }

        /// <summary>Label colour. Transparent derives it from the accent.</summary>
        public static readonly DependencyProperty NeuLabelProperty =
            DependencyProperty.Register(
                nameof(NeuLabel), typeof(Color),
                typeof(NeuButton),
                new FrameworkPropertyMetadata(Colors.Transparent, OnNeuChanged));

        public Color NeuLabel
        {
            get => (Color)GetValue(NeuLabelProperty);
            set => SetValue(NeuLabelProperty, value);
        }

        #endregion

        private readonly NeuSurfaceParts _parts = new NeuSurfaceParts();
        private bool _bound;
        private bool _wired;

        public NeuButton()
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

        private static void OnNeuChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NeuButton b) b.Repaint();
        }

        /// <summary>
        /// The state machine. Precedence is deliberate: a disabled button
        /// still being hovered must read as disabled, and a button that
        /// has just been clicked out of must not flicker through hover on
        /// its way back to rest.
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

        /// <summary>Recomputes and applies the extrusion.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            // Buttons sit at Raised: something you can act on should not
            // be flush with the list it lives in.
            NeuSurfaceParts.Paint(
                _parts, dark, SoftDepthTier.Raised, ResolveState(),
                NeuSurface.TopLeft(NeuCornerRadius), ResolveFill(dark));

            UpdateLabel();
        }

        private Color ResolveFill(bool dark)
        {
            // The page colour, always. Even a primary button keeps the
            // monochrome base and carries its emphasis on the label and
            // the ring — an accent-filled neumorphic button is a
            // different style wearing this one's shadows.
            var brush = Application.Current?.TryFindResource(
                "ModuleBackgroundBrush") as SolidColorBrush;
            return brush?.Color ?? Colors.Gray;
        }

        /// <summary>
        /// Publishes the label colour. A primary button's label takes the
        /// ACCENT; a secondary one takes the body ink at its full
        /// contrast. Both are governed by WCAG 1.4.3, which is the one
        /// requirement this style can actually satisfy on its own, and it
        /// is what makes a clickable thing findable.
        /// </summary>
        private void UpdateLabel()
        {
            var text = NeuLabel;
            if (text.A == 0)
            {
                var key = NeuPrimary
                    ? "AccentBrush"
                    : "PrimaryForegroundBrush";
                var token = Application.Current?.TryFindResource(key)
                    as SolidColorBrush;
                text = token?.Color ?? Colors.White;
            }
            if (Template?.FindName("NeuLabelBrush", this)
                is SolidColorBrush brush)
            {
                brush.Color = text;
            }
        }
    }
}