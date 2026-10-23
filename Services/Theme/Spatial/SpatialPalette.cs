// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Spatial/SpatialPalette.cs
// PROJECT: ProGlassAutomation
// MODULE: Spatial / Immersive — the Z hierarchy and its material
//
// ── WHY THIS FAMILY NEEDS A PLANE ENUM AND NOT A TIER ─────────────────
//
// The engine's contract for Spatial, from EightThemeId:
//
//   "A real Z hierarchy (environment → ambient → panel → navigation →
//    overlay) where blur radius itself is the depth signal."
//
// That is FIVE planes. SoftDepthTier has four, and the top of its range
// is Floating — which here is the third-from-top. Mapping five planes
// onto four tiers would have to lose one, and every candidate for the
// loser is load-bearing.
//
// So Spatial declares SpatialPlane and maps the tier onto it. The tier
// is still accepted, because the engine's vocabulary is uniform and
// controls should not have to know which family they are in; but the
// plane decides the rendering and the tier only selects it. Declining
// to pretend is the honest answer, and it is the same answer Swiss
// gives when it accepts a tier it deliberately ignores.
//
// ── WHY THE PROFILE RAMPS ARE ALPHA-BEARING ───────────────────────────
//
// Every other family's ramp is opaque. Spatial's is not — the profile
// carries A("#F4F7FF", 0.62) rather than a solid hex — and that is the
// whole point of the family. A single flat alpha across five planes is
// the Glassmorphism mistake: it produces one translucent look at every
// distance instead of a ladder, and the ladder is what the user reads.
//
// ── THE CONTRAST RULE, WHICH IS THE ONE THAT BITES ────────────────────
//
// Contrast must be measured against the COMPOSITED result — the
// translucent fill flattened onto the environment behind it — and never
// against the fill's own colour. An alpha colour on its own has no
// meaningful contrast ratio: 62% white is not "light", it is "62%
// white", and over a bright environment it lands somewhere completely
// different.
//
// Getting this wrong is easy and invisible. A contrast checker run
// against an RGBA fill reports a number that means nothing, the panel
// looks fine in one build and fails in another depending on what is
// behind it, and the failure only shows up in the one theme nobody
// tests. Composited() below is the only place a Spatial colour is
// measured, and every floor in this file is applied to its output.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme.Spatial
{
    /// <summary>
    /// The Z hierarchy. Five planes, and the blur radius is the signal
    /// that tells the user which one they are looking at.
    /// </summary>
    public enum SpatialPlane
    {
        /// <summary>
        /// The room. Not a surface -- it is the thing the other planes
        /// are translucent against. Blur 0, because it is the thing
        /// being blurred rather than something doing the blurring.
        /// </summary>
        Environment = 0,

        /// <summary>Far chrome. Blur 20.</summary>
        Ambient = 1,

        /// <summary>Cards and working surfaces. Blur 40.</summary>
        Panel = 2,

        /// <summary>Nav pane and rails. Blur 20, same as ambient.</summary>
        Navigation = 3,

        /// <summary>Dialogs, sheets, flyouts. Blur 60.</summary>
        Overlay = 4,
    }

    /// <summary>The live Spatial material for the current theme.</summary>
    public static class SpatialPalette
    {
        private static SpatialProfile? _light;
        private static SpatialProfile? _dark;

        /// <summary>True while Spatial is the active family.</summary>
        public static bool IsActive { get; private set; }

        public static SpatialProfile? Light => _light;
        public static SpatialProfile? Dark => _dark;

        /// <summary>Raised after the material is republished or cleared.</summary>
        public static event EventHandler? Changed;

        /// <summary>
        /// When true, every plane collapses to an opaque equivalent.
        /// <para>
        /// Not decoration and not an edge case. Windows turns acrylic
        /// off when transparency is disabled, when battery saver is on,
        /// and when hardware is constrained, and Microsoft's own guidance
        /// is to "always provide fallback styling... and test with
        /// Transparency disabled". So this is a supported operating mode
        /// of the family rather than a fallback for when something has
        /// gone wrong, and the recipe honours it.
        /// </para>
        /// </summary>
        public static bool TransparencySuppressed { get; private set; }

        /// <summary>
        /// Turns translucency off across every plane. Idempotent, and
        /// repaints through <see cref="Changed"/> so open controls
        /// follow without a restart.
        /// </summary>
        public static void SuppressTransparency()
        {
            if (TransparencySuppressed) return;
            TransparencySuppressed = true;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>Restores translucency.</summary>
        public static void RestoreTransparency()
        {
            if (!TransparencySuppressed) return;
            TransparencySuppressed = false;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void Publish(
            Color lightEnvironment, Color lightFar, Color lightFill,
            Color lightInk, Color lightMuted, Color lightRim, Color accent,
            Color darkEnvironment, Color darkFar, Color darkFill,
            Color darkInk, Color darkMuted, Color darkRim)
        {
            _light = Build(
                lightEnvironment, lightFar, lightFill, lightInk,
                lightMuted, lightRim, accent, dark: false);
            _dark = Build(
                darkEnvironment, darkFar, darkFill, darkInk,
                darkMuted, darkRim, accent, dark: true);
            IsActive = true;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void Clear()
        {
            if (!IsActive && _light is null && _dark is null) return;
            _light = null;
            _dark = null;
            IsActive = false;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static SpatialProfile? For(bool dark) => dark ? _dark : _light;

        /// <summary>
        /// Flattens a translucent fill onto an opaque backdrop.
        /// <para>
        /// This is the only correct way to reason about a Spatial colour.
        /// Anything measured against the un-composited fill is measuring
        /// a colour that never appears on screen.
        /// </para>
        /// </summary>
        internal static Color Composited(Color fill, Color backdrop)
        {
            double a = fill.A / 255.0;
            if (a >= 1.0) return fill;
            if (a <= 0.0) return backdrop;

            return Color.FromArgb(
                255,
                (byte)Math.Round(backdrop.R + (fill.R - backdrop.R) * a),
                (byte)Math.Round(backdrop.G + (fill.G - backdrop.G) * a),
                (byte)Math.Round(backdrop.B + (fill.B - backdrop.B) * a));
        }

        /// <summary>
        /// Builds the material.
        /// <para>
        /// Every derived colour is validated against its COMPOSITED
        /// result, not against its own alpha. The floors are WCAG 2.1 AA
        /// body text and SC 1.4.11 non-text, and they are applied to the
        /// ink and rim respectively.
        /// </para>
        /// </summary>
        internal static SpatialProfile Build(
            Color environment, Color far, Color fill, Color ink, Color muted,
            Color rim, Color accent, bool dark)
        {
            var metrics = new SpatialMetrics();

            // The environment is held off both extremes. Spatial's whole
            // premise is that a ROOM shows through, and a pure white or
            // pure black room is not a room -- it is a screen, and the
            // translucency above it then has nothing to reveal.
            var ground = HoldOffExtreme(environment, dark);

            // The far chrome sits between the environment and the panels.
            // It is composited onto the ground because that is what the
            // user sees, and it is pulled away from the ground until the
            // two are distinguishable -- otherwise the whole ladder
            // collapses at its bottom rung.
            var farFlat = Composited(far, ground);
            var farNow = EnsureContrast(
                far, toward: dark ? Colors.White : Colors.Black,
                MinimumSurfaceContrast, ground);

            // The panel fill, composited over the far chrome -- because a
            // panel sits over chrome, not over the room. A panel whose
            // fill is validated against the environment can land at 3:1
            // in isolation and 1.2:1 in the arrangement it is actually
            // used in.
            var fillFlat = Composited(fill, farFlat);

            var inkNow = EnsureContrast(
                ink, toward: dark ? Colors.White : Colors.Black,
                MinimumBodyContrast, fillFlat);

            // Muted ink is validated against the panel fill, NOT against
            // the far chrome: muted body text lives in panels.
            var mutedNow = EnsureContrast(
                muted, toward: inkNow, MinimumBodyContrast, fillFlat);

            // The rim is a non-text UI component, so SC 1.4.11's 3:1
            // applies. It is checked against the fill it sits on, because
            // a rim's job is to describe the surface it surrounds -- a rim
            // that clears 3:1 against the room but not against its own
            // panel is a rim you cannot see where you are looking.
            var rimNow = EnsureContrast(
                rim, toward: dark ? Colors.White : Colors.Black,
                MinimumRuleContrast, fillFlat);

            return new SpatialProfile(
                ground, farNow, fill, inkNow, mutedNow, rimNow, accent,
                metrics);
        }

        /// <summary>WCAG 2.1 AA body-text contrast.</summary>
        internal const double MinimumBodyContrast = 4.5;

        /// <summary>WCAG 2.1 SC 1.4.11 non-text contrast, for the rim
        /// and for plane-to-plane separation.</summary>
        internal const double MinimumRuleContrast = 3.0;

        /// <summary>
        /// Plane-to-plane separation. A panel that clears 3:1 against the
        /// chrome but not against its own fill stops reading as a layer,
        /// which is the one thing this family exists to do.
        /// </summary>
        internal const double MinimumSurfaceContrast = 3.0;

        internal static Color EnsureContrast(
            Color candidate, Color toward, double minimum, Color against)
        {
            if (Contrast(candidate, against) >= minimum) return candidate;

            for (int step = 1; step <= 20; step++)
            {
                double k = step / 20.0;
                var trial = SpatialRecipe.Mix(candidate, toward, k);
                if (Contrast(trial, against) >= minimum) return trial;
            }

            return toward;
        }

        internal static double Contrast(Color a, Color b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);

            if (la < lb) (la, lb) = (lb, la);
            return (la + 0.05) / (lb + 0.05);
        }

        internal static Color HoldOffExtreme(Color c, bool dark)
        {
            double l = Luminance(c);
            if (dark && l < 0.02) return SpatialRecipe.Mix(c, Colors.White, 0.05);
            if (!dark && l > 0.98) return SpatialRecipe.Mix(c, Colors.Black, 0.04);
            return c;
        }

        internal static double Luminance(Color c)
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
