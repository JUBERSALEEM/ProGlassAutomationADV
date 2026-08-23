// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Licensing/WelcomeView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: First-run / no-license dialog
//
// Hosted by App.xaml.cs in a modal window when
// LicenseService.Current.Status == LicenseStatus.NotActivated.
// The three buttons return a choice via the public property
// <see cref="Result"/>:
//
//   Activate       — the user picked a .lic file, InstallFile
//                    succeeded, status was reloaded.
//   StartTrial     — InstallTrial succeeded, status was reloaded.
//   ContinueReadOnly — the user accepted read-only mode.
//
// If any step fails (bad file, no key, no permission), the
// dialog stays open and the failure is shown in the
// StatusDetailText block so the user can recover without
// restarting the app.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.Logging;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ProGlassAutomation.Views.Licensing
{
    /// <summary>
    /// First-run / no-license dialog. See file header.
    /// </summary>
    public partial class WelcomeView : UserControl
    {
        /// <summary>Outcome chosen by the user.</summary>
        public enum WelcomeResult
        {
            /// <summary>User closed the dialog without choosing.
            /// App should exit.</summary>
            Cancelled = 0,
            Activate = 1,
            StartTrial = 2,
            ContinueReadOnly = 3
        }

        /// <summary>The result the user picked. Read after
        /// the dialog has been shown and dismissed.</summary>
        public WelcomeResult Result { get; private set; } = WelcomeResult.Cancelled;

        public WelcomeView()
        {
            InitializeComponent();

            // Mirror the current LicenseService status into
            // the visible text. Most of the time this will be
            // NotActivated ("No license file found at …"), but
            // the welcome dialog is also shown when the
            // public key is still the placeholder — in which
            // case the placeholder diagnostic is the more
            // useful message.
            var current = LicenseService.Current;
            StatusText.Text = LicenseUiText.StatusBadge(current.Status);
            StatusDetailText.Text = string.IsNullOrEmpty(current.ErrorDetail)
                ? LicenseUiText.StatusDescription(current.Status)
                : current.ErrorDetail;
            HintText.Text =
                "The license file (or its absence) is checked at " +
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "ProGlassAutomation",
                    LicenseService.DefaultLicenseFileName) +
                ". You can change the activation at any time.";
        }

        // ── Button handlers ─────────────────────────────────────

        private void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select a ProGlass Automation license file",
                Filter = "License files (*.lic)|*.lic|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            SetBusy("Verifying…");
            var result = LicenseService.InstallFile(dialog.FileName);
            if (!result.CanRun)
            {
                SetError("Activation failed: " + (result.ErrorDetail
                    ?? LicenseUiText.StatusDescription(result.Status)));
                return;
            }

            Result = WelcomeResult.Activate;
            RaiseAccepted();
        }

        private void TrialButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Start a 30-day trial?\n\n" +
                "The trial unlocks Inquiry, Proforma, Job Order, " +
                "Delivery Note, Tax Invoice, Statement, Master Data " +
                "and Sheet Store for 30 days from now. The trial " +
                "is signed against the developer's private key and " +
                "stored locally on this machine. You can activate a " +
                "full license at any time; the trial will be replaced.",
                "Start 30-day trial",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes) return;

            SetBusy("Creating trial…");
            var result = LicenseService.InstallTrial(30);
            if (!result.CanRun)
            {
                SetError("Trial failed: " + (result.ErrorDetail
                    ?? LicenseUiText.StatusDescription(result.Status)));
                return;
            }

            Result = WelcomeResult.StartTrial;
            RaiseAccepted();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Continue without a license?\n\n" +
                "The app will boot in read-only mode. You can " +
                "browse the UI but cannot save data, print, or " +
                "export. To leave read-only mode, activate a " +
                "license from Settings → Data → Database Health.",
                "Continue without a license",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes) return;

            Result = WelcomeResult.ContinueReadOnly;
            RaiseAccepted();
        }

        // ── State updates ───────────────────────────────────────

        private void SetBusy(string message)
        {
            StatusText.Text = message;
            StatusDetailText.Text = "Please wait…";
        }

        private void SetError(string message)
        {
            StatusText.Text = "Failed";
            StatusDetailText.Text = message;
            AppLogService.Warning(
                "Welcome flow failed: " + message,
                new LogContext
                {
                    Module = "Licensing",
                    Class = nameof(WelcomeView),
                    Operation = "OnError"
                });
        }

        /// <summary>
        /// Raised once when the user has chosen an option
        /// that succeeds. The host window closes itself in
        /// response and reads <see cref="Result"/>.
        /// </summary>
        public event EventHandler? Accepted;

        private void RaiseAccepted()
        {
            try { Accepted?.Invoke(this, EventArgs.Empty); }
            catch { /* host should still close */ }
        }
    }
}
