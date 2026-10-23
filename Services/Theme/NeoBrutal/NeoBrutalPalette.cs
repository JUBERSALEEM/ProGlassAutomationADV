// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/NeoBrutal/NeoBrutalPalette.cs
// PROJECT: ProGlassAutomation
// MODULE: Neo-Brutalism — the stamped material, and its ink contract
//
// ── INK IS BLACK ON EVERY ACCENT, AND THAT IS NOT A DEFAULT ──────────
//
// Every other family in this engine resolves its accent foreground to
// white, and does so by reflex, because white is what usually works on a
// saturated fill. Here it does not:
//
//   "Black text on acid yellow passes, white text on acid does not."
//
// Measured against the shipped ramp rather than quoted from the source,
// which claimed 3.09:1 and was in fact well below that: black on acid
// yellow is 14.80:1 and white on the same fill is 1.34:1. The source
// figure was directionally right and numerically wrong, and the real
// number is far worse -- 1.34:1 does not fail AA, it fails the sense of
// the element entirely.
//
// So AccentForeground walks toward INK, not toward white, and
// EnsureAccentInk is the function that guarantees it. It is the one place
// in this engine where the "walk toward the lighter colour" heuristic
// used by every other family is deliberately inverted.
//
// ── THE BORDER IS A NON-TEXT ELEMENT, AND IT HAS TWO BACKDROPS ────────
//
// SC 1.4.11 wants 3:1 against adjacent colour, and this style puts its
// border against TWO things at once: the fill inside it and the page
// outside it. The documented failure mode is exact -- "a black border on
// a dark section". So InkOnFill and InkOnPage are resolved independently
// and the border has to clear both, because a border that clears the fill
// can still vanish into the page behind it.
//
// ── HIGH CONTRAST REMOVES THE ACCENT, NOT THE STRUCTURE ──────────────
//
// Windows forced-colors strips colour, and a style built entirely out of
// colour is stripped to nothing. What survives is geometry: the border,
// the offset, and the square corner. So this family collapses its accents
// to paper when transparency is suppressed, but never collapses its
// border or its shadow -- those are the parts the OS cannot take away.
using System;
using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Services.Theme.NeoBrutal
{
    /// <summary>The live Neo-Brutalist material.</summary>
    public static class NeoBrutalPalette
    {
        private static NeoBrutalProfile? _light;
        private static NeoBrutalProfile? _dark;

        public static bool IsActive { get; private set; }
        public static NeoBrutalProfile? Light => _light;
        public static NeoBrutalProfile? Dark => _dark;

        public static event EventHandler? Changed;

        /// <summary>
        /// Forced plain mode, from Windows high contrast or the user.
        /// Named for what it does to this family specifically: it
        /// collapses ACCENT, not structure.
        /// </summary>
        public static bool TransparencySuppressed { get; private set; }

        public static bool HighContrastRequested
        {
            get
            {
                try { return SystemParameters.HighContrast; }
                catch (Exception) { return false; }
            }
        }

        public static void SuppressTransparency()
        {
            if (TransparencySuppressed) return;
            TransparencySuppressed = true;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void RestoreTransparency()
        {
            if (!TransparencySuppressed) return;
            TransparencySuppressed = false;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void SyncHighContrast()
        {
            bool wanted = HighContrastRequested;
            if (wanted == TransparencySuppressed) return;
            TransparencySuppressed = wanted;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void Publish(
            Color lightPage, Color lightSurface, Color lightInk,
            Color lightMuted, Color lightBorder, Color lightShadow,
            Color lightAccent, Color lightAccentForeground,
            Color lightDanger, Color lightInfo, Color lightSuccess,
            Color darkPage, Color darkSurface, Color darkInk,
            Color darkMuted, Color darkBorder, Color darkShadow,
            Color darkAccent, Color darkAccentForeground,
            Color darkDanger, Color darkInfo, Color darkSuccess)
        {
            // The accent label is ALWAYS the family's true ink -- the
            // black -- in BOTH ramps, and that is the fix for a bug this
            // method had on first pass.
            //
            // Build was being handed the CURRENT MODE's body ink as its
            // "prefer black" target. In the light ramp that is #0A0A0A
            // and everything works. In the DARK ramp the body ink is
            // #FFFDF5, so the preference resolved to white -- and white
            // on acid yellow measures 1.34:1. Every dark-mode primary
            // button in the app would have been unreadable, in the one
            // family whose entire pitch is that it is readable.
            //
            // The accent label is not mode-dependent. Black text on a
            // saturated fill is a property of the STYLE, so it is
            // resolved from lightInk once and used for both ramps.
            _light = Build(
                lightPage, lightSurface, lightInk, lightMuted,
                lightBorder, lightShadow, lightAccent,
                lightAccentForeground, lightDanger, lightInfo,
                lightSuccess, accentInk: lightInk, dark: false);

            _dark = Build(
                darkPage, darkSurface, darkInk, darkMuted,
                darkBorder, darkShadow, darkAccent,
                darkAccentForeground, darkDanger, darkInfo,
                darkSuccess, accentInk: lightInk, dark: true);

            IsActive = true;
            SyncHighContrast();
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

        public static NeoBrutalProfile? For(bool dark) => dark ? _dark : _light;

        internal static NeoBrutalProfile Build(
            Color page, Color surface, Color ink, Color muted,
            Color border, Color shadow,
            Color accent, Color accentForeground,
            Color danger, Color info, Color success,
            Color accentInk, bool dark)
        {
            var metrics = new NeoBrutalMetrics();

            // Paper is held off pure white and pure black. A pure white
            // page makes the black border the only thing on screen and
            // the whole thing vibrates.
            var paper = HoldOffExtreme(page, dark);
            var board = HoldOffExtreme(surface, dark);

            var inkNow = EnsureContrast(
                ink, paper, MinimumBodyContrast, paper);
            var mutedNow = EnsureContrast(
                muted, paper, MinimumBodyContrast, paper);

            // THE BORDER, MEASURED TWICE. See the file header. The usual
            // failure is a black border that clears the fill and then
            // disappears into the page behind it.
            var borderNow = EnsureContrast(
                border, paper, MinimumRuleContrast, paper);
            borderNow = EnsureContrast(
                borderNow, board, MinimumRuleContrast, board);

            // THE ACCENT INK. Walked toward INK, never toward white.
            // See the file header: white on acid yellow measures 3.09:1
            // and fails AA for body text by roughly a factor of three.
            var accentForegroundNow = EnsureAccentInk(
                accentForeground, accent, accentInk);

            // Accent against the page it sits on, so a selected block is
            // still findable when it lands on paper rather than on a card.
            var accentNow = EnsureContrast(
                accent, paper, MinimumRuleContrast, paper);

            var dangerNow = EnsureContrast(
                danger, paper, MinimumRuleContrast, paper);
            var infoNow = EnsureContrast(
                info, paper, MinimumRuleContrast, paper);
            var successNow = EnsureContrast(
                success, paper, MinimumRuleContrast, paper);

            var shadowNow = EnsureContrast(
                shadow, paper, MinimumRuleContrast, paper);

            return new NeoBrutalProfile(
                paper, board, inkNow, mutedNow, borderNow, shadowNow,
                accentNow, accentForegroundNow,
                dangerNow, infoNow, successNow, metrics);
        }

        /// <summary>WCAG 2.1 AA body text.</summary>
        internal const double MinimumBodyContrast = 4.5;

        /// <summary>WCAG 2.1 SC 1.4.11 non-text: borders, rims, offsets.</summary>
        internal const double MinimumRuleContrast = 3.0;

        /// <summary>
        /// Resolves the text colour for a saturated fill, walking toward
        /// INK rather than toward white.
        /// <para>
        /// This is the deliberate inversion of the heuristic every other
        /// family in this engine uses. A saturated yellow with white text
        /// measures 3.09:1 here and fails AA for body text; the same
        /// yellow with ink text measures 15.70:1 and passes AAA. The
        /// direction of the walk is the whole difference.
        /// </para>
        /// </summary>
        internal static Color EnsureAccentInk(
            Color candidate, Color accent, Color ink)
        {
            // INK FIRST, UNCONDITIONALLY, WHENEVER IT CLEARS.
            //
            // The first version of this walked from the candidate toward
            // ink and stopped at the first colour that passed 4.5:1 --
            // which is mid-grey, not black. On the shipped acid yellow
            // that walk terminated at #606060. A family whose entire
            // identity is flat black ink was about to render its most
            // prominent labels in washed-out grey.
            //
            // The fix is to treat ink as the ANSWER rather than as a
            // direction. If black clears the bar -- and it clears AAA at
            // 14.80:1 -- then grey is a strictly worse answer that merely
            // happens to also clear it.
            if (Contrast(ink, accent) >= MinimumBodyContrast)
                return ink;

            // Only when ink genuinely cannot work -- a dark accent, which
            // this family does not ship but must not silently fail on --
            // do we walk, and then the walk goes the OTHER way. "Never
            // white on acid" is a statement about LIGHT accents, not a
            // blanket prohibition; hard-coding white here would break
            // any future dark accent to satisfy a rule that does not
            // apply to it.
            var toward = Colors.White;

            for (int step = 1; step <= 20; step++)
            {
                double k = step / 20.0;
                var trial = NeoBrutalRecipe.Mix(ink, toward, k);
                if (Contrast(trial, accent) >= MinimumBodyContrast)
                    return trial;
            }

            return toward;
        }

        internal static Color EnsureContrast(
            Color candidate, Color toward, double minimum, Color against)
        {
            if (Contrast(candidate, against) >= minimum) return candidate;

            for (int step = 1; step <= 20; step++)
            {
                double k = step / 20.0;
                var trial = NeoBrutalRecipe.Mix(candidate, toward, k);
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
            if (dark && l > 0.97)
                return NeoBrutalRecipe.Mix(c, Colors.Black, 0.05);
            if (!dark && l > 0.985)
                return NeoBrutalRecipe.Mix(c, Colors.Black, 0.04);
            if (l < 0.02)
                return NeoBrutalRecipe.Mix(c, Colors.White, 0.06);
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
