// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/ThemeManager.cs
// PROJECT: ProGlassAutomation
// MODULE: Dynamic Theme Engine
//
// UPGRADE — "adapt colour more wisely"
// ─────────────────────────────────────────────────────────────────────
// The original manager only swapped Light/Dark dictionaries. It kept
// every accent colour hard-coded, so switching the brand accent meant
// editing a dozen hex values by hand — and nothing guaranteed that
// hover/pressed/tint variants stayed consistent or readable.
//
// The upgraded engine:
//   1. Still swaps Light/Dark dictionaries (ApplyTheme — unchanged API).
//   2. Derives the ENTIRE accent family from ONE colour using HSL math
//      (Services/Theme/ColorMath.cs): hover, dark, light, overlay and
//      tint variants — all computed, never hard-coded.
//   3. Picks text-on-accent colour by WCAG contrast so buttons are
//      always readable, even if someone chooses a yellow accent.
//   4. Re-derives automatically after every light↔dark swap so the
//      accent always sits correctly against the active surfaces.
//   5. Publishes card-shape + motion resources that the rest of the
//      UI binds to via DynamicResource (CornerRadius, durations).
//
// PUBLIC API (all preserved — nothing removed):
//   AppTheme enum, CurrentTheme, ThemeChanged,
//   ApplyTheme(theme), ToggleTheme()
// NEW API:
//   AccentColor, AccentChanged, ApplyAccent(color), ResetAccent()
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Theme;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Services
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    public static class ThemeManager
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

        /// <summary>Raised after the accent family is (re)derived.</summary>
        public static event EventHandler AccentChanged;

        /// <summary>Raised after the user UI scale (Ctrl+Wheel) changes.</summary>
        public static event EventHandler? UiScaleChanged;

        // ══════════════════════════════════════════════════════════════
        //  THEME SWITCHING (original API — behaviour preserved)
        // ══════════════════════════════════════════════════════════════

        public static void ApplyTheme(AppTheme theme)
        {
            string themeUri = theme == AppTheme.Dark
                ? "pack://application:,,,/Themes/DarkTheme.xaml"
                : "pack://application:,,,/Themes/LightTheme.xaml";

            ResourceDictionary newDict;
            try
            {
                newDict = new ResourceDictionary
                {
                    Source = new Uri(themeUri, UriKind.Absolute)
                };
            }
            catch (Exception ex)
            {
                // Never leave the app without a theme dictionary. If the
                // new dictionary cannot load, KEEP the active one and
                // surface the failure — a themeless resource scope is
                // exactly the "every theme broken" symptom.
                ProGlassAutomation.Services.Logging.AppLogService.Warning(
                    $"Theme dictionary load failed ({themeUri}): {ex.Message}",
                    new ProGlassAutomation.Services.Logging.LogContext
                    {
                        Module = "Themes",
                        Operation = "ApplyTheme"
                    });
                return;
            }

            var appDicts = Application.Current.Resources.MergedDictionaries;

            // Add FIRST, then remove the old instance. A later merged
            // dictionary wins DynamicResource lookups, so the new theme
            // becomes authoritative the moment it is added — there is
            // never a gap where theme keys resolve to nothing (that gap
            // blanked every DynamicResource-bound surface during each
            // Light/Dark swap).
            appDicts.Add(newDict);

            // Remove the PREVIOUS theme dictionary (the new one is
            // deliberately excluded from the match).
            var existing = appDicts.FirstOrDefault(d =>
                d != null && d != newDict && d.Source != null &&
                (d.Source.OriginalString.Contains("LightTheme.xaml") ||
                 d.Source.OriginalString.Contains("DarkTheme.xaml")));

            if (existing != null)
                appDicts.Remove(existing);

            // Only now does the reported mode match the active
            // dictionary (a failed load above returns early and leaves
            // the previous theme reporting itself).
            _currentTheme = theme;

            // NEW: re-derive the accent family against the fresh theme
            // surfaces so hover/tint/contrast stay correct.
            ApplyAccent(_customAccent, raiseEvent: false);

            // Loading a theme dictionary restores that theme's own
            // OutlineBrush, which would silently discard the user's
            // "defined edge" choice on every Light/Dark swap.
            ReapplyOutline();

            // Same for the card border thickness — a fresh dictionary resets
            // CardBorderThickness to the theme default, so re-apply the user's
            // saved value after every theme swap.
            ReapplyCardBorderThickness();

            // Same for card corner radius — theme files ship 12/10/16
            // defaults, then the user's saved radius wins.
            ReapplyCardCornerRadius();

            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void ToggleTheme()
        {
            ApplyTheme(_currentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
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

            // Resolve the effective accent: custom, or theme default.
            Color effective = accent ?? ThemeDefaultAccent();
            AccentColor = effective;

            var (h, s, l) = ColorMath.ToHsl(effective);
            bool dark = _currentTheme == AppTheme.Dark;

            // ── Core accent family ────────────────────────────────────
            // Light theme: accent as chosen.  Dark theme: lift lightness
            // a touch so the colour survives against dark surfaces.
            Color core = dark ? ColorMath.FromHsl(h, s, Math.Max(l, 0.55)) : effective;

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

            // ── Contrast-aware text on accent ─────────────────────────
            FreezeAndSet(resources, "OnAccentForegroundBrush",
                ColorMath.BestTextOn(core));
            FreezeAndSet(resources, "OverlayButtonForegroundBrush",
                ColorMath.BestTextOn(core));

            // ── Tinted surfaces (cards, chips, KPI tiles) ────────────
            Color surface = LookupColor(resources, "ModuleBackgroundBrush",
                dark ? Color.FromRgb(0x1E, 0x29, 0x3B)
                     : Color.FromRgb(0xFF, 0xFF, 0xFF));

            // The iOS Liquid Glass family OWNS the tint brushes (it
            // renders them as see-through hue frost). ApplyAccent runs
            // AFTER the engine on every family switch (settings path),
            // so without this guard it would repaint the Blue/Indigo
            // chips opaque on top of the frost — the same class of bug
            // as the dashboard-surface guard below.
            if (VisualThemeEngine.Current != VisualThemeEngine.LiquidGlass)
            {
                FreezeAndSet(resources, "TintBlueBrush",
                    ColorMath.Tint(core, surface, dark ? 0.16 : 0.08));
                FreezeAndSet(resources, "TintIndigoBrush",
                    ColorMath.Tint(
                        ColorMath.FromHsl(h + 18, s, l), surface,
                        dark ? 0.18 : 0.09));
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
            // The About page and the iOS Liquid Glass family share the
            // Liquid* palette. Re-publish it from the current accent so
            // About finally follows custom accents, theme-family swaps
            // and live Settings changes instead of staying fixed to one accent.
            PublishAboutPalette(resources, core, surface, dark, h, s, l);

            // ── Dashboard ambience — derived, never hard-coded ──────
            // The dashboards consume these via DynamicResource, so the
            // whole analytics surface follows the theme AND the accent.
            PublishDashAmbience(resources, core, dark);

            if (raiseEvent)
                AccentChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Re-derives the About / Liquid palette from the active accent.
        /// This keeps the About module's crystal chrome, tab strip,
        /// chips, orbs and shared iOS Liquid Glass family resources
        /// aligned with live accent changes instead of freezing them to
        /// the theme's original blue.
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

            if (dark)
            {
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(topTint, 0x34), 0.00));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(topTint, 0x2C), 0.06));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(ColorMath.Mix(topTint,
                        Color.FromRgb(255, 255, 255), 0.05), 0x40), 0.16));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(ColorMath.Mix(topTint, deepTint, 0.5), 0x20), 0.50));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(deepTint, 0x16), 0.93));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.WithAlpha(ColorMath.Mix(deepTint,
                        Color.FromRgb(255, 255, 255), 0.06), 0x1B), 1.00));
            }
            else
            {
                card.GradientStops.Add(new GradientStop(topTint, 0.00));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.Mix(topTint, deepTint, 0.25), 0.06));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.Mix(topTint, Color.FromRgb(255, 255, 255), 0.08), 0.16));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.Mix(topTint, deepTint, 0.5), 0.50));
                card.GradientStops.Add(new GradientStop(deepTint, 0.93));
                card.GradientStops.Add(new GradientStop(
                    ColorMath.Mix(deepTint, Color.FromRgb(255, 255, 255), 0.10), 1.00));
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

            Color panelFill = dark
                ? ColorMath.WithAlpha(ColorMath.Tint(accent, Color.FromRgb(255, 255, 255), 0.20), 0x24)
                : ColorMath.Tint(accent, Color.FromRgb(255, 255, 255), 0.08);
            Color panelHover = dark
                ? ColorMath.WithAlpha(ColorMath.Tint(accent, Color.FromRgb(255, 255, 255), 0.28), 0x32)
                : ColorMath.Tint(accent, Color.FromRgb(255, 255, 255), 0.14);
            Color panelBorder = dark
                ? ColorMath.WithAlpha(accent, 0x58)
                : ColorMath.Tint(accent, surface, 0.42);
            Color panelBorderStrong = dark
                ? ColorMath.WithAlpha(ColorMath.Lighten(accent, 0.08), 0x84)
                : ColorMath.FromHsl(h, Math.Max(0.48, s * 0.82), Math.Min(0.58, Math.Max(0.46, l + 0.02)));
            Color cardBorder = dark
                ? ColorMath.WithAlpha(ColorMath.Lighten(accent, 0.12), 0x78)
                : ColorMath.Tint(accent, Color.FromRgb(255, 255, 255), 0.26);
            Color divider = dark
                ? ColorMath.WithAlpha(accent, 0x2C)
                : ColorMath.Tint(accent, surface, 0.22);

            FreezeAndSet(resources, "AboutInteractiveBrush", panelFill);
            FreezeAndSet(resources, "AboutInteractiveHoverBrush", panelHover);
            FreezeAndSet(resources, "AboutInteractiveBorderBrush", panelBorder);
            FreezeAndSet(resources, "AboutInteractiveBorderStrongBrush", panelBorderStrong);
            FreezeAndSet(resources, "AboutCardBorderBrush", cardBorder);
            FreezeAndSet(resources, "AboutDividerBrush", divider);
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
            // ── GLASSY GUARD ────────────────────────────────────────
            // The iOS Liquid Glass visual family OWNS these four
            // dashboard keys (it renders them as glass). ApplyAccent
            // runs AFTER the engine on every theme change
            // (ApplyVisualTheme order), so without this guard it would
            // repaint the whole dashboard surface OPAQUE on top of the
            // glass transparency — the "dashboards don't accept the
            // setting" bug. DashAccentTextBrush below stays
            // accent-driven in every family.
            bool liquidActive =
                VisualThemeEngine.Current == VisualThemeEngine.LiquidGlass;

            ambient.Freeze();
            if (!liquidActive)
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
            if (!liquidActive)
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
            if (!liquidActive)
                resources["DashAmbientBlob2Brush"] = blob2;

            // Liquid card fill.
            var glass = new SolidColorBrush(
                ColorMath.Tint(core, surface, dark ? 0.06 : 0.03))
            { Opacity = 0.82 };
            glass.Freeze();
            if (!liquidActive)
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
            var resources = Application.Current?.Resources;
            if (resources != null)
            {
                var dict = resources.MergedDictionaries.FirstOrDefault(d =>
                    d.Source != null &&
                    (d.Source.OriginalString.Contains("LightTheme.xaml") ||
                     d.Source.OriginalString.Contains("DarkTheme.xaml")));

                if (dict != null &&
                    dict["AccentBrush"] is SolidColorBrush themeBrush)
                {
                    return themeBrush.Color;
                }
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
            var resources = Application.Current?.Resources;
            if (resources == null) return fallback;

            var dict = resources.MergedDictionaries.FirstOrDefault(d =>
                d.Source != null &&
                (d.Source.OriginalString.Contains("LightTheme.xaml") ||
                 d.Source.OriginalString.Contains("DarkTheme.xaml")));

            if (dict == null) return fallback;
            return dict[key] switch
            {
                SolidColorBrush brush => brush.Color,
                Color color => color,
                _ => fallback
            };
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
        /// "defined" edge.
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

            // Read the theme-provided value rather than hardcoding a colour
            // here, so each theme keeps control of what "strong" and "subtle"
            // mean for its own surfaces.
            string sourceKey = defined ? "OutlineStrongBrush" : "OutlineDefaultBrush";

            if (resources[sourceKey] is SolidColorBrush src)
            {
                FreezeAndSet(resources, "OutlineBrush", src.Color);
            }
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
        public static double CardBorderThickness => _cardBorderThickness;

        /// <summary>
        /// Applies a uniform border thickness to every card / panel via the
        /// shared CardBorderThickness theme resource. Cards reference it with
        /// {StaticResource CardBorderThickness}, so one call restyles the whole
        /// app instead of editing hundreds of individual card declarations.
        /// </summary>
        public static void ApplyCardBorderThickness(double thickness)
        {
            if (thickness < 0.0) thickness = 0.0;
            _cardBorderThickness = thickness;

            var resources = Application.Current?.Resources;
            if (resources == null) return;

            resources["CardBorderThickness"] = new Thickness(thickness);
        }

        /// <summary>
        /// Re-applies the saved card border thickness. Called after a theme
        /// swap because loading a new dictionary restores that theme's default.
        /// </summary>
        public static void ReapplyCardBorderThickness()
            => ApplyCardBorderThickness(_cardBorderThickness);

        private static double _cardCornerRadius = 12;

        /// <summary>Current global card corner radius in DIPs.</summary>
        public static double CardCornerRadius => _cardCornerRadius;

        /// <summary>
        /// Applies one corner radius to every card, input and button via
        /// CardCornerRadius / ControlCornerRadius / CardCornerRadiusLarge.
        /// All three are the SAME value so nothing looks "more round"
        /// than the Appearance slider (default 12).
        /// </summary>
        public static void ApplyCardCornerRadius(double radius)
        {
            if (radius < 0.0) radius = 0.0;
            if (radius > 28.0) radius = 28.0;
            _cardCornerRadius = radius;

            var resources = Application.Current?.Resources;
            if (resources == null) return;

            var cr = new CornerRadius(radius);
            resources["CardCornerRadius"] = cr;
            resources["ControlCornerRadius"] = cr;
            resources["CardCornerRadiusLarge"] = cr;
        }

        /// <summary>
        /// Re-applies the saved card corner radius after a theme swap.
        /// </summary>
        public static void ReapplyCardCornerRadius()
            => ApplyCardCornerRadius(_cardCornerRadius);

        private static string _displayDensity = "Compact";

        public static string DisplayDensity => _displayDensity;

        /// <summary>
        /// One switch restyles shared chrome for laptop vs desktop:
        /// input/button height, title size, KPI size, card padding.
        /// Views bind these tokens — do not edit each screen.
        /// </summary>
        public static void ApplyDisplayDensity(string density)
        {
            string key = (density ?? "Compact").Trim();
            if (key is not ("Compact" or "Normal" or "Comfortable"))
                key = "Compact";
            _displayDensity = key;

            var resources = Application.Current?.Resources;
            if (resources == null) return;

            double inputH, buttonH, rowH, title, kpi, body, buttonMin;
            Thickness cardPad, stripPad, pageMargin;

            switch (key)
            {
                case "Comfortable":
                    inputH = 38; buttonH = 40; rowH = 34;
                    title = 22; kpi = 20; body = 14; buttonMin = 110;
                    cardPad = new Thickness(16, 12, 16, 12);
                    stripPad = new Thickness(18, 12, 18, 12);
                    pageMargin = new Thickness(18, 12, 18, 12);
                    break;
                case "Normal":
                    inputH = 34; buttonH = 36; rowH = 30;
                    title = 20; kpi = 18; body = 13; buttonMin = 96;
                    cardPad = new Thickness(12, 9, 12, 9);
                    stripPad = new Thickness(14, 9, 14, 9);
                    pageMargin = new Thickness(14, 10, 14, 10);
                    break;
                default: // Compact — default, slightly tighter than before
                    inputH = 28; buttonH = 30; rowH = 26;
                    title = 17; kpi = 16; body = 13; buttonMin = 84;
                    cardPad = new Thickness(8, 6, 8, 6);
                    stripPad = new Thickness(10, 6, 10, 6);
                    pageMargin = new Thickness(10, 6, 10, 6);
                    break;
            }

            resources["DensityInputHeight"] = inputH;
            resources["DensityButtonHeight"] = buttonH;
            resources["DensityRowHeight"] = rowH;
            resources["DensityTitleFontSize"] = title;
            resources["DensityKpiValueFontSize"] = kpi;
            resources["DensityBodyFontSize"] = body;
            resources["DensityButtonMinWidth"] = buttonMin;
            resources["DensityCardPadding"] = cardPad;
            resources["DensityStripPadding"] = stripPad;
            resources["DensityPageMargin"] = pageMargin;
        }

        public static void ReapplyDisplayDensity()
            => ApplyDisplayDensity(_displayDensity);

        private static double _uiScale = 1.0;

        /// <summary>User zoom 0.80–1.25. 1.0 = native layout (reflow).</summary>
        public static double UiScale => _uiScale;

        public static void ApplyUiScale(double scale)
        {
            scale = Viewport.ViewportService.ClampUiScale(scale);
            if (Math.Abs(_uiScale - scale) < 0.001) return;
            _uiScale = scale;

            var resources = Application.Current?.Resources;
            if (resources != null)
                resources["UiScale"] = scale;

            UiScaleChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void FreezeAndSet(
            ResourceDictionary resources, string key, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();                 // frozen = thread-safe + fast
            resources[key] = brush;
        }

        private static void FreezeAndSetBrush(
            ResourceDictionary resources, string key, Brush brush)
        {
            if (brush.CanFreeze) brush.Freeze();
            resources[key] = brush;
        }
    }
}
