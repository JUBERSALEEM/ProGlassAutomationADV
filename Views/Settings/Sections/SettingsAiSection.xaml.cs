// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/SettingsAiSection.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v2 — AI ASSISTANT page
//
// BUILD FIX (owner build 2026-09-17): this page is now pure composition —
// AiAssistantCard + AiProvidersCard + AiSecurityCard. The three API-key
// PasswordBoxes live in AiProvidersCard, so their PasswordChanged handlers
// and the key seeding moved into AiProvidersCard.xaml.cs, next to the
// controls they touch. Keeping them here produced CS0103 (GeminiKeyBox /
// GroqKeyBox / CohereKeyBox do not exist in this namescope) while the card
// failed with CS1061 (no handler on AiProvidersCard).
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Settings.Sections
{
    public partial class SettingsAiSection : UserControl
    {
        public SettingsAiSection()
        {
            InitializeComponent();
        }
    }
}
