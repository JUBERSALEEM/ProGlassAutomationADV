// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Clay/ClayDepth.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — depth tiers, interaction states, light source
//
// WHY THIS FILE EXISTS
//   Claymorphism was, until this work, a palette and one drop shadow.
//   That is the "neumorphism with a warmer colour" reading of the style
//   and it does not look like clay. What makes clay clay is the RECIPE:
//
//     1. one OUTER drop shadow  — tinted to the object's own hue, offset
//        AWAY from the light, carrying the object's mass off the page;
//     2. one INNER highlight    — on the side TOWARD the light, the roll
//        of a surface catching the light;
//     3. one INNER shade        — mirroring it on the far side, which is
//        what gives the object visible WALL THICKNESS.
//
//   Layer 3 is the one everyone skips, and it is the layer that
//   separates "inflated" from "hollow". Without a far-side inset the
//   highlight alone reads as a pale rectangle with a glow, not as a
//   moulded body with an edge you could press your thumb into.
//
//   None of that can live in a colour ramp. It is a construction, and
//   this file is the construction's vocabulary: how deep something is,
//   what state it is in, and where the light is.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Services.Theme.Clay
{
    /// <summary>
    /// The four depth tiers a clay surface can occupy.
    /// <para>
    /// Every shadow length, blur and highlight strength in the recipe is
    /// derived from this one value, so promoting a surface is a change of
    /// a single integer rather than a retune of six numbers. That is what
    /// makes hover able to "lift" a card a tier without any component
    /// knowing what a shadow is.
    /// </para>
    /// <para>
    /// The order is not cosmetic. It is a ratchet: a hover may promote,
    /// and a promote may demote, but nothing may jump more than one
    /// tier in a single transition. A card that leaps from Resting to
    /// Floating on hover has not gained depth, it has gained a jump scare.
    /// </para>
    /// </summary>
    public enum ClayDepthTier
    {
        /// <summary>
        /// A concave WELL — a recess you type into or drop content into.
        /// <para>
        /// The one tier with NO outer drop. A field you are about to type
        /// into should recede; giving it a lift makes it read as a button.
        /// This is the inversion that separates clay from a card, and
        /// getting it wrong is why so many "clay" forms feel like a
        /// keyboard made of pillows.
        /// </para>
        /// </summary>
        Well = 0,

        /// <summary>The default resting surface. Lists, panels, chips.</summary>
        Resting = 1,

        /// <summary>Something you can act on. Buttons, toolbars, cards.</summary>
        Raised = 2,

        /// <summary>
        /// Over everything else. Modals, dragged objects, menus, sheets.
        /// </summary>
        Floating = 3,
    }

    /// <summary>What the user is doing to a surface right now.</summary>
    public enum ClayInteractionState
    {
        /// <summary>Not hovered, not pressed, not focused.</summary>
        Rest = 0,

        /// <summary>Pointer is over it (and it is enabled).</summary>
        Hover = 1,

        /// <summary>Pointer or key is holding it down.</summary>
        Pressed = 2,

        /// <summary>
        /// Keyboard focus ring is showing. Not a shadow state at all —
        /// see <see cref="ClaySurfaceMetrics"/> for why this is separate.
        /// </summary>
        Focused = 3,

        /// <summary>Disabled. Never receives Hover or Pressed.</summary>
        Disabled = 4,
    }

    /// <summary>
    /// THE light source.
    /// <para>
    /// One light, for the entire application, from the top-left. This is
    /// not a default — it is the rule the entire style rests on. Inset
    /// highlights that point different ways on different elements are
    /// the single most common way a soft-UI surface betrays itself: the
    /// eye reads the inconsistent lighting long before it can name what is
    /// wrong. Clay does not survive being lit from two directions.
    /// </para>
    /// <para>
    /// The convention throughout the engine:
    /// <list type="bullet">
    /// <item>the DROP falls AWAY from the light (down-right);</item>
    /// <item>the HIGHLIGHT sits TOWARD it (up-left);</item>
    /// <item>the SHADE mirrors the highlight exactly (down-right).</item>
    /// </list>
    /// If you add a clay surface and it does not obey all three, the
    /// surface is wrong — not the rule.
    /// </para>
    /// </summary>
    public static class ClayLight
    {
        /// <summary>
        /// Where the light is, in WPF <c>DropShadowEffect.Direction</c>
        /// degrees. WPF measures clockwise from the X axis, so 315° points
        /// up-left — and therefore casts the shadow down-right, away from
        /// the light, which is what we want.
        /// </summary>
        public const double ShadowDirection = 315.0;

        /// <summary>
        /// Horizontal component of the light, normalised to -1..1 where
        /// -1 is "toward the left". Used by the gradient insets, which
        /// have no Direction property of their own.
        /// </summary>
        public const double X = -1.0;

        /// <summary>
        /// Vertical component of the light, normalised to -1..1 where -1
        /// is "toward the top". Used by the gradient insets.
        /// </summary>
        public const double Y = -1.0;

        /// <summary>
        /// The diagonal the gradients run along. A highlight roll and a
        /// shade roll are the SAME gradient with opposite stops, which is
        /// why they read as two sides of one light rather than two
        /// unrelated effects. In WPF a LinearGradientBrush runs from
        /// StartPoint to EndPoint across the bounding box, so
        /// (0,0)→(1,1) is a clean top-left-to-bottom-right diagonal.
        /// </summary>
        public const double GradientDiagonal = 1.0;
    }

    /// <summary>
    /// State arithmetic. Kept separate from the metrics so that the
    /// ratchet rule — no transition may skip a tier — is enforced in one
    /// place and tested once.
    /// </summary>
    public static class ClayDepthRules
    {
        /// <summary>
        /// Resolves the tier a surface should actually paint in, given its
        /// declared tier and its interaction state.
        /// <para>
        /// <see cref="ClayInteractionState.Focused"/> deliberately returns
        /// the base tier untouched. Focus is NOT a depth change — see
        /// <see cref="ClaySurfaceMetrics.FocusRingWidth"/> for why a
        /// raised-versus-flat shadow is the wrong signal for focus.
        /// </para>
        /// <para>
        /// Hover promotes exactly one tier. Not "promote to Raised",
        /// not "promote by the tier's natural target" — ONE. A floating
        /// modal that promoted on hover would try to leave the top of the
        /// scale, and a resting list row that promoted one tier would
        /// suddenly out-depth the card it sits inside.
        /// </para>
        /// </summary>
        public static ClayDepthTier Resolve(
            ClayDepthTier declared, ClayInteractionState state)
        {
            switch (state)
            {
                case ClayInteractionState.Hover:
                    return Promote(declared);

                case ClayInteractionState.Pressed:
                    // A press does NOT change tier. It changes the SHAPE
                    // of the tier — the insets invert and the drop
                    // collapses. The object stays at the depth it was at;
                    // it just moves down into the page.
                    return declared;

                case ClayInteractionState.Focused:
                case ClayInteractionState.Rest:
                case ClayInteractionState.Disabled:
                default:
                    return declared;
            }
        }

        /// <summary>Promotes one tier, ratcheting at the top.</summary>
        public static ClayDepthTier Promote(ClayDepthTier tier) =>
            tier >= ClayDepthTier.Floating
                ? ClayDepthTier.Floating
                : tier + 1;

        /// <summary>Demotes one tier, ratcheting at the bottom.</summary>
        public static ClayDepthTier Demote(ClayDepthTier tier) =>
            tier <= ClayDepthTier.Well
                ? ClayDepthTier.Well
                : tier - 1;

        /// <summary>
        /// True when this tier's depth comes from concavity rather than
        /// elevation. Wells have no drop shadow at all.
        /// </summary>
        public static bool IsConcave(ClayDepthTier tier) =>
            tier == ClayDepthTier.Well;
    }
}