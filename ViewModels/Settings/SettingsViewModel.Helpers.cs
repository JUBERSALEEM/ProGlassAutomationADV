// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Helpers.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.1 — SettingsViewModel partial split. This part owns the Helpers
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
using ProGlassAutomation.Utilities;
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
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        // REGRESSION FIX: this method was accidentally dropped in the
        // phase-3a partial split (e5c73db) while 26 call sites across the
        // Settings partials kept using it. Restored verbatim from 3baa39e.
        // Locked by SettingsRedesignAuditTests.Shell_Helpers_Are_Defined.
        private void SaveConfig()
        {
            if (_config != null)
                ConfigService.SaveDebounced(_config);
        }

        private void InitializeV2Sections()
        {
            InitializeSecurityCommands();
            InitializeThemeWallpaperCommands();
            InitializeModuleWallpaperRows();
            // InitializeUpdateCommands() moved: wired in the UpdatesPage ctor.
            InitializeLanWifiCommands();
            InitializeBackupCommands();
            InitializeDatabaseHealthCommands();
        }

        private void InitializeSecurityCommands()
        {
            // ── Security commands ─────────────────────────────────────
            RemoveSavedKeysCommand = new RelayCommand(_ =>
            {
                var confirm = _dialogs.Show(
                    LocalizationService.Instance.T("Msg.RemoveAllSavedAPIKeys"),
                    LocalizationService.Instance.T("Settings.RemoveSavedKeys"),
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm != System.Windows.MessageBoxResult.Yes) return;

                Services.Security.SecretVault.RemoveAll();

                if (_config?.Ai != null)
                {
                    _config.Ai.GeminiApiKey = "";
                    _config.Ai.GroqApiKey = "";
                    _config.Ai.CohereApiKey = "";
                    SaveConfig();
                }

                OnPropertyChanged(nameof(GeminiApiKey));
                OnPropertyChanged(nameof(GroqApiKey));
                OnPropertyChanged(nameof(CohereApiKey));

                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    "API keys removed",
                    "All stored AI keys deleted from device");
            });

            LoadSampleDataCommand = new RelayCommand(async _ =>
            {
                try
                {
                    var summary = await ProGlassAutomation.Services
                        .SampleData.SampleDataService.SeedAsync();
                    _dialogs.Show(
                        summary, LocalizationService.Instance.T("Settings.SampleDATA"),
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    _dialogs.Show(
                        "Could not load sample data: " + ExceptionMessages.Deep(ex),
                        LocalizationService.Instance.T("Settings.SampleDATA"), System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            });

            ClearAllDataCommand = new RelayCommand(async _ =>
            {
                string folder = _database.IsDemo
                    ? _database.DemoFolder
                    : _database.ProductionFolder;
                string which = _database.IsDemo
                    ? "the isolated DEMO folder (production files stay closed)"
                    : "the PRODUCTION folder";

                var confirm = _dialogs.Show(
                    "This will PERMANENTLY DELETE every record in " +
                    which + ":\n\n" +
                    folder + "\n\n" +
                    "  • Glass sheets\n" +
                    "  • Proforma invoices\n" +
                    "  • Job orders\n" +
                    "  • Delivery notes\n" +
                    "  • Tax invoices\n\n" +
                    "This cannot be undone (take a backup first if in " +
                    "doubt).\n\nContinue?",
                    LocalizationService.Instance.T("Settings.ClearDATA"),
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm != System.Windows.MessageBoxResult.Yes) return;

                try
                {
                    var summary = await ProGlassAutomation.Services
                        .SampleData.SampleDataService.ClearAllDataAsync();
                    _dialogs.Show(
                        summary, LocalizationService.Instance.T("Settings.ClearDATA"),
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    _dialogs.Show(
                        "Could not clear data: " + ExceptionMessages.Deep(ex),
                        LocalizationService.Instance.T("Settings.ClearDATA"), System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            });

                        ClearAiMemoryCommand = new RelayCommand(async _ =>
            {
                await Services.AI.AIMemory.AiMemoryService.ClearAsync();
                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    "AI memory cleared", "");
            });

        }

        private void InitializeThemeWallpaperCommands()
        {
            // ── Visual theme & wallpaper commands ─────────────────────
            SetWallpaperCommand = new RelayCommand(_ =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Choose wallpaper image",
                    Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp|" +
                             "All files|*.*"
                };

                if (dialog.ShowDialog() != true) return;

                if (_config == null) return;
                _config.Appearance.WallpaperPath = dialog.FileName;
                SaveConfig();

                // 2026-09-05 Phase-1 (F-04 class): this ran ApplyVisualTheme()
                // AND a second raw ApplyModule() — a double full-dictionary
                // rebuild left over from the pre-pipeline era. Picking a
                // wallpaper file is a wallpaper-LAYER change: one
                // purpose-built rebuild paints family layer + studio
                // profile exactly once.
                ProGlassAutomation.Services.Theme.ThemePipeline
                    .RebuildWallpaperLayer("wallpaper file set (settings)");

                OnPropertyChanged(nameof(WallpaperStatus));
                OnPropertyChanged(nameof(WallpaperOpacity));

                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    "Wallpaper set",
                    System.IO.Path.GetFileName(dialog.FileName));
            });

            ClearWallpaperCommand = new RelayCommand(_ =>
            {
                if (_config == null) return;
                _config.Appearance.WallpaperPath = "";
                SaveConfig();

                // 2026-09-05 Phase-1 (F-04 class): same pre-pipeline
                // double-paint removed — one wallpaper-layer rebuild
                // restores opaque surfaces + typography exactly once.
                ProGlassAutomation.Services.Theme.ThemePipeline
                    .RebuildWallpaperLayer("wallpaper cleared (settings)");

                OnPropertyChanged(nameof(WallpaperStatus));
            });

            // 2026-09-02 (splash colour-bounce fix): this used to re-apply
            // the ENTIRE visual theme every time a SettingsViewModel was
            // constructed. SettingsViewModel is built eagerly inside
            // MainViewModel's ctor, so each MainViewModel construction
            // triggered a full ClearOverrides + rebuild — while the splash
            // was still on screen and bound to those exact DynamicResource
            // keys. That is the flash users see before the shell appears.
            //
            // App.StartupCore step 9d already applies the persisted family,
            // colour mode, accent and wallpaper before the splash is shown.
            // Only re-apply if the engine has somehow not run yet.
            if (string.IsNullOrEmpty(
                    ProGlassAutomation.Services.Theme.VisualThemeEngine
                        .LastRequestedModule) &&
                ProGlassAutomation.Services.Theme.VisualThemeEngine.Current ==
                    ProGlassAutomation.Services.Theme.VisualThemeEngine.NormalIndustrial &&
                (_config?.Appearance?.VisualTheme ?? "Normal (Industrial)") != "Normal (Industrial)")
            {
                ApplyVisualTheme();
            }

            // v2 §34: show the persisted update history on open.
            _ = UpdatesPage.LoadUpdateHistoryAsync();

        }

        private void InitializeModuleWallpaperRows()
        {
            // ── Module wallpaper rows (§9) ────────────────────────────
            foreach (var (key, label) in
                ProGlassAutomation.Services.Theme.WallpaperModules.All)
            {
                ModuleWallpaperRows.Add(new ModuleWallpaperRow(key, label, _config));
            }

            SetModuleWallpaperCommand = new RelayCommand(param =>
            {
                if (param is not ModuleWallpaperRow row) return;

                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = $"Wallpaper for {row.Label}",
                    Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp|" +
                             "All files|*.*"
                };

                if (dialog.ShowDialog() != true) return;

                row.SetPath(dialog.FileName);

                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    "Module wallpaper set",
                    $"{row.Label} → " +
                    System.IO.Path.GetFileName(dialog.FileName));
            });

            ClearModuleWallpaperCommand = new RelayCommand(param =>
            {
                if (param is not ModuleWallpaperRow row) return;
                row.ClearPath();

                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    "Module wallpaper cleared", row.Label);
            });

        }

    }
}
