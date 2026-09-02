// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/ThemeStudioService.cs
// PROJECT: ProGlassAutomation
// MODULE: Theme Studio — live application of 0–100 appearance settings
//
// HOW IT WORKS (safe layering over VisualThemeEngine):
//   1. VisualThemeEngine.Apply / ApplyModule paint the family's real
//      brushes into Application.Resources (unchanged — the six themes
//      keep their identity).
//   2. ThemeStudioService.OnFamilyApplied() SNAPSHOTS those instances
//      ("base values") right after the family paints, then re-applies
//      the user's saved 0–100 profile on top.
//   3. Apply() CLONES the snapshot brushes, applies parametric
//      transforms (alpha scaling, tint blend, shadow parameters) and
//      writes fresh instances under the SAME keys. Every XAML in the
//      app binds those keys through DynamicResource, so the whole UI
//      retints live while a slider moves — no view recreation, no
//      restart.
//   4. Persistence is the VM's job (debounced ConfigService.Save) —
//      this service never writes disk.
//
// MAPPINGS (0–100 → WPF):
//   Frostness        alpha floor + milky boost on glass/surface keys
//   Transparency     alpha scale 1.0 → 0.45 on surface keys (never
//                    below the readability floor — see ReadableFloor)
//   BlurIntensity    frost-density boost on the glass-key group (WPF
//                    backgrounds are alpha-composited, not GPU-blurred)
//   GlassTint        blend of the glass group toward AccentColor
//   CornerRoundness  0–100 → 0–28 DIP (engine clamp, pill at 100)
//   CardPadding      DensityCardPadding scaled 0.75×–1.25× (the token
//                    the card styles actually consume)
//   ShadowIntensity  DropShadowEffect.Opacity (base × 0.35–1.7)
//   ShadowSoftness   DropShadowEffect.BlurRadius (2 → 64)
//   ShadowDepth      DropShadowEffect.ShadowDepth (base × 0.2–1.5)
//   BorderVisibility Border alpha multiplier (0.12 → 1.0)
//   BorderOpacity    Border alpha multiplier (0.2 → 1.0)
//   BorderThickness  0–100 → 0–4 DIP (ThemeManager.ApplyCardBorderThickness)
//   BackgroundVisibility wallpaper/window opacity (0.15 → 1.0)
//   BackgroundGlow    glow-key alpha (0.2 → 1.0)
//   HighlightStrength sheen-key alpha (0.15 → 1.0)
//   AnimationIntensity MotionDurationFast/Norm/Slow scaled 0.5×–1.5×
//                    (consumed by the GodViews / SectionActivityView)
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Settings;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>
    /// Applies ThemeStudioSettings to the live application resources.
    /// Static, idempotent, and layered strictly ON TOP of the existing
    /// VisualThemeEngine output — removing it leaves the original
    /// family visuals untouched.
    /// </summary>
    public static class ThemeStudioService
    {
        /// <summary>Currently active profile (null = no customization).</summary>
        public static ThemeStudioSettings? Active { get; private set; }

        private static readonly Dictionary<string, object> _base = new();
        private static bool _snapshotTaken;

        // ── Managed key groups ────────────────────────────────────────

        /// <summary>Surface keys — frost + transparency apply here.</summary>
        private static readonly string[] SurfaceKeys =
        {
            "ModuleBackgroundBrush", "ElevatedSurfaceBrush",
            "SideMenuBackgroundBrush", "MenuHoverBrush",
            "SidebarCardBrush", "ShellLiquidBrush",
            "DashLiquidSurfaceBrush",
            "GlassSurfaceBrush", "GlassSurfaceElevated",
            "GlassSurfaceUltra", "GlassSurfaceStandard",
            "GlassCrystalBrush", "GlassWaterBrush", "GlassFrostedBrush",
            "LiquidCard",
        };

        /// <summary>Glass keys — frost, blur boost AND tint apply here.</summary>
        private static readonly string[] GlassKeys =
        {
            "LiquidGlassBrush", "LiquidGlassSubtleBrush",
            "LiquidGlassStrongBrush", "GlassUltraClearBrush",
        };

        /// <summary>Ambient blob keys — transparency + glow apply here.</summary>
        private static readonly string[] AmbientKeys =
        {
            "DashAmbientBrush", "DashAmbientBlob1Brush",
            "DashAmbientBlob2Brush",
        };

        /// <summary>Glow keys (BackgroundGlow).</summary>
        private static readonly string[] GlowKeys =
        {
            "LiquidInnerHighlightBrush", "GlassInnerHighlight",
        };

        /// <summary>Top-sheen keys (HighlightStrength).</summary>
        private static readonly string[] HighlightKeys =
        {
            "ShellSheenBrush", "LiquidReflectionBrush",
        };

        /// <summary>Border keys (BorderVisibility + BorderOpacity).</summary>
        private static readonly string[] BorderKeys =
        {
            "BorderBrush", "OutlineBrush", "GlassBorderSoft",
            "GlassBorderGradientSoft", "GlassBorderGradientCrystal",
        };

        /// <summary>Shadow keys (Intensity / Softness / Depth).</summary>
        private static readonly string[] ShadowKeys =
        {
            "ShadowSoft", "ShadowElevated",
            "GlassShadowSoft", "GlassShadowSoftBorder",
            "GlassShadowSoftButton", "GlassShadowSoftCheckBox",
            "GlassShadowSoftTextBox", "GlassShadowSoftToggle",
        };

        /// <summary>Wallpaper/window keys (BackgroundVisibility).</summary>
        private static readonly string[] BackgroundKeys =
        {
            "WindowBackgroundBrush", "ModuleWallpaperBrush",
        };

        /// <summary>Animation keys (AnimationIntensity) — consumed by
        /// the GodViews and SectionActivityView via DynamicResource.</summary>
        private static readonly string[] DurationKeys =
        {
            "MotionDurationFast", "MotionDurationNorm",
            "MotionDurationSlow",
        };

        /// <summary>
        /// Restores the saved profile after startup family apply.
        /// Called from App.StartupCore once the theme chain has run.
        /// </summary>
        public static void Restore()
        {
            var saved = App.Config?.Appearance?.Studio;
            if (saved != null && saved.IsCustomized != false)
            {
                Snapshot();
                Apply(saved);
            }
        }

        /// <summary>
        /// Called by AppearanceService after every VisualThemeEngine
        /// Apply / ApplyModule: re-snapshots the family's fresh values
        /// and re-applies the active profile so a family switch never
        /// drops the user's customization.
        /// </summary>
        public static void OnFamilyApplied()
        {
            Snapshot();
            if (Active != null)
                Apply(Active);
        }

        /// <summary>
        /// Applies a profile live. Idempotent; safe to call on every
        /// slider tick (clones + fresh instances only — no disk IO).
        /// </summary>
        public static void Apply(ThemeStudioSettings s)
        {
            if (s == null) return;
            Active = s;
            var resources = Application.Current?.Resources;
            if (resources == null) return;
            if (!_snapshotTaken) Snapshot();

            // Theme Studio writes glass brushes on TOP of the family the
            // engine painted. The next ApplyModule must therefore repaint
            // for real rather than be skipped as an identical scene
            // (splash colour-bounce de-duplication, 2026-09-02).
            VisualThemeEngine.InvalidateSceneCache();

            double frost = Clamp01(s.Frostness);
            double transp = Clamp01(s.Transparency);
            double blur = Clamp01(s.BlurIntensity);
            double tint = Clamp01(s.GlassTint);

            // Readability floor (master prompt §19): transparency can
            // never drop surface alpha below this; dense frost raises
            // the floor further so text keeps contrast.
            double floor = 0.38 + 0.25 * frost;
            double tScale = 1.0 - 0.55 * transp;
            double surfScale = Math.Max(tScale, floor);

            // Frost = milky alpha boost on top of the transparency scale.
            double frostBoost = 0.12 * frost;
            // Blur maps to frost density (WPF has no background blur).
            double blurBoost = 0.10 * blur;

            var accent = GetAccentColor();

            foreach (var key in SurfaceKeys)
                WriteScaledBrush(resources, key, surfScale + frostBoost);
            foreach (var key in GlassKeys)
                WriteScaledBrush(resources, key,
                    Math.Min(1.0, surfScale + frostBoost + blurBoost), tint, accent);
            foreach (var key in AmbientKeys)
                WriteScaledBrush(resources, key, Math.Max(0.25, surfScale));

            // Background visibility — wallpaper / window stage.
            double bgScale = 0.15 + 0.85 * Clamp01(s.BackgroundVisibility);
            foreach (var key in BackgroundKeys)
                WriteScaledBrush(resources, key, bgScale);

            // Glow / highlight.
            double glowScale = 0.20 + 0.80 * Clamp01(s.BackgroundGlow);
            foreach (var key in GlowKeys)
                WriteScaledBrush(resources, key, glowScale);
            double hlScale = 0.15 + 0.85 * Clamp01(s.HighlightStrength);
            foreach (var key in HighlightKeys)
                WriteScaledBrush(resources, key, hlScale);

            // Borders — visibility × opacity, then thickness (0–100 →
            // 0–4 DIP, same range as the classic Appearance slider so
            // both controls can never disagree about "no border").
            double borderScale =
                (0.12 + 0.88 * Clamp01(s.BorderVisibility)) *
                (0.20 + 0.80 * Clamp01(s.BorderOpacity));
            foreach (var key in BorderKeys)
                WriteScaledBrush(resources, key, borderScale);
            ThemeManager.ApplyCardBorderThickness(4.0 * Clamp01(s.BorderThickness));

            // Corner roundness — 0–100 → 0–28 DIP (28 is the engine's
            // clamp; 100 ≈ pill on cards).
            ThemeManager.ApplyCardCornerRadius(28.0 * Clamp01(s.CornerRoundness));

            // Shadows — clone each effect, remap parameters.
            double shIntensity = Clamp01(s.ShadowIntensity);
            double shSoftness = Clamp01(s.ShadowSoftness);
            double shDepth = Clamp01(s.ShadowDepth);
            foreach (var key in ShadowKeys)
                WriteShadow(resources, key, shIntensity, shSoftness, shDepth);

            // Padding — scale the density-token the card styles actually
            // consume (DesignTokens binds DensityCardPadding). The base
            // is re-snapshotted on every Apply so a density change keeps
            // working underneath the studio layer.
            double padScale = 0.75 + 0.50 * Clamp01(s.CardPadding);
            WriteScaledThickness(resources, "DensityCardPadding", padScale);

            // Animation intensity — scale the MotionDuration* tokens
            // consumed by the GodViews / SectionActivityView.
            double animScale = 0.5 + 1.0 * Clamp01(s.AnimationIntensity);
            foreach (var key in DurationKeys)
                WriteScaledDuration(resources, key, animScale);
        }

        /// <summary>Resets the ACTIVE profile to its family's defaults.</summary>
        public static ThemeStudioSettings ResetToFamilyDefaults()
        {
            var d = ThemeStudioSettings.DefaultsFor(
                VisualThemeEngine.Current ?? "Normal");
            Apply(d);
            return d;
        }

        /// <summary>Drops all customization (family defaults everywhere).</summary>
        public static void ResetAll()
        {
            Active = null;
            var resources = Application.Current?.Resources;
            if (resources == null) return;
            _base.Clear();
            _snapshotTaken = false;

            // Restore the family's original values: re-run the engine
            // chain exactly like a family switch, then drop the profile.
            AppearanceService.ApplyTheme(
                ThemeManager.CurrentTheme == AppTheme.Dark,
                correlationId: null);
        }

        // ── Internals ─────────────────────────────────────────────────

        /// <summary>Captures the family's freshly painted instances.</summary>
        private static void Snapshot()
        {
            _base.Clear();
            var resources = Application.Current?.Resources;
            if (resources == null) return;
            foreach (var key in AllKeys)
            {
                if (resources[key] != null)
                    _base[key] = resources[key];
            }
            _snapshotTaken = true;
        }

        private static IEnumerable<string> AllKeys
        {
            get
            {
                foreach (var k in SurfaceKeys) yield return k;
                foreach (var k in GlassKeys) yield return k;
                foreach (var k in AmbientKeys) yield return k;
                foreach (var k in GlowKeys) yield return k;
                foreach (var k in HighlightKeys) yield return k;
                foreach (var k in BorderKeys) yield return k;
                foreach (var k in ShadowKeys) yield return k;
                foreach (var k in BackgroundKeys) yield return k;
                yield return "DensityCardPadding";
                foreach (var k in DurationKeys) yield return k;
            }
        }

        private static void WriteScaledBrush(
            ResourceDictionary r, string key, double scale)
            => WriteScaledBrush(r, key, scale, 0.0, default);

        private static void WriteScaledBrush(
            ResourceDictionary r, string key, double scale,
            double tintStrength, Color accent)
        {
            if (!_base.TryGetValue(key, out var baseVal)) return;
            if (baseVal is not Brush baseBrush) return;
            var clone = baseBrush.Clone();
            clone.Opacity = Math.Min(1.0, Math.Max(0.0, scale));

            if (tintStrength > 0.01 && accent != default)
            {
                // Tint: blend solid colors toward the accent; gradients
                // get their dominant stop tinted.
                if (clone is SolidColorBrush solid)
                    solid.Color = Blend(solid.Color, accent, tintStrength);
                else if (clone is GradientBrush grad)
                {
                    foreach (var stop in grad.GradientStops)
                        stop.Color = Blend(stop.Color, accent, tintStrength * 0.6);
                }
            }
            r[key] = clone;
        }

        private static void WriteScaledThickness(
            ResourceDictionary r, string key, double scale)
        {
            if (!_base.TryGetValue(key, out var baseVal)) return;
            if (baseVal is not Thickness baseT) return;
            r[key] = new Thickness(
                baseT.Left * scale, baseT.Top * scale,
                baseT.Right * scale, baseT.Bottom * scale);
        }

        private static void WriteScaledDuration(
            ResourceDictionary r, string key, double scale)
        {
            if (!_base.TryGetValue(key, out var baseVal)) return;
            if (baseVal is not Duration baseD) return;
            var ms = Math.Max(1.0, baseD.TimeSpan.TotalMilliseconds * scale);
            r[key] = new Duration(TimeSpan.FromMilliseconds(ms));
        }

        private static void WriteShadow(
            ResourceDictionary r, string key,
            double intensity, double softness, double depth)
        {
            if (!_base.TryGetValue(key, out var baseVal)) return;
            if (baseVal is not DropShadowEffect baseFx) return;

            var fx = (DropShadowEffect)baseFx.Clone();
            fx.Opacity = Math.Min(1.0, baseFx.Opacity * (0.35 + 1.35 * intensity));
            fx.BlurRadius = 2.0 + 62.0 * softness;
            fx.ShadowDepth = baseFx.ShadowDepth * (0.2 + 1.3 * depth);
            r[key] = fx;
        }

        private static Color Blend(Color c, Color accent, double t)
        {
            byte R = (byte)(c.R + (accent.R - c.R) * t);
            byte G = (byte)(c.G + (accent.G - c.G) * t);
            byte B = (byte)(c.B + (accent.B - c.B) * t);
            return Color.FromArgb(c.A, R, G, B);
        }

        private static Color GetAccentColor()
        {
            var resources = Application.Current?.Resources;
            if (resources == null) return default;
            if (resources["AccentColor"] is Color c) return c;
            if (resources["AccentBrush"] is SolidColorBrush b) return b.Color;
            return default;
        }

        private static double Clamp01(double v)
        {
            if (v < 0) return 0;
            if (v > 100) return 1;
            return v / 100.0;
        }
    }
}
