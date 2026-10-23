// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/SoftUi/SoftDepth.cs
// PROJECT: ProGlassAutomation
// MODULE: Soft UI — the vocabulary both extruding families share
//
// ── WHY THIS EXISTS, RATHER THAN A COPY IN EACH FAMILY ───────────────
// Claymorphism and neumorphism are both "soft UI": objects modelled by
// light rather than by a border. They share four things that MUST NOT be
// allowed to drift:
//
//   · the LIGHT SOURCE,
//   · the depth TIERS,
//   · the interaction STATES,
//   · the accessibility floor (ring, edge, hit target).
//
// They differ in exactly one respect — how the object is modelled:
//   clay  = one outer drop for mass, two inner rolls for volume
//   neum  = two mirrored outer shadows, and NO inner rolls at rest
//
// Everything below is shared, because the first version of this put the
// light in each family's own file. That compiles, passes review, and
// produces a bug nobody sees until a user switches themes: the
// highlights point one way on the clay screen and another way on the
// neumorphic one. Soft UI lives or dies on the light being consistent
// screen-wide, so there is now exactly one light source in the engine.
//
// ── WHAT IS DELIBERATELY NOT HERE ───────────────────────────────────
// No shadow recipe. That is each family's own business, in its own file,
// because the two constructions genuinely cannot be parameterised into
// one function without the result being mush.
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Services.Theme.SoftUi
{
    /// <summary>
    /// The four depth tiers a soft-UI surface can occupy.
    /// <para>
    /// A ratchet, not a scale you read freely: a surface may promote one
    /// tier or demote one tier, never leap. A floating modal that promoted
    /// on hover would try to leave the top of the range, and a resting
    /// list row that promoted would out-depth the card it sits inside.
    /// </para>
    /// </summary>
    public enum SoftDepthTier
    {
        /// <summary>
        /// A concave recess — a well, a pressed control, a slider track.
        /// No outer shadow at all: depth comes from moving INWARD.
        /// </summary>
        Well = 0,

        /// <summary>The default resting surface. Lists, panels, cards.</summary>
        Resting = 1,

        /// <summary>Something you can act on. Buttons, toolbars, tiles.</summary>
        Raised = 2,

        /// <summary>Over everything else. Modals, dragged objects, sheets.</summary>
        Floating = 3,
    }

    /// <summary>What the user is doing to a surface right now.</summary>
    public enum SoftInteractionState
    {
        /// <summary>Not hovered, not pressed, not focused.</summary>
        Rest = 0,

        /// <summary>Pointer is over it (and it is enabled).</summary>
        Hover = 1,

        /// <summary>Pointer or key is holding it down.</summary>
        Pressed = 2,

        /// <summary>Keyboard focus ring is showing.</summary>
        Focused = 3,

        /// <summary>Disabled. Never receives Hover or Pressed.</summary>
        Disabled = 4,
    }

    /// <summary>
    /// THE light source for the whole application.
    /// <para>
    /// One light, top-left, forever. This is not a default — it is the
    /// rule the entire family of styles rests on. Inset highlights and
    /// outset shadows pointing different ways on different elements are
    /// the single most common way a soft-UI surface betrays itself: the
    /// eye reads the inconsistent lighting long before it can name what is
    /// wrong, and the result reads as "broken" rather than "bold".
    /// </para>
    /// <para>
    /// The convention everywhere in the engine:
    /// <list type="bullet">
    /// <item>the SHADE falls AWAY from the light (down-right);</item>
    /// <item>the HIGHLIGHT sits TOWARD it (up-left).</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class SoftLight
    {
        /// <summary>
        /// Where the light is, in WPF <c>DropShadowEffect.Direction</c>
        /// degrees. WPF measures clockwise from the X axis, so 315° points
        /// up-left and therefore casts the shadow down-right, away from
        /// the light, which is what we want.
        /// </summary>
        public const double ShadowDirection = 315.0;

        /// <summary>
        /// The opposite bearing, for the half of a mirrored pair that
        /// highlights toward the light. A DropShadowEffect needs a
        /// direction even for a highlight, because the "shadow" it draws
        /// IS the light the surface is bouncing.
        /// </summary>
        public const double HighlightDirection = 135.0;

        /// <summary>Horizontal light component, -1 = toward the left.</summary>
        public const double X = -1.0;

        /// <summary>Vertical light component, -1 = toward the top.</summary>
        public const double Y = -1.0;

        /// <summary>
        /// The corner the highlight is born in. Used by the gradient
        /// insets, which have no Direction property of their own.
        /// </summary>
        public static readonly System.Windows.Point HighlightOrigin =
            new(0, 0);

        /// <summary>The corner the shade is born in.</summary>
        public static readonly System.Windows.Point ShadeOrigin =
            new(1, 1);

        /// <summary>The far corner of the same diagonal.</summary>
        public static readonly System.Windows.Point HighlightFar =
            new(1, 1);

        /// <summary>The far corner of the shade's diagonal.</summary>
        public static readonly System.Windows.Point ShadeFar =
            new(0, 0);
    }

    /// <summary>
    /// State arithmetic. One place, so the ratchet rule is enforced once
    /// and tested once rather than per family.
    /// </summary>
    public static class SoftDepthRules
    {
        /// <summary>
        /// Resolves the tier a surface should paint in.
        /// <para>
        /// <see cref="SoftInteractionState.Focused"/> deliberately returns
        /// the base tier untouched. Focus is not a depth change — see
        /// <see cref="SoftUiAccessibility"/> for why a raised-versus-flat
        /// shadow is the wrong signal for focus.
        /// </para>
        /// <para>
        /// Press also leaves the tier alone. A press changes the SHAPE of
        /// the modelling — clay crosses its inner rolls over, neumorphism
        /// inverts from outset to inset — but the object stays at the
        /// depth it was at. It simply moves into the page.
        /// </para>
        /// </summary>
        public static SoftDepthTier Resolve(
            SoftDepthTier declared, SoftInteractionState state) =>
            state switch
            {
                SoftInteractionState.Hover => Promote(declared),
                _ => declared,
            };

        /// <summary>Promotes one tier, ratcheting at the top.</summary>
        public static SoftDepthTier Promote(SoftDepthTier tier) =>
            tier >= SoftDepthTier.Floating
                ? SoftDepthTier.Floating
                : tier + 1;

        /// <summary>Demotes one tier, ratcheting at the bottom.</summary>
        public static SoftDepthTier Demote(SoftDepthTier tier) =>
            tier <= SoftDepthTier.Well
                ? SoftDepthTier.Well
                : tier - 1;

        /// <summary>
        /// True when this tier's depth comes from concavity. Concave
        /// surfaces have no outer shadow in EITHER family.
        /// </summary>
        public static bool IsConcave(SoftDepthTier tier) =>
            tier == SoftDepthTier.Well;
    }

    /// <summary>
    /// The accessibility floor, shared.
    /// <para>
    /// This is the part of soft UI that is a genuine fight rather than a
    /// styling decision, and both families lose the same way: the
    /// object's boundary is a blurred shadow a few percent off the
    /// surface, which measures around 1.2–1.9:1 against WCAG 2.2 SC
    /// 1.4.11's required 3:1. It cannot be tuned up — push the shadows
    /// far enough apart to pass and they stop reading as shadows on a
    /// shared surface, at which point you are no longer building the
    /// style.
    /// </para>
    /// <para>
    /// So the shadow is treated as DECORATION and every interactive
    /// element carries a second, compliant signal: a real edge, a real
    /// focus ring, and state that is never signalled by depth alone.
    /// </para>
    /// </summary>
    public static class SoftUiAccessibility
    {
        /// <summary>
        /// Focus ring width. A ring, and a real one — a soft-UI shadow
        /// reads as a focus state closely enough that outlines are
        /// routinely deleted because they break the look, which is
        /// precisely the affordance these styles most need to replace.
        /// </summary>
        public const double FocusRingWidth = 3;

        /// <summary>
        /// Gap between the element's edge and the focus ring. The ring
        /// sits OUTSIDE the shadow entirely, so its contrast never
        /// depends on the surface illusion holding up — and it does not
        /// touch the fill, or it stops reading as a ring and starts
        /// reading as a border.
        /// </summary>
        public const double FocusRingOffset = 3;

        /// <summary>
        /// Minimum hit target. WCAG 2.2 SC 2.5.8.
        /// </summary>
        public const double MinHitTarget = 44;

        /// <summary>
        /// How far the fill moves on press, 0..1.
        /// <para>
        /// Not optional styling. A raised-versus-pressed shadow change is
        /// a low-contrast, non-textual, shadow-only cue, which is the
        /// class WCAG 1.4.1 excludes — and it is invisible to anyone with
        /// reduced contrast sensitivity, which is a large share of the
        /// people drawn to a soft-UI theme in the first place.
        /// </para>
        /// </summary>
        public const double PressedFillShift = 0.10;

        /// <summary>
        /// Builds the accessible edge pen for a soft-UI surface.
        /// <para>
        /// Deliberately subtle in light mode, where the shadow already
        /// does most of the separating and a heavy hairline would fight
        /// it. It does real work in dark mode, where the shadow has the
        /// least contrast to give — which is exactly why the dark recipes
        /// also raise the fill lightness rather than leaning on the
        /// shadow harder.
        /// </para>
        /// </summary>
        public static System.Windows.Media.Pen BuildEdge(
            System.Windows.Media.Color fill, bool dark)
        {
            double alpha = dark ? 0.55 : 0.32;
            var c = System.Windows.Media.Color.FromArgb(
                (byte)Math.Round(alpha * 255),
                dark ? (byte)Math.Max(0, fill.R - 40) : (byte)Math.Min(255, fill.R + 60),
                dark ? (byte)Math.Max(0, fill.G - 40) : (byte)Math.Min(255, fill.G + 60),
                dark ? (byte)Math.Max(0, fill.B - 40) : (byte)Math.Min(255, fill.B + 60));

            var brush = new System.Windows.Media.SolidColorBrush(c);
            brush.Freeze();

            var pen = new System.Windows.Media.Pen(brush, 1.0);
            pen.Freeze();
            return pen;
        }
    }
}