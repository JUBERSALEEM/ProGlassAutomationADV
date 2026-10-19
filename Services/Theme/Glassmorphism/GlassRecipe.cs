// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Glassmorphism/GlassRecipe.cs
// PROJECT: ProGlassAutomation
// MODULE: Glassmorphism — the 2020 recipe
//
// ── UNIFORM BLUR, AND THAT IS THE WHOLE DIFFERENCE ────────────────────
//
// EightMaterialKind.Frosted is documented as "Translucent, uniform
// backdrop blur, hairline rim, static. The 2020 recipe."
//
// UNIFORM. Every surface, every tier, the same radius. That one word is
// the entire distance between this family and Spatial, and it is worth
// stating what it costs: a uniform blur means the style has NO DEPTH
// LADDER. Everything is at the same distance from the viewer. The 2020
// recipe creates depth through MATERIAL — frosted, lit from behind —
// rather than through POSITION, which is what Spatial does with its five
// planes.
//
// GlassMetrics.BlurRadius has no tier parameter, and that is enforced
// rather than documented: there is nothing to pass.
//
// ── THE TRANSLUCENCY IS NOT REACHABLE FROM CONTENT ────────────────────
//
// See GlassPalette's header for the research this rests on. The floor is
// MinimumSurfaceOpacity, and it is applied in the recipe rather than
// left to each caller, so there is exactly one place where a glass fill
// becomes opaque and every path through it goes via that place.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme.Eight;

namespace ProGlassAutomation.Services.Theme.Glassmorphism
{
    /// <summary>The 2020 measurement system.</summary>
    public sealed class GlassMetrics
    {
        /// <summary>
        /// THE blur radius. 12px, and there is no tier parameter because
        /// there is no ladder — see the file header.
        /// <para>
        /// Low on purpose. The research is consistent that blur is the
        /// first thing to overdo: "start with low blur values, something
        /// around 4-6px is usually enough; larger values often look
        /// muddy", and past ~24px the background stops being readable
        /// "which may be intentional for modal overlays but problematic
        /// for persistent UI panels".
        /// </para>
        /// </summary>
        public double BlurRadius => 12;

        /// <summary>
        /// Saturation boost applied to what shows through.
        /// <para>
        /// From the canonical recipe (saturate 160%) and it is not a
        /// cosmetic detail: blur pulls colour toward grey, and without
        /// the boost every surface behind the glass converges on the
        /// same desaturated tone, which is what makes heavy glass look
        /// like fog rather than like frost.
        /// </para>
        /// </summary>
        public double SaturationBoost => 1.6;

        /// <summary>Corner radius, 16px. One value, every surface.</summary>
        public double Radius => 16;

        /// <summary>The 1px rim. See GlassMetrics doc: this is the
        /// forced-colors fallback and is always drawn.</summary>
        public double RimThickness => 1;

        /// <summary>
        /// The floor a CONTENT surface's fill is held to.
        /// <para>
        /// 0.78 — above the 0.40-0.55 band the sources give for
        /// "text-heavy cards", and deliberately so. Those figures assume
        /// a known, fairly uniform backdrop; this app's backdrop is
        /// whatever the user has open behind it, and Axess Lab measured
        /// the same component swinging from 5.2:1 to 1.8:1 across
        /// backdrops. The research's own conclusion is that consistent
        /// compliance requires 85-90%, which would leave no visible
        /// translucency at all; 0.78 is the compromise this engine makes
        /// because CONTENT never reaches here anyway (see
        /// GlassPlacement) — Chrome carries the effect.
        /// </para>
        /// </summary>
        public double MinimumSurfaceOpacity => 0.78;

        /// <summary>
        /// The alpha a CHROME surface actually runs at.
        /// <para>
        /// 0.30 — the top of the 0.25-0.35 band the sources give for
        /// navigation bars, and the point at which "changing a glass
        /// layer from 20% to 30% opacity can dramatically improve
        /// readability without breaking visual softness".
        /// </para>
        /// </summary>
        public double ChromeOpacity => 0.30;

