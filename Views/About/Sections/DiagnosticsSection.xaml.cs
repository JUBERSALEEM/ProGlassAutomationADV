// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/About/Sections/DiagnosticsSection.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: About → "Diagnostics" tab (2026-09-15 iOS-26 redesign)
//
// Owns the app-log open-in-Explorer click handler. The crash reports
// card is fully self-contained (AboutCrashReportsCard).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.About;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.About.Sections
{
    public partial class DiagnosticsSection : UserControl
    {
        public DiagnosticsSection()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Opens the folder containing today's app log in Explorer.
        /// Silently fails if the folder is missing or Explorer is
        /// unavailable (diagnostic convenience — never throws from a
        /// click, Rule E01).
        /// </summary>
        private void OpenLogFolder_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is AboutViewModel vm)
            {
                try
                {
                    var logPath = vm.AppLogPath;
                    if (string.IsNullOrEmpty(logPath))
                        return;

                    var dir = Path.GetDirectoryName(logPath);
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                        return;

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "\"" + dir + "\"",
                        UseShellExecute = true
                    });
                }
                catch (System.Exception ex)
                {
                    // Explorer launch failed — silent fail (Rule E01 with reason)
                    ProGlassAutomation.Services.Logging.AppLogService.Debug("[DiagnosticsSection] log folder launch failed: " + ExceptionMessages.Deep(ex));
                }
            }
        }
    }
}
