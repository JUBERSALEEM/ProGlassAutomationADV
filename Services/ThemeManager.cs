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
        /// User-chosen accent. Null = follow the theme's built-in blue.
        /// Persisted by callers if desired (SettingsViewModel).
        /// </summary>
        private static Color? _customAccent;

        public static AppTheme CurrentTheme => _currentTheme;

        /// <summary>The accent colour currently in effect.</summary>
        public static Color AccentColor { get; private set; } =
            Color.FromRgb(0x1D, 0x4E, 0xD8);

        public static event EventHandler ThemeChanged;

        /// <summary>Raised after the accent family is (re)derived.</summary>
        public static event EventHandler AccentChanged;

        // ══════════════════════════════════════════════════════════════
        //  THEME SWITCHING (original API — behaviour preserved)
        // ══════════════════════════════════════════════════════════════

        public static void ApplyTheme(AppTheme theme)
        {
            _currentTheme = theme;

            string themeUri = theme == AppTheme.Dark
                ? "pack://application:,,,/Themes/DarkTheme.xaml"
                : "pack://application:,,,/Themes/LightTheme.xaml";

            var newDict = new ResourceDictionary
            {
                Source = new Uri(themeUri, UriKind.Absolute)
            };

            var appDicts = Application.Current.Resources.MergedDictionaries;

            // Remove existing theme dictionaries
            var existing = appDicts.FirstOrDefault(d =>
                d.Source != null &&
                (d.Source.OriginalString.Contains("LightTheme.xaml") ||
                 d.Source.OriginalString.Contains("DarkTheme.xaml")));

            if (existing != null)
                appDicts.Remove(existing);

            appDicts.Add(newDict);

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
        /// Pass null to follow the theme's default blue.
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

            FreezeAndSet(resources, "TintBlueBrush",
                ColorMath.Tint(core, surface, dark ? 0.16 : 0.08));
            FreezeAndSet(resources, "TintIndigoBrush",
                ColorMath.Tint(
                    ColorMath.FromHsl(h + 18, s, l), surface,
                    dark ? 0.18 : 0.09));

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

            // ── Dashboard ambience — derived, never hard-coded ──────
            // The dashboards consume these via DynamicResource, so the
            // whole analytics surface follows the theme AND the accent.
            PublishDashAmbience(resources, core, dark);

            if (raiseEvent)
                AccentChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Derives the dashboard ambience family (ambient gradient,
        /// accent blobs, glass surfaces, accent text) from the active
        /// accent + theme. Published at application level so every
        /// dashboard's DynamicResource references update instantly and
        /// stay correct across light/dark swaps and accent changes.
        /// </summary>
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

            // Glass card fill.
            var glass = new SolidColorBrush(
                ColorMath.Tint(core, surface, dark ? 0.06 : 0.03))
            { Opacity = 0.82 };
            glass.Freeze();
            resources["DashGlassSurfaceBrush"] = glass;

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
                ? Color.FromRgb(0x3B, 0x82, 0xF6)
                : Color.FromRgb(0x1D, 0x4E, 0xD8);
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

            return dict != null && dict[key] is SolidColorBrush brush
                ? brush.Color
                : fallback;
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

        private static void FreezeAndSet(
            ResourceDictionary resources, string key, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();                 // frozen = thread-safe + fast
            resources[key] = brush;
        }
    }
}
