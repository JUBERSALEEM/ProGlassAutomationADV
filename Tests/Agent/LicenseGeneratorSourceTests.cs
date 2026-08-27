using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class LicenseGeneratorSourceTests
    {
        [Fact]
        public void App_loads_theme_explicitly_before_creating_the_window()
        {
            string root = LocateProjectRoot();
            string appXaml = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "App.xaml"));
            string appCode = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "App.xaml.cs"));

            Assert.DoesNotContain("StartupUri=", appXaml);
            Assert.Contains("Theme.xaml", appCode);
            Assert.Contains("Resources.MergedDictionaries.Add", appCode);
            Assert.Contains("new MainWindow()", appCode);
            Assert.Contains("Unwrap(", appCode);
        }

        [Fact]
        public void Main_window_uses_safe_brush_resolution_and_written_output_path()
        {
            string root = LocateProjectRoot();
            string code = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "MainWindow.xaml.cs"));
            string theme = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "Theme.xaml"));

            Assert.Contains("ResolveBrush(", code);
            Assert.Contains("result.OutputPath", code);
            Assert.Contains("<FontFamily x:Key=\"AppFont\">Segoe UI</FontFamily>", theme);
            Assert.DoesNotContain("DropShadowEffect", theme);
        }

        [Fact]
        public void Startup_guards_radio_checked_events_until_the_window_is_fully_built()
        {
            string root = LocateProjectRoot();
            string code = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "MainWindow.xaml.cs"));

            Assert.Contains("private bool _uiReady;", code);
            Assert.Contains("if (!_uiReady || FingerprintRow == null || SingleRadio == null || MultiRadio == null)", code);
            Assert.Contains("_uiReady = true;", code);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Tools", "LicenseGenerator", "MainWindow.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
