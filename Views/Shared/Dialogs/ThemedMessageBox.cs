// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Shared/Dialogs/ThemedMessageBox.cs
// PROJECT: ProGlassAutomation
// MODULE: Unified dialog platform — themed replacement for MessageBox
//
// Static facade with a MessageBox.Show-shaped API. Services/Dialogs/
// DialogService routes Info / Warning / Error / Confirm / Show through
// this, which converts every one of the ~263 prompt call sites (Delivery
// Note delete/void included) to the themed dialog chrome without touching
// a single ViewModel.
//
// THREAD AFFINITY: mirrors DialogService.OnUi — window creation and
// ShowDialog always run on the UI (STA) thread; callers from background
// threads are marshalled with Dispatcher.Invoke (modal semantics kept).
//
// SAFETY: any failure building/showing the themed window falls back to
// the native MessageBox so a prompt is never silently swallowed (e.g.
// headless test hosts).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Threading;

namespace ProGlassAutomation.Views.Shared.Dialogs
{
    public static class ThemedMessageBox
    {
        public static MessageBoxResult Show(
            string message,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None)
            => Show(null, message, caption, button, icon, defaultResult);

        public static MessageBoxResult Show(
            Window? owner,
            string message,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None)
            => OnUi(() =>
            {
                owner ??= Application.Current?.MainWindow;
                if (owner == null || !owner.IsLoaded)
                    owner = null;

                try
                {
                    var window = ThemedMessageBoxWindow.Create(
                        message, caption, button, icon, defaultResult);

                    if (owner != null)
                        window.Owner = owner;

                    window.ShowDialog();
                    return window.GetResult();
                }
                catch
                {
                    // Never swallow a prompt: degrade to the native box.
                    return MessageBox.Show(owner, message, caption, button, icon);
                }
            });

        private static T OnUi<T>(Func<T> work)
        {
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                return work();
            return dispatcher.Invoke(work);
        }
    }
}
