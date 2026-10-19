// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Neumorphism/NeuSurface.cs
// PROJECT: ProGlassAutomation
// MODULE: Neumorphism — the extruded surface
//
// ── WHY THIS STYLE NEEDS MORE HELP THAN CLAY ─────────────────────────
// Clay has an advantage neumorphism structurally cannot have: its object
// is a DIFFERENT colour from the page. That difference is the boundary,
// and it is why clay largely escapes the contrast problem.
//
// A neumorphic surface IS the page colour. So there is nothing at the
// edge — every cue has to come from the shadow pair, and the shadow pair
// measures around 1.2–1.9:1 against the 3:1 that WCAG 2.2 SC 1.4.11
// wants for a control boundary. That number cannot be tuned up without
// destroying the effect, so this control pairs the extrusion with a real
// edge and a real focus ring and treats the shadows as decoration.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme.Neu;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;

namespace ProGlassAutomation.Controls.Neumorphism
{
    /// <summary>A neumorphic surface: the page colour, extruded.</summary>
    public class NeuSurface : ContentControl
    {
        #region ── Dependency properties ──

        /// <summary>Which depth tier this object occupies.</summary>
        public static readonly DependencyProperty NeuDepthProperty =
            DependencyProperty.Register(
                nameof(NeuDepth), typeof(SoftDepthTier),
                typeof(NeuSurface),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Resting, OnNeuChanged));

        public SoftDepthTier NeuDepth
        {
            get => (SoftDepthTier)GetValue(NeuDepthProperty);
            set => SetValue(NeuDepthProperty, value);
        }

        /// <summary>
        /// Corner radius. Neumorphism uses 12–20px — rounded enough to
        /// sell the moulded look, never balloon-round, which would turn
        /// the object into a blob and lose the edge entirely.
        /// </summary>
        public static readonly DependencyProperty NeuCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(NeuCornerRadius), typeof(CornerRadius),
                typeof(NeuSurface),
                new FrameworkPropertyMetadata(
                    new CornerRadius(16), OnNeuChanged));

        public CornerRadius NeuCornerRadius
        {
            get => (CornerRadius)GetValue(NeuCornerRadiusProperty);
            set => SetValue(NeuCornerRadiusProperty, value);
        }

        /// <summary>
        /// Set True for objects the user can act on. A non-interactive
        /// card still gets its extrusion — that is what distinguishes it
        /// from the page — but it never presses.
        /// </summary>
        public static readonly DependencyProperty NeuInteractiveProperty =
            DependencyProperty.Register(
                nameof(NeuInteractive), typeof(bool),
                typeof(NeuSurface),
                new FrameworkPropertyMetadata(false, OnNeuChanged));

        public bool NeuInteractive
        {
            get => (bool)GetValue(NeuInteractiveProperty);
            set => SetValue(NeuInteractiveProperty, value);
        }

        /// <summary>Current interaction state, readable by tests and templates.</summary>
        public static readonly DependencyProperty NeuStateProperty =
            DependencyProperty.Register(
                nameof(NeuState), typeof(SoftInteractionState),
                typeof(NeuSurface),
                new PropertyMetadata(
                    SoftInteractionState.Rest, OnNeuStateChanged));

        public SoftInteractionState NeuState
        {
            get => (SoftInteractionState)GetValue(NeuStateProperty);
            set => SetValue(NeuStateProperty, value);
        }

        #endregion

        private readonly NeuSurfaceParts _parts = new NeuSurfaceParts();
        private bool _bound;
        private bool _wired;

        public NeuSurface()
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

        private static void OnNeuChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NeuSurface s) s.Repaint();
        }

        private static void OnNeuStateChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NeuSurface s) s.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
        }

        /// <summary>Recomputes and applies the extrusion.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            NeuSurfaceParts.Paint(
                _parts, dark, NeuDepth, ResolveState(),
                TopLeft(NeuCornerRadius), ResolveFill(dark));
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (!NeuInteractive) return SoftInteractionState.Rest;
            return NeuState;
        }

        private Color ResolveFill(bool dark)
        {
            // THE defining rule, and the reason neumorphism looks like
            // itself: the surface takes the PAGE's colour, not a surface
            // token. Point this at Surface.Primary.BackgroundBrush and
            // you get a card with strange shadows, which is a different
            // style entirely.
            var brush = Application.Current?.TryFindResource(
                "ModuleBackgroundBrush") as SolidColorBrush;
            return brush?.Color ?? Colors.Gray;
        }

        /// <summary>The recipe works off one scalar radius.</summary>
        internal static double TopLeft(CornerRadius r) =>
            System.Math.Max(0,
                System.Math.Max(r.TopLeft, System.Math.Max(r.TopRight,
                    System.Math.Max(r.BottomRight, r.BottomLeft))));
    }
}