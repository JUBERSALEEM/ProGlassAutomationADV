// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/CrashReports/CrashReportDialog.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Crash report — user-facing dialog
//
// A small modal that appears after an unhandled crash.
// Shows the ErrorId, exception type and message, and
// the path to the saved report. Four buttons:
//
//   [RESTART APP]      — closes the app cleanly and
//                         re-launches via Process.Start
//                         (ProcessStartGuard-validated).
//   [COPY ERROR ID]    — copies the ErrorId to the
//                         clipboard. The user pastes it
//                         into an email / WhatsApp to
//                         support.
//   [OPEN CRASH FOLDER]— opens Explorer with the
//                         crash report selected, so the
//                         user can drag it into an
//                         email.
//   [CLOSE]            — dismisses the dialog. The
//                         user can keep working; the
//                         report is already on disk.
//
// The dialog itself is read-only and never throws —
// showing it must not make a bad situation worse.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;   // ProcessStartGuard

namespace ProGlassAutomation.Views.CrashReports
{
    public partial class CrashReportDialog : Window
    {
        private readonly string _errorId;
        private readonly string _reportPath;
        private readonly string _exceptionType;
        private readonly string _exceptionMessage;

        /// <summary>
        /// Show the dialog modally. Returns when the user
        /// closes it (any button). The caller does not
        /// need to do anything — the report is already
        /// on disk; the dialog is purely informational +
        /// action-button.
        /// </summary>
        public CrashReportDialog(
            string errorId,
            string reportPath,
            string exceptionType,
            string exceptionMessage)
        {
            InitializeComponent();

            _errorId = string.IsNullOrEmpty(errorId) ? "ERR-UNKNOWN" : errorId;
            _reportPath = string.IsNullOrEmpty(reportPath) ? "" : reportPath;
            _exceptionType = string.IsNullOrEmpty(exceptionType) ? "—" : exceptionType;
            _exceptionMessage = string.IsNullOrEmpty(exceptionMessage)
                ? "—" : exceptionMessage;

            ErrorIdText.Text = _errorId;
            ExceptionTypeText.Text = _exceptionType;
            ExceptionMessageText.Text = _exceptionMessage;
            ReportPathText.Text = string.IsNullOrEmpty(_reportPath)
                ? "(report could not be written — see CrashReports folder)"
                : _reportPath;
        }

        // ── Button handlers ─────────────────────────────────────

        private void RestartButton_Click(object sender, RoutedEventArgs e)
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
                    Safety.ProcessStartGuard.IsPathInsideAllowedRoot(exe))
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
                    "[CrashDialog] Restart failed: " + ex.Message);
            }
            finally
            {
                Close();
                // Shutdown so the new instance is the
                // only one. 0 = clean exit code.
                Application.Current.Shutdown(0);
            }
        }

        private void CopyErrorIdButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_errorId);
            }
            catch (Exception ex)
            {
                // Clipboard.SetText can throw on transient
                // busy conditions. Never let the dialog
                // crash because of a clipboard problem.
                AppLogService.Debug(
                    "[CrashDialog] Clipboard copy failed: " + ex.Message);
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_reportPath) ||
                    !File.Exists(_reportPath))
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
                    Arguments = "/select,\"" + _reportPath + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    "[CrashDialog] Open folder failed: " + ex.Message);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
