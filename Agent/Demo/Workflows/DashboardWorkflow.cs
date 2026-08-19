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
                $"{dash.WelcomeLine}. Overview is the same figures the Dashboard computes from the isolated demo folder.",
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
                $"Walked {visited.Count} live tabs: {string.Join(", ", visited)}. Each tab reloads its own module figures.",
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
