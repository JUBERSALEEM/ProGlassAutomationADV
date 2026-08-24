// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/SettingsFeaturesSection.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v2 — FEATURES page
//
// Pure presentation. All state lives on
// ViewModels.Settings.ProformaInvoiceFeaturesViewModel, which the
// XAML reaches via {Binding ProformaFeatures}.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Settings.Sections
{
    public partial class SettingsFeaturesSection : UserControl
    {
        public SettingsFeaturesSection()
        {
            InitializeComponent();
        }
    }
}
