// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.ThemeWallpaper.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.1 — SettingsViewModel partial split. This part owns the ThemeWallpaper
// members; the class declaration, fields and constructor live in
// SettingsViewModel.cs.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Fonts;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
    {
        // ══════════════════════════════════════════════════════════════
        //  v2 — VISUAL THEME & WALLPAPER (Normal / Monet / Professional /
        //       iOS Liquid Glass / Ocean). The legacy theme entries were
        //       removed in the 2026-08-29 rewrite; saved configs still
        //       resolve to iOS Liquid Glass via the engine's Normalize
        //       mapping.
        // ══════════════════════════════════════════════════════════════

        public List<string> VisualThemeOptions { get; } = new()
        {
            ProGlassAutomation.Services.Theme.VisualThemeEngine.Normal,
            ProGlassAutomation.Services.Theme.VisualThemeEngine.Monet,
            ProGlassAutomation.Services.Theme.VisualThemeEngine.Professional,
            ProGlassAutomation.Services.Theme.VisualThemeEngine.LiquidGlass,
            ProGlassAutomation.Services.Theme.VisualThemeEngine.UltraTransparent,
            ProGlassAutomation.Services.Theme.VisualThemeEngine.Ocean
        };

        public string SelectedVisualTheme
        {
            get => _config?.Appearance?.VisualTheme
                ?? ProGlassAutomation.Services.Theme.VisualThemeEngine.LiquidGlass;
            set
            {
                if (_config == null || string.IsNullOrEmpty(value))
                    return;

                _config.Appearance.VisualTheme = value;
                SaveConfig();
                ApplyVisualTheme();
                OnPropertyChanged();

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Visual theme changed", value);
            }
        }

        public double WallpaperOpacity
        {
            get => _config?.Appearance?.WallpaperOpacity ?? 0.85;
            set
            {
                if (_config == null) return;

                _config.Appearance.WallpaperOpacity = value;
                SaveConfig();
                ApplyVisualTheme();
                OnPropertyChanged();
            }
        }

        public string WallpaperStatus
        {
            get
            {
                var path = _config?.Appearance?.WallpaperPath ?? "";
                return string.IsNullOrEmpty(path)
                    ? "No wallpaper set"
                    : $"Wallpaper: {System.IO.Path.GetFileName(path)}";
            }
        }

        public ICommand SetWallpaperCommand { get; private set; }
        public ICommand ClearWallpaperCommand { get; private set; }

        // ── Per-module wallpapers (§9) ─────────────────────────────────

        public ObservableCollection<ModuleWallpaperRow>
            ModuleWallpaperRows { get; } = new();

        public ICommand SetModuleWallpaperCommand { get; private set; }
        public ICommand ClearModuleWallpaperCommand { get; private set; }

        private void ApplyVisualTheme()
        {
            var appearance = _config?.Appearance;
            ProGlassAutomation.Services.Theme.VisualThemeEngine.Apply(
                appearance?.VisualTheme ?? "Normal",
                appearance?.WallpaperPath ?? "",
                appearance?.WallpaperOpacity ?? 0.85);

            // Re-layer the module on screen (its own wallpaper override
            // or the system-wide wallpaper) so changes apply instantly.
            ProGlassAutomation.Services.Theme.VisualThemeEngine.ApplyModule(
                ProGlassAutomation.Services.Theme.VisualThemeEngine
                    .LastRequestedModule,
                appearance);

            // The engine rewrites the surface brushes, and the keyline is
            // derived FROM a surface, so re-derive the accent family
            // against the new surfaces before restoring the user's
            // Defined Card Edges choice on top. Order matters: accent
            // first (writes OutlineDefaultBrush), toggle second (reads
            // it).
            ProGlassAutomation.Services.ThemeManager.ApplyAccent(
                ProGlassAutomation.Services.ThemeManager.AccentColor,
                raiseEvent: false);
            ProGlassAutomation.Services.ThemeManager
                .ReapplyOutlinePreference();
        }

    }
}
