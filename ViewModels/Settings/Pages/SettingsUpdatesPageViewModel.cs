// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/Pages/SettingsUpdatesPageViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v3 — per-page ViewModel (phase 3b, promotion #3)
//
// Updates page (OTA check / download / install + history) promoted out of
// the SettingsViewModel god class after Sharing and Voice. Members moved
// verbatim from SettingsViewModel.Updates.cs; the command wiring moved from
// SettingsViewModel.Helpers.Updates.cs (InitializeUpdateCommands) into
// SettingsUpdatesPageViewModel.Commands.cs.
//
// Dependency scan: the page touches only statics (UpdateService,
// VelopackUpdateService, AppInfo, ConfigService, LocalizationService,
// MessageBox) — no shell state, so the VM is parameterless. The shell keeps
// a CurrentVersionDisplay pass-through because the About section binds it
// too; both read the same static AppInfo.Version.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.Fonts;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services;
using ProGlassAutomation.Utilities;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using System;

namespace ProGlassAutomation.ViewModels.Settings
{
    /// <summary>Updates page state: OTA state machine projection, release
    /// info, download progress and persisted update history.</summary>
    public sealed partial class SettingsUpdatesPageViewModel : ViewModelBase
    {
        public SettingsUpdatesPageViewModel()
        {
            InitializeUpdateCommands();
        }

        /// <summary>Unhooks the UpdateService event. Called by the shell's
        /// Dispose — mirrors the detach the god class used to do itself.</summary>
        public void Detach()
        {
            Services.Updates.UpdateService.StateChanged -= OnUpdateServiceStateChanged;
        }

        // ══════════════════════════════════════════════════════════════
        //  v2 — UPDATES (OTA check via GitHub releases)
        // ══════════════════════════════════════════════════════════════

        private string _updateStatus =
            $"Current version: v{Services.Updates.AppInfo.Version}";
        public string UpdateStatus
        {
            get => _updateStatus;
            private set { _updateStatus = value; OnPropertyChanged(); }
        }

        private bool _isUpdateAvailable;
        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            private set { _isUpdateAvailable = value; OnPropertyChanged(); }
        }

        private int _updateDownloadProgress;
        public int UpdateDownloadProgress
        {
            get => _updateDownloadProgress;
            private set
            {
                _updateDownloadProgress = value;
                OnPropertyChanged();
            }
        }

        private bool _isDownloadingUpdate;
        public bool IsDownloadingUpdate
        {
            get => _isDownloadingUpdate;
            private set
            {
                _isDownloadingUpdate = value;
                OnPropertyChanged();
            }
        }

        private Services.Updates.UpdateCheckResult? _lastUpdateCheck;

        // ── v2 §34: release info display ─────────────────────────────

        /// <summary>Version this build is running.</summary>
        public string CurrentVersionDisplay =>
            Services.Updates.AppInfo.Version;

        private string _latestVersionDisplay = "";
        public string LatestVersionDisplay
        {
            get => _latestVersionDisplay;
            private set { _latestVersionDisplay = value; OnPropertyChanged(); }
        }

        private string _releaseNameDisplay = "";
        public string ReleaseNameDisplay
        {
            get => _releaseNameDisplay;
            private set { _releaseNameDisplay = value; OnPropertyChanged(); }
        }

        private string _releasePublishedDisplay = "";
        public string ReleasePublishedDisplay
        {
            get => _releasePublishedDisplay;
            private set { _releasePublishedDisplay = value; OnPropertyChanged(); }
        }

        private string _releaseNotesPreview = "";
        public string ReleaseNotesPreview
        {
            get => _releaseNotesPreview;
            private set { _releaseNotesPreview = value; OnPropertyChanged(); }
        }

        private bool _hasReleaseInfo;
        public bool HasReleaseInfo
        {
            get => _hasReleaseInfo;
            private set { _hasReleaseInfo = value; OnPropertyChanged(); }
        }

        // ── v3 OTA §7: state-driven display ──────────────────────────

        /// <summary>True when a downloaded update can be installed.</summary>
        private bool _isUpdateReady;
        public bool IsUpdateReady
        {
            get => _isUpdateReady;
            private set { _isUpdateReady = value; OnPropertyChanged(); }
        }

        /// <summary>True while a check is in flight (disables buttons).</summary>
        private bool _isCheckingUpdate;
        public bool IsCheckingUpdate
        {
            get => _isCheckingUpdate;
            private set { _isCheckingUpdate = value; OnPropertyChanged(); }
        }

