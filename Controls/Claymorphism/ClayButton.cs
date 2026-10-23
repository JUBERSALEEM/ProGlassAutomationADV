// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClayButton.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the pressed object
//
// A button you push INTO the page.
//
// ── THE PRESS IS THE WHOLE POINT ─────────────────────────────────────
// On every other button in this app, :active changes a colour. Here it
// pushes the object two pixels into the page and takes two percent off
// its scale. That is what pressing clay looks like, and substituting a
// tint for it is the mistake that makes most "clay" buttons feel like
// tinted rectangles.
//
// ── PRESS IS A MOVE, NOT A SHADOW SWAP ──────────────────────────────
// The press used to darken the fill as well, on the theory that a
// shadow-only cue fails WCAG 1.4.1. That theory was right and its
// remedy was wrong for this family: the reference grammar makes press a
// TRANSFORM (translateY(2px) scale(0.98)), and a transform is movement —
// a perceivable non-colour cue in its own right, so 1.4.1 is satisfied
// by the geometry rather than by piling a colour change on top of it.
// Two cues where one is enough is how a control starts to feel mushy.
//
// The tier is deliberately left alone on press (see SoftDepthRules):
// the object does not change depth, it moves.
//
// ── SHADOWS ARE NEVER ANIMATED ───────────────────────────────────────
// The mould is painted as three discrete states rather than interpolated.
// box-shadow/blur animation is the single most expensive thing a soft-UI
// surface can do, and a scrolling grid pays for it on every frame.
// The transform is the only thing that moves, and it is cheap.
//
//// ── REDUCED MOTION ───────────────────────────────────────────────────
// The crossing is an instant state change, not an animation, so the
// preference has nothing to switch off here — the press still reads,
// because it now carries a colour change as well as a shape change.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services;
using System.ComponentModel;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System;

namespace ProGlassAutomation.Controls.Claymorphism
{
    /// <summary>A claymorphic button with a physical press.</summary>
    public class ClayButton : Button
    {
        #region ── Dependency properties ──

        /// <summary>Corner radius. Clay exaggerates roundness.</summary>
        public static readonly DependencyProperty ClayCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ClayCornerRadius), typeof(CornerRadius),
                typeof(ClayButton),
                new FrameworkPropertyMetadata(
                    new CornerRadius(18), OnClayChanged));

        public CornerRadius ClayCornerRadius
        {
            get => (CornerRadius)GetValue(ClayCornerRadiusProperty);
            set => SetValue(ClayCornerRadiusProperty, value);
        }

        /// <summary>
        /// The button's own colour. Transparent means "use the accent",
        /// which is what makes a clay CTA an object on the page rather
        /// than a hole in it.
        /// </summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClayButton),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        /// <summary>
        /// Label colour. Transparent means "derive from the fill", so a
        /// caller cannot accidentally ship unreadable label-on-accent.
        /// </summary>
        public static readonly DependencyProperty ClayLabelProperty =
            DependencyProperty.Register(
                nameof(ClayLabel), typeof(Color),
                typeof(ClayButton),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayLabel
        {
            get => (Color)GetValue(ClayLabelProperty);
            set => SetValue(ClayLabelProperty, value);
        }

        #endregion

        private readonly ClaySurfaceParts _parts = new ClaySurfaceParts();
        private readonly ClayPressHost _press = new ClayPressHost();
        private bool _bound;
        private bool _wired;

        public ClayButton()
        {
            // A real Button, so Command, Click and keyboard activation all
            // behave normally. This is a material, not a reimplementation
            // of a button.
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (!_bound)
            {
                _parts.Bind(this);
                _press.Bind(this, "ClayPressScale", "ClayPressTranslate");
                _bound = true;
                Wire();
            }
            Repaint();
        }

        private static void OnClayChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ClayButton b) b.Repaint();
        }

        /// <summary>
        /// The state machine. Precedence matters and is not arbitrary:
        /// a disabled button that is still hovered must read as disabled,
        /// so Disabled is checked before Hover. A pressed button that has
        /// just been clicked out of must not flicker back to hover first.
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

            // IsPressed and IsKeyboardFocused are DPs on ButtonBase with
            // AffectsRender-style change notifications, but they do not
            // raise a routed event, so the repaint is hooked to the
            // properties themselves.
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

        /// <summary>Recomputes and applies the whole recipe.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var state = ResolveState();
            var fill = ResolveFill(dark, state);

            // Buttons sit at Raised: something you can act on should not
            // be flush with the list it lives in.
            ClaySurfaceParts.Paint(
                _parts, dark,
                SoftDepthTier.Raised, state,
                ClaySurface.TopLeft(ClayCornerRadius),
                small: false,
                fill);

            UpdateLabel(fill, dark, state);
        }

        /// <summary>
        /// Resolves the button's fill, applying the press shift.
        /// </summary>
        private Color ResolveFill(bool dark, SoftInteractionState state)
        {
            var baseFill = ClayFill;
            if (baseFill.A == 0)
            {
                var accent = Application.Current?.TryFindResource(
                    "AccentBrush") as SolidColorBrush;
                baseFill = accent?.Color ?? Colors.Gray;
            }

            if (state == SoftInteractionState.Disabled)
            {
                return ClayPaletteShade(baseFill, 0.55);
            }
            if (state == SoftInteractionState.Hover)
            {
                return ClayPaletteLighten(baseFill, 0.06);
            }
            return baseFill;
        }

        /// <summary>
        /// Moves a fill toward black. Local rather than reaching into the
        /// recipe because the fill belongs to the ACCENT pipeline, not to
        /// the clay recipe — clay shapes a surface, it does not own its
        /// colour.
        /// </summary>
        private static Color ClayPaletteShade(Color c, double amount) =>
            Color.FromRgb(
                (byte)Math.Round(c.R * (1 - Math.Clamp(amount, 0, 1))),
                (byte)Math.Round(c.G * (1 - Math.Clamp(amount, 0, 1))),
                (byte)Math.Round(c.B * (1 - Math.Clamp(amount, 0, 1))));

        private static Color ClayPaletteLighten(Color c, double amount)
        {
            double k = Math.Clamp(amount, 0, 1);
            return Color.FromRgb(
                (byte)Math.Round(c.R + (255 - c.R) * k),
                (byte)Math.Round(c.G + (255 - c.G) * k),
                (byte)Math.Round(c.B + (255 - c.B) * k));
        }

        /// <summary>
        /// Publishes the resolved label colour onto the brush the
        /// template's label presenter is bound to.
        /// <para>
        /// The brush is looked up by its key in the template's resource
        /// dictionary and mutated in place. That works because the
        /// presenter bound to it through a StaticResource already holds
        /// the SAME instance — so recolouring it updates the rendered
        /// text without touching the template. The brush is therefore
        /// deliberately left unfrozen; see the note in ClayControls.xaml.
        /// </para>
        /// </summary>
        private void UpdateLabel(
            Color fill, bool dark, SoftInteractionState state)
        {
            var text = ClayLabel;
            if (text.A == 0)
            {
                var token = Application.Current?.TryFindResource(
                    "AccentForegroundBrush") as SolidColorBrush;
                text = token?.Color ?? Colors.White;
            }
            if (state == SoftInteractionState.Disabled)
            {
                text = ClayPaletteShade(text, 0.35);
            }
            if (Template?.FindName("ClayLabelBrush", this)
                is SolidColorBrush brush)
            {
                brush.Color = text;
            }
        }
    }
}