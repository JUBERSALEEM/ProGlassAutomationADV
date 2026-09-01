// ═══════════════════════════════════════════════════════════════════════
// FILE: LicenseActivationView.Part1.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.Logging;
using ThemeManager = ProGlassAutomation.Services.ThemeManager;

namespace ProGlassAutomation.Views.Licensing
{
        public partial class LicenseActivationView : UserControl
    {
            public static readonly DependencyProperty
            IsClosableProperty = DependencyProperty.Register(
                nameof(IsClosable), typeof(bool),
                typeof(LicenseActivationView),
                new FrameworkPropertyMetadata(
                    true,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnIsClosableChanged));

        public bool IsClosable
        {
            get => (bool)GetValue(IsClosableProperty);
            set => SetValue(IsClosableProperty, value);
        }

        public event EventHandler? Activated;
        public event EventHandler? Closed;

        // ── Named elements (built in code — no generated partial) ──────
        private TextBlock HeadlineText = null!;
        private TextBlock SubheadText = null!;
        private TextBlock StatusBadgeText = null!;
        private TextBlock StatusDetailText = null!;
        private Grid LicenseDetailsGrid = null!;
        private TextBlock EditionValue = null!;
        private TextBlock CustomerValue = null!;
        private TextBlock ExpiresValue = null!;
        private TextBlock LicenseIdValue = null!;
        private TextBlock SourceValue = null!;
        private TextBlock ErrorText = null!;
        private TextBlock HintText = null!;
        private TextBox KeyPasteTextBox = null!;
        private TextBox InstallationIdBox = null!;
        private TextBox FingerprintDisplayBox = null!;
        private RadioButton Plan7Radio = null!;
        private RadioButton Plan30Radio = null!;
        private RadioButton Plan6mRadio = null!;
        private RadioButton Plan1yRadio = null!;
        private RadioButton Plan2yRadio = null!;
        private Button CloseButton = null!;

        public LicenseActivationView()
        {
            BuildUi();
            LicenseService.StatusChanged += OnLicenseStatusChanged;
            ThemeManager.ThemeChanged += OnThemeChanged;
            Unloaded += OnLicenseViewUnloaded;

            InstallationIdBox.Text = LicenseService.CurrentInstallationId;
            FingerprintDisplayBox.Text = LicenseService.CurrentMachineFingerprint;
            RefreshFromService();

            HintText.Text =
                "Paste the license key your vendor sent you (it starts with PGKEY1). " +
                "The 30-day trial keeps the app fully usable until the key is applied.";
        }

        private static void OnIsClosableChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((LicenseActivationView)d).ApplyClosable((bool)e.NewValue);

