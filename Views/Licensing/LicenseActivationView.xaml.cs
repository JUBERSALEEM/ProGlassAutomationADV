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
                "Secure offline activation stores the installed response in Windows-protected local storage. " +
                "Generate a request file, send it to your vendor, then import the signed response they return.";
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
                    HeadlineText.Text = "Vendor-issued trial active";
                    SubheadText.Text =
                        "A signed offline trial response is active on this machine. " +
                        "Request a renewed signed response before the trial ends.";
                    break;
                case LicenseStatus.TrialExpired:
                    HeadlineText.Text = "Trial expired";
                    SubheadText.Text =
                        "The signed offline trial response has expired. " +
                        "Generate a fresh request and ask your vendor for renewal or a full license response.";
                    break;
                case LicenseStatus.NotActivated:
                    HeadlineText.Text = "Activate ProGlass Automation";
                    SubheadText.Text =
                        "Generate an activation request, send it to your vendor, and import the signed activation response they return.";
                    break;
                case LicenseStatus.Invalid:
                    HeadlineText.Text = "License problem";
                    SubheadText.Text =
                        "The installed activation state could not be trusted. " +
                        "Generate a fresh request and re-import a vendor-signed response.";
                    break;
                case LicenseStatus.MachineMismatch:
                    HeadlineText.Text = "Wrong machine";
                    SubheadText.Text =
                        "This activation response was issued for a different machine fingerprint. " +
                        "Generate a new request from this PC and ask your vendor to re-issue it.";
                    break;
                case LicenseStatus.GracePeriod:
                    HeadlineText.Text = "Read-only grace mode";
                    SubheadText.Text =
                        "The signed activation response is past expiry but still inside its signed grace policy. " +
                        "Renew before the grace window ends.";
                    break;
                case LicenseStatus.Expired:
                    HeadlineText.Text = "License expired";
                    SubheadText.Text =
                        "The signed activation response has expired. Generate a fresh request and obtain a renewed response from your vendor.";
                    break;
                case LicenseStatus.ExpiringSoon:
                    HeadlineText.Text = "License expires soon";
                    SubheadText.Text =
                        "The current signed activation response is valid but nearing expiry. Renew it before the signed window ends.";
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
                SourceValue.Text = "Signed offline activation response";
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
                string licenseReference = (LicenseReferenceBox.Text ?? string.Empty).Trim();
                if (licenseReference.Length == 0)
                {
                    ShowError("Enter the vendor license reference before generating the activation request.");
                    return;
                }

                _lastRequestJson = LicenseService.CreateActivationRequestJson(licenseReference);
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
