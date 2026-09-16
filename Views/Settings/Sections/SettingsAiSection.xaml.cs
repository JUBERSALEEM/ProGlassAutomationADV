// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/SettingsAiSection.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v2 — AI ASSISTANT page
//
// PasswordBox cannot bind Password as a dependency property, so the
// three API-key boxes sync through code-behind exactly as v1 did —
// only relocated from the monolithic SettingsView code-behind.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.Settings;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Settings.Sections
{
    public partial class SettingsAiSection : UserControl
    {
        public SettingsAiSection()
        {
            InitializeComponent();

            Loaded += (_, __) =>
            {
                if (DataContext is Pages.SettingsAiPageViewModel vm)
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
            if (DataContext is Pages.SettingsAiPageViewModel vm && sender is PasswordBox pb)
                vm.GeminiApiKey = pb.Password;
        }

        private void GroqKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is Pages.SettingsAiPageViewModel vm && sender is PasswordBox pb)
                vm.GroqApiKey = pb.Password;
        }

        private void CohereKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is Pages.SettingsAiPageViewModel vm && sender is PasswordBox pb)
                vm.CohereApiKey = pb.Password;
        }
    }
}
