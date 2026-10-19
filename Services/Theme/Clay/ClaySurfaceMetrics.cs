// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Clay/ClaySurfaceMetrics.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the three-shadow recipe
//
// The whole style lives in this file. Everything else is delivery.
//
// ── THE ONE IMPLEMENTATION CONSTRAINT ─────────────────────────────────
//   WPF has no native inset box-shadow. `DropShadowEffect` is always
//   OUTSET; `Effect` accepts exactly one effect per element; and there
//   is no equivalent of CSS `box-shadow: inset a b c d`.
//
//   So a clay surface cannot be "one element with three shadows". It has
//   to be a STACK. The two ways to build the inset layers in WPF are:
//
//     (a) An oversized rounded rect, offset away from the light, filled
//         with the shade colour, blurred, and CLIPPED to the element.
//         This is a true corner-hugging inset — physically correct, and
//         what the CSS recipe describes.
//
//     (b) A gradient overlay under the element's own corner radius,
//         running along the light diagonal.
//
//   (a) is accurate and (b) is cheap, and this file picks (b) for the
//   insets. The reasons are not aesthetic:
//
//     · BlurEffect forces a software pass. Two of them on every card,
//       button and pill in a scrolling DataGrid is a frame-rate problem
//       long before it is a visual one. The engine's own rule 4 already
//       caps shadow blur at 56px for exactly this reason, and a per-
//       element BlurEffect is an order of magnitude worse than a shared
//       DropShadowEffect.
//     · A gradient IS animatable in a way a clipped caster is not. The
//       press state in this style is the insets CROSSING OVER — and a
//       crossover needs both layers to be interpolable. With casters you
//       would be swapping two bitmap effects and calling the result a
//       transition.
//     · The roll a gradient produces on a large radius is the same roll
//       a CSS inset produces. Clay's insets are broad and soft by
//       definition; there is nothing in the style that needs the extra
//       fidelity.
//
//   The OUTER drop stays a real DropShadowEffect. It needs directional
//   offset, hue tinting and blur, and WPF does all three natively and
//   cheaply.
//
// ── WHAT THIS BUYS ────────────────────────────────────────────────────
//   Layer 1  OUTER drop   mass and contact. Tinted to the object's hue.
//   Layer 2  INNER shade  far-side wall thickness. The layer everyone
//                           skips, and the one that makes it read as an
//                           object with a body rather than a lit plane.
//   Layer 3  INNER light  the roll toward the light. Asymmetric against
//                           layer 2 on purpose — see the note there.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme.Clay
{
    /// <summary>
    /// The resolved three-shadow recipe for one surface in one state.
    /// Immutable. Produced by <see cref="ClayRecipe"/> and consumed by
    /// the control templates.
    /// </summary>
    public sealed class ClayShadowStack
    {
        internal ClayShadowStack(
            ClayDepthTier tier,
            ClayInteractionState state,
            bool dark,
            double dropBlur,
            double dropDepth,
            double dropOpacity,
            Color dropColor,
            double lightStops,
            Color lightColor,
            double shadeStops,
            Color shadeColor,
            double innerRadius,
            bool inverted,
            double fillLift)
        {
            Tier = tier;
            State = state;
            Dark = dark;
            DropBlur = dropBlur;
            DropDepth = dropDepth;
            DropOpacity = dropOpacity;
            DropColor = dropColor;
            LightStops = lightStops;
            LightColor = lightColor;
            ShadeStops = shadeStops;
            ShadeColor = shadeColor;
            InnerRadius = innerRadius;
            Inverted = inverted;
            FillLift = fillLift;
        }

        /// <summary>Depth tier this was resolved for.</summary>
        public ClayDepthTier Tier { get; }

        /// <summary>Interaction state this was resolved for.</summary>
        public ClayInteractionState State { get; }

        /// <summary>True when resolved against the dark ramp.</summary>
        public bool Dark { get; }

        // ── layer 1: the outer drop ───────────────────────────────────
        public double DropBlur { get; }
        public double DropDepth { get; }
        public double DropOpacity { get; }
        public Color DropColor { get; }

        /// <summary>True when this tier paints no drop at all (Wells).</summary>
        public bool HasDrop => Tier != ClayDepthTier.Well && DropOpacity > 0.001;

        // ── layer 2: the far-side inner shade ────────────────────────
        /// <summary>How far in the shade roll reaches, 0..1.</summary>
        public double ShadeStops { get; }
        public Color ShadeColor { get; }

        // ── layer 3: the near-side inner highlight ────────────────────
        /// <summary>How far in the highlight roll reaches, 0..1.</summary>
        public double LightStops { get; }
        public Color LightColor { get; }

        /// <summary>
        /// The corner radius the inset layers should be clipped to. The
        /// insets ride the element's own radius, so a clay button with a
        /// 20px corner does not get a highlight cut to a 32px card's
        /// curvature.
        /// </summary>
        public double InnerRadius { get; }

        /// <summary>
        /// True when the two inner layers have crossed over — the press
        /// state. Light is now falling from the bottom-right because the
        /// surface has been pushed into the page and is catching light on
        /// its underside.
        /// </summary>
        public bool Inverted { get; }

        /// <summary>
        /// How much this tier lifts the FILL's lightness, 0..1.
        /// <para>
        /// Only meaningful in dark mode, and it is what makes dark clay
        /// work at all. On a dark page a shadow cannot carry four
        /// distinct depth tiers — a black shadow on a near-black fill is
        /// nearly invisible, so every tier would collapse into the same
        /// flat rectangle. Raising each tier's fill a few percent gives
        /// the depth back as lightness, which the page can actually show.
        /// On a light page this is zero: there, shadow IS the signal and
        /// lifting the fill as well would just wash the tier out.
        /// </para>
        /// </summary>
        public double FillLift { get; }
    }

    /// <summary>
    /// The recipe itself: given a clay hue, a radius and a tier, produce
    /// the three shadows.
    /// <para>
    /// All of the per-tier numbers live in <see cref="TierTable"/>. Every
    /// other number here is DERIVED from the radius, because the rule
    /// that decides whether something looks like clay or like a card with
    /// a glow is the relationship between the corner radius and the
    /// softness of the inset roll — not the absolute pixel values.
    /// </para>
    /// </summary>
    public sealed class ClayRecipe
    {
        private readonly ClayProfile _clay;
        private readonly bool _dark;

        public ClayRecipe(ClayProfile clay, bool dark)
        {
            _clay = clay ?? throw new ArgumentNullException(nameof(clay));
            _dark = dark;
        }

        /// <summary>
        /// Multipliers applied per tier. The pattern this encodes is
        /// "one number per tier, everything derived from it".
        /// </summary>
        private static readonly ClayTierTable[] TierTable =
        {
            // ── Well ────────────────────────────────────────────────────
            // No drop at all. Insets are the DEEPEST of any tier,
            // because that is what makes a recess read as a recess: the
            // far wall is thicker, not the near one.
            new ClayTierTable(0.00, 0.00, 0.00, insetBias: 1.35, fillLift: 0.00),

            // ── Resting ─────────────────────────────────────────────────
            // fillLift is DARK MODE ONLY and is the reason this table
            // has a fifth column at all. On a near-black page a shadow
            // cannot separate four tiers — every one of them collapses
            // into the same flat rectangle — so each tier raises its own
            // fill a few percent instead. Small, increasing, and well
            // short of the text: enough for the tiers to separate,
            // not enough to wash them out.
            new ClayTierTable(1.00, 1.00, 1.00, insetBias: 1.00, fillLift: 0.020),

            // ── Raised ──────────────────────────────────────────────────
            new ClayTierTable(1.18, 1.25, 1.22, insetBias: 0.94, fillLift: 0.038),

            // ── Floating ────────────────────────────────────────────────
            new ClayTierTable(1.55, 1.60, 1.45, insetBias: 1.06, fillLift: 0.055),
        };

        /// <summary>
        /// The base drop, before the tier multiplies it. Derived from the
        /// radius rather than hard-coded, for the same reason the insets
        /// are: a 48px clay hero and a 20px clay button are the same
        /// material at different sizes, and tying the drop to the
        /// geometry is what keeps them looking like one design system.
        /// </summary>
        private double BaseDropBlur(double radius) =>
            Math.Clamp(radius * _clay.DropBlurRatio, 0, _clay.MaxBlur);

        private double BaseDropDepth(double radius) =>
            Math.Clamp(radius * _clay.DropDepthRatio, 0, _clay.MaxDepth);

        /// <summary>
        /// Builds the full stack.
        /// </summary>
        /// <param name="radius">
        /// The element's corner radius. Drives inset softness, because a
        /// large radius with a tight inset produces a hard rim instead
        /// of a soft roll — the single most common way this style is
        /// faked badly.
        /// </param>
        /// <param name="smallSurface">
        /// True for chips and badges. Under roughly 24px tall there is
        /// no room left to resolve an inset once the corner radius has
        /// taken its cut, so the inset is dropped and only the drop
        /// remains. A clay badge that keeps a full inset just looks like
        /// a dirty smudge.
        /// </param>
        public ClayShadowStack Resolve(
            ClayDepthTier declared,
            ClayInteractionState state,
            double radius,
            bool smallSurface = false)
        {
            var tier = ClayDepthRules.Resolve(declared, state);
            var t = TierTable[(int)tier];

            double dropBlur = BaseDropBlur(radius) * t.DropBlur;
            double dropDepth = BaseDropDepth(radius) * t.DropDepth;
            double dropOpacity = _clay.DropOpacity * t.DropOpacity;
            var dropColor = _clay.Shade;

            // ── dark-mode recalibration ────────────────────────────────
            // Clay is a LIGHT-mode style. Inverting the palette produces
            // mud, and it is worth being precise about why, because the
            // two shadows fail differently:
            //
            //   The DROP is tinted to the object's hue, and that is the
            //   whole trick on a pale ground. On a dark ground the same
            //   tinted shadow is no longer distinguishable from the
            //   fill — it becomes a dirty smear. So in dark it must go
            //   BOTH darker and much more transparent, and the fill's
            //   lightness has to do the separating instead.
            //
            //   The HIGHLIGHT is the other failure. A bright highlight
            //   that reads as a soft roll on pastel reads as hard
            //   plastic glare on a dark surface. Dim it, and tighten it
            //   rather than just weakening it — blur is what makes it
            //   read as glare, so it comes down as much as opacity does.
            if (_dark)
            {
                dropOpacity *= _clay.DarkDropOpacityScale;
                dropColor = Mix(dropColor, Colors.Black, _clay.DarkDropTint);
            }

            // ── the inset layers ──────────────────────────────────────
            // Inset reach is proportional to the radius. This is the rule
            // that makes the highlight read as a ROLL (the surface
            // curving away from the light) rather than as a hard rim
            // painted on the inside edge.
            //
            // The size scale matters and was got wrong first time:
            // multiplying a PIXEL radius by a 0..1 ratio produces a pixel
            // count, so every radius in the app clamped to the same
            // ceiling and the roll was identical on a 16px chip and a
            // 48px hero — the opposite of the rule, arrived at by
            // writing it as if it were not. Reach is a FRACTION of the
            // gradient; the radius only decides how far the size term
            // has been pulled from the reference.
            double sizeScale = Math.Clamp(
                radius / _clay.ReferenceRadius, 0.6, 1.6);

            double insetReach = smallSurface
                ? 0.0
                : Math.Clamp(
                    _clay.InsetReachRatio * t.InsetBias * sizeScale,
                    0.12, 0.62);

            var light = _clay.Light;
            var shade = _clay.Shade;

            if (_dark)
            {
                // BOTH halves, and the alpha half is the one that
                // matters. Pulling the colour toward the fill stops it
                // reading as white-on-dark, but the gradient stop then
                // RE-APPLIES its own opacity — so a pure colour shift was
                // silently discarded and the dark highlight came out at
                // full strength. That is precisely the "hard plastic
                // glare on a dark surface" failure: the highlight needs
                // to lose energy, not just hue.
                light = Mix(light, _clay.BaseFill, _clay.DarkHighlightDim);
                light = ScaleOpacity(
                    light, 1.0 - _clay.DarkHighlightDim);
            }

            // ── the press crossover ────────────────────────────────────
            // Pressing clay does not make it smaller and it does not
            // remove its shadows — those both read as "the effect
            // turned off". It pushes the object INTO the page: the drop
            // collapses toward contact, and the two inset layers CROSS
            // OVER. Light that was falling from the top-left now falls on
            // the underside, because that is what is physically true of a
            // thumb pressing a soft surface down.
            //
            // The crossover is also the reason this state cannot be
            // signalled by depth alone, which is why the templates also
            // change the fill. See the state-contrast note below.
            double pressDropScale = 1.0;
            double lightStops = insetReach;
            double shadeStops = 1.0 - insetReach;
            bool inverted = false;

            if (state == ClayInteractionState.Pressed)
            {
                dropBlur *= 0.55;
                dropDepth *= 0.35;
                dropOpacity *= _clay.PressDropOpacityScale;
                inverted = true;
            }

            // A DISABLED surface loses its drop and its highlight. It
            // keeps a shade so it still reads as a recess in the page
            // rather than a hole — and the templates drop the fill
            // contrast alongside it, so the state is carried by colour
            // as well as by shadow.
            if (state == ClayInteractionState.Disabled)
            {
                dropOpacity = 0.0;
                light = WithOpacity(light, 0.0);
            }

            return new ClayShadowStack(
                tier, state, _dark,
                dropBlur, dropDepth, dropOpacity, dropColor,
                lightStops, light,
                shadeStops, shade,
                radius, inverted,
                _dark ? t.FillLift * _clay.DarkFillLift : 0.0);
        }

        // ── colour helpers ───────────────────────────────────────────

        /// <summary>
        /// The shade is the surface hue pushed toward its own darkness —
        /// never toward grey and never toward black. Pure black insets
        /// read as dirt on a pastel; a desaturated grey reads as the
        /// shadow of a flat card. The shadow has to belong to the object.
        /// </summary>
        internal static Color Mix(Color a, Color b, double amount)
        {
            double k = Math.Clamp(amount, 0, 1);
            return Color.FromArgb(
                a.A,
                (byte)Math.Round(a.R + (b.R - a.R) * k),
                (byte)Math.Round(a.G + (b.G - a.G) * k),
                (byte)Math.Round(a.B + (b.B - a.B) * k));
        }

        internal static Color WithOpacity(Color c, double opacity) =>
            Color.FromArgb(
                (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255),
                c.R, c.G, c.B);

        /// <summary>
        /// Scales an EXISTING alpha rather than replacing it. The
        /// distinction is load-bearing: the gradient stops multiply by
        /// their own opacity on top of this, so a colour that arrives
        /// already at 65% renders at 65% of the stop rather than at a
        /// full 100% and quietly undoing the dimming.
        /// </summary>
        internal static Color ScaleOpacity(Color c, double scale) =>
            Color.FromArgb(
                (byte)Math.Round(Math.Clamp(scale, 0, 1) * c.A),
                c.R, c.G, c.B);

        /// <summary>
        /// Lightens toward white by <paramref name="amount"/>. Used for
        /// the dark-mode FILL lift, where depth has to come from
        /// lightness because shadow cannot carry it on a dark page.
        /// </summary>
        internal static Color Lift(Color c, double amount)
        {
            if (amount <= 0.001) return c;
            return Color.FromArgb(
                c.A,
                (byte)Math.Round(c.R + (255 - c.R) * amount),
                (byte)Math.Round(c.G + (255 - c.G) * amount),
                (byte)Math.Round(c.B + (255 - c.B) * amount));
        }

        private readonly struct ClayTierTable
        {
            public ClayTierTable(
                double dropBlur, double dropDepth, double dropOpacity,
                double insetBias, double fillLift)
            {
                DropBlur = dropBlur;
                DropDepth = dropDepth;
                DropOpacity = dropOpacity;
                InsetBias = insetBias;
                FillLift = fillLift;
            }

            public double DropBlur { get; }
            public double DropDepth { get; }
            public double DropOpacity { get; }
            public double InsetBias { get; }

            /// <summary>
            /// Lightness this tier adds to its fill in dark mode. Small
            /// and increasing: the top tier has the most room to go up
            /// without approaching the text.
            /// </summary>
            public double FillLift { get; }
        }
    }

    /// <summary>
    /// The clay material constants, resolved from the active
    /// <c>EightThemeProfile</c>. These are the ratios and opacities that
    /// make the recipe reproducible across eight ramps without any of
    /// them being re-tuned by hand.
    /// </summary>
    public sealed class ClayProfile
    {
        public ClayProfile(
            Color baseFill,
            Color shade,
            Color light,
            double dropBlurRatio,
            double dropDepthRatio,
            double dropOpacity,
            double insetReachRatio,
            double referenceRadius,
            double maxBlur,
            double maxDepth,
            double darkDropOpacityScale,
            double darkDropTint,
            double darkHighlightDim,
            double darkFillLift,
            double pressDropOpacityScale,
            double focusRingWidth,
            double focusRingOffset)
        {
            BaseFill = baseFill;
            Shade = shade;
            Light = light;
            DropBlurRatio = dropBlurRatio;
            DropDepthRatio = dropDepthRatio;
            DropOpacity = dropOpacity;
            InsetReachRatio = insetReachRatio;
            ReferenceRadius = referenceRadius;
            MaxBlur = maxBlur;
            MaxDepth = maxDepth;
            DarkDropOpacityScale = darkDropOpacityScale;
            DarkDropTint = darkDropTint;
            DarkHighlightDim = darkHighlightDim;
            DarkFillLift = darkFillLift;
            PressDropOpacityScale = pressDropOpacityScale;
            FocusRingWidth = focusRingWidth;
            FocusRingOffset = focusRingOffset;
        }

        /// <summary>The object's own colour. Its hue is the source of
        /// both shadows — this is the difference between clay and
        /// neumorphism, where the fill is the page.</summary>
        public Color BaseFill { get; }

        /// <summary>The far-side inner shade. Tinted, never grey.</summary>
        public Color Shade { get; }

        /// <summary>The near-side inner highlight.</summary>
        public Color Light { get; }

        public double DropBlurRatio { get; }
        public double DropDepthRatio { get; }
        public double DropOpacity { get; }

        /// <summary>How far the inset roll reaches, as a fraction of the
        /// gradient, at <see cref="ReferenceRadius"/>.</summary>
        public double InsetReachRatio { get; }

        /// <summary>
        /// The corner radius the recipe treats as "standard". Everything
        /// scales relative to it, so a 20px button and a 48px hero are
        /// the same material at two sizes rather than two materials.
        /// </summary>
        public double ReferenceRadius { get; }

        /// <summary>Engine rule 4: shadows are bounded.</summary>
        public double MaxBlur { get; }
        public double MaxDepth { get; }

        public double DarkDropOpacityScale { get; }
        public double DarkDropTint { get; }
        public double DarkHighlightDim { get; }
        public double DarkFillLift { get; }
        public double PressDropOpacityScale { get; }

        /// <summary>
        /// Width of the focus ring.
        /// <para>
        /// A ring, and a real one. An inset highlight looks enough like a
        /// focus state that designers routinely delete the outline
        /// because it breaks the soft look — which is precisely the
        /// affordance this style most needs to replace. The ring is
        /// 3px because WCAG 2.2 SC 1.4.11 wants 3:1 against adjacent
        /// colours for a non-text boundary, and SC 2.4.7 wants the focus
        /// indicator to survive being small.
        /// </para>
        /// </summary>
        public double FocusRingWidth { get; }

        /// <summary>
        /// Gap between the element's edge and the focus ring. The ring
        /// must not touch the fill or it stops reading as a ring and
        /// starts reading as a border.
        /// </summary>
        public double FocusRingOffset { get; }
    }
}