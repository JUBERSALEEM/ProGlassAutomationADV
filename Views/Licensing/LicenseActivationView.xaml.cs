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
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ProGlass.Licensing;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.Logging;
// Both licensing namespaces are imported above, and the contract also
// defines a LicenseService type — the alias pins every bare
// "LicenseService" reference in this file to the APP service.
using LicenseService = ProGlassAutomation.Services.Licensing.LicenseService;

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
            TryApplyKeyText(KeyPasteTextBox.Text ?? string.Empty);
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

            string text;
            try
            {
                var info = new FileInfo(dialog.FileName);
                if (info.Length > 700_000L)
                {
                    ShowError("License key file exceeds the safe size limit.");
                    return;
                }
                text = File.ReadAllText(dialog.FileName);
            }
            catch (Exception ex)
            {
                ShowError("Could not read the license key file: " + ex.Message);
                return;
            }

            TryApplyKeyText(text);
        }

        /// <summary>
        /// The single paste path: armor check → decode → signature
        /// verification → plan-mismatch guard → verify-before-store
        /// install. Nothing is stored unless every stage passes.
        /// </summary>
        private bool TryApplyKeyText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowError("Paste the license key your vendor sent you (starts with PGKEY1), then click Apply.");
                return false;
            }

            if (!LicenseKeyArmor.IsArmoredBlock(text))
            {
                ShowError(
                    "Only license keys produced by the License Key Generator are accepted. " +
                    "Paste the single-line block starting with \"" + LicenseKeyArmor.Prefix +
                    "\" exactly as generated.");
                return false;
            }

            // Decode + verify BEFORE any warning dialogs, so the plan
            // checks below always run against a key we would accept.
            string? json = LicenseKeyArmor.TryExtractJson(text);
            LicenseValidationResult? validation = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    validation = ProGlassLicense.VerifyText(json);
                }
                catch (Exception ex)
                {
                    ShowError("The license key could not be verified: " + ex.Message);
                    return false;
                }
            }

            if (validation == null || !validation.IsValid || validation.Document == null)
            {
                ShowError(
                    "The license key is not valid" +
                    (string.IsNullOrWhiteSpace(validation?.Message) ? "." : ": " + validation!.Message) +
                    " It may have been modified or produced by a different tool. " +
                    "Get a fresh key from your vendor.");
                return false;
            }

            // ── Plan-mismatch guard ────────────────────────────────────
            LicensePlanCatalog.Plan? pastedPlan = LicensePlanCatalog.Of(validation.Document);
            if (!ConfirmPlanChange(pastedPlan))
                return false;

            ErrorText.Visibility = Visibility.Collapsed;
            var result = LicenseService.InstallKeyText(text);
            if (!result.CanRun)
            {
                ShowError("License key was not applied: " + (result.ErrorDetail
                    ?? LicenseUiText.StatusDescription(result.Status)));
                return false;
            }

            KeyPasteTextBox.Text = string.Empty;
            OnActivated(result);
            return true;
        }

        /// <summary>
        /// Plan-mismatch warnings (2026-08-29): a key for a different
        /// plan than expected is almost always the wrong key.
        ///   1. A LONGER plan is already active → pasting a shorter one
        ///      would downgrade the customer. Warn strongly.
        ///   2. The same plan is already active → replace confirmation.
        ///   3. Nothing active but the pasted plan differs from the plan
        ///      selected in the PLANS cards → "you are pasting the wrong
        ///      plan key" warning.
        /// </summary>
        private bool ConfirmPlanChange(LicensePlanCatalog.Plan? pastedPlan)
        {
            if (pastedPlan == null)
                return true;   // open-ended / non-plan key: no plan logic

            Window owner = OwnerWindow();
            var current = LicenseService.Current;
            LicensePlanCatalog.Plan? activePlan =
                current.Status is LicenseStatus.Valid or LicenseStatus.ExpiringSoon
                    ? PlanOfPayload(current.Payload)
                    : null;

            // 1) Longer plan already active → downgrade warning.
            if (activePlan != null && activePlan.Days > pastedPlan.Days)
            {
                string until = SafeLocalDate(current.Payload?.ExpiresAt);
                var answer = MessageBox.Show(owner,
                    "A " + activePlan.Name + " license is already active on this app" +
                    (until == null ? "." : " and runs until " + until + ".") + "\n\n" +
                    "The key you pasted is a " + pastedPlan.Name + " key. Applying it would " +
                    "REPLACE the active " + activePlan.Name + " license with a shorter one — " +
                    "this is usually the wrong key.\n\nApply the " + pastedPlan.Name + " key anyway?",
                    "A longer license is already active",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                return answer == MessageBoxResult.Yes;
            }

            // 2) Same plan already active → replace confirmation.
            if (activePlan != null && activePlan.Days == pastedPlan.Days)
            {
                var answer = MessageBox.Show(owner,
                    "A " + activePlan.Name + " license is already active. " +
                    "Applying this key replaces it.\n\nContinue?",
                    "License already active",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                return answer == MessageBoxResult.Yes;
            }

            // 3) No active license → selected-plan mismatch warning.
            string selected = SelectedPlanName();
            if (!string.Equals(selected, pastedPlan.Name, StringComparison.OrdinalIgnoreCase))
            {
                var answer = MessageBox.Show(owner,
                    "You selected \"" + selected + "\" in the plans, but the key you pasted " +
                    "is a " + pastedPlan.Name + " key.\n\nIf you ordered \"" + selected +
                    "\", this is the wrong key — ask your vendor to re-check. " +
                    "Apply the " + pastedPlan.Name + " key anyway?",
                    "Plan mismatch — possible wrong key",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                return answer == MessageBoxResult.Yes;
            }

            return true;
        }

        private static LicensePlanCatalog.Plan? PlanOfPayload(LicensePayload? payload)
        {
            if (payload == null) return null;
            DateTime issued = payload.IssuedAt == default
                ? DateTime.UtcNow
                : payload.IssuedAt.ToUniversalTime();
            DateTime? expires =
                payload.ExpiresAt == default || payload.ExpiresAt == DateTime.MaxValue
                    ? null
                    : payload.ExpiresAt.ToUniversalTime();
            return LicensePlanCatalog.FromValidityWindow(issued, expires);
        }

        private static string? SafeLocalDate(DateTime? utc)
        {
            if (utc == null || utc.Value == default || utc.Value == DateTime.MaxValue)
                return null;
            try { return utc.Value.ToLocalTime().ToString("yyyy-MM-dd"); }
            catch { return null; }
        }

        private Window OwnerWindow() =>
            Window.GetWindow(this) ?? Application.Current.MainWindow;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseClosed();
        }

        private string SelectedPlanName()
        {
            if (Plan7Radio.IsChecked == true) return "7 Days";
            if (Plan30Radio.IsChecked == true) return "30 Days";
            if (Plan6mRadio.IsChecked == true) return "6 Months";
            if (Plan2yRadio.IsChecked == true) return "2 Years";
            return "1 Year";
        }

        /// <summary>
        /// Copies a ready-to-send plan request (plan + PC ID). The vendor
        /// replies with a PGKEY1 key the customer pastes in the box
        /// above — the generator tool produces it with one click.
        /// </summary>
        private void GetLicenseKeyButton_Click(object sender, RoutedEventArgs e)
        {
            string message =
                "Hi — I would like a ProGlass Automation license key.\n" +
                "Plan: " + SelectedPlanName() + "\n" +
                "My PC ID (only needed if you bind the key to my machine): " +
                LicenseService.CurrentMachineFingerprint;

            try
            {
                Clipboard.SetText(message);
            }
            catch
            {
                // Clipboard can be locked by another process; the text is
                // still shown in the dialog so nothing is lost.
            }

            MessageBox.Show(OwnerWindow(),
                "Your request is copied to the clipboard — send it to your vendor " +
                "(email, WhatsApp, chat). They will reply with a single key block " +
                "starting with PGKEY1; paste it in the box at the top and press " +
                "APPLY LICENSE KEY to activate instantly.\n\n" +
                "Request preview:\n" + message,
                "Get license key",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
