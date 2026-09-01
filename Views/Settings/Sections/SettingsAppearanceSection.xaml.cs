// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/SettingsAppearanceSection.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v2 — Appearance page (theme, wallpaper, accent)
// Pure presentation — all state lives in SettingsViewModel.
//
// 2026-09-02 (Phase 3.7): the LANGUAGE & CURRENCY group's controller
// logic moved into ViewModels/Settings/SettingsViewModel.AppearanceSection.cs
// (pickers, FxStatusText, reset command, service subscriptions). This
// file is now only Loaded/Unloaded glue that hands the VM its lifetime
// hooks. DataContext = SettingsViewModel (inherited from SettingsView).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.Settings;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Settings.Sections
{
    public partial class SettingsAppearanceSection : UserControl
    {
        public SettingsAppearanceSection()
        {
            InitializeComponent();
            Loaded += OnSectionLoaded;
            Unloaded += OnSectionUnloaded;
        }

        private void OnSectionLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
                vm.InitializeAppearanceSection();
        }

        private void OnSectionUnloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
                vm.TeardownAppearanceSection();
        }
    }
}
