// ═══════════════════════════════════════════════════════════════════════
//  FILE: Views/Shared/LanguageSuggestionDialog.xaml.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02
//
//  One-time, first-launch offer to switch from English to the detected
//  region's language. See LocalizationService.GetPendingRegionalSuggestion
//  for the rules that decide whether this is shown at all.
//
//  The dialog only reports a CHOICE. It does not apply the language and
//  does not write config -- the caller owns both, so the "asked once"
//  flag is persisted exactly once, in one place, whichever way the user
//  answers (including closing the window with the X).
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using ProGlassAutomation.Services.Localization;

namespace ProGlassAutomation.Views.Shared
{
    public partial class LanguageSuggestionDialog : Window
    {
        /// <summary>
        /// True when the user chose the regional language. False for
        /// "Keep English", for Escape, and for closing the window — the
        /// safe default is always to leave English in place.
        /// </summary>
        public bool SwitchRequested { get; private set; }

        public LanguageSuggestionDialog(LanguageInfo suggested)
        {
            InitializeComponent();

            // Native script first: the user may not read the current UI
            // language, which is the whole reason this dialog is not
            // localized.
            NativePromptText.Text = suggested.NativeName;

            ExplainText.Text =
                $"We detected your region, where {suggested.EnglishName} "
                + "is commonly used. Would you like to use the app in "
                + $"{suggested.EnglishName} instead of English?";

            SwitchButton.Content = $"Switch to {suggested.NativeName}";

            // Right-to-left languages must preview in their own direction.
            if (suggested.IsRtl)
                NativePromptText.FlowDirection = FlowDirection.RightToLeft;
        }

        private void Switch_Click(object sender, RoutedEventArgs e)
        {
            SwitchRequested = true;
            DialogResult = true;
        }

        private void KeepEnglish_Click(object sender, RoutedEventArgs e)
        {
            SwitchRequested = false;
            DialogResult = false;
        }
    }
}
