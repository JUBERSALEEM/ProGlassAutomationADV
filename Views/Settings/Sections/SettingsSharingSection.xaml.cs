// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/SettingsSharingSection.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings — SHARING page (Stage 2)
// Pure presentation — all state lives in SettingsViewModel.Sharing.cs.
// Ensures the sharing editors are initialized on load, and persists
// document-rule edits (rule objects bind directly; the VM saves).
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Settings.Sections
{
    public partial class SettingsSharingSection : UserControl
    {
        public SettingsSharingSection()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                if (DataContext is ViewModels.Settings.Pages.SettingsSharingPageViewModel vm)
                    vm.EnsureSharingInit();
            };
        }

        private void RuleCombo_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Settings.Pages.SettingsSharingPageViewModel vm)
                vm.SaveSharingDocumentRules();
        }
    }
}
