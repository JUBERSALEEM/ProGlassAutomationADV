// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/MainWindow.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Developer-only tool — WPF main window code-behind
//
// Code-behind for MainWindow.xaml. Thin shims over the UI-free
// LicenseGeneratorCore. The tool's only job: generate a signing key
// once, and produce a copy/paste license KEY the vendor sends to the
// customer. The customer pastes it into Settings → License and it
// activates instantly — no file, no path.
//
// WORKFLOW
//   1. GENERATE KEY PAIR   — creates the RSA-2048 key pair (private
//      key stored on this machine, never shared).
//   2. INSTALL PUBLIC KEY INTO APP — stamps the public half into
//      Services/Licensing/LicensePublicKey.cs so the app verifies
//      keys produced by this tool. Do once, rebuild the app.
//   3. For each customer: edition, days, features, optional hardware
//      binding. Click GENERATE LICENSE KEY → the key string appears.
//      COPY KEY sends it. Save-as-.lic is optional.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ProGlassAutomation.Services.Licensing;

namespace ProGlassAutomation.Tools.LicenseGenerator
{
    /// <summary>
    /// Main WPF window for the LicenseGenerator tool.
    /// </summary>
    public partial class MainWindow : Window
    {
        private LicenseGeneratorCore.KeyStore? _keys;
        private string? _lastKey;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        // ── Window lifecycle ────────────────────────────────────

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _keys = LicenseGeneratorCore.LoadOrCreateKeyStore(out bool wasCreated);
                RefreshKeyUi();
                if (wasCreated)
                {
                    SetStatus(
                        "New key pair created. Click INSTALL PUBLIC KEY INTO APP, " +
                        "then rebuild the app so it verifies keys from this tool.",
                        StatusKind.Info);
                }
                else
                {
                    SetStatus("Signing key loaded. Ready to generate license keys.",
                        StatusKind.Info);
                }
            }
            catch (Exception ex)
            {
                SetStatus("Could not load the signing key: " + ex.Message,
                          StatusKind.Error);
            }
        }

        // ── Key pair ────────────────────────────────────────────

        private void GenerateKeyPairButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Generate a new RSA-2048 signing key pair?\n\n" +
                "The old private key (if any) is REPLACED. Any license signed " +
                "with the old key stops working once the app is rebuilt with " +
                "the new public key.\n\nBack up the key at:\n" +
                LicenseGeneratorCore.KeysFilePath,
                "Generate key pair",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                _keys = LicenseGeneratorCore.LoadOrCreateKeyStore(out bool wasCreated);
                if (!wasCreated)
                {
                    (string publicXml, string privateXml) = LicenseSigner.GenerateKeyPair();
                    _keys = new LicenseGeneratorCore.KeyStore
                    {
                        PublicKeyXml = publicXml,
                        PrivateKeyXml = privateXml,
                        CreatedUtc = DateTime.UtcNow
                    };
                    LicenseGeneratorCore.SaveKeyStore(_keys);
                }
                RefreshKeyUi();
                SetStatus("New key pair generated. Install the public key into " +
                          "the app, then rebuild.", StatusKind.Success);
            }
            catch (Exception ex)
            {
                SetStatus("Key generation failed: " + ex.Message, StatusKind.Error);
            }
        }

        private void InstallPublicKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_keys == null)
            {
                SetStatus("Generate a key pair first.", StatusKind.Error);
                return;
            }

            bool ok = LicenseGeneratorCore.WritePublicKeyToSource(
                _keys.PublicKeyXml, out string error);
            if (!ok)
            {
                SetStatus(error, StatusKind.Error);
                MessageBox.Show(this, error, "Install public key",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetStatus("Public key written to Services/Licensing/LicensePublicKey.cs. " +
                      "Rebuild the app.", StatusKind.Success);
            MessageBox.Show(
                "Public key installed into:\n\n" +
                "  Services/Licensing/LicensePublicKey.cs\n\n" +
                "It now matches the private key this tool signs with.\n\n" +
                "Rebuild the main app. Every key generated here will be accepted.",
                "Public key installed",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Generate license key ────────────────────────────────

        private void BindToHardware_Changed(object sender, RoutedEventArgs e)
        {
            HardwareRow.Visibility = BindToHardware.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (BindToHardware.IsChecked == true && string.IsNullOrWhiteSpace(FingerprintBox.Text))
            {
                try
                {
                    FingerprintBox.Text = LicenseGeneratorCore.CurrentFingerprint();
                }
                catch (System.Exception hwEx)
                {
                    // Best-effort: leave the field blank and let the user
                    // type a hardware ID from the customer's About page.
                    ProGlassAutomation.Services.Logging.AppLogService.Debug(
                        "[LicenseGenerator] Auto-fill hardware ID failed: " +
                        hwEx.Message);
                }
            }
        }

        private void UseThisPcButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string fp = LicenseGeneratorCore.CurrentFingerprint();
                FingerprintBox.Text = fp;
                SetStatus("Filled hardware ID from this PC: " + fp, StatusKind.Info);
            }
            catch (Exception ex)
            {
                SetStatus("Could not read this PC's hardware ID: " + ex.Message,
                          StatusKind.Error);
            }
        }

        private void GenerateLicenseKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_keys == null)
            {
                SetStatus("Generate a key pair first.", StatusKind.Error);
                return;
            }

            // KEY-MATCH GUARD: only generate if the signing key matches the
            // app's embedded public key — otherwise the app rejects it.
            if (!LicenseGeneratorCore.PublicKeyMatchesApp(_keys.PublicKeyXml))
            {
                string genFp = LicenseGeneratorCore.PublicKeyFingerprint(_keys.PublicKeyXml);
                string appFp = LicenseGeneratorCore.PublicKeyFingerprint(
                    ProGlassAutomation.Services.Licensing.LicensePublicKey.PublicKeyXml);
                string msg =
                    "The signing key does not match the public key embedded in the app.\n\n" +
                    "Signing key : " + genFp + "\n" +
                    "App key     : " + appFp + "\n\n" +
                    "Keys generated now will be REJECTED by the app.\n\n" +
                    "Click INSTALL PUBLIC KEY INTO APP (top card), rebuild the app, " +
                    "then generate again.";
                SetStatus(msg, StatusKind.Error);
                MessageBox.Show(this, msg, "Signing key mismatch",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var request = new LicenseGeneratorCore.IssueRequest
                {
                    Edition = ParseEdition(EditionCombo),
                    CustomerId = CustomerIdBox.Text ?? "",
                    CompanyName = CompanyNameBox.Text ?? "",
                    DaysValid = ParseInt(DaysBox.Text, 365, 1, 3650, "Valid days"),
                    MaxActivations = 1,
                    Features = FeaturesBox.Text?.Split(
                        new[] { '\n', ',' },
                        StringSplitOptions.RemoveEmptyEntries)
                        ?? Array.Empty<string>(),
                    MachineFingerprint = BindToHardware.IsChecked == true
                        ? (string.IsNullOrWhiteSpace(FingerprintBox.Text)
                            ? LicenseGeneratorCore.CurrentFingerprint()
                            : FingerprintBox.Text.Trim())
                        : null,
                    Notes = ""
                };

                string key = LicenseGeneratorCore.GenerateKey(request, _keys.PrivateKeyXml);
                _lastKey = key;
                KeyOutputBox.Text = LicenseGeneratorCore.FormatKey(key);
                OutputCard.Visibility = Visibility.Visible;

                SetStatus(
                    "Key generated for " +
                    (string.IsNullOrWhiteSpace(request.CompanyName)
                        ? request.CustomerId
                        : request.CompanyName) +
                    ". Copy and send it to the customer.", StatusKind.Success);
            }
            catch (Exception ex)
            {
                SetStatus("Key generation failed: " + ex.Message, StatusKind.Error);
                MessageBox.Show(this, ex.Message, "Generate key",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lastKey))
            {
                SetStatus("No key generated yet.", StatusKind.Warning);
                return;
            }
            try
            {
                // Copy the raw (un-grouped) key so paste is always clean.
                Clipboard.SetText(LicenseGeneratorCore.UnformatKey(_lastKey));
                SetStatus("License key copied to clipboard.", StatusKind.Success);
            }
            catch (Exception ex)
            {
                SetStatus("Copy failed: " + ex.Message, StatusKind.Error);
            }
        }

        private void SaveKeyFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lastKey))
            {
                SetStatus("No key generated yet.", StatusKind.Warning);
                return;
            }

            string defaultName = "license.lic";
            if (!string.IsNullOrWhiteSpace(CompanyNameBox.Text))
                defaultName = SanitiseFileName(CompanyNameBox.Text) + ".lic";

            var dialog = new SaveFileDialog
            {
                Title = "Save license key as a .lic file",
                Filter = "License files (*.lic)|*.lic|All files (*.*)|*.*",
                DefaultExt = ".lic",
                FileName = defaultName,
                OverwritePrompt = true,
                AddExtension = true
            };
            if (dialog.ShowDialog(this) == true)
            {
                try
                {
                    // The .lic file IS the decoded JSON envelope (the app's
                    // file path also accepts it). Write raw key JSON.
                    byte[] bytes = Convert.FromBase64String(
                        LicenseGeneratorCore.UnformatKey(_lastKey));
                    File.WriteAllText(dialog.FileName,
                        System.Text.Encoding.UTF8.GetString(bytes));
                    SetStatus("Saved to " + dialog.FileName, StatusKind.Success);
                }
                catch (Exception ex)
                {
                    SetStatus("Save failed: " + ex.Message, StatusKind.Error);
                }
            }
        }

        // ── UI plumbing ─────────────────────────────────────────

        private void RefreshKeyUi()
        {
            if (_keys == null)
            {
                KeyStatusText.Text = "No signing key.";
                KeyStatusDetail.Text =
                    "Click GENERATE KEY PAIR to create one. The private key is " +
                    "stored on this machine only.";
                KeyPairInfo.Visibility = Visibility.Collapsed;
                return;
            }

            KeyStatusText.Text = "Signing key ready.";
            KeyStatusDetail.Text =
                "Created " + _keys.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") +
                " · private key kept on this machine.";
            KeyFingerprintText.Text =
                "Key fingerprint: " + LicenseGeneratorCore.PublicKeyFingerprint(_keys.PublicKeyXml);
            KeyPairInfo.Visibility = Visibility.Visible;
        }

        // ── Status bar ──────────────────────────────────────────

        private enum StatusKind { Info, Success, Error, Warning }

        private void SetStatus(string message, StatusKind kind)
        {
            StatusText.Text = message;
            string brushKey = kind switch
            {
                StatusKind.Success => "SuccessBrush",
                StatusKind.Error => "DangerBrush",
                StatusKind.Warning => "WarningBrush",
                _ => "TextDimBrush"
            };
            if (FindResource(brushKey) is System.Windows.Media.Brush brush)
                StatusText.Foreground = brush;
            else
                StatusText.Foreground = System.Windows.Media.Brushes.Gray;
            StatusTimeText.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        // ── Pure helpers (no UI) ───────────────────────────────

        private static LicenseEdition ParseEdition(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && Enum.TryParse<LicenseEdition>(tag, out var parsed))
            {
                return parsed;
            }
            return LicenseEdition.Business;
        }

        private static int ParseInt(string text, int defaultValue,
            int min, int max, string fieldName)
        {
            if (!int.TryParse(text, out int v))
            {
                MessageBox.Show(fieldName + " must be a whole number; using " +
                    defaultValue + ".", "Invalid input",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return defaultValue;
            }
            if (v < min || v > max)
            {
                MessageBox.Show(fieldName + " must be between " + min + " and " +
                    max + "; using " + defaultValue + ".", "Invalid input",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return defaultValue;
            }
            return v;
        }

        private static string SanitiseFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }
}
