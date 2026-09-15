// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Helpers.Updates.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Settings v3 redesign (2026-09-17): the 580-line InitializeV2Sections
// command-wiring method became one dispatcher + per-domain init methods.
// Same partial class, members moved verbatim — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════
// NOTE: owns update + LAN/Wi-Fi command wiring
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
        private void InitializeUpdateCommands()
        {
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

        }

        private void InitializeLanWifiCommands()
        {
            // ── LAN / Wi-Fi shared database commands ──────────────────
            TestSharedDataRootCommand = new RelayCommand(_ =>
            {
                var result = SharedDataRootService.Probe(SharedDataRootPath);
                NetworkDataRootStatus = result.Message;
            });

            PrepareSharedDataRootCommand = new RelayCommand(_ =>
            {
                var confirm = _dialogs.Show(
                    "This will verify the shared folder and, if enabled, copy the current local ProGlass dataset only when the shared folder is empty.\n\n" +
                    "Use a reliable Windows shared folder on the same LAN/Wi-Fi. Do not use cloud-sync folders for live databases. Continue?",
                    "Prepare shared company database folder",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);
                if (confirm != System.Windows.MessageBoxResult.Yes) return;

                var backup = Services.Database.DatabaseBackup.BackupService
                    .CreateFullBackupSet("before shared database preparation");
                if (string.IsNullOrWhiteSpace(backup))
                {
                    NetworkDataRootStatus = "✗ Safety backup failed. Shared database preparation was cancelled.";
                    return;
                }

                var (ok, message) = SharedDataRootService.PrepareSharedFolder(
                    SharedDataRootPath, CopyLocalDataOnEnable);
                NetworkDataRootStatus = message;
                if (ok)
                {
                    UseSharedDataRoot = true;
                    SharedDataRootService.ApplyToConfig(_config, true,
                        SharedDataRootPath, CopyLocalDataOnEnable);
                    ConfigService.Save(_config);
                    OnPropertyChanged(nameof(CurrentDataRootDisplay));
                }
            });

            ApplySharedDataRootCommand = new RelayCommand(_ =>
            {
                var probe = SharedDataRootService.Probe(SharedDataRootPath);
                if (UseSharedDataRoot && !probe.Ok)
                {
                    NetworkDataRootStatus = probe.Message;
                    return;
                }

                SharedDataRootService.ApplyToConfig(_config, UseSharedDataRoot,
                    SharedDataRootPath, CopyLocalDataOnEnable);
                ConfigService.Save(_config);
                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    UseSharedDataRoot ? "Shared database enabled" : "Shared database disabled",
                    UseSharedDataRoot
                        ? $"Shared data root saved: {_config.DataConnectivity.SharedDataRootPath}"
                        : "Workstation will use local AppData database root after restart.",
                    "Settings",
                    "DataConnectivity",
                    "Company LAN/Wi-Fi database connectivity setting changed.");
                NetworkDataRootStatus = "✓ Connectivity settings saved. Restart ProGlass on every workstation so all modules open the same database root.";

                var restart = _dialogs.Show(
                    "Connectivity settings were saved. ProGlass must restart before any module opens the new database root.\n\nRestart this workstation now?",
                    "Restart required",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
                if (restart == System.Windows.MessageBoxResult.Yes)
                    RestartApplication();
            });

        }

    }
}
