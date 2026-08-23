// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/MainWindow.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Developer-only tool — WPF main window code-behind
//
// Code-behind for MainWindow.xaml. Every click handler is
// a thin shim that builds an IssueRequest (or modifies
// UI state) and calls the UI-free service in
// LicenseGeneratorCore. The window holds no business
// logic — the service is the single source of truth.
//
// WORKFLOW
//   1. Window opens; on Loaded, LoadOrCreateKeyStore runs.
//      If no key pair exists, the user is prompted to
//      generate one (the button is also visible and
//      always clickable, so this is just a hint).
//   2. The Public-key card shows the current public key
//      with copy + reveal + open-folder actions.
//   3. The Sign-a-license card has a form. The user fills
//      it in and clicks SIGN LICENSE & SAVE. A SaveFileDialog
//      lets them pick the exact path; otherwise the
//      default %USERPROFILE%\Documents\ProGlassAutomation\
//      Licenses\<company>-<date>.lic is used.
//   4. Every action updates the status bar at the bottom
//      with a one-line result + a timestamp.
//
// NEVER THROWS
//   Click handlers never let an exception escape. A
//   failing operation shows a MessageBox and the status
//   bar turns red. The App.xaml.cs
//   DispatcherUnhandledException is the last line of
//   defence for the truly unexpected.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;          // ComboBox, TextBox, Expander
using Microsoft.Win32;
using ProGlassAutomation.Services.Licensing;

