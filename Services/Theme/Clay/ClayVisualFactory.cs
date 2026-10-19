// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Clay/ClayVisualFactory.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — recipe → frozen WPF visuals
//
// Turns a ClayShadowStack into the three objects a control template binds
// to:
//
//   DropEffect   layer 1, a real DropShadowEffect (WPF does direction,
//                hue tinting and blur natively, and cheaply)
//   LightBrush   layer 3, a LinearGradientBrush along the light diagonal
//   ShadeBrush   layer 2, the same diagonal with inverted stops
//
// Everything is FROZEN. A clay surface is repainted on theme change and
// on mode change, and freezing means a frozen Freezable can be shared by
// every card on the page through a single application resource instead of
// being re-rasterised per instance. Unfrozen brushes on a scrolling grid
// are one of the classic ways a soft-UI app loses its frame budget.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ProGlassAutomation.Services.Theme.Clay
{
    /// <summary>The three frozen visuals one clay surface binds to.</summary>
    public sealed class ClayVisuals
    {
        internal ClayVisuals(
            DropShadowEffect? drop,
            SolidColorBrush? fill,
            LinearGradientBrush? light,
            LinearGradientBrush? shade)
        {
            Drop = drop;
            Fill = fill;
            Light = light;
            Shade = shade;
        }

        /// <summary>
        /// The outer drop, or null for a Well — concavity has no mass.
        /// </summary>
        public DropShadowEffect? Drop { get; }

        /// <summary>
        /// The object's own fill, with the tier's lightness lift already
        /// applied. Null means "paint the ramp's surface" and lets the
        /// template bind straight to the semantic token.
        /// </summary>
        public SolidColorBrush? Fill { get; }

        /// <summary>The inner highlight roll. Null on small surfaces.</summary>
        public LinearGradientBrush? Light { get; }

        /// <summary>The inner shade roll. Null on small surfaces.</summary>
        public LinearGradientBrush? Shade { get; }
    }

    /// <summary>Builds frozen clay visuals. Stateless.</summary>
    public static class ClayVisualFactory
    {
        /// <summary>
        /// The two inner layers are ASYMMETRIC on purpose.
        /// <para>
        /// The highlight is lighter but MORE TRANSPARENT than the shade
        /// is dark. A matched pair reads as plastic — it is the
        /// signature of a cheap 3D render, because real diffuse clay has
        /// a much weaker response to a broad light than it does to its
        /// own occlusion. The asymmetry is the whole difference between
        /// "moulded" and "glossy".
        /// </para>
        /// </summary>
        private const double ShadeAlpha = 0.85;
        private const double LightAlpha = 0.70;

        /// <summary>
        /// Builds the three visuals for a resolved stack.
        /// </summary>
        /// <param name="stack">The resolved recipe.</param>
        /// <param name="baseFill">
        /// The fill the tier starts from — normally the ramp's surface.
        /// The dark-mode lightness lift is applied on top of this.
        /// </param>
        public static ClayVisuals Build(
            ClayShadowStack stack, Color baseFill)
        {
            if (stack is null) throw new ArgumentNullException(nameof(stack));

            // ── layer 1: the outer drop ───────────────────────────────
            DropShadowEffect? drop = null;
            if (stack.HasDrop)
            {
                drop = new DropShadowEffect
                {
                    // Falls AWAY from the light. 315° points up-left, so
                    // the shadow lands down-right.
                    Direction = ClayLight.ShadowDirection,
                    BlurRadius = stack.DropBlur,
                    ShadowDepth = stack.DropDepth,
                    Color = stack.DropColor,
                    Opacity = stack.DropOpacity,
                    // RenderCapability is NOT checked here: a software
                    // rasteriser still draws this correctly, just
                    // slower, and refusing to draw a shadow would be a
                    // far worse outcome than dropping a frame.
                };
                drop.Freeze();
            }

            // ── the fill, with the dark-mode lift ───────────────────
            SolidColorBrush? fill = null;
            if (stack.FillLift > 0.001)
            {
                fill = Frozen(new SolidColorBrush(
                    ClayRecipe.Lift(baseFill, stack.FillLift)));
            }

            // ── layers 2 and 3: the inner rolls ─────────────────────
            LinearGradientBrush? light = null;
            LinearGradientBrush? shade = null;

            if (stack.LightStops > 0.0)
            {
                bool inverted = stack.Inverted;

                // Normal: highlight is born top-left, shade is born
                // bottom-right. Inverted (pressed): they cross, because
                // the object has been pushed down into the page and now
                // catches light on its underside.
                var lightBrush = new LinearGradientBrush();
                lightBrush.GradientStops.Clear();
                lightBrush.GradientStops.Add(new GradientStop(
                    WithAlpha(stack.LightColor,
                        stack.State == ClayInteractionState.Disabled
                            ? 0.0 : LightAlpha),
                    0.0));
                lightBrush.GradientStops.Add(new GradientStop(
                    WithAlpha(stack.LightColor, 0.0),
                    stack.LightStops));

                var shadeBrush = new LinearGradientBrush();
                shadeBrush.GradientStops.Clear();
                shadeBrush.GradientStops.Add(new GradientStop(
                    WithAlpha(stack.ShadeColor, 0.0),
                    stack.ShadeStops));
                shadeBrush.GradientStops.Add(new GradientStop(
                    WithAlpha(stack.ShadeColor, ShadeAlpha),
                    1.0));

                SetDiagonal(lightBrush, inverted);
                SetDiagonal(shadeBrush, !inverted);

                lightBrush.Freeze();
                shadeBrush.Freeze();
                light = lightBrush;
                shade = shadeBrush;
            }

            return new ClayVisuals(drop, fill, light, shade);
        }

        /// <summary>
        /// Runs a gradient along the light diagonal — (0,0) top-left to
        /// (1,1) bottom-right, or the reverse when inverted. Relative
        /// coordinates so the gradient follows the element's bounding box
        /// whatever size it is laid out at.
        /// </summary>
        private static void SetDiagonal(LinearGradientBrush brush, bool inverted)
        {
            if (inverted)
            {
                brush.StartPoint = new Point(1, 1);
                brush.EndPoint = new Point(0, 0);
            }
            else
            {
                brush.StartPoint = new Point(0, 0);
                brush.EndPoint = new Point(1, 1);
            }
        }

        /// <summary>
        /// Scales a colour's OWN alpha by a factor, rather than
        /// replacing it.
        /// <para>
        /// Replacing it is the obvious thing to write and it silently
        /// undoes every dimming decision made upstream. The dark-mode
        /// highlight arrives here already weakened — its RGB pulled
        /// toward the fill and its alpha cut to 65% — and a stop that
        /// overwrites alpha would render it at full strength anyway,
        /// which is exactly the hard-plastic-glare-on-a-dark-surface
        /// failure the dimming exists to prevent. Multiplying keeps the
        /// two decisions composable.
        /// </para>
        /// </summary>
        private static Color WithAlpha(Color c, double alpha)
        {
            double a = Math.Clamp(alpha, 0, 1) * (c.A / 255.0);
            return Color.FromArgb(
                (byte)Math.Round(a * 255), c.R, c.G, c.B);
        }

        /// <summary>Freezes a brush for sharing across instances.</summary>
        internal static SolidColorBrush Frozen(SolidColorBrush b)
        {
            b.Freeze();
            return b;
        }

        /// <summary>
        /// The accessible edge.
        /// <para>
        /// Clay's real weakness, and the one that makes it an
        /// accessibility problem rather than just a taste problem: a
        /// shadow is not a boundary. WCAG 2.2 SC 1.4.11 requires 3:1
        /// between a control's boundary and what is behind it, and a
        /// blurred, hue-tinted drop shadow measures well under that — it
        /// is soft by definition, which is the entire point of it.
        /// </para>
        /// <para>
        /// So clay surfaces keep a real edge. It is nearly invisible in
        /// light mode, where the fill already differs strongly from the
        /// page; in dark mode it is doing real work, which is why the
        /// dark recipe lifts fill lightness instead of leaning on the
        /// shadow harder.
        /// </para>
        /// </summary>
        public static Pen BuildEdge(ClayProfile clay, Color fill, bool dark)
        {
            double strength = dark ? 0.55 : 0.32;
            var c = ClayRecipe.Mix(
                fill, dark ? Colors.Black : Colors.White,
                dark ? 0.45 : 0.30);
            var pen = new Pen(
                Solid(c, strength), Math.Max(1.0, clay.FocusRingWidth - 1.0));
            pen.Freeze();
            return pen;
        }

        private static SolidColorBrush Solid(Color c, double opacity) =>
            Frozen(new SolidColorBrush(WithAlpha(c, opacity)));

        // ── the palette factory ───────────────────────────────────────

        /// <summary>
        /// Derives the clay material constants from the family's dark or
        /// light ramp.
        /// <para>
        /// The SHADE is the surface's own hue pushed toward black — not a
        /// grey token, not a black token. This is the rule that makes
        /// clay warm: the shadow belongs to the object. A pink card with
        /// a pink shadow reads as a pink object sitting on a page; a pink
        /// card with a grey shadow reads as a pink rectangle hovering.
        /// </para>
        /// </summary>
        public static ClayProfile BuildProfile(
            Color surface,
            Color shadeHue,
            Color lightHue,
            bool dark)
        {
            var shade = ClayRecipe.Mix(shadeHue, Colors.Black, 0.38);

            // On light ground the drop carries the depth, so it is the
            // strongest thing in the recipe. On dark ground it cannot
            // (see ClayRecipe.Resolve) and comes down hard.
            double dropOpacity = dark ? 0.30 : 0.34;

            return new ClayProfile(
                baseFill: surface,
                shade: shade,
                light: lightHue,
                dropBlurRatio: 0.75,
                dropDepthRatio: 0.28,
                dropOpacity: dropOpacity,
                // The roll reaches a little over half the radius. Short
                // of half and the highlight hugs the rim like a bevel;
                // much past it and the surface stops reading as inflated.
                insetReachRatio: 0.55,
                // The radius the reach ratio is calibrated against. The
                // clay card default; a 32px button sits exactly on it
                // and everything else scales from there.
                referenceRadius: 32.0,
                // Engine rule 4. Clay is the family that pushes closest
                // to the ceiling, because a large radius genuinely wants
                // a large blur to resolve its roll.
                maxBlur: 56,
                maxDepth: 12,
                // Dark: the tinted drop has to become much more
                // transparent or it smears. The page's own darkness does
                // the separating instead.
                darkDropOpacityScale: 0.58,
                darkDropTint: 0.45,
                // A bright highlight is glare on a dark surface. Dim it,
                // and let the fill lift carry the volume.
                darkHighlightDim: 0.35,
                darkFillLift: 1.0,
                // Press collapses the drop toward contact.
                pressDropOpacityScale: 0.62,
                focusRingWidth: 3,
                focusRingOffset: 3);
        }
    }
}