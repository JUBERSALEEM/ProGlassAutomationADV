// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Neu/NeuRecipe.cs
// PROJECT: ProGlassAutomation
// MODULE: Neumorphism — the dual-shadow recipe
//
// ── WHAT MAKES THIS NEUMORPHISM ──────────────────────────────────────
// An object extruded from the PAGE. Not placed on it — extruded from it.
// The single defining rule is that the object's fill IS the page's fill,
// so there is no colour difference at the boundary at all and the entire
// illusion is carried by a MIRRORED OUTSET pair:
//
//     dark shadow   offset AWAY  from the light (down-right)
//     light shadow  offset TOWARD the light (up-left)
//
// Everything else follows from that, including the things that make this
// style hard:
//
//   · The background must be a MID-TONE. Pure white has no headroom for
//     a lighter shadow and pure black has none for a darker one, so the
//     effect simply does not exist at either extreme. The canonical light
//     value is a pale blue-grey, not white — despite "neumorphism = grey".
//   · BOTH shadows derive from ONE depth value. Mismatched offsets
//     destroy the extrusion instantly: the eye reads the pair as two
//     unrelated smudges rather than as one surface lit from one place.
//   · There are NO inset shadows at rest. The insets are the PRESSED
//     state and nothing else — an extruded object that is also dented
//     is not one object.
//   · Pressed INVERTS the pair (outset → inset). That is the one motion
//     neumorphism gets right, because a physical dent reads as "pressed"
//     without any colour change at all.
//
// ── THE ACCESSIBILITY FIGHT, STATED PLAINLY ──────────────────────────
// This style is famous for failing audits, and it is not a fixable
// oversight. The best measured boundary contrast in the canonical
// palette is about 1.9:1; SC 1.4.11 wants 3:1. You cannot tune it up —
// push the shadows far enough apart to pass and they stop reading as
// shadows on a shared surface, at which point you are not building
// neumorphism any more.
//
// So the shadow is DECORATION here, deliberately, and every interactive
// element carries a second compliant signal: a real edge
// (SoftUiAccessibility.BuildEdge), a real focus ring outside the shadow,
// a body-ink label, and a fill change on press. The shadow is what makes
// it beautiful; the edge, the ring and the ink are what make it usable.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Services.Theme.Neu
{
    /// <summary>One half of a mirrored shadow pair.</summary>
    public sealed class NeuShadowLayer
    {
        internal NeuShadowLayer(
            double blurRadius, double shadowDepth,
            double opacity, double direction, Color color)
        {
            BlurRadius = blurRadius;
            ShadowDepth = shadowDepth;
            Opacity = opacity;
            Direction = direction;
            Color = color;
        }

        public double BlurRadius { get; }
        public double ShadowDepth { get; }
        public double Opacity { get; }

        /// <summary>Bearing in WPF DropShadowEffect degrees.</summary>
        public double Direction { get; }

        public Color Color { get; }

        /// <summary>Builds the frozen effect. Null when this layer is off.</summary>
        public DropShadowEffect? ToEffect() =>
            Opacity <= 0.001 || BlurRadius <= 0
                ? null
                : Frozen(new DropShadowEffect
                {
                    Direction = Direction,
                    BlurRadius = BlurRadius,
                    ShadowDepth = ShadowDepth,
                    Color = Color,
                    Opacity = Opacity,
                });

        private static DropShadowEffect Frozen(DropShadowEffect e)
        {
            e.Freeze();
            return e;
        }
    }

    /// <summary>
    /// The resolved neumorphic construction for one surface in one state.
    /// </summary>
    public sealed class NeuShadowStack
    {
        internal NeuShadowStack(
            SoftDepthTier tier,
            SoftInteractionState state,
            bool dark,
            NeuShadowLayer? light,
            NeuShadowLayer? shade,
            NeuShadowLayer? insetLight,
            NeuShadowLayer? insetShade,
            double insetReach,
            bool pressed,
            double fillLift,
            double fillShift)
        {
            Tier = tier;
            State = state;
            Dark = dark;
            Light = light;
            Shade = shade;
            InsetLight = insetLight;
            InsetShade = insetShade;
            InsetReach = insetReach;
            Pressed = pressed;
            FillLift = fillLift;
            FillShift = fillShift;
        }

        public SoftDepthTier Tier { get; }
        public SoftInteractionState State { get; }
        public bool Dark { get; }

        /// <summary>Outset highlight, toward the light. Up-left.</summary>
        public NeuShadowLayer? Light { get; }

        /// <summary>Outset shade, away from the light. Down-right.</summary>
        public NeuShadowLayer? Shade { get; }

        /// <summary>Inset highlight — only while pressed or concave.</summary>
        public NeuShadowLayer? InsetLight { get; }

        /// <summary>Inset shade — only while pressed or concave.</summary>
        public NeuShadowLayer? InsetShade { get; }

        /// <summary>How far the inset roll reaches, 0..1.</summary>
        public double InsetReach { get; }

        /// <summary>True when the pair has inverted into the surface.</summary>
        public bool Pressed { get; }

        /// <summary>
        /// Lightness this tier adds to its fill. DARK MODE ONLY, and the
        /// reason the four tiers stay distinguishable there: on a
        /// near-black page a shadow has almost no contrast to give, so
        /// every tier would collapse into the same flat shape.
        /// </summary>
        public double FillLift { get; }

        /// <summary>
        /// How far the fill moves on press, 0..1. The accessibility half
        /// of the press — see SoftUiAccessibility.PressedFillShift.
        /// </summary>
        public double FillShift { get; }

        /// <summary>
        /// True when the object is extruded rather than dented. An
        /// extruded object and a dented one are different OBJECTS, and
        /// rendering both at once is the classic mistake.
        /// </summary>
        public bool IsExtruded =>
            !Pressed && Tier != SoftDepthTier.Well;

        public bool HasOutset => IsExtruded;
    }

    /// <summary>
    /// The neumorphic material constants, derived from the family's ramp.
    /// </summary>
    public sealed class NeuProfile
    {
        public NeuProfile(
            Color baseFill,
            Color lightStop,
            Color darkStop,
            double depthRatio,
            double blurRatio,
            double insetDepthRatio,
            double insetBlurRatio,
            double maxBlur,
            double maxDepth,
            double referenceRadius,
            double darkDepthScale,
            double darkFillLift)
        {
            BaseFill = baseFill;
            LightStop = lightStop;
            DarkStop = darkStop;
            DepthRatio = depthRatio;
            BlurRatio = blurRatio;
            InsetDepthRatio = insetDepthRatio;
            InsetBlurRatio = insetBlurRatio;
            MaxBlur = maxBlur;
            MaxDepth = maxDepth;
            ReferenceRadius = referenceRadius;
            DarkDepthScale = darkDepthScale;
            DarkFillLift = darkFillLift;
        }

        /// <summary>
        /// The object's colour — and it is the PAGE's colour. That is the
        /// defining constraint, not an oversight: introduce a colour
        /// difference between surface and element and you have a regular
        /// card with strange shadows, which is claymorphism.
        /// </summary>
        public Color BaseFill { get; }

        /// <summary>
        /// The highlight. White in light mode; in dark mode a light GREY,
        /// never white — a pure white highlight on a near-black surface
        /// reads as a hard specular flare rather than as diffuse bounce.
        /// </summary>
        public Color LightStop { get; }

        /// <summary>
        /// The shade. The base darkened about 12%. In dark mode a dark
        /// GREY, never black, for the same reason.
        /// </summary>
        public Color DarkStop { get; }

        /// <summary>Shadow offset, as a fraction of the radius.</summary>
        public double DepthRatio { get; }

        /// <summary>
        /// Blur, as a multiple of the offset. Roughly 2× is the rule:
        /// harsher than that and it stops reading as a soft shadow and
        /// starts reading as an edge.
        /// </summary>
        public double BlurRatio { get; }

        public double InsetDepthRatio { get; }
        public double InsetBlurRatio { get; }

        public double MaxBlur { get; }
        public double MaxDepth { get; }
        public double ReferenceRadius { get; }

        /// <summary>
        /// Dark mode shrinks the offsets. On a dark surface a large
        /// shadow reads as a smudge long before it reads as volume, so
        /// the extrusion is tightened and the fill lift does the rest.
        /// </summary>
        public double DarkDepthScale { get; }

        public double DarkFillLift { get; }
    }

    /// <summary>The recipe. Stateless.</summary>
    public sealed class NeuRecipe
    {
        private readonly NeuProfile _neu;
        private readonly bool _dark;

        public NeuRecipe(NeuProfile neu, bool dark)
        {
            _neu = neu ?? throw new ArgumentNullException(nameof(neu));
            _dark = dark;
        }

        /// <summary>
        /// Per-tier multipliers. One number per tier; everything derived
        /// from it, so hover can promote a surface without any component
        /// knowing what a shadow is.
        /// </summary>
        private static readonly (double Depth, double Blur, double Fill)[]
            TierTable =
        {
            (0.00, 0.00, 0.000),   // Well     — concave, no outset at all
            (1.00, 1.00, 0.018),   // Resting
            (1.10, 1.10, 0.030),   // Raised
            (1.34, 1.30, 0.045),   // Floating
        };

        /// <summary>Builds the construction for one surface in one state.</summary>
        public NeuShadowStack Resolve(
            SoftDepthTier declared,
            SoftInteractionState state,
            double radius)
        {
            var tier = SoftDepthRules.Resolve(declared, state);
            var t = TierTable[(int)tier];

            bool concave = SoftDepthRules.IsConcave(tier);
            bool pressed = state == SoftInteractionState.Pressed
                           || state == SoftInteractionState.Focused
                           || concave;

            // ONE depth value for both halves. The research is blunt
            // about this: mismatched highlight and shade offsets destroy
            // the extrusion instantly, because the eye reads the pair as
            // two unrelated smudges instead of one lit surface. So there
            // is a single `offset` and both layers are built from it.
            double offset = _neu.DepthRatio * radius * t.Depth;
            if (_dark) offset *= _neu.DarkDepthScale;

            double blur = _neu.BlurRatio * offset;

            // Rule 4 from the painter: shadows are bounded. A
            // DropShadowEffect is re-rasterised on every repaint.
            double d = Math.Clamp(offset, 0, _neu.MaxDepth);
            double b = Math.Clamp(blur, 0, _neu.MaxBlur);

            NeuShadowLayer? light = null;
            NeuShadowLayer? shade = null;

            if (!pressed)
            {
                light = new NeuShadowLayer(
                    b, d, 0.85,
                    SoftLight.HighlightDirection, _neu.LightStop);
                shade = new NeuShadowLayer(
                    b, d, 0.75,
                    SoftLight.ShadowDirection, _neu.DarkStop);
            }

            // The inset pair. Tightly inset and soft: the offsets come
            // down to roughly two thirds, because a dent has less travel
            // than a lift before it stops looking like either.
            double iOff = Math.Clamp(
                _neu.InsetDepthRatio * radius, 0, _neu.MaxDepth);
            double iBlur = Math.Clamp(
                _neu.InsetBlurRatio * iOff, 0, _neu.MaxBlur);

            NeuShadowLayer? insetLight = pressed
                ? new NeuShadowLayer(
                    iBlur, iOff, 0.80,
                    SoftLight.ShadowDirection, _neu.LightStop)
                : null;
            NeuShadowLayer? insetShade = pressed
                ? new NeuShadowLayer(
                    iBlur, iOff, 0.70,
                    SoftLight.HighlightDirection, _neu.DarkStop)
                : null;

            double reach = pressed
                ? Math.Clamp(radius / _neu.ReferenceRadius * 0.42,
                             0.12, 0.55)
                : 0.0;

            double lift = _dark ? t.Fill * _neu.DarkFillLift : 0.0;

            // Disabled keeps a construction — a control that simply
            // stopped being drawn would be a control the user cannot
            // find — but loses the outset entirely and flattens.
            if (state == SoftInteractionState.Disabled)
            {
                light = null;
                shade = null;
                insetLight = null;
                insetShade = null;
                pressed = false;
            }

            double shift = 0.0;
            if (state == SoftInteractionState.Pressed)
                shift = SoftUiAccessibility.PressedFillShift;
            else if (state == SoftInteractionState.Disabled)
                shift = 0.45;

            return new NeuShadowStack(
                tier, state, _dark, light, shade,
                insetLight, insetShade, reach, pressed, lift, shift);
        }
    }

    /// <summary>
    /// Builds the frozen WPF effects. Stateless.
    /// </summary>
    public static class NeuVisualFactory
    {
        /// <summary>
        /// The extruded pair, as two frozen effects.
        /// <para>
        /// WPF allows ONE Effect per element, and the mirrored pair needs
        /// two shadows on the SAME shape — so they are rendered as two
        /// stacked Borders of identical fill: the lower casts the shade
        /// down-right, the upper casts the highlight up-left. Because
        /// both fills are the same opaque colour, the second completely
        /// covers the first and only their outside shadows differ. That
        /// is the WPF shape of the canonical CSS pair.
        /// </para>
        /// </summary>
        public static DropShadowEffect? OutsetLight(NeuShadowStack s) =>
            s.Light?.ToEffect();

        public static DropShadowEffect? OutsetShade(NeuShadowStack s) =>
            s.Shade?.ToEffect();

        /// <summary>The inset pair, for a dented surface.</summary>
        public static DropShadowEffect? InsetLight(NeuShadowStack s) =>
            s.InsetLight?.ToEffect();

        public static DropShadowEffect? InsetShade(NeuShadowStack s) =>
            s.InsetShade?.ToEffect();

        /// <summary>
        /// Derives the neumorphic constants from a ramp.
        /// <para>
        /// The light and shade stops are the whole recipe, and the
        /// canonical pair is the one everyone actually measures against:
        /// white highlight, base darkened about 12%, on a MID-TONE base.
        /// A pure white page cannot host this style — there is no
        /// headroom for a lighter shadow — so the base is pushed off
        /// white deliberately even when the ramp asks for it.
        /// </para>
        /// </summary>
        public static NeuProfile BuildProfile(
            Color baseFill, Color rampLight, Color rampDark, bool dark)
        {
            // Force a mid-tone. If the ramp is within a whisker of pure
            // white or pure black the extrude cannot exist, so it is
            // pushed to the nearest tone that can actually carry it.
            var baseTone = ForceMidTone(baseFill);

            var light = dark
                ? Blend(baseTone, Colors.White, 0.18)
                : Colors.White;
            var shade = dark
                ? Blend(baseTone, Colors.Black, 0.55)
                : Blend(baseTone, Colors.Black, 0.12);

            return new NeuProfile(
                baseFill: baseTone,
                lightStop: light,
                darkStop: shade,
                // 12-20px radius, offsets of 4-8px, blur roughly 2x.
                depthRatio: 0.28,
                blurRatio: 2.0,
                // The dent travels less than the lift.
                insetDepthRatio: 0.18,
                insetBlurRatio: 1.6,
                maxBlur: 56,
                maxDepth: 12,
                referenceRadius: 16.0,
                darkDepthScale: 0.72,
                darkFillLift: 1.0);
        }

        /// <summary>
        /// Pushes a colour into the band where a shadow pair can exist.
        /// <para>
        /// This is not a preference — it is the style's precondition. A
        /// neumorphic surface on pure white has nowhere to put the
        /// highlight and reads as a flat outline; on pure black it has
        /// nowhere to put the shade. Both look broken in a way that is
        /// instantly recognisable as "wrong neumorphism" rather than as a
        /// colour choice.
        /// </para>
        /// </summary>
        internal static Color ForceMidTone(Color c)
        {
            double l = Luminance(c);

            // Blend toward the OPPOSITE extreme, not toward white in both
            // branches. Blending white toward white with a negative
            // factor is a no-op — which is exactly what the first version
            // did, so a pure white base sailed straight through the check
            // that exists to stop it. The whole point of this function is
            // to move a colour that cannot host the effect, so it has to
            // actually move.
            if (l > 0.82) return Blend(c, Colors.Black, 0.10);
            if (l < 0.06) return Blend(c, Colors.White, 0.16);
            return c;
        }

        internal static Color Blend(Color a, Color b, double k) =>
            Color.FromArgb(
                a.A,
                (byte)Math.Clamp(Math.Round(a.R + (b.R - a.R) * k), 0, 255),
                (byte)Math.Clamp(Math.Round(a.G + (b.G - a.G) * k), 0, 255),
                (byte)Math.Clamp(Math.Round(a.B + (b.B - a.B) * k), 0, 255));

        /// <summary>Applies the tier's dark-mode lightness lift.</summary>
        public static Color Fill(Color baseFill, NeuShadowStack s) =>
            s.FillLift <= 0.001
                ? baseFill
                : Blend(baseFill, Colors.White, s.FillLift);

        /// <summary>Applies the press shift. The accessibility half of the press.</summary>
        public static Color Pressed(Color baseFill, NeuShadowStack s) =>
            s.FillShift <= 0.001
                ? baseFill
                : Blend(baseFill, Colors.Black, s.FillShift);

        private static double Luminance(Color c)
        {
            double L(double v)
            {
                v /= 255.0;
                return v <= 0.04045 ? v / 12.92
                                    : Math.Pow((v + 0.055) / 1.055, 2.4);
            }
            return L(c.R) * 0.2126 + L(c.G) * 0.7152 + L(c.B) * 0.0722;
        }
    }
}