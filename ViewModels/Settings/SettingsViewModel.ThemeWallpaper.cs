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
        //  Phase 8: Settings exposes exactly Industrial / Monet / Crystal.
        //  Saved "iOS 26" migrates to Crystal. Engine Ios26 painter stays
        //  until Phase 12 zero-ref delete.
        // ══════════════════════════════════════════════════════════════

        /// <summary>One selectable visual family. Display pairs the family
        /// name with its built-in wallpaper scene (2026-09-05 owner ask:
        /// "show which theme comes with which scene"); Key drives the
        /// SelectedValue binding so the saved config keeps the raw key.</summary>
        public sealed class VisualThemeOptionRow
        {
            public string Key { get; }
            public string Display { get; }
            public VisualThemeOptionRow(string key)
            {
                Key = key;
                string scene = "Studio";
                foreach (var s in ProGlassAutomation.Services.Theme
                             .BuiltinWallpaperScenes.All)
                    if (s.Key == key) { scene = s.Label; break; }
                // Display name (Ios-17pm for Crystal Fluid) — the family
                // IDENTITY stays the persisted engine const.
                Display = ProGlassAutomation.Services.Theme.VisualThemeEngine
                    .FamilyDisplayName(key) + "  —  " + scene;
            }
            /// <summary>Style-immune label (see file header).</summary>
            public override string ToString() => Display;
        }

        public List<VisualThemeOptionRow> VisualThemeOptions { get; } = new()
        {
            // Normal — August-25 cream SaaS (persisted as Normal Industrial).
            new(ProGlassAutomation.Services.Theme.VisualThemeEngine.NormalIndustrial),
            new(ProGlassAutomation.Services.Theme.VisualThemeEngine.Monet),
            // Surfaced as "iPhone 17 Pro Max" (FamilyDisplayName).
            new(ProGlassAutomation.Services.Theme.VisualThemeEngine.CrystalFluid),
            new(ProGlassAutomation.Services.Theme.VisualThemeEngine.Ios26),
        };

        public string SelectedVisualTheme
        {
            get
            {
                var raw = _config?.Appearance?.VisualTheme
                    ?? ProGlassAutomation.Services.Theme.VisualThemeEngine.NormalIndustrial;
                return ProGlassAutomation.Services.Theme.Engine.ProGlassThemeEngine
                    .PersistFamily(
                        ProGlassAutomation.Services.Theme.Engine.ProGlassThemeEngine
                            .ParseFamily(raw));
            }
            set
            {
                if (_config == null || string.IsNullOrEmpty(value))
                    return;

                value = ProGlassAutomation.Services.Theme.Engine.ProGlassThemeEngine
                    .PersistFamily(
                        ProGlassAutomation.Services.Theme.Engine.ProGlassThemeEngine
                            .ParseFamily(value));
                _config.Appearance.VisualTheme = value;
                SaveConfig();

                // Mode coupling (engine data, VisualThemeEngine.ModePairFor):
                // Crystal Fluid (Ios-17pm) is light-only, so picking it
                // auto-engages Light. Normal and Monet keep the user's own
                // Light/Dark choice.
                bool? modePair = ProGlassAutomation.Services.Theme
                    .VisualThemeEngine.ModePairFor(value);
                if (modePair.HasValue)
                {
                    // The full pipeline (Light/Dark swap INCLUDED) runs via
                    // the domain service so the mode change + family change
                    // land in ONE deterministic rebuild.
                    ProGlassAutomation.Services.Settings.AppearanceService
                        .ApplyTheme(modePair.Value);
                }
                else
                {
                    ApplyVisualTheme();
                }
                OnPropertyChanged();

                // Family gate for the Themes section's Crystal Fluid
                // material group (visible only while Ios-17pm is active).
                OnPropertyChanged(nameof(IsCrystalThemeActive));

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
                // 2026-09-05 Phase-1 (F-04): this used to repaint the
                // ENTIRE family — 64-key teardown + accent re-derive —
                // on every slider tick, recreating the flash class the
                // single-paint pipeline closed. Now the live preview is
                // a COALESCED wallpaper-layer repaint (≤ ~8/s during a
                // drag, trailing pass guaranteed) and the config write
                // still settles 500 ms after the last tick.
                ScheduleLiveWallpaperRepaint();
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
            if (_wallpaperRepaintTimer?.IsEnabled == true)
            {
                // A pending trailing repaint would otherwise fire into a
                // disposed view model — land the final drag value NOW
                // (config already holds it).
                _wallpaperRepaintTimer.Stop();
                FlushLiveWallpaperRepaint();
            }
            SaveDebouncedNow();
        }

        // ── Live wallpaper repaint throttle (2026-09-05 Phase-1, F-04) ──

        private DateTime _lastWallpaperRepaintUtc = DateTime.MinValue;
        private System.Windows.Threading.DispatcherTimer? _wallpaperRepaintTimer;

        /// <summary>
        /// Coalesced live repaint for wallpaper slider drags: the first
        /// tick paints immediately (drag stays live); further ticks
        /// repaint at most once per 120 ms; a trailing pass guarantees
        /// the released value is what is on screen. The repaint is the
        /// wallpaper LAYER only (RebuildWallpaperLayer) — a family-level
        /// rebuild per tick was the F-04 flash bug.
        /// </summary>
        private void ScheduleLiveWallpaperRepaint()
        {
            if (_wallpaperRepaintTimer == null)
            {
                _wallpaperRepaintTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(120)
                };
                _wallpaperRepaintTimer.Tick += (_, __) =>
                {
                    _wallpaperRepaintTimer?.Stop();
                    FlushLiveWallpaperRepaint();
                };
            }

            var elapsed = DateTime.UtcNow - _lastWallpaperRepaintUtc;
            if (elapsed >= TimeSpan.FromMilliseconds(120))
            {
                FlushLiveWallpaperRepaint();
            }
            else if (!_wallpaperRepaintTimer.IsEnabled)
            {
                _wallpaperRepaintTimer.Interval =
                    TimeSpan.FromMilliseconds(120) - elapsed;
                _wallpaperRepaintTimer.Start();
            }
        }

        private void FlushLiveWallpaperRepaint()
        {
            _lastWallpaperRepaintUtc = DateTime.UtcNow;
            ProGlassAutomation.Services.Theme.ThemePipeline
                .RebuildWallpaperLayer("wallpaper opacity drag (settings)");
        }

        public string WallpaperStatus
        {
            get
            {
                // Phase 2 (F-05): the status line was a hard-coded
                // English string in a 15-language app — localized now,
                // en-US fallback guaranteed for the other languages.
                var path = _config?.Appearance?.WallpaperPath ?? "";
                return string.IsNullOrEmpty(path)
                    ? Services.Localization.LocalizationService.Instance
                        .T("Settings.WallpaperNone")
                    : string.Format(
                        Services.Localization.LocalizationService.Instance
                            .T("Settings.WallpaperNamed"),
                        System.IO.Path.GetFileName(path));
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
