using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace ProGlassAutomation.Agent.Demo.Automation
{
    /// <summary>
    /// In-process WPF driver. Walks the live visual and logical trees
    /// by AutomationId, then invokes the same Command the user would click.
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

        public static async Task DwellAsync(int ms, CancellationToken ct)
        {
            if (ms <= 0) return;
            await WaitReadyAsync(Math.Min(120, ms), ct).ConfigureAwait(false);
            int left = ms - Math.Min(120, ms);
            if (left > 0) await Task.Delay(left, ct).ConfigureAwait(false);
        }

        public static async Task<bool> WaitForIdAsync(
            string automationId, int timeoutMs, CancellationToken ct)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < until)
            {
                ct.ThrowIfCancellationRequested();
                if (await ExistsAsync(automationId, ct).ConfigureAwait(false))
                    return true;
                await Task.Delay(150, ct).ConfigureAwait(false);
            }
            return await ExistsAsync(automationId, ct).ConfigureAwait(false);
        }

        public static Task<bool> ExistsAsync(string automationId, CancellationToken ct) =>
            OnUiAsync(() => FindById(Application.Current?.MainWindow, automationId) != null, ct);

        public static async Task<bool> ClickAsync(string automationId, CancellationToken ct)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                bool ok = await OnUiAsync(() =>
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
                }, ct).ConfigureAwait(false);
                if (ok) return true;
                await Task.Delay(200, ct).ConfigureAwait(false);
            }
            return false;
        }

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

        public static DependencyObject? FindById(DependencyObject? root, string id)
            => FindByIdCore(root, id, new HashSet<DependencyObject>());

        /// <summary>
        /// Walks visual children, then logical children. Grid logical
        /// children include ColumnDefinition / RowDefinition, which are
        /// NOT Visuals — VisualTreeHelper.GetChildrenCount throws
        /// "is not a Visual or Visual3D" on them. That exception was
        /// aborting every demo walk the moment a wizard Grid was reached.
        /// </summary>
        private static DependencyObject? FindByIdCore(
            DependencyObject? root, string id, HashSet<DependencyObject> seen)
        {
            if (root == null || string.IsNullOrWhiteSpace(id)) return null;
            if (!seen.Add(root)) return null;
            if (string.Equals(AutomationProperties.GetAutomationId(root), id,
                    StringComparison.Ordinal))
                return root;

            if (root is Visual or Visual3D)
            {
                int n = VisualTreeHelper.GetChildrenCount(root);
                for (int i = 0; i < n; i++)
                {
                    var hit = FindByIdCore(VisualTreeHelper.GetChild(root, i), id, seen);
                    if (hit != null) return hit;
                }
            }

            foreach (var child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject d)
                {
                    var hit = FindByIdCore(d, id, seen);
                    if (hit != null) return hit;
                }
            }
            return null;
        }

        public static Task<T> OnUiAsync<T>(Func<T> work, CancellationToken ct)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp == null) return Task.FromResult(default(T)!);
            if (disp.CheckAccess()) return Task.FromResult(work());
            return disp.InvokeAsync(work, DispatcherPriority.Normal, ct).Task;
        }
    }
}
