// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/Pages/SettingsUpdatesPageViewModel.Commands.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v3 — Updates page VM: OTA command wiring
//
// Moved verbatim from SettingsViewModel.InitializeUpdateCommands
// (SettingsViewModel.Helpers.Updates.cs). Split out so both files stay
// inside the 350-line Settings VM budget.
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

namespace ProGlassAutomation.ViewModels.Settings.Pages
{
    public sealed partial class SettingsUpdatesPageViewModel
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

                // Installed builds use Velopack for applying updates, but
                // release metadata still comes from GitHub so the user can
                // read the exact changelog for the latest published EXE.
                if (Services.Updates.VelopackUpdateService.IsInstalled)
                {
                    var releaseInfo = await Services.Updates.UpdateService
                        .CheckForUpdateAsync();
                    if (releaseInfo.Success)
                    {
                        _lastUpdateCheck = releaseInfo;
                        ApplyReleaseInfo(releaseInfo);
                    }
                    else
                    {
                        HasReleaseInfo = false;
                        ReleaseNotesPreview = "";
                    }

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
    }
}
