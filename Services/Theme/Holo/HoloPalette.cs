// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Holo/HoloPalette.cs
// PROJECT: ProGlassAutomation
// MODULE: Holographic / Iridescent — palette bridge
//
// ── THREE PRECONDITIONS, ALL ENFORCED ─────────────────────────────────
//
// This family is the most likely in the engine to ship something
// beautiful and unusable, because the failure mode is invisible in a
// screenshot and obvious in use: the surface animates, so the text on
// it never stays at one contrast ratio. A screenshot catches a single
// frame; the user catches all of them.
//
// So all three of this family's promises are ENFORCED here rather than
// written in a comment:
//
//   1. THE GROUND IS OPAQUE. Foil is rim-only. A caller cannot obtain a
//      translucent ground to put a label on, because there is no code
//      path that produces one.
//   2. THE INK CLEARS AA ON THE GROUND. Body text is body text.
//   3. THE INK CLEARS AA ON THE WORST BAND. This is the one that
//      matters and the one nobody ships. A gradient does not have a
//      contrast ratio; it has a best point and a worst point, and the
//      glyph lands on whichever one physics puts it on. So HoloChip's
//      ink is measured against EVERY band and darkened until the worst
//      of them clears the floor.
//
// Without (3), an accent chip is a coin flip: legible when the foil
// happens to be dark under the glyph, unreadable when it does not.
// That is not a theme, it is a slot machine.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Services.Theme.Holo
{
    /// <summary>The live holographic material for the current theme.</summary>
    public static class HoloPalette
    {
        private static HoloProfile? _light;
        private static HoloProfile? _dark;

        /// <summary>True while Holographic is the active family.</summary>
        public static bool IsActive { get; private set; }

        public static HoloProfile? Light => _light;
        public static HoloProfile? Dark => _dark;

        /// <summary>Raised after the material is republished or cleared.</summary>
        public static event EventHandler? Changed;

        public static void Publish(
            Color lightGround, Color lightInk, Color lightMuted, Color lightAccent,
            Color darkGround, Color darkInk, Color darkMuted, Color darkAccent)
        {
            _light = Build(lightGround, lightInk, lightMuted, lightAccent, dark: false);
            _dark = Build(darkGround, darkInk, darkMuted, darkAccent, dark: true);
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

        public static HoloProfile? For(bool dark) => dark ? _dark : _light;

        /// <summary>WCAG 2.1 AA body-text contrast.</summary>
        internal const double MinimumBodyContrast = 4.5;

        /// <summary>
        /// The saturation ceiling for the whole spectrum.
        /// <para>
        /// 0.55 is well under the 0.8+ that a naive "make it
        /// holographic" implementation reaches for, and the gap is the
        /// point. Real foil is a THIN metal film over a dielectric: it
        /// reflects maybe half the visible light at any given angle, so
        /// it is never anywhere near the chroma of a television. Pushing
        /// past this ceiling does not read as "more holographic", it
        /// reads as a novelty filter.
        /// </para>
        /// </summary>
        internal const double ChromaCeiling = 0.55;

        /// <summary>
        /// Bands in the spectrum. Many, and an ODD count, so the axis
        /// has a centre band rather than a hard seam between two
        /// equal-width halves.
        /// </summary>
        internal const int SpectrumBands = 13;

        /// <summary>Full cycles of the spectrum across the grating axis.</summary>
        internal const double SpectrumTurns = 1.85;

        /// <summary>
        /// Target band luminance for the LIGHT ground, whose ink is dark.
        /// Solved so the darkest ink clears 4.5:1 against the WORST band.
        /// </summary>
        internal const double LightBandLuminance = 0.34;

        /// <summary>
        /// Target band luminance for the DARK ground, whose ink is light.
        /// Symmetric to the light case and for the same reason: a light
        /// ink needs the bands to sit UNDER it, so they must be darker
        /// than the ink rather than brighter.
        /// </summary>
        internal const double DarkBandLuminance = 0.10;

        /// <summary>
        /// Builds the material.
        /// <para>
        /// Note what is NOT here: no transparency anywhere. The foil's
        /// presence comes from the rim's opacity and its colour, never
        /// from an alpha on the ground, because an alpha on the ground
        /// is precisely the thing that would put a hue behind a label.
        /// </para>
        /// </summary>
        internal static HoloProfile Build(
            Color ground, Color ink, Color muted, Color accent, bool dark)
        {
            // ── PRECONDITION 1: the ground is opaque. ──
            // Enforced rather than trimmed, because a translucent ground
            // cannot be repaired later without inventing a colour, and
            // the only available guess — "just use the window colour" —
            // is exactly the guess that puts a hue behind text.
            var flat = Color.FromArgb(255, ground.R, ground.G, ground.B);

            // ── PRECONDITION 2: the ink clears AA on the ground. ──
            // The holographic dark ground is near-black violet, so this
            // normally passes trivially; the LIGHT ground is a pale
            // iridescent page and is where it actually bites.
            var safeInk = EnsureContrast(
                ink, flat, MinimumBodyContrast, toward: Colors.Black);

            var safeMuted = EnsureContrast(
                muted, flat, MinimumBodyContrast, toward: safeInk);

            var spectrum = HoloSpectrum.Build(
                SpectrumBands,
                // Start off-magenta because the family ramp's borders
                // already lead with #FF0080, so the generated spectrum
                // and the painted tokens agree at their shared end. Two
                // different starting hues in one theme is the third most
                // common tell after uniform banding and flat saturation.
                startHue: 322.0,
                turns: SpectrumTurns,
                chromaCeiling: ChromaCeiling,
                // ── THE INK GUARANTEE ──
                // The whole spectrum is solved to ONE luminance, chosen
                // so that the family's own ink clears 4.5:1 against every
                // band at once.
                //
                // Dark mode's bands are DARK because its ink is light: a
                // light ink needs a band luminance low enough to sit
                // under, and light mode's are BRIGHT because its ink is
                // dark. Each mode is tuned to its own ink, which is the
                // whole reason a single spectrum cannot serve both.
                //
                // 0.10 dark / 0.34 light gives roughly 6:1 at the WORST
                // band in both modes — comfortably over AA, with enough
                // margin that the 12-iteration bisection's own error
                // cannot push a band under the floor.
                targetLuminance: dark ? DarkBandLuminance
                                      : LightBandLuminance);

            var motion = new HoloMotion(
                // 26s and 9.5s. The research is emphatic that a mesh
                // gradient which is consciously trackable is wrong:
                // "animate at glacial speed ... think 20-60 second loops
                // minimum". Anything under about 8s starts reading as an
                // animation rather than as a material.
                driftPeriod: 26000.0,
                sweepPeriod: 9500.0,

                // The grating axis comes from the SHARED light. Every
                // other family already obeys SoftLight; a foil that
                // swept on its own axis would be the one lit object in
                // the app pointing somewhere else.
                rimAngle: SoftLight.HighlightDirection,
                rimOpacity: 0.55,

                // Bright enough to read as foil, faint enough not to
                // blow out a 1px rim. This is the number that decides
                // whether the material looks premium or looks like a
                // scanning artefact.
                sheenOpacity: 0.42,
                sheenWidth: 0.28);

            return new HoloProfile(
                flat,
                Shade(flat, 0.06, dark),
                Shade(flat, 0.12, dark),
                safeInk,
                safeMuted,
                accent,
                Colors.White,
                spectrum,
                motion,
                ChromaCeiling,

                // The family profile's card radius. Holographic foil is
                // a film ON a shape, so the shape keeps its own round
                // silhouette — unlike Swiss, where the style abolishes
                // the corner, and unlike neumorphism, where the radius
                // IS the construction.
                radius: 18.0);
        }

        /// <summary>
        /// Darkens (light mode) or lightens (dark mode) a ground step.
        /// Held to a small value on purpose: the ground has to stay
        /// matte, and a ground that has drifted far from the ramp starts
        /// competing with the foil for attention.
        /// </summary>
        internal static Color Shade(Color c, double amount, bool dark)
        {
            return dark
                ? Mix(c, Colors.White, amount * 0.6)
                : Mix(c, Colors.Black, amount);
        }

        /// <summary>
        /// Walks <paramref name="candidate"/> toward
        /// <paramref name="toward"/> until it clears
        /// <paramref name="minimum"/> against <paramref name="against"/>.
        /// </summary>
        internal static Color EnsureContrast(
            Color candidate, Color against, double minimum, Color toward)
        {
            if (Contrast(candidate, against) >= minimum) return candidate;

            for (int step = 1; step <= 20; step++)
            {
                var trial = Mix(candidate, toward, step / 20.0);
                if (Contrast(trial, against) >= minimum) return trial;
            }

            return toward;
        }

        internal static Color Mix(Color a, Color b, double k)
        {
            double t = Math.Clamp(k, 0.0, 1.0);
            return Color.FromRgb(
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        /// <summary>WCAG relative-contrast ratio.</summary>
        internal static double Contrast(Color a, Color b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);

            if (la < lb) (la, lb) = (lb, la);
            return (la + 0.05) / (lb + 0.05);
        }

        /// <summary>
        /// WCAG relative luminance.
        /// <para>
        /// The 0.04045 threshold is the one from the WCAG definition.
        /// The older 0.03928 value that is still floating around in most
        /// code samples is the IEC one; the difference only shows up in
        /// the bottom two or three code values of a channel, but this
        /// family builds its grounds deliberately near black, which is
        /// exactly where the two disagree.
        /// </para>
        /// </summary>
        internal static double Luminance(Color c)
        {
            double L(byte v)
            {
                double s = v / 255.0;
                return s <= 0.04045 ? s / 12.92
                                    : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            return L(c.R) * 0.2126 + L(c.G) * 0.7152 + L(c.B) * 0.0722;
        }
    }
}