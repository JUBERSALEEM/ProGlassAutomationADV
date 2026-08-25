// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/About/Sections/AboutLicenseCard.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: About — License section code-behind
//
// Minimal code-behind. All real data binding happens
// through LicenseStatusViewModel.Instance. The single
// interactive element is the COPY button for the
// current machine fingerprint.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using ProGlassAutomation.ViewModels.Licensing;

namespace ProGlassAutomation.Views.About.Sections
{
    /// <summary>
    /// Self-contained license card for the About page
    /// and the Settings → Data → Database Health card.
    /// Binds to <see cref="LicenseStatusViewModel.Instance"/>.
    /// </summary>
    public partial class AboutLicenseCard : UserControl
    {
        public AboutLicenseCard()
        {
            InitializeComponent();
            // The VM is a singleton; the binding targets
            // the static instance, not a per-window VM.
            // That way the card refreshes automatically
            // when LicenseService.Reload() runs.
            //
            // 2026-08-25 senior hardening: if the singleton ever fails
            // to construct (licensing data in an unexpected shape), the
            // card renders unbound instead of taking the whole About
            // page down with a XamlParseException cascade.
            try
            {
                DataContext = LicenseStatusViewModel.Instance;
            }
            catch (Exception ex)
            {
                ProGlassAutomation.Services.Logging.AppLogService.Warning(
                    "[AboutLicenseCard] License VM unavailable: " + ex.Message);
            }
        }

        private void CopyFingerprintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string fp = LicenseStatusViewModel.Instance.CurrentFingerprint;
                if (string.IsNullOrEmpty(fp) || fp == "—" || fp == "Computing…")
                {
                    MessageBox.Show(
                        "The current machine fingerprint is unavailable.",
                        "Copy fingerprint",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                Clipboard.SetText(fp);
            }
            catch (Exception ex)
            {
                // Clipboard.SetText can throw
                // ExternalException on transient clipboard
                // busy conditions. Surface, do not crash.
                MessageBox.Show(
                    "Could not copy to clipboard: " + ex.Message,
                    "Copy fingerprint",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
