// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Data.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.1 — SettingsViewModel partial split. This part owns the Data
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
        //  v2 — DATABASE HEALTH (diagnostics + emergency rebuild)
        //  SettingsViewModel is the only intended caller of
        //  SchemaHealthService; the service is destructive so we gate
        //  it behind a Manager+ role check.
        // ══════════════════════════════════════════════════════════════

        /// <summary>Row per ProGlass Automation database file.
        /// Populated by <see cref="RefreshDatabaseHealthCommand"/>.</summary>
        public ObservableCollection<DatabaseHealthRow> DatabaseHealthRows { get; } =
            new();

        /// <summary>
        /// Live license status. The same singleton
        /// instance is used by the About page's license
        /// card, so changes propagate everywhere. The
        /// VM subscribes to LicenseService.StatusChanged
        /// via its own constructor and calls Refresh()
        /// automatically.
        /// </summary>
        public ProGlassAutomation.ViewModels.Licensing.LicenseStatusViewModel
            LicenseStatus =>
            ProGlassAutomation.ViewModels.Licensing.LicenseStatusViewModel.Instance;

        private string _databaseHealthStatus = "Press REFRESH to probe the live databases.";
        public string DatabaseHealthStatus
        {
            get => _databaseHealthStatus;
            private set { _databaseHealthStatus = value; OnPropertyChanged(); }
        }

        private bool _isDiagnosing;
        public bool IsDiagnosing
        {
            get => _isDiagnosing;
            private set
            {
                if (_isDiagnosing == value) return;
                _isDiagnosing = value;
                OnPropertyChanged();
            }
        }

        private bool _isRebuilding;
        public bool IsRebuilding
        {
            get => _isRebuilding;
            private set
            {
                if (_isRebuilding == value) return;
                _isRebuilding = value;
                OnPropertyChanged();
            }
        }

        public ICommand RefreshDatabaseHealthCommand { get; private set; }
        public ICommand RebuildAllDatabasesCommand { get; private set; }



        // Root selection is a draft until the explicit Save + Restart
        // command is confirmed. Keeping these values out of the shared
        // AppConfig object prevents an unrelated debounced save elsewhere
        // in the app from switching roots behind the operator's back.
        private bool _useSharedDataRootDraft;
        private string _sharedDataRootPathDraft = "";

        // ══════════════════════════════════════════════════════════════
        //  LAN / Wi-Fi SHARED DATABASE CONNECTIVITY
        // ══════════════════════════════════════════════════════════════

        public bool UseSharedDataRoot
        {
            get => _useSharedDataRootDraft;
            set
            {
                if (_useSharedDataRootDraft == value) return;
                _useSharedDataRootDraft = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DataRootModeDisplay));
                OnPropertyChanged(nameof(CurrentDataRootDisplay));
                NetworkDataRootStatus = value
                    ? "Shared database mode selected. Test/prepare the folder, then Save + Restart on every workstation."
                    : "Local database mode selected. Save + Restart to return this workstation to local AppData.";
            }
        }

        public string SharedDataRootPath
        {
            get => _sharedDataRootPathDraft;
            set
            {
                var v = value ?? "";
                if (_sharedDataRootPathDraft == v) return;
                _sharedDataRootPathDraft = v;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DataRootModeDisplay));
                OnPropertyChanged(nameof(CurrentDataRootDisplay));
                NetworkDataRootStatus = "Shared folder changed. Run Test Connection before enabling across the company.";
            }
        }

        public bool CopyLocalDataOnEnable
        {
            get => _config?.DataConnectivity?.CopyLocalDataOnEnable ?? true;
            set
            {
                if (_config?.DataConnectivity == null) return;
                if (_config.DataConnectivity.CopyLocalDataOnEnable == value) return;
                _config.DataConnectivity.CopyLocalDataOnEnable = value;
                OnPropertyChanged();
                ScheduleDebouncedSave();
            }
        }

        public string WorkstationName
        {
            get => string.IsNullOrWhiteSpace(_config?.DataConnectivity?.WorkstationName)
                ? Environment.MachineName
                : _config!.DataConnectivity.WorkstationName;
            set
            {
                if (_config?.DataConnectivity == null) return;
                _config.DataConnectivity.WorkstationName = value ?? "";
                OnPropertyChanged();
                ScheduleDebouncedSave();
            }
        }

        private string _networkDataRootStatus = "Local database mode. Use this only after creating a reliable shared Windows folder on the same LAN/Wi-Fi.";
        public string NetworkDataRootStatus
        {
            get => _networkDataRootStatus;
            private set { _networkDataRootStatus = value; OnPropertyChanged(); }
        }

        private bool SelectedDataRootDiffersFromLiveRoot
        {
            get
            {
                if (UseSharedDataRoot !=
                    Services.Database.AppDataRoot.SharedRootConfigured)
                    return true;
                if (!UseSharedDataRoot) return false;
                return !AppDataRoot.TryNormalizeSharedRoot(
                           SharedDataRootPath, out var selectedPath)
                       || !selectedPath.Equals(
                           Services.Database.AppDataRoot.ConfiguredSharedDataRoot,
                           StringComparison.OrdinalIgnoreCase);
            }
        }

        public string DataRootModeDisplay
        {
            get
            {
                if (Services.Database.AppDataRoot.SharedRootFallbackActive)
                    return Services.Database.AppDataRoot.SharedRootReconnectAvailable
                        ? "Local mode — shared folder found; review required"
                        : "Local mode — shared folder unavailable";

                if (SelectedDataRootDiffersFromLiveRoot)
                    return "A different root is selected; current root stays active until Save + Restart";

                return Services.Database.AppDataRoot.IsSharedDataRootEnabled
                    ? "Shared LAN/Wi-Fi database mode — active"
                    : "Local workstation database mode — active";
            }
        }

        public string CurrentDataRootDisplay
        {
            get
            {
                if (Services.Database.AppDataRoot.SharedRootFallbackActive)
                {
                    var configured = string.IsNullOrWhiteSpace(
                        Services.Database.AppDataRoot.ConfiguredSharedDataRoot)
                        ? "(empty or invalid saved path)"
                        : Services.Database.AppDataRoot.ConfiguredSharedDataRoot;
                    var status = Services.Database.AppDataRoot.SharedRootReconnectAvailable
                        ? "shared path is reachable; reconnect is pending review"
                        : "shared path is unavailable; changes stay on this workstation";
                    return Services.Database.AppDataRoot.ProductionFolder +
                        "   |   LOCAL DATA ACTIVE   |   saved shared path: " + configured +
                        "   |   " + status + PendingRootSelectionSuffix;
                }

                var root = Services.Database.AppDataRoot.CurrentFolder;
                string activeRoot = root + (Services.Database.AppDataRoot.IsSharedDataRootEnabled
                    ? "   |   shared root active"
                    : "   |   local root active");
                return activeRoot + PendingRootSelectionSuffix;
            }
        }

        private string PendingRootSelectionSuffix => SelectedDataRootDiffersFromLiveRoot
            ? "   |   selected root is not active; restart is required"
            : "";

        /// <summary>
        /// Warning banner for the Data card. Non-empty when a configured
        /// shared root is inactive and this workstation is using local data.
        /// The XAML collapses the tile via StringToVisibilityConverter.
        /// </summary>
        private string _sharedRootWarning = "";
        public string SharedRootWarning
        {
            get => _sharedRootWarning;
            private set { _sharedRootWarning = value ?? ""; OnPropertyChanged(); }
        }

        /// <summary>
        /// Re-derives <see cref="SharedRootWarning"/> from the live
        /// AppDataRoot state. Called at VM init and after every
        /// test/prepare/apply/revert step.
        /// </summary>
        public void RefreshSharedRootWarning()
        {
            if (Services.Database.AppDataRoot.SharedRootFallbackActive)
            {
                SharedRootWarning = string.IsNullOrEmpty(
                    Services.Database.AppDataRoot.SharedRootFallbackReason)
                    ? "A shared company database is configured but inactive. ProGlass is using this " +
                      "workstation's local data; changes will not be shared automatically. Review the " +
                      "configured folder and reconnect explicitly from this page."
                    : Services.Database.AppDataRoot.SharedRootFallbackReason;
            }
            else
            {
                SharedRootWarning = "";
            }
            OnPropertyChanged(nameof(DataRootModeDisplay));
            OnPropertyChanged(nameof(CurrentDataRootDisplay));
        }

        public ICommand TestSharedDataRootCommand { get; private set; }
        public ICommand CreateSharedFolderCommand { get; private set; }
        public ICommand PrepareSharedDataRootCommand { get; private set; }
        public ICommand ApplySharedDataRootCommand { get; private set; }
        public ICommand RevertToLocalDataRootCommand { get; private set; }

        // ══════════════════════════════════════════════════════════════
        //  v2 — BACKUP & RESTORE (full dataset sets)
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<
            Services.Database.DatabaseBackup.BackupSetInfo> BackupSets
        { get; } = new();

        private Services.Database.DatabaseBackup.BackupSetInfo? _selectedBackup;
        public Services.Database.DatabaseBackup.BackupSetInfo? SelectedBackup
        {
            get => _selectedBackup;
            set { _selectedBackup = value; OnPropertyChanged(); }
        }

        private string _backupStatus = "";
        public string BackupStatus
        {
            get => _backupStatus;
            private set { _backupStatus = value; OnPropertyChanged(); }
        }

        public ICommand CreateBackupNowCommand { get; private set; }
        public ICommand RestoreSelectedBackupCommand { get; private set; }
        public ICommand RefreshBackupsCommand { get; private set; }
        public ICommand OpenBackupsFolderCommand { get; private set; }

    }
}
