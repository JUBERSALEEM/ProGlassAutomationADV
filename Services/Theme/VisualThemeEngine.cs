// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/VisualThemeEngine.cs
// PROJECT: ProGlassAutomation
// MODULE: Dynamic Theme Engine — visual theme families (v2)
//
// Three complete visual languages layered ON TOP of the Light/Dark
// base themes — the base brushes are derived, never hard-coded twice:
//
//   Normal        — clean, practical default (no overrides).
//   Monet         — soft artistic look: wallpaper visible behind
//                   transparent/glass surfaces, gentle tints.
//   Professional  — premium corporate: tighter card radii, calmer
//                   shadows, crisp surfaces.
//
// Overrides are applied at APPLICATION level (they win over the merged
// theme dictionaries) and are removed when switching back to Normal —
// so nothing is ever destroyed, only layered.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>Applies/removes visual theme families at runtime.</summary>
    public static class VisualThemeEngine
    {
        public const string Normal = "Normal";
        public const string Monet = "Monet";
        public const string Professional = "Professional";

        /// <summary>Keys the engine may override (removed on Normal).</summary>
        private static readonly string[] ManagedKeys =
        {
            "WindowBackgroundBrush", "ModuleBackgroundBrush",
            "ElevatedSurfaceBrush", "SideMenuBackgroundBrush",
            "MenuHoverBrush",
            "CardCornerRadius", "CardCornerRadiusLarge",
            "ControlCornerRadius",
            "WallpaperOpacityValue",
            "ModuleWallpaperBrush"
        };

        /// <summary>Currently active family.</summary>
        public static string Current { get; private set; } = Normal;

        /// <summary>Module whose wallpaper is currently layered (or "").</summary>
        public static string CurrentModule { get; private set; } = "";

        /// <summary>
        /// Applies one family. wallpaperPath/opacity are only used by
        /// Monet. Never throws.
        /// </summary>
        public static void Apply(
            string family, string wallpaperPath = "",
            double wallpaperOpacity = 0.85)
        {
            try
            {
                var resources = Application.Current?.Resources;
                if (resources == null) return;

                ClearOverrides(resources);
                Current = Normalize(family);

                switch (Current)
                {
                    case Monet:
                        ApplyMonet(resources, wallpaperPath,
                            ClampOpacity(wallpaperOpacity));
                        break;

                    case Professional:
                        ApplyProfessional(resources);
                        break;
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
        /// so each screen can carry its own background image.
        /// </summary>
        public static void ApplyModule(
            string moduleKey,
            AI.AIConfig.AppearanceSettings appearance)
        {
            try
            {
                var resources = Application.Current?.Resources;
                if (resources == null) return;

                // Rebuild the family baseline (removes the previous
                // module's wallpaper layer).
                ClearOverrides(resources);
                Current = Normalize(appearance?.VisualTheme ?? Normal);

                switch (Current)
                {
                    case Monet:
                        ApplyMonet(resources,
                            appearance?.WallpaperPath ?? "",
                            ClampOpacity(appearance?.WallpaperOpacity ?? 0.85));
                        break;
                    case Professional:
                        ApplyProfessional(resources);
                        break;
                }

                CurrentModule = "";
                if (string.IsNullOrEmpty(moduleKey) || appearance == null)
                    return;

                if (!appearance.ModuleWallpapers.TryGetValue(
                    moduleKey, out var moduleWallpaper) ||
                    moduleWallpaper == null ||
                    !moduleWallpaper.Enabled)
                    return;

                if (!System.IO.File.Exists(moduleWallpaper.Path))
                    return;

                var brush = BuildWallpaperBrush(
                    moduleWallpaper.Path,
                    ClampOpacity(moduleWallpaper.Opacity));

                if (brush == null) return;

                resources["ModuleWallpaperBrush"] = brush;
                CurrentModule = moduleKey;

                // Let the image read through the module surface.
                SetTranslucent(resources, "ModuleBackgroundBrush",
                    ThemeManager.GetThemeColor(
                        "ModuleBackgroundBrush", Colors.White), 0.78);
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

        // ── MONET — wallpaper + glass surfaces ─────────────────────────

        private static void ApplyMonet(
            ResourceDictionary resources, string wallpaperPath,
            double opacity)
        {
            // Window background = wallpaper (or a soft gradient fallback).
            if (!string.IsNullOrWhiteSpace(wallpaperPath) &&
                File.Exists(wallpaperPath))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(wallpaperPath, UriKind.Absolute);
                image.EndInit();
                image.Freeze();

                var wallpaper = new ImageBrush(image)
                {
                    Stretch = Stretch.UniformToFill,
                    Opacity = opacity
                };
                wallpaper.Freeze();
                resources["WindowBackgroundBrush"] = wallpaper;
            }
            else
            {
                var soft = new LinearGradientBrush(
                    Color.FromRgb(0xDB, 0xEA, 0xFE),
                    Color.FromRgb(0xBF, 0xDB, 0xFE),
                    new Point(0, 0), new Point(1, 1));
                soft.Freeze();
                resources["WindowBackgroundBrush"] = soft;
            }

            // Glass surfaces — the active base theme's colours with
            // transparency so the wallpaper reads through.
            SetTranslucent(resources, "ModuleBackgroundBrush",
                ThemeManager.GetThemeColor(
                    "ModuleBackgroundBrush", Colors.White), 0.72);
            SetTranslucent(resources, "ElevatedSurfaceBrush",
                ThemeManager.GetThemeColor(
                    "ElevatedSurfaceBrush", Colors.White), 0.80);
            SetTranslucent(resources, "SideMenuBackgroundBrush",
                ThemeManager.GetThemeColor(
                    "SideMenuBackgroundBrush", Colors.White), 0.86);
            SetTranslucent(resources, "MenuHoverBrush",
                ThemeManager.GetThemeColor(
                    "MenuHoverBrush", Color.FromRgb(0xF3, 0xF4, 0xF6)), 0.60);

            // Softer, rounder cards fit the artistic look.
            resources["CardCornerRadius"] = new CornerRadius(18);
            resources["CardCornerRadiusLarge"] = new CornerRadius(24);
            resources["ControlCornerRadius"] = new CornerRadius(12);
        }

        // ── PROFESSIONAL — premium corporate ───────────────────────────

        private static void ApplyProfessional(ResourceDictionary resources)
        {
            // Tighter radii + crisp hierarchy; surfaces stay solid.
            resources["CardCornerRadius"] = new CornerRadius(10);
            resources["CardCornerRadiusLarge"] = new CornerRadius(14);
            resources["ControlCornerRadius"] = new CornerRadius(8);

            // Slightly cooler, restrained hover tint.
            var hover = new SolidColorBrush(
                ThemeManager.CurrentTheme == AppTheme.Dark
                    ? Color.FromRgb(0x2B, 0x3A, 0x52)
                    : Color.FromRgb(0xF1, 0xF5, 0xF9));
            hover.Freeze();
            resources["MenuHoverBrush"] = hover;
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
                _ => Normal
            };
        }

        private static double ClampOpacity(double value) =>
            value < 0.3 ? 0.3 : value > 1.0 ? 1.0 : value;
    }
}
