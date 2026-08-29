// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/MainWindow.xaml.cs
// PROJECT: ProGlassAutomation.LicenseGenerator
// MODULE: License Key Generator — window logic (2026 single-click flow)
//
// ONE JOB: pick a plan (7 days → 2 years), press GENERATE, copy the
// single PGKEY1 block. No key files, no requests, no responses.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ProGlassAutomation.Tools.LicenseGenerator;

public partial class MainWindow : Window
{
    private string? _lastLicenseKey;

    public MainWindow()
    {
        InitializeComponent();
        // Set in code (not XAML) so the Checked handler can never fire
        // before the preview TextBlock exists.
        Plan1YearRadio.IsChecked = true;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetStatus(
            "Ready — pick a plan and press GENERATE. The customer pastes the key in the app and it activates instantly.",
            StatusKind.Info);
    }

    // ────────────────────────────────────────────────────────────────────
    // Plan + generate
    // ────────────────────────────────────────────────────────────────────

    private LicenseGeneratorCore.LicensePlan SelectedPlan()
    {
        if (Plan7DaysRadio.IsChecked == true) return LicenseGeneratorCore.Plans[0];
        if (Plan30DaysRadio.IsChecked == true) return LicenseGeneratorCore.Plans[1];
        if (Plan6MonthsRadio.IsChecked == true) return LicenseGeneratorCore.Plans[2];
        if (Plan2YearsRadio.IsChecked == true) return LicenseGeneratorCore.Plans[4];
        return LicenseGeneratorCore.Plans[3]; // 1 Year (also the default)
    }

    private void PlanRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (KeyPreviewText == null) return;

        LicenseGeneratorCore.LicensePlan plan = SelectedPlan();
        string duration = plan.Days switch
        {
            7 => "seven days",
            30 => "thirty days",
            182 => "six months",
            365 => "one year",
            _ => "two years"
        };
        KeyPreviewText.Text =
            plan.Name + " — the customer's key expires " + duration +
            " from the moment it is generated.";
    }

    private void GenerateLicenseKeyButton_Click(object sender, RoutedEventArgs e)
    {
        LicenseGeneratorCore.LicensePlan plan = SelectedPlan();
        LicenseGeneratorCore.LicenseKeyIssueResult result =
            LicenseGeneratorCore.GenerateLicenseKey(plan);

        if (!result.Success)
        {
            LicenseKeyOutputBox.Text = string.Empty;
            _lastLicenseKey = null;
            SetStatus(result.Message, StatusKind.Error);
            return;
        }

        LicenseKeyOutputBox.Text = result.ArmoredKey;
        _lastLicenseKey = result.ArmoredKey;
        KeyStatusText.Text = plan.Name + " key generated — verified against the app's embedded trust key.";
        SetStatus(result.Message, StatusKind.Success);
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
}
