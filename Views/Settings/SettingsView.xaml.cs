// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/SettingsView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings (Chat 25 v1.1)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.Settings;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.Settings
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();

            Loaded += (_, __) =>
            {
                if (DataContext is SettingsViewModel vm)
                {
                    if (!string.IsNullOrEmpty(vm.GeminiApiKey))
                        GeminiKeyBox.Password = vm.GeminiApiKey;
                    if (!string.IsNullOrEmpty(vm.GroqApiKey))
                        GroqKeyBox.Password = vm.GroqApiKey;
                    if (!string.IsNullOrEmpty(vm.CohereApiKey))
                        CohereKeyBox.Password = vm.CohereApiKey;
                }
            };
        }

        private void GeminiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm && sender is PasswordBox pb)
                vm.GeminiApiKey = pb.Password;
        }

        private void GroqKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm && sender is PasswordBox pb)
                vm.GroqApiKey = pb.Password;
        }

        private void CohereKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm && sender is PasswordBox pb)
                vm.CohereApiKey = pb.Password;
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
                DataContext is SettingsViewModel vm)
            {
                vm.SelectedWhisperModel = modelKey;
            }
        }
    }
}