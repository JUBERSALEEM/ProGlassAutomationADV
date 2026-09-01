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
        public TWindow Create<TWindow>(params object[] ctorArgs) where TWindow : System.Windows.Window
        {
            var window = (TWindow)System.Activator.CreateInstance(typeof(TWindow), ctorArgs)!;
            if (window.Owner == null)
                window.Owner = System.Windows.Application.Current?.MainWindow;
            return window;
        }

        public bool? ShowDialog<TWindow>(params object[] ctorArgs) where TWindow : System.Windows.Window
            => Create<TWindow>(ctorArgs).ShowDialog();

        public void Show<TWindow>(params object[] ctorArgs) where TWindow : System.Windows.Window
            => Create<TWindow>(ctorArgs).Show();
        public MessageBoxResult Show(string message, string? caption = null,
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None)
            => MessageBox.Show(message, caption ?? string.Empty,
                button, icon, defaultResult);

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
