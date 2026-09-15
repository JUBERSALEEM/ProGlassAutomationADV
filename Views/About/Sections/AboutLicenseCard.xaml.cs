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
using ProGlassAutomation.Utilities;
using System.Windows;
using System.Windows.Controls;
using ProGlassAutomation.ViewModels.Licensing;
using ProGlassAutomation.Services.Dialogs;

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
                    "[AboutLicenseCard] License VM unavailable: " + ExceptionMessages.Deep(ex));

                // 2026-09-15: never let the License screen look silently
                // empty — surface the exact state on the card itself.
                try
                {
                    VmUnavailableNote.Visibility = Visibility.Visible;
                }
                catch { /* the note is cosmetic; never throw again here */ }
            }
        }

        private void CopyFingerprintButton_Click(object sender, RoutedEventArgs e)
        {
            CopyValue(
                LicenseStatusViewModel.Instance.CurrentFingerprint,
                "The current machine fingerprint is unavailable.",
                "Copy fingerprint");
        }

        private void CopyLicenseIdButton_Click(object sender, RoutedEventArgs e)
        {
            CopyValue(
                LicenseStatusViewModel.Instance.LicenseIdDisplay,
                "No active license ID is available yet.",
                "Copy license ID");
        }

        private static void CopyValue(string? value, string emptyMessage, string title)
        {
            try
            {
                string text = (value ?? string.Empty).Trim();
                if (text.Length == 0 || text == "—" || text == "Computing…")
                {
                    Prompts.Show(
                        emptyMessage,
                        title,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                Prompts.Show(
                    "Could not copy to clipboard: " + ExceptionMessages.Deep(ex),
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
