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
//   Frostness        milky alpha boost on glass/surface keys
//   Transparency     alpha scale 1.0 → 0.04 on surface keys — TRUE full
//                    range (2026-09-05): 100% really is near-invisible
//                    glass. Readability is owned by the text layer
//                    (TextStrength + the contrast guard), never again by
//                    an alpha floor that made 100% look like 60%.
//   ColorRichness    saturation ×0.45 (muted pastel) … ×1.85 (deep
//                    rich) + gentle depth lift on surfaces, glass,
//                    ambient, wallpaper scene and status tints. True
//                    greys (S<0.08) stay neutral — no muddy chrome.
//   TextStrength     ink blend toward stage-aware max-contrast text
//                    (bright ink on dark stages, deep ink on light
//                    stages). AUTOMATIC CONTRAST GUARD: when cards go
//                    near-invisible (effective surface alpha < 0.22) all
//                    text forces to max ink — "text outside the cards
//                    disappears" is impossible by construction.
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
using ProGlassAutomation.Services.Logging;
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

        /// <summary>Text colours — TextStrength + the automatic contrast
        /// guard (2026-09-05). These feed every typography style in the
        /// app (TypographyStyles / KPI / money values all bind them).</summary>
        private static readonly string[] TextKeysPrimary =
        {
            "TextPrimaryBrush", "PrimaryForegroundBrush",
        };

        private static readonly string[] TextKeysSecondary =
        {
            "TextSecondaryBrush", "SecondaryForegroundBrush",
            "MutedForegroundBrush",
        };

        /// <summary>Status hue tints — ColorRichness saturates these so
        /// dashboards/list chips join the rich system instead of staying
        /// pastel against rich surfaces.</summary>
        private static readonly string[] TintKeys =
        {
            "TintIndigoBrush", "TintBlueBrush", "TintGreenBrush",
            "TintRedBrush", "TintAmberBrush", "TintPurpleBrush",
            "TintGrayBrush",
        };

        /// <summary>Water-fill keys (2026-09-05) — the surfaces that
        /// receive the liquid-crystal gradient when WaterFlow > 0.
        /// They also belong to Surface/Glass groups (scaled first);
        /// the water pass runs AFTER those and rewrites them into
        /// layered water gradients.</summary>
        private static readonly string[] WaterKeys =
        {
            "GlassWaterBrush", "GlassCrystalBrush",
            "LiquidGlassBrush", "LiquidCard",
            "GlassSurfaceStandard",
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

        /// <summary>Restores the saved profile after startup family apply.
        /// Called from App.StartupCore once the theme chain has run.
        /// Guarded — a poisoned profile degrades to the family look at
        /// startup instead of crashing the boot chain.</summary>
        public static void Restore()
        {
            try
            {
                var saved = App.Config?.Appearance?.Studio;
                if (saved != null && saved.IsCustomized != false)
                {
                    Snapshot();
                    Apply(saved);
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Theme Studio restore skipped: {ex.Message}",
                    new LogContext { Module = "Themes", Operation = "StudioRestore" });
            }
        }

        /// <summary>
        /// Called by AppearanceService after every VisualThemeEngine
        /// Apply / ApplyModule: re-snapshots the family's fresh values
        /// and re-applies the active profile so a family switch never
        /// drops the user's customization.
        /// FULLY GUARDED (ERR-46877E, 2026-09-05): this method is called
        /// from the engine's finally block on EVERY navigation — an
        /// exception here used to propagate through relay commands into
        /// an unhandled UI crash. The studio layer must never be able to
        /// take down theme application, no matter what is in the user's
        /// saved profile.
        /// </summary>
        public static void OnFamilyApplied()
        {
            try
            {
                Snapshot();
                if (Active != null)
                    Apply(Active);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Theme Studio re-apply skipped: {ex.Message}",
                    new LogContext { Module = "Themes", Operation = "StudioApply" });
            }
        }

        /// <summary>Re-applies the active profile after an accent change
        /// so glass tint and the richness layer pick up the new accent
        /// immediately. Previously the tint kept the OLD accent until the
        /// next family switch or slider move (2026-09-05).</summary>
        public static void OnAccentChanged()
        {
            try
            {
                if (Active != null && _snapshotTaken)
                    Apply(Active);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Theme Studio accent re-apply skipped: {ex.Message}",
                    new LogContext { Module = "Themes", Operation = "StudioAccent" });
            }
        }

        /// <summary>Refreshes ONE snapshot key (2026-09-05 — Wallpaper
        /// Studio): the wallpaper orchestrator paints its scene AFTER the
        /// studio snapshot, so without this the next Apply() would scale
        /// the stale pre-overlay wallpaper. The orchestrator calls it
        /// whenever it overlays ModuleWallpaperBrush.</summary>
        public static void UpdateBase(string key, object value)
        {
            if (_snapshotTaken && value != null)
                _base[key] = value;
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
            double rich = Clamp01(s.ColorRichness);
            double text = Clamp01(s.TextStrength);

            // ── Transparency — TRUE full range (2026-09-05 master
            // request): 0 = fully solid, 100 = near-invisible glass.
            // The old 0.38–0.63 readability FLOOR is gone; readability
            // is guaranteed by the text layer + contrast guard below.
            double tScale = 1.0 - 0.96 * transp;
            double surfScale = Math.Max(tScale, 0.04 + 0.06 * frost);

            // Frost = milky alpha boost on top of the transparency scale.
            double frostBoost = 0.12 * frost;
            // Blur maps to frost density (WPF has no background blur).
            double blurBoost = 0.10 * blur;

            // ── Colour richness — saturation ×0.45 (muted pastel) …
            // ×1.85 (deep rich), with a gentle depth lift at the rich
            // end so saturated colours read DEEP, not neon. Neutrals
            // (S<0.08) are untouched — grey chrome never turns muddy.
            double satMul = 0.45 + 1.40 * rich;
            double deepLift = -0.10 * Math.Max(0.0, rich - 0.5) * 2.0;

            var accent = GetAccentColor();

            foreach (var key in SurfaceKeys)
                WriteScaledBrush(resources, key, surfScale + frostBoost, 0.0, default, satMul, deepLift);
            foreach (var key in GlassKeys)
                WriteScaledBrush(resources, key,
                    Math.Min(1.0, surfScale + frostBoost + blurBoost), tint, accent, satMul, deepLift);
            foreach (var key in AmbientKeys)
                WriteScaledBrush(resources, key, Math.Max(0.15, surfScale), 0.0, default, satMul, deepLift);
            foreach (var key in TintKeys)
                WriteScaledBrush(resources, key, 1.0, 0.0, default, satMul, deepLift);

            // Background visibility — wallpaper / window stage. Richness
            // also saturates the wallpaper SCENE (gradient stops) so the
            // per-theme dynamic wallpaper follows the same colour
            // system; a user-supplied image is only ever dimmed, never
            // recoloured.
            double bgScale = 0.15 + 0.85 * Clamp01(s.BackgroundVisibility);
            foreach (var key in BackgroundKeys)
                WriteScaledBrush(resources, key, bgScale, 0.0, default, satMul, deepLift);

            // Glow / highlight.
            double glowScale = 0.20 + 0.80 * Clamp01(s.BackgroundGlow);
            foreach (var key in GlowKeys)
                WriteScaledBrush(resources, key, glowScale);
            double hlScale = 0.15 + 0.85 * Clamp01(s.HighlightStrength);
            foreach (var key in HighlightKeys)
                WriteScaledBrush(resources, key, hlScale);

            // ── WATER-FILL GLASS (2026-09-05 master request): cards
            // become liquid crystal — top specular sheen, crystal body,
            // a thin waterline glint, and colour that SETTLES toward the
            // bottom like filled water. Pure layered-gradient illusion;
            // WPF has no backdrop blur, so depth is painted, not blurred.
            double water = Clamp01(s.WaterFlow);
            if (water > 0.01)
            {
                foreach (var key in WaterKeys)
                {
                    bool isGlass = Array.IndexOf(GlassKeys, key) >= 0;
                    double scale = isGlass
                        ? Math.Min(1.0, surfScale + frostBoost + blurBoost)
                        : surfScale + frostBoost;
                    WriteWaterBrush(resources, key, scale,
                        isGlass ? tint : 0.0, accent, water, satMul, deepLift);
                }
            }

            // ── TEXT LAYER (2026-09-05). The complaint: "outside-card
            // text is invisible in transparent themes". The five text
            // keys (which feed EVERY typography style — page titles,
            // section headers, KPI/money values, hints) now belong to
            // this pass:
            //   55  = the family's designed ink, untouched
            //   100 = stage-aware maximum-contrast ink
            //   0   = deliberately softened (pulled toward the stage)
            // CONTRAST GUARD: with cards near-invisible (surfScale <
            // 0.22) ink forces to maximum regardless of the slider —
            // extreme transparency keeps full readability.
            double stageLum = StageLuminance(resources);
            bool darkStage = stageLum < 0.42;
            Color primaryInk = darkStage
                ? Color.FromRgb(0xF5, 0xF8, 0xFF)
                : Color.FromRgb(0x0A, 0x11, 0x20);
            Color secondaryInk = darkStage
                ? Color.FromRgb(0xC9, 0xD4, 0xE8)
                : Color.FromRgb(0x2E, 0x3A, 0x4D);

            bool guard = surfScale < 0.22;
            double up = guard ? 1.0 : Math.Max(0.0, (text - 0.55) / 0.45);
            double down = guard ? 0.0 : Math.Max(0.0, (0.55 - text) / 0.55);

            foreach (var key in TextKeysPrimary)
                WriteTextBrush(resources, key, up, down, primaryInk, darkStage);
            foreach (var key in TextKeysSecondary)
                WriteTextBrush(resources, key, up, down, secondaryInk, darkStage);

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
                foreach (var k in TextKeysPrimary) yield return k;
                foreach (var k in TextKeysSecondary) yield return k;
                foreach (var k in TintKeys) yield return k;
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
            => WriteScaledBrush(r, key, scale, tintStrength, accent, 1.0, 0.0);

        private static void WriteScaledBrush(
            ResourceDictionary r, string key, double scale,
            double tintStrength, Color accent, double satMul, double deepLift)
        {
            try
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

            // Colour richness (2026-09-05): saturation/depth transform.
            // ImageBrush (user wallpaper) is deliberately NOT recoloured.
            if (Math.Abs(satMul - 1.0) > 0.01 || Math.Abs(deepLift) > 0.001)
            {
                if (clone is SolidColorBrush richSolid)
                    richSolid.Color = Enrich(richSolid.Color, satMul, deepLift);
                else if (clone is GradientBrush richGrad)
                {
                    foreach (var stop in richGrad.GradientStops)
                        stop.Color = Enrich(stop.Color, satMul, deepLift);
                }
            }
            r[key] = clone;
            }
            catch { /* one poisoned key must never abort the studio */ }
        }

        /// <summary>Richness transform (HSL): scales saturation but keeps
        /// true greys (S&lt;0.08) neutral, and applies the depth lift.</summary>
        private static Color Enrich(Color c, double satMul, double lift)
        {
            var (h, sat, lum) = ColorMath.ToHsl(c);
            if (sat > 0.08 && Math.Abs(satMul - 1.0) > 0.01)
                sat = Math.Clamp(sat * satMul, 0.0, 1.0);
            lum = Math.Clamp(lum + lift, 0.0, 1.0);
            return ColorMath.FromHsl(h, sat, lum, c.A);
        }

        /// <summary>Perceived luminance (0–1) of the LIVE window stage —
        /// samples the just-written WindowBackgroundBrush (gradient stops
        /// averaged) so ink choice reflects what is actually composited.</summary>
        private static double StageLuminance(ResourceDictionary r)
        {
            try
            {
                if (r["WindowBackgroundBrush"] is SolidColorBrush solid)
                    return ColorMath.RelativeLuminance(solid.Color);
                if (r["WindowBackgroundBrush"] is GradientBrush grad
                    && grad.GradientStops.Count > 0)
                {
                    double sum = 0;
                    foreach (var stop in grad.GradientStops)
                        sum += ColorMath.RelativeLuminance(stop.Color);
                    return (sum / grad.GradientStops.Count) * grad.Opacity;
                }
            }
            catch { /* fall through to the safe default */ }
            return 0.5; // unknown stage: treat as light-neutral
        }

        /// <summary>Writes one text key: blends the family's snapshot
        /// colour toward the stage-aware target ink (up) or toward the
        /// stage tone for a deliberately soft look (down).</summary>
        private static void WriteTextBrush(
            ResourceDictionary r, string key,
            double up, double down, Color targetInk, bool darkStage)
        {
            try
            {
            if (!_base.TryGetValue(key, out var baseVal)) return;
            if (baseVal is not SolidColorBrush baseBrush) return;

            Color c0 = baseBrush.Color;
            Color c;
            if (up > 0.001)
                c = ColorMath.Mix(c0, targetInk, up);
            else if (down > 0.001)
            {
                // Soft text = pull toward the stage tone (lower contrast
                // by intent — the user steered left of the family ink).
                Color stageTone = darkStage
                    ? Color.FromRgb(0x12, 0x1B, 0x2E)
                    : Color.FromRgb(0xF2, 0xF0, 0xEA);
                c = ColorMath.Mix(c0, ColorMath.Mix(c0, stageTone, 0.55), down * 0.7);
            }
            else
                return; // sits at the family design — leave the key alone

            // Fresh instance: DynamicResource subscribers only update on
            // dictionary replacement, never on a mutated shared brush.
            r[key] = new SolidColorBrush(Color.FromArgb(c0.A, c.R, c.G, c.B));
            }
            catch { /* one poisoned key must never abort the studio */ }
        }

        /// <summary>
        /// Builds the liquid-crystal "water fill" gradient for one
        /// surface key (2026-09-05). Five stops: wet top specular → airy
        /// crystal body → thin waterline glint → settled dense depth.
        /// The base colour comes from the family's snapshot (tinted by the
        /// accent for glass keys, enriched by Colour richness); alpha is
        /// baked per stop from the same scale maths the flat passes use,
        /// so Frost/Transparency keep full authority over the water.
        /// </summary>
        private static void WriteWaterBrush(
            ResourceDictionary r, string key, double scale,
            double tintStrength, Color accent, double water,
            double satMul, double deepLift)
        {
            try
            {
            if (!_base.TryGetValue(key, out var baseVal)) return;
            if (baseVal is not Brush baseBrush) return;

            Color c0 = DominantColor(baseBrush);
            if (tintStrength > 0.01 && accent != default)
                c0 = Blend(c0, accent, tintStrength);
            // Richness applies to the body colour, never to the white
            // specular streaks (they must stay pure light).
            c0 = Enrich(c0, satMul, deepLift);

            double clamped = Math.Clamp(scale, 0.04, 1.0);
            byte Body(double k) => (byte)Math.Clamp(c0.A * clamped * k, 0, 255);
            byte Spec(double a) => (byte)Math.Clamp(a * water, 0, 255);

            var deep = ColorMath.Mix(c0, Colors.Black, 0.10 + 0.22 * water);

            var b = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
            };
            // 1) top edge: wet specular light (the "just filled" look)
            b.GradientStops.Add(new GradientStop(
                Color.FromArgb(Spec(70), 255, 255, 255), 0.00));
            // 2) crystal body — airy, unmistakably glass
            b.GradientStops.Add(new GradientStop(
                Color.FromArgb(Body(0.50), c0.R, c0.G, c0.B), 0.10));
            b.GradientStops.Add(new GradientStop(
                Color.FromArgb(Body(0.72), c0.R, c0.G, c0.B), 0.55));
            // 3) waterline glint — the "surface of the water" running
            //    across the card at ~78% height
            b.GradientStops.Add(new GradientStop(
                Color.FromArgb(Spec(54), 255, 255, 255), 0.78));
            // 4) settled depth — denser coloured water at the floor
            b.GradientStops.Add(new GradientStop(
                Color.FromArgb(Body(1.0), deep.R, deep.G, deep.B), 1.00));
            r[key] = b;
            }
            catch { /* one poisoned key must never abort the studio */ }
        }

        /// <summary>Effective colour of any brush: solid colour, or the
        /// middle stop of a gradient; white as the safe fallback.</summary>
        private static Color DominantColor(Brush brush)
        {
            if (brush is SolidColorBrush solid) return solid.Color;
            if (brush is GradientBrush grad && grad.GradientStops.Count > 0)
                return grad.GradientStops[grad.GradientStops.Count / 2].Color;
            return Colors.White;
        }

        private static void WriteScaledThickness(
            ResourceDictionary r, string key, double scale)
        {
            try
            {
                if (!_base.TryGetValue(key, out var baseVal)) return;
                if (baseVal is not Thickness baseT) return;
                r[key] = new Thickness(
                    baseT.Left * scale, baseT.Top * scale,
                    baseT.Right * scale, baseT.Bottom * scale);
            }
            catch { /* one poisoned key must never abort the studio */ }
        }

        /// <summary>Scales one MotionDuration token. ERR-46877E RCA
        /// (2026-09-05): Duration.Forever IS TimeSpan-backed
        /// (TimeSpan.MaxValue) with HasTimeSpan == TRUE — a Forever-class
        /// value in the snapshot multiplied by the Animation slider
        /// overflowed IntervalFromDoubleTicks and crashed the app on
        /// navigation. Rules: no-timeSpan tokens untouchable, anything
        /// ≥ 1 minute is an animation TOKEN, not a motion duration
        /// (never scaled), and the result is clamped to a sane motion
        /// envelope. Fully guarded.</summary>
        private static void WriteScaledDuration(
            ResourceDictionary r, string key, double scale)
        {
            try
            {
                if (!_base.TryGetValue(key, out var baseVal)) return;
                if (baseVal is not Duration baseD || !baseD.HasTimeSpan) return;

                double baseMs = baseD.TimeSpan.TotalMilliseconds;
                if (double.IsNaN(baseMs) || double.IsInfinity(baseMs)) return;
                if (baseMs > 60_000.0) return; // Forever-class token — not a motion duration

                double ms = baseMs * scale;
                if (double.IsNaN(ms) || double.IsInfinity(ms)) return;
                ms = Math.Clamp(ms, 1.0, 60_000.0); // 1 ms … 1 minute envelope
                r[key] = new Duration(TimeSpan.FromMilliseconds(ms));
            }
            catch { /* a motion token must never crash theme application */ }
        }

        private static void WriteShadow(
            ResourceDictionary r, string key,
            double intensity, double softness, double depth)
        {
            try
            {
                if (!_base.TryGetValue(key, out var baseVal)) return;
                if (baseVal is not DropShadowEffect baseFx) return;

                var fx = (DropShadowEffect)baseFx.Clone();
                fx.Opacity = Math.Min(1.0, baseFx.Opacity * (0.35 + 1.35 * intensity));
                fx.BlurRadius = 2.0 + 62.0 * softness;
                fx.ShadowDepth = baseFx.ShadowDepth * (0.2 + 1.3 * depth);
                r[key] = fx;
            }
            catch { /* one poisoned key must never abort the studio */ }
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
