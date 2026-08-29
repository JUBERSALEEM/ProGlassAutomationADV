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
//   iOS Liquid Glass — the app's DEFAULT look (2026-08-29 rewrite):
//                   one coherent crystal design system that adapts to
//                   Light and Dark. The window paints its own OPAQUE
//                   aurora stage (deep navy in Dark, sky-ice in Light)
//                   and every surface becomes a translucent frosted
//                   pane with crystal edges, top reflections and soft
//                   shadows — self-contained, identical on every
//                   machine, no OS backdrop dependency. (The old
//                   "Glass"/"Glassy" families were removed; saved
//                   configs referencing them resolve to this family.)
//   Ocean         — deep teal corporate.
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
        /// <summary>The default family — iOS Liquid Glass (2026-08-29).</summary>
        public const string LiquidGlass = "iOS Liquid Glass";
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

            // Status-tint cards/chips (InfoCard + the BrushKeyConverter
            // chips in dashboards/lists). The iOS Liquid Glass family
            // renders these as see-through hue glass (hue kept, alpha
            // dropped), so the overrides must be cleaned up on family
            // switch exactly like the surfaces above.
            "TintIndigoBrush", "TintBlueBrush", "TintGreenBrush",
            "TintRedBrush", "TintAmberBrush", "TintPurpleBrush",
            "TintGrayBrush",

            // Dashboard glass + ambient layer, and the shell (top bar)
            // glass — the iOS family re-skins these to the glass stage.
            "DashGlassSurfaceBrush", "DashAmbientBrush",
            "DashAmbientBlob1Brush", "DashAmbientBlob2Brush",
            "ShellGlassBrush", "SidebarCardBrush",

            // Liquid Glass design system tokens (2026-08-29): the
            // Glass/Glassy families write accent-tinted crystal values;
            // every other family restores the theme-file defaults.
            "LiquidGlassBrush", "LiquidGlassSubtleBrush",
            "LiquidGlassStrongBrush",
            "GlassEdgeBrush", "GlassInnerHighlightBrush",
            "GlassReflectionBrush",
            "GlassControlHoverBrush", "GlassControlPressedBrush",
            "GlassControlDisabledBrush",
            "GlassFocusBrush", "GlassSelectionBrush",
            "GlassPrimaryButtonBrush", "GlassOverlayBrush",
            "GlassShadowColor",

            // Glass typography: the About page's text palette is
            // applied so text auto-adapts when every card goes
            // transparent.
            "PrimaryForegroundBrush",

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
            try { ThemeManager.AccentChanged -= _accentChangedHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[VisualThemeEngine] AccentChanged detach failed: " + ex.Message); }
        }

        private static void OnAccentChanged(object? sender, EventArgs e)
        {
            // Monet and iOS Liquid Glass derive their surfaces and
            // window stage from the active accent colour, so both must
            // re-derive when the user changes the accent.
            if (Current != Monet && Current != LiquidGlass)
                return;
            Apply(Current, _lastWallpaperPath, _lastWallpaperOpacity);
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

                    case LiquidGlass:
                        ApplyLiquidGlass(resources);
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

                // Last: the global Font Color preference (or its Auto
                // restore) so a user-chosen text palette always wins
                // over whatever the family/wallpaper just wrote.
                ApplyFontPreference(resources, false);

                // Window-level acrylic blur follows the family: Glass and
                // Glassy get the DWM accent backdrop, everything else
                // restores the normal window surface.
                SyncGlassChrome();

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
                    case LiquidGlass:
                        ApplyLiquidGlass(resources);
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

                        // Global font colour preference still wins last.
                        ApplyFontPreference(resources, true);

                        // Window chrome follows the family.
                        SyncGlassChrome();
                        return;
                    }
                }

                // 2) Otherwise the system-wide wallpaper shows in
                //    every view.
                bool systemWallpaperLayered = false;
                if (!string.IsNullOrWhiteSpace(_lastWallpaperPath) &&
                    File.Exists(_lastWallpaperPath))
                {
                    LayerWallpaperBehindContent(
                        resources, _lastWallpaperPath,
                        _lastWallpaperOpacity, translucentSurface: 0.82);
                    systemWallpaperLayered = true;
                }

                // Last: the global Font Color preference (or its Auto
                // restore) runs after the wallpaper typography, so a
                // user-chosen text palette always wins.
                ApplyFontPreference(resources, systemWallpaperLayered);

                // Window-level acrylic blur follows the family.
                SyncGlassChrome();
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
                // 2026-08-25: saturation caps eased up a touch so the
                // background visibly belongs to the chosen accent.
                Color from = dark
                    ? ColorMath.FromHsl(h, Math.Min(s, 0.42), 0.14)
                    : ColorMath.FromHsl(h, Math.Min(s, 0.70), 0.94);
                Color to = dark
                    ? ColorMath.FromHsl(h + 26, Math.Min(s, 0.38), 0.10)
                    : ColorMath.FromHsl(h + 26, Math.Min(s, 0.62), 0.88);

                if (ThemeManager.UseGradients)
                {
                    var soft = new LinearGradientBrush(
                        from, to, new Point(0, 0), new Point(1, 1));
                    soft.Freeze();
                    resources["WindowBackgroundBrush"] = soft;
                }
                else
                {
                    var flat = new SolidColorBrush(from);
                    flat.Freeze();
                    resources["WindowBackgroundBrush"] = flat;
                }
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
        /// Builds one Monet glass surface: theme colour → tinted with
        /// the accent hue → translucent.
        /// <para>
        /// 2026-08-25 (richness fix): the tint ratios were raised from
        /// 0.12/0.18 to 0.18/0.26 — at the old values the glass barely
        /// carried the chosen colour and Monet read as "too light".
        /// 2026-08-29: the LIGHT-mode ratio was nudged 0.18 → 0.22 so
        /// Monet's colour adaptation is clearly visible on the pale
        /// light surfaces too (dark stays 0.26).
        /// When gradient surfaces are enabled (the Appearance toggle)
        /// the surface is a diagonal gradient from the tint to a lifted
        /// tint, which gives Material-You style depth without any
        /// hard-coded colour.
        /// </para>
        /// </summary>
        private static Brush GlassSurface(
            ResourceDictionary resources, string key, Color accent,
            Color fallback, double opacity, bool dark)
        {
            Color baseColor = ThemeManager.GetThemeColor(key, fallback);
            // Light: 0.10 keeps the pane WHITE (water glass) instead of
            // the old 0.22 blue wash. Dark: 0.26 unchanged.
            Color tinted = ColorMath.Tint(accent, baseColor,
                dark ? 0.26 : 0.10);

            if (ThemeManager.UseGradients)
            {
                Color lifted = ColorMath.Lighten(tinted, dark ? 0.04 : 0.05);
                var gradient = new LinearGradientBrush(
                    tinted, lifted, new Point(0, 0), new Point(1, 1))
                {
                    Opacity = opacity,
                };
                gradient.Freeze();
                return gradient;
            }

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

        // ── iOS LIQUID GLASS — the default family (2026-08-29) ────────
        //
        // ONE coherent crystal design system, adaptive to Light/Dark.
        // The previous "Glass" and "Glassy" families were two
        // overlapping, inconsistent attempts — both deleted. This
        // family merges their best parts into a single scene:
        //
        //   * WINDOW — an OPAQUE, self-contained aurora stage: deep
        //     navy + orbs in Dark, sky-ice + orbs in Light. Painting
        //     the scene INSIDE the window (instead of veiling the
        //     surface and hoping the DWM backdrop shows through an
        //     opaque WPF surface) fixed the "full fade white" bug —
        //     the look is identical on every Windows 10/11 machine.
        //   * SURFACES — translucent white panes with a top-lit fade:
        //     high-alpha white water glass in Light, deep frost in
        //     Dark. The stage must stay noticeably more colourful
        //     than the panes — that contrast is what makes glass
        //     read as glass instead of white-on-white.
        //   * STATUS TINTS — hue kept, fill made glass.
        //   * TYPOGRAPHY — About-page text palette in Dark, fixed
        //     ink/slate in Light (decoupled from surface keys).
        //   * LIQUID GLASS TOKENS — crystal edge, reflections and
        //     control states published last so the whole
        //     LiquidGlassStyles.xaml control system re-tints live.

        private static void ApplyLiquidGlass(ResourceDictionary resources)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var accent = ThemeManager.AccentColor;

            // ── 1) WINDOW — the aurora stage (always opaque) ────────
            resources["WindowBackgroundBrush"] =
                BuildLiquidStageBrush(dark, accent);

            // ── 2) CARD SURFACES — one material ramp per mode ───────
            if (dark)
            {
                resources["ModuleBackgroundBrush"] = FrostFill(accent, 0.20, 0.06);
                resources["ElevatedSurfaceBrush"] = FrostFill(accent, 0.26, 0.09);
                resources["SideMenuBackgroundBrush"] = FrostFill(accent, 0.32, 0.11);
                resources["MenuHoverBrush"] = FrostFill(accent, 0.22, 0.11);
                resources["SidebarCardBrush"] = FrostFill(accent, 0.24, 0.09);
                resources["ShellGlassBrush"] = FrostFill(accent, 0.28, 0.09);
                resources["DashGlassSurfaceBrush"] = FrostFill(accent, 0.22, 0.09);

                // ── 3) DASHBOARD AMBIENT — soft glow under the frost ─
                resources["DashAmbientBrush"] = FrostFill(accent, 0.14, 0.06);
                resources["DashAmbientBlob1Brush"] = SolidGlass(accent, 0.10);
                resources["DashAmbientBlob2Brush"] = SolidGlass(accent, 0.08);
            }
            else
            {
                // White water glass: translucent white panes, densest
                // at the top (iOS light materials catch the light),
                // see-through toward the lower edge.
                resources["ModuleBackgroundBrush"] = FrostFill(accent, 0.84, 0.54);
                resources["ElevatedSurfaceBrush"] = FrostFill(accent, 0.90, 0.62);
                resources["SideMenuBackgroundBrush"] = FrostFill(accent, 0.94, 0.72);
                resources["MenuHoverBrush"] = FrostFill(accent, 0.50, 0.30);
                resources["SidebarCardBrush"] = FrostFill(accent, 0.82, 0.52);
                resources["ShellGlassBrush"] = FrostFill(accent, 0.88, 0.58);
                resources["DashGlassSurfaceBrush"] = FrostFill(accent, 0.84, 0.56);

                // ── 3) DASHBOARD AMBIENT — blue glow under the panes ─
                resources["DashAmbientBrush"] = FrostFill(accent, 0.20, 0.08);
                resources["DashAmbientBlob1Brush"] = SolidGlass(accent, 0.16);
                resources["DashAmbientBlob2Brush"] = SolidGlass(accent, 0.12);
            }

            // ── 4) STATUS TINTS — hue kept, fill made glass ─────────
            resources["TintIndigoBrush"] = HueGlass("TintIndigoBrush", 0.30, 0.14);
            resources["TintBlueBrush"] = HueGlass("TintBlueBrush", 0.30, 0.14);
            resources["TintGreenBrush"] = HueGlass("TintGreenBrush", 0.30, 0.14);
            resources["TintRedBrush"] = HueGlass("TintRedBrush", 0.30, 0.14);
            resources["TintAmberBrush"] = HueGlass("TintAmberBrush", 0.30, 0.14);
            resources["TintPurpleBrush"] = HueGlass("TintPurpleBrush", 0.30, 0.14);
            resources["TintGrayBrush"] = HueGlass("TintGrayBrush", 0.30, 0.14);

            // ── 5) TYPOGRAPHY — per-mode text palette ───────────────
            SetLiquidTextPalette(resources, dark);

            // ── 6) SHAPE + KEYLINE ──────────────────────────────────
            resources["CardCornerRadius"] = new CornerRadius(14);
            resources["CardCornerRadiusLarge"] = new CornerRadius(18);
            resources["ControlCornerRadius"] = new CornerRadius(10);
            ApplyKeyline(resources, dark
                ? Color.FromRgb(0x6E, 0x8C, 0xC4)
                : Color.FromRgb(0x94, 0xB4, 0xDE));

            // ── 7) LIQUID GLASS TOKENS — design-system controls ─────
            ApplyLiquidGlassTokens(resources, dark);
        }

        /// <summary>
        /// One frosted card fill: accent-tinted white glass with a
        /// top-lit vertical alpha fade (denser at the top — the
        /// "lit from above" iOS sheen). Flat average alpha when
        /// Gradient Surfaces is off.
        /// </summary>
        private static Brush FrostFill(
            Color accent, double topAlpha, double bottomAlpha)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            return FrostBrush(accent, Colors.White, dark,
                topAlpha, bottomAlpha);
        }

        /// <summary>
        /// A flat, accent-tinted glass at one fixed alpha (decorative
        /// glows such as the dashboard ambient blobs).
        /// </summary>
        private static Brush SolidGlass(Color accent, double alpha)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            return FrostBrush(accent, Colors.White, dark,
                alpha, alpha);
        }

        /// <summary>
        /// Hue-preserved glass: the ACTIVE theme's colour of the key
        /// rendered at a low alpha — status cards/chips stay coloured
        /// (green = success, red = danger, ...) but the fill becomes
        /// transparent. Gradient when Gradient Surfaces is on.
        /// </summary>
        private static Brush HueGlass(
            string key, double topAlpha, double bottomAlpha)
        {
            Color hue = ThemeManager.GetThemeColor(
                key, Colors.SlateGray);

            byte topA = (byte)Math.Round(topAlpha * 255);
            byte bottomA = (byte)Math.Round(bottomAlpha * 255);

            if (ThemeManager.UseGradients)
            {
                var gradient = new LinearGradientBrush(
                    Color.FromArgb(topA, hue.R, hue.G, hue.B),
                    Color.FromArgb(bottomA, hue.R, hue.G, hue.B),
                    new Point(0, 0), new Point(1, 1));
                gradient.Freeze();
                return gradient;
            }

            var flat = new SolidColorBrush(Color.FromArgb(
                (byte)((topA + bottomA) / 2), hue.R, hue.G, hue.B));
            flat.Freeze();
            return flat;
        }

        /// <summary>
        /// The About page's text palette, applied as the app's
        /// Primary/Secondary/Muted foreground: primary = GlassSnow
        /// (near-white on the navy stage), secondary = GlassCerulean,
        /// muted = GlassMuted — the exact colours the About page's
        /// headings and captions use, read from the ACTIVE theme's
        /// Glass* keys so Light/Dark swaps auto-select the match.
        /// </summary>
        private static void SetLiquidTextPalette(
            ResourceDictionary resources, bool dark)
        {
            // Dark mode reads the dark About text palette keys
            // (GlassSnow / GlassCerulean / GlassMuted). LIGHT mode uses
            // fixed ink/slate constants — decoupled from the Glass*
            // SURFACE keys (2026-08-29) so the white water-glass
            // surfaces can be recoloured freely without ever washing
            // the text out (previously light text read GlassFrost, a
            // surface-named key, so re-colouring the frost would have
            // made light text white-on-white).
            resources["PrimaryForegroundBrush"] = dark
                ? FrozenGlassText("GlassSnow",
                    Color.FromRgb(0xF8, 0xFA, 0xFC))
                : FrozenSolid(Color.FromRgb(0x33, 0x41, 0x55));
            resources["SecondaryForegroundBrush"] = dark
                ? FrozenGlassText("GlassCerulean",
                    Color.FromRgb(0x93, 0xC5, 0xFD))
                : FrozenSolid(Color.FromRgb(0x1D, 0x4E, 0xD8));
            resources["MutedForegroundBrush"] = dark
                ? FrozenGlassText("GlassMuted",
                    Color.FromRgb(0x8F, 0xB3, 0xE6))
                : FrozenSolid(Color.FromRgb(0x47, 0x55, 0x69));
        }

        /// <summary>Frozen solid brush from a constant colour.</summary>
        private static Brush FrozenSolid(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        // ── LIQUID GLASS DESIGN SYSTEM TOKENS (2026-08-29) ────────────
        //
        // The Glass/Glassy families publish the full crystal material
        // here: edge, reflections, control states, selection, focus,
        // primary glass — accent-tinted and per-mode. The
        // LiquidGlassStyles.xaml dictionary consumes every token via
        // DynamicResource, so one family switch re-tints the entire
        // card/button/switch/input/grid/dialog system live.

        /// <summary>
        /// Writes the design-system tokens for the active glass
        /// family. Center surfaces mirror the family's own ramp
        /// (already written into ModuleBackgroundBrush /
        /// MenuHoverBrush / ElevatedSurfaceBrush) so cards and
        /// controls stay one material with the window stage.
        /// </summary>
        private static void ApplyLiquidGlassTokens(
            ResourceDictionary resources, bool dark)
        {
            var accent = ThemeManager.AccentColor;

            // Center surfaces — same material as the family's cards.
            resources["LiquidGlassBrush"] =
                resources["ModuleBackgroundBrush"];
            resources["LiquidGlassSubtleBrush"] =
                resources["MenuHoverBrush"];
            resources["LiquidGlassStrongBrush"] =
                resources["ElevatedSurfaceBrush"];

            // Crystal edges + reflections.
            resources["GlassEdgeBrush"] = CrystalEdge(accent, dark);
            resources["GlassInnerHighlightBrush"] =
                HorizontalSheen(dark ? (byte)0x59 : (byte)0xB3);
            resources["GlassReflectionBrush"] =
                DiagonalSheen(dark ? (byte)0x26 : (byte)0x59);

            // Interaction states — white glass at state alphas.
            resources["GlassControlHoverBrush"] =
                SolidGlass(accent, dark ? 0.30 : 0.60);
            resources["GlassControlPressedBrush"] =
                SolidGlass(accent, dark ? 0.22 : 0.70);
            resources["GlassControlDisabledBrush"] =
                SolidGlass(accent, dark ? 0.14 : 0.35);

            // Focus / selection / primary — accent-tinted crystal.
            resources["GlassFocusBrush"] = FrozenSolid(Color.FromArgb(
                dark ? (byte)0x99 : (byte)0xB3,
                accent.R, accent.G, accent.B));
            resources["GlassSelectionBrush"] =
                AccentGlass(accent, dark ? (0x4D, 0x33) : (0x66, 0x40));
            resources["GlassPrimaryButtonBrush"] =
                AccentGlass(accent, dark ? (0xA6, 0x73) : (0xE8, 0xA6));

            // Dialog veil + soft shadow tint.
            resources["GlassOverlayBrush"] = FrozenSolid(dark
                ? Color.FromArgb(0x99, 0x03, 0x10, 0x26)
                : Color.FromArgb(0x59, 0x06, 0x17, 0x24));
            resources["GlassShadowColor"] = dark
                ? Color.FromArgb(0xFF, 0x00, 0x04, 0x10)
                : Color.FromArgb(0x4D, 0x0B, 0x1B, 0x2E);
        }

        /// <summary>
        /// Crystal edge gradient: bright white top → soft
        /// accent-mixed mid → slightly deeper lower edge. Frozen.
        /// </summary>
        private static Brush CrystalEdge(Color accent, bool dark)
        {
            byte topA = dark ? (byte)0x8C : (byte)0xFF;
            byte midA = dark ? (byte)0x26 : (byte)0x59;
            byte botA = dark ? (byte)0x33 : (byte)0x73;

            Color mid = ColorMath.Mix(
                Color.FromRgb(0xFF, 0xFF, 0xFF), accent,
                dark ? 0.25 : 0.18);
            Color bot = ColorMath.Mix(
                Color.FromRgb(0xFF, 0xFF, 0xFF), accent,
                dark ? 0.15 : 0.10);

            var brush = new LinearGradientBrush(
                Color.FromArgb(topA, 0xFF, 0xFF, 0xFF),
                Color.FromArgb(botA, bot.R, bot.G, bot.B),
                new Point(0, 0), new Point(0, 1));
            brush.GradientStops.Insert(1, new GradientStop(
                Color.FromArgb(midA, mid.R, mid.G, mid.B), 0.5));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Horizontal top-reflection line: transparent ends, glowing
        /// white center. Frozen.
        /// </summary>
        private static Brush HorizontalSheen(byte centerAlpha)
        {
            var brush = new LinearGradientBrush(
                Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
                Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
                new Point(0, 0), new Point(1, 0));
            brush.GradientStops.Insert(1, new GradientStop(
                Color.FromArgb(centerAlpha, 0xFF, 0xFF, 0xFF), 0.5));
            brush.Freeze();
            return brush;
        }

        /// <summary>Diagonal specular sweep, fading out. Frozen.</summary>
        private static Brush DiagonalSheen(byte alpha)
        {
            var brush = new LinearGradientBrush(
                Color.FromArgb(alpha, 0xFF, 0xFF, 0xFF),
                Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
                new Point(0, 0), new Point(1, 1));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Accent-tinted crystal glass (top-lit vertical gradient at
        /// the given alphas) — selection and primary fills. Frozen.
        /// </summary>
        private static Brush AccentGlass(
            Color accent, (int top, int bottom) alphas)
        {
            var brush = new LinearGradientBrush(
                Color.FromArgb((byte)alphas.top, accent.R, accent.G, accent.B),
                Color.FromArgb((byte)alphas.bottom, accent.R, accent.G, accent.B),
                new Point(0, 0), new Point(0, 1));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Frozen solid brush for one of the About page's text palette
        /// keys (GlassSnow / GlassFrost / GlassCerulean / GlassMuted)
        /// read from the ACTIVE theme dictionary — falling back to the
        /// given colour if the key is ever missing.
        /// </summary>
        private static Brush FrozenGlassText(string key, Color fallback)
        {
            var brush = new SolidColorBrush(
                ThemeManager.GetThemeColor(key, fallback));
            brush.Freeze();
            return brush;
        }

        // ── GLOBAL FONT COLOUR (Settings → Appearance → Font Color) ──
        //
        // One user-chosen text colour drives the WHOLE UI's
        // Primary/Secondary/Muted foregrounds, in EVERY visual family.
        // Secondary and muted are derived by mixing the chosen colour
        // toward the theme's neutral slate, so the heading/caption/hint
        // contrast order survives whatever colour is picked — this is
        // the tool that guarantees text visibility when Glassy makes
        // every card transparent.
        //
        // Called at the END of Apply and ApplyModule, so it runs after
        // the family baseline AND after any wallpaper typography
        // adaptation: user intent wins last.

        /// <summary>
        /// Applies (or removes) the persisted global font colour
        /// preference. Auto (""/null) restores ownership: Glassy gets
        /// its About-page palette, every other family gets the Light/
        /// Dark theme defaults (with the wallpaper contrast adaptation
        /// re-applied when a wallpaper is on screen).
        /// </summary>
        public static void ApplyFontPreference(
            ResourceDictionary resources, bool wallpaperActive)
        {
            try
            {
                string hex = App.Config?.Appearance?.FontColor ?? "";

                if (string.IsNullOrWhiteSpace(hex))
                {
                    if (Current == LiquidGlass)
                    {
                        bool dark = ThemeManager.CurrentTheme ==
                            AppTheme.Dark;
                        SetLiquidTextPalette(resources, dark);
                    }
                    else
                    {
                        resources.Remove("PrimaryForegroundBrush");
                        resources.Remove("SecondaryForegroundBrush");
                        resources.Remove("MutedForegroundBrush");

                        // The wallpaper layer sets stronger secondary/
                        // muted for readability over pictures — if we
                        // just deleted them and a wallpaper is showing,
                        // restore that adaptation.
                        if (wallpaperActive)
                            AdaptTypographyForWallpaper(resources);
                    }
                    return;
                }

                var chosen = AccentPresets.TryParseHex(hex);
                if (chosen == null) return; // invalid persisted hex — keep current

                // (isDark, not dark — that name is already taken by the
                // Glassy branch above; overlapping scopes may not
                // redeclare a local name in C#.)
                Color primary = chosen.Value;
                bool isDark = ThemeManager.CurrentTheme == AppTheme.Dark;

                Color secNeutral = ThemeManager.GetThemeColor(
                    "SecondaryForegroundColor",
                    isDark ? Color.FromRgb(0x94, 0xA3, 0xB8)
                           : Color.FromRgb(0x47, 0x55, 0x69));
                Color mutNeutral = ThemeManager.GetThemeColor(
                    "MutedForegroundColor",
                    isDark ? Color.FromRgb(0x64, 0x74, 0x8B)
                           : Color.FromRgb(0x64, 0x74, 0x8B));

                FrozenAndSet(resources, "PrimaryForegroundBrush", primary);
                FrozenAndSet(resources, "SecondaryForegroundBrush",
                    ColorMath.Mix(primary, secNeutral, 0.35));
                FrozenAndSet(resources, "MutedForegroundBrush",
                    ColorMath.Mix(primary, mutNeutral, 0.55));
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Font colour preference apply failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Themes",
                        Class = nameof(VisualThemeEngine)
                    });
            }
        }

        // ── WINDOW-LEVEL ACRYLIC BLUR (iOS-style glass) ───────────────
        //
        // Glass and Glassy are the two frosted families. When the OS
        // supports it, GlassChromeService applies the DWM accent policy
        // (acrylic on Win10 1809+/Win11, blur-behind on Win10 1507+),
        // and these families paint the window surface as a translucent
        // veil instead of an opaque gradient — the blurred backdrop then
        // reads through every translucent card, which is the actual
        // iOS-glass material effect. Every other family restores the
        // normal window surface. Runs inside Apply/ApplyModule so theme
        // swaps, accent changes and module navigation all stay in sync.

        /// <summary>
        /// Turns the window acrylic backdrop on (Glass/Glassy) or off
        /// (every other family), with an accent-derived DWM tint.
        /// </summary>
        private static void SyncGlassChrome()
        {
            try
            {
                if (Current == LiquidGlass)
                {
                    bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
                    Color accent = ThemeManager.AccentColor;

                    // DWM tint: deep slate (dark) or clean white water
                    // glass (light), pulled gently toward the accent so
                    // the backdrop belongs to the user's colour. Alpha
                    // lowered (0x80, was 0xB0) so any solid fallback
                    // tint stays subtle behind the opaque stage.
                    Color baseTint = dark
                        ? Color.FromRgb(0x0B, 0x14, 0x2A)
                        : Color.FromRgb(0xF8, 0xFB, 0xFF);
                    Color tint = ColorMath.Mix(baseTint, accent,
                        dark ? 0.28 : 0.12);

                    GlassChromeService.Apply(
                        ColorMath.WithAlpha(tint, 0x80));
                }
                else
                {
                    GlassChromeService.Remove();
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Glass chrome sync failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Themes",
                        Class = nameof(VisualThemeEngine)
                    });
            }
        }

        /// <summary>
        /// Re-runs the window chrome sync for the CURRENT family. Called
        /// once by MainWindow after it attaches GlassChromeService (the
        /// engine applied the family before the window existed).
        /// </summary>
        public static void ReapplyWindowChrome()
            => SyncGlassChrome();

        /// <summary>Frozen SolidColorBrush helper for the font palette.</summary>
        private static void FrozenAndSet(
            ResourceDictionary resources, string key, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[key] = brush;
        }

        /// <summary>
        /// Builds the About page's stage as a single window brush:
        /// diagonal base gradient + two soft radial light orbs, in one
        /// DrawingBrush so the whole window paints one scene.
        /// <para>
        /// The colours are the About page's own — the stage gradient
        /// that preceded the transparent-stage refactor and the exact
        /// GlassOrbA / GlassOrbB stops from the theme files — so Glassy
        /// makes the entire app read as the About scene instead of only
        /// one page. The mid-stop carries a light accent tint, which is
        /// what lets an accent change re-tint the stage (the engine
        /// re-applies on AccentChanged).
        /// </para>
        /// </summary>
        private static Brush BuildLiquidStageBrush(
            bool dark, Color accent)
        {
            // Base stage: the About page's diagonal A → B → A gradient.
            Color baseA, baseB;
            if (dark)
            {
                baseA = Color.FromRgb(0x0B, 0x10, 0x26);           // deep navy
                baseB = ColorMath.Tint(
                    accent, Color.FromRgb(0x10, 0x2A, 0x56), 0.30);
            }
            else
            {
                // White water-ice stage: ice blue with an accent
                // under-tint — deliberately NOT near-white (2026-08-29
                // fix): white glass panes on a near-white stage read
                // as one flat white sheet. A visible sky-ice stage is
                // what makes the white frost read as water glass.
                baseA = Color.FromRgb(0xD7, 0xE5, 0xF9);           // ice
                baseB = ColorMath.Tint(
                    accent, Color.FromRgb(0xAE, 0xCB, 0xF0), 0.42);
            }

            if (!ThemeManager.UseGradients)
            {
                // Flat preference: one solid tinted stage colour.
                var flat = new SolidColorBrush(baseB);
                flat.Freeze();
                return flat;
            }

            var stage = new DrawingGroup();

            // All geometry shares one 0..1 design space; the orbs
            // intentionally extend past it, anchored at the corners the
            // way the About page places them. The base rectangle is
            // sized to the UNION of the bounds (orbs included) so that
            // Stretch.Fill maps the whole scene — and only the scene —
            // onto the window, giving full coverage at any size.
            //   Orb A: (0.87, 0.10) r 0.45 → x 0.42..1.32, y -0.35..0.55
            //   Orb B: (0.06, 0.94) r 0.36 → x -0.30..0.42, y 0.58..1.30
            const double baseLeft = -0.30, baseTop = -0.35;
            const double baseWidth = 1.62, baseHeight = 1.65;

            // Diagonal base gradient (top-left → bottom-right), frozen.
            var baseBrush = new LinearGradientBrush(
                baseA, baseB, new Point(0, 0), new Point(1, 1));
            baseBrush.Freeze();
            stage.Children.Add(new GeometryDrawing(
                baseBrush, null,
                new RectangleGeometry(
                    new Rect(baseLeft, baseTop, baseWidth, baseHeight))));

            // Orb A — top-right, the About page's larger orb.
            stage.Children.Add(new GeometryDrawing(
                LiquidOrb(dark, orbA: true), null,
                new EllipseGeometry(new Point(0.87, 0.10), 0.45, 0.45)));

            // Orb B — bottom-left, the About page's smaller orb.
            stage.Children.Add(new GeometryDrawing(
                LiquidOrb(dark, orbA: false), null,
                new EllipseGeometry(new Point(0.06, 0.94), 0.36, 0.36)));

            stage.Freeze();

            // Deliberately NO Viewport / ViewportUnits here: with the
            // default (empty) viewport a TileBrush stretches its
            // drawing over the ENTIRE area of whatever element it fills
            // — the window, at whatever size it has. Stretch.Fill maps
            // the scene's bounds onto that whole area, so coverage is
            // complete and the orbs stay anchored at the corners.
            // (This also keeps us off the BrushMappingMode enum, whose
            // members do not compile in this project's reference set.)
            var brush = new DrawingBrush(stage)
            {
                Stretch = Stretch.Fill
            };
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// One About-stage orb: the exact radial stops the About page
        /// uses (GlassOrbA / GlassOrbB in the theme files), frozen.
        /// </summary>
        private static Brush LiquidOrb(bool dark, bool orbA)
        {
            // Stops are the XAML values verbatim (AARRGGBB in the files):
            //   Dark  A: #4D82F6FF / #1D4ED84D / #1D4ED800
            //   Dark  B: #60A5FA66 / #3B82F633 / #3B82F600
            //   Light A: #6660A5FA / #332563EB / #002563EB   (2026-08-29 boost)
            //   Light B: #7A3B82F6 / #3D2563EB / #002563EB   (2026-08-29 boost)
            var (c0, c1, c2) = dark
                ? orbA
                    ? (Color.FromArgb(0x4D, 0x82, 0xF6, 0xFF),
                       Color.FromArgb(0x1D, 0x4E, 0xD8, 0x4D),
                       Color.FromArgb(0x00, 0x4E, 0xD8, 0x4D))
                    : (Color.FromArgb(0x60, 0xA5, 0xFA, 0x66),
                       Color.FromArgb(0x33, 0x82, 0xF6, 0x33),
                       Color.FromArgb(0x00, 0x82, 0xF6, 0x33))
                : orbA
                    ? (Color.FromArgb(0x66, 0x60, 0xA5, 0xFA),
                       Color.FromArgb(0x33, 0x25, 0x63, 0xEB),
                       Color.FromArgb(0x00, 0x25, 0x63, 0xEB))
                    : (Color.FromArgb(0x7A, 0x3B, 0x82, 0xF6),
                       Color.FromArgb(0x3D, 0x25, 0x63, 0xEB),
                       Color.FromArgb(0x00, 0x25, 0x63, 0xEB));

            var orb = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
                GradientStops =
                {
                    new GradientStop(c0, 0.0),
                    new GradientStop(c1, 0.55),
                    new GradientStop(c2, 1.0)
                }
            };
            orb.Freeze();
            return orb;
        }

        /// <summary>
        /// Core frost builder shared by every Glassy surface: base
        /// colour tinted toward the accent + diagonal alpha fade.
        /// Flat average alpha when Gradient Surfaces is off.
        /// </summary>
        private static Brush FrostBrush(
            Color accent, Color baseColor, bool dark,
            double topAlpha, double bottomAlpha)
        {
            // Light: a WHISPER of accent over clean white (0.06) — the
            // white water-glass material stays white. Dark: the
            // original 0.22 keeps the deep-frost hue.
            Color tinted = ColorMath.Tint(accent, baseColor,
                dark ? 0.22 : 0.06);

            byte topA = (byte)Math.Round(topAlpha * 255);
            byte bottomA = (byte)Math.Round(bottomAlpha * 255);

            if (ThemeManager.UseGradients)
            {
                // Vertical top-light fade: the denser alpha sits at the
                // TOP of the card, which is what gives iOS materials
                // their soft "lit from above" sheen. (Border has no
                // Template property, so the crisp 1px specular overlay
                // attempted earlier is impossible at the style level —
                // this brush-level sheen delivers the same identity to
                // every surface that consumes these keys.)
                var gradient = new LinearGradientBrush(
                    Color.FromArgb(topA, tinted.R, tinted.G, tinted.B),
                    Color.FromArgb(bottomA, tinted.R, tinted.G, tinted.B),
                    new Point(0, 0), new Point(0, 1));
                gradient.Freeze();
                return gradient;
            }

            var flat = new SolidColorBrush(
                Color.FromArgb((byte)((topA + bottomA) / 2),
                    tinted.R, tinted.G, tinted.B));
            flat.Freeze();
            return flat;
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
                LiquidGlass => LiquidGlass,
                // Legacy families removed in the 2026-08-29 rewrite —
                // saved configs pointing at them resolve to the new
                // iOS Liquid Glass family instead of breaking.
                "Glass" => LiquidGlass,
                "Glassy" => LiquidGlass,
                Ocean => Ocean,
                _ => Normal
            };
        }

        private static double ClampOpacity(double value) =>
            value < 0.3 ? 0.3 : value > 1.0 ? 1.0 : value;
    }
}
