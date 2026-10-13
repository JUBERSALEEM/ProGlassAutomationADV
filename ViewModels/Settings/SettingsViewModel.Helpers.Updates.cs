using Microsoft.Win32;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed partial class SettingsViewModel
    {
        private sealed record DataRootConfigSnapshot(
            bool UseSharedDataRoot,
            string SharedDataRootPath,
            bool OfflineFallbackNeedsReview,
            bool LocalModeNoticePending,
            bool ReconnectNoticeShown);

        private string _lastSuccessfullyTestedSharedDataRootPath = "";

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
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
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

        private void BrowseSharedDataRoot()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select the shared ProGlass data folder",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
                return;

            SharedDataRootPath = dialog.FolderName;
            NetworkDataRootStatus = "Folder selected. Test access before applying; the active data root has not changed.";
        }

        private async Task TestSharedDataRootAsync()
        {
            if (IsTestingSharedDataRoot) return;
            IsTestingSharedDataRoot = true;
            NetworkDataRootStatus = "Testing folder access from this workstation…";
            (BrowseSharedDataRootCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (TestSharedDataRootCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ApplySharedDataRootCommand as RelayCommand)?.RaiseCanExecuteChanged();

            string pathBeingTested = SharedDataRootPath;
            try
            {
                var result = await Task.Run(
                    () => SharedDataRootService.Probe(pathBeingTested));

                bool inputWasValid = AppDataRoot.TryNormalizeSharedRoot(
                    pathBeingTested, out var testedPath);
                bool pathStillMatches = inputWasValid
                    && AppDataRoot.TryNormalizeSharedRoot(
                        SharedDataRootPath, out var currentPath)
                    && currentPath.Equals(
                        testedPath, StringComparison.OrdinalIgnoreCase);
                _lastSuccessfullyTestedSharedDataRootPath =
                    result.Ok && result.LooksLikeNetworkPath && pathStillMatches
                        ? result.NormalizedPath
                        : "";
                NetworkDataRootStatus = result.Message;
                if (inputWasValid && !pathStillMatches)
                    NetworkDataRootStatus += " The path changed during the test; test the current path before applying.";
                RefreshSharedRootWarning();
            }
            catch (Exception ex)
            {
                _lastSuccessfullyTestedSharedDataRootPath = "";
                NetworkDataRootStatus = "✗ Folder test failed: " + ex.Message;
                AppLogService.Warning(
                    "Testing the selected shared data folder failed.", ex,
                    new LogContext { Module = "Database", Operation = "ProbeSharedDataRoot" });
            }
            finally
            {
                IsTestingSharedDataRoot = false;
                (BrowseSharedDataRootCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (TestSharedDataRootCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ApplySharedDataRootCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private bool CanApplySharedDataRoot()
        {
            return !IsTestingSharedDataRoot
                && !string.IsNullOrWhiteSpace(_lastSuccessfullyTestedSharedDataRootPath)
                && AppDataRoot.TryNormalizeSharedRoot(
                    SharedDataRootPath, out var selectedPath)
                && selectedPath.Equals(
                    _lastSuccessfullyTestedSharedDataRootPath,
                    StringComparison.OrdinalIgnoreCase);
        }

        private void ApplySharedDataRoot()
        {
            // Re-test immediately before applying so a stale earlier test
            // cannot switch this workstation to a now-unavailable dataset.
            var probe = SharedDataRootService.Probe(SharedDataRootPath);
            if (!probe.Ok || !probe.LooksLikeNetworkPath
                || !probe.NormalizedPath.Equals(
                    _lastSuccessfullyTestedSharedDataRootPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                _lastSuccessfullyTestedSharedDataRootPath = "";
                NetworkDataRootStatus = probe.Ok
                    ? probe.Message + " Test the current network path again before applying."
                    : probe.Message;
                (ApplySharedDataRootCommand as RelayCommand)?.RaiseCanExecuteChanged();
                RefreshSharedRootWarning();
                return;
            }

            bool offlineDataNeedsReview =
                _config.DataConnectivity?.SharedRootOfflineFallbackNeedsReview == true;
            string offlineWarning = offlineDataNeedsReview
                ? "This workstation may contain local-only changes made while the shared folder was unavailable. They will not be copied or merged into the shared data. Review and reconcile them separately before reconnecting.\n\n"
                : "";
            string currentRoot = Path.GetFullPath(AppDataRoot.CurrentFolder);
            bool changingRoot = !probe.NormalizedPath.Equals(
                currentRoot, StringComparison.OrdinalIgnoreCase);
            string rootChangeWarning = changingRoot
                ? "The selected folder differs from this workstation's active data folder and may contain a different company dataset. ProGlass will not copy, merge, or overwrite records. Confirm that this is the intended shared folder.\n\n"
                : "";
            string emptyFolderWarning = probe.DatabaseFileCount == 0
                ? "No top-level ProGlass database files were found. Applying this path may open or create a separate, empty dataset. Confirm that the folder is intended.\n\n"
                : "";

            var restart = _dialogs.Show(
                offlineWarning + rootChangeWarning + emptyFolderWarning +
                "The folder test passed from this workstation: " + probe.NormalizedPath + ". ProGlass will use this network path after restart; no data will be copied, merged, or overwritten. Enter the same UNC or mapped-drive path on each workstation.\n\n" +
                "Save this path and restart ProGlass now?",
                "Apply shared data folder",
                MessageBoxButton.YesNo,
                offlineDataNeedsReview || probe.DatabaseFileCount == 0
                    ? MessageBoxImage.Warning
                    : MessageBoxImage.Question);
            if (restart != MessageBoxResult.Yes)
            {
                NetworkDataRootStatus = "No root settings were saved. The active data root is unchanged; the tested path remains a draft.";
                RefreshSharedRootWarning();
                return;
            }

            var snapshot = CaptureDataRootConfig();
            UseSharedDataRoot = true;
            var applied = SharedDataRootService.ApplyToConfig(
                _config, true, probe.NormalizedPath);
            if (!applied.Ok)
            {
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
            OnPropertyChanged(nameof(CurrentDataRootDisplay));
            RefreshSharedRootWarning();
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
                "Shared company database folder setting changed.");
        }

        private void RevertToLocalDataRoot()
        {
            var confirm = _dialogs.Show(
                "This workstation will stop using the shared company database and return to its own local AppData database. Other workstations stay on the shared folder. Local and shared records are not copied or merged; verify and back up the local data before switching.\n\n" +
                "Save the local-root setting and restart ProGlass now?",
                "Return to local database",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            var snapshot = CaptureDataRootConfig();
            UseSharedDataRoot = false;
            var applied = SharedDataRootService.ApplyToConfig(
                _config, false, SharedDataRootPath);
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
                "Shared company database was reverted to local on this workstation.");
        }

        private void InitializeLanWifiCommands()
        {
            var dataSettings = _config?.DataConnectivity;
            _useSharedDataRootDraft = dataSettings?.UseSharedDataRoot ?? false;
            _sharedDataRootPathDraft = dataSettings?.SharedDataRootPath ?? "";
            _lastSuccessfullyTestedSharedDataRootPath = "";
            RefreshSharedRootWarning();

            if (AppDataRoot.IsSharedDataRootEnabled)
                NetworkDataRootStatus = "✓ Shared company data folder is active: " + AppDataRoot.SharedDataRoot;
            else if (AppDataRoot.SharedRootFallbackActive)
                NetworkDataRootStatus = AppDataRoot.SharedRootFallbackReason;

            BrowseSharedDataRootCommand = new RelayCommand(
                _ => BrowseSharedDataRoot(),
                _ => !IsTestingSharedDataRoot);

            TestSharedDataRootCommand = new RelayCommand(
                _ => { _ = TestSharedDataRootAsync(); },
                _ => !IsTestingSharedDataRoot);

            ApplySharedDataRootCommand = new RelayCommand(
                _ => ApplySharedDataRoot(),
                _ => CanApplySharedDataRoot());

            RevertToLocalDataRootCommand = new RelayCommand(
                _ => RevertToLocalDataRoot(),
                _ => !IsTestingSharedDataRoot);
        }
    }
}
