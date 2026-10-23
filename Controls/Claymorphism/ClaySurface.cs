// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClaySurface.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the surface control
//
// A clay object: its OWN colour, three shadows, large radius. Cards,
// panels, hero blocks, stat tiles.
//
// ── WHY A CONTROL AND NOT A STYLE ───────────────────────────────────
// WPF Borders cannot host a ControlTemplate, and this style needs to
// change SHAPE with state — the press state crosses the two inner layers
// over, which is a change of brush stops rather than of colour. No
// Trigger can express that, because there is no trigger source for "the
// insets have swapped". So the geometry stays declarative in XAML and
// only the recipe's brush objects are written from code.
//
// ── THE STATE IS NOT DECORATIVE ─────────────────────────────────────
// Reduced motion does not disable the clay construction — the shadows
// ARE the surface's shape, not an ornament on it. It removes the
// TRANSITION between states. Taking the shadows off a pressed button
// would not calm anyone; it would just stop looking pressed.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System;

namespace ProGlassAutomation.Controls.Claymorphism
{
    /// <summary>
    /// A claymorphic surface: its own colour, three shadows, large radius.
    /// </summary>
    public class ClaySurface : ContentControl
    {
        #region ── Dependency properties ──

        /// <summary>Which depth tier this object occupies.</summary>
        public static readonly DependencyProperty ClayDepthProperty =
            DependencyProperty.Register(
                nameof(ClayDepth), typeof(SoftDepthTier),
                typeof(ClaySurface),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Resting, OnClayChanged));

        public SoftDepthTier ClayDepth
        {
            get => (SoftDepthTier)GetValue(ClayDepthProperty);
            set => SetValue(ClayDepthProperty, value);
        }

