// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Aurora/AuroraPalette.cs
// PROJECT: ProGlassAutomation
// MODULE: Aurora — the live sky, and its contrast contract
//
// ── WORST CASE MEANS THE BRIGHTEST BLOB, NOT THE BASE ─────────────────
//
// Every other family validates ink against a colour that is, more or
// less, fixed behind it. Aurora cannot. The backdrop is a live gradient
// and it is brighter in one place than another, and it is brighter at the
// centre of the indigo blob than the base colour ever is.
//
// "WCAG 1.4.3 requires 4.5:1 AT THE WORST MOMENT, not on average."
//
// So the worst moment here is computed, not assumed: Build() composites
// the panel fill over the SKY PEAK (base + the anchor blob at its core
// strength) and over the SKY TROUGH (bare base), and holds ink to 4.5:1
// against BOTH. A design that passes against the average sky and fails
// against the blob is the failure, not the design.
//
// ── WHY THERE IS NO LIGHT MODE, ONLY A DIMMER ONE ─────────────────────
//
// "The dark void is the required base" and "aurora over white dies".
// This family therefore does not invert for light mode -- there is no
// light mode to invert into.
//
// AuroraProfile.Light exists ONLY because EightThemePainter's switch is
// global and a profile pair is a structural requirement. It is a violet
// cast of the same void, slightly lifted so the two are distinguishable
// on screen. Ink stays near-white in BOTH profiles, which is correct
// rather than lazy: the base is dark in both, so dark ink would fail in
// both.
//
// That is a deliberate contract violation of the light/dark convention
// the other seven families follow, and it is the honest consequence of
// the research. Flagging it here so it is not mistaken for a bug later.
using System;
using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Services.Theme.Aurora
{
    /// <summary>The live Aurora sky for the current theme.</summary>
    public static class AuroraPalette
    {
        private static AuroraProfile? _light;
        private static AuroraProfile? _dark;

        public static bool IsActive { get; private set; }

        public static AuroraProfile? Light => _light;
        public static AuroraProfile? Dark => _dark;

        public static event EventHandler? Changed;

        /// <summary>
        /// Forced opaque mode. Set by OS high contrast or by the user.
        /// <para>
        /// Live rather than a startup flag: the preference can change
        /// while the app runs, and every panel has to follow it without
        /// a restart. That is what <see cref="Changed"/> is for.
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

        public static void SyncHighContrast()
        {
            bool wanted = HighContrastRequested;
            if (wanted == TransparencySuppressed) return;
            TransparencySuppressed = wanted;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void Publish(
            Color lightBase, Color lightBlobIndigo, Color lightBlobViolet,
            Color lightBlobTeal, Color lightInk, Color lightMuted,
            Color lightRimLow, Color lightRimHigh, Color lightAccent,
            Color lightGlow,
            Color darkBase, Color darkBlobIndigo, Color darkBlobViolet,
            Color darkBlobTeal, Color darkInk, Color darkMuted,
            Color darkRimLow, Color darkRimHigh, Color darkAccent,
            Color darkGlow)
        {
            _light = Build(
                lightBase, lightBlobIndigo, lightBlobViolet, lightBlobTeal,
                lightInk, lightMuted, lightRimLow, lightRimHigh,
                lightAccent, Colors.White, lightGlow, dark: false);

            _dark = Build(
                darkBase, darkBlobIndigo, darkBlobViolet, darkBlobTeal,
                darkInk, darkMuted, darkRimLow, darkRimHigh,
                darkAccent, Colors.White, darkGlow, dark: true);

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

        public static AuroraProfile? For(bool dark) => dark ? _dark : _light;

        /// <summary>
        /// Flattens a translucent fill onto an opaque backdrop.
        /// <para>
        /// Same reason as GlassPalette and SpatialPalette: an alpha colour
        /// on its own has no meaningful contrast ratio, and a checker run
        /// against one reports a number that means nothing.
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

        internal static AuroraProfile Build(
            Color baseColor,
            Color blobIndigo, Color blobViolet, Color blobTeal,
            Color ink, Color muted,
            Color rimLow, Color rimHigh,
            Color accent, Color accentForeground,
            Color glow, bool dark)
        {
            var metrics = new AuroraMetrics();

            // The void is held off true black for the same reason every
            // family holds its stage off an extreme: there is nothing to
            // modulate, and the blobs read as smears on pure black.
            var ground = HoldOffExtreme(baseColor, dark);

            // THE SKY PEAK. The anchor blob composited over the void at
            // its core strength -- the brightest the backdrop ever becomes,
            // and therefore the worst backdrop any ink can land on.
            var skyPeak = Composited(
                AuroraRecipe.WithAlpha(blobIndigo, metrics.PanelOpacity),
                Composited(
                    AuroraRecipe.WithAlpha(blobIndigo, 0.62), ground));

            // The sky trough: the bare void, in its darkest corner.
            var skyTrough = ground;

            // ── INK, HELD TO BOTH ENDS ──────────────────────────────────
            //
            // A Panel composites the void at PanelOpacity, so the two
            // backdrops an ink can actually land on are the panel over the
            // peak and the panel over the trough.
            var panelFillAtRest = AuroraRecipe.WithAlpha(ground, metrics.PanelOpacity);

            var inkPeakBackdrop = Composited(panelFillAtRest, skyPeak);
            var inkTroughBackdrop = Composited(panelFillAtRest, skyTrough);

            var inkNow = EnsureWorstCase(
                ink, dark, MinimumBodyContrast, inkPeakBackdrop, inkTroughBackdrop);

            var mutedNow = EnsureWorstCase(
                muted, dark, MinimumBodyContrast, inkPeakBackdrop, inkTroughBackdrop);

            // The rim describes the PANEL, so it is measured against the
            // panel's own composited colour rather than the raw sky.
            var rimAgainst = Composited(panelFillAtRest, skyTrough);

            var rimLowNow = EnsureWorstCase(
                rimLow, dark, MinimumRuleContrast, rimAgainst, inkPeakBackdrop);
            var rimHighNow = EnsureWorstCase(
                rimHigh, dark, MinimumRuleContrast, rimAgainst, inkPeakBackdrop);

            // The accent carries white label text inside a primary button,
            // so it is held to body contrast against ITSELF.
            var accentNow = EnsureContrast(
                accent, dark ? Colors.White : Colors.Black,
                MinimumBodyContrast, accentForeground);

            var glowNow = EnsureContrast(
                glow, ground, MinimumRuleContrast, ground);

            return new AuroraProfile(
                ground, blobIndigo, blobViolet, blobTeal,
                inkNow, mutedNow, rimLowNow, rimHighNow,
                accentNow, accentForeground, glowNow, metrics);
        }

        /// <summary>WCAG 2.1 AA body-text contrast.</summary>
        internal const double MinimumBodyContrast = 4.5;

        /// <summary>WCAG 2.1 SC 1.4.11 non-text, for the rim and glow.</summary>
        internal const double MinimumRuleContrast = 3.0;

        /// <summary>
        /// Walks a colour toward white or black until it clears
        /// <paramref name="minimum"/> against BOTH backdrops. A candidate
        /// that satisfies only one is not compliant -- it is compliant on
        /// the half of the time the user will notice.
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
                var trial = AuroraRecipe.Mix(candidate, toward, k);
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
                var trial = AuroraRecipe.Mix(candidate, toward, k);
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
            if (dark && l < 0.004)
                return AuroraRecipe.Mix(c, Colors.White, 0.06);
            if (!dark && l < 0.004)
                return AuroraRecipe.Mix(c, Colors.White, 0.10);
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
