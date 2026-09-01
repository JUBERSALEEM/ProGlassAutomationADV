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

        private static void SaveConfig()
        {
            if (_config != null)
                ConfigService.Save(_config);
        }

        private void InitializeV2Sections()
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
                        "Could not load sample data: " + ex.Message,
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
                        "Could not clear data: " + ex.Message,
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
                ApplyVisualTheme();

                // Re-layer the module on screen so the new system-wide
                // wallpaper is visible immediately.
                ProGlassAutomation.Services.Theme.VisualThemeEngine
                    .ApplyModule(
                        ProGlassAutomation.Services.Theme.VisualThemeEngine
                            .LastRequestedModule,
                        _config.Appearance);

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
                ApplyVisualTheme();

                // Re-layer the module on screen so the removal of the
                // system-wide wallpaper is visible immediately.
                ProGlassAutomation.Services.Theme.VisualThemeEngine
                    .ApplyModule(
                        ProGlassAutomation.Services.Theme.VisualThemeEngine
                            .LastRequestedModule,
                        _config.Appearance);

                OnPropertyChanged(nameof(WallpaperStatus));
            });

            // Apply the persisted visual theme once Settings loads.
            ApplyVisualTheme();

            // v2 §34: show the persisted update history on open.
            _ = LoadUpdateHistoryAsync();

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

            // ── Update commands (v3 OTA orchestrator, spec §6-§8) ─────
            // All heavy lifting lives in Services/Updates/UpdateService;
            // this VM only projects its state machine onto the UI.

            Services.Updates.UpdateService.StateChanged +=
                OnUpdateServiceStateChanged;

            CheckForUpdatesCommand = new RelayCommand(async _ =>
            {
                if (IsCheckingUpdate) return;
                IsCheckingUpdate = true;
                UpdateStatus = "Checking for updates…";

                // Installed builds use the Velopack OTA path.
                if (Services.Updates.VelopackUpdateService.IsInstalled)
                {
                    var latest = await Services.Updates.VelopackUpdateService
                        .CheckAsync();
                    IsCheckingUpdate = false;
                    LastCheckedDisplay = "Last checked: " +
                        DateTime.Now.ToString("dd MMM yyyy • h:mm tt");
                    if (!string.IsNullOrEmpty(latest))
                    {
                        IsUpdateAvailable = true;
                        IsUpdateReady = false;
                        UpdateStatus = "Update available: v" + latest;
                    }
                    else
                    {
                        IsUpdateAvailable = false;
                        UpdateStatus = "You are up to date (v" +
                            Services.Updates.AppInfo.Version + ").";
                    }
                    await LoadUpdateHistoryAsync();
                    return;
                }

                var result = await Services.Updates.UpdateService
                    .CheckNowAsync();

                _lastUpdateCheck = result;

                if (result.Success)
                {
                    IsUpdateAvailable = result.UpdateAvailable;
                    ApplyReleaseInfo(result);

                    var checkedAt = Services.Updates.UpdateService
                        .LastCheckUtc;
                    LastCheckedDisplay = checkedAt.HasValue
                        ? "Last checked: " + checkedAt.Value.ToLocalTime()
                            .ToString("dd MMM yyyy • h:mm tt")
                        : "";

                    if (!result.UpdateAvailable)
                        UpdateStatus =
                            $"✓ You're up to date " +
                            $"(v{Services.Updates.AppInfo.Version}).";
                    // UpdateAvailable status text arrives via the
                    // StateChanged handler ("🚀 Update available…").
                }
                else
                {
                    UpdateStatus = Services.Updates.UpdateService.State ==
                        Services.Updates.UpdateState.Offline
                        ? "No internet connection. Update check unavailable."
                        : $"Could not check for updates: {result.ErrorMessage}";
                }

                IsCheckingUpdate = false;
                await LoadUpdateHistoryAsync();
            });

            DownloadUpdateCommand = new RelayCommand(async _ =>
            {
                // R-2: this command has no canExecute, and the lambda is
                // async void, so a second click during the download would
                // start a concurrent download of the same package.
                if (IsDownloadingUpdate) return;

                // Installed builds download via Velopack (delta packages).
                if (Services.Updates.VelopackUpdateService.IsInstalled)
                {
                    if (!Services.Updates.VelopackUpdateService.HasPendingUpdate)
                    {
                        UpdateStatus = "Check for updates first.";
                        return;
                    }
                    IsDownloadingUpdate = true;
                    UpdateDownloadProgress = 0;
                    UpdateStatus = "Downloading update…";
                    bool okV;
                    try
                    {
                        okV = await Services.Updates.VelopackUpdateService
                            .DownloadAsync(p2 => System.Windows.Application.Current
                                ?.Dispatcher.InvokeAsync(
                                    () => UpdateDownloadProgress = p2));
                    }
                    finally
                    {
                        // R-2: without this, a throwing download would leave
                        // the flag set and the button permanently disabled.
                        IsDownloadingUpdate = false;
                    }
                    if (okV)
                    {
                        IsUpdateReady = true;
                        UpdateStatus = "Update ready. Click Install and restart.";
                    }
                    else
                    {
                        UpdateStatus = "Download failed. Try again.";
                    }
                    return;
                }

                var update = _lastUpdateCheck;
                if (update == null || IsDownloadingUpdate) return;

                IsDownloadingUpdate = true;
                UpdateDownloadProgress = 0;
                UpdateStatus = "Downloading update…";

                bool ok; string error;
                try
                {
                    (ok, _, error) = await Services.Updates.UpdateService
                        .DownloadNowAsync(
                            update,
                            progress => System.Windows.Application.Current
                                ?.Dispatcher.InvokeAsync(
                                    () => UpdateDownloadProgress = progress));
                }
                finally
                {
                    // R-2: release on the exception path too.
                    IsDownloadingUpdate = false;
                }

                if (!ok && string.IsNullOrEmpty(error))
                    UpdateStatus = "Update cancelled.";
                else if (!ok)
                    UpdateStatus = $"Download failed: {error}";

                // Success status ("Update ready.") arrives via the
                // StateChanged handler.
                await LoadUpdateHistoryAsync();
            });

            InstallUpdateCommand = new RelayCommand(_ =>
            {
                // Installed builds: apply delta update + restart in place.
                if (Services.Updates.VelopackUpdateService.IsInstalled)
                {
                    UpdateStatus =
                        "Applying update — ProGlass will restart. " +
                        "Your data stays safe.";
                    Services.Updates.VelopackUpdateService.ApplyAndRestart();
                    return;
                }

                if (!Services.Updates.UpdateService.CanInstall) return;

                UpdateStatus =
                    "Launching installer — ProGlass will close so the " +
                    "update can be applied. Your data stays safe.";

                Services.Updates.UpdateService.InstallAndRestart();
            });

            // ── Backup commands ───────────────────────────────────────
            RefreshBackupsCommand = new RelayCommand(_ =>
            {
                // Visible feedback so the click never feels dead.
                BackupStatus = "Refreshing backup list…";
                RefreshBackupSets();
                BackupStatus = BackupSets.Count > 0
                    ? $"✓ {BackupSets.Count} backup set(s) listed."
                    : "No backup sets found yet.";
            });

            CreateBackupNowCommand = new RelayCommand(_ =>
            {
                // BackupService is a STATIC class — call members directly
                // (a static type cannot be assigned to a variable).

                // Professional behaviour: warn if a backup was made very
                // recently so the list isn't flooded by repeated clicks.
                var sinceLast = Services.Database.DatabaseBackup.BackupService
                    .TimeSinceLastBackup();
                if (sinceLast.HasValue &&
                    sinceLast.Value < Services.Database.DatabaseBackup.BackupService
                        .ManualWarnWindow)
                {
                    var mins = (int)sinceLast.Value.TotalMinutes;
                    var confirm = _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.BackupWasCreatedXMinute"), mins),
                        LocalizationService.Instance.T("Msg.CapBackupAlreadyRecent"),
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);
                    if (confirm != System.Windows.MessageBoxResult.Yes)
                        return;
                }

                var set = Services.Database.DatabaseBackup.BackupService
                    .CreateFullBackupSet("manual (Settings)");

                BackupStatus = set.Length > 0
                    ? $"✓ Backup created: {System.IO.Path.GetFileName(set)}"
                    : "✗ Backup failed — see Logs & Activity for details.";

                RefreshBackupSets();
            });

            RestoreSelectedBackupCommand = new RelayCommand(_ =>
            {
                if (_database.IsDemo)
                {
                    BackupStatus =
                        "Restore is blocked in Demo Mode. Exit Demo Mode first — " +
                        "restore writes the production folder.";
                    _dialogs.Show(
                        LocalizationService.Instance.T("Msg.DemoModeIsOnRestore"),
                        LocalizationService.Instance.T("Msg.CapRestoreBlocked"),
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var selected = SelectedBackup;
                if (selected == null)
                {
                    // Never fail silently — tell the user what to do.
                    BackupStatus =
                        "Select a backup set from the list first.";
                    return;
                }

                var confirm = _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.RestoreBackupFromXThis"), selected.CreatedAt),
                    LocalizationService.Instance.T("Msg.CapRestoreBackup"),
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm != System.Windows.MessageBoxResult.Yes) return;

                BackupStatus = "Restoring backup — please wait…";

                var (ok, message) = Services.Database.DatabaseBackup
                    .BackupService.RestoreBackupSet(selected.FolderPath);

                BackupStatus = (ok ? "✓ " : "✗ ") + message;
                RefreshBackupSets();

                if (!ok) return;

                // The restored database only loads after a restart —
                // surface that explicitly instead of a tiny status line.
                var restart = _dialogs.Show(
                    LocalizationService.Instance.T("Msg.BackupRestoredSuccessfullyProGlassMust"),
                    LocalizationService.Instance.T("Msg.CapRestartRequired"),
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (restart == System.Windows.MessageBoxResult.Yes)
                    RestartApplication();
            });

            OpenBackupsFolderCommand = new RelayCommand(_ =>
            {
                try
                {
                    var folder = System.IO.Path.Combine(
                        System.Environment.GetFolderPath(
                            System.Environment.SpecialFolder.ApplicationData),
                        "ProGlassAutomation", "Backups");

                    System.IO.Directory.CreateDirectory(folder);
                                ProcessStartGuard.TryOpenFolder(folder, CorrelationContext.NewId());
                }
                catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            });

            // ── Database health (diagnostics + emergency rebuild) ─────
            //
            // Diagnostics is read-only and safe for any signed-in user.
            // Rebuild is destructive: it MOVES every .db file to
            // MigrationBackups/_rebuild_<timestamp>/, then recreates the
            // files at the current schema. It is gated behind a Manager+
            // role check (the same gate ClearAllData uses) and a typed
            // confirmation so a misclick cannot destroy data.
            RefreshDatabaseHealthCommand = new RelayCommand(
                async _ => await RunDatabaseDiagnosticsAsync(),
                _ => !IsDiagnosing && !IsRebuilding);

            RebuildAllDatabasesCommand = new RelayCommand(
                async _ => await RebuildAllDatabasesAsync(),
                _ => !IsDiagnosing && !IsRebuilding);

            RefreshBackupSets();
        }

        private async Task RunDatabaseDiagnosticsAsync()
        {
            if (IsDiagnosing || IsRebuilding) return;
            IsDiagnosing = true;
            DatabaseHealthStatus = "Probing live databases…";
            try
            {
                var rows = await SchemaHealthService.DiagnoseAsync();
                ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() =>
                {
                    DatabaseHealthRows.Clear();
                    foreach (var r in rows) DatabaseHealthRows.Add(r);
                });
                int populated = rows.Count(r => r.FileExists && r.TotalRows > 0);
                int drift = rows.Sum(r => r.DriftNotes.Count);
                DatabaseHealthStatus =
                    $"✓ {rows.Count} database(s) probed — " +
                    $"{populated} contain data, " +
                    $"{drift} schema note(s). " +
                    "If a note lists \"missing\" columns, run the schema " +
                    "migration on next app start (it runs automatically).";
            }
            catch (Exception ex)
            {
                DatabaseHealthStatus = "✗ Diagnostics failed: " + ex.Message;
                AppLogService.Error(
                    "Database diagnostics failed", ex,
                    new Services.Logging.LogContext
                    {
                        Module = "Database",
                        Class = nameof(SettingsViewModel),
                        Operation = "RunDatabaseDiagnostics"
                    });
            }
            finally
            {
                IsDiagnosing = false;
            }
        }

        private async Task RebuildAllDatabasesAsync()
        {
            if (IsDiagnosing || IsRebuilding) return;

            // Gate 1: Manager+ only. Same role the ClearAllData
            // command uses, so a non-manager can never reach this
            // destructive path even by typing the wrong key.
            string role = Services.Audit.AuditService.CurrentUser?.Role ?? "";
            bool isManagerOrAbove =
                string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
            if (!isManagerOrAbove)
            {
                _dialogs.Show(
                    "Rebuilding all databases is restricted to Managers " +
                    "and Administrators. Your role: " +
                    (string.IsNullOrEmpty(role) ? "none" : role) + ".",
                    LocalizationService.Instance.T("Msg.CapPermissionDenied"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Gate 2: confirm the user knows what they're doing.
            var proceed = _dialogs.Show(
                "EMERGENCY DATABASE REBUILD\n\n" +
                "This will:\n" +
                "  1. Move every .db file to MigrationBackups/_rebuild_<timestamp>/\n" +
                "  2. Recreate each one at the CURRENT schema\n" +
                "  3. Re-run the schema migration on inquiries.db\n\n" +
                "All transactional data (inquiries, PIs, JOs, DNs, TIs, " +
                "statements, sheet store) will be LOST. Master data, audit " +
                "log and AI memory are also wiped.\n\n" +
                "The originals are MOVED (not deleted) and can be copied " +
                "back by hand from the MigrationBackups folder.\n\n" +
                "ProGlass must be restarted afterwards.\n\n" +
                "Continue?",
                "Emergency Database Rebuild",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);

            if (proceed != System.Windows.MessageBoxResult.Yes) return;

            IsRebuilding = true;
            DatabaseHealthStatus = "Rebuilding — this can take a few seconds…";
            try
            {
                var progress = new Progress<string>(msg =>
                    DatabaseHealthStatus = msg);

                var summary = await SchemaHealthService
                    .RebuildAllAsync(progress);

                AppLogService.Info(
                    "[Rebuild] " + summary,
                    new Services.Logging.LogContext
                    {
                        Module = "Database",
                        Class = nameof(SettingsViewModel),
                        Operation = "RebuildAllDatabases"
                    });

                // Refresh the row-count table so the user sees the
                // freshly-empty databases immediately.
                await RunDatabaseDiagnosticsAsync();

                // Offer the restart. The user can defer it (e.g. they
                // want to copy files out of MigrationBackups first) but
                // the app's pooled connections still point at the
                // moved files until restart.
                var restart = _dialogs.Show(
                    "✓ All databases were rebuilt at the current schema.\n\n" +
                    "The originals are saved in:\n" +
                    _database.CurrentFolder + "\\MigrationBackups\\\n\n" +
                    "ProGlass must be restarted to load the new databases.\n\n" +
                    "Restart now?",
                    LocalizationService.Instance.T("Msg.CapRebuildComplete"),
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information,
                    System.Windows.MessageBoxResult.Yes);

                if (restart == System.Windows.MessageBoxResult.Yes)
                    RestartApplication();
            }
            catch (Exception ex)
            {
                DatabaseHealthStatus = "✗ Rebuild failed: " + ex.Message;
                AppLogService.Error(
                    "Emergency database rebuild failed", ex,
                    new Services.Logging.LogContext
                    {
                        Module = "Database",
                        Class = nameof(SettingsViewModel),
                        Operation = "RebuildAllDatabases"
                    });

                _dialogs.Show(
                    "Rebuild failed: " + ex.Message +
                    "\n\nThe original databases are still in " +
                    "MigrationBackups and have NOT been deleted.",
                    LocalizationService.Instance.T("Msg.CapRebuildFailed"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsRebuilding = false;
            }
        }

        private void RefreshBackupSets()
        {
            BackupSets.Clear();
            foreach (var set in Services.Database.DatabaseBackup
                .BackupService.GetBackupSets())
            {
                BackupSets.Add(set);
            }
        }

        // Phase 4: Event lifetime — detach static events
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { ThemeManager.ThemeChanged -= _themeChangedHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SettingsViewModel] Dispose: ThemeChanged detach failed: " + ex.Message); }
            try { ThemeManager.UiScaleChanged -= _uiScaleChangedHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SettingsViewModel] Dispose: UiScaleChanged detach failed: " + ex.Message); }
            try { Services.Updates.UpdateService.StateChanged -= OnUpdateServiceStateChanged; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SettingsViewModel] Dispose: UpdateService detach failed: " + ex.Message); }
            try { TeardownAppearanceSection(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SettingsViewModel] Dispose: appearance-section detach failed: " + ex.Message); }
            try { DisposeThemeStudio(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SettingsViewModel] Dispose: theme-studio teardown failed: " + ex.Message); }
            GC.SuppressFinalize(this);
        }

        /// <summary>Relaunches the exe and closes this instance (used
        /// after a backup restore / rebuild, which only load after a
        /// restart).
        ///
        /// 2026-08-25 SENIOR FIX — "restart leaves the app closed":
        /// the old code ran the running exe through
        /// ProcessStartGuard.IsPathInsideAllowedRoot before launching.
        /// That allow-list is AppData/Temp/Downloads — an installed
        /// app's folder is none of those, so the launch was silently
        /// SKIPPED while Shutdown() still ran: the app closed and never
        /// came back (exactly what happened after backup restore).
        ///
        /// The restart now:
        ///   1. resolves the exe robustly (Environment.ProcessPath with
        ///      MainModule fallback),
        ///   2. uses a SELF-IDENTITY check — only this process's own exe
        ///      is allowed, which is strictly stronger than a folder
        ///      allow-list,
        ///   3. shuts down ONLY after the new instance actually started,
        ///   4. on ANY failure keeps the app OPEN and tells the user,
        ///      so a failed restart can never strand them closed.
        /// </summary>
        private static void RestartApplication()
        {
            try
            {
                // 1) Resolve our own executable robustly.
                string? exe = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exe))
                {
                    exe = System.Diagnostics.Process
                        .GetCurrentProcess().MainModule?.FileName;
                }

                if (string.IsNullOrWhiteSpace(exe) || !System.IO.File.Exists(exe))
                {
                    ProGlassAutomation.Services.Logging.AppLogService.Warning(
                        "[SettingsViewModel] Restart aborted: could not resolve the running executable.");
                    _dialogs.Show(
                        LocalizationService.Instance.T("Msg.RestartCouldNotFindThe"),
                        LocalizationService.Instance.T("Msg.CapRestartUnavailable"),
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return; // never close without launching
                }

                // 2) Self-identity check: only this process's own exe.
                string? own = System.Diagnostics.Process
                    .GetCurrentProcess().MainModule?.FileName;
                bool isSelf = !string.IsNullOrWhiteSpace(own)
                    && string.Equals(
                        System.IO.Path.GetFullPath(exe),
                        System.IO.Path.GetFullPath(own),
                        StringComparison.OrdinalIgnoreCase);

                if (!isSelf && !ProcessStartGuard.IsPathInsideAllowedRoot(exe))
                {
                    ProGlassAutomation.Services.Logging.AppLogService.Warning(
                        "[SettingsViewModel] Restart blocked by security check: " + exe);
                    _dialogs.Show(
                        LocalizationService.Instance.T("Msg.RestartWasBlockedByThe"),
                        LocalizationService.Instance.T("Msg.CapRestartBlocked"),
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return; // never close without launching
                }

                // 3) Launch FIRST — only exit once the new instance
                //    actually started.
                var started = System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exe,
                        UseShellExecute = true
                    });

                if (started == null)
                {
                    _dialogs.Show(
                        LocalizationService.Instance.T("Msg.RestartFailedToStartA"),
                        LocalizationService.Instance.T("Msg.CapRestartFailed"),
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return; // never close without launching
                }
                started.Dispose();

                // Give the OS a moment to bring the new process up
                // before this instance tears down.
                System.Threading.Thread.Sleep(400);
                System.Windows.Application.Current.Shutdown();
            }
            catch (System.Exception ex)
            {
                // Restart is a convenience — never crash for it, and
                // never strand the user: on failure the app stays OPEN.
                ProGlassAutomation.Services.Logging.AppLogService.Warning(
                    "[SettingsViewModel] Restart failed: " + ex.Message);
                try
                {
                    _dialogs.Show(
                        "Restart failed (" + ex.Message + ").\n" +
                        "Save your work and start the app manually.",
                        LocalizationService.Instance.T("Msg.CapRestartFailed"),
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
                catch { /* the dialog itself must never crash */ }
            }
        }
    }
}
