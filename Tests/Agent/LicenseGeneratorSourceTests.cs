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
        public void Generator_window_is_key_only_and_uses_external_key_loading()
        {
            string root = LocateProjectRoot();
            string code = File.ReadAllText(Path.Combine(root,
                "Tools", "LicenseGenerator", "MainWindow.xaml.cs"));

            Assert.Contains("IssuerKeyMaterial.LoadFromFile", code);
            Assert.Contains("LicenseGeneratorCore.GenerateLicenseKey", code);
            Assert.Contains("result.ArmoredKey", code);
            Assert.Contains("LicenseGeneratorCore.RunSelfTest", code);

            // The 2026 simple-flow rewrite removed the activation
            // request/response UI from the generator entirely.
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
