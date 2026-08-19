using System;
using System.Windows;
using System.Windows.Threading;

namespace ProGlassAutomation.ViewModels.Shared
{
    /// <summary>
    /// Marshals collection / binding mutations onto the WPF dispatcher.
    /// ObservableCollection throws if a bound view is changed off-thread
    /// (ERR-A65374 during demo Activity / God View loads).
    /// </summary>
    public static class UiDispatch
    {
        public static void Run(Action work)
        {
            if (work == null) return;
            var disp = Application.Current?.Dispatcher;
            if (disp == null || disp.CheckAccess())
            {
                work();
                return;
            }
            disp.Invoke(work, DispatcherPriority.DataBind);
        }
    }
}
