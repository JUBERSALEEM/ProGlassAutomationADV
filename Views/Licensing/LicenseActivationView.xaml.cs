using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.Logging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ProGlassAutomation.Views.Licensing
{
    /// <summary>
    /// In-app secure offline activation page.
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

        private string? _lastRequestJson;

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
                              "Install a vendor-signed license key before it ends to keep working without interruption."
                            : "A vendor-issued trial is active on this machine. " +
                              "Install a full license key before the trial ends.";
                        break;
                    }
                case LicenseStatus.TrialExpired:
                    HeadlineText.Text = "Trial ended — read-only mode";
                    SubheadText.Text =
                        "The trial window has ended and all data is preserved, but the app is now in read-only mode. " +
                        "Paste a vendor-signed license key to continue creating and editing.";
                    break;
                case LicenseStatus.NotActivated:
                    HeadlineText.Text = "Activate ProGlass Automation";
                    SubheadText.Text =
                        "Paste the license key your vendor sent you, or use the advanced request flow below.";
                    break;
                case LicenseStatus.Invalid:
                    HeadlineText.Text = "License problem";
                    SubheadText.Text =
                        "The installed license state could not be trusted. " +
                        "Paste a fresh vendor-signed license key, or generate a new activation request and re-import the signed response.";
                    break;
                case LicenseStatus.MachineMismatch:
                    HeadlineText.Text = "Wrong machine";
                    SubheadText.Text =
                        "This license is bound to a different machine fingerprint. " +
                        "Ask your vendor for a key bound to this PC's ID, or generate a new activation request from this PC.";
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
                        "The license has expired. Paste a renewed license key from your vendor to restore full access.";
                    break;
                case LicenseStatus.ExpiringSoon:
                    HeadlineText.Text = "License expires soon";
                    SubheadText.Text =
                        "The current license is valid but nearing expiry. Ask your vendor for a renewed key before it expires.";
                    break;
                default:
                    HeadlineText.Text = "ProGlass Automation";
                    SubheadText.Text = "Secure offline activation";
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

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select a signed activation response file",
                Filter =
                    "Activation response (*.lic,*.json)|*.lic;*.json|" +
                    "All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;
            TryInstallFromFile(dialog.FileName);
        }

        private void GenerateRequestButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 2026 workflow simplification: no vendor license reference
                // is required anymore. The request binds this installation,
                // machine fingerprint and a fresh nonce on its own.
                _lastRequestJson = LicenseService.CreateActivationRequestJson(string.Empty);
                RequestOutputBox.Text = _lastRequestJson;
                ErrorText.Visibility = Visibility.Collapsed;
                SetHint(
                    "Activation request generated. Save or copy it exactly as shown and send it to your vendor for signing.");
            }
            catch (Exception ex)
            {
                ShowError("Could not generate activation request: " + ex.Message);
            }
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

        private void SaveRequestButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_lastRequestJson))
            {
                ShowError("Generate the activation request first.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Save activation request",
                Filter = "Activation request (*.pgrequest)|*.pgrequest|JSON (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = LicenseService.ActivationRequestFileExtension,
                FileName = "activation-request" + LicenseService.ActivationRequestFileExtension,
                AddExtension = true
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(dialog.FileName, _lastRequestJson);
                SetHint("Activation request saved to " + dialog.FileName);
            }
            catch (Exception ex)
            {
                ShowError("Could not save activation request: " + ex.Message);
            }
        }

        private void CopyRequestButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_lastRequestJson))
            {
                ShowError("Generate the activation request first.");
                return;
            }

            try
            {
                Clipboard.SetText(_lastRequestJson);
                SetHint("Activation request copied to clipboard.");
            }
            catch (Exception ex)
            {
                ShowError("Could not copy the activation request: " + ex.Message);
            }
        }

        private void PasteApplyButton_Click(object sender, RoutedEventArgs e)
        {
            string text = (PasteTextBox.Text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                ShowError("Paste the signed activation response JSON from your vendor, then click Apply.");
                return;
            }

            ErrorText.Visibility = Visibility.Collapsed;
            var result = LicenseService.InstallText(text);
            if (!result.CanRun)
            {
                ShowError("Activation failed: " + (result.ErrorDetail
                    ?? LicenseUiText.StatusDescription(result.Status)));
                return;
            }

            OnActivated(result);
        }

        private void AdvancedToggleButton_Click(object sender, RoutedEventArgs e)
        {
            // The advanced request/response flow is collapsed by default —
            // the normal path is simply pasting the license key.
            bool show = AdvancedPanel.Visibility != Visibility.Visible;
            AdvancedPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            AdvancedToggleButton.Content = show
                ? "ADVANCED: ACTIVATION REQUEST (NOT REQUIRED) ▴"
                : "ADVANCED: ACTIVATION REQUEST (NOT REQUIRED) ▾";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseClosed();
        }

        private void TryInstallFromFile(string path)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            var result = LicenseService.InstallFile(path);
            if (!result.CanRun)
            {
                ShowError("Activation failed: " + (result.ErrorDetail
                    ?? LicenseUiText.StatusDescription(result.Status)));
                return;
            }

            OnActivated(result);
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

        private void SetHint(string message)
        {
            HintText.Text = message;
        }

        private void RaiseClosed()
        {
            try { Closed?.Invoke(this, EventArgs.Empty); }
            catch (Exception ex) { AppLogService.Debug("[LicenseActivationView] RaiseClosed failed: " + ex.Message); }
        }
    }
}
