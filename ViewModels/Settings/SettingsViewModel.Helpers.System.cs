// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Helpers.System.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Settings v3 redesign (2026-09-17): the 580-line InitializeV2Sections
// command-wiring method became one dispatcher + per-domain init methods.
// Same partial class, members moved verbatim — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════
// NOTE: owns Dispose + application restart
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
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { ThemeManager.ThemeChanged -= _themeChangedHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SettingsViewModel] Dispose: ThemeChanged detach failed: " + ExceptionMessages.Deep(ex)); }
            try { ThemeManager.UiScaleChanged -= _uiScaleChangedHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SettingsViewModel] Dispose: UiScaleChanged detach failed: " + ExceptionMessages.Deep(ex)); }
            try { UpdatesPage?.Detach(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SettingsViewModel] Dispose: UpdateService detach failed: " + ExceptionMessages.Deep(ex)); }
            try { RecordsSyncPage?.Detach(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SettingsViewModel] Dispose: Records Sync status detach failed: " + ExceptionMessages.Deep(ex)); }
            try { TeardownAppearanceSection(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SettingsViewModel] Dispose: appearance-section detach failed: " + ExceptionMessages.Deep(ex)); }
            try { FlushDebouncedSave(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SettingsViewModel] Dispose: debounced save flush failed: " + ExceptionMessages.Deep(ex)); }
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
        private void RestartApplication()
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
                    "[SettingsViewModel] Restart failed: " + ExceptionMessages.Deep(ex));
                try
                {
                    _dialogs.Show(
                        "Restart failed (" + ExceptionMessages.Deep(ex) + ").\n" +
                        "Save your work and start the app manually.",
                        LocalizationService.Instance.T("Msg.CapRestartFailed"),
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
                catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("catch suppressed");
            }
            }
        }
    }
}
