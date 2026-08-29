// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/MainWindow.xaml.cs
// PROJECT: ProGlassAutomation.LicenseGenerator
// MODULE: License Key Generator — window logic (2026 simple flow)
//
// ONE JOB: sign a customer license key with the operator's external
// issuer key and hand back a single PGKEY1 text block.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using ProGlass.Licensing;

namespace ProGlassAutomation.Tools.LicenseGenerator;

public partial class MainWindow : Window
{
    private IssuerKeyMaterial? _loadedKeyMaterial;
    private string? _lastLicenseKey;

    public MainWindow()
    {
        InitializeComponent();
        // SelectedIndex is set in code (not XAML) so the SelectionChanged
        // handler never fires before KeyPreviewText exists.
        KeyEditionCombo.SelectedIndex = 1; // Business
        ApplyKeyPreview();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshIssuerUi();
        SetStatus(
            "Ready — load your issuer key file, pick an edition, generate the key, send it to the customer.",
            StatusKind.Info);
    }

    // ────────────────────────────────────────────────────────────────────
    // ISSUER KEY — load / create / copy public half / self-test
    // ────────────────────────────────────────────────────────────────────

    private void LoadKeyButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load issuer key file",
            Filter = "Issuer key (*.pgkey;*.json)|*.pgkey;*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            _loadedKeyMaterial = IssuerKeyMaterial.LoadFromFile(dialog.FileName);
            KeyFilePathBox.Text = dialog.FileName;
            RefreshIssuerUi();
            SetStatus("Issuer key loaded.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus("Key load failed: " + ex.Message, StatusKind.Error);
            MessageBox.Show(this, ex.Message, "Load issuer key", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateKeyButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Create issuer key file",
            Filter = "Issuer key (*.pgkey)|*.pgkey|JSON (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".pgkey",
            FileName = "issuer-key.pgkey",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            _loadedKeyMaterial = IssuerKeyMaterial.Generate(
                KeyIdBox.Text?.Trim() ?? string.Empty,
                "Production");
            _loadedKeyMaterial.SaveToFile(dialog.FileName);
            KeyFilePathBox.Text = dialog.FileName;
            RefreshIssuerUi();
            SetStatus(
                "New issuer key file created. The app will trust it only after its public key is embedded in a released build.",
                StatusKind.Warning);
        }
        catch (Exception ex)
        {
            SetStatus("Key creation failed: " + ex.Message, StatusKind.Error);
            MessageBox.Show(this, ex.Message, "Create issuer key", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CopyPublicKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_loadedKeyMaterial == null)
        {
            SetStatus("Load an issuer key file first.", StatusKind.Warning);
            return;
        }

        try
        {
            Clipboard.SetText(_loadedKeyMaterial.PublicKeyXml);
            SetStatus("Loaded public key copied to clipboard.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus("Copy failed: " + ex.Message, StatusKind.Error);
        }
    }

    private void SelfTestButton_Click(object sender, RoutedEventArgs e)
    {
        if (_loadedKeyMaterial == null)
        {
            SetStatus("Load an issuer key file before running the self-test.", StatusKind.Warning);
            return;
        }

        var test = LicenseGeneratorCore.RunSelfTest(_loadedKeyMaterial);
        SetStatus(test.Message, test.IsValid ? StatusKind.Success : StatusKind.Error);
        MessageBox.Show(this, test.Message,
            test.IsValid ? "Generator self-test" : "Generator self-test failed",
            MessageBoxButton.OK,
            test.IsValid ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    // ────────────────────────────────────────────────────────────────────
    // LICENSE KEY — generate / copy / save
    // ────────────────────────────────────────────────────────────────────

    private string SelectedKeyEdition() =>
        SelectedComboText(KeyEditionCombo, "Business");

    private void ApplyKeyPreview()
    {
        string edition = SelectedKeyEdition();
        KeyPreviewText.Text = edition switch
        {
            "Trial" => "Trial — 30-day key, all features (\"*\").",
            "Basic" => "Basic — inquiry and proforma only.",
            "Business" => "Business — full ERP workflow, excluding Cutting.*.",
            "Factory" => "Factory — Business + Cutting.* workflow.",
            "Enterprise" => "Enterprise — unrestricted feature scope.",
            _ => "Business — full ERP workflow, excluding Cutting.*."
        };
    }

    private void KeyEditionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyKeyPreview();
        // Trial keys have a fixed 30-day window; keep the days box honest.
        if (string.Equals(SelectedKeyEdition(), "Trial", StringComparison.OrdinalIgnoreCase))
            KeyDaysBox.Text = "30";
    }

    private void GenerateLicenseKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_loadedKeyMaterial == null)
        {
            SetStatus("Load an issuer key file first.", StatusKind.Warning);
            return;
        }

        try
        {
            int days = ParseInt(KeyDaysBox.Text, 365, 0, 3650);
            string edition = SelectedKeyEdition();

            var result = LicenseGeneratorCore.GenerateLicenseKey(
                new LicenseGeneratorCore.LicenseKeyIssueRequest
                {
                    Edition = edition,
                    CustomerId = (KeyCustomerIdBox.Text ?? string.Empty).Trim(),
                    CompanyName = (KeyCompanyBox.Text ?? string.Empty).Trim(),
                    DaysValid = days,
                    DeviceFingerprint = string.IsNullOrWhiteSpace(KeyFingerprintBox.Text)
                        ? null
                        : KeyFingerprintBox.Text.Trim(),
                    Features = LicenseGeneratorCore.DefaultFeatures(edition).ToList()
                },
                _loadedKeyMaterial);

            if (!result.Success)
            {
                LicenseKeyOutputBox.Text = string.Empty;
                _lastLicenseKey = null;
                SetStatus(result.Message, StatusKind.Error);
                return;
            }

            LicenseKeyOutputBox.Text = result.ArmoredKey;
            _lastLicenseKey = result.ArmoredKey;
            SetStatus(result.Message + " Send the PGKEY1 block to the customer.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus("License key generation failed: " + ex.Message, StatusKind.Error);
        }
    }

    private void CopyLicenseKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastLicenseKey))
        {
            SetStatus("Generate a license key first.", StatusKind.Warning);
            return;
        }

