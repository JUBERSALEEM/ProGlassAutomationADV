// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/CrashReports/CrashReportViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Crash report — user-facing dialog.
//
// 2026-09-02 (Phase 3.7): the crash dialog's state + actions moved out of
// Views/CrashReports/CrashReportDialog.xaml.cs into this VM. The window
// keeps the read-only display wiring (ctor contract unchanged:
// App.StartupCore passes errorId/reportPath/exceptionType/exceptionMessage)
// and wires the two window-side callbacks (Close / Shutdown).
//
// The dialog itself is read-only and never throws — showing it must not
// make a bad situation worse.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;   // ProcessStartGuard
using System;
using ProGlassAutomation.Utilities;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.CrashReports
{
    /// <summary>Crash-report dialog: display-only data + four actions
    /// (restart / copy error id / open folder / close).</summary>
    public sealed class CrashReportViewModel : ViewModelBase
    {
        public string ErrorId { get; }
        public string ExceptionType { get; }
        public string ExceptionMessage { get; }
        public string ReportPath { get; }

        /// <summary>View wiring: closes the dialog (Close button).</summary>
        public Action? RequestClose { get; set; }

        /// <summary>View wiring: closes the dialog AND shuts the app down
        /// (restart path — the new instance is already launched).</summary>
        public Action? RequestShutdown { get; set; }

        public ICommand RestartCommand { get; }
        public ICommand CopyErrorIdCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand CloseCommand { get; }

        public CrashReportViewModel(
            string errorId,
            string reportPath,
            string exceptionType,
            string exceptionMessage)
        {
            ErrorId = string.IsNullOrEmpty(errorId) ? "ERR-UNKNOWN" : errorId;
            ReportPath = string.IsNullOrEmpty(reportPath) ? "" : reportPath;
            ExceptionType = string.IsNullOrEmpty(exceptionType) ? "—" : exceptionType;
            ExceptionMessage = string.IsNullOrEmpty(exceptionMessage)
                ? "—" : exceptionMessage;

            RestartCommand = new RelayCommand(_ => Restart());
            CopyErrorIdCommand = new RelayCommand(_ => CopyErrorId());
            OpenFolderCommand = new RelayCommand(_ => OpenFolder());
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
        }

        private void Restart()
        {
            try
            {
                // Re-launch this executable. Use the same
                // ProcessStartGuard the rest of the app
                // uses so a hostile EXE in the app folder
                // cannot be substituted in the restart
                // process.
                string? exe = Process.GetCurrentProcess()
                    .MainModule?.FileName;
                if (!string.IsNullOrEmpty(exe) &&
                    ProcessStartGuard.IsPathInsideAllowedRoot(exe))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exe,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                // Restart is a convenience. If it fails,
                // the user can close and re-open manually.
                AppLogService.Debug(
                    "[CrashDialog] Restart failed: " + ExceptionMessages.Deep(ex));
            }
            finally
            {
                // Close + shutdown so the new instance is the
                // only one. 0 = clean exit code.
                RequestShutdown?.Invoke();
            }
        }

        private void CopyErrorId()
        {
            try
            {
                Clipboard.SetText(ErrorId);
            }
            catch (Exception ex)
            {
                // Clipboard.SetText can throw on transient
                // busy conditions. Never let the dialog
                // crash because of a clipboard problem.
                AppLogService.Debug(
                    "[CrashDialog] Clipboard copy failed: " + ExceptionMessages.Deep(ex));
            }
        }

        private void OpenFolder()
        {
            try
            {
                if (string.IsNullOrEmpty(ReportPath) ||
                    !File.Exists(ReportPath))
                {
                    // The report was not written, or the
                    // user moved / deleted it. Fall back
                    // to opening the crash folder.
                    string folder = CrashReportService.CrashReportFolder;
                    Directory.CreateDirectory(folder);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
                    return;
                }
                // explorer /select,"path" highlights the
                // file. UseShellExecute is required on
                // modern .NET for verbs.
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "/select,\"" + ReportPath + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    "[CrashDialog] Open folder failed: " + ExceptionMessages.Deep(ex));
            }
        }
    }
}
