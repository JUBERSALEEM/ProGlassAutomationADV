using Microsoft.Win32;
using ProGlassAutomation.Services.Licensing;
using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProGlassAutomation.Tools.LicenseGenerator;

public partial class MainWindow : Window
{
    private string? _lastLicenseJson;
    private string? _lastPasteKey;
    private bool _uiReady;

    public MainWindow()
    {
        InitializeComponent();
        _uiReady = true;
        LicenseTypeCombo.SelectedIndex = 2; // Business
        ApplyEditionPreset(force: true);
        RefreshDeviceRow();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshIssuerUi();
        SetStatus(
            "Ready — generates an app-compatible .lic and paste key for the current ProGlass activation screen.",
            StatusKind.Info);
    }

    private void LicenseTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady) return;
        ApplyEditionPreset(force: true);
        RefreshDeviceRow();
    }

    private void UnboundRadio_Checked(object sender, RoutedEventArgs e) => RefreshDeviceRow();
    private void SingleRadio_Checked(object sender, RoutedEventArgs e) => RefreshDeviceRow();

    private void RefreshDeviceRow()
    {
        if (!_uiReady || FingerprintRow == null || FingerprintPanel == null || ActivationLimitPanel == null)
            return;

        bool machineBound = SingleRadio.IsChecked == true;
        bool highVolumePortable = MultiRadio.IsChecked == true;

        FingerprintPanel.Visibility = machineBound ? Visibility.Visible : Visibility.Collapsed;
        ActivationLimitPanel.Visibility = machineBound ? Visibility.Visible : Visibility.Visible;

        FingerprintBox.IsEnabled = machineBound;
        FingerprintBox.Opacity = machineBound ? 1.0 : 0.55;
        if (!machineBound)
            FingerprintBox.Text = string.Empty;

        if (machineBound)
        {
            ActivationLimitLabel.Text = "Max activations";
            DeviceLimitBox.Text = "1";
            DeviceLimitBox.IsEnabled = false;
        }
        else
        {
            ActivationLimitLabel.Text = highVolumePortable
                ? "Max activations (high-volume preset)"
                : "Max activations";

            if (highVolumePortable && ParseInt(DeviceLimitBox.Text, 0, 0, 1_000_000) < 25)
                DeviceLimitBox.Text = LicenseGeneratorCore.UnlimitedPortableActivations.ToString();
            else if (!highVolumePortable && string.IsNullOrWhiteSpace(DeviceLimitBox.Text))
                DeviceLimitBox.Text = LicenseGeneratorCore
                    .SuggestedActivationLimit(ParseSelectedEdition())
                    .ToString();

            DeviceLimitBox.IsEnabled = true;
        }

        PreviewText.Text = machineBound
            ? "Machine-bound license — paste the customer PC fingerprint from About → License. The app will run only on that machine."
            : highVolumePortable
                ? "Portable license — high activation cap preset. Good for factory / enterprise fleets."
                : "Portable license — the app accepts the same .lic on multiple PCs until the activation limit is reached.";
    }

    private void GenerateKeyButton_Click(object sender, RoutedEventArgs e)
    {
        var test = LicenseGeneratorCore.RunSelfTest();
        SetStatus(test.Message, test.IsValid ? StatusKind.Success : StatusKind.Error);
        MessageBox.Show(this, test.Message,
            test.IsValid ? "Generator self-test" : "Generator self-test failed",
            MessageBoxButton.OK,
            test.IsValid ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    private void CopyPublicKeyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(AppCompatibleIssuerKeys.PublicKeyXml);
            SetStatus("App-compatible public key copied to clipboard.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus("Copy failed: " + ex.Message, StatusKind.Error);
        }
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var request = BuildRequest();
            string defaultPath = System.IO.Path.Combine(
                LicenseGeneratorCore.DefaultOutputFolder,
                Sanitise(request.CompanyName) + "-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + LicenseGeneratorCore.LicenseFileExtension);

            var result = LicenseGeneratorCore.GenerateLicense(request, defaultPath);
            OutputCard.Visibility = Visibility.Visible;

            if (!result.Success)
            {
                ResultText.Text = "FAILED: " + result.Message;
                ResultText.Foreground = ResolveBrush("DangerBrush");
                PasteKeyOutputBox.Text = result.PasteKey ?? string.Empty;
                KeyOutputBox.Text = result.LicenseJson ?? string.Empty;
                SetStatus(result.Message, StatusKind.Error);
                return;
            }

            string file = string.IsNullOrWhiteSpace(result.OutputPath)
                ? defaultPath
                : result.OutputPath;

            _lastLicenseJson = result.LicenseJson;
            _lastPasteKey = result.PasteKey;
            PasteKeyOutputBox.Text = result.PasteKey;
            KeyOutputBox.Text = result.LicenseJson;
            ResultText.Text = result.Message + "\nSaved to: " + file;
            ResultText.Foreground = ResolveBrush("SuccessBrush");
            SetStatus("License generated. Use SAVE AS for the .lic file or COPY PASTE KEY for the activation text.",
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
        if (string.IsNullOrWhiteSpace(_lastLicenseJson))
        {
            SetStatus("Generate or inspect a license first.", StatusKind.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save app-compatible license",
            Filter = "ProGlass license (*.lic)|*.lic|All files (*.*)|*.*",
            DefaultExt = ".lic",
            FileName = "CustomerLicense.lic",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            System.IO.File.WriteAllText(dialog.FileName, _lastLicenseJson, new UTF8Encoding(false));
            SetStatus("Saved to " + dialog.FileName, StatusKind.Success);
        }
    }

    private void InspectButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Inspect an existing .lic",
            Filter = "ProGlass license (*.lic,*.json,*.txt)|*.lic;*.json;*.txt|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;

        var verdict = LicenseGeneratorCore.InspectFile(dialog.FileName);
        OutputCard.Visibility = Visibility.Visible;
        ResultText.Foreground = ResolveBrush(verdict.IsValid ? "SuccessBrush" : "DangerBrush");
        ResultText.Text = verdict.Message;
        PasteKeyOutputBox.Text = verdict.PasteKey;
        KeyOutputBox.Text = verdict.LicenseJson;
        _lastLicenseJson = verdict.LicenseJson;
        _lastPasteKey = verdict.PasteKey;
        SetStatus(verdict.IsValid ? "Inspection complete — file is app-compatible." : "Inspection complete — file is NOT app-compatible.",
            verdict.IsValid ? StatusKind.Info : StatusKind.Warning);
    }

    private void CopyLicenseButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastPasteKey))
        {
            SetStatus("No paste key available yet.", StatusKind.Warning);
            return;
        }

        try
        {
            Clipboard.SetText(_lastPasteKey);
            SetStatus("Paste key copied to clipboard.", StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus("Copy failed: " + ex.Message, StatusKind.Error);
        }
    }

    private LicenseGeneratorCore.IssueRequest BuildRequest()
    {
        LicenseEdition edition = ParseSelectedEdition();
        int days = ParseInt(DaysBox.Text, edition == LicenseEdition.Trial ? 30 : 365, 1, 3650);
        bool machineBound = SingleRadio.IsChecked == true;
        bool highVolumePortable = MultiRadio.IsChecked == true;

        int maxActivations = machineBound
            ? 1
            : ParseInt(
                DeviceLimitBox.Text,
                highVolumePortable
                    ? LicenseGeneratorCore.UnlimitedPortableActivations
                    : LicenseGeneratorCore.SuggestedActivationLimit(edition),
                1,
                1_000_000);

        string? fingerprint = machineBound
            ? (FingerprintBox.Text?.Trim() ?? string.Empty)
            : null;

        if (machineBound && string.IsNullOrWhiteSpace(fingerprint))
        {
            throw new InvalidOperationException(
                "Machine-bound license selected, but the customer fingerprint is blank.");
        }

        return new LicenseGeneratorCore.IssueRequest
        {
            CustomerId = CustomerIdBox.Text?.Trim() ?? string.Empty,
            CompanyName = CustomerNameBox.Text?.Trim() ?? string.Empty,
            Edition = edition,
            ValidDays = days,
            MaxActivations = maxActivations,
            MachineFingerprint = string.IsNullOrWhiteSpace(fingerprint) ? null : fingerprint,
            Features = ParseFeatures(FeaturesBox.Text),
            Notes = CompanyBox.Text?.Trim() ?? string.Empty,
            LicenseIdOverride = EmailBox.Text?.Trim() ?? string.Empty,
            IssuedAtUtc = DateTime.UtcNow
        };
    }

    private LicenseEdition ParseSelectedEdition()
    {
        string text = (LicenseTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? LicenseTypeCombo.Text
            ?? LicenseEdition.Business.ToString();
        return LicenseGeneratorCore.ParseEdition(text);
    }

    private void ApplyEditionPreset(bool force)
    {
        if (!_uiReady && !force) return;

        LicenseEdition edition = ParseSelectedEdition();
        FeaturesBox.Text = string.Join(", ", LicenseGeneratorCore.DefaultFeatures(edition));

        if (SingleRadio.IsChecked != true)
            DeviceLimitBox.Text = LicenseGeneratorCore.SuggestedActivationLimit(edition).ToString();

        KeyDetailText.Text = edition switch
        {
            LicenseEdition.Trial => "Trial — 30-day evaluation, usually 1 activation, all modules allowed.",
            LicenseEdition.Basic => "Basic — inquiry and proforma workflow only, single-device by default.",
            LicenseEdition.Business => "Business — all ERP workflow modules, no cutting optimisation, 5 activations by default.",
            LicenseEdition.Factory => "Factory — full ERP plus cutting optimisation, high activation cap preset.",
            LicenseEdition.Enterprise => "Enterprise — unrestricted feature set and very high activation cap preset.",
            _ => "App-compatible RSA issuer key trusted by the current ProGlass build."
        };
    }

    private void RefreshIssuerUi()
    {
        KeyStatusText.Text = "Trusted by app · key fingerprint " + LicenseGeneratorCore.PublicKeyFingerprint;
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
        return new string((string.IsNullOrWhiteSpace(name) ? "License" : name.Trim())
            .Select(c => invalid.Contains(c) ? '_' : c)
            .ToArray());
    }
}
