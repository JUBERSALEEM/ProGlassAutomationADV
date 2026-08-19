using ProGlassAutomation.Agent.Demo.Automation;
using ProGlassAutomation.ViewModels.JobProfitability;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Live Job Profitability: load Step 9 snapshots, filter, read margins.
    /// Figures are not recalculated here.
    /// </summary>
    public sealed class JobProfitabilityWorkflow : IDemoWorkflowRunner
    {
        public string ModuleId => "JobProfitability";
        public string OutputFileName => "JobProfitability_Demo.mp4";
        public IReadOnlyList<string> DeepEvidenceIds { get; } =
            ["loaded", "filtered"];

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();
            if (!DemoWalk.Navigate("JobProfitability")) return events;

            await WpfUiDriver.WaitReadyAsync(500, ct).ConfigureAwait(false);
            var jp = await ResolveAsync(ct).ConfigureAwait(false);
            if (jp == null) return events;

            await jp.LoadAsync().ConfigureAwait(true);
            await DemoWalk.WaitUntilAsync(
                () => !jp.IsBusy, 10_000, ct).ConfigureAwait(false);

            await DemoWalk.PresentAsync(events, "loaded",
                "Job profitability",
                $"Margins come from Proforma Step 9 snapshots — they are not recalculated here. {jp.Rows.Count} jobs. Portfolio {jp.TotalProfitLabel} {jp.TotalProfitDisplay}.",
                $"rows={jp.Rows.Count} profit={jp.TotalProfit:0.00} losses={jp.LossCount}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            await WpfUiDriver.OnUiAsync(() =>
            {
                jp.SearchText = "Skyline";
                if (jp.CompanyFilterOptions.Count > 1)
                    jp.CompanyFilter = jp.CompanyFilterOptions
                        .FirstOrDefault(o => o != JobProfitabilityViewModel.AllFilter)
                        ?? jp.CompanyFilter;
                return true;
            }, ct).ConfigureAwait(false);

            await WpfUiDriver.DwellAsync(900, ct).ConfigureAwait(false);
            await DemoWalk.PresentAsync(events, "filtered",
                "Filter the book",
                $"Search Skyline. {jp.Rows.Count} rows remain. Net {jp.TotalNetDisplay}, cost {jp.TotalCostDisplay}.",
                $"search={jp.SearchText} rows={jp.Rows.Count} net={jp.TotalNet:0.00}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            await WpfUiDriver.OnUiAsync(() =>
            {
                jp.SearchText = "";
                jp.CompanyFilter = JobProfitabilityViewModel.AllFilter;
                return true;
            }, ct).ConfigureAwait(false);

            await WpfUiDriver.DwellAsync(500, ct).ConfigureAwait(false);
            return events;
        }

        private static Task<JobProfitabilityViewModel?> ResolveAsync(CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var main = System.Windows.Application.Current?.MainWindow?.DataContext
                    as ViewModels.MainViewModel;
                return main?.JobProfitabilityVM;
            }, ct);
    }
}
