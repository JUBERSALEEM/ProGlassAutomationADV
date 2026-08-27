// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/MainWindow.xaml.cs
// PROJECT: ProGlassAutomation.LicenseGenerator
// MODULE: Generator window code-behind
//
// Thin shims over LicenseGeneratorCore / ProGlass.Licensing. The window
// has no business logic and no embedded crypto. It drives the standalone
// contract library, and it NEVER references ProGlassAutomation.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ProGlass.Licensing;

namespace ProGlassAutomation.Tools.LicenseGenerator;

public partial class MainWindow : Window
{
    private LicenseGeneratorCore.KeyStore? _keys;
    private string? _lastLicenseJson;

    public MainWindow()
    {
        InitializeComponent();
        LicenseTypeCombo.SelectedIndex = 1;
        RefreshDeviceRow();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _keys = LicenseGeneratorCore.LoadOrCreateKeyStore(out bool wasCreated);
            RefreshKeyUi();
            SetStatus(wasCreated
                ? "New signing key generated. Copy the public key into the app so it can verify licenses from this tool."
                : "Signing key loaded. Ready to generate licenses.", StatusKind.Info);
        }
        catch (Exception ex)
        {
            SetStatus("Could not load signing key: " + ex.Message, StatusKind.Error);
        }
    }

    // ── Device policy visibility ────────────────────────────────

    private void UnboundRadio_Checked(object sender, RoutedEventArgs e) => RefreshDeviceRow();
    private void SingleRadio_Checked(object sender, RoutedEventArgs e) => RefreshDeviceRow();

    private void RefreshDeviceRow()
    {
        bool bound = SingleRadio.IsChecked == true || MultiRadio.IsChecked == true;
        FingerprintRow.Visibility = bound ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Signing key ─────────────────────────────────────────────

    private void GenerateKeyButton_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Generate a new RSA-2048 signing key?\n\n" +
            "The old key is replaced. Any license already issued with the old " +
            "key keeps working only while the app still trusts that key.\n\n" +
            "The private key is stored DPAPI-protected on this machine.",
            "Generate key", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            _keys = LicenseGeneratorCore.GenerateNewKeyPair();
            RefreshKeyUi();
            SetStatus("New signing key generated. Copy the public key into the app.",
                StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus("Key generation failed: " + ex.Message, StatusKind.Error);
        }
    }

    private void CopyPublicKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_keys == null) { SetStatus("No signing key.", StatusKind.Error); return; }
        try
        {
            Clipboard.SetText(_keys.PublicKeyXml);
            SetStatus("Public key copied to clipboard. Paste it into the app's trusted-keys configuration.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus("Copy failed: " + ex.Message, StatusKind.Error);
        }
    }

    // ── Generate ────────────────────────────────────────────────

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_keys == null) { SetStatus("Generate a signing key first.", StatusKind.Error); return; }

        try
        {
            var req = BuildRequest();
            string defaultPath = Path.Combine(
                LicenseGeneratorCore.DefaultOutputFolder,
                Sanitise(req.CustomerName) + "-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + LicenseFormat.FileExtension);

            var result = LicenseGeneratorCore.GenerateLicense(req, _keys, defaultPath);
            LicenseGeneratorCore.RecordHistory(result);

            if (!result.Success)
            {
                OutputCard.Visibility = Visibility.Visible;
                ResultText.Text = "FAILED: " + result.Message;
                ResultText.Foreground = ResolveBrush("DangerBrush");
                KeyOutputBox.Text = "";
                SetStatus("License generation FAILED: " + result.Message, StatusKind.Error);
                return;
            }

            // Always re-read + verify the written file (self-verification).
            string file = string.IsNullOrWhiteSpace(result.OutputPath)
                ? defaultPath
                : result.OutputPath;
            string json = File.ReadAllText(file);
            _lastLicenseJson = json;

            var verify = LicenseGeneratorCore.Inspect(json, appVersion: "1.0.0",
                trustedKeys: new Dictionary<string, string> { [_keys.KeyId] = _keys.PublicKeyXml });

            OutputCard.Visibility = Visibility.Visible;
            KeyOutputBox.Text = LicenseWriter.ToJson(verify.License ?? new SignedLicense());
            if (verify.IsValid)
                ResultText.Text = "VALID ✓  LicenseId=" + result.Document!.LicenseId +
                    " · expires=" + (result.Document.ExpiresAtUtc?.ToString("u") ?? "never") +
                    " · key=" + result.KeyId;
            else
            {
                ResultText.Text = "SELF-VERIFY FAILED: " + verify.Message;
                ResultText.Foreground = ResolveBrush("DangerBrush");
            }
            ResultText.Foreground = ResolveBrush("SuccessBrush");
            SetStatus("License generated + verified. Send the " + LicenseFormat.FileExtension + " file to the customer.",
                StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus("Generation failed: " + ex.Message, StatusKind.Error);
            MessageBox.Show(this, ex.Message, "Generate license", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save license",
            Filter = "ProGlass license (*" + LicenseFormat.FileExtension + ")|*" + LicenseFormat.FileExtension + "|All files (*.*)|*.*",
            DefaultExt = LicenseFormat.FileExtension,
            FileName = "CustomerLicense" + LicenseFormat.FileExtension,
            AddExtension = true
        };
        if (dialog.ShowDialog(this) == true && _lastLicenseJson != null)
        {
            File.WriteAllText(dialog.FileName, _lastLicenseJson);
            SetStatus("Saved to " + dialog.FileName, StatusKind.Success);
        }
    }

    private void InspectButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Inspect a license",
            Filter = "ProGlass license (*" + LicenseFormat.FileExtension + ")|*" + LicenseFormat.FileExtension + "|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true || _keys == null) return;

        var read = LicenseReader.ReadFile(dialog.FileName);
        var verdict = LicenseService.VerifyAndValidate(
            read, appVersion: "1.0.0", utcNow: DateTime.UtcNow,
            trustedPublicKeys: new Dictionary<string, string> { [_keys.KeyId] = _keys.PublicKeyXml });
        OutputCard.Visibility = Visibility.Visible;
        ResultText.Foreground = ResolveBrush(verdict.IsValid ? "SuccessBrush" : "DangerBrush");
        ResultText.Text = (verdict.IsValid ? "VALID" : "INVALID") + " — " + verdict.Message;
        KeyOutputBox.Text = read.License != null ? LicenseWriter.ToJson(read.License) : "";
        SetStatus("Inspection complete.", StatusKind.Info);
    }

    private void CopyLicenseButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastLicenseJson)) { SetStatus("No license generated yet.", StatusKind.Warning); return; }
        try { Clipboard.SetText(_lastLicenseJson); SetStatus("License copied to clipboard.", StatusKind.Success); }
        catch (Exception ex) { SetStatus("Copy failed: " + ex.Message, StatusKind.Error); }
    }

    // ── Request building ────────────────────────────────────────

    private LicenseRequest BuildRequest()
    {
        string type = (LicenseTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Subscription";

        string device = "Unbound";
        if (SingleRadio.IsChecked == true) device = "SingleDevice";
        else if (MultiRadio.IsChecked == true) device = "MultiDevice";

        int days = ParseInt(DaysBox.Text, 365, 1, 3650);
        int? limit = device == "MultiDevice" ? ParseInt(DeviceLimitBox.Text, 1, 1, 1000) : null;

        return new LicenseRequest
        {
            CustomerId = CustomerIdBox.Text?.Trim() ?? "",
            CustomerName = CustomerNameBox.Text?.Trim() ?? "",
            Company = CompanyBox.Text?.Trim(),
            Email = EmailBox.Text?.Trim(),
            LicenseType = type,
            DaysValid = days,
            Features = (FeaturesBox.Text ?? "").Split(',')
                .Select(f => f.Trim()).Where(f => f.Length > 0).ToList(),
            DeviceBinding = device,
            DeviceFingerprint = device != "Unbound" ? FingerprintBox.Text?.Trim() : null,
            DeviceLimit = limit,
            MinimumApplicationVersion = string.IsNullOrWhiteSpace(MinVersionBox.Text) ? "1.0.0" : MinVersionBox.Text.Trim(),
            MaximumApplicationVersion = string.IsNullOrWhiteSpace(MaxVersionBox.Text) ? null : MaxVersionBox.Text.Trim()
        };
    }

    // ── UI plumbing ─────────────────────────────────────────────

    private void RefreshKeyUi()
    {
        if (_keys == null)
        {
            KeyStatusText.Text = "No signing key.";
            KeyDetailText.Text = "Click GENERATE KEY to create one. The private key stays on this machine.";
            return;
        }
        KeyStatusText.Text = "Key: " + (_keys.KeyId.Length > 8 ? _keys.KeyId[..8] : _keys.KeyId);
        KeyDetailText.Text =
            "Created " + _keys.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") +
            " · key id " + _keys.KeyId + " · private key DPAPI-protected.";
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

    private System.Windows.Media.Brush ResolveBrush(string key)
    {
        return TryFindResource(key) as System.Windows.Media.Brush
            ?? System.Windows.Media.Brushes.DimGray;
    }

    private static int ParseInt(string text, int def, int min, int max)
    {
        if (int.TryParse(text, out int v) && v >= min && v <= max) return v;
        return def;
    }

    private static string Sanitise(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string((string.IsNullOrEmpty(name) ? "License" : name)
            .Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
