// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/WallpaperOrchestrator.cs
// PROJECT: ProGlassAutomation
// MODULE: Wallpaper system — mode resolution + dynamic scene overlay
//         (2026-09-05)
//
// THE PIPELINE (where this sits — no engine rewiring):
//   VisualThemeEngine.Apply / ApplyModule rebuild the family scene and
//   layer any FILE wallpaper exactly as before (unchanged behavior).
//   At the very END of ApplyModule (finally block) the engine asks
//   TryGetLinkedScene() here: when the user's mode is "ThemeLinked" or
//   "Scene", the code-drawn built-in scene is written over
//   "ModuleWallpaperBrush" — the same resource key every wallpaper-aware
//   view already binds, so navigation, dashboards and modules all pick
//   the scene up with zero XAML changes.
//
//   Theme Studio then snapshots the overlaid brush (OnFamilyApplied
//   runs after ApplyModule in both apply chains), so Backdrop
//   visibility / Colour richness act on whatever is really on screen.
//   UpdateBase() keeps that snapshot honest when the scene is redrawn
//   on an accent change without a family rebuild.
//
// MODES (AppearanceSettings.WallpaperMode):
//   Auto        — legacy-safe default: a saved file path wins; with no
//                 path the theme-linked scene shows.
//   ThemeLinked — ALWAYS the code-drawn scene of the active family;
//                 follows family + accent changes (Golden Hour literally
//                 re-derives from the accent hue).
//   File        — the image at WallpaperPath (browsed or downloaded).
//   Scene       — one pinned built-in scene (BuiltinScene key).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Settings;
using System;
using System.Windows;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>Resolves the wallpaper mode and paints the dynamic
    /// built-in scenes over the engine's wallpaper key.</summary>
    public static class WallpaperOrchestrator
    {
        public const string ModeAuto = "Auto";
        public const string ModeThemeLinked = "ThemeLinked";
        public const string ModeFile = "File";
        public const string ModeScene = "Scene";

        /// <summary>The mode that is effectively in force, resolving the
        /// migration-safe "Auto": a legacy saved path behaves as File,
        /// otherwise the theme-linked dynamic scene is active.</summary>
        public static string EffectiveMode(AppearanceSettings? a)
        {
            if (a == null) return ModeThemeLinked;
            string mode = a.WallpaperMode ?? ModeAuto;
            if (mode == ModeAuto)
            {
                bool hasFile = !string.IsNullOrWhiteSpace(a.WallpaperPath)
                            && System.IO.File.Exists(a.WallpaperPath);
                return hasFile ? ModeFile : ModeThemeLinked;
            }
            return mode;
        }

        /// <summary>
        /// PURE DECISION used by VisualThemeEngine.ApplyModule: when a
        /// theme-linked or pinned scene must paint the window stage,
        /// returns the scene brush + intended opacity. False in File
        /// mode and before config loads. The ENGINE performs the write
        /// itself because it owns the private translucency/typography
        /// helpers (v2 root-cause fix — v1 wrote only
        /// "ModuleWallpaperBrush", consumed by exactly ONE view, so the
        /// scene existed but was invisible behind opaque module
        /// surfaces: the "fake/broken wallpaper" report).
        /// </summary>
        public static bool TryGetLinkedScene(
            out System.Windows.Media.Brush? scene, out double opacity)
        {
            scene = null;
            opacity = 0.85;
            try
            {
                var appearance = App.Config?.Appearance;
                if (appearance == null) return false;

                // 2026-09-05 factory behavior (owner directive — "as soon
                // as the user changes the visual theme the wallpaper must
                // change"): any legacy "Auto" value upgrades to the
                // theme-linked dynamic scene. A previously picked file
                // stays on disk untouched and remains one tap away via
                // "My file / downloaded" — only the ACTIVE mode changes.
                if ((appearance.WallpaperMode ?? ModeAuto) == ModeAuto)
                    appearance.WallpaperMode = ModeThemeLinked;

                string mode = EffectiveMode(appearance);
                if (mode != ModeThemeLinked && mode != ModeScene) return false;

                string sceneKey = mode == ModeScene
                    ? (appearance.BuiltinScene ?? "")
                    : (VisualThemeEngine.Current ?? VisualThemeEngine.LiquidGlass);

                var s = BuiltinWallpaperScenes.For(sceneKey);
                // Ambient scenery, not a page background — the stage's
                // own gradient must still glow through by design.
                opacity = Math.Min(0.95,
                    Math.Max(0.10, appearance.WallpaperOpacity));
                scene = s;
                return true;
            }
            catch
            {
                scene = null;
                return false;
            }
        }

        /// <summary>True while an OS window backdrop (Mica/Acrylic) is
        /// active — the scene must NOT paint an opaque stage over the
        /// OS-rendered backdrop (Mica IS the backdrop then), and module
        /// surfaces go extra-translucent so it can breathe.</summary>
        public static bool BackdropActive =>
            App.Config?.Appearance?.WindowBackdrop is
                SystemBackdropService.Mica or SystemBackdropService.Acrylic;

        /// <summary>Redraws the active scene by forcing the proven
        /// ApplyModule pipeline to rebuild (invalidate + re-apply): one
        /// code path paints scenes — the engine's — so accent, Light/
        /// Dark and family changes always produce the same result.</summary>
        public static void RefreshScene()
        {
            try
            {
                VisualThemeEngine.InvalidateSceneCache();
                VisualThemeEngine.ApplyModule(
                    VisualThemeEngine.LastRequestedModule,
                    App.Config?.Appearance);
            }
            catch { /* never break a theme path */ }
        }

        // ── OS window backdrop (Windows 11 Mica/Acrylic — EXPERIMENTAL) ──

        /// <summary>Sets the OS-level window backdrop: "Off" (the default
        /// painted stage), "Mica" or "Acrylic" (Windows 11 22H2+,
        /// DWM-rendered behind WPF content). Unsupported OS or any
        /// failure degrades to the painted stage; returning to "Off"
        /// repaints the normal theme stage via the full ApplyTheme chain
        /// so nothing visually sticks.</summary>
        public static void SetWindowBackdrop(string mode)
        {
            var appearance = App.Config?.Appearance;
            if (appearance == null) return;
            appearance.WindowBackdrop = mode;
            if (App.Config != null)
                ConfigService.Save(App.Config);

            var window = Application.Current?.MainWindow;
            var resources = Application.Current?.Resources;
            bool applied = SystemBackdropService.Apply(window, mode);

            if (applied
                && mode != SystemBackdropService.Off
                && resources != null)
            {
                SystemBackdropService.GhostWindowStage(resources);
                // Module surfaces must go extra-translucent so the
                // OS backdrop can breathe (the engine overlay reads
                // BackdropActive on its next rebuild).
                RefreshScene();
                Audit("Window backdrop", mode);
            }
            else
            {
                // Off (or unsupported): restore the full painted stage.
                AppearanceService.ApplyTheme(
                    ThemeManager.CurrentTheme == AppTheme.Dark);
                Audit("Window backdrop",
                    mode == SystemBackdropService.Off
                        ? "Off (painted stage)"
                        : mode + " unsupported on this Windows — painted stage kept");
            }
        }

        /// <summary>Re-applies the saved backdrop preference once the
        /// main window exists (startup hook, called right after the
        /// window's first Show()).</summary>
        public static void ApplyBackdropPreference(Window window)
        {
            try
            {
                var mode = App.Config?.Appearance?.WindowBackdrop
                    ?? SystemBackdropService.Off;
                if (mode == SystemBackdropService.Off) return;
                var resources = Application.Current?.Resources;
                if (SystemBackdropService.Apply(window, mode)
                    && resources != null)
                {
                    SystemBackdropService.GhostWindowStage(resources);
                    RefreshScene();
                }
            }
            catch { /* the painted stage is the safe default */ }
        }

        // ── User actions (Settings → Appearance → Wallpaper Studio) ──

        /// <summary>Theme-linked mode: the dynamic scene follows the
        /// current theme from now on.</summary>
        public static void SetThemeLinked()
        {
            var appearance = App.Config?.Appearance;
            if (appearance == null) return;
            appearance.WallpaperMode = ModeThemeLinked;
            SaveAndRepaint();
            Audit("Wallpaper mode", "Theme-linked (dynamic scene)");
        }

        /// <summary>File mode with an explicit path (browse or freshly
        /// downloaded): persists and repaints through the full theme
        /// chain so the image + studio layers all re-derive correctly.</summary>
        public static void SetFile(string path)
        {
            var appearance = App.Config?.Appearance;
            if (appearance == null) return;
            if (string.IsNullOrWhiteSpace(path)) return;
            appearance.WallpaperPath = path;
            appearance.WallpaperMode = ModeFile;
            SaveAndRepaint();
            Audit("Wallpaper applied", System.IO.Path.GetFileName(path));
        }

        /// <summary>Pins one built-in scene (independent of the active
        /// family). Empty key = re-follow the active family.</summary>
        public static void SetScene(string familyKey)
        {
            var appearance = App.Config?.Appearance;
            if (appearance == null) return;
            appearance.WallpaperMode = string.IsNullOrWhiteSpace(familyKey)
                ? ModeThemeLinked : ModeScene;
            appearance.BuiltinScene = familyKey ?? "";
            SaveAndRepaint();
            Audit("Wallpaper scene pinned",
                string.IsNullOrWhiteSpace(familyKey) ? "Follow theme" : familyKey);
        }

        /// <summary>Clearing the wallpaper returns to the theme-linked
        /// dynamic scene (instead of a bare stage) — the 2026 default.</summary>
        public static void ClearToLinked()
        {
            var appearance = App.Config?.Appearance;
            if (appearance == null) return;
            appearance.WallpaperPath = "";
            appearance.WallpaperMode = ModeThemeLinked;
            SaveAndRepaint();
            Audit("Wallpaper cleared", "Theme-linked scene");
        }

        // ── Internals ─────────────────────────────────────────────────

        private static void SaveAndRepaint()
        {
            if (App.Config != null)
                ConfigService.Save(App.Config);
            // Repaint through the ONE proven chain (engine → module →
            // overlay → studio resnapshot → accent re-derive): identical
            // to Light/Dark ApplyTheme, so the wallpaper modes inherit
            // every ordering guarantee that chain already encodes.
            AppearanceService.ApplyTheme(
                ThemeManager.CurrentTheme == AppTheme.Dark);
        }

        private static void Audit(string action, string detail)
        {
            try
            {
                global::ProGlassAutomation.Services.Audit.AuditService.Log(
                    global::ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    action, detail);
            }
            catch { /* wallpaper audit must never break the UI path */ }
        }

        private static double Clamp01(double v) =>
            v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
