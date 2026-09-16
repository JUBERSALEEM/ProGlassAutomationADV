// ═══════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/Cards/AiProvidersCard.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v3 — AiProvidersCard (Gemini / Groq / Cohere keys).
//
// BUILD FIX (owner build 2026-09-17, CS1061 ×3 + CS0103 ×3):
// The card split moved the three API-key PasswordBoxes into THIS card,
// but their PasswordChanged handlers — and the Loaded seeding that fills
// them from saved config — stayed behind in SettingsAiSection.xaml.cs.
// XAML event attributes resolve against the x:Class type, so the markup
// compiler could not find them, and the section code-behind referenced
// x:Name fields it no longer generates. Both moved here with the controls.
//
// PasswordBox cannot bind Password as a dependency property, so the keys
// sync through code-behind exactly as v1 did; the secret never lives in a
// bindable property. DataContext inherits the AI page VM
// (SettingsAiPageViewModel) from the section's page template.
// ═══════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.Settings.Pages;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Settings.Sections.Cards
{
    public partial class AiProvidersCard : UserControl
    {
        public AiProvidersCard()
        {
            InitializeComponent();

            // Seed the boxes once the page VM is inherited — a PasswordBox
            // has no bindable Password, so saved keys are pushed in here.
            Loaded += (_, __) =>
            {
                if (DataContext is not SettingsAiPageViewModel vm) return;
                if (!string.IsNullOrEmpty(vm.GeminiApiKey))
                    GeminiKeyBox.Password = vm.GeminiApiKey;
                if (!string.IsNullOrEmpty(vm.GroqApiKey))
                    GroqKeyBox.Password = vm.GroqApiKey;
                if (!string.IsNullOrEmpty(vm.CohereApiKey))
                    CohereKeyBox.Password = vm.CohereApiKey;
            };
        }

        private void GeminiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsAiPageViewModel vm && sender is PasswordBox pb)
                vm.GeminiApiKey = pb.Password;
        }

        private void GroqKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsAiPageViewModel vm && sender is PasswordBox pb)
                vm.GroqApiKey = pb.Password;
        }

        private void CohereKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsAiPageViewModel vm && sender is PasswordBox pb)
                vm.CohereApiKey = pb.Password;
        }
    }
}
