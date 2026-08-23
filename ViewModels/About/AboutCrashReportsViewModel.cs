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
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;

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

        public AboutCrashReportsViewModel()
        {
            RefreshCommand = new RelayCommand(_ => Refresh());
            ClearAllCommand = new RelayCommand(_ => ClearAll(),
                _ => !IsEmpty);
            OpenFolderCommand = new RelayCommand(_ => OpenFolder());
            Refresh();
        }

        public void Refresh()
        {
            Reports.Clear();
            foreach (var raw in CrashReportService.GetRecentReports(10))
            {
                Reports.Add(new CrashReportEntry(raw));
            }
            OnPropertyChanged(nameof(IsEmpty));
            (ClearAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void ClearAll()
        {
            var answer = MessageBox.Show(
                "Delete every crash report on disk?\n\n" +
                "This action cannot be undone. The next crash " +
                "will start with a fresh report file.",
                "Clear crash reports",
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

