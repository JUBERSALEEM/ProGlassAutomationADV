// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/VisualThemeEngine.cs
// PROJECT: ProGlassAutomation
// MODULE: Dynamic Theme Engine — visual theme families (v3)
//
// Three complete visual languages layered ON TOP of the Light/Dark
// base themes — the base brushes are derived, never hard-coded twice:
//
//   Normal        — clean, practical default (no surface overrides).
//   Monet         — colour-adaptive artistic look (Material-You style):
//                   the palette is DERIVED from the user's accent
//                   colour — hue-tinted glass surfaces, tinted fallback
//                   gradient — and re-derives live when the accent
//                   changes. Wallpaper reads through the glass.
//   Professional  — premium corporate finish: cool slate gradient
//                   window, crisp cooler surfaces, tighter radii,
//                   restrained hover — visibly distinct from Normal.
//
// SYSTEM-WIDE WALLPAPER (v3):
//   A single global wallpaper can be set in Settings and now applies
//   to EVERY view in EVERY family: it is layered into the module area
//   (ModuleWallpaperBrush) behind the content, with surfaces made
//   translucent so it reads through. Per-module wallpapers still win
//   where configured. Typography contrast is strengthened whenever a
//   wallpaper shows through so text stays readable.
//
// Overrides are applied at APPLICATION level (they win over the merged
// theme dictionaries) and are removed when switching back to Normal —
// so nothing is ever destroyed, only layered.
//
// PUBLIC API (preserved): Normal/Monet/Professional constants,
//   Current, CurrentModule, Apply(family, wallpaperPath, opacity),
//   ApplyModule(moduleKey, appearance).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>Applies/removes visual theme families at runtime.</summary>
    public static class VisualThemeEngine
    {
        public const string Normal = "Normal";
        public const string Monet = "Monet";
        public const string Professional = "Professional";
        public const string Glass = "Glass";
        public const string Ocean = "Ocean";

        /// <summary>Keys the engine may override (removed on Normal).</summary>
        private static readonly string[] ManagedKeys =
        {
            "WindowBackgroundBrush", "ModuleBackgroundBrush",
            "ElevatedSurfaceBrush", "SideMenuBackgroundBrush",
            "MenuHoverBrush",
            "SecondaryForegroundBrush", "MutedForegroundBrush",
            "CardCornerRadius", "CardCornerRadiusLarge",
            "ControlCornerRadius",
            "WallpaperOpacityValue",
            "ModuleWallpaperBrush",

            // The card keyline. Monet and Professional recolour every
            // surface, so a keyline fixed by the base theme stops
            // relating to the surface it outlines and the cards read as
            // flat. These families now derive it from their own palette
            // and it is removed again on Normal, exactly like the
            // surfaces above.

            // NOTE: the four keyline keys - OutlineBrush,
            // OutlineDefaultBrush, BorderBrush, DividerBrush - are
            // deliberately NOT listed here.
            //
            // ThemeManager.ApplyAccent derives them from the accent hue
            // and writes them into the same application-level dictionary.
            // Listing them would make ClearOverrides delete the
            // accent-derived values every time a visual family is applied
            // - including on Normal, which is the family most users run -
            // and the cards would drop back to the base theme grey. That
            // is precisely the bug this whole sequence of changes was
            // chasing. Leave them out.
        };

        /// <summary>Currently active family.</summary>
        public static string Current { get; private set; } = Normal;

        /// <summary>Module whose wallpaper is currently layered (or "").</summary>
        public static string CurrentModule { get; private set; } = "";

        /// <summary>
        /// Last module key the engine was asked to render — preserved
        /// across re-applies (accent change, opacity drag, theme swap)
        /// so that module's own wallpaper override is restored instead
        /// of being forgotten.
        /// </summary>
        public static string LastRequestedModule { get; private set; } = "";

        // Last wallpaper the engine was asked to use — lets the accent
        // re-apply (Monet) rebuild the exact same scene.
        private static string _lastWallpaperPath = "";
        private static double _lastWallpaperOpacity = 0.85;

        // Phase 4: Named handler for static event — detachable, no lambda leak
        private static readonly EventHandler _accentChangedHandler = OnAccentChanged;

        /// <summary>
        /// Re-derive Monet's accent-tinted palette whenever the user
        /// changes the accent colour, then re-layer the wallpaper of
        /// the module on screen so nothing disappears. Subscribed
        /// exactly once (static engine lives for the whole process).
        /// Phase 4: Now named handler + WeakEventManager option.
        /// </summary>
        static VisualThemeEngine()
        {
            ThemeManager.AccentChanged += _accentChangedHandler;
        }

        public static void Unsubscribe()
        {
            try { ThemeManager.AccentChanged -= _accentChangedHandler; } catch { }
        }

        private static void OnAccentChanged(object? sender, EventArgs e)
        {
            if (Current != Monet) return;
            Apply(Monet, _lastWallpaperPath, _lastWallpaperOpacity);
            ApplyModule(LastRequestedModule, App.Config?.Appearance);
        }

        /// <summary>
        /// Applies one family. wallpaperPath/opacity feed the Monet
        /// window background and the system-wide wallpaper layer.
        /// Never throws.
        /// </summary>
        public static void Apply(
            string family, string wallpaperPath = "",
            double wallpaperOpacity = 0.85)
        {
            try
            {
                var resources = Application.Current?.Resources;
                if (resources == null) return;

                _lastWallpaperPath = wallpaperPath ?? "";
                _lastWallpaperOpacity = ClampOpacity(wallpaperOpacity);

                ClearOverrides(resources);
                Current = Normalize(family);

                switch (Current)
                {
                    case Monet:
                        ApplyMonet(resources, _lastWallpaperPath,
                            _lastWallpaperOpacity);
                        break;

                    case Professional:
                        ApplyProfessional(resources);
                        break;

                    case Glass:
                        ApplyGlass(resources);
                        break;

                    case Ocean:
                        ApplyOcean(resources);
                        break;
                }

                // System-wide wallpaper chrome: paint the window
                // background for non-Monet families (Monet handles it
                // above). The module-area layer is owned by
                // ApplyModule, which callers invoke right after this —
                // that keeps module-specific wallpapers authoritative.
                if (Current != Monet &&
                    !string.IsNullOrWhiteSpace(_lastWallpaperPath) &&
                    File.Exists(_lastWallpaperPath))
                {
                    var windowBrush = BuildWallpaperBrush(
                        _lastWallpaperPath, _lastWallpaperOpacity);
                    if (windowBrush != null)
                        resources["WindowBackgroundBrush"] = windowBrush;
                }

                AppLogService.Info(
                    $"Visual theme applied: {Current}",
                    new LogContext
                    {
                        Module = "Themes",
                        Class = nameof(VisualThemeEngine),
                        Operation = "Apply"
                    });
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Visual theme apply failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Themes",
                        Class = nameof(VisualThemeEngine)
                    });
            }
        }

        // ── PER-MODULE WALLPAPER (§9) ────────────────────────────────────

        /// <summary>
        /// Called on every module navigation. Rebuilds the base family
        /// first, then layers that module's wallpaper (when configured)
        /// — otherwise the system-wide wallpaper — so every screen can
        /// carry a background image.
        /// </summary>
        public static void ApplyModule(
            string moduleKey,
            AI.AIConfig.AppearanceSettings appearance)
        {
            try
            {
                var resources = Application.Current?.Resources;
                if (resources == null) return;

                LastRequestedModule = moduleKey ?? "";

                _lastWallpaperPath = appearance?.WallpaperPath ?? "";
                _lastWallpaperOpacity =
                    ClampOpacity(appearance?.WallpaperOpacity ?? 0.85);

                // Rebuild the family baseline (removes the previous
                // module's wallpaper layer).
                ClearOverrides(resources);
                Current = Normalize(appearance?.VisualTheme ?? Normal);

                switch (Current)
                {
                    case Monet:
                        ApplyMonet(resources, _lastWallpaperPath,
                            _lastWallpaperOpacity);
                        break;
                    case Professional:
                        ApplyProfessional(resources);
                        break;
                    case Glass:
                        ApplyGlass(resources);
                        break;
                    case Ocean:
                        ApplyOcean(resources);
                        break;
                }

                if (Current != Monet &&
                    !string.IsNullOrWhiteSpace(_lastWallpaperPath) &&
                    File.Exists(_lastWallpaperPath))
                {
                    var windowBrush = BuildWallpaperBrush(
                        _lastWallpaperPath, _lastWallpaperOpacity);
                    if (windowBrush != null)
                        resources["WindowBackgroundBrush"] = windowBrush;
                }

                CurrentModule = "";

                // 1) Module-specific wallpaper wins when enabled.
                if (!string.IsNullOrEmpty(moduleKey) && appearance != null &&
                    appearance.ModuleWallpapers.TryGetValue(
                        moduleKey, out var moduleWallpaper) &&
                    moduleWallpaper != null &&
                    moduleWallpaper.Enabled &&
                    File.Exists(moduleWallpaper.Path))
                {
                    var brush = BuildWallpaperBrush(
                        moduleWallpaper.Path,
                        ClampOpacity(moduleWallpaper.Opacity));

                    if (brush != null)
                    {
                        resources["ModuleWallpaperBrush"] = brush;
                        CurrentModule = moduleKey;

                        // Let the image read through the module surface.
                        SetTranslucent(resources, "ModuleBackgroundBrush",
                            ThemeManager.GetThemeColor(
                                "ModuleBackgroundBrush", Colors.White), 0.78);
                        AdaptTypographyForWallpaper(resources);
                        return;
                    }
                }

                // 2) Otherwise the system-wide wallpaper shows in
                //    every view.
                if (!string.IsNullOrWhiteSpace(_lastWallpaperPath) &&
                    File.Exists(_lastWallpaperPath))
                {
                    LayerWallpaperBehindContent(
                        resources, _lastWallpaperPath,
                        _lastWallpaperOpacity, translucentSurface: 0.82);
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Module wallpaper apply failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Themes",
                        Class = nameof(VisualThemeEngine)
                    });
            }
        }

        // ── SYSTEM-WIDE WALLPAPER LAYER ─────────────────────────────────

        /// <summary>
        /// Puts one image behind the content of the framed module area
        /// and lets it read through: the module surface becomes
        /// translucent and typography contrast is strengthened so text
        /// stays professional and readable over the picture.
        /// </summary>
        private static void LayerWallpaperBehindContent(
            ResourceDictionary resources, string path, double opacity,
            double translucentSurface)
        {
            var brush = BuildWallpaperBrush(path, opacity);
            if (brush == null) return;

            resources["ModuleWallpaperBrush"] = brush;

            SetTranslucent(resources, "ModuleBackgroundBrush",
                ThemeManager.GetThemeColor(
                    "ModuleBackgroundBrush", Colors.White),
                translucentSurface);

            AdaptTypographyForWallpaper(resources);
        }

        /// <summary>
        /// Text over a wallpaper loses contrast once surfaces turn
        /// translucent. Strengthen the secondary/muted foregrounds
        /// (theme-aware) so captions and hints stay crisp. Removed
        /// again by ClearOverrides when no wallpaper shows.
        /// </summary>
        private static void AdaptTypographyForWallpaper(
            ResourceDictionary resources)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            var secondary = new SolidColorBrush(dark
                ? Color.FromRgb(0xB8, 0xC4, 0xD6)   // stronger on dark
                : Color.FromRgb(0x4B, 0x55, 0x63)); // stronger on light
            secondary.Freeze();
            resources["SecondaryForegroundBrush"] = secondary;

            var muted = new SolidColorBrush(dark
                ? Color.FromRgb(0x93, 0xA2, 0xB8)
                : Color.FromRgb(0x6B, 0x72, 0x80));
            muted.Freeze();
            resources["MutedForegroundBrush"] = muted;
        }

        private static ImageBrush? BuildWallpaperBrush(
            string path, double opacity)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();

                var brush = new ImageBrush(image)
                {
                    Stretch = Stretch.UniformToFill,
                    Opacity = opacity
                };
                brush.Freeze();
                return brush;
            }
            catch
            {
                return null;
            }
        }

        // ── MONET — colour-adaptive glass (Material-You style) ──────────

        private static void ApplyMonet(
            ResourceDictionary resources, string wallpaperPath,
            double opacity)
        {
            // The whole palette derives from the active accent colour.
            var accent = ThemeManager.AccentColor;
            var (h, s, _) = ColorMath.ToHsl(accent);
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            // Window background = wallpaper, else an accent-hue
            // gradient (no more hard-coded blue).
            if (!string.IsNullOrWhiteSpace(wallpaperPath) &&
                File.Exists(wallpaperPath))
            {
                var wallpaper = BuildWallpaperBrush(wallpaperPath, opacity);
                if (wallpaper != null)
                    resources["WindowBackgroundBrush"] = wallpaper;
            }
            else
            {
                Color from = dark
                    ? ColorMath.FromHsl(h, Math.Min(s, 0.35), 0.14)
                    : ColorMath.FromHsl(h, Math.Min(s, 0.60), 0.94);
                Color to = dark
                    ? ColorMath.FromHsl(h + 26, Math.Min(s, 0.32), 0.10)
                    : ColorMath.FromHsl(h + 26, Math.Min(s, 0.55), 0.88);

                var soft = new LinearGradientBrush(
                    from, to, new Point(0, 0), new Point(1, 1));
                soft.Freeze();
                resources["WindowBackgroundBrush"] = soft;
            }

            // Glass surfaces — the active base theme's colours tinted
            // toward the accent hue, then made translucent so the
            // wallpaper reads through. Tint strength is deliberately
            // visible so the colour adaptation is unmistakable.
            resources["ModuleBackgroundBrush"] = GlassSurface(
                resources, "ModuleBackgroundBrush", accent,
                Colors.White, 0.74, dark);
            resources["ElevatedSurfaceBrush"] = GlassSurface(
                resources, "ElevatedSurfaceBrush", accent,
                Colors.White, 0.82, dark);
            resources["SideMenuBackgroundBrush"] = GlassSurface(
                resources, "SideMenuBackgroundBrush", accent,
                Colors.White, 0.88, dark);
            resources["MenuHoverBrush"] = GlassSurface(
                resources, "MenuHoverBrush", accent,
                Color.FromRgb(0xF3, 0xF4, 0xF6), 0.62, dark);

            // Softer, rounder cards fit the artistic look.
            resources["CardCornerRadius"] = new CornerRadius(18);
            resources["CardCornerRadiusLarge"] = new CornerRadius(24);
            resources["ControlCornerRadius"] = new CornerRadius(12);

            // Keyline derived from the SAME accent hue as the glass
            // surfaces, so the outline belongs to the material instead
            // of sitting on it as a foreign grey. Lightness is pinned to
            // a value that keeps roughly 2-3:1 against the card it
            // outlines: visible as a hairline, never a heavy box.
            ApplyKeyline(resources,
                ColorMath.FromHsl(h, Math.Min(s, 0.28),
                    dark ? 0.46 : 0.62));
        }

        /// <summary>
        /// Writes the card keyline for a visual family.
        /// <para>
        /// Sets BOTH OutlineBrush (what every card binds) and
        /// OutlineDefaultBrush (the restore value the Defined Card Edges
        /// toggle reads). Without the second one, turning the toggle off
        /// would snap the keyline back to the base theme's grey and lose
        /// the family's derived colour.
        /// </para>
        /// </summary>
        private static void ApplyKeyline(
            ResourceDictionary resources, Color keyline)
        {
            // Intentionally a no-op for the outline keys.
            //
            // Monet and Professional used to derive their own neutral
            // keyline here. ThemeManager.ApplyAccent now owns all four
            // keyline keys and derives them from the accent hue, which is
            // what makes every card match the Settings "Appearance" cards.
            // Both run on every theme change, and ApplyAccent runs LAST,
            // so anything written here would simply be overwritten - and
            // when it was not (module wallpaper re-layer, which calls the
            // engine on its own) it produced a grey edge on one screen and
            // an accent edge on the next.
            //
            // The parameter is kept so the two call sites still document
            // which family they belong to, and so restoring a
            // family-specific tweak later is a one-line change.
            _ = resources;
            _ = keyline;
        }

        /// <summary>
        /// Builds one Monet glass surface: theme colour → gently tinted
        /// with the accent hue → translucent.
        /// </summary>
        private static SolidColorBrush GlassSurface(
            ResourceDictionary resources, string key, Color accent,
            Color fallback, double opacity, bool dark)
        {
            Color baseColor = ThemeManager.GetThemeColor(key, fallback);
            Color tinted = ColorMath.Tint(accent, baseColor,
                dark ? 0.18 : 0.12);

            var brush = new SolidColorBrush(tinted) { Opacity = opacity };
            brush.Freeze();
            return brush;
        }

        // ── PROFESSIONAL — premium corporate ───────────────────────────

        private static void ApplyProfessional(ResourceDictionary resources)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            // Cool slate window gradient — immediately distinguishes
            // Professional from Normal.
            var window = new LinearGradientBrush(
                dark ? Color.FromRgb(0x0B, 0x12, 0x20)
                     : Color.FromRgb(0xF8, 0xFA, 0xFC),
                dark ? Color.FromRgb(0x10, 0x18, 0x27)
                     : Color.FromRgb(0xEC, 0xF1, 0xF7),
                new Point(0, 0), new Point(1, 1));
            window.Freeze();
            resources["WindowBackgroundBrush"] = window;

            // Crisp, clearly cooler surfaces (all views consume these
            // via DynamicResource, so the change is global). The mix
            // ratios are strong enough to be seen at a glance — this
            // is what separates Professional from Normal.
            Color cool = dark
                ? Color.FromRgb(0x13, 0x1B, 0x2A)
                : Color.FromRgb(0xEA, 0xF0, 0xF7);

            Color moduleBg = ThemeManager.GetThemeColor(
                "ModuleBackgroundBrush", Colors.White);
            var moduleBrush = new SolidColorBrush(
                ColorMath.Mix(moduleBg, cool, 0.55));
            moduleBrush.Freeze();
            resources["ModuleBackgroundBrush"] = moduleBrush;

            Color elevated = ThemeManager.GetThemeColor(
                "ElevatedSurfaceBrush", Colors.White);
            var elevatedBrush = new SolidColorBrush(
                ColorMath.Mix(elevated, cool, 0.45));
            elevatedBrush.Freeze();
            resources["ElevatedSurfaceBrush"] = elevatedBrush;

            Color sideMenu = ThemeManager.GetThemeColor(
                "SideMenuBackgroundBrush", Colors.White);
            var sideMenuBrush = new SolidColorBrush(
                ColorMath.Mix(sideMenu, cool, 0.40));
            sideMenuBrush.Freeze();
            resources["SideMenuBackgroundBrush"] = sideMenuBrush;

            // Tighter radii + restrained hover tint.
            resources["CardCornerRadius"] = new CornerRadius(10);
            resources["CardCornerRadiusLarge"] = new CornerRadius(14);
            resources["ControlCornerRadius"] = new CornerRadius(8);

            var hover = new SolidColorBrush(dark
                ? Color.FromRgb(0x2B, 0x3A, 0x52)
                : Color.FromRgb(0xF1, 0xF5, 0xF9));
            hover.Freeze();
            resources["MenuHoverBrush"] = hover;

            // Cool slate keyline to match the cooled surfaces above.
            // Professional is the "premium corporate" family, so the
            // edge is a touch crisper than Monet's.
            ApplyKeyline(resources, dark
                ? Color.FromRgb(0x5A, 0x6B, 0x82)
                : Color.FromRgb(0x8C, 0x9C, 0xB0));
        }

        // ── GLASS — frosted translucency (premium glass identity) ─────

        private static void ApplyGlass(ResourceDictionary resources)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            // Cool glass stage gradient.
            var window = new LinearGradientBrush(
                dark ? Color.FromRgb(0x0B, 0x14, 0x2A)
                     : Color.FromRgb(0xEE, 0xF4, 0xFB),
                dark ? Color.FromRgb(0x12, 0x22, 0x44)
                     : Color.FromRgb(0xE4, 0xED, 0xF9),
                new Point(0, 0), new Point(1, 1));
            window.Freeze();
            resources["WindowBackgroundBrush"] = window;

            // Frosted surfaces — translucent and lightly accent-tinted so
            // the stage gradient reads through each card (the glass look).
            var accent = ThemeManager.AccentColor;
            resources["ModuleBackgroundBrush"] = GlassSurface(
                resources, "ModuleBackgroundBrush", accent,
                Colors.White, dark ? 0.86 : 0.78, dark);
            resources["ElevatedSurfaceBrush"] = GlassSurface(
                resources, "ElevatedSurfaceBrush", accent,
                Colors.White, dark ? 0.92 : 0.84, dark);
            resources["SideMenuBackgroundBrush"] = GlassSurface(
                resources, "SideMenuBackgroundBrush", accent,
                Colors.White, dark ? 0.94 : 0.86, dark);
            resources["MenuHoverBrush"] = GlassSurface(
                resources, "MenuHoverBrush", accent,
                Color.FromRgb(0xF3, 0xF4, 0xF6), dark ? 0.60 : 0.55, dark);

            // Softer radii suit the frosted material.
            resources["CardCornerRadius"] = new CornerRadius(14);
            resources["CardCornerRadiusLarge"] = new CornerRadius(18);
            resources["ControlCornerRadius"] = new CornerRadius(10);

            ApplyKeyline(resources, dark
                ? Color.FromRgb(0x4A, 0x5E, 0x7E)
                : Color.FromRgb(0x9C, 0xB2, 0xD0));
        }

        // ── OCEAN — deep teal corporate ───────────────────────────────

        private static void ApplyOcean(ResourceDictionary resources)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            var window = new LinearGradientBrush(
                dark ? Color.FromRgb(0x07, 0x1A, 0x24)
                     : Color.FromRgb(0xEC, 0xF7, 0xF9),
                dark ? Color.FromRgb(0x0A, 0x24, 0x33)
                     : Color.FromRgb(0xDD, 0xF0, 0xF4),
                new Point(0, 0), new Point(1, 1));
            window.Freeze();
            resources["WindowBackgroundBrush"] = window;

            Color teal = dark
                ? Color.FromRgb(0x0E, 0x2A, 0x38)
                : Color.FromRgb(0xD6, 0xEC, 0xF1);

            Color moduleBg = ThemeManager.GetThemeColor(
                "ModuleBackgroundBrush", Colors.White);
            var moduleBrush = new SolidColorBrush(
                ColorMath.Mix(moduleBg, teal, 0.55));
            moduleBrush.Freeze();
            resources["ModuleBackgroundBrush"] = moduleBrush;

            Color elevated = ThemeManager.GetThemeColor(
                "ElevatedSurfaceBrush", Colors.White);
            var elevatedBrush = new SolidColorBrush(
                ColorMath.Mix(elevated, teal, 0.45));
            elevatedBrush.Freeze();
            resources["ElevatedSurfaceBrush"] = elevatedBrush;

            Color sideMenu = ThemeManager.GetThemeColor(
                "SideMenuBackgroundBrush", Colors.White);
            var sideMenuBrush = new SolidColorBrush(
                ColorMath.Mix(sideMenu, teal, 0.40));
            sideMenuBrush.Freeze();
            resources["SideMenuBackgroundBrush"] = sideMenuBrush;

            resources["CardCornerRadius"] = new CornerRadius(12);
            resources["CardCornerRadiusLarge"] = new CornerRadius(16);
            resources["ControlCornerRadius"] = new CornerRadius(8);

            var hover = new SolidColorBrush(dark
                ? Color.FromRgb(0x14, 0x35, 0x47)
                : Color.FromRgb(0xE4, 0xF2, 0xF6));
            hover.Freeze();
            resources["MenuHoverBrush"] = hover;

            ApplyKeyline(resources, dark
                ? Color.FromRgb(0x2E, 0x5A, 0x6E)
                : Color.FromRgb(0x7F, 0xB4, 0xC4));
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static void ClearOverrides(ResourceDictionary resources)
        {
            foreach (var key in ManagedKeys)
                resources.Remove(key);
        }

        private static void SetTranslucent(
            ResourceDictionary resources, string key,
            Color baseColor, double opacity)
        {
            var brush = new SolidColorBrush(baseColor)
            {
                Opacity = opacity
            };
            brush.Freeze();
            resources[key] = brush;
        }

        private static string Normalize(string family)
        {
            return family?.Trim() switch
            {
                Monet => Monet,
                Professional => Professional,
                Glass => Glass,
                Ocean => Ocean,
                _ => Normal
            };
        }

        private static double ClampOpacity(double value) =>
            value < 0.3 ? 0.3 : value > 1.0 ? 1.0 : value;
    }
}