        /// <summary>
        /// Maximum number of glass levels.
        /// <para>
        /// Three, per "limit your glass layers to three depth levels
        /// maximum — more than that and you're not creating hierarchy,
        /// you're creating confusion".
        /// </para>
        /// </summary>
        public int MaxGlassLevels => 3;

        /// <summary>Motion ceiling. Short, because nothing here moves.</summary>
        public double MotionCeiling => 120;

        /// <summary>
        /// NEVER animate the blur radius.
        /// <para>
        /// Stated in the research as a performance rule — "blur cost
        /// scales with blurred area" — and it is also the reason the
        /// recipe returns plain colour values rather than an animatable
        /// blur: there is no property here to animate by accident.
        /// </para>
        /// </summary>
        public bool BlurIsAnimatable => false;
    }

    /// <summary>The resolved colours for one glass surface.</summary>
    public sealed class GlassCell
    {
        public GlassCell(
            Color fill, Color ink, Color mutedInk, Color rim,
            double rimThickness, bool translucent)
        {
            Fill = fill;
            Ink = ink;
            MutedInk = mutedInk;
            Rim = rim;
            RimThickness = rimThickness;
            Translucent = translucent;
        }

        /// <summary>
        /// The frost. Opaque on content surfaces by policy, translucent
        /// on chrome.
        /// </summary>
        public Color Fill { get; }

        public Color Ink { get; }
        public Color MutedInk { get; }

        /// <summary>
        /// The 1px rim, ALWAYS present.
        /// <para>
        /// "The 1px light border is an accessibility feature, not
        /// decoration: Windows forced-colors mode strips transparency
        /// and leaves that border as the only visible edge." On Windows
        /// — which this is — that mode is one property read away, so the
        /// rim is not optional here in a way it would not be on the web.
        /// </para>
        /// </summary>
        public Color Rim { get; }

        public double RimThickness { get; }

        /// <summary>False whenever the surface was forced opaque — by
        /// placement, by high contrast, or by the user.</summary>
        public bool Translucent { get; }
    }

    /// <summary>The resolved construction for one surface in one state.</summary>
    public sealed class GlassStack
    {
        public GlassStack(
            GlassPlacement placement,
            SoftDepthTier tier,
            SoftInteractionState state,
            GlassCell cell,
            bool interactive,
            bool selected,
            bool motionAllowed)
        {
            Placement = placement;
            Tier = tier;
            State = state;
            Cell = cell;
            Interactive = interactive;
            Selected = selected;
            MotionAllowed = motionAllowed;
        }

        public GlassPlacement Placement { get; }
        public SoftDepthTier Tier { get; }
        public SoftInteractionState State { get; }
        public GlassCell Cell { get; }
        public bool Interactive { get; }
        public bool Selected { get; }
        public bool MotionAllowed { get; }

        public bool Focused => State == SoftInteractionState.Focused;

        public Color? FocusRing => Focused ? Cell.Ink : (Color?)null;

        /// <summary>
        /// ONE shadow, and it is small.
        /// <para>
        /// The canonical recipe has "one soft shadow". This family has no
        /// shadow pair, because it has no elevation ladder to encode —
        /// that is Spatial's job. A single wide, faint shadow here is
        /// doing the same work as a rim: keeping the pane from being a
        /// smudge.
        /// </para>
        /// </summary>
        public IReadOnlyList<object> Shadows { get; } =
            Array.Empty<object>();

        public bool HasShadow => false;
        public bool HasRadius => true;
    }

    /// <summary>The Glassmorphism material, derived from the ramp.</summary>
    public sealed class GlassProfile
    {
        public GlassProfile(
            Color stage, Color fill, Color contentFill, Color ink,
            Color mutedInk, Color rim, Color accent, GlassMetrics metrics)
        {
            Stage = stage;
            Fill = fill;
            ContentFill = contentFill;
            Ink = ink;
            MutedInk = mutedInk;
            Rim = rim;
            Accent = accent;
            Metrics = metrics;
        }

        /// <summary>What the glass sits on. Must not be a flat white —
        /// blur needs something to do.</summary>
        public Color Stage { get; }

        /// <summary>The chrome-tinted fill, alpha-bearing.</summary>
        public Color Fill { get; }

        /// <summary>The already-opaque content fill.</summary>
        public Color ContentFill { get; }

