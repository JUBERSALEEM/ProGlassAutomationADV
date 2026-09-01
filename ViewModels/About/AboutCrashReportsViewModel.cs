// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/About/AboutCrashReportsViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: About — recent crash reports panel
//
// A small VM that backs the "Recent crash reports" card
// in the About tab. Reads the disk via
// CrashReportService.GetRecentReports and exposes an
// ObservableCollection<CrashReportEntry> the
// UserControl binds to. Commands: Refresh, OpenFile,
// ClearAll.
//
// Display formatting is done in the VM (formatted
// strings) so the XAML stays converter-free. This is
// intentionally simple — a per-row view-model would
// add files without value for ten items.
//
// THREADING
//   Construction runs on the UI thread. Refresh is
//   synchronous and fast (reads the first 32 KB of each
//   file at most), so no async machinery is needed.
//   ClearAll deletes files on the UI thread because the
//   count is small.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Localization;

namespace ProGlassAutomation.ViewModels.About
{
    public sealed class AboutCrashReportsViewModel : ViewModelBase
    {
        /// <summary>
        /// Per-row view of a crash report. Wraps
        /// <see cref="CrashReportService.ReportListing"/>
        /// with formatted display strings so the XAML
        /// can bind to plain text.
        /// </summary>
        public sealed class CrashReportEntry
        {
            public CrashReportService.ReportListing Raw { get; }
            public CrashReportEntry(CrashReportService.ReportListing raw)
            {
                Raw = raw;
            }

            public string FilePath => Raw.FilePath;
            public string ErrorId =>
                string.IsNullOrEmpty(Raw.ErrorId) ? "(no id)" : Raw.ErrorId;
            public string TopExceptionType => Raw.TopExceptionType;
            public string TopExceptionMessage => Raw.TopExceptionMessage;

            public string TimestampDisplay =>
                Raw.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

            public string SizeDisplay => FormatBytes(Raw.FileSize);

            public bool HasErrorId => !string.IsNullOrEmpty(Raw.ErrorId);
            public bool HasMessage => !string.IsNullOrEmpty(Raw.TopExceptionMessage);

            private static string FormatBytes(long bytes)
            {
                if (bytes < 1024) return bytes + " B";
                if (bytes < 1024 * 1024)
                    return (bytes / 1024.0).ToString("0.0") + " KB";
                return (bytes / (1024.0 * 1024)).ToString("0.00") + " MB";
            }
        }

        public ObservableCollection<CrashReportEntry> Reports { get; } = new();

        /// <summary>True when the listing is empty.</summary>
        public bool IsEmpty => Reports.Count == 0;

        public ICommand RefreshCommand { get; }
        public ICommand ClearAllCommand { get; }
        public ICommand OpenFolderCommand { get; }

        /// <summary>
        /// 2026-08-25 "not updating dynamically" fix: tracks the scan
        /// currently in flight. A new Refresh() supersedes the old one,
        /// and the superseded scan's continuation is discarded below
        /// instead of running Reports.Clear() over the newer results.
        /// </summary>
        private int _scanGeneration;

        public AboutCrashReportsViewModel()
        {
            RefreshCommand = new RelayCommand(_ => Refresh());
            ClearAllCommand = new RelayCommand(_ => ClearAll(),
                _ => !IsEmpty);
            OpenFolderCommand = new RelayCommand(_ => OpenFolder());

            // 2026-08-25 senior fix: the crash-folder scan used to run
            // synchronously in this constructor — on the UI thread, the
            // moment the About page opened. Reading and parsing the report
            // files stalled the page. The scan now runs in the background.
            Refresh();
        }

        /// <summary>
        /// Fire-and-forget wrapper around the async scan. Safe from the
        /// constructor and from the Refresh button alike. A new call
        /// supersedes any scan still in flight (see _scanGeneration).
        /// </summary>
        public void Refresh()
        {
            // Bump the generation: the previous scan (if still running)
            // will see a stale generation when it returns and drop its
            // results instead of clearing the list the new scan filled.
            int generation = Interlocked.Increment(ref _scanGeneration);
            _ = RefreshAsync(generation);
        }

        private async Task RefreshAsync(int generation)
        {
            try
            {
                var recent = await Task.Run(() =>
                    CrashReportService.GetRecentReports(10).ToList())
                    .ConfigureAwait(true);

                // A newer Refresh() started while this scan was running
                // (open page + REFRESH click, or a double REFRESH). The
                // newer scan owns the list now — this one must NOT
                // Clear() over its results. Without this guard two
                // overlapping scans interleaved Clear()/Add() and the
                // list ended up empty or stale — the "the card is not
                // updating dynamically" symptom.
                if (generation != Volatile.Read(ref _scanGeneration))
                    return;

                Reports.Clear();
                foreach (var raw in recent)
                {
                    Reports.Add(new CrashReportEntry(raw));
                }
                OnPropertyChanged(nameof(IsEmpty));
                (ClearAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                // A scan failure must never take the About page down.
                AppLogService.Debug(
                    "[AboutCrashReports] Report scan failed: " + ex.Message);
            }
        }


        private void ClearAll()
        {
            var answer = MessageBox.Show(
                LocalizationService.Instance.T("Msg.DeleteEveryCrashReportOn"),
                LocalizationService.Instance.T("Msg.CapClearCrashReports"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;

            // Deleting files is fast; do it on the UI
            // thread because the count is small.
            CrashReportService.DeleteAllReports();
            Refresh();
        }

        private void OpenFolder()
        {
            try
            {
                string folder = CrashReportService.CrashReportFolder;
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    "[AboutCrashReports] Open folder failed: " + ex.Message);
            }
        }
    }
}

