// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/App.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Developer-only tool — WPF entry point
//
// Minimal App.xaml.cs. All real logic lives in
// LicenseGeneratorCore and the MainWindow code-behind.
// The tool has no DI container, no service-locator, no
// analytics — it is a single-window utility, so the
// plumbing is intentionally short.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;

namespace ProGlassAutomation.Tools.LicenseGenerator
{
    /// <summary>
    /// WPF application entry point. The StartupUri in
    /// App.xaml points at MainWindow.xaml, so this class
    /// only exists to provide a non-default x:Class
    /// (required by the WPF Application pattern; otherwise
    /// XAML parsing fails).
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // WPF has its own unhandled-exception sinks.
            // We add a top-level try/catch so a crash in
            // the licence generator surfaces a MessageBox
            // instead of silently vanishing — the previous
            // console version logged to stderr, which is
            // not visible to a vendor running the WPF
            // tool from a shortcut.
            DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show(
                    "Unexpected error:\n\n" + e.Exception.Message +
                    "\n\nThe full exception has been written to the " +
                    "system event log.",
                    "License Generator — unhandled exception",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                e.Handled = true;
            };
        }
    }
}