        try
        {
            Clipboard.SetText(_lastLicenseKey);
            SetStatus("License key copied to clipboard — send it to the customer.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus("Copy failed: " + ex.Message, StatusKind.Error);
        }
    }

    private void SaveLicenseKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastLicenseKey))
        {
            SetStatus("Generate a license key first.", StatusKind.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save license key",
            Filter = "License key (*.lic)|*.lic|Text (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".lic",
            AddExtension = true,
            InitialDirectory = LicenseGeneratorCore.DefaultOutputFolder,
            FileName = "license-key-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".lic"
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            File.WriteAllText(dialog.FileName, _lastLicenseKey, new UTF8Encoding(false));
            SetStatus("License key saved to " + dialog.FileName, StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus("Save failed: " + ex.Message, StatusKind.Error);
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────

    private void RefreshIssuerUi()
    {
        if (_loadedKeyMaterial == null)
        {
            KeyStatusText.Text = "No issuer key loaded.";
            return;
        }

        bool trusted = AppCompatibleIssuerKeys.IsKnownKey(_loadedKeyMaterial.KeyId, _loadedKeyMaterial.PublicKeyXml);
        KeyStatusText.Text = trusted
            ? "App-trusted issuer loaded · keyId " + _loadedKeyMaterial.KeyId + " · fingerprint " + _loadedKeyMaterial.Fingerprint
            : "External issuer loaded · NOT currently trusted by this app build · keyId " + _loadedKeyMaterial.KeyId;
    }

    private static string SelectedComboText(ComboBox comboBox, string fallback)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
               ?? comboBox.Text
               ?? fallback;
    }

    private enum StatusKind { Info, Success, Error, Warning }

    private void SetStatus(string message, StatusKind kind)
    {
        StatusText.Text = message;
        string key = kind switch
        {
            StatusKind.Success => "SuccessBrush",
            StatusKind.Error => "DangerBrush",
            StatusKind.Warning => "WarningBrush",
            _ => "TextDimBrush"
        };
        StatusText.Foreground = ResolveBrush(key);
        StatusTimeText.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    private Brush ResolveBrush(string key)
    {
        return TryFindResource(key) as Brush
            ?? Brushes.DimGray;
    }

    private static int ParseInt(string? text, int def, int min, int max)
    {
        if (int.TryParse(text, out int v) && v >= min && v <= max) return v;
        return def;
    }
}
