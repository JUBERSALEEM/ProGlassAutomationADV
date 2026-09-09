// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Dialogs/DialogService.cs
// PROJECT: ProGlassAutomation
// MODULE: MVVM leakage sweep (Phase 2, audit item 2.1)
//
// Default IDialogService: delegates to MessageBox.Show with identical
// semantics (zero behaviour change during migration). Registered as a
// singleton in DI and exposed as App.Dialogs.
//
// THREAD AFFINITY (Sept 2026 senior audit — Sharing Center STA crash)
// ───────────────────────────────────────────────────────────────────
//   WPF Window construction (InitializeComponent) and ShowDialog REQUIRE
//   the UI (STA) thread. Callers that reach these methods from a
//   background/thread-pool thread — e.g. an async share flow whose
//   continuation resumed off the UI thread after ConfigureAwait(false) —
//   used to crash with "The calling thread must be STA…", surfaced as a
//   TargetInvocationException ("Exception has been thrown by the target of
//   an invocation") out of Activator.CreateInstance.
//
//   Every window method below now marshals its body onto the application
//   Dispatcher when the caller is off-thread (a no-op when already on the
//   UI thread), so window dialogs open reliably from any thread, app-wide.
//   MessageBox helpers are marshalled too so error prompts keep the right
//   owner and never race the UI thread.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Threading;

namespace ProGlassAutomation.Services.Dialogs
{
    public sealed class DialogService : IDialogService
    {
        /// <summary>
        /// Runs <paramref name="work"/> on the WPF UI (STA) thread and
        /// returns its result. Executes inline when already on the UI
        /// thread (or when no Application/Dispatcher exists, e.g. a
        /// headless test host); otherwise blocks the caller on a
        /// Dispatcher.Invoke so modal semantics are preserved.
        /// </summary>
        private static T OnUi<T>(Func<T> work)
        {
            Dispatcher? disp = Application.Current?.Dispatcher;
            if (disp == null || disp.CheckAccess())
                return work();
            return disp.Invoke(work);
        }

        private static void OnUi(Action work)
        {
            Dispatcher? disp = Application.Current?.Dispatcher;
            if (disp == null || disp.CheckAccess())
            {
                work();
                return;
            }
            disp.Invoke(work);
        }

        public TWindow Create<TWindow>(params object[] ctorArgs) where TWindow : System.Windows.Window
            => OnUi(() =>
            {
                var window = (TWindow)System.Activator.CreateInstance(typeof(TWindow), ctorArgs)!;
                if (window.Owner == null)
                    window.Owner = System.Windows.Application.Current?.MainWindow;
                return window;
            });

        public bool? ShowDialog<TWindow>(params object[] ctorArgs) where TWindow : System.Windows.Window
            => OnUi(() => Create<TWindow>(ctorArgs).ShowDialog());

        public void Show<TWindow>(params object[] ctorArgs) where TWindow : System.Windows.Window
            => OnUi(() => Create<TWindow>(ctorArgs).Show());
        public MessageBoxResult Show(string message, string? caption = null,
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None)
            => OnUi(() =>
            {
                var owner = System.Windows.Application.Current?.MainWindow;
                return owner != null && owner.IsLoaded
                    ? MessageBox.Show(owner, message, caption ?? string.Empty,
                        button, icon, defaultResult)
                    : MessageBox.Show(message, caption ?? string.Empty,
                        button, icon, defaultResult);
            });

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
