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
        private sealed record DataRootConfigSnapshot(
            bool UseSharedDataRoot,
            string SharedDataRootPath,
            bool OfflineFallbackNeedsReview,
            bool LocalModeNoticePending,
            bool ReconnectNoticeShown);

        private DataRootConfigSnapshot CaptureDataRootConfig()
        {
            var data = _config.DataConnectivity ??= new DataConnectivitySettings();
            return new DataRootConfigSnapshot(
                data.UseSharedDataRoot,
                data.SharedDataRootPath ?? "",
                data.SharedRootOfflineFallbackNeedsReview,
                data.SharedRootLocalModeNoticePending,
                data.SharedRootReconnectNoticeShown);
        }

        private bool RestoreDataRootConfig(DataRootConfigSnapshot snapshot)
        {
            var data = _config.DataConnectivity ??= new DataConnectivitySettings();
            data.UseSharedDataRoot = snapshot.UseSharedDataRoot;
            data.SharedDataRootPath = snapshot.SharedDataRootPath;
            data.SharedRootOfflineFallbackNeedsReview = snapshot.OfflineFallbackNeedsReview;
            data.SharedRootLocalModeNoticePending = snapshot.LocalModeNoticePending;
            data.SharedRootReconnectNoticeShown = snapshot.ReconnectNoticeShown;
            return ConfigService.TrySave(_config);
        }

        private void ReportRootActivationFailure(
            DataRootConfigSnapshot snapshot, string message)
        {
            bool restored = RestoreDataRootConfig(snapshot);
            NetworkDataRootStatus = restored
                ? message + " The previous root settings were restored; this session remains on its active root."
                : message + " The previous root settings could not be saved. Do not continue entering data or restart until the saved configuration has been reviewed.";
            if (!restored)
            {
                AppLogService.Critical(
                    "Could not restore the prior data-root configuration after an unsuccessful root change.",
                    null,
                    new LogContext { Module = "Database", Operation = "RestoreDataRootConfig" });
                try
                {
                    _dialogs.Show(
                        "ProGlass is still using the previously active data root, but its saved root settings could not be restored. Do not enter new records or restart until the saved configuration has been reviewed; the next launch could otherwise open a different dataset.",
                        "Data-root settings need attention",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
                catch (Exception dialogEx)
                {
                    AppLogService.Warning(
                        "Could not display the critical data-root rollback warning.",
                        dialogEx,
                        new LogContext { Module = "Database", Operation = "RestoreDataRootConfig" });
                }
            }
            RefreshSharedRootWarning();
        }

        private void InitializeLanWifiCommands()
        {
            // Root settings remain an explicit draft until Save + Restart.
            // Initialize them from the persisted config once per VM instance.
            var dataSettings = _config?.DataConnectivity;
            _useSharedDataRootDraft = dataSettings?.UseSharedDataRoot ?? false;
            _sharedDataRootPathDraft = dataSettings?.SharedDataRootPath ?? "";

            // ── LAN / Wi-Fi shared database commands ──────────────────
            // Surface any startup offline-fallback state on the Data card.
            RefreshSharedRootWarning();

            // Reflect the live mode in the status line at page open, so the
            // card never claims "local mode" while the company is actually
            // on a shared root (2026-09-24).
            if (AppDataRoot.IsSharedDataRootEnabled)
                NetworkDataRootStatus = "✓ Shared company database is active: " + AppDataRoot.SharedDataRoot;
            else if (AppDataRoot.SharedRootFallbackActive)
                NetworkDataRootStatus = AppDataRoot.SharedRootFallbackReason;
            else
                NetworkDataRootStatus = "Local database mode. Press Create Folder to make C:\\ProGlassData and publish the Windows share, or type an existing share path.";

            TestSharedDataRootCommand = new RelayCommand(_ =>
            {
                var result = SharedDataRootService.Probe(SharedDataRootPath);
                NetworkDataRootStatus = result.Message;
                RefreshSharedRootWarning();
            });

            // Create the folder (and the Windows share, when Windows allows
            // it) before any backup. The old Prepare path cancelled on a
            // failed safety backup and never created the folder at all.
            CreateSharedFolderCommand = new RelayCommand(_ =>
            {
                var created = SharedDataRootService.CreateSharedFolder(SharedDataRootPath);
                if (!string.IsNullOrWhiteSpace(created.Path))
                    SharedDataRootPath = created.Path;
                NetworkDataRootStatus = created.Message;
                RefreshSharedRootWarning();
            });

            PrepareSharedDataRootCommand = new RelayCommand(_ =>
            {
                if (string.IsNullOrWhiteSpace(SharedDataRootPath))
                {
                    var created = SharedDataRootService.CreateSharedFolder(null);
                    if (!string.IsNullOrWhiteSpace(created.Path))
                        SharedDataRootPath = created.Path;
                    // Set after the path setter — that setter replaces the
                    // status line with "run Test Connection".
                    NetworkDataRootStatus = created.Message;
                    if (!created.Ok)
                    {
                        RefreshSharedRootWarning();
                        return;
                    }
                }

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
                    // A failed backup must not hide the folder. Copy is the
                    // only step that needed the backup; the folder itself
                    // can still be created so the company share exists.
                    var created = SharedDataRootService.CreateSharedFolder(SharedDataRootPath);
                    if (!string.IsNullOrWhiteSpace(created.Path))
                        SharedDataRootPath = created.Path;
                    NetworkDataRootStatus = "✗ Safety backup failed, so local data was not copied. "
                        + (created.Ok
                            ? created.Message
                            : "The folder was also not created: " + created.Message);
                    RefreshSharedRootWarning();
                    return;
                }

                var (ok, message) = SharedDataRootService.PrepareSharedFolder(
                    SharedDataRootPath, CopyLocalDataOnEnable);
                NetworkDataRootStatus = message;
                if (ok)
                {
                    UseSharedDataRoot = true;
                    bool offlineDataNeedsReview =
                        _config.DataConnectivity?.SharedRootOfflineFallbackNeedsReview == true;
                    string reviewWarning = offlineDataNeedsReview
                        ? "This workstation may contain local-only changes made while the share was offline. Preparation seeds only an empty folder; it does not merge data into an existing company dataset. Review/reconcile the local records separately before reconnecting.\n\n"
                        : "";
                    var restart = _dialogs.Show(
                        reviewWarning +
                        "The shared folder is prepared. To switch this workstation to it, the root setting must be saved and ProGlass restarted now.\n\n" +
                        "If you choose No, the folder remains prepared but this workstation keeps its current active root and no root change is saved.\n\n" +
                        "Restart this workstation now?",
                        "Apply prepared shared folder",
                        System.Windows.MessageBoxButton.YesNo,
                        offlineDataNeedsReview
                            ? System.Windows.MessageBoxImage.Warning
                            : System.Windows.MessageBoxImage.Question);
                    if (restart != System.Windows.MessageBoxResult.Yes)
                    {
                        NetworkDataRootStatus = message +
                            " The folder is ready, but no root change was saved; this session remains on its current active root.";
                        RefreshSharedRootWarning();
                        return;
                    }

                    var snapshot = CaptureDataRootConfig();
                    var applied = SharedDataRootService.ApplyToConfig(_config, true,
                        SharedDataRootPath, CopyLocalDataOnEnable);
                    if (!applied.Ok)
                    {
                        NetworkDataRootStatus = applied.Message;
                        RefreshSharedRootWarning();
                        return;
                    }
                    if (!ConfigService.TrySave(_config))
                    {
                        ReportRootActivationFailure(snapshot,
                            "Could not save the prepared shared-root settings.");
                        return;
                    }

                    NetworkDataRootStatus = applied.Message
                        + " Restarting now so all modules open the selected shared root.";
                    OnPropertyChanged(nameof(CurrentDataRootDisplay));
                    RefreshSharedRootWarning();
                    if (!RestartApplication())
                        ReportRootActivationFailure(snapshot,
                            "ProGlass could not restart to apply the prepared shared root.");
                }
            });

            ApplySharedDataRootCommand = new RelayCommand(_ =>
            {
                var probe = SharedDataRootService.Probe(SharedDataRootPath);
                if (UseSharedDataRoot && !probe.Ok)
                {
                    NetworkDataRootStatus = probe.Message;
                    RefreshSharedRootWarning();
                    return;
                }

                bool offlineDataNeedsReview =
                    UseSharedDataRoot &&
                    _config.DataConnectivity?.SharedRootOfflineFallbackNeedsReview == true;
                string reviewWarning = offlineDataNeedsReview
                    ? "This workstation may contain local-only changes made while the share was offline. Reconnecting will not merge those records into the company dataset; review and reconcile them separately first.\n\n"
                    : "";
                string dataSetWarning = SelectedDataRootDiffersFromLiveRoot
                    ? "Local and shared folders may contain different records. Changing roots does not merge them; verify the intended dataset and make a backup before switching.\n\n"
                    : "";
                var restart = _dialogs.Show(
                    reviewWarning + dataSetWarning +
                    "Applying a root alone does not copy or merge data; use Prepare Folder first if an empty share should be seeded from local data. The selected root is saved only if ProGlass restarts now. If you choose No, the saved and active roots remain unchanged. Restart this workstation to apply the selected root?",
                    "Apply data-root settings",
                    System.Windows.MessageBoxButton.YesNo,
                    offlineDataNeedsReview
                        ? System.Windows.MessageBoxImage.Warning
                        : System.Windows.MessageBoxImage.Question);
                if (restart != System.Windows.MessageBoxResult.Yes)
                {
                    NetworkDataRootStatus = "No root settings were saved. The current active root is unchanged; your selection remains a draft until you apply it with a restart.";
                    RefreshSharedRootWarning();
                    return;
                }

                var snapshot = CaptureDataRootConfig();
                var applied = SharedDataRootService.ApplyToConfig(_config, UseSharedDataRoot,
                    SharedDataRootPath, CopyLocalDataOnEnable);
                if (!applied.Ok)
                {
                    // Never report success on a half-configured shared mode
                    // (2026-09-24) — an invalid target must not be persisted.
                    NetworkDataRootStatus = applied.Message;
                    RefreshSharedRootWarning();
                    return;
                }

                if (!ConfigService.TrySave(_config))
                {
                    ReportRootActivationFailure(snapshot,
                        "Could not save the selected data-root settings.");
                    return;
                }

                NetworkDataRootStatus = applied.Message
                    + " Restarting now so all modules open the selected root.";
                if (!RestartApplication())
                {
                    ReportRootActivationFailure(snapshot,
                        "ProGlass could not restart to apply the selected data root.");
                    return;
                }

                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    UseSharedDataRoot ? "Shared database enabled" : "Shared database disabled",
                    UseSharedDataRoot
                        ? $"Shared data root saved: {_config.DataConnectivity.SharedDataRootPath}"
                        : "Workstation will use local AppData database root after restart.",
                    "Settings",
                    "DataConnectivity",
                    "Company LAN/Wi-Fi database connectivity setting changed.");
            });

            RevertToLocalDataRootCommand = new RelayCommand(_ =>
            {
                var confirm = _dialogs.Show(
                    "This workstation will stop using the shared company database and return to its own local AppData database.\n\n" +
                    "Other workstations stay on the shared folder. Local and shared databases are not copied or merged automatically; verify the local dataset and back up/reconcile data before switching.\n\n" +
                    "The setting is saved only if this workstation restarts now; choosing No leaves the current root and saved settings unchanged.\n\n" +
                    "Save the local-root setting and restart ProGlass now?",
                    "Revert to local database",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);
                if (confirm != System.Windows.MessageBoxResult.Yes) return;

                UseSharedDataRoot = false;
                var snapshot = CaptureDataRootConfig();
                var applied = SharedDataRootService.ApplyToConfig(_config, false,
                    SharedDataRootPath, CopyLocalDataOnEnable);
                if (!applied.Ok)
                {
                    NetworkDataRootStatus = applied.Message;
                    RefreshSharedRootWarning();
                    return;
                }

                if (!ConfigService.TrySave(_config))
                {
                    ReportRootActivationFailure(snapshot,
                        "Could not save the local-root setting.");
                    return;
                }

                NetworkDataRootStatus = "✓ Saved. Restarting now to return this workstation to its local database.";
                if (!RestartApplication())
                {
                    ReportRootActivationFailure(snapshot,
                        "ProGlass could not restart to apply the local-root setting.");
                    return;
                }

                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    "Shared database disabled (revert to local)",
                    "Workstation will use local AppData database root after restart.",
                    "Settings",
                    "DataConnectivity",
                    "Company LAN/Wi-Fi shared database was reverted to local on this workstation.");
            });

        }

    }
}
