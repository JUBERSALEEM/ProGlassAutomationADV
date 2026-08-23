// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/About/Sections/AboutCrashReportsCard.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: About — recent crash reports card code-behind
//
// The card owns its own AboutCrashReportsViewModel.
// The Open button on each row reads the row's
// FilePath from its Tag and routes through
// ProcessStartGuard.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.ViewModels.About;

namespace ProGlassAutomation.Views.About.Sections
{
    public partial class AboutCrashReportsCard : UserControl
    {
        public AboutCrashReportsCard()
        {
            InitializeComponent();
            DataContext = new AboutCrashReportsViewModel();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AboutCrashReportsViewModel vm)
                vm.Refresh();
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AboutCrashReportsViewModel vm && vm.OpenFolderCommand.CanExecute(null))
                vm.OpenFolderCommand.Execute(null);
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AboutCrashReportsViewModel vm && vm.ClearAllCommand.CanExecute(null))
                vm.ClearAllCommand.Execute(null);
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string? path = btn.Tag as string;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            try
            {
                // ProcessStartGuard validation: only
                // open files inside the app's standard
                // data folders. A malicious crash
                // report whose ErrorId line points at
                // \\evil\share\evil.exe would be
                // rejected here.
                if (!ProcessStartGuard.IsPathInsideAllowedRoot(path))
                {
                    MessageBox.Show(
                        "This crash report is in a location that " +
                        "is not part of the ProGlass data folder. " +
                        "It will not be opened automatically. Please " +
                        "open it manually from the crash folder.",
                        "Crash report location",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    "[AboutCrashReports] Open file failed: " + ex.Message);
            }
        }
    }
}