        private void ApplyClosable(bool closable)
        {
            if (CloseButton != null)
                CloseButton.Visibility = closable
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void OnLicenseViewUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LicenseService.StatusChanged -= OnLicenseStatusChanged;
                ThemeManager.ThemeChanged -= OnThemeChanged;
                Unloaded -= OnLicenseViewUnloaded;
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[LicenseActivationView] View cleanup failed: " + ex.Message);
            }
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(RebuildForTheme));
                return;
            }
            RebuildForTheme();
        }

        /// <summary>
        /// Theme swaps re-tint every brush in the app; this control
        /// builds from resolved brush instances, so it rebuilds its
        /// tree (keeping the pasted text) instead of going stale.
        /// </summary>
        private void RebuildForTheme()
        {
            string pasted = KeyPasteTextBox?.Text ?? string.Empty;
            BuildUi();
            if (!string.IsNullOrEmpty(pasted))
                KeyPasteTextBox.Text = pasted;
            RefreshFromService();
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

            if (!ProGlass.Licensing.LicenseKeyArmor.IsArmoredBlock(text))
            {
                ShowError(
                    "Only license keys produced by the License Key Generator are accepted. " +
                    "Paste the single-line block starting with \"" + ProGlass.Licensing.LicenseKeyArmor.Prefix +
                    "\" exactly as generated.");
                return false;
            }

            // Decode + verify BEFORE any warning dialogs, so the plan
            // checks below always run against a key we would accept.
            string? json = ProGlass.Licensing.LicenseKeyArmor.TryExtractJson(text);
            ProGlass.Licensing.LicenseValidationResult? validation = null;
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
            ProGlass.Licensing.LicensePlanCatalog.Plan? pastedPlan =
                ProGlass.Licensing.LicensePlanCatalog.Of(validation.Document);
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
        private bool ConfirmPlanChange(ProGlass.Licensing.LicensePlanCatalog.Plan? pastedPlan)
        {
            if (pastedPlan == null)
                return true;   // open-ended / non-plan key: no plan logic

            Window owner = OwnerWindow();
            var current = LicenseService.Current;
            ProGlass.Licensing.LicensePlanCatalog.Plan? activePlan =
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

        private static ProGlass.Licensing.LicensePlanCatalog.Plan? PlanOfPayload(LicensePayload? payload)
        {
            if (payload == null) return null;
            DateTime issued = payload.IssuedAt == default
                ? DateTime.UtcNow
                : payload.IssuedAt.ToUniversalTime();
            DateTime? expires =
                payload.ExpiresAt == default || payload.ExpiresAt == DateTime.MaxValue
                    ? null
                    : payload.ExpiresAt.ToUniversalTime();
            return ProGlass.Licensing.LicensePlanCatalog.FromValidityWindow(issued, expires);
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

        // ────────────────────────────────────────────────────────────────────
        // Code-built UI (replaces the XAML tree + generated partial)
        // ────────────────────────────────────────────────────────────────────

        private void BuildUi()
        {
            double body = Sz("TextBody", 13);
            double caption = Sz("TextCaption", 12);
            double bodyLarge = Sz("TextBodyLarge", 14);
            double section = Sz("TextSection", 15);
            double displayLarge = Sz("TextDisplayLarge", 28);
            CornerRadius corner = Cr("CardCornerRadius", new CornerRadius(12));

            var root = new Border { Padding = new Thickness(28, 24, 28, 24) };
            root.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush");

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top
            };

            // ── Header ────────────────────────────────────────────────
            HeadlineText = new TextBlock
            {
                Text = "Activate ProGlass Automation",
                FontSize = displayLarge,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            HeadlineText.SetResourceReference(TextElement.ForegroundProperty, "PrimaryForegroundBrush");
            stack.Children.Add(HeadlineText);

            SubheadText = new TextBlock
            {
                Text = "Paste the license key your vendor sent you. A 30-day trial is already running, so the app is fully usable while you wait for your key.",
                FontSize = bodyLarge,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 24)
            };
            SubheadText.SetResourceReference(TextElement.ForegroundProperty, "SecondaryForegroundBrush");
            stack.Children.Add(SubheadText);

            // ── Card 1 · LICENSE KEY — PASTE TO ACTIVATE ──────────────
            var card1 = MakeCard(corner);
            var c1 = new StackPanel();
            c1.Children.Add(CardLabel("LICENSE KEY — PASTE TO ACTIVATE"));
            c1.Children.Add(Para(
                "Your vendor sends you a single text block starting with PGKEY1. Paste it here and click Apply — that is the entire process.",
                body));
            KeyPasteTextBox = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 110,
                MaxHeight = 200,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = body,
                Margin = new Thickness(0, 0, 0, 12)
            };
            c1.Children.Add(KeyPasteTextBox);

            var row1 = new StackPanel { Orientation = Orientation.Horizontal };
            var apply = MakeButton("APPLY LICENSE KEY", primary: true);
            apply.Click += KeyApplyButton_Click;
            row1.Children.Add(apply);
            var browse = MakeButton("OPEN KEY FILE…", primary: false);
            browse.Click += KeyBrowseButton_Click;
            browse.Margin = new Thickness(10, 0, 0, 0);
            row1.Children.Add(browse);
            c1.Children.Add(row1);

            card1.Child = c1;
            stack.Children.Add(card1);

            // ── Card 2 · PLANS — GET A LICENSE KEY ────────────────────
            var card2 = MakeCard(corner);
            var c2 = new StackPanel();
            c2.Children.Add(CardLabel("PLANS — GET A LICENSE KEY"));
            c2.Children.Add(Para(
                "Choose the plan you want, then press GET LICENSE KEY. It copies a ready-to-send request with this PC's ID — send it to your vendor, and they reply with a PGKEY1 key to paste in the box above.",
                body));

            var wrap = new WrapPanel();
            Plan7Radio = MakePlanCard("7 DAYS", "Short evaluation");
            Plan30Radio = MakePlanCard("30 DAYS", "Monthly key");
            Plan6mRadio = MakePlanCard("6 MONTHS", "Half-year key");
            Plan1yRadio = MakePlanCard("1 YEAR", "Annual key");
            Plan2yRadio = MakePlanCard("2 YEARS", "Two-year key");
            foreach (var radio in new[] { Plan7Radio, Plan30Radio, Plan6mRadio, Plan1yRadio, Plan2yRadio })
            {
                radio.GroupName = "CustomerPlan";
                wrap.Children.Add(radio);
            }
            Plan1yRadio.IsChecked = true;
            c2.Children.Add(wrap);

            var getKey = MakeButton("GET LICENSE KEY", primary: true);
            getKey.Click += GetLicenseKeyButton_Click;
            getKey.HorizontalAlignment = HorizontalAlignment.Left;
            getKey.Margin = new Thickness(0, 4, 0, 0);
            c2.Children.Add(getKey);

            card2.Child = c2;
            stack.Children.Add(card2);

            // ── Card 3 · CURRENT LICENSE STATE ────────────────────────
            var card3 = MakeCard(corner);
            var c3 = new StackPanel();
            c3.Children.Add(CardCaption("CURRENT LICENSE STATE"));

            StatusBadgeText = new TextBlock
            {
                Text = "Trial",
                FontSize = section,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            StatusBadgeText.SetResourceReference(TextElement.ForegroundProperty, "AccentBrush");
            c3.Children.Add(StatusBadgeText);

            StatusDetailText = new TextBlock
            {
                FontSize = body,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            StatusDetailText.SetResourceReference(TextElement.ForegroundProperty, "SecondaryForegroundBrush");
            c3.Children.Add(StatusDetailText);

            LicenseDetailsGrid = new Grid
            {
                Margin = new Thickness(0, 12, 0, 0),
                Visibility = Visibility.Collapsed
            };
            LicenseDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            LicenseDetailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 5; i++)
                LicenseDetailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            EditionValue = new TextBlock();
            CustomerValue = new TextBlock();
            ExpiresValue = new TextBlock();
            LicenseIdValue = new TextBlock { TextWrapping = TextWrapping.Wrap };
            SourceValue = new TextBlock { TextWrapping = TextWrapping.Wrap };
            string[] detailLabels = { "Edition", "Customer", "Expires", "License ID", "Source" };
            TextBlock[] detailValues = { EditionValue, CustomerValue, ExpiresValue, LicenseIdValue, SourceValue };
            for (int i = 0; i < 5; i++)
            {
                var lab = new TextBlock
                {
                    Text = detailLabels[i],
                    FontSize = body,
                    Margin = new Thickness(0, i == 0 ? 0 : 4, 0, 0)
                };
                lab.SetResourceReference(TextElement.ForegroundProperty, "SecondaryForegroundBrush");
                Grid.SetRow(lab, i);
                Grid.SetColumn(lab, 0);

                detailValues[i].FontSize = body;
                detailValues[i].Margin = new Thickness(0, i == 0 ? 0 : 4, 0, 0);
                detailValues[i].SetResourceReference(TextElement.ForegroundProperty, "PrimaryForegroundBrush");
                Grid.SetRow(detailValues[i], i);
                Grid.SetColumn(detailValues[i], 1);

                LicenseDetailsGrid.Children.Add(lab);
                LicenseDetailsGrid.Children.Add(detailValues[i]);
            }
            c3.Children.Add(LicenseDetailsGrid);

            card3.Child = c3;
            stack.Children.Add(card3);

            // ── Card 4 · YOUR PC ID ───────────────────────────────────
            var card4 = MakeCard(corner);
            var c4 = new StackPanel();
            c4.Children.Add(CardCaption("YOUR PC ID"));
            c4.Children.Add(Para(
                "Send this to your vendor ONLY if they ask for it (a key bound to this machine). You normally do not need it.",
                body));

            FingerprintDisplayBox = new TextBox
            {
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas"),
                FontSize = body,
                TextWrapping = TextWrapping.Wrap
            };
            c4.Children.Add(FingerprintDisplayBox);

            var installCaption = new TextBlock
            {
                Text = "Installation ID",
                FontSize = caption,
                Margin = new Thickness(0, 10, 0, 0)
            };
            installCaption.SetResourceReference(TextElement.ForegroundProperty, "SecondaryForegroundBrush");
            c4.Children.Add(installCaption);

            InstallationIdBox = new TextBox
            {
                IsReadOnly = true,
                FontSize = body,
                Margin = new Thickness(0, 2, 0, 0)
            };
            c4.Children.Add(InstallationIdBox);

            card4.Child = c4;
            stack.Children.Add(card4);

            // ── Error / hint / close ──────────────────────────────────
            ErrorText = new TextBlock
            {
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = body,
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            ErrorText.SetResourceReference(TextElement.ForegroundProperty, "DangerBrush");
            stack.Children.Add(ErrorText);

            HintText = new TextBlock
            {
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = caption,
                TextWrapping = TextWrapping.Wrap
            };
            HintText.SetResourceReference(TextElement.ForegroundProperty, "SecondaryForegroundBrush");
            stack.Children.Add(HintText);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            CloseButton = MakeButton("CLOSE", primary: false);
            CloseButton.Click += CloseButton_Click;
            footer.Children.Add(CloseButton);
            stack.Children.Add(footer);

            scroll.Content = stack;
            root.Child = scroll;
            Content = root;

            ApplyClosable(IsClosable);
        }
    }
}
