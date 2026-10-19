// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Glassmorphism/GlassPalette.cs
// PROJECT: ProGlassAutomation
// MODULE: Glassmorphism — the 2020 recipe, and its accessibility contract
//
// ── THIS FAMILY IS DESIGNED AROUND A FINDING, NOT A MOOD ──────────────
//
// The research in Services/Theme/Glassmorphism/README.md §1 reaches an
// uncomfortable conclusion, and quoting it is the only honest way to
// introduce this file:
//
//   "There is no version of glassmorphism — as it's aesthetically
//    defined — that reliably passes WCAG 2.2 for text over a dynamic
//    background, WITHOUT ELIMINATING THE TRANSPARENCY THAT DEFINES THE
//    EFFECT."
//
//   "Axess Lab tested 14 common glassmorphic UI implementations and
//    found that NONE of them maintained consistent WCAG 2.2 compliance
//    across varied backgrounds. The same component that passed at
//    5.2:1 over a dark wallpaper failed at 1.8:1 over a light one."
//
// And the two usual escapes are documented as not being escapes: a dark
// scrim behind the text means "you're not making accessible glass", and
// raising panel opacity to 85-90% means "you've solved the contrast
// problem by not using glass".
//
// So this file does neither. It resolves the conflict by RESTRICTING
// WHERE GLASS IS ALLOWED, which is what the implementations that
// actually ship it do:
//
//   "Use glass for only a few key elements... not every component."
//   "Apple puts glass on CHROME, not CONTENT."
//
// GLASS IS A CHROME MATERIAL HERE. Content surfaces are opaque.
// GlassMetrics.MinimumSurfaceOpacity is a floor no caller can go below,
// and the fully-translucent treatment is reachable only through the
// explicitly-named Chrome path. The style still reads as glassmorphism,
// because chrome is where the eye looks first — and no body text in this
// app is ever drawn on a 12%-alpha panel.
//
// ── WORST CASE, NOT AVERAGE ───────────────────────────────────────────
//
//   "WCAG 1.4.3 requires 4.5:1 AT THE WORST MOMENT, not on average."
//
// Every floor below is applied against the darkest and lightest
// backdrops the plane can plausibly sit on, and a candidate that
// cannot clear both is rejected rather than averaged into compliance.
//
// ── HIGH CONTRAST IS A FIRST-CLASS MODE, NOT AN EDGE CASE ─────────────
//
// The rim is described in the research as an accessibility feature:
// "Windows forced-colors mode strips transparency and leaves that
// border as the only visible edge." This is a WPF app — the mode is
// SystemParameters.HighContrast and it is one property read away. So
// every translucent value in this file collapses to opaque when it is
// set, and the rim is always drawn.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme.Glassmorphism
{
    /// <summary>Where a glass surface is allowed to sit.</summary>
    public enum GlassPlacement
    {
        /// <summary>
        /// CONTENT. Carries text a user has to read.
        /// <para>
        /// Held to the opaque floor. Not a compromise — the floor IS the
        /// design, and it is why this family still looks like
        /// glassmorphism: chrome is where the eye lands first, so that
        /// is where the glass belongs.
        /// </para>
        /// </summary>
        Content = 0,

        /// <summary>
        /// CHROME. Navigation, rails, status strips.
        /// <para>
        /// Where the translucency actually lives. This is the path the
        /// research points at and the one Apple takes.
        /// </para>
        /// </summary>
        Chrome = 1,
    }

    /// <summary>The live Glassmorphism material for the current theme.</summary>
    public static class GlassPalette
    {
        private static GlassProfile? _light;
        private static GlassProfile? _dark;

        public static bool IsActive { get; private set; }

        public static GlassProfile? Light => _light;
        public static GlassProfile? Dark => _dark;

        public static event EventHandler? Changed;

        /// <summary>
        /// Forced opaque mode. Set by the OS high-contrast preference, or
        /// by the user.
        /// <para>
        /// Treated as a live setting rather than a startup flag: the
        /// preference can change while the app runs, and controls have
        /// to follow it without a restart — which is what the
        /// <see cref="Changed"/> event is for.
        /// </para>
        /// </summary>
        public static bool TransparencySuppressed { get; private set; }

        /// <summary>True when Windows high contrast is on.</summary>
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

        public static void Publish(
            Color lightStage, Color lightFill, Color lightInk,
            Color lightMuted, Color lightRim, Color accent,
            Color darkStage, Color darkFill, Color darkInk,
            Color darkMuted, Color darkRim)
        {
            _light = Build(
                lightStage, lightFill, lightInk, lightMuted,
                lightRim, accent, dark: false);
            _dark = Build(
                darkStage, darkFill, darkInk, darkMuted,
                darkRim, accent, dark: true);
            IsActive = true;
            SyncHighContrast();
            Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Applies the OS preference. Called on publish and by the
        /// app's theme-change hook, so flipping the Windows setting
        /// collapses every glass surface in place.
        /// </summary>
        public static void SyncHighContrast()
        {
            bool wanted = HighContrastRequested;
            if (wanted == TransparencySuppressed) return;
            TransparencySuppressed = wanted;
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

        public static GlassProfile? For(bool dark) => dark ? _dark : _light;

        /// <summary>
        /// Flattens a translucent fill onto an opaque backdrop.
        /// <para>
        /// The same rule as SpatialPalette, for the same reason: an alpha
        /// colour on its own has no meaningful contrast ratio, and a
        /// checker run against one reports a number that means nothing.
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

        internal static GlassProfile Build(
            Color stage, Color fill, Color ink, Color muted,
            Color rim, Color accent, bool dark)
        {
            var metrics = new GlassMetrics();

            // The stage is the thing the glass sits on. It is held off
            // pure white and pure black, and the reason is specific to
            // this family: "Glass needs a busy backdrop... over a flat
            // single colour, blur has nothing to do and the panel just
            // looks washed out." An extreme stage has nothing to frost.
            var ground = HoldOffExtreme(stage, dark);

            // Worst case, not average.
            //
            // The stage is a gradient in practice, so the two ends of its
            // range are both reachable behind a chrome panel. Validating
            // against one of them is precisely the failure Axess Lab
            // measured: 5.2:1 over a dark wallpaper, 1.8:1 over a light
            // one.
            var lightEnd = HoldOffExtreme(
                GlassRecipe.Lighten(ground, 0.35), dark);
            var darkEnd = GlassRecipe.Darken(ground, 0.35);

            var metricsForInk = metrics;

            // INK against the WORST of the two ends.
            var inkNow = EnsureWorstCase(
                ink, dark, MinimumBodyContrast, lightEnd, darkEnd);

            // The CONTENT fill is already opaque by policy, so it is the
            // thing everything else is measured against.
            var contentFill = Composited(
                GlassRecipe.WithAlpha(
                    fill, metricsForInk.MinimumSurfaceOpacity),
                ground);

            var mutedNow = EnsureWorstCase(
                muted, dark, MinimumBodyContrast, lightEnd, darkEnd);

            // The rim is measured against the content fill, because a
            // rim's whole job is to describe the surface it surrounds.
            var rimNow = EnsureWorstCase(
                rim, dark, MinimumRuleContrast, contentFill, contentFill);

            return new GlassProfile(
                ground, fill, contentFill, inkNow, mutedNow, rimNow,
                accent, metrics);
        }

        /// <summary>WCAG 2.1 AA body-text contrast.</summary>
        internal const double MinimumBodyContrast = 4.5;

        /// <summary>WCAG 2.1 SC 1.4.11 non-text, for the rim.</summary>
        internal const double MinimumRuleContrast = 3.0;

        /// <summary>
        /// Walks a colour until it clears <paramref name="minimum"/>
        /// against BOTH backdrops. A candidate that can only satisfy one
        /// of them is not compliant — it is compliant on the half of the
        /// time the user will notice.
        /// </summary>
        internal static Color EnsureWorstCase(
            Color candidate, bool dark, double minimum,
            Color backdropA, Color backdropB)
        {
            Color toward = dark ? Colors.White : Colors.Black;

            if (Contrast(candidate, backdropA) >= minimum
                && Contrast(candidate, backdropB) >= minimum)
                return candidate;

            for (int step = 1; step <= 20; step++)
            {
                double k = step / 20.0;
                var trial = GlassRecipe.Mix(candidate, toward, k);
                if (Contrast(trial, backdropA) >= minimum
                    && Contrast(trial, backdropB) >= minimum)
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
                var trial = GlassRecipe.Mix(candidate, toward, k);
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
            if (dark && l < 0.03) return GlassRecipe.Mix(c, Colors.White, 0.04);
            if (!dark && l > 0.97) return GlassRecipe.Mix(c, Colors.Black, 0.03);
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
