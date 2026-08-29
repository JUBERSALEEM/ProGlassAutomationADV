// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Licensing/LicenseActivationView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Licensing page — key-paste activation (2026 simple flow)
//
// ONE ACTION: the user pastes the PGKEY1 license key their vendor sent
// and clicks Apply. LicenseService.InstallKeyText verifies the vendor
// signature against the embedded trusted-key ring BEFORE storing
// anything; a key that fails verification changes nothing and the
// automatic trial keeps the app running.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Views.Licensing
{
    /// <summary>
    /// In-app license page: paste the key → instant activation.
    /// </summary>
    public partial class LicenseActivationView : UserControl
    {
        public static readonly DependencyProperty
            IsClosableProperty = DependencyProperty.Register(
                nameof(IsClosable), typeof(bool),
                typeof(LicenseActivationView),
                new FrameworkPropertyMetadata(
                    true,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public bool IsClosable
        {
            get => (bool)GetValue(IsClosableProperty);
            set => SetValue(IsClosableProperty, value);
        }

        public event EventHandler? Activated;
        public event EventHandler? Closed;

        public LicenseActivationView()
        {
            InitializeComponent();
            LicenseService.StatusChanged += OnLicenseStatusChanged;
            Unloaded += OnLicenseViewUnloaded;

            InstallationIdBox.Text = LicenseService.CurrentInstallationId;
            FingerprintDisplayBox.Text = LicenseService.CurrentMachineFingerprint;
            RefreshFromService();

            HintText.Text =
                "Paste the license key your vendor sent you (it starts with PGKEY1). " +
                "The 30-day trial keeps the app fully usable until the key is applied.";
        }

        private void OnLicenseViewUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LicenseService.StatusChanged -= OnLicenseStatusChanged;
                Unloaded -= OnLicenseViewUnloaded;
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[LicenseActivationView] View cleanup failed: " + ex.Message);
            }
        }

        private void OnLicenseStatusChanged(object? sender, LicenseVerificationResult e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(RefreshFromService));
                return;
            }
            RefreshFromService();
        }

        private void RefreshFromService()
        {
            InstallationIdBox.Text = LicenseService.CurrentInstallationId;
            FingerprintDisplayBox.Text = LicenseService.CurrentMachineFingerprint;

            var current = LicenseService.Current;
            StatusBadgeText.Text = LicenseUiText.StatusBadge(current.Status);
            StatusDetailText.Text = string.IsNullOrEmpty(current.ErrorDetail)
                ? LicenseUiText.StatusDescription(current.Status)
                : current.ErrorDetail;

            switch (current.Status)
            {
                case LicenseStatus.Trial:
                    {
                        bool autoTrial = current.Payload != null
                            && string.Equals(current.Payload.TrialSource,
                                LicenseService.AutoTrialSourceMarker,
                                StringComparison.Ordinal);
                        HeadlineText.Text = autoTrial
                            ? "Trial active — app fully usable"
                            : "Trial active";
                        SubheadText.Text = autoTrial
                            ? "A 30-day automatic evaluation trial is running on this machine. " +
                              "Paste your license key before it ends to keep working without interruption."
                            : "A vendor-issued trial is active on this machine. " +
                              "Paste your full license key before the trial ends.";
                        break;
                    }
                case LicenseStatus.TrialExpired:
                    HeadlineText.Text = "Trial ended — read-only mode";
                    SubheadText.Text =
                        "The trial window has ended and all data is preserved, but the app is now in read-only mode. " +
                        "Paste your license key to continue creating and editing.";
                    break;
                case LicenseStatus.NotActivated:
                    HeadlineText.Text = "Activate ProGlass Automation";
                    SubheadText.Text =
                        "Paste the license key your vendor sent you to activate.";
                    break;
                case LicenseStatus.Invalid:
                    HeadlineText.Text = "License problem";
                    SubheadText.Text =
                        "The installed license state could not be trusted. " +
                        "Paste a fresh license key from your vendor.";
                    break;
                case LicenseStatus.MachineMismatch:
                    HeadlineText.Text = "Wrong machine";
                    SubheadText.Text =
                        "This license is bound to a different machine fingerprint. " +
                        "Ask your vendor for a key bound to this PC's ID.";
                    break;
                case LicenseStatus.GracePeriod:
                    HeadlineText.Text = "Read-only grace mode";
                    SubheadText.Text =
                        "The license is past expiry but still inside its signed grace policy. " +
                        "Paste a renewed license key before the grace window ends.";
                    break;
                case LicenseStatus.Expired:
                    HeadlineText.Text = "License expired";
                    SubheadText.Text =
                        "The license has expired. Paste a renewed license key to restore full access.";
                    break;
                case LicenseStatus.ExpiringSoon:
                    HeadlineText.Text = "License expires soon";
                    SubheadText.Text =
                        "The current license is valid but nearing expiry. Ask your vendor for a renewed key before it expires.";
                    break;
                default:
                    HeadlineText.Text = "ProGlass Automation";
                    SubheadText.Text = "Licensing";
                    break;
            }

            if (current.Payload != null)
            {
                var p = current.Payload;
                EditionValue.Text = p.Edition.ToString();
                CustomerValue.Text = string.IsNullOrEmpty(p.CustomerId)
                    ? "(none)"
                    : p.CustomerId + (string.IsNullOrEmpty(p.CompanyName)
                        ? "" : " — " + p.CompanyName);
                ExpiresValue.Text = p.ExpiresAt == default || p.ExpiresAt == DateTime.MaxValue
                    ? "(no expiry)"
                    : p.ExpiresAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                LicenseIdValue.Text = string.IsNullOrEmpty(p.LicenseId)
                    ? "(none)"
                    : p.LicenseId;
                SourceValue.Text = LicenseService.IsLicenseKeyActive
                    ? "Vendor-signed license key"
                    : "Signed offline activation response";
                LicenseDetailsGrid.Visibility = Visibility.Visible;
            }
            else
            {
                LicenseDetailsGrid.Visibility = Visibility.Collapsed;
            }

            ErrorText.Visibility = Visibility.Collapsed;
            ErrorText.Text = string.Empty;
        }

        private void KeyApplyButton_Click(object sender, RoutedEventArgs e)
        {
            string text = KeyPasteTextBox.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowError("Paste the license key your vendor sent you (starts with PGKEY1), then click Apply.");
                return;
            }

            ErrorText.Visibility = Visibility.Collapsed;
            var result = LicenseService.InstallKeyText(text);
            if (!result.CanRun)
            {
                ShowError("License key was not applied: " + (result.ErrorDetail
                    ?? LicenseUiText.StatusDescription(result.Status)));
                return;
            }

            KeyPasteTextBox.Text = string.Empty;
            OnActivated(result);
        }

        private void KeyBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select a license key file",
                Filter =
                    "License key (*.lic;*.pgkey;*.proglasslicense;*.json)|*.lic;*.pgkey;*.proglasslicense;*.json|" +
                    "All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            ErrorText.Visibility = Visibility.Collapsed;
            var result = LicenseService.InstallKeyFile(dialog.FileName);
            if (!result.CanRun)
            {
                ShowError("License key was not applied: " + (result.ErrorDetail
                    ?? LicenseUiText.StatusDescription(result.Status)));
                return;
            }

            OnActivated(result);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseClosed();
        }

        private void OnActivated(LicenseVerificationResult result)
        {
            try
            {
                if (result.Status is LicenseStatus.Valid or LicenseStatus.ExpiringSoon or LicenseStatus.Trial)
                    LicenseActivatedDialog.ShowFor(Window.GetWindow(this), result);

                Activated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[LicenseActivationView] Activation host close failed: " + ex.Message);
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
            AppLogService.Warning(
                "License activation failed: " + message,
                new LogContext
                {
                    Module = "Licensing",
                    Class = nameof(LicenseActivationView),
                    Operation = "OnError"
                });
        }

        private void RaiseClosed()
        {
            try { Closed?.Invoke(this, EventArgs.Empty); }
            catch (Exception ex) { AppLogService.Debug("[LicenseActivationView] RaiseClosed failed: " + ex.Message); }
        }
    }
}
