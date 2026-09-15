// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/About/Sections/AboutLicenseCard.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: About — License section code-behind
//
// Minimal code-behind. All real data binding happens
// through LicenseStatusViewModel.Instance, which is attached to the
// card's INNER container (x:Name="CardRoot") — never to the root
// element of this UserControl.
//
// ─────────────────────────────────────────────────────────────────────
// 2026-09-15 ROOT-CAUSE FIX — "the License tab is still empty"
// ─────────────────────────────────────────────────────────────────────
// This constructor used to run `DataContext = LicenseStatusViewModel
// .Instance;` on the UserControl ROOT. A binding declared on an element
// resolves against that element's own DataContext, and the About shell
// switches sections with
//     Visibility="{Binding SelectedTabIndex, ...}"
// on the section host. Re-pointing the root DataContext therefore
// re-targeted the SHELL's tab binding at a license VM that has no
// SelectedTabIndex property:
//     • the DataTrigger never fired,
//     • the section stayed at its Collapsed default,
//     • the tab strip still highlighted "License" (it lives in the
//       shell), so the user saw a working tab over a blank page.
// The card now owns its VM on the inner container, which keeps the card
// self-contained WITHOUT swallowing the shell's DataContext. The
// invariant is locked by
// AboutModuleAuditTests.About_views_do_not_hijack_a_hosted_section_datacontext
// and by scripts/layout-verify/datacontext_scan.py.
//
// The single interactive element is the COPY button for the
// current machine fingerprint and the license id.
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
    /// Binds to <see cref="LicenseStatusViewModel.Instance"/>, attached to
    /// the inner <c>CardRoot</c> container so this control can never
    /// hijack a host's DataContext.
    /// </summary>
    public partial class AboutLicenseCard : UserControl
    {
        public AboutLicenseCard()
        {
            InitializeComponent();

            // Late host assignments (a parent that binds its own VM to
            // DataContext="…") land after this constructor, so re-resolve
            // once the element is loaded.
            Loaded += (_, _) => AttachViewModel();

            AttachViewModel();
        }

        /// <summary>
        /// Points the card's INNER container at the license VM.
        ///
        /// Resolution order:
        ///   1. a host that deliberately supplies a
        ///      <see cref="LicenseStatusViewModel"/> through DataContext —
        ///      that VM wins;
        ///   2. otherwise the singleton.
        ///
        /// The root element's DataContext is deliberately left alone: it
        /// belongs to the host (the About shell binds its tab switch to it).
        /// Never throws — if the singleton cannot be constructed the card
        /// shows <c>VmUnavailableNote</c> instead of rendering silently
        /// empty.
        /// </summary>
        private void AttachViewModel()
        {
            try
            {
                if (CardRoot is null) return;

                var vm = DataContext as LicenseStatusViewModel
                         ?? LicenseStatusViewModel.Instance;

                if (ReferenceEquals(CardRoot.DataContext, vm)) return;

                CardRoot.DataContext = vm;
                VmUnavailableNote.Visibility = Visibility.Collapsed;
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
