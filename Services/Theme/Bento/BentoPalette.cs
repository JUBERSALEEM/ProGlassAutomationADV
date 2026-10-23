// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Bento/BentoPalette.cs
// PROJECT: ProGlassAutomation
// MODULE: Bento Grid — palette bridge and layout vocabulary
//
// ── WHAT MAKES BENTO DIFFERENT FROM THE OTHER SEVEN ──────────────────
// Bento is the only family in the engine whose defining feature is not a
// material. Clay, neu, holo, liquid and glassmorphism are all about how
// light behaves on a surface; Swiss is about the absence of that. Bento is
// about GRID GEOMETRY, and its materials are deliberately plain — an
// opaque fill and a real edge — because the composition is the design.
//
// That has a concrete consequence for the palette: there is no "hero
// material". A hero tile is the same colour as a unit tile, at most one
// step lighter, because the moment the anchor cell gets its own special
// material the grid stops reading as one composition and starts reading
// as a card with a favourite. See BentoRecipe.HeroDelta.
//
// ── WHY THE ENUM LIVES HERE, NOT IN THE PANEL ────────────────────────
// BentoSpan is a LAYOUT primitive, and it is declared here beside the
// metrics rather than beside BentoPanel so that the palette, the recipe
// and the controls all resolve footprints through one type. The research
// in README.md is explicit that an unenumerated bento collapses into a
// uniform card grid, so the footprint is a closed set: you cannot express
// "roughly two and a bit", and you cannot express a 3×2 without going
// through the free-form path the panel refuses.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme.Bento
{
    /// <summary>
    /// Cell footprints, in grid units. The closed set is the point: a
    /// bento needs at least two distinct footprints to be a bento at all,
    /// and free-form spans are how that requirement gets quietly dropped.
    /// </summary>
    public enum BentoSpan
    {
        /// <summary>1×1. One label or one number — no more.</summary>
        Unit = 0,

        /// <summary>2×1. A metric with a caption, or a short trend.</summary>
        Wide = 1,

        /// <summary>1×2. A stacked list, a legend plus its series.</summary>
        Tall = 2,

        /// <summary>
        /// 2×2. The anchor. At most ONE per grid — see BentoPanel.
        /// </summary>
        Hero = 3,
    }

    /// <summary>The live Bento material for the current theme.</summary>
    public static class BentoPalette
    {
        private static BentoProfile? _light;
        private static BentoProfile? _dark;

        /// <summary>True while Bento is the active family.</summary>
        public static bool IsActive { get; private set; }

        public static BentoProfile? Light => _light;
        public static BentoProfile? Dark => _dark;

        /// <summary>Raised after the material is republished or cleared.</summary>
        public static event EventHandler? Changed;

        public static void Publish(
            Color lightCanvas, Color lightSurface, Color lightInk,
            Color lightMuted, Color lightEdge, Color accent,
            Color darkCanvas, Color darkSurface, Color darkInk,
            Color darkMuted, Color darkEdge)
        {
            _light = Build(
                lightCanvas, lightSurface, lightInk, lightMuted,
                lightEdge, accent, dark: false);
            _dark = Build(
                darkCanvas, darkSurface, darkInk, darkMuted,
                darkEdge, accent, dark: true);
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

        public static BentoProfile? For(bool dark) => dark ? _dark : _light;

        /// <summary>
        /// Builds the material.
        /// <para>
        /// Bento has no directional light of its own — it borrows
        /// <c>SoftLight</c> only so that its edge, which is a real 1px
        /// stroke rather than a shadow, sits consistently with the other
        /// families. The metrics are the load-bearing part of this file;
        /// see <see cref="BentoMetrics"/> for why each number is what it
        /// is.
        /// </para>
        /// </summary>
        internal static BentoProfile Build(
            Color canvas, Color surface, Color ink, Color muted,
            Color edge, Color accent, bool dark)
        {
            var metrics = new BentoMetrics();

            // The canvas is held off the extremes. Bento leans on its
            // gutter to separate cells, and on pure white or pure black
            // the gutter has nothing to separate FROM — a 16px gap
            // between two identical cells on pure white is not a gap,
            // it is a rendering artefact.
            var ground = HoldOffExtreme(canvas, dark);

            // The surface must be distinguishable from the canvas, or
            // the cells stop reading as cells sitting ON something and
            // start reading as gaps in one field. This is the same
            // non-text contrast floor as the edge below, applied between
            // two adjacent planes.
            var fill = EnsureContrast(
                surface, dark ? Colors.White : Colors.Black,
                MinimumSurfaceContrast, ground);

            return new BentoProfile(
                ground,
                fill,

                // INK is the caller's, taken as given, for the same
                // reason SwissPalette takes it as given: a theme that
                // cannot produce readable primary text has failed
                // below this file.
                ink,

                // MUTED is pushed toward the ink until it clears 4.5:1
                // against the SURFACE, not the canvas. Muted text lives
                // inside a tile, and the tile is the surface it is
                // measured against — a muted label that clears 4.5:1 on
                // the canvas and fails on the tile is unreadable exactly
                // where it is used. This distinction is the single most
                // common contrast bug in card UIs and it is why this
                // call passes `fill` and not `ground`.
                EnsureContrast(muted, ink, MinimumBodyContrast, fill),

                // EDGE clears the WCAG non-text floor of 3:1, measured
                // against BOTH planes it can appear on: a tile border
                // can be seen against the canvas outside it, and where
                // two tiles abut, against the neighbouring surface.
                EnforceDualContrast(edge, ground, fill),

                accent,
                metrics);
        }

        /// <summary>WCAG 2.1 AA body-text contrast.</summary>
        internal const double MinimumBodyContrast = 4.5;

        /// <summary>
        /// WCAG 2.1 SC 1.4.11 non-text contrast, applied to the cell
        /// edge and to the surface/canvas separation. A 1px border is a
        /// UI component, not decoration.
        /// </summary>
        internal const double MinimumSurfaceContrast = 3.0;

        /// <summary>
        /// A tile edge that clears 3:1 against the canvas but not
        /// against an abutting tile is invisible at the seam, which is
        /// the one place the grid's structure has to be legible. Takes
        /// the stronger of the two adjustments.
        /// </summary>
        internal static Color EnforceDualContrast(
            Color candidate, Color a, Color b)
        {
            double worst = Math.Min(Contrast(candidate, a), Contrast(candidate, b));
            if (worst >= MinimumSurfaceContrast) return candidate;

            Color againstWorst = Contrast(candidate, a) <= Contrast(candidate, b) ? a : b;

            // Push away from the plane it fails against. In dark mode
            // that means toward white; in light mode, toward black.
            // Sign is taken from luminance rather than hard-coded so
            // this stays correct if a caller passes an inverted pair.
            Color toward = Luminance(againstWorst) > 0.5 ? Colors.Black : Colors.White;
            return EnsureContrast(
                candidate, toward, MinimumSurfaceContrast, againstWorst);
        }

        /// <summary>
        /// Walks <paramref name="candidate"/> toward
        /// <paramref name="toward"/> until it clears
        /// <paramref name="minimum"/> against <paramref name="against"/>.
        /// <para>
        /// Steps rather than solving algebraically, because the target is
        /// a perceptual quantity — sRGB luminance is not linear in the
        /// channel value — and a closed form would be wrong at both ends
        /// of the range while looking suspiciously precise in the middle.
        /// </para>
        /// <para>
        /// Returns <paramref name="toward"/> unchanged if even that
        /// fails, so the worst case is "as readable as the fallback",
        /// never "less readable than it was". A guard that could make a
        /// colour worse would be worse than no guard.
        /// </para>
        /// </summary>
        internal static Color EnsureContrast(
            Color candidate, Color toward, double minimum, Color against)
        {
            if (Contrast(candidate, against) >= minimum) return candidate;

            for (int step = 1; step <= 20; step++)
            {
                double k = step / 20.0;
                var trial = BentoRecipe.Mix(candidate, toward, k);
                if (Contrast(trial, against) >= minimum) return trial;
            }

            return toward;
        }

        /// <summary>WCAG relative-contrast ratio. Same maths as the
        /// engine's other palettes, so a Bento ratio can be compared
        /// against a Swiss one without a second implementation.</summary>
        internal static double Contrast(Color a, Color b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);

            if (la < lb) (la, lb) = (lb, la);
            return (la + 0.05) / (lb + 0.05);
        }

        /// <summary>
        /// Keeps the canvas off pure white and pure black. Same class of
        /// precondition as Swiss's paper rule and neumorphism's mid-tone
        /// rule: bento's structure is carried entirely by the gutter, and
        /// an extreme ground leaves the gutter nothing to describe.
        /// </summary>
        internal static Color HoldOffExtreme(Color c, bool dark)
        {
            double l = Luminance(c);
            if (dark && l < 0.03) return BentoRecipe.Mix(c, Colors.White, 0.04);
            if (!dark && l > 0.97) return BentoRecipe.Mix(c, Colors.Black, 0.03);
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
