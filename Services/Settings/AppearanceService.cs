// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Settings/AppearanceService.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings — Appearance Domain Service (Phase 6b)
// VERSION: Phase 6b — 2026-08-21 — Extracted from SettingsViewModel 1874 LOC
//
// PURPOSE:
//   SettingsViewModel was 1874 LOC — handles theme, accent, density,
//   wallpaper, outline, border thickness, corner radius, PDF layout, etc.
//   This service extracts pure appearance logic.
//
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Theme;
using System;

namespace ProGlassAutomation.Services.Settings
{
    public sealed class AppearanceService
    {
        public static Result ApplyTheme(bool isDark, string? correlationId = null)
        {
            var corrId = correlationId ?? CorrelationContext.NewId();
            try
            {
                ThemeManager.ApplyTheme(isDark ? AppTheme.Dark : AppTheme.Light);
                VisualThemeEngine.Apply(
                    App.Config?.Appearance?.VisualTheme ?? "Normal",
                    App.Config?.Appearance?.WallpaperPath ?? "",
                    App.Config?.Appearance?.WallpaperOpacity ?? 0.85);

                // Re-layer the module wallpaper / system wallpaper so the
                // module surface keeps its picture after the swap (same
                // ordering the Settings path uses).
                VisualThemeEngine.ApplyModule(
                    VisualThemeEngine.LastRequestedModule,
                    App.Config?.Appearance);

                // Theme Studio: re-snapshot the family's freshly painted
                // brushes and re-apply the user's 0–100 profile on top.
                // A Light/Dark toggle or family switch must never drop
                // the user's customization (master prompt §live apply).
                ThemeStudioService.OnFamilyApplied();

                // Persist the base colour mode so it survives restarts
                // (the AI-agent path changes theme too, not only the
                // Settings toggle).
                if (App.Config?.Appearance != null)
                {
                    App.Config.Appearance.Theme =
                        isDark ? "Dark" : "Light";
                    SaveConfig();
                }

                // Re-derive every accent-dependent surface AGAIN after the
                // visual family has rebuilt its own module brushes. This is
                // the same ordering guarantee used by SettingsViewModel:
                // theme → family surfaces → accent family → outline.
                ThemeManager.ApplyAccent(ThemeManager.AccentColor, raiseEvent: false);
                ThemeManager.ReapplyOutlinePreference();

                AppLogService.Info($"Theme applied: {(isDark ? "Dark" : "Light")}",
                    new LogContext { Module = "Settings", Operation = "ApplyTheme", CorrelationId = corrId });

                return Result.Ok(corrId);
            }
            catch (Exception ex)
            {
                var errId = AppLogService.Error($"ApplyTheme failed: {ex.Message}", ex,
                    new LogContext { Module = "Settings", Operation = "ApplyTheme", CorrelationId = corrId });
                return Result.Fail(ex.Message, ex, corrId, errId);
            }
        }

        public static Result ApplyAccent(string presetKey, string? correlationId = null)
        {
            var corrId = correlationId ?? CorrelationContext.NewId();
            try
            {
                var color = AccentPresets.ColorFor(presetKey);
                ThemeManager.ApplyAccent(color);

                if (App.Config?.Appearance != null)
                {
                    App.Config.Appearance.AccentPreset = string.IsNullOrEmpty(presetKey) ? "GlassBlue" : presetKey;
                    SaveConfig();
                }

                AppLogService.Info($"Accent applied: {presetKey}",
                    new LogContext { Module = "Settings", Operation = "ApplyAccent", CorrelationId = corrId });

                return Result.Ok(corrId);
            }
            catch (Exception ex)
            {
                var errId = AppLogService.Error($"ApplyAccent failed: {ex.Message}", ex,
                    new LogContext { Module = "Settings", Operation = "ApplyAccent", CorrelationId = corrId });
                return Result.Fail(ex.Message, ex, corrId, errId);
            }
        }

        public static Result ApplyDensity(string density, string? correlationId = null)
        {
            var corrId = correlationId ?? CorrelationContext.NewId();
            try
            {
                if (App.Config?.Appearance == null)
                    return Result.Fail("Appearance config not initialized", corrId);

                string v = string.IsNullOrWhiteSpace(density) ? "Compact" : density.Trim();
                App.Config.Appearance.DisplayDensity = v;
                ThemeManager.ApplyDisplayDensity(v);
                SaveConfig();

                AppLogService.Info($"Density applied: {v}",
                    new LogContext { Module = "Settings", Operation = "ApplyDensity", CorrelationId = corrId });

                return Result.Ok(corrId);
            }
            catch (Exception ex)
            {
                var errId = AppLogService.Error($"ApplyDensity failed: {ex.Message}", ex,
                    new LogContext { Module = "Settings", Operation = "ApplyDensity", CorrelationId = corrId });
                return Result.Fail(ex.Message, ex, corrId, errId);
            }
        }

        public static Result ApplyUiScale(double scale, string? correlationId = null)
        {
            var corrId = correlationId ?? CorrelationContext.NewId();
            try
            {
                if (App.Config?.Appearance == null)
                    return Result.Fail("Appearance config not initialized", corrId);

                double v = Services.Viewport.ViewportService.ClampUiScale(scale);
                App.Config.Appearance.UiScale = v;
                ThemeManager.ApplyUiScale(v);
                SaveConfig();

                return Result.Ok(corrId);
            }
            catch (Exception ex)
            {
                var errId = AppLogService.Error($"ApplyUiScale failed: {ex.Message}", ex,
                    new LogContext { Module = "Settings", Operation = "ApplyUiScale", CorrelationId = corrId });
                return Result.Fail(ex.Message, ex, corrId, errId);
            }
        }

        private static void SaveConfig()
        {
            if (App.Config != null)
                Services.AI.AIConfig.ConfigService.Save(App.Config);
        }
    }
}
