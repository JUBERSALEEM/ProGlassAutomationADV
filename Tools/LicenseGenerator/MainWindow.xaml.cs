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
    private OfflineActivationRequestDocument? _loadedRequest;
    private string? _lastResponseJson;

    public MainWindow()
    {
        InitializeComponent();
        LicenseTypeCombo.SelectedIndex = 2; // Business
        LicenseModeCombo.SelectedIndex = 0; // Subscription
        ApplyEditionPreset();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshIssuerUi();
        SetStatus(
            "Ready — load an issuer key file, import a customer activation request, then generate a signed offline response.",
            StatusKind.Info);
    }

    private void LicenseTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyEditionPreset();
    }

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

    private void LoadRequestButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load customer activation request",
            Filter = "Activation request (*.pgrequest;*.json)|*.pgrequest;*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            string text = System.IO.File.ReadAllText(dialog.FileName);
            ActivationRequestBox.Text = text;
            ParseActivationRequestText();
            SetStatus("Activation request loaded.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus("Request load failed: " + ex.Message, StatusKind.Error);
            MessageBox.Show(this, ex.Message, "Load activation request", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ParseRequestButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ParseActivationRequestText();
            SetStatus("Activation request parsed.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus("Request parse failed: " + ex.Message, StatusKind.Error);
            MessageBox.Show(this, ex.Message, "Parse activation request", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_loadedKeyMaterial == null)
        {
            SetStatus("Load an issuer key file first.", StatusKind.Warning);
            return;
        }

        try
        {
            ParseActivationRequestText();
            var request = BuildIssueRequest();
            string defaultPath = System.IO.Path.Combine(
                LicenseGeneratorCore.DefaultOutputFolder,
                Sanitise(request.CompanyName) + "-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + LicenseGeneratorCore.ActivationResponseExtension);

            var result = LicenseGeneratorCore.GenerateActivationResponse(request, _loadedKeyMaterial, defaultPath);
            OutputCard.Visibility = Visibility.Visible;
            KeyOutputBox.Text = result.ResponseJson ?? string.Empty;
            _lastResponseJson = result.ResponseJson;

            if (!result.Success)
            {
                ResultText.Text = "FAILED: " + result.Message;
                ResultText.Foreground = ResolveBrush("DangerBrush");
                SetStatus(result.Message, StatusKind.Error);
                return;
            }

            ResultText.Text = result.Message + "\nSaved to: " + result.OutputPath;
            ResultText.Foreground = ResolveBrush("SuccessBrush");
            SetStatus("Signed offline activation response generated.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus("Generation failed: " + ex.Message, StatusKind.Error);
            MessageBox.Show(this, ex.Message, "Generate response", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastResponseJson))
        {
            SetStatus("Generate or inspect a response first.", StatusKind.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save signed activation response",
            Filter = "ProGlass activation response (*.lic)|*.lic|JSON (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".lic",
            FileName = "CustomerActivation.lic",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            System.IO.File.WriteAllText(dialog.FileName, _lastResponseJson, new UTF8Encoding(false));
            SetStatus("Saved to " + dialog.FileName, StatusKind.Success);
        }
    }

    private void InspectButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Inspect a signed activation response",
            Filter = "Activation response (*.lic;*.json)|*.lic;*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;

        var verdict = LicenseGeneratorCore.InspectResponseFile(
            dialog.FileName,
            AppCompatibleIssuerKeys.TrustedPublicKeys);
        OutputCard.Visibility = Visibility.Visible;
        ResultText.Foreground = ResolveBrush(verdict.IsValid ? "SuccessBrush" : "DangerBrush");
        ResultText.Text = verdict.Message;
        KeyOutputBox.Text = verdict.ResponseJson;
        _lastResponseJson = verdict.ResponseJson;
        SetStatus(verdict.IsValid ? "Inspection complete — response matches an app-trusted key." : "Inspection complete — response is not trusted/valid.",
            verdict.IsValid ? StatusKind.Info : StatusKind.Warning);
    }

    private void CopyLicenseButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastResponseJson))
        {
            SetStatus("No signed response is available yet.", StatusKind.Warning);
            return;
        }

        try
        {
            Clipboard.SetText(_lastResponseJson);
            SetStatus("Response JSON copied to clipboard.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus("Copy failed: " + ex.Message, StatusKind.Error);
        }
    }

    private void ParseActivationRequestText()
    {
        var parsed = LicenseGeneratorCore.ReadActivationRequest(ActivationRequestBox.Text ?? string.Empty);
        if (!parsed.Success || parsed.Request == null)
            throw new InvalidOperationException(parsed.Message ?? "Activation request is invalid.");

        _loadedRequest = parsed.Request;
        RequestDetailText.Text =
            "Loaded request · License ID " + parsed.Request.LicenseId +
            " · Install " + parsed.Request.InstallationId +
            " · Fingerprint " + parsed.Request.MachineFingerprint +
            " · Nonce " + parsed.Request.RequestNonce;
    }

    private LicenseGeneratorCore.IssueRequest BuildIssueRequest()
    {
        if (_loadedRequest == null)
            throw new InvalidOperationException("Load and parse a customer activation request first.");

        return new LicenseGeneratorCore.IssueRequest
        {
            ActivationRequest = _loadedRequest,
            CustomerId = CustomerIdBox.Text?.Trim() ?? string.Empty,
            CompanyName = CustomerNameBox.Text?.Trim() ?? string.Empty,
            Edition = SelectedComboText(LicenseTypeCombo, "Business"),
            LicenseType = SelectedComboText(LicenseModeCombo, "Subscription"),
            LicenseStatus = "Active",
            ValidDays = ParseInt(DaysBox.Text, 365, 1, 3650),
            GraceDays = ParseInt(GraceDaysBox.Text, 7, 0, 90),
            ReadOnlyOnExpiry = ReadOnlyOnExpiryCheckBox.IsChecked == true,
            Features = ParseFeatures(FeaturesBox.Text),
            Notes = NotesBox.Text?.Trim() ?? string.Empty
        };
    }

    private void ApplyEditionPreset()
    {
        string edition = SelectedComboText(LicenseTypeCombo, "Business");
        FeaturesBox.Text = string.Join(", ", LicenseGeneratorCore.DefaultFeatures(edition));

        PreviewText.Text = edition switch
        {
            "Trial" => "Trial — vendor-issued offline trial response. No client self-signing.",
            "Basic" => "Basic — inquiry and proforma only.",
            "Business" => "Business — full ERP workflow, excluding Cutting.*.",
            "Factory" => "Factory — Business + Cutting.* workflow.",
            "Enterprise" => "Enterprise — unrestricted feature scope.",
            _ => "Edition preset ready."
        };
    }

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

    private static string[] ParseFeatures(string? text)
    {
        return (text ?? string.Empty)
            .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
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

    private static string Sanitise(string? name)
    {
        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        return new string((string.IsNullOrWhiteSpace(name) ? "Activation" : name.Trim())
            .Select(c => invalid.Contains(c) ? '_' : c)
            .ToArray());
    }
}
