// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/SettingsThemesSection.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v2 — Themes page (visual family + Crystal Fluid tuning)
// Pure presentation — all state lives in SettingsViewModel. Loaded/Unloaded
// glue hands the VM its lifetime hooks (same pattern as the Appearance
// section). DataContext = SettingsViewModel (inherited from SettingsView).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.Settings;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Settings.Sections
{
    public partial class SettingsThemesSection : UserControl
    {
        public SettingsThemesSection()
        {
            InitializeComponent();
            Loaded += OnSectionLoaded;
        }

        private void OnSectionLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
                vm.InitializeThemesSection();
        }
    }
}
