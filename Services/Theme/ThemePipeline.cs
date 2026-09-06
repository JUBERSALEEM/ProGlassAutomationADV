// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/ThemePipeline.cs
// PROJECT: ProGlassAutomation
// MODULE: THE one appearance pipeline (2026-09-05 system-wide rewrite)
//
// SENIOR ROOT-CAUSE ANALYSIS THAT LED TO THIS FILE
// ───────────────────────────────────────────────────────────────────────
// Before this file existed, FOUR copies of the same pipeline ran in
// DIFFERENT orders from different places, each believing it was the last
// step:
//
//   1. AppearanceService.ApplyTheme:    Theme → Apply → ApplyModule
//      (→ finally: scene + studio) → STUDIO AGAIN → accent → outline →
//      WallpaperOrchestrator.RefreshScene (→ ApplyModule AGAIN → finally
//      AGAIN)                                    ⇒ up to 4 repaints/action
//   2. SettingsViewModel.ApplyVisualTheme: Apply → ApplyModule (→ finally)
//      → accent → outline → studio (again, different order than #1)
//   3. Engine finally:                     scene overlay + studio re-apply
//   4. WallpaperOrchestrator.SaveAndRepaint: AppearanceService.ApplyTheme
//      (→ all of #1, duplicated)
//
// Consequences the owner kept reporting, all with the same machine under
// them: the same click produced a different final state depending on
// which service walked last; the studio snapshot base could be captured
// mid-pipeline (before accent re-derivation); Light/Dark+family+studio
// combos renedered differently from the same save state after restart;
// resets "did nothing" because an idempotence guard in one service was
// unknown to another.
//
// THE CONTRACT FROM NOW ON — ONE SEQUENCE, ONE WRITER PER LAYER
// ───────────────────────────────────────────────────────────────────────
//   ThemePipeline.Rebuild() is the ONLY full appearance sequence;
//   ThemePipeline.RebuildFamily() is its "family changed" flavour with
//   no ThemeManager.ApplyTheme (Light/Dark untouched). Ordered layers:
//
//     1. ThemeManager.ApplyTheme      (Light/Dark dictionary swap — only
//                                      the full flavour, only when asked)
//     2. VisualThemeEngine.ApplyModule(family baseline + module overlay +
//        wallpaper/scene overlay in ONE paint — its finally applies the
//        studio profile ONCE on the fresh family snapshot, the canonical
//        studio hook; 2026-09-05: previously Apply()+ApplyModule()
//        double-painted every switch, see ERR-50472F)
//     3. ThemeManager.ApplyAccent     (accent family + outline source,
//                                      re-derived from the NEW surfaces)
//     4. ThemeManager.ReapplyOutlinePreference (user's card-edges choice)
//
//   NOBODY else runs those layers after this: no trailing RefreshScene,
//   no second ApplyModule. Services keep their public APIs and just
//   delegate here, so every existing caller keeps working AND inherits
//   the same order.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>The single canonical appearance orchestrator.</summary>
    public static class ThemePipeline
    {
        /// <summary>
        /// Full appearance rebuild: Light/Dark dictionary swap + family +
        /// module/scene overlay + studio(create once) + accent/outline
        /// re-derivation. `reason` goes to the app log so any future
        /// "who repainted?" question has a provable answer.
        /// </summary>
        public static void Rebuild(bool isDark, string reason)
        {
            try
            {
                ThemeManager.ApplyTheme(
                    isDark ? AppTheme.Dark : AppTheme.Light);
                RebuildFamily(
                    $"full-rebuild · {reason} · mode={(isDark ? "Dark" : "Light")}");
            }
            catch (Exception ex)
            {
                // The appearance chain must never surface as a crash — an
                // earlier symptom class (ERR-46877E) proved that. Degrade
                // to log + whatever partial paint already landed.
                AppLogService.Warning(
                    $"Theme pipeline rebuild degraded ({reason}): {ex.Message}",
                    new LogContext { Module = "Themes", Operation = "Pipeline" });
            }
        }

        /// <summary>
        /// Family-level rebuild (visual theme switch, wallpaper mode
        /// change, studio reset — everything that keeps Light/Dark as-is).
        /// The ordering here is THE contract; keep the comment above if
        /// the steps ever change.
        /// </summary>
        public static void RebuildFamily(string reason)
        {
            try
            {
                var appearance = App.Config?.Appearance;

                // 2) ONE module pass paints family baseline + module and
                //    wallpaper/scene layers (ApplyModule rebuilds the
                //    family internally; its finally block runs the ONE
                //    canonical studio re-apply on the fresh family
                //    snapshot — OnFamilyApplied).
                //
                //    2026-09-05 (ERR-50472F postmortem): this used to call
                //    VisualThemeEngine.Apply() FIRST and ApplyModule()
                //    after — the ENTIRE 64-key dictionary was torn down
                //    and republished TWICE per switch. That was the
                //    visible flash/"dull blur" bounce the owner reported,
                //    and it opened two missing-key windows instead of
                //    one — the second killed a customer's app when a
                //    template inflated against the half-rebuilt
                //    dictionary. Now: a family change flips the scene
                //    signature, so this pass always repaints for real;
                //    an identical repeat is skipped — zero churn.
                VisualThemeEngine.ApplyModule(
                    VisualThemeEngine.LastRequestedModule, appearance);

                // 3) Accent family re-derived from the NEW surfaces —
                //    the keyline source must be the post-family surface.
                ThemeManager.ApplyAccent(
                    ThemeManager.AccentColor, raiseEvent: false);

                // 4) The user's Defined Card Edges choice reads the
                //    freshly derived accent keyline — always last.
                ThemeManager.ReapplyOutlinePreference();

                AppLogService.Debug(
                    $"[ThemePipeline] Rebuilt: {reason} | family={appearance?.VisualTheme} " +
                    $"wallpaper={appearance?.WallpaperMode}/{appearance?.BuiltinScene}",
                    new LogContext { Module = "Themes", Operation = "Pipeline" });
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Theme pipeline family rebuild degraded ({reason}): {ex.Message}",
                    new LogContext { Module = "Themes", Operation = "Pipeline" });
            }
        }

        /// <summary>
        /// Wallpaper-only repaint: mode/scene/file/opactiy changed but the
        /// family did NOT. The module pass is enough — its finally paints
        /// the linked scene AND the studio profile; running the family
        /// pass first would only flash.
        /// </summary>
        public static void RebuildWallpaperLayer(string reason)
        {
            try
            {
                VisualThemeEngine.InvalidateSceneCache();
                VisualThemeEngine.ApplyModule(
                    VisualThemeEngine.LastRequestedModule,
                    App.Config?.Appearance);
                AppLogService.Debug(
                    $"[ThemePipeline] Wallpaper layer rebuilt: {reason}",
                    new LogContext { Module = "Themes", Operation = "Pipeline" });
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Wallpaper layer rebuild degraded ({reason}): {ex.Message}",
                    new LogContext { Module = "Themes", Operation = "Pipeline" });
            }
        }

        /// <summary>
        /// Accent change aftermath: accent + outline + wallpaper scene
        /// re-derive, studio already re-applied by its own accent hook.
        /// (AppearanceService.ApplyAccent owns the config + audit part.)
        /// </summary>
        public static void RebuildAccentLayer()
        {
            try
            {
                ThemeManager.ApplyAccent(
                    ThemeManager.AccentColor, raiseEvent: false);
                ThemeManager.ReapplyOutlinePreference();
                WallpaperOrchestrator.RefreshScene(); // Monet/accent scenes
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Accent layer rebuild degraded: {ex.Message}",
                    new LogContext { Module = "Themes", Operation = "Pipeline" });
            }
        }
    }
}