        /// <summary>"14 Aug 2026 • 10:42 AM" style last-check stamp.</summary>
        private string _lastCheckedDisplay = "";
        public string LastCheckedDisplay
        {
            get => _lastCheckedDisplay;
            private set { _lastCheckedDisplay = value; OnPropertyChanged(); }
        }

        // ── v2 §34: update history ledger ────────────────────────────

        public System.Collections.ObjectModel.ObservableCollection
            <Services.Updates.UpdateHistoryEntry> UpdateHistory { get; } =
            new();

        private bool _hasUpdateHistory;
        public bool HasUpdateHistory
        {
            get => _hasUpdateHistory;
            private set { _hasUpdateHistory = value; OnPropertyChanged(); }
        }

        /// <summary>Loads the persisted update-event ledger.</summary>
        /// <summary>Max history rows shown (keeps the card compact).</summary>
        private const int MaxDisplayedHistory = 6;

        public async Task LoadUpdateHistoryAsync()
        {
            var entries = await Services.Updates.UpdateHistoryService
                .ReadAllAsync();

            ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() =>
            {
                UpdateHistory.Clear();
                foreach (var e in entries.Take(MaxDisplayedHistory))
                    UpdateHistory.Add(e);
                HasUpdateHistory = UpdateHistory.Count > 0;
            });
        }

        /// <summary>
        /// v3 OTA: projects UpdateService state changes onto the UI.
        /// Raised from a background thread — marshals to the dispatcher.
        /// </summary>
        private void OnUpdateServiceStateChanged()
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var s = Services.Updates.UpdateService.State;
                var d = Services.Updates.UpdateService.StateDetail;

                IsCheckingUpdate =
                    s == Services.Updates.UpdateState.Checking;
                IsDownloadingUpdate =
                    s == Services.Updates.UpdateState.Downloading;
                IsUpdateReady =
                    s == Services.Updates.UpdateState.DownloadCompleted;
                UpdateDownloadProgress =
                    Services.Updates.UpdateService.DownloadPercent;

                switch (s)
                {
                    case Services.Updates.UpdateState.Checking:
                        UpdateStatus = "Checking for updates…";
                        break;
                    case Services.Updates.UpdateState.UpToDate:
                        UpdateStatus =
                            $"✓ You're up to date (v{AppVersionSafe()}).";
                        break;
                    case Services.Updates.UpdateState.UpdateAvailable:
                        UpdateStatus = $"🚀 Update available: v{d}";
                        break;
                    case Services.Updates.UpdateState.Downloading:
                        UpdateStatus =
                            $"Downloading… {Services.Updates.UpdateService.DownloadPercent}%";
                        break;
                    case Services.Updates.UpdateState.DownloadCompleted:
                        UpdateStatus =
                            "Update ready. Click Install & restart.";
                        break;
                    case Services.Updates.UpdateState.Installing:
                        UpdateStatus = "Installing — restarting soon…";
                        break;
                    case Services.Updates.UpdateState.Failed:
                        UpdateStatus = $"❌ Update failed: {d}";
                        break;
                    case Services.Updates.UpdateState.Offline:
                        UpdateStatus =
                            "No internet connection. Update check unavailable.";
                        break;
                    case Services.Updates.UpdateState.Cancelled:
                        UpdateStatus = "Update cancelled.";
                        break;
                }
            });
        }

        private static string AppVersionSafe()
        {
            try { return Services.Updates.AppInfo.Version; }
            catch { return ""; }
        }

        private void ApplyReleaseInfo(
            Services.Updates.UpdateCheckResult result)
        {
            LatestVersionDisplay = result.LatestVersion;
            ReleaseNameDisplay = string.IsNullOrEmpty(result.ReleaseName)
                ? $"Release v{result.LatestVersion}"
                : result.ReleaseName;
            ReleasePublishedDisplay = result.PublishedAtUtc.HasValue
                ? result.PublishedAtUtc.Value.ToLocalTime()
                    .ToString("dd MMM yyyy")
                : "";
            ReleaseNotesPreview = (result.ReleaseNotes ?? "").Length > 400
                ? result.ReleaseNotes[..400] + "…"
                : (result.ReleaseNotes ?? "");
            HasReleaseInfo = !string.IsNullOrEmpty(result.LatestVersion);
        }

        public ICommand CheckForUpdatesCommand { get; private set; }
        public ICommand DownloadUpdateCommand { get; private set; }

        /// <summary>v3 OTA: launches the downloaded installer & exits.</summary>
        public ICommand InstallUpdateCommand { get; private set; }

    }
}