namespace ProGlassAutomation.Tools.LicenseGenerator
{
    /// <summary>
    /// Main WPF window for the LicenseGenerator tool.
    /// </summary>
    public partial class MainWindow : Window
    {
        // The single in-memory key store. The file
        // version is the source of truth; this is a
        // cached copy that survives the window's
        // lifetime.
        private LicenseGeneratorCore.KeyStore? _keys;

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
                if (wasCreated)
                {
                    SetStatus(
                        "A new key pair was generated. " +
                        "Click REVEAL FULL XML below and paste the " +
                        "public key into Services/Licensing/LicensePublicKey.cs.",
                        StatusKind.Success);
                }
                else
                {
                    SetStatus("Key pair loaded from " +
                              LicenseGeneratorCore.KeysFilePath,
                              StatusKind.Info);
                }
                RefreshKeyUi();
                UpdateDefaultOutputPath();
            }
            catch (Exception ex)
            {
                SetStatus("Could not load or create the key store: " + ex.Message,
                          StatusKind.Error);
            }
        }

        // ── Key pair card ───────────────────────────────────────

        private void GenerateKeyButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Generate a brand-new RSA-2048 key pair?\n\n" +
                "The old private key (if any) will be REPLACED. " +
                "Any license signed with the old key will stop " +
                "working as soon as the main app is rebuilt with " +
                "the new public key. Existing licenses keep " +
                "working on old builds.\n\n" +
                "Backup the file at:\n" +
                LicenseGeneratorCore.KeysFilePath,
                "Generate new key pair",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                _keys = LicenseGeneratorCore.LoadOrCreateKeyStore(out bool wasCreated);
                // wasCreated is true iff no previous file
                // existed; if a file did exist, this code
                // path is reached after the user confirmed
                // overwrite — but LoadOrCreateKeyStore does
                // not overwrite. Do it explicitly here.
                if (!wasCreated)
                {
                    (string publicXml, string privateXml) =
                        LicenseSigner.GenerateKeyPair();
                    _keys = new LicenseGeneratorCore.KeyStore
                    {
                        PublicKeyXml = publicXml,
                        PrivateKeyXml = privateXml,
                        CreatedUtc = DateTime.UtcNow
                    };
                    LicenseGeneratorCore.SaveKeyStore(_keys);
                }

                RefreshKeyUi();
                SetStatus(
                    "New key pair generated and saved. " +
                    "Reveal the public key below and paste it into " +
                    "Services/Licensing/LicensePublicKey.cs, " +
                    "then rebuild the main app.",
                    StatusKind.Success);
            }
            catch (Exception ex)
            {
                SetStatus("Key generation failed: " + ex.Message,
                          StatusKind.Error);
            }
        }

        private void CopyPublicKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_keys == null) return;
            try
            {
                Clipboard.SetText(
                    LicenseGeneratorCore.FormatPublicKeyForApp(_keys.PublicKeyXml));
                SetStatus("Public key copied to clipboard.", StatusKind.Success);
            }
            catch (Exception ex)
            {
                SetStatus("Copy failed: " + ex.Message, StatusKind.Error);
            }
        }

        private void RevealPublicKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_keys == null) return;
            // The expander's IsExpanded is the source of
            // truth. The reveal button is a convenience
            // that opens it AND scrolls the public-key
            // text into view.
            PublicKeyExpander.IsExpanded = true;
            PublicKeyTextBox.Focus();
            PublicKeyTextBox.SelectAll();
            // Bring the expander into view. The parent
            // ScrollViewer handles the rest.
            PublicKeyExpander.BringIntoView();
        }

        private void OpenKeysFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = Path.GetDirectoryName(
                    LicenseGeneratorCore.KeysFilePath)!;
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
                SetStatus("Opened keys folder: " + folder, StatusKind.Info);
            }
            catch (Exception ex)
            {
                SetStatus("Could not open keys folder: " + ex.Message,
                          StatusKind.Error);
            }
        }

        // ── Sign-a-license card ─────────────────────────────────

        private void UseThisMachineButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string fp = LicenseGeneratorCore.CurrentFingerprint();
                FingerprintBox.Text = fp;
                SetStatus("Filled fingerprint from this machine: " + fp,
                          StatusKind.Info);
            }
            catch (Exception ex)
            {
                SetStatus("Could not read fingerprint: " + ex.Message,
                          StatusKind.Error);
            }
        }

        private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
        {
            string defaultName = "License.lic";
            if (!string.IsNullOrWhiteSpace(CompanyNameBox.Text))
                defaultName = SanitiseFileName(CompanyNameBox.Text) + ".lic";

            var dialog = new SaveFileDialog
            {
                Title = "Save signed license as…",
                Filter = "License files (*.lic)|*.lic|All files (*.*)|*.*",
                DefaultExt = ".lic",
                FileName = defaultName,
                InitialDirectory = LicenseGeneratorCore.DefaultOutputFolder,
                OverwritePrompt = true,
                AddExtension = true
            };
            if (!Directory.Exists(dialog.InitialDirectory))
            {
                Directory.CreateDirectory(dialog.InitialDirectory);
            }
            if (dialog.ShowDialog(this) == true)
            {
                OutputPathBox.Text = dialog.FileName;
                SetStatus("Output path set to " + dialog.FileName,
                          StatusKind.Info);
            }
        }

        private void SignButton_Click(object sender, RoutedEventArgs e)
        {
            if (_keys == null)
            {
                SetStatus("No signing key. Generate one first.",
                          StatusKind.Error);
                return;
            }

            var request = new LicenseGeneratorCore.IssueRequest
            {
                Edition = ParseEdition(EditionCombo),
                CustomerId = CustomerIdBox.Text ?? "",
                CompanyName = CompanyNameBox.Text ?? "",
                DaysValid = ParseInt(DaysBox.Text, defaultValue: 365,
                    min: 1, max: 365 * 10, fieldName: "Days valid"),
                MaxActivations = ParseInt(MaxActivationsBox.Text,
                    defaultValue: 1, min: 1, max: 1000,
                    fieldName: "Max activations"),
                Features = FeaturesBox.Text?.Split(
                    new[] { '\n', ',' },
                    StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>(),
                MachineFingerprint = string.IsNullOrWhiteSpace(FingerprintBox.Text)
                    ? null
                    : FingerprintBox.Text.Trim(),
                Notes = NotesBox.Text ?? ""
            };

            // Default the output path if the user did not
            // click BROWSE.
            string outputPath = string.IsNullOrWhiteSpace(OutputPathBox.Text)
                ? null!
                : OutputPathBox.Text;

            try
            {
                var result = LicenseGeneratorCore.IssueLicense(
                    request, _keys.PrivateKeyXml, outputPath);

                if (!result.Success)
                {
                    SetStatus(result.Message, StatusKind.Error);
                    MessageBox.Show(this, result.Message, "Sign failed",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                OutputPathBox.Text = result.OutputPath;
                SetStatus(
                    "✓ Signed. LicenseId=" + result.Payload!.LicenseId +
                    " • " + Path.GetFileName(result.OutputPath),
                    StatusKind.Success);
                RevealIssuedPayload(result);
            }
            catch (Exception ex)
            {
                SetStatus("Sign failed: " + ex.Message, StatusKind.Error);
                MessageBox.Show(this, ex.ToString(), "Sign crashed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetFormButton_Click(object sender, RoutedEventArgs e)
        {
            EditionCombo.SelectedIndex = 2; // Business
            CustomerIdBox.Text = "CUST-2026-001";
            CompanyNameBox.Text = "Acme Glass LLC";
            DaysBox.Text = "365";
            MaxActivationsBox.Text = "1";
            FeaturesBox.Text = "Inquiry.*, Proforma.*, JobOrder.*, " +
                               "DeliveryNote.*, TaxInvoice.*, Statement.*, " +
                               "MasterData.*, SheetStore.*";
            FingerprintBox.Text = "";
            NotesBox.Text = "";
            UpdateDefaultOutputPath();
            SetStatus("Form reset.", StatusKind.Info);
        }

        // ── UI plumbing ────────────────────────────────────────

        private void RefreshKeyUi()
        {
            if (_keys == null)
            {
                KeyStatusText.Text = "No key pair found.";
                KeyStatusDetail.Text =
                    "Click GENERATE to create a fresh RSA-2048 key pair. " +
                    "The private key is saved to " +
                    LicenseGeneratorCore.KeysFilePath + ".";
                PublicKeyTextBox.Text = "";
                return;
            }

            KeyStatusText.Text = "Key pair ready.";
            KeyStatusDetail.Text =
                "Created " + _keys.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") +
                "  •  private key at " + LicenseGeneratorCore.KeysFilePath +
                "  •  public key length: " + _keys.PublicKeyXml.Length + " chars";

            // Compact preview in the text box. The full
            // XML is in the keys file; the user can copy
            // it via COPY TO CLIPBOARD.
            string xml = _keys.PublicKeyXml;
            if (xml.Length > 600)
            {
                xml = xml.Substring(0, 600) + "\n…(truncated; click COPY TO CLIPBOARD for the full string)";
            }
            PublicKeyTextBox.Text = xml;
        }

        private void UpdateDefaultOutputPath()
        {
            string company = string.IsNullOrWhiteSpace(CompanyNameBox.Text)
                ? "(unnamed)" : CompanyNameBox.Text.Trim();
            string fileName = SanitiseFileName(company) + "-" +
                              DateTime.UtcNow.ToString("yyyy-MM-dd") + ".lic";
            OutputPathBox.Text = Path.Combine(
                LicenseGeneratorCore.DefaultOutputFolder, fileName);
        }

        private void RevealIssuedPayload(LicenseGeneratorCore.IssueResult result)
        {
            // Open Explorer with the produced .lic
            // selected, so the developer can verify
            // the file landed in the right place.
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{result.OutputPath}\"",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Non-fatal: the status bar already tells
                // the user where the file is.
            }
        }

        // ── Status bar ──────────────────────────────────────────

        private enum StatusKind { Info, Success, Error }

        private void SetStatus(string message, StatusKind kind)
        {
            StatusText.Text = message;
            // FindResource returns the brush instance itself
            // (the resource is declared as SolidColorBrush,
            // not as a raw Color), so we cast to SolidColorBrush
            // and reuse the existing instance. Creating a new
            // SolidColorBrush from .Color would also work but
            // is wasteful and loses the frozen state.
            string brushKey = kind switch
            {
                StatusKind.Success => "SuccessBrush",
                StatusKind.Error => "DangerBrush",
                _ => "TextDimBrush"
            };
            if (FindResource(brushKey) is System.Windows.Media.Brush brush)
            {
                StatusText.Foreground = brush;
            }
            else
            {
                // Defensive: never let a status-bar update
                // throw. Fall back to a default brush.
                StatusText.Foreground = System.Windows.Media.Brushes.Gray;
            }
            StatusTimeText.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        // ── Pure helpers (no UI) ───────────────────────────────

        private static LicenseEdition ParseEdition(ComboBox combo)
        {
            // The ComboBoxItem.Tag carries the enum name;
            // falling back to the text content is a
            // safety net for hand-edited XAML.
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
                MessageBox.Show(
                    fieldName + " must be a whole number; using " +
                    defaultValue + ".",
                    "Invalid input",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return defaultValue;
            }
            if (v < min || v > max)
            {
                MessageBox.Show(
                    fieldName + " must be between " + min + " and " + max +
                    "; using " + defaultValue + ".",
                    "Invalid input",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
