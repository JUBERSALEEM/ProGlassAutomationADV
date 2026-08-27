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

using System;
using System.Windows;
using System.Windows.Threading;

namespace ProGlassAutomation.Tools.LicenseGenerator
{
    /// <summary>
    /// WPF application entry point.
    /// The tool now loads Theme.xaml explicitly before creating MainWindow so
    /// the standalone EXE does not depend on StartupUri/resource-resolution
    /// reflection magic to find its theme on clean machines.
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var themeUri = new Uri(
                    "/ProGlassAutomation.LicenseGenerator;component/Theme.xaml",
                    UriKind.Relative);
                var theme = (ResourceDictionary)LoadComponent(themeUri);
                Resources.MergedDictionaries.Add(theme);

                MainWindow = new MainWindow();
                MainWindow.Show();
            }
            catch (Exception ex)
            {
                var deep = Unwrap(ex);
                MessageBox.Show(
                    "Startup failed:\n\n" + deep.Message +
                    "\n\nDetails: " + deep.GetType().FullName,
                    "License Generator — startup error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var deep = Unwrap(e.Exception);
            MessageBox.Show(
                "Unexpected error:\n\n" + deep.Message +
                "\n\nDetails: " + deep.GetType().FullName,
                "License Generator — unhandled exception",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private static Exception Unwrap(Exception ex)
        {
            while (ex.InnerException != null)
                ex = ex.InnerException;
            return ex;
        }
    }
}
