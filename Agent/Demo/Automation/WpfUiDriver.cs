using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ProGlassAutomation.Agent.Demo.Automation
{
    /// <summary>
    /// In-process WPF driver. Walks the live visual tree by AutomationId,
    /// then invokes the same Command the user would click.
    /// </summary>
    public static class WpfUiDriver
    {
        public static async Task WaitReadyAsync(int ms, CancellationToken ct)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp == null) { await Task.Delay(ms, ct).ConfigureAwait(false); return; }
            var tcs = new TaskCompletionSource<bool>();
            disp.BeginInvoke(DispatcherPriority.Loaded, new Action(() => tcs.TrySetResult(true)));
            using (ct.Register(() => tcs.TrySetCanceled(ct)))
                await tcs.Task.ConfigureAwait(false);
            if (ms > 0) await Task.Delay(ms, ct).ConfigureAwait(false);
        }

        public static Task<bool> ClickAsync(string automationId, CancellationToken ct) =>
            OnUiAsync(() =>
            {
                var el = FindById(Application.Current?.MainWindow, automationId);
                if (el is ButtonBase btn)
                {
                    if (btn.Command != null && btn.Command.CanExecute(btn.CommandParameter))
                    {
                        btn.Command.Execute(btn.CommandParameter);
                        return true;
                    }
                    btn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    return true;
                }
                return false;
            }, ct);

        public static Task<bool> SetTextAsync(string automationId, string value, CancellationToken ct) =>
            OnUiAsync(() =>
            {
                var el = FindById(Application.Current?.MainWindow, automationId);
                if (el is TextBox tb)
                {
                    tb.Focus();
                    tb.Text = value ?? "";
                    tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                    return true;
                }
                if (el is ComboBox cb)
                {
                    cb.Text = value ?? "";
                    return true;
                }
                return false;
            }, ct);

        public static Task<string?> ReadTextAsync(string automationId, CancellationToken ct) =>
            OnUiAsync(() =>
            {
                var el = FindById(Application.Current?.MainWindow, automationId);
                return el switch
                {
                    TextBox tb => tb.Text,
                    TextBlock tblk => tblk.Text,
                    ContentControl cc => cc.Content?.ToString(),
                    _ => AutomationProperties.GetName(el)
                };
            }, ct);

        public static DependencyObject? FindById(DependencyObject? root, string id)
        {
            if (root == null || string.IsNullOrWhiteSpace(id)) return null;
            if (string.Equals(AutomationProperties.GetAutomationId(root), id,
                    StringComparison.Ordinal))
                return root;

            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var hit = FindById(VisualTreeHelper.GetChild(root, i), id);
                if (hit != null) return hit;
            }
            return null;
        }

        private static Task<T> OnUiAsync<T>(Func<T> work, CancellationToken ct)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp == null) return Task.FromResult(default(T)!);
            if (disp.CheckAccess()) return Task.FromResult(work());
            return disp.InvokeAsync(work, DispatcherPriority.Normal, ct).Task;
        }
    }
}
