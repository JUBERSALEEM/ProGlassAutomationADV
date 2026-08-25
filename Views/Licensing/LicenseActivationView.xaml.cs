// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Licensing/LicenseActivationView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: In-app license activation page
//
// Commit 3 of the licensing refactor. Replaces the pre-login
// WelcomeView with an in-app activation page that can be
// hosted both as a pre-login modal (Commit 3 wiring in
// App.xaml.cs) and as a "license required" page inside the
// main shell (Commit 4).
//
// The OLD WelcomeView had three exit paths:
//   Activate       — the user picked a .lic file, status reloaded.
//   StartTrial     — the user requested a 30-day trial.
//   ContinueReadOnly — the user accepted read-only mode.
//
// The NEW LicenseActivationView has two:
//   Activated      — a license was successfully installed,
//                    LicenseService.Current.CanRun is now true,
//                    the host window should advance (pre-login:
//                    go to splash; in-app: dismiss the page).
//   Closed         — the user closed the dialog without
//                    installing a license. The host window
//                    reads the current LicenseService.Current
//                    to decide what to do (pre-login: exit;
//                    in-app: keep showing this page because
//                    the license is still missing/expired).
//
// The OLD "Start a 30-day Trial" and "Continue Read-Only"
// buttons are intentionally NOT present:
//   • Trial is now auto-issued by
//     LicenseService.IssueAutoTrialIfEligible (Commit 2).
//   • Read-only is gone because the strict 7-day expiry
//     makes read-only a meaningless halfway state.
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
    /// In-app license activation page. Hosted as a
    /// pre-login modal window (Commit 3 wiring) and as a
    /// "license required" page inside the main shell
    /// (Commit 4). Always shows the current
    /// LicenseService status so the user can see whether
    /// they need to take action.
    /// </summary>
    public partial class LicenseActivationView : UserControl
    {
        /// <summary>
        /// Dependency property that controls whether
        /// the Close button is visible. Defaults to true
        /// (legacy modal-host usage). The main shell
        /// (Commit 4) sets this to false because there
        /// is no meaningful close action when the
        /// overlay is blocking the content area — the
        /// user must install a license to make the
        /// overlay go away.
        /// </summary>
        public static readonly System.Windows.DependencyProperty
            IsClosableProperty = System.Windows.DependencyProperty.Register(
                nameof(IsClosable), typeof(bool),
                typeof(LicenseActivationView),
                new System.Windows.FrameworkPropertyMetadata(
                    true,
                    System.Windows.FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// True when the Close button should be
        /// visible. Host XAML sets this to false for
        /// the in-app shell overlay; the modal host
        /// (Commit 3) leaves it at the default true.
        /// </summary>
        public bool IsClosable
        {
            get => (bool)GetValue(IsClosableProperty);
            set => SetValue(IsClosableProperty, value);
        }

        /// <summary>
        /// Raised when the user has successfully installed
        /// a license. <see cref="LicenseService.Current"/>
        /// is guaranteed to be CanRun when this fires.
        /// </summary>
        public event EventHandler? Activated;

        /// <summary>
        /// Raised when the user closes the dialog without
        /// installing a license. The host reads
        /// <see cref="LicenseService.Current"/> to decide
        /// what to do next.
        /// </summary>
        public event EventHandler? Closed;

        public LicenseActivationView()
        {
            InitializeComponent();

            // Live status binding. Re-mirror on every
            // LicenseService.StatusChanged so external
            // reloads (e.g. a .lic dropped by another
            // process) update the view without the user
            // having to click anything.
            LicenseService.StatusChanged += OnLicenseStatusChanged;

            // Unloaded subscription (instead of an
            // OnUnloaded override) detaches the static
            // event handler when the view is detached
            // from the visual tree — the modal window
            // closes, the host page navigates away, etc.
            //
            // Why an event subscription and not the
            // conventional OnUnloaded virtual: the
            // override requires the WPF
            // FrameworkElement.OnUnloaded(RoutedEventArgs)
            // signature, which only resolves if the
            // project compiles against
            // PresentationFramework. Some hosts
            // (including the in-app shell overlay
            // pattern used by MainWindow) build against
            // trimmed WPF references where the virtual
            // is not visible, and the override fails
            // with CS0115 ("no suitable method found
            // to override"). The Unloaded event is the
            // same hook with a portable signature, and
            // adding / removing the handler is symmetric
            // so the subscription is balanced.
            Unloaded += OnLicenseViewUnloaded;

            RefreshFromService();

            HintText.Text =
                "The license file lives at " +
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "ProGlassAutomation",
                    LicenseService.DefaultLicenseFileName) +
                ". Drop / paste / browse above to replace it.";
        }

        /// <summary>
        /// Detaches the static <see cref="LicenseService.StatusChanged"/>
        /// subscription when the view is detached from
        /// the visual tree. Without this, the static
        /// <see cref="LicenseService"/> holds a reference
        /// to the dead view via the event delegate, and
        /// every modal that ever opened stays in memory
        /// for the lifetime of the process.
        ///
        /// See the constructor for why this is an event
        /// handler subscription rather than an
        /// OnUnloaded override.
        /// </summary>
        private void OnLicenseViewUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LicenseService.StatusChanged -= OnLicenseStatusChanged;
                Unloaded -= OnLicenseViewUnloaded;
            }
            catch (System.Exception ex)
            {
                // Defensive: never let the cleanup
                // throw, which would mask the real
                // reason the view is being unloaded.
                ProGlassAutomation.Services.Logging.AppLogService.Debug("[LicenseActivationView] View cleanup failed: " + ex.Message);
            }
        }

        private void OnLicenseStatusChanged(object? sender, LicenseVerificationResult e)
        {
            // Marshal back to the UI thread. The view is
            // a WPF control, so updates must run on the
            // dispatcher; the StatusChanged event is
            // raised on whatever thread called
            // LicenseService.Reload.
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action(() => RefreshFromService()));
                return;
            }
            RefreshFromService();
        }

        private void RefreshFromService()
        {
            var current = LicenseService.Current;
            StatusBadgeText.Text = LicenseUiText.StatusBadge(current.Status);
            StatusDetailText.Text = string.IsNullOrEmpty(current.ErrorDetail)
                ? LicenseUiText.StatusDescription(current.Status)
                : current.ErrorDetail;

            // Headline + subhead are tweaked per status so
            // the user is not asked to "activate" when the
            // problem is actually an expired trial.
            switch (current.Status)
            {
                case LicenseStatus.Trial:
                    HeadlineText.Text = "ProGlass is on a 7-day trial";
                    SubheadText.Text =
                        "The trial unlocks every module. " +
                        "Activate a full license before the trial " +
                        "ends to keep using the app.";
                    break;
                case LicenseStatus.TrialExpired:
                    HeadlineText.Text = "Trial expired";
                    SubheadText.Text =
                        "The 7-day trial has ended and the trial " +
                        "license was removed from this machine. " +
                        "Activate a full license below to continue.";
                    break;
                case LicenseStatus.NotActivated:
                    HeadlineText.Text = "Activate ProGlass Automation";
                    SubheadText.Text =
                        "Paste a license key from your vendor, or " +
                        "drop a .lic file. A 7-day trial is " +
                        "automatically issued on first launch; if " +
                        "you do not see one, ask your vendor for a " +
                        "key.";
                    break;
                case LicenseStatus.Invalid:
                case LicenseStatus.MachineMismatch:
                case LicenseStatus.Expired:
                case LicenseStatus.ActivationLimit:
                    HeadlineText.Text = "License problem";
                    SubheadText.Text =
                        "The current license is not in a runnable " +
                        "state. Install a replacement .lic file or " +
                        "paste a fresh key below.";
                    break;
                default:
                    HeadlineText.Text = "ProGlass Automation";
                    SubheadText.Text = "Activation";
                    break;
            }

            // Detail card: edition / customer / expires /
            // id / source. Only shown when we have a
            // payload (any non-NotActivated state).
            if (current.Payload != null)
            {
                var p = current.Payload;
                EditionValue.Text = p.Edition.ToString();
                CustomerValue.Text = string.IsNullOrEmpty(p.CustomerId)
                    ? "(none)"
                    : p.CustomerId + (string.IsNullOrEmpty(p.CompanyName)
                        ? "" : " — " + p.CompanyName);
                if (!string.IsNullOrEmpty(p.TrialSource)
                    && p.TrialExpiresAt != default)
                {
                    ExpiresValue.Text =
                        p.TrialExpiresAt.ToLocalTime()
                            .ToString("yyyy-MM-dd HH:mm")
                        + "  (auto-trial, " + p.TrialSource + ")";
                }
                else
                {
                    ExpiresValue.Text = p.ExpiresAt == default
                        ? "(no expiry)"
                        : p.ExpiresAt.ToLocalTime()
                            .ToString("yyyy-MM-dd HH:mm");
                }
                LicenseIdValue.Text = string.IsNullOrEmpty(p.LicenseId)
                    ? "(none)"
                    : p.LicenseId;
                SourceValue.Text = string.IsNullOrEmpty(p.TrialSource)
                    ? "Customer-issued (LicenseGenerator)"
                    : "Auto-issued trial (" + p.TrialSource + ")";
                LicenseDetailsGrid.Visibility = Visibility.Visible;
            }
            else
            {
                LicenseDetailsGrid.Visibility = Visibility.Collapsed;
            }

            // Clear any stale error / busy state on
            // reload so the user is not staring at a
            // red label from a previous attempt.
            ErrorText.Visibility = Visibility.Collapsed;
            ErrorText.Text = "";
        }

        // ── Button handlers ─────────────────────────────────────

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select a ProGlass Automation license file",
                Filter = "License files (*.lic)|*.lic|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            TryInstallFromFile(dialog.FileName);
        }

        private void PasteApplyButton_Click(object sender, RoutedEventArgs e)
        {
            string text = (PasteTextBox.Text ?? "").Trim();
            if (text.Length == 0)
            {
                ShowError("Paste the license JSON your vendor sent, then click Apply.");
                return;
            }

            // Two accepted formats:
            //   1) Raw JSON envelope (starts with '{').
            //   2) Base64 of the JSON envelope (no '{').
            // We write to a temp .lic, then call
            // InstallFile so the same code path verifies
            // the signature. The temp file is deleted
            // after a successful install; on failure we
            // keep it for the user to inspect in
            // %TEMP%\ProGlassAutomation-paste-*.lic.
            string tempPath = Path.Combine(
                Path.GetTempPath(),
                "ProGlassAutomation-paste-" +
                Guid.NewGuid().ToString("N").Substring(0, 8) + ".lic");

            try
            {
                if (text.StartsWith("{", StringComparison.Ordinal))
                {
                    File.WriteAllText(tempPath, text);
                }
                else
                {
                    // Treat as base64. Convert.FromBase64String
                    // throws on garbage input; we catch below
                    // and report a friendly message.
                    byte[] bytes = Convert.FromBase64String(text);
                    File.WriteAllText(tempPath,
                        System.Text.Encoding.UTF8.GetString(bytes));
                }
            }
            catch (Exception ex)
            {
                ShowError("The pasted text is neither valid JSON nor base64. " +
                    "Check the file your vendor sent and try again. " +
                    "Details: " + ex.Message);
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch (System.Exception cleanupEx) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[LicenseActivationView] Temp license-file delete failed: " + cleanupEx.Message); }
                return;
            }

            TryInstallFromFile(tempPath, deleteAfter: true);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseClosed();
        }

        // ── Install helper ──────────────────────────────────────

        private void TryInstallFromFile(string path, bool deleteAfter = false)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            var result = LicenseService.InstallFile(path);
            // InstallFile already called Reload, so
            // StatusChanged fired and RefreshFromService
            // ran. We only need to display the new
            // status. If the install was not runnable,
            // surface the verifier's explanation in the
            // error label.

            if (deleteAfter)
            {
                // Best-effort cleanup of the temp file
                // we wrote for the paste path. We do
                // this after the install attempt
                // regardless of success so a future
                // paste is not contaminated by an old
                // .lic in %TEMP%.
                try { if (File.Exists(path)) File.Delete(path); }
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[LicenseActivationView.xaml] TryInstallFromFile: best effort: " + ex.Message); }
            }

            if (!result.CanRun)
            {
                ShowError("Activation failed: " + (result.ErrorDetail
                    ?? LicenseUiText.StatusDescription(result.Status)));
                return;
            }

            try { Activated?.Invoke(this, EventArgs.Empty); }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[LicenseActivationView.xaml] TryInstallFromFile: host should still close: " + ex.Message); }
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
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[LicenseActivationView.xaml] RaiseClosed: host should still close: " + ex.Message); }
        }
    }
}
