using ProGlassAutomation.Agent.Demo.Automation;
using ProGlassAutomation.ViewModels.Dashboard;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Live Dashboard: overview plus every real module tab via SelectTabCommand.
    /// </summary>
    public sealed class DashboardWorkflow : IDemoWorkflowRunner
    {
        public string ModuleId => "Dashboard";
        public string OutputFileName => "Dashboard_Demo.mp4";
        public IReadOnlyList<string> DeepEvidenceIds { get; } =
            ["overview", "tabs"];

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();
            if (!DemoWalk.Navigate("Dashboard")) return events;

            await WpfUiDriver.WaitReadyAsync(600, ct).ConfigureAwait(false);
            var dash = await ResolveAsync(ct).ConfigureAwait(false);
            if (dash == null) return events;

            dash.OnViewLoaded();
            await DemoWalk.WaitUntilAsync(
                () => !dash.ActivePage.IsLoading, 8000, ct).ConfigureAwait(false);

            await DemoWalk.PresentAsync(events, "overview",
                "Factory pulse",
                $"{dash.WelcomeLine}. This is the operations dashboard — the first screen the team opens every morning. The tiles you see summarise the whole factory: today's quotations, jobs on the shop floor, deliveries due, invoices waiting for payment and the running profit and loss. Every figure is calculated live from the demo folder, so nothing here is hard-coded. If a quote is edited or a delivery is closed, the numbers refresh on the very next load.",
                $"tab={dash.ActivePage.TabTitle} pages={dash.Pages.Count}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            var visited = new List<string>();
            foreach (var page in dash.Pages)
            {
                await DemoWalk.TryCommandAsync(dash.SelectTabCommand, page, ct)
                    .ConfigureAwait(false);
                await DemoWalk.WaitUntilAsync(
                    () => dash.ActivePage == page && !page.IsLoading,
                    6000, ct).ConfigureAwait(false);
                visited.Add(page.TabTitle);
                await WpfUiDriver.DwellAsync(900, ct).ConfigureAwait(false);
            }

            await DemoWalk.PresentAsync(events, "tabs",
                "Every dashboard tab",
                $"Now the dashboard is walking through each of its {visited.Count} module tabs: {string.Join(", ", visited)}. Each tab is a mini-report for one part of the business — quotations, production, deliveries, receivables and stock — and each one reloads its own figures on demand, so you always see the latest state without having to open every module manually. Think of it as the factory's control panel.",
                string.Join("|", visited),
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            if (dash.Pages.Count > 0)
            {
                await DemoWalk.TryCommandAsync(
                    dash.SelectTabCommand, dash.Pages[0], ct)
                    .ConfigureAwait(false);
            }

            await WpfUiDriver.DwellAsync(500, ct).ConfigureAwait(false);
            return events;
        }

        private static Task<DashboardViewModel?> ResolveAsync(CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var main = System.Windows.Application.Current?.MainWindow?.DataContext
                    as ViewModels.MainViewModel;
                return main?.DashboardVM;
            }, ct);
    }
}
