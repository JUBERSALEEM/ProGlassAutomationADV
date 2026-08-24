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
                $"This is the Job Profitability book — the finance team's favourite screen. Every row is one job we quoted, and each row shows what we told the customer, what we actually spent on glass, cutting, delivery and overhead, and what is left as profit. The important thing to understand is that these margins are not being recalculated on the fly — they are the frozen figures we saved on Step 9 of each Proforma when the quote was approved, so the numbers you see here are the ones we actually committed to. Right now we are looking at {jp.Rows.Count} jobs, and the whole portfolio is {jp.TotalProfitLabel} {jp.TotalProfitDisplay}.",
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
                $"You can slice this book any way you need to answer a question. Here we searched for the customer 'Skyline' and picked one of our companies from the filter. Instantly the totals at the bottom recomputed for just that slice: {jp.Rows.Count} jobs remain, net value {jp.TotalNetDisplay} and total cost {jp.TotalCostDisplay}. This is how a manager quickly answers questions like — how much profit did we make from this customer, or which projects lost money last quarter — without exporting anything to Excel.",
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
