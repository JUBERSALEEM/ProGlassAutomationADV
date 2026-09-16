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
