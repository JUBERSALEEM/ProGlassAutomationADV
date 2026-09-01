// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Dialogs/DialogService.cs
// PROJECT: ProGlassAutomation
// MODULE: MVVM leakage sweep (Phase 2, audit item 2.1)
//
// Default IDialogService: delegates to MessageBox.Show with identical
// semantics (zero behaviour change during migration). Registered as a
// singleton in DI and exposed as App.Dialogs.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;

namespace ProGlassAutomation.Services.Dialogs
{
    public sealed class DialogService : IDialogService
    {
        public MessageBoxResult Show(string message, string? caption = null,
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None)
            => MessageBox.Show(message, caption ?? string.Empty,
                button, icon);

        public void Info(string message, string? caption = null)
            => Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);

        public void Warning(string message, string? caption = null)
            => Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Warning);

        public void Error(string message, string? caption = null)
            => Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);

        public bool Confirm(string message, string? caption = null)
            => Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question)
               == MessageBoxResult.Yes;
    }
}
