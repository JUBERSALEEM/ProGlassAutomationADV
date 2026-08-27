using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class LicenseActivationAboutSourceTests
    {
        [Fact]
        public void Paste_activation_shows_a_post_activation_success_dialog_before_closing_the_overlay()
        {
            string root = LocateProjectRoot();
            string code = File.ReadAllText(Path.Combine(root,
                "Views", "Licensing", "LicenseActivationView.xaml.cs"));
            string popup = File.ReadAllText(Path.Combine(root,
                "Views", "Licensing", "LicenseActivatedDialog.cs"));

            Assert.Contains("LicenseActivatedDialog.ShowFor", code);
            Assert.Contains("result.Status is LicenseStatus.Valid or LicenseStatus.ExpiringSoon or LicenseStatus.Trial", code);
            Assert.Contains("License activated successfully", popup);
            Assert.Contains("STARTING FROM", popup);
            Assert.Contains("VALID UNTIL", popup);
            Assert.Contains("LICENSE WINDOW", popup);
            Assert.Contains("ACTIVATION MODE", popup);
        }

        [Fact]
        public void About_license_card_surfaces_window_mode_issuer_ring_and_copy_actions()
        {
            string root = LocateProjectRoot();
            string xaml = File.ReadAllText(Path.Combine(root,
                "Views", "About", "Sections", "AboutLicenseCard.xaml"));
            string code = File.ReadAllText(Path.Combine(root,
                "Views", "About", "Sections", "AboutLicenseCard.xaml.cs"));
            string vm = File.ReadAllText(Path.Combine(root,
                "ViewModels", "Licensing", "LicenseStatusViewModel.cs"));
            string hero = File.ReadAllText(Path.Combine(root,
                "Views", "About", "AboutView.xaml"));

            Assert.Contains("LicenseWindowDisplay", xaml);
            Assert.Contains("ActivationModeDisplay", xaml);
            Assert.Contains("LicenseSourceDisplay", xaml);
            Assert.Contains("TrustedIssuerDisplay", xaml);
            Assert.Contains("COPY LICENSE ID", xaml);
            Assert.Contains("CopyLicenseIdButton_Click", code);
            Assert.Contains("HasPayloadDetails", vm);
            Assert.Contains("FeatureSummaryDisplay", vm);
            Assert.Contains("BuildActivationMode", vm);
            Assert.Contains("SafeLocalString(p.EffectiveExpiresAt)", vm);
            Assert.Contains("LicenseStatus.StatusBadge", hero);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Views", "Licensing", "LicenseActivationView.xaml.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
