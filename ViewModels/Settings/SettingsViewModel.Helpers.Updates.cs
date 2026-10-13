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

        private async Task CreateHostSharedFolderAsync()
        {
            if (IsCreatingHostShare) return;
            IsCreatingHostShare = true;
            (CreateSharedFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
            NetworkDataRootStatus = "Creating the local host folder and configuring the Windows SMB share. Approve the administrator prompt if Windows asks; this can take a short time.";
            await Task.Yield();

            try
            {
                string preferredLocalPath = SharedDataRootPath;
                var created = await Task.Run(() =>
                    SharedDataRootService.CreateHostSharedFolder(preferredLocalPath));
                // A failed host setup may mean the fixed share name already
                // points at some other dataset. Do not auto-fill that UNC
                // path on failure; require the operator to resolve/test it.
                if (created.Ok && !string.IsNullOrWhiteSpace(created.Path))
                    SharedDataRootPath = created.Path;
                NetworkDataRootStatus = created.Message;
                RefreshSharedRootWarning();
            }
            catch (Exception ex)
            {
                NetworkDataRootStatus = "✗ Could not create the host share: " + ex.Message;
                AppLogService.Error(
                    "Creating the primary-PC shared data folder failed.", ex,
                    new LogContext { Module = "Database", Operation = "CreateHostSharedFolder" });
            }
            finally
            {
                IsCreatingHostShare = false;
                (CreateSharedFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private async Task FindSharedDataRootsAsync()
        {
            if (IsDiscoveringSharedDataRoots) return;
            IsDiscoveringSharedDataRoots = true;
            (FindSharedDataRootsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            DiscoveredSharedDataRootCandidates.Clear();
            SelectedSharedDataRootCandidate = null;
            OnPropertyChanged(nameof(HasDiscoveredSharedDataRootCandidates));
            NetworkDataRootStatus = "Scanning this PC's local private IPv4 subnets (up to about 2,048 addresses; 30-second safety limit) for ProGlass SMB shares. This does not change the active database.";
            await Task.Yield();

            try
            {
                var candidates = await Task.Run(
                    () => SharedDataRootService.DiscoverSharedDataRootsAsync());
                foreach (string candidate in candidates)
                    DiscoveredSharedDataRootCandidates.Add(candidate);
                OnPropertyChanged(nameof(HasDiscoveredSharedDataRootCandidates));
                // Discovery is candidate listing only. Do not preselect the
                // first network host: the operator must choose the intended
                // company dataset explicitly, even when there is one result.
                SelectedSharedDataRootCandidate = null;
                (UseDiscoveredSharedDataRootCommand as RelayCommand)?.RaiseCanExecuteChanged();

                NetworkDataRootStatus = candidates.Count switch
                {
                    0 => "No ProGlass-marked share was found in the local private subnets scanned (the scan stops after 30 seconds). On the primary PC use Create Share on This PC. On a client, confirm the host is on the same LAN/Wi-Fi, awake, using a Private/Domain network profile, and permits SMB/TCP 445; then enter \\\\HOSTNAME\\ProGlassData or \\\\192.168.x.x\\ProGlassData manually. A Windows host/domain credential may be required.",
                    1 => "Found one ProGlass share candidate. Select it from the list, choose Use Selected Path, then Test Connection. It is only a draft; ProGlass will not switch or merge data until you review and Save + Restart.",
                    _ => $"Found {candidates.Count} ProGlass share candidates. Select the correct company host, choose Use Selected Path, then Test Connection. Nothing is selected or applied automatically."
                };
            }
            catch (Exception ex)
            {
                NetworkDataRootStatus = "✗ LAN share discovery failed: " + ex.Message
                    + " You can still enter \\\\HOSTNAME\\ProGlassData or \\\\192.168.x.x\\ProGlassData manually.";
                AppLogService.Warning(
                    "LAN discovery of ProGlass shared data roots failed.", ex,
                    new LogContext { Module = "Database", Operation = "DiscoverSharedDataRoots" });
            }
            finally
            {
                IsDiscoveringSharedDataRoots = false;
                (FindSharedDataRootsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
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
                NetworkDataRootStatus = "This PC currently uses its local database. On the primary PC choose Create Share on This PC; on clients choose Find on LAN or enter the host computer name/IP. The example \\\\SERVER is not a real host name.";

            TestSharedDataRootCommand = new RelayCommand(_ =>
            {
                var result = SharedDataRootService.Probe(SharedDataRootPath);
                NetworkDataRootStatus = result.Message;
                RefreshSharedRootWarning();
            });

            // Host creation is a deliberate action: it creates a local
            // data-only folder on this PC, requests administrator approval
            // to publish a private-LAN SMB share, and returns the UNC path.
            // It never treats a local C:\\ path as if clients could use it.
            CreateSharedFolderCommand = new RelayCommand(
                unused => _ = CreateHostSharedFolderAsync(),
                _ => !IsCreatingHostShare);

            FindSharedDataRootsCommand = new RelayCommand(
                unused => _ = FindSharedDataRootsAsync(),
                _ => !IsDiscoveringSharedDataRoots);

            UseDiscoveredSharedDataRootCommand = new RelayCommand(
                _ =>
                {
                    string? selectedCandidate = SelectedSharedDataRootCandidate;
                    if (string.IsNullOrWhiteSpace(selectedCandidate)) return;
                    SharedDataRootPath = selectedCandidate;
                    NetworkDataRootStatus = "Candidate path selected: " + SharedDataRootPath
                        + ". Test Connection and verify the intended company data before Save + Restart. "
                        + "The active database has not changed and no data was copied or merged.";
                },
                _ => !string.IsNullOrWhiteSpace(SelectedSharedDataRootCandidate));

            PrepareSharedDataRootCommand = new RelayCommand(_ =>
            {
                if (string.IsNullOrWhiteSpace(SharedDataRootPath)
                    || SharedDataRootService.IsServerPlaceholderPath(SharedDataRootPath))
                {
                    NetworkDataRootStatus = "Enter a real shared path first. On the primary PC click Create Share on This PC; on a client click Find on LAN or enter the actual host computer name/IP (\\\\HOSTNAME\\ProGlassData or \\\\192.168.x.x\\ProGlassData). The literal \\\\SERVER is only an example.";
                    return;
                }

                var targetProbe = SharedDataRootService.Probe(SharedDataRootPath);
                if (!targetProbe.Ok || !targetProbe.LooksLikeNetworkPath)
                {
                    NetworkDataRootStatus = targetProbe.Ok
                        ? "✗ Prepare Folder requires a UNC or mapped network share, not a local C:\\ path. On the primary PC use Create Share on This PC first."
                        : targetProbe.Message;
                    RefreshSharedRootWarning();
                    return;
                }

                if (!SharedDataRootService.IsCurrentComputerShareRoot(targetProbe.NormalizedPath))
                {
                    NetworkDataRootStatus = "✗ Prepare Folder is host-only. Use the primary PC's own \\\\HOSTNAME\\ProGlassData path to seed or set journal mode; client PCs should test the intended share and use Save + Restart. No company records were copied or changed.";
                    return;
                }

                bool targetHasNoTopLevelDatabases = targetProbe.Message.Contains(
                    "No database files found yet", StringComparison.OrdinalIgnoreCase);
                string emptyShareNotice = targetHasNoTopLevelDatabases
                    && !CopyLocalDataOnEnable
                    ? "The share has no top-level database files and copy-local is off. Preparing without copying leaves this workstation's existing local records separate; the shared database will start blank/new. Only cancel, enable copy, and retry if this is the primary PC and its local dataset is the approved company dataset.\n\n"
                    : "";

                var confirm = _dialogs.Show(
                    emptyShareNotice +
                    "This will verify the shared folder and copy this workstation's local ProGlass dataset only if the folder is empty and the copy-local checkbox is enabled (off by default). Use that option only on the primary PC when its local dataset is the approved company dataset. Do not prepare from a client PC or use it to merge/overwrite an existing company dataset. Close ProGlass on all other workstations while preparation changes shared database journal settings.\n\n" +
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
                    var created = SharedDataRootService.CreateSharedFolder(
                        SharedDataRootPath, publishWindowsShare: false);
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
                        reviewWarning + emptyShareNotice +
                        "The shared folder is prepared. To switch this workstation to it, the root setting must be saved and ProGlass restarted now.\n\n" +
                        "If you choose No, the folder remains prepared but this workstation keeps its current active root and no root change is saved.\n\n" +
                        "Restart this workstation now?",
                        "Apply prepared shared folder",
                        System.Windows.MessageBoxButton.YesNo,
                        offlineDataNeedsReview || !string.IsNullOrEmpty(emptyShareNotice)
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
                if (!probe.Ok || !probe.LooksLikeNetworkPath)
                {
                    NetworkDataRootStatus = probe.Ok
                        ? "✗ This is a local folder, not a network share. On the primary PC use Create Share on This PC, then Test the returned UNC path; clients must use the host name/IP UNC path."
                        : probe.Message;
                    RefreshSharedRootWarning();
                    return;
                }

                // This is the explicit "use the selected share" action.
                // The separate Revert button is the only action that saves
                // local mode; a fresh client must not remain local merely
                // because no hidden mode checkbox was previously ticked.
                UseSharedDataRoot = true;
                bool offlineDataNeedsReview =
                    _config.DataConnectivity?.SharedRootOfflineFallbackNeedsReview == true;
                string reviewWarning = offlineDataNeedsReview
                    ? "This workstation may contain local-only changes made while the share was offline. Reconnecting will not merge those records into the company dataset; review and reconcile them separately first.\n\n"
                    : "";
                string dataSetWarning = SelectedDataRootDiffersFromLiveRoot
                    ? "Local and shared folders may contain different records. Changing roots does not merge them; verify the intended company dataset and make a backup before switching.\n\n"
                    : "";
                bool targetNeedsIdentityReview =
                    probe.Message.Contains("No database files found yet", StringComparison.OrdinalIgnoreCase)
                    || probe.Message.Contains("manifest not found", StringComparison.OrdinalIgnoreCase)
                    || probe.Message.Contains("not identified as a ProGlass", StringComparison.OrdinalIgnoreCase)
                    || probe.Message.Contains("manifest file exists but could not be read", StringComparison.OrdinalIgnoreCase);
                string targetIdentityWarning = targetNeedsIdentityReview
                    ? "The tested share has no top-level database files and/or no recognized ProGlass shared-root manifest. Applying it could start a new blank dataset. Proceed only if you have confirmed that this is the intended company share; if it is a new share to seed, use Prepare Folder on the primary PC only.\n\n"
                    : "";
                var restart = _dialogs.Show(
                    reviewWarning + dataSetWarning + targetIdentityWarning +
                    "This will enable the tested shared root on this workstation after restart. It does not copy or merge data; use Prepare Folder first only if the company share is empty and this local dataset is explicitly approved to seed it. If you choose No, the saved and active roots remain unchanged. Restart this workstation to apply the selected shared root?",
                    "Apply shared data root",
                    System.Windows.MessageBoxButton.YesNo,
                    offlineDataNeedsReview
                        ? System.Windows.MessageBoxImage.Warning
                        : System.Windows.MessageBoxImage.Question);
                if (restart != System.Windows.MessageBoxResult.Yes)
                {
                    NetworkDataRootStatus = "No root settings were saved. The current active root is unchanged; the tested share remains a draft until you explicitly apply it with a restart.";
                    RefreshSharedRootWarning();
                    return;
                }

                var snapshot = CaptureDataRootConfig();
                var applied = SharedDataRootService.ApplyToConfig(_config, true,
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
                    + " Restarting now so all modules open the selected shared root.";
                if (!RestartApplication())
                {
                    ReportRootActivationFailure(snapshot,
                        "ProGlass could not restart to apply the selected data root.");
                    return;
                }

                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    "Shared database enabled",
                    $"Shared data root saved: {_config.DataConnectivity.SharedDataRootPath}",
                    "Settings",
                    "DataConnectivity",
                    "Company LAN/Wi-Fi shared database connectivity setting changed.");
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
