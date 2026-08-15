// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/SettingsView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v2 — shell code-behind
//
// v1 held PasswordBox sync + Whisper card handlers here; those moved
// with their content into Sections/SettingsAiSection.xaml.cs and
// Sections/SettingsVoiceSection.xaml.cs. The shell itself is pure
// presentation — navigation state lives in SettingsViewModel.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Settings
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }
    }
}
