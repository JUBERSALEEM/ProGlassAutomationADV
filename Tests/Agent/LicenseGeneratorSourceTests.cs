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
        public void Main_window_uses_safe_brush_resolution_and_default_output_folder()
        {
            string root = LocateProjectRoot();
            string code = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "MainWindow.xaml.cs"));
            string theme = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "Theme.xaml"));

            Assert.Contains("ResolveBrush(", code);
            Assert.Contains("LicenseGeneratorCore.DefaultOutputFolder", code);
            Assert.Contains("SaveLicenseKeyButton_Click", code);
            Assert.Contains("<FontFamily x:Key=\"AppFont\">Segoe UI</FontFamily>", theme);
            Assert.DoesNotContain("DropShadowEffect", theme);
        }

        [Fact]
        public void Generator_window_keeps_output_blank_when_signing_is_disabled()
        {
            string root = LocateProjectRoot();
            string xaml = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "MainWindow.xaml"));
            string code = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "MainWindow.xaml.cs"));

            // The five sellable plans are the whole choice surface.
            Assert.Contains("Plan7DaysRadio", xaml);
            Assert.Contains("Plan30DaysRadio", xaml);
            Assert.Contains("Plan6MonthsRadio", xaml);
            Assert.Contains("Plan1YearRadio", xaml);
            Assert.Contains("Plan2YearsRadio", xaml);

            // The UI must route through the fail-closed issuance gate and
            // clear any previously generated output on failure.
            Assert.Contains("LicenseGeneratorCore.GenerateLicenseKey", code);
            Assert.Contains("if (!result.Success)", code);
            Assert.Contains("LicenseKeyOutputBox.Text = string.Empty;", code);
            Assert.Contains("SetStatus(result.Message, StatusKind.Error)", code);
            Assert.Contains("result.ArmoredKey", code);

            // No key-file UI and no request/response machinery remain.
            Assert.DoesNotContain("LOAD .PGKEY", xaml);
            Assert.DoesNotContain("CREATE NEW .PGKEY", xaml);
            Assert.DoesNotContain("GenerateActivationResponse", code);
            Assert.DoesNotContain("ActivationRequestBox", code);
            Assert.DoesNotContain("private bool _uiReady;", code);
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
