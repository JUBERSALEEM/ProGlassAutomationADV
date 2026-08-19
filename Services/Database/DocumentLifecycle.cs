// Cross-module signal: PI / JO / DN / TI lists recompute derived status
// when any related document is created or deleted.
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ProGlassAutomation.Services.Database
{
    public static class DocumentLifecycle
    {
        public static event Action? Changed;

        public static void Raise()
        {
            try { Changed?.Invoke(); }
            catch { /* a listener must never undo a completed write */ }
        }

        public static void Subscribe(Func<Task> reload)
        {
            Changed += () =>
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                {
                    _ = Safe(reload);
                    return;
                }

                if (dispatcher.CheckAccess())
                    _ = Safe(reload);
                else
                    dispatcher.InvokeAsync(() => Safe(reload));
            };
        }

        private static async Task Safe(Func<Task> reload)
        {
            try { await reload(); }
            catch { }
        }
    }
}
