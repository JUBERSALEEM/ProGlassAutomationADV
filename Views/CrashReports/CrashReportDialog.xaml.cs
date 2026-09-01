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
//                         clipboard.
//   [OPEN CRASH FOLDER]— opens Explorer with the
//                         crash report selected.
//   [CLOSE]            — dismisses the dialog.
//
// 2026-09-02 (Phase 3.7): display data + the four actions moved to
// CrashReportViewModel (bound). This file only wires the two window-side
// callbacks (Close / Shutdown) and the VM. The dialog is read-only and
// never throws — showing it must not make a bad situation worse.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.CrashReports;
using System.Windows;

namespace ProGlassAutomation.Views.CrashReports
{
    public partial class CrashReportDialog : Window
    {
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
            DataContext = new CrashReportViewModel(
                errorId, reportPath, exceptionType, exceptionMessage);
        }
    }
}
