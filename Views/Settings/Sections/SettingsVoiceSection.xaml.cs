// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/SettingsVoiceSection.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v2 — VOICE page (Whisper models)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.Settings;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Settings.Sections
{
    public partial class SettingsVoiceSection : UserControl
    {
        public SettingsVoiceSection()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Whisper model card clicked — sets SelectedWhisperModel on VM.
        /// Tag on the Border holds the model key ("base","medium","large-v3").
        /// </summary>
        private void ModelOption_MouseLeftButtonDown(
            object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el &&
                el.Tag is string modelKey &&
                DataContext is Pages.SettingsVoicePageViewModel vm)
            {
                vm.SelectedWhisperModel = modelKey;
            }
        }
    }
}
