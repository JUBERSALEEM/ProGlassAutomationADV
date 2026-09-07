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



        // ══════════════════════════════════════════════════════════════
        //  LAN / Wi-Fi SHARED DATABASE CONNECTIVITY
        // ══════════════════════════════════════════════════════════════

        public bool UseSharedDataRoot
        {
            get => _config?.DataConnectivity?.UseSharedDataRoot ?? false;
            set
            {
                if (_config?.DataConnectivity == null) return;
                if (_config.DataConnectivity.UseSharedDataRoot == value) return;
                _config.DataConnectivity.UseSharedDataRoot = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DataRootModeDisplay));
                NetworkDataRootStatus = value
                    ? "Shared database mode selected. Test/prepare the folder, save, then restart all workstations."
                    : "Local database mode selected. Save and restart to return this workstation to local AppData.";
                ScheduleDebouncedSave();
            }
        }

        public string SharedDataRootPath
        {
            get => _config?.DataConnectivity?.SharedDataRootPath ?? "";
            set
            {
                if (_config?.DataConnectivity == null) return;
                var v = value ?? "";
                if (_config.DataConnectivity.SharedDataRootPath == v) return;
                _config.DataConnectivity.SharedDataRootPath = v;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentDataRootDisplay));
                NetworkDataRootStatus = "Shared folder changed. Run Test Connection before enabling across the company.";
                ScheduleDebouncedSave();
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

        public string DataRootModeDisplay => UseSharedDataRoot
            ? "Shared LAN/Wi-Fi database mode"
            : "Local workstation database mode";

        public string CurrentDataRootDisplay => Services.Database.AppDataRoot.CurrentFolder;

        public ICommand TestSharedDataRootCommand { get; private set; }
        public ICommand PrepareSharedDataRootCommand { get; private set; }
        public ICommand ApplySharedDataRootCommand { get; private set; }

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
