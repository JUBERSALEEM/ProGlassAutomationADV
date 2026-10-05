// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/SettingsLicenseSection.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v2 — LICENSE page
//
// Wrapper that hosts the shared LicenseActivationView inside the
// Settings shell. All licensing state lives in
// ViewModels.Licensing.LicenseStatusViewModel (singleton) and
// Services.Licensing.LicenseService (static), so this class is
// purely presentational.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Settings.Sections
{
    public partial class SettingsLicenseSection : UserControl
    {
        public SettingsLicenseSection()
        {
            InitializeComponent();
        }
    }
}