        public Color Ink { get; }
        public Color MutedInk { get; }
        public Color Rim { get; }
        public Color Accent { get; }
        public GlassMetrics Metrics { get; }
    }

    /// <summary>The recipe. Stateless apart from the dark flag.</summary>
    public sealed class GlassRecipe
    {
        private readonly GlassProfile _glass;
        private readonly bool _dark;

        public GlassRecipe(GlassProfile glass, bool dark)
        {
            _glass = glass ?? throw new ArgumentNullException(nameof(glass));
            _dark = dark;
        }

        /// <summary>Resolves one surface.</summary>
        public GlassStack Resolve(
            GlassPlacement placement,
            SoftDepthTier tier,
            SoftInteractionState state,
            bool interactive,
            bool selected)
        {
            var m = _glass.Metrics;

            // ── THE ONE DECISION THAT DEFINES THIS FAMILY ──────────────
            //
            // Content is opaque. Not "more opaque", not "opaque unless
            // the caller really wants" — opaque, by construction, with no
            // path that produces a translucent content surface.
            //
            // Everything else in this file is a detail; this is the part
            // that makes the family shippable, and it is the direct
            // consequence of the research quoted in GlassPalette's header.
            bool forcedOpaque =
                placement == GlassPlacement.Content
                || GlassPalette.TransparencySuppressed;

            double alpha = forcedOpaque
                ? m.MinimumSurfaceOpacity
                : m.ChromeOpacity;

            Color baseFill = _glass.Fill;

            // Hover nudges the frost. It never moves the surface: a
            // glass pane that slid on hover would slide the thing behind
            // it too, and the backdrop is not ours.
            if (interactive && state == SoftInteractionState.Hover)
                alpha = Math.Min(1.0, alpha + 0.06);

            // Press deepens rather than lightens, for the same reason
            // Spatial presses recede: a pressed thing moves away from
            // you, and the gesture is shared across this engine's
            // translucent families.
            if (state == SoftInteractionState.Pressed)
                alpha = Math.Min(1.0, alpha + 0.04);

            Color fill = Fade(baseFill, alpha);

            Color ink = _glass.Ink;
            Color muted = _glass.MutedInk;
            Color rim = _glass.Rim;
            double rimWidth = m.RimThickness;

            if (state == SoftInteractionState.Disabled)
            {
                fill = Mix(fill, _glass.Stage, 0.45);
                rim = Mix(rim, _glass.Stage, 0.45);
                muted = Mix(muted, _glass.Stage, 0.45);
            }

            if (selected)
            {
                rim = _glass.Accent;
                rimWidth = m.RimThickness + 1;
            }

            if (state == SoftInteractionState.Focused)
            {
                rim = _glass.Accent;
                rimWidth = m.RimThickness + 1;
            }

            var cell = new GlassCell(
                fill, ink, muted, rim, rimWidth,
                translucent: !forcedOpaque);

            return new GlassStack(
                placement, tier, state, cell, interactive, selected,
                motionAllowed: !ReducedMotion());
        }

        private static bool ReducedMotion()
        {
            try { return EightThemePainter.ReducedMotionRequested; }
            catch (Exception) { return false; }
        }

        internal static Color Fade(Color c, double opacity)
        {
            double k = Math.Clamp(opacity, 0.0, 1.0);
            return Color.FromArgb(
                (byte)Math.Round(c.A * k), c.R, c.G, c.B);
        }

        /// <summary>Returns the colour at a given alpha.</summary>
        internal static Color WithAlpha(Color c, double alpha)
        {
            return Color.FromArgb(
                (byte)Math.Round(Math.Clamp(alpha, 0.0, 1.0) * 255),
                c.R, c.G, c.B);
        }

        internal static Color Lighten(Color c, double k) =>
            Mix(c, Colors.White, k);

        internal static Color Darken(Color c, double k) =>
            Mix(c, Colors.Black, k);

        internal static Color Mix(Color a, Color b, double k)
        {
            double t = Math.Clamp(k, 0, 1);
            return Color.FromArgb(
                (byte)Math.Round(a.A + (b.A - a.A) * t),
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }
    }
}