        /// <summary>
        /// Corner radius. Drives the inset softness as well as the corner
        /// itself, which is why it is ONE number: a large radius with a
        /// tight inset produces a hard rim instead of a soft roll, and
        /// that is the single most common way this style gets faked badly.
        /// Typed as CornerRadius rather than double so it flows straight
        /// through TemplateBinding without a converter.
        /// </summary>
        public static readonly DependencyProperty ClayCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ClayCornerRadius), typeof(CornerRadius),
                typeof(ClaySurface),
                new FrameworkPropertyMetadata(
                    new CornerRadius(32), OnClayChanged));

        public CornerRadius ClayCornerRadius
        {
            get => (CornerRadius)GetValue(ClayCornerRadiusProperty);
            set => SetValue(ClayCornerRadiusProperty, value);
        }

        /// <summary>
        /// True for chips and badges. Under roughly 24px tall there is no
        /// room left to resolve an inset once the corner radius has taken
        /// its cut, so the inset is dropped and the drop alone carries the
        /// form — a clay badge that keeps a full inset reads as a smudge.
        /// </summary>
        public static readonly DependencyProperty ClaySmallProperty =
            DependencyProperty.Register(
                nameof(ClaySmall), typeof(bool),
                typeof(ClaySurface),
                new FrameworkPropertyMetadata(false, OnClayChanged));

        public bool ClaySmall
        {
            get => (bool)GetValue(ClaySmallProperty);
            set => SetValue(ClaySmallProperty, value);
        }

        /// <summary>
        /// True for objects the user can act on.
        /// <para>
        /// A non-interactive card gets its outer drop only. There is no
        /// point insetting a container that will never be pressed, and
        /// nested insets turn to mush within three levels — which is why
        /// the rule is "no clay on clay deeper than two", not "clay
        /// everywhere".
        /// </para>
        /// </summary>
        public static readonly DependencyProperty ClayInteractiveProperty =
            DependencyProperty.Register(
                nameof(ClayInteractive), typeof(bool),
                typeof(ClaySurface),
                new FrameworkPropertyMetadata(false, OnClayChanged));

        public bool ClayInteractive
        {
            get => (bool)GetValue(ClayInteractiveProperty);
            set => SetValue(ClayInteractiveProperty, value);
        }

        /// <summary>
        /// The object's own colour. A transparent value means "use the
        /// ramp's surface token", which is what makes a clay card a
        /// DIFFERENT colour from the page — the clearest single line
        /// between clay and neumorphism.
        /// </summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClaySurface),
                new FrameworkPropertyMetadata(
                    Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        /// <summary>
        /// Current interaction state. ClayButton drives this rather than
        /// duplicating the visual code, and tests read it to assert the
        /// state machine without driving a mouse.
        /// </summary>
        public static readonly DependencyProperty ClayStateProperty =
            DependencyProperty.Register(
                nameof(ClayState), typeof(SoftInteractionState),
                typeof(ClaySurface),
                new PropertyMetadata(
                    SoftInteractionState.Rest,
                    OnClayStateChanged));

        public SoftInteractionState ClayState
        {
            get => (SoftInteractionState)GetValue(ClayStateProperty);
            set => SetValue(ClayStateProperty, value);
        }

        #endregion

        private readonly ClaySurfaceParts _parts = new ClaySurfaceParts();
        private bool _bound;
        private bool _wired;

        public ClaySurface()
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
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
            if (d is ClaySurface s) s.Repaint();
        }

        private static void OnClayStateChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ClaySurface s) s.Repaint();
        }

        /// <summary>
        /// Subscribes to the two things that can invalidate a clay surface:
        /// a theme swap and a light/dark flip. Weakly, so a control torn
        /// down without unsubscribing cannot keep itself alive.
        /// </summary>
        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged(
                (_, _) => Repaint());
        }

        /// <summary>Recomputes and applies the whole recipe. Never throws.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            ClaySurfaceParts.Paint(
                _parts, dark,
                ClayDepth, ResolveState(),
                TopLeft(ClayCornerRadius), ClaySmall,
                ResolveFill(dark));
        }

        /// <summary>
        /// Derives the interaction state.
        /// <para>
        /// Non-interactive surfaces never hover or press. A card that
        /// lights up on mouse-over but does nothing on click is a lie
        /// about being a control — and, because the highlight IS the
        /// affordance in this style, a lie the eye believes.
        /// </para>
        /// </summary>
        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (!ClayInteractive) return SoftInteractionState.Rest;
            return ClayState;
        }

        private Color ResolveFill(bool dark)
        {
            if (ClayFill.A > 0) return ClayFill;
            var brush = Application.Current?.TryFindResource(
                "Surface.Primary.BackgroundBrush") as SolidColorBrush;
            return brush?.Color ?? Colors.Transparent;
        }

        /// <summary>
        /// The recipe works off a single scalar radius. A uniform corner
        /// radius is the norm for clay — the style exaggerates roundness
        /// rather than varying it — so this takes the top-left corner and
        /// the template applies it to all four.
        /// </summary>
        internal static double TopLeft(CornerRadius r) =>
            Math.Max(0,
                Math.Max(r.TopLeft, Math.Max(r.TopRight,
                    Math.Max(r.BottomRight, r.BottomLeft))));

        /// <summary>
        /// Moves a fill toward black. Shared so the whole family dims a
        /// disabled control identically — ClayButton used to keep its own
        /// private copy, which is exactly how two controls end up
        /// disagreeing about what "disabled" looks like.
        /// </summary>
        /// <remarks>
        /// Deliberately NOT part of the clay recipe: a fill belongs to the
        /// accent pipeline, whereas the recipe shapes a surface. Clay
        /// geometry and surface colour are separate concerns.
        /// </remarks>
        internal static Color Shade(Color c, double amount)
        {
            double k = Math.Clamp(amount, 0, 1);
            return Color.FromRgb(
                (byte)Math.Round(c.R * (1 - k)),
                (byte)Math.Round(c.G * (1 - k)),
                (byte)Math.Round(c.B * (1 - k)));
        }

        /// <summary>Moves a fill toward white.</summary>
        internal static Color Lighten(Color c, double amount)
        {
            double k = Math.Clamp(amount, 0, 1);
            return Color.FromRgb(
                (byte)Math.Round(c.R + (255 - c.R) * k),
                (byte)Math.Round(c.G + (255 - c.G) * k),
                (byte)Math.Round(c.B + (255 - c.B) * k));
        }
    }
}