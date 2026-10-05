// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/SettingsView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v2 — shell code-behind
//
// v1 held PasswordBox sync + Whisper card handlers here; those moved
// with their content into Sections/SettingsAiSection.xaml.cs and
// Sections/SettingsVoiceSection.xaml.cs. The shell itself is pure
// presentation — navigation state lives in SettingsViewModel.
//
// Phase 3 (2026-09-05): Esc closes the navigation rail — the standard
// drawer affordance every operator already knows from the web.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Settings
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        /// <summary>Esc closes the open navigation rail (and only the
        /// rail — nothing else claims the key here). Routed via
        /// PreviewKeyDown so the gesture also lands while a TextBox
        /// inside the section has focus.</summary>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape &&
                DataContext is ViewModels.Settings.SettingsViewModel vm &&
                vm.IsSettingsRailOpen &&
                vm.ToggleSettingsRailCommand != null)
            {
                vm.ToggleSettingsRailCommand.Execute(null);
                e.Handled = true;
                return;
            }
            base.OnPreviewKeyDown(e);
        }
    }
}
