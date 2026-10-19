// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Swiss/SwissPalette.cs
// PROJECT: ProGlassAutomation
// MODULE: Swiss — palette bridge
//
// The third soft-UI sibling, and the only one with no shadow behind it.
// Kept separate from ClayPalette and NeuPalette because the material is
// genuinely different in kind: those two publish colours to composite
// with light, this one publishes a flat paper/ink pair with nothing to
// composite.
//
// It shares SoftLight and the SoftDepth vocabulary like the others, but
// it does not use them for rendering — Swiss has no directional light,
// because it has no shadow to direct. The types are shared so the
// engine's vocabulary stays uniform; the recipe simply declines to read
// the light, and that is not a contradiction.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme.Swiss
{
    /// <summary>The live Swiss material for the current theme.</summary>
    public static class SwissPalette
    {
        private static SwissProfile? _light;
        private static SwissProfile? _dark;

        /// <summary>True while Swiss is the active family.</summary>
        public static bool IsActive { get; private set; }

        public static SwissProfile? Light => _light;
        public static SwissProfile? Dark => _dark;

        /// <summary>Raised after the material is republished or cleared.</summary>
        public static event EventHandler? Changed;

        public static void Publish(
            Color lightPaper, Color lightInk, Color lightMuted,
            Color lightRule, Color accent,
            Color darkPaper, Color darkInk, Color darkMuted, Color darkRule)
        {
            _light = Build(lightPaper, lightInk, lightMuted, lightRule, accent);
            _dark = Build(darkPaper, darkInk, darkMuted, darkRule, accent);
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

        public static SwissProfile? For(bool dark) => dark ? _dark : _light;

        /// <summary>
        /// Builds the material.
        /// <para>
        /// The paper is held off pure white and off pure black. Pure
        /// white is the screen, not paper, and on it every hairline reads
        /// as a rendering artefact rather than a drawn rule; pure black
        /// makes the hairline disappear entirely. Both extremes push the
        /// ink/rule contrast below what the style needs to read as
        /// typography at all.
        /// </para>
        /// </summary>
        internal static SwissProfile Build(
            Color paper, Color ink, Color muted, Color rule, Color accent)
        {
            var metrics = new SwissMetrics(
                unit: 8,
                gutter: 24,
                margin: 64,
                ruleThickness: 1,
                ruleSelectedThickness: 2,
                motionCeiling: 150,
                bodyLineHeight: 1.5,
                displayLineHeight: 1.1);

            var held = HoldOffExtreme(paper, light: true);

            return new SwissProfile(
                held,

                // The INK is taken as given. It is the one colour the
                // caller owns outright: a theme that cannot produce
                // readable primary text has failed at a level below
                // this file, and quietly adjusting it here would hide
                // that. The rule and the muted ink are different —
                // they are DERIVED accents that inherit the caller's
                // light/dark intent, and they are enforced below.
                ink,

                // MUTED INK is pushed toward the ink until it clears
                // 4.5:1 against the paper.
                //
                // SwissContrast's doc comment promises that "muted" means
                // smaller and lighter, never washed out. A promise in a
                // comment is worth exactly nothing, though, and this is
                // the easiest promise in the engine to break by accident:
                // a mid-grey that reads as fine on #FAFAF8 sits at about
                // 3.6:1 on a #121212 dark paper, and the failure is
                // invisible until someone runs a contrast checker. So
                // the promise is enforced here instead of asserted.
                EnsureContrast(muted, ink, MinimumBodyContrast, held),

                // THE RULE is pushed toward the ink until it clears
                // 3:1 — the WCAG non-text floor, because a hairline IS
                // a non-text UI component.
                //
                // This one matters most in dark mode, where a
                // dark-on-dark rule lands around 1.08:1: a line that is
                // measurably invisible while still being drawn every
                // frame. Enforcing it means dark mode gets a LIGHT
                // hairline, which is the correct Swiss answer anyway —
                // the rule is ink on whatever ground it sits.
                EnsureContrast(rule, ink, MinimumRuleContrast, held),

                accent,
                metrics);
        }

        /// <summary>
        /// WCAG 2.1 AA body-text contrast. The floor for anything that
        /// carries words.
        /// </summary>
        internal const double MinimumBodyContrast = 4.5;

        /// <summary>
        /// WCAG 2.1 SC 1.4.11 non-text contrast. The floor for a
        /// hairline, which is a UI component rather than text.
        /// </summary>
        internal const double MinimumRuleContrast = 3.0;

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
        /// Returns <paramref name="toward"/> unchanged if even that fails,
        /// so the worst case is "as readable as the primary ink", never
        /// "less readable than it was". A guard that could make a colour
        /// worse would be worse than no guard.
        /// </para>
        /// </summary>
        internal static Color EnsureContrast(
            Color candidate, Color toward, double minimum, Color against)
        {
            if (Contrast(candidate, against) >= minimum) return candidate;

            for (int step = 1; step <= 20; step++)
            {
                double k = step / 20.0;
                var trial = SwissRecipe.Mix(candidate, toward, k);
                if (Contrast(trial, against) >= minimum) return trial;
            }

            return toward;
        }

        /// <summary>WCAG relative-contrast ratio. Same maths as the
        /// engine's other palettes, so a Swiss ratio can be compared
        /// against a clay one without a second implementation.</summary>
        internal static double Contrast(Color a, Color b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);

            if (la < lb) (la, lb) = (lb, la);
            return (la + 0.05) / (lb + 0.05);
        }

        /// <summary>
        /// Keeps the paper off pure white and pure black. Same class of
        /// precondition as neumorphism's mid-tone rule, for the same
        /// underlying reason: an extreme ground leaves the hairlines with
        /// nothing to be measured against.
        /// </summary>
        internal static Color HoldOffExtreme(Color c, bool light)
        {
            double l = Luminance(c);
            if (l > 0.97) return SwissRecipe.Mix(c, Colors.Black, 0.03);
            if (l < 0.03) return SwissRecipe.Mix(c, Colors.White, 0.04);
            return c;
        }

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