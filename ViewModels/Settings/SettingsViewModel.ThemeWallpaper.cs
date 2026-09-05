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
                OnPropertyChanged(nameof(StudioFamilyName));

                // 2026-09-05 PIPELINE UNIFICATION: the extra RefreshScene
                // toggle used to duplicate the module overlay that is
                // already guaranteed inside RebuildFamily; removed — the
                // pipeline paints the theme-linked scene exactly once.
                OnPropertyChanged(nameof(SelectedWallpaperMode));

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
                // Live preview applies immediately; the config write is
                // debounced so dragging the slider never hits the disk
                // on every tick (2026-09-02 perf pass).
                ApplyVisualTheme();
                ScheduleDebouncedSave();
                OnPropertyChanged();
            }
        }

        private System.Windows.Threading.DispatcherTimer? _debouncedSaveTimer;
        private bool _debouncedSavePending;
        private bool _auditBorderOnSave;
        private bool _auditCornerOnSave;

        /// <summary>
        /// Debounced config write for slider-driven settings (2026-09-02
        /// perf pass). Dragging a slider used to hit the disk AND the
        /// audit database on every tick; now the value applies live and
        /// the write settles 500 ms after the last change. Audit entries
        /// also fire once per drag instead of once per tick.
        /// </summary>
        private void ScheduleDebouncedSave(
            bool auditBorder = false, bool auditCorner = false)
        {
            if (auditBorder) _auditBorderOnSave = true;
            if (auditCorner) _auditCornerOnSave = true;
            _debouncedSavePending = true;

            if (_debouncedSaveTimer == null)
            {
                _debouncedSaveTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _debouncedSaveTimer.Tick += (_, __) => SaveDebouncedNow();
            }
            _debouncedSaveTimer.Stop();
            _debouncedSaveTimer.Start();
        }

        private void SaveDebouncedNow()
        {
            _debouncedSaveTimer?.Stop();
            if (!_debouncedSavePending || _config == null) return;
            _debouncedSavePending = false;

            Services.AI.AIConfig.ConfigService.SaveDebounced(_config);

            if (_auditBorderOnSave)
            {
                _auditBorderOnSave = false;
                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Card border thickness changed",
                    $"{_config.Appearance?.CardBorderThickness ?? 1.5:0.#} DIP");
            }
            if (_auditCornerOnSave)
            {
                _auditCornerOnSave = false;
                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Card corner radius changed",
                    $"{_config.Appearance?.CardCornerRadius ?? 12:0} px");
            }
        }

        /// <summary>
        /// Flushes a pending debounced save (called on VM dispose so a
        /// close right after a slider drag never loses the last value).
        /// </summary>
        public void FlushDebouncedSave()
        {
            _debouncedSaveTimer?.Stop();
            SaveDebouncedNow();
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
            // 2026-09-05 PIPELINE UNIFICATION: the hand-rolled 5-step
            // chain below was one of FOUR competing orderings (family →
            // module → accent → outline → studio, while AppearanceService
            // ran accent/outline AFTER studio). One pipeline now: family
            // → module+overlay → studio once → accent → outline.
            ProGlassAutomation.Services.Theme.ThemePipeline.RebuildFamily(
                "family-switch (settings)");
        }

    }
}
