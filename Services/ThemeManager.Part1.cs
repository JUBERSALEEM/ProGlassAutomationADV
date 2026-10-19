// ═══════════════════════════════════════════════════════════════════════
// FILE: ThemeManager.Part1.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Theme;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ProGlassAutomation.Services
{
        public static partial class ThemeManager
    {
            private static AppTheme _currentTheme = AppTheme.Light;

        /// <summary>
        /// User-chosen accent. Null = follow the theme's built-in industrial cyan.
        /// Persisted by callers if desired (SettingsViewModel).
        /// </summary>
        private static Color? _customAccent;

        public static AppTheme CurrentTheme => _currentTheme;

        /// <summary>The accent colour currently in effect.</summary>
        public static Color AccentColor { get; private set; } =
            Color.FromRgb(0x03, 0x69, 0xA1);

        public static event EventHandler ThemeChanged;

        /// <summary>
        /// Raises the public ThemeChanged event after an external orchestrator
        /// (ThemePipeline) has completed the full theme/family/accent pass.
        /// Keeping this helper inside ThemeManager preserves event ownership
        /// while avoiding duplicate repaint work.
        /// </summary>
        public static void NotifyThemeChanged()
            => ThemeChanged?.Invoke(null, EventArgs.Empty);

        private static void InvalidateVisualThemeSceneCache()
        {
            try { Theme.VisualThemeEngine.InvalidateSceneCache(); }
            catch (Exception ex)
            {
                Logging.AppLogService.Debug(
                    "[ThemeManager] scene-cache invalidation failed: " + ex.Message);
            }
        }

        private static void ApplyThemePostProcessing(AppTheme theme)
        {
            PublishLiquidReference(theme == AppTheme.Dark);
            InvalidateVisualThemeSceneCache();
            Theme.VisualThemeEngine.ApplyModule(
                Theme.VisualThemeEngine.LastRequestedModule,
                App.Config?.Appearance);
            ApplyAccent(_customAccent, raiseEvent: false);
            ReapplyOutlinePreference();
            ReapplySemanticSurfaces();
            PublishProGlassDesignSystem();

            // 2026-09-16: a visual family (Monet / iOS 26 / Crystal Fluid /
            // Normal industrial) writes CardCornerRadius and Radius.* of its
            // own, and PublishAboutPalette has just refreshed the accent
            // derived About edge. Re-derive the About surface tokens here so
            // an ENGINE switch moves the About cards too — before this, the
            // engine changed keys no About view reads, and About read keys no
            // engine changes.
            AboutSurfaceTokens.Publish(Application.Current?.Resources, _cardCornerRadius);
        }

        /// <summary>Raised after the accent family is (re)derived.</summary>
        public static event EventHandler AccentChanged;

        /// <summary>Raised after the user UI scale (Ctrl+Wheel) changes.</summary>
        public static event EventHandler? UiScaleChanged;

        // ══════════════════════════════════════════════════════════════
        //  THEME SWITCHING (original API — behaviour preserved)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Parsed Light/Dark palettes. Phase 4: overlay sources only —
        /// never swapped in MergedDictionaries. App.xaml keeps LightTheme
        /// merged as the startup fallback.
        /// </summary>
        private static ResourceDictionary? _lightDict;
        private static ResourceDictionary? _darkDict;

        private static readonly string[] OverlaySkipPrefixes =
        {
            "Density", "CardCorner", "ControlCorner", "CardBorder", "CardEdge",
            "Radius.", "Border.", "Surface.", "ProGlass.", "UiScale",
            "ReducedMotion", "MotionSpeedRatio", "CrystalWater", "CrystalAmbient",
            "CrystalPreview"
        };

        private static ResourceDictionary LoadThemeDictionary(AppTheme theme)
        {
            if (theme == AppTheme.Dark)
            {
                return _darkDict ??= new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Themes/DarkTheme.xaml", UriKind.Absolute)
                };
            }

            return _lightDict ??= new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Themes/LightTheme.xaml", UriKind.Absolute)
            };
        }

        private static void OverlayThemeDictionary(ResourceDictionary source)
        {
            var resources = Application.Current?.Resources;
            if (resources == null) return;

            var keys = new List<object>();
            foreach (DictionaryEntry entry in source)
                keys.Add(entry.Key);

            foreach (var keyObj in keys)
            {
                if (keyObj is not string key) continue;
                bool skip = false;
                for (int i = 0; i < OverlaySkipPrefixes.Length; i++)
                {
                    if (key.StartsWith(OverlaySkipPrefixes[i], StringComparison.Ordinal))
                    {
                        skip = true;
                        break;
                    }
                }
                if (skip) continue;

                try { resources[key] = source[key]; }
                catch (Exception ex)
                {
                    Logging.AppLogService.Debug(
                        $"[ThemeManager] overlay skip {key}: {ex.Message}");
                }
            }
        }

        public static void ApplyTheme(AppTheme theme, bool deferPostProcessing = false)
        {
            // The new dictionary replaces the merged brushes. The live-fill
            // cache must not skip the republish that puts the operator's
            // plate back on top of that dictionary. The family key is
            // cleared for the same reason: the next surface pass has to
            // publish once, then slider ticks can skip that copy.
            _liveFillSignature = null;
            _familyPublishKey = null;
            _publishedSurfaceStroke = null;
            ResourceDictionary palette;
            try
            {
                palette = LoadThemeDictionary(theme);
            }
            catch (Exception ex)
            {
                ProGlassAutomation.Services.Logging.AppLogService.Warning(
                    $"Theme dictionary load failed ({theme}): {ex.Message}",
                    new ProGlassAutomation.Services.Logging.LogContext
                    {
                        Module = "Themes",
                        Operation = "ApplyTheme"
                    });
                return;
            }

            OverlayThemeDictionary(palette);
            _currentTheme = theme;

            // 2026-09-16 (owner report: "cards not accepting dynamic colors …
            // no border lines on cards"): an unresolvable {DynamicResource}
            // does not throw — a Border renders with a null BorderBrush and a
            // null Background, and a TextBlock keeps the inherited size. That
            // is invisible in every log. The About module reads 45 keys from
            // three different dictionaries, so verify them here after every
            // apply and repair anything absent with live theme tokens, then
            // name the repaired keys in one warning line.
            var aboutScope = Application.Current?.Resources;
            if (aboutScope != null)
                Theme.AboutModuleTokenGuard.Ensure(aboutScope);

            if (!deferPostProcessing)
            {
                ApplyThemePostProcessing(theme);
                ThemeChanged?.Invoke(null, EventArgs.Empty);
            }
            else
            {
                // ThemePipeline performs the family/accent/outline/surface
                // sequence once, after the dictionary swap. Keep only the
                // cheap Liquid reference palette update here; skipping the
                // duplicate accent/outline/family work removes a visible
                // switching stutter on large modules without changing the
                // public ApplyTheme behavior.
                PublishLiquidReference(theme == AppTheme.Dark);
                InvalidateVisualThemeSceneCache();
            }
        }

        public static void ToggleTheme()
        {
            ApplyTheme(_currentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
        }

        // ══════════════════════════════════════════════════════════════
        //  LIQUID GLASS REFERENCE PALETTE (2026-08-29)
        //
        //  The canonical design lives in Themes/Styles/LiquidGlassDesign.xaml
        //  (LIGHT values). This publishes the per-mode values into the
        //  application scope so every adopted style (buttons, panels,
        //  cards, switches, inputs, typography) re-tints live on
        //  Light/Dark — while staying byte-faithful to the reference in
        //  Light mode.
        // ══════════════════════════════════════════════════════════════

        private static void PublishLiquidReference(bool dark)
        {
            var resources = Application.Current?.Resources;
            if (resources == null) return;

            if (dark)
            {
                FreezeAndSet(resources, "TextPrimaryBrush", Color.FromRgb(0xF2, 0xF5, 0xF8));
                FreezeAndSet(resources, "TextSecondaryBrush", Color.FromArgb(0xB4, 0xE2, 0xEA, 0xF4));
                FreezeAndSet(resources, "GlassSurfaceUltra", Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF));
                FreezeAndSet(resources, "GlassSurfaceStandard", Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
                FreezeAndSet(resources, "GlassSurfaceElevated", Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF));
                FreezeAndSet(resources, "GlassBorderPrimary", Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF));
                FreezeAndSet(resources, "GlassBorderBright", Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF));
                FreezeAndSet(resources, "GlassBorderSoft", Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));

                var idle = new LinearGradientBrush(
                    Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF),
                    Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF),
                    new Point(0, 0), new Point(1, 1));
                idle.Freeze();
                resources["GlassGradientIdle"] = idle;

                var topRef = new LinearGradientBrush(
                    Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF),
                    Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
                    new Point(0, 0), new Point(0, 1));
                topRef.Freeze();
                resources["GlassTopReflection"] = topRef;

                // 2026-09-15: AppBackgroundBrush is NO LONGER published here.
                // The page background is the About stage and is deliberately
                // theme-INVARIANT ("Background page dont make it dynamic"),
                // so both theme dictionaries declare the identical drawing and
                // a Light/Dark switch must not re-tint it. The overlay in
                // OverlayThemeDictionary still copies the theme dictionary's
                // own value across, which is the same object either way.
                // See Themes/Styles/AppStage.xaml and AppStageAuditTests.
            }
            else
            {
                // Light mode — the reference values verbatim, EXCEPT the
                // secondary ink: 2026-09-01 cream pass replaces the cool
                // slate (#1E2832) with the same warm stone as the Normal
                // canvas (#57534E @ 99-alpha) so no blue cast survives in
                // Light — in any family.
                FreezeAndSet(resources, "TextPrimaryBrush", Color.FromRgb(0x1C, 0x19, 0x17));
                FreezeAndSet(resources, "TextSecondaryBrush", Color.FromArgb(0x99, 0x57, 0x53, 0x4E));
                FreezeAndSet(resources, "GlassSurfaceUltra", Color.FromArgb(0x0E, 0xFF, 0xFF, 0xFF));
                FreezeAndSet(resources, "GlassSurfaceStandard", Color.FromArgb(0x17, 0xFF, 0xFF, 0xFF));
                FreezeAndSet(resources, "GlassSurfaceElevated", Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
                FreezeAndSet(resources, "GlassBorderPrimary", Color.FromArgb(0xE0, 0xFF, 0xFF, 0xFF));
                FreezeAndSet(resources, "GlassBorderBright", Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
                FreezeAndSet(resources, "GlassBorderSoft", Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF));

                var idle = new LinearGradientBrush(
                    Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF),
                    Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF),
                    new Point(0, 0), new Point(1, 1));
                idle.Freeze();
                resources["GlassGradientIdle"] = idle;

                var topRef = new LinearGradientBrush(
                    Color.FromArgb(0xBF, 0xFF, 0xFF, 0xFF),
                    Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
                    new Point(0, 0), new Point(0, 1));
                topRef.Freeze();
                resources["GlassTopReflection"] = topRef;

                // 2026-09-15: dark/light AppBackgroundBrush publish removed —
                // see the note in the dark branch above. The stage is invariant
                // by owner directive, so the theme dictionaries own it and no
                // runtime publisher may repaint it.
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  ACCENT ENGINE (NEW)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Derives and publishes the full accent family from one colour.
        /// Pass null to follow the theme's default industrial cyan.
        /// </summary>
        public static void ApplyAccent(Color? accent, bool raiseEvent = true)
        {
            _customAccent = accent;

            var resources = Application.Current?.Resources;
            if (resources == null) return;

            // Accent participates in the visual-theme scene fingerprint.
            try { Theme.VisualThemeEngine.InvalidateSceneCache(); }
            catch (Exception ex)
            {
                Logging.AppLogService.Debug(
                    "[ThemeManager] scene-cache invalidation failed: " + ex.Message);
            }

            // Resolve the effective accent: custom, or theme default.
            Color effective = accent ?? ThemeDefaultAccent();
            AccentColor = effective;

            var (h, s, l) = ColorMath.ToHsl(effective);
            bool dark = _currentTheme == AppTheme.Dark;

            // Normal = pure cream SaaS canvas. Its chrome (hover fills,
            // tints, card keylines) must stay WARM-NEUTRAL so the
            // default blue accent can never wash the family blue again.
            // The accent still drives CTAs (AccentBrush family), and
            // Monet / Crystal Fluid derive their own tinted surfaces
            // per family below.
            bool normalFamily =
                VisualThemeEngine.Current == VisualThemeEngine.Normal
                || VisualThemeEngine.Current == VisualThemeEngine.NormalIndustrial;

            // ── Core accent family ────────────────────────────────────
            // A colour the operator picked is that colour. The old dark-theme
            // floor (L >= 0.55) turned a picked black into grey. Only the
            // theme's own default accent still lifts, so it stays visible
            // on a dark canvas.
            Color core = (dark && accent == null)
                ? ColorMath.FromHsl(h, s, Math.Max(l, 0.55))
                : effective;

            FreezeAndSet(resources, "AccentBrush", core);
            FreezeAndSet(resources, "AccentHoverBrush",
                ColorMath.Lighten(core, dark ? 0.08 : 0.06));

            // Color-typed counterparts for gradient stops /
            // DropShadowEffect.Color so they follow the accent too.
            // (A Color is a struct — no Freeze needed, just assign.)
            resources["AccentColor"] = core;
            resources["AccentHoverColor"] =
                ColorMath.Lighten(core, dark ? 0.08 : 0.06);
            resources["AccentDarkColor"] =
                ColorMath.FromHsl(h, Math.Min(1, s + 0.05), Math.Max(0.16, l - 0.18));
            resources["AccentLightColor"] =
                ColorMath.Lighten(core, dark ? 0.12 : 0.12);
            FreezeAndSet(resources, "AccentDarkBrush",
                ColorMath.FromHsl(h, Math.Min(1, s + 0.05), Math.Max(0.16, l - 0.18)));
            FreezeAndSet(resources, "AccentLightBrush",
                ColorMath.Lighten(core, 0.12));
            FreezeAndSet(resources, "OverlayButtonBackgroundBrush", core);

            // Active control treatment follows the selected material family:
            // only glass-like themes use the luminous liquid ramp; solid/editorial
            // families keep crisp fills so all eight identities remain distinct.
            Color activeTop;
            Color activeMid;
            Color activeBot;
            switch (VisualThemeEngine.Current)
            {
                case VisualThemeEngine.LiquidGlass:
                case VisualThemeEngine.Glassmorphism:
                    activeTop = ColorMath.Mix(Color.FromArgb(0xB7, 0x28, 0xBE, 0xFF), core, 0.45);
                    activeMid = ColorMath.Mix(Color.FromArgb(0x66, 0x82, 0xE1, 0xFF), core, 0.45);
                    activeBot = ColorMath.Mix(Color.FromArgb(0xA6, 0x32, 0xAF, 0xF5), core, 0.45);
                    break;
                case VisualThemeEngine.HolographicIridescent:
                    activeTop = ColorMath.Mix(Color.FromRgb(166, 108, 255), core, 0.40);
                    activeMid = ColorMath.Mix(Color.FromRgb(80, 205, 255), core, 0.30);
                    activeBot = ColorMath.Mix(Color.FromRgb(244, 117, 210), core, 0.40);
                    break;
                case VisualThemeEngine.SpatialImmersive:
                    activeTop = ColorMath.Lighten(core, 0.16);
                    activeMid = ColorMath.Mix(core, Color.FromRgb(53, 184, 201), 0.22);
                    activeBot = ColorMath.Mix(core, Color.FromRgb(0, 0, 0), 0.10);
                    break;
                case VisualThemeEngine.SwissInternational:
                    activeTop = core;
                    activeMid = core;
                    activeBot = core;
                    break;
                case VisualThemeEngine.Neumorphism:
                    activeTop = ColorMath.Lighten(core, 0.10);
                    activeMid = core;
                    activeBot = ColorMath.Mix(core, Color.FromRgb(0, 0, 0), 0.04);
                    break;
                case VisualThemeEngine.Claymorphism:
                    activeTop = ColorMath.Lighten(core, 0.12);
                    activeMid = core;
                    activeBot = ColorMath.Mix(core, Color.FromRgb(0, 0, 0), 0.06);
                    break;
                default:
                    activeTop = ColorMath.Lighten(core, 0.08);
                    activeMid = core;
                    activeBot = ColorMath.Mix(core, Color.FromRgb(0, 0, 0), 0.08);
                    break;
            }
            var activeGlass = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            activeGlass.GradientStops.Add(new GradientStop(activeTop, 0.0));
            activeGlass.GradientStops.Add(new GradientStop(activeMid, 0.5));
            activeGlass.GradientStops.Add(new GradientStop(activeBot, 1.0));
            activeGlass.Freeze();
            resources["GlassGradientActive"] = activeGlass;

            // ── Contrast-aware text on accent ─────────────────────────
            FreezeAndSet(resources, "OnAccentForegroundBrush",
                ColorMath.BestTextOn(core));
            FreezeAndSet(resources, "OverlayButtonForegroundBrush",
                ColorMath.BestTextOn(core));

            // ── Tinted surfaces (cards, chips, KPI tiles) ────────────
            Color surface = LookupColor(resources, "ModuleBackgroundBrush",
                dark ? Color.FromRgb(0x1E, 0x29, 0x3B)
                     : Color.FromRgb(0xFF, 0xFF, 0xFF));

            // Glass-like families own their tint brushes (Liquid Glass and
            // Glassmorphism use frost; Spatial and Holographic use atmospheric
            // tints). ApplyAccent runs after the family engine, so repainting
            // those brushes as opaque accent chips would erase the material.
            if (VisualThemeEngine.Current != VisualThemeEngine.CrystalFluid &&
                VisualThemeEngine.Current != VisualThemeEngine.LiquidGlass &&
                VisualThemeEngine.Current != VisualThemeEngine.Glassmorphism &&
                VisualThemeEngine.Current != VisualThemeEngine.SpatialImmersive &&
                VisualThemeEngine.Current != VisualThemeEngine.HolographicIridescent)
            {
                if (normalFamily)
                {
                    // Warm-neutral SaaS tints (hover chips, info
                    // badges) — Normal must never read blue. The
                    // colourful families below keep accent tints.
                    FreezeAndSet(resources, "TintBlueBrush", dark
                        ? Color.FromRgb(0x23, 0x2B, 0x34)
                        : Color.FromRgb(0xF2, 0xED, 0xE3));
                    FreezeAndSet(resources, "TintIndigoBrush", dark
                        ? Color.FromRgb(0x27, 0x2F, 0x3A)
                        : Color.FromRgb(0xED, 0xE7, 0xDA));
                }
                else
                {
                    FreezeAndSet(resources, "TintBlueBrush",
                        ColorMath.Tint(core, surface, dark ? 0.16 : 0.08));
                    FreezeAndSet(resources, "TintIndigoBrush",
                        ColorMath.Tint(
                            ColorMath.FromHsl(h + 18, s, l), surface,
                            dark ? 0.18 : 0.09));
                }
            }

            // ── Card keyline — accent-derived, applied app-wide ───────
            // The Settings "Appearance" cards always looked right because
            // SectionCardStyle binds AccentBrush directly, so their edge
            // was a real colour. Every other card in the app bound the
            // neutral OutlineBrush, and no amount of darkening that grey
            // ever matched, because the difference was HUE, not weight.
            //
            // The keyline is now derived from the accent for the whole
            // app: same hue, saturation eased back and lightness clamped
            // so it reads as a definite coloured edge on both themes
            // without turning into a heavy box. Measured across all six
            // presets x light/dark the result sits between 2.3:1 and
            // 7.9:1 against the card it outlines.
            //
            // Written to OutlineDefaultBrush as well, so the
            // "Defined Card Edges" toggle can still restore this value
            // when it is switched back off.
            //
            // The saturation floor is conditional. Raising a vivid accent
            // to 0.45 keeps the edge from washing out, but applying the
            // same floor to a deliberately neutral accent (the Black
            // preset, #111827 at S=0.39) would drag it to a navy blue -
            // a "Black" option that renders blue. So near-neutral accents
            // keep their own saturation and only get a lightness lift.
            bool neutralAccent = s < 0.42;

            double keySat = neutralAccent
                ? s
                : Math.Max(s * (dark ? 0.80 : 0.85), dark ? 0.40 : 0.45);

            double keyLight = dark
                ? Math.Max(l, neutralAccent ? 0.62 : 0.52)
                : Math.Min(Math.Max(l, neutralAccent ? 0.26 : 0.42), 0.55);

            Color keyline = ColorMath.FromHsl(h, keySat, keyLight);

            if (normalFamily)
            {
                // Normal keyline = the theme's own warm-neutral hairline
                // (cream #D8D0C2 light / warm slate dark), NOT the accent
                // hue — a pure cream SaaS canvas has neutral card edges;
                // only the accent families carry a coloured keyline.
                keyline = ThemeManager.GetThemeColor("OutlineBrush",
                    dark ? Color.FromRgb(0x5C, 0x57, 0x4C)
                         : Color.FromRgb(0xD8, 0xD0, 0xC2));
            }

            FreezeAndSet(resources, "OutlineBrush", keyline);
            FreezeAndSet(resources, "OutlineDefaultBrush", keyline);

            // The soft edge (inputs, dividers, gridlines, table rules)
            // takes the same hue but is mixed most of the way back to the
            // surface, so forms stay light while still belonging to the
            // accent family.
            FreezeAndSet(resources, "BorderBrush",
                ColorMath.Mix(keyline, surface, 0.62));
            FreezeAndSet(resources, "DividerBrush",
                ColorMath.Mix(keyline, surface, 0.62));

            // ── About / Liquid palette — accent-aware, never hard-coded ──
            // The About page and the Crystal Fluid (Ios-17pm) family
            // share the Liquid* palette. Re-publish it from the current
            // accent so About follows custom accents, theme-family swaps
            // and live Settings changes instead of staying fixed to one accent.
            //
            // 2026-09-06 (About adaptation fix): this previously SKIPPED
            // the crystal family, leaving the About cards pinned to the
            // base theme-file values while every other surface adopted the
            // crystal material — the About module looked like a foreign
            // screen under Ios-17pm. PublishAboutPalette already paints
            // ultra-low-alpha crystal glass (0x0D–0x22 fills), so running
            // it under Crystal Fluid makes About wear the same
            // accent-tinted transparent glass as the rest of the app.
            PublishAboutPalette(resources, core, surface, dark, h, s, l);

            // ── Dashboard ambience — derived, never hard-coded ──────
            // The dashboards consume these via DynamicResource, so the
            // whole analytics surface follows the theme AND the accent.
            PublishDashAmbience(resources, core, dark);

            PublishProGlassDesignSystem();

            if (raiseEvent)
                AccentChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Re-derives the About / Liquid palette from the active accent.
        /// This keeps the About module's crystal chrome, tab strip,
        /// chips, orbs and shared Crystal Fluid (Ios-17pm) family
        /// resources aligned with live accent changes instead of freezing
        /// them to the theme's original blue.
        /// </summary>
        private static void PublishAboutPalette(
            ResourceDictionary resources,
            Color core,
            Color surface,
            bool dark,
            double h,
            double s,
            double l)
        {
            Color accent = dark
                ? ColorMath.Lighten(core, 0.08)
                : ColorMath.FromHsl(h, Math.Max(0.42, s * 0.88), Math.Min(0.46, Math.Max(0.34, l)));
            Color accentDeep = dark
                ? ColorMath.FromHsl(h, Math.Min(1.0, s + 0.04), Math.Max(0.42, l - 0.05))
                : ColorMath.FromHsl(h, Math.Min(1.0, s + 0.06), Math.Max(0.28, l - 0.14));
            Color cerulean = dark
                ? ColorMath.Lighten(core, 0.18)
                : ColorMath.FromHsl(h, Math.Max(0.34, s * 0.58), Math.Min(0.40, Math.Max(0.28, l - 0.12)));

            FreezeAndSet(resources, "LiquidAccent", accent);
            FreezeAndSet(resources, "LiquidAccentDeep", accentDeep);
            FreezeAndSet(resources, "LiquidCerulean", cerulean);

            var card = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };

            // Watery meniscus ramp (2026-08-29): the same droplet light
            // curve the engine's FrostBrush uses — dip under the top
            // edge, bright surface-tension band, transparent center,
            // light gathered at the bottom rim.
            Color topTint = ColorMath.Tint(accent,
                Color.FromRgb(255, 255, 255), dark ? 0.14 : 0.12);
            Color deepTint = ColorMath.Tint(accentDeep,
                Color.FromRgb(255, 255, 255), dark ? 0.08 : 0.05);

            // ── CRYSTAL GLASS (Ios-17pm) ────────────────────────────
            // Both themes now use very low alpha so the background
            // gradient shows through every card, matching the iPhone
            // crystal-glass effect where surfaces are near-invisible
            // and only the crystal edge defines the shape.
            if (dark)
            {
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(topTint, 0x18), 0.00));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(topTint, 0x12), 0.06));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(ColorMath.Mix(topTint,
                        Color.FromRgb(255, 255, 255), 0.05), 0x1E), 0.16));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(ColorMath.Mix(topTint, deepTint, 0.5), 0x0D), 0.50));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(deepTint, 0x08), 0.93));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(ColorMath.Mix(deepTint,
                        Color.FromRgb(255, 255, 255), 0.06), 0x0B), 1.00));
            }
            else
            {
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(topTint, 0x1C), 0.00));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(ColorMath.Mix(topTint, deepTint, 0.25), 0x14), 0.06));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(ColorMath.Mix(topTint, Color.FromRgb(255, 255, 255), 0.08), 0x22), 0.16));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(ColorMath.Mix(topTint, deepTint, 0.5), 0x0E), 0.50));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(deepTint, 0x0A), 0.93));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(ColorMath.Mix(deepTint, Color.FromRgb(255, 255, 255), 0.10), 0x0D), 1.00));
            }
            FreezeAndSetBrush(resources, "LiquidCard", card);

            var sheen = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            sheen.GradientStops.Add(new GradientStop(
                ColorMath.WithAlpha(dark ? cerulean : accent, dark ? (byte)0x34 : (byte)0x18), 0.0));
            sheen.GradientStops.Add(new GradientStop(Color.FromArgb(0, accent.R, accent.G, accent.B), 0.65));
            FreezeAndSetBrush(resources, "LiquidSheen", sheen);

            var orbA = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            orbA.GradientStops.Add(new GradientStop(
                ColorMath.WithAlpha(dark ? ColorMath.Lighten(accent, 0.10) : accent, dark ? (byte)0x54 : (byte)0x30), 0.0));
            orbA.GradientStops.Add(new GradientStop(
                ColorMath.WithAlpha(accentDeep, dark ? (byte)0x2A : (byte)0x16), 0.55));
            orbA.GradientStops.Add(new GradientStop(
                Color.FromArgb(0, accentDeep.R, accentDeep.G, accentDeep.B), 1.0));
            FreezeAndSetBrush(resources, "LiquidOrbA", orbA);

            var orbB = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            orbB.GradientStops.Add(new GradientStop(
                ColorMath.WithAlpha(dark ? cerulean : ColorMath.Lighten(accent, 0.08), dark ? (byte)0x46 : (byte)0x2E), 0.0));
            orbB.GradientStops.Add(new GradientStop(
                ColorMath.WithAlpha(accent, dark ? (byte)0x24 : (byte)0x12), 0.55));
            orbB.GradientStops.Add(new GradientStop(
                Color.FromArgb(0, accent.R, accent.G, accent.B), 1.0));
            FreezeAndSetBrush(resources, "LiquidOrbB", orbB);

            // ── ULTRA-TRANSPARENT PANEL FILLS (iOS glass) ─────────────
            // Panel fills are now very low alpha so the background
            // gradient bleeds through. Borders stay visible (crystal
            // edge effect) but fills are nearly invisible.
            Color panelFill = dark
                ? ColorMath.WithAlpha(ColorMath.Tint(accent, Color.FromRgb(255, 255, 255), 0.20), 0x10)
                : ColorMath.WithAlpha(ColorMath.Tint(accent, Color.FromRgb(255, 255, 255), 0.50), 0x0D);
            Color panelHover = dark
                ? ColorMath.WithAlpha(ColorMath.Tint(accent, Color.FromRgb(255, 255, 255), 0.28), 0x1C)
                : ColorMath.WithAlpha(ColorMath.Tint(accent, Color.FromRgb(255, 255, 255), 0.50), 0x16);
            Color panelBorder = dark
                ? ColorMath.WithAlpha(accent, 0x50)
                : ColorMath.WithAlpha(accent, 0x40);
            Color panelBorderStrong = dark
                ? ColorMath.WithAlpha(ColorMath.Lighten(accent, 0.08), 0x70)
                : ColorMath.WithAlpha(accent, 0x60);
            Color cardBorder = dark
                ? ColorMath.WithAlpha(ColorMath.Lighten(accent, 0.12), 0x60)
                : ColorMath.WithAlpha(accent, 0x38);
            Color divider = dark
                ? ColorMath.WithAlpha(accent, 0x22)
                : ColorMath.WithAlpha(accent, 0x1A);

            FreezeAndSet(resources, "AboutInteractiveBrush", panelFill);
            FreezeAndSet(resources, "AboutInteractiveHoverBrush", panelHover);
            FreezeAndSet(resources, "AboutInteractiveBorderBrush", panelBorder);
            FreezeAndSet(resources, "AboutInteractiveBorderStrongBrush", panelBorderStrong);
            FreezeAndSet(resources, "AboutCardBorderBrush", cardBorder);
            FreezeAndSet(resources, "AboutDividerBrush", divider);

            // ── ABOUT MODULE BRIDGE (owner report: About "adapts font but NOT theme") ──
            // The About views read Ios26* keys; this engine publishes About*/Liquid*
            // keys. Exactly one bridge was ever built - AboutSurfaceTokens.Publish
            // aliases AboutCardBorderBrush onto Ios26CardBorderBrush - so 97 of the
            // module's 98 visual tokens stayed at their theme-dictionary literals and
            // moved on a Light/Dark switch alone, never on a Monet / family pass.
            //
            // Ios26AccentBrush was the clearest case: a hardcoded #0369A1 (light) /
            // #38BDF8 (dark) blue, read by eight About files, while the real accent was
            // computed twelve lines above and published as LiquidAccent - a key no About
            // file reads. The engine changed keys nobody read; About read keys nobody
            // changed. These aliases close that gap on the same pass, so ordering is
            // guaranteed and no second publish hook is needed.
            //
            // Bridged: only the tokens whose whole purpose is to carry the accent.
            // Deliberately NOT bridged:
            //   - Ios26Text* : neutral ink. Tinting body copy with the accent would
            //     trade readability for theme-awareness.
            //   - Ios26CardBrush / Ios26TileBrush / Ios26RailBrush : the approved card
            //     design ("Keeping the cards as it is design"), pinned by
            //     AboutModuleAuditTests and AppStageAuditTests.
            //   - Ios26CanvasBrush and the four Ios26Orb* : AppStageAuditTests requires
            //     them declared in Themes/Styles/AppStage.xaml, OUTSIDE the theme swap.
            // The XAML literals in Light/DarkTheme.xaml are left in place on purpose:
            // they are the first-frame fallback and AboutModuleAuditTests asserts every
            // one of them is declared there.
            FreezeAndSet(resources, "Ios26AccentBrush", accent);
            FreezeAndSet(resources, "Ios26AccentSoftTextBrush", accentDeep);
            FreezeAndSet(resources, "Ios26AccentSoftBrush",
                ColorMath.WithAlpha(accent, dark ? (byte)0x33 : (byte)0x22));
            FreezeAndSet(resources, "Ios26HairlineBrush", divider);
            FreezeAndSet(resources, "Ios26HoverBrush", panelHover);
        }

        private static void PublishDashAmbience(
            ResourceDictionary resources,
            Color core, bool dark)
        {
            Color surface = dark
                ? Color.FromRgb(0x0F, 0x17, 0x2A)
                : Color.FromRgb(0xFF, 0xFF, 0xFF);

            // Ambient page gradient — accent tinted, theme aware.
            // (LinearGradientBrush has no (Point,Point) ctor — set
            // StartPoint/EndPoint as properties.)
            var ambient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            ambient.GradientStops.Add(new GradientStop(
                ColorMath.Tint(core, surface, dark ? 0.10 : 0.07), 0.0));
            ambient.GradientStops.Add(new GradientStop(
                ColorMath.Tint(core, surface, dark ? 0.14 : 0.11), 0.5));
            ambient.GradientStops.Add(new GradientStop(
                ColorMath.Tint(core, surface, dark ? 0.20 : 0.16), 1.0));
            // 2026-09-06 theme reduction: no surviving family paints the
            // dashboard surface as glass (the iOS Liquid Glass painter was
            // removed), so the accent-derived dashboard ambient always
            // applies. DashAccentTextBrush below stays accent-driven in
            // every family.
            ambient.Freeze();
            resources["DashAmbientBrush"] = ambient;

            // Top-right glow blob.
            var blob1 = new RadialGradientBrush();
            blob1.Center = new Point(0.85, 0.15);
            blob1.GradientOrigin = new Point(0.85, 0.15);
            blob1.RadiusX = 0.35;
            blob1.RadiusY = 0.35;
            blob1.GradientStops.Add(new GradientStop(
                Color.FromArgb(dark ? (byte)0x45 : (byte)0x38,
                    core.R, core.G, core.B), 0.0));
            blob1.GradientStops.Add(new GradientStop(
                Color.FromArgb(0, core.R, core.G, core.B), 1.0));
            blob1.Freeze();
            resources["DashAmbientBlob1Brush"] = blob1;

            // Bottom-left deep blob.
            Color deep = LookupColor(resources, "AccentDarkBrush", core);
            var blob2 = new RadialGradientBrush();
            blob2.Center = new Point(0.12, 0.88);
            blob2.GradientOrigin = new Point(0.12, 0.88);
            blob2.RadiusX = 0.30;
            blob2.RadiusY = 0.30;
            blob2.GradientStops.Add(new GradientStop(
                Color.FromArgb(dark ? (byte)0x30 : (byte)0x20,
                    deep.R, deep.G, deep.B), 0.0));
            blob2.GradientStops.Add(new GradientStop(
                Color.FromArgb(0, deep.R, deep.G, deep.B), 1.0));
            blob2.Freeze();
            resources["DashAmbientBlob2Brush"] = blob2;

            // Liquid card fill — ultra-transparent for iOS glass effect.
            var glass = new SolidColorBrush(
                ColorMath.Tint(core, surface, dark ? 0.04 : 0.02))
            { Opacity = 0.12 };
            glass.Freeze();
            resources["DashLiquidSurfaceBrush"] = glass;

            // Accent text on the dashboard.
            FreezeAndSet(resources, "DashAccentTextBrush",
                dark ? ColorMath.Lighten(core, 0.18) : deep);
        }

        /// <summary>Back to the theme's built-in blue accent.</summary>
        public static void ResetAccent() => ApplyAccent(null);

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Reads the theme dictionary's own AccentBrush — the per-theme
        /// default (blue-700 light / blue-500 dark).
        /// </summary>
        private static Color ThemeDefaultAccent()
        {
            try
            {
                var dict = LoadThemeDictionary(_currentTheme);
                if (dict["AccentBrush"] is SolidColorBrush themeBrush)
                    return themeBrush.Color;
            }
            catch
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("ThemeDefaultAccent suppressed");
            }

            return _currentTheme == AppTheme.Dark
                ? Color.FromRgb(0x38, 0xBD, 0xF8)
                : Color.FromRgb(0x03, 0x69, 0xA1);
        }

        private static Color LookupColor(
            ResourceDictionary resources, string key, Color fallback)
        {
            return resources[key] is SolidColorBrush brush
                ? brush.Color
                : fallback;
        }

        /// <summary>
        /// Reads a colour from the ACTIVE theme dictionary (Light/Dark).
        /// Used by the visual theme engine to derive translucent
        /// variants that always match the current base theme.
        /// </summary>
        internal static Color GetThemeColor(string key, Color fallback)
        {
            try
            {
                var dict = LoadThemeDictionary(_currentTheme);
                return dict[key] switch
                {
                    SolidColorBrush brush => brush.Color,
                    Color color => color,
                    _ => fallback
                };
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>
        /// Sets an application-level resource with a frozen brush.
        /// App-level entries override the merged theme dictionaries,
        /// which is exactly how derived values win over defaults.
        /// </summary>
        // ══════════════════════════════════════════════════════════════
        //  OUTLINE ENGINE — the "defined edge" toggle
        // ══════════════════════════════════════════════════════════════

        private static bool _definedOutline;

        /// <summary>User's custom card-edge colour (null = follow the
        /// theme's strong outline brush). Parsed once from the saved hex
        /// so ApplyOutline can repaint without re-parsing per switch.</summary>
        private static Color? _cardEdgeColor;

        // 2026-08-25: gradient surface support. VisualThemeEngine reads
        // this when it builds Monet glass / window surfaces: true =
        // accent-derived gradients (richer depth), false = flat colour.
        // Default true. Persisted in AppearanceSettings.UseGradientSurfaces.
        private static bool _useGradients = true;

        /// <summary>Whether adaptive surfaces render as gradients.</summary>
        public static bool UseGradients => _useGradients;

        /// <summary>
        /// Sets the gradient-surface preference. The caller re-applies
        /// the active visual family afterwards so the surfaces rebuild
        /// with the new style.
        /// </summary>
        public static void ApplyGradientPreference(bool useGradients)
        {
            _useGradients = useGradients;
        }

        /// <summary>
        /// True when rounded pills and cards draw a strong, near-black
        /// definition edge instead of the subtle default hairline.
        /// </summary>
        public static bool UseDefinedOutline => _definedOutline;

        /// <summary>
        /// Switches OutlineBrush between the subtle default and the strong
        /// "defined" edge. When <paramref name="defined"/> is true it ALSO
        /// repaints GlassBorderSoft + GlassBorderGradientCrystal, so the one
        /// toggle (and the custom <see cref="ApplyCardEdgeColor"/> colour)
        /// outlines EVERY card surface app-wide.
        ///
        /// WHY A SEPARATE KEY FROM BorderBrush: BorderBrush is also used by
        /// text inputs, dividers and table gridlines. Darkening that globally
        /// would make every field look heavy. OutlineBrush is consumed only by
        /// pill buttons and card shapes, which are the elements that need to
        /// stay visually separate from a tinted (Monet) surface.
        ///
        /// WHY NOT LITERAL BLACK: on the dark theme a #000 edge against a
        /// near-black card disappears and reads as a hole, so the dark theme
        /// resolves the same key to a LIFTED slate. Both themes therefore get
        /// a visible edge from one switch.
        /// </summary>
        public static void ApplyOutline(bool defined)
        {
            _definedOutline = defined;

            var resources = Application.Current?.Resources;
            if (resources == null) return;

            // The user's custom card-edge colour wins; otherwise read the
            // theme-provided value rather than hardcoding a colour here, so
            // each theme keeps control of what "strong" and "subtle" mean
            // for its own surfaces.
            Color edge;
            if (defined && _cardEdgeColor is Color custom)
            {
                edge = custom;
            }
            else
            {
                string sourceKey = defined ? "OutlineStrongBrush" : "OutlineDefaultBrush";
                if (resources[sourceKey] is not SolidColorBrush src)
                    return;   // theme dictionary not loaded yet — nothing safe to paint
                edge = src.Color;
            }

            FreezeAndSet(resources, "OutlineBrush", edge);
            // The ON/OFF switch controls the visible edge weight too. Keep the
            // user's 1-100 DIP slider value stored, but collapse cards back to
            // the normal subtle card border thickness while OFF.
            double effectiveEdge = defined ? _cardEdgeThickness : _cardBorderThickness;
            resources["CardEdgeThickness"] = new Thickness(effectiveEdge);
            ApplyCardEdgePaddingResources(effectiveEdge);

            // 2026-09-07 — "Define Card Edges" now reaches EVERY card.
            // Base/Task/Stat/Section/Tile/Soft/Dash cards all bind
            // GlassBorderSoft or GlassBorderGradientCrystal, so writing
            // those two tokens outlines the whole app from one switch (and
            // one colour). When the toggle is OFF the Glass* tokens are
            // deliberately left alone so the active visual family keeps its
            // own authored edge (industrial slate, crystal white, cream soft).
            FreezeAndSet(resources, "GlassBorderSoft", edge);
            FreezeAndSet(resources, "GlassBorderGradientCrystal", edge);

            // Settings cards do not read OutlineBrush. Their rounded stroke,
            // including the corner arc, is SettingsCardEdgeBrush /
            // SettingsInnerEdgeBrush. Weight already worked because those
            // styles bind CardEdgeThickness. Colour did not, so the corner
            // stayed the theme grey. Publish the same edge colour there.
            PublishDefinedCardEdge(resources, defined, edge);


            // Semantic surface border resources are derived from the final
            // card edge/keyline choice, so repaint them in the same live tick.
            ReapplySemanticSurfaces();

            // After the semantic pass, so DualWrite cannot put a second
            // brush under the one About cards actually stroke.
            PublishAboutDefinedEdge(resources, defined, edge, effectiveEdge);
        }

        private static DropShadowEffect? _aboutCardShadowRest;

        /// <summary>
        /// One About card edge. The style reads these two keys only.
        /// A strong edge is a single inset stroke: pixel-snapping is off
        /// in the style, thickness is clamped so a 26 px corner cannot
        /// self-intersect across the next card, and the shared shadow
        /// loses its 10 px offset so it cannot stamp a second outline
        /// onto the card below.
        /// </summary>
        private static void PublishAboutDefinedEdge(
            ResourceDictionary resources, bool defined, Color edge, double thickness)
        {
            if (_aboutCardShadowRest == null
                && resources["Ios26CardShadow"] is DropShadowEffect current
                && current.ShadowDepth > 0)
            {
                _aboutCardShadowRest = current;
            }

            Brush brush;
            double painted;
            if (defined)
            {
                var solid = new SolidColorBrush(edge);
                if (solid.CanFreeze) solid.Freeze();
                brush = solid;
                painted = thickness < 1.0 ? 1.0 : (thickness > 4.0 ? 4.0 : thickness);
            }
            else if (resources["Ios26CardBorderBrush"] is Brush live)
            {
                brush = live;
                painted = 1.0;
            }
            else
            {
                var solid = new SolidColorBrush(edge);
                if (solid.CanFreeze) solid.Freeze();
                brush = solid;
                painted = 1.0;
            }

            resources["AboutDefinedEdgeBrush"] = brush;
            resources["AboutDefinedEdgeThickness"] = new Thickness(painted);

            if (defined)
            {
                var halo = new DropShadowEffect
                {
                    Color = Color.FromRgb(0x33, 0x41, 0x55),
                    Opacity = 0.08,
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Direction = 270,
                };
                if (halo.CanFreeze) halo.Freeze();
                resources["Ios26CardShadow"] = halo;
            }
            else if (_aboutCardShadowRest != null)
            {
                resources["Ios26CardShadow"] = _aboutCardShadowRest;
            }
        }

        /// <summary>
        /// Stores the user's custom card-edge colour (the "Define Card
        /// Edges" hex picker). Empty/invalid text stores null so
        /// <see cref="ApplyOutline"/> falls back to the theme's strong
        /// outline. Callers re-apply <see cref="ApplyOutline"/> afterwards
        /// to repaint live.
        /// </summary>
        public static void ApplyCardEdgeColor(string? hex)
        {
            _cardEdgeColor = Theme.AccentPresets.TryParseHex(hex);
        }

        /// <summary>
        /// Re-applies the current outline choice. Called after a theme swap,
        /// because loading a new dictionary restores that theme's own
        /// OutlineBrush and would silently discard the user's preference.
        /// </summary>
        private static void ReapplyOutline() => ApplyOutline(_definedOutline);

        /// <summary>
        /// Public re-apply for callers that rewrite the outline keys behind
        /// ThemeManager's back — specifically VisualThemeEngine, whose Monet
        /// and Professional families derive their own keyline.
        /// <para>
        /// Ordering matters: the engine must run FIRST (it writes
        /// OutlineDefaultBrush for its family), then this restores the
        /// user's Defined Card Edges choice on top. Without it, selecting a
        /// visual theme silently turned the toggle off.
        /// </para>
        /// </summary>
        public static void ReapplyOutlinePreference() => ReapplyOutline();

        private static double _cardBorderThickness = 1.5;

        /// <summary>
        /// Current global card border thickness in DIPs.
        /// </summary>
    }
}
