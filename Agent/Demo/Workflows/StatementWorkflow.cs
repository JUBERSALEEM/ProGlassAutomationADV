using ProGlassAutomation.Agent.Demo.Automation;
using ProGlassAutomation.ViewModels.Reports;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Live statement generator: new statement, customer filter, preview,
    /// select all. Does not run Generate — that can open a warning dialog.
    /// </summary>
    public sealed class StatementWorkflow : IDemoWorkflowRunner
    {
        public string ModuleId => "SalesReport";
        public string OutputFileName => "Statement_Demo.mp4";
        public IReadOnlyList<string> DeepEvidenceIds { get; } =
            ["wizard", "filtered", "preview"];

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();
            if (!DemoWalk.Navigate("SalesReport")) return events;

            await WpfUiDriver.WaitReadyAsync(500, ct).ConfigureAwait(false);
            var sales = await ResolveAsync(ct).ConfigureAwait(false);
            if (sales == null) return events;

            await sales.ListVM.LoadAsync().ConfigureAwait(true);
            await DemoWalk.PresentAsync(events, "opened_list",
                "Statement list",
                "Sales Report holds saved account statements. A new statement walks the live four-step generator.",
                $"showList={sales.ShowList}",
                DemoWalk.BeatMs, ct).ConfigureAwait(false);

            await WpfUiDriver.OnUiAsync(() =>
            {
                if (sales.NewStatementCommand.CanExecute(null))
                    sales.NewStatementCommand.Execute(null);
                return true;
            }, ct).ConfigureAwait(false);

            bool wizard = await DemoWalk.WaitUntilAsync(
                () => sales.ShowGenerator && sales.GeneratorVM.CurrentStep == 1,
                6000, ct).ConfigureAwait(false);
            if (!wizard) return events;

            await DemoWalk.PresentAsync(events, "wizard",
                "New statement",
                "New Statement opened the generator. Same command as the list button.",
                $"step={sales.GeneratorVM.CurrentStep}",
                DemoWalk.BeatMs, ct).ConfigureAwait(false);

            await WpfUiDriver.OnUiAsync(() =>
            {
                var g = sales.GeneratorVM;
                g.FilterCustomer = "Skyline Interiors LLC";
                g.FilterCompany = "ProGlass Automation Demo";
                return g.HasFilter;
            }, ct).ConfigureAwait(false);

            await DemoWalk.PresentAsync(events, "filtered",
                "Filters",
                "Customer Skyline Interiors LLC, company ProGlass Automation Demo. Status chips stay on Issued, Paid, Partially Paid and Overdue.",
                $"customer={sales.GeneratorVM.FilterCustomer} hasFilter={sales.GeneratorVM.HasFilter}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            if (sales.GeneratorVM.PreviewCommand.CanExecute(null))
            {
                await DemoWalk.TryCommandAsync(
                    sales.GeneratorVM.PreviewCommand, null, ct)
                    .ConfigureAwait(false);
            }

            await DemoWalk.WaitUntilAsync(
                () => !sales.GeneratorVM.IsLoading, 12_000, ct)
                .ConfigureAwait(false);
            await WpfUiDriver.DwellAsync(800, ct).ConfigureAwait(false);

            await WpfUiDriver.OnUiAsync(() =>
            {
                sales.GeneratorVM.PreviewVM.SelectAll();
                return true;
            }, ct).ConfigureAwait(false);

            if (sales.GeneratorVM.NextStepCommand.CanExecute(null))
            {
                await DemoWalk.TryCommandAsync(
                    sales.GeneratorVM.NextStepCommand, null, ct)
                    .ConfigureAwait(false);
            }

            await DemoWalk.PresentAsync(events, "preview",
                "Preview invoices",
                sales.GeneratorVM.HasPreview
                    ? $"Preview found {sales.GeneratorVM.MatchCount} invoices. {sales.GeneratorVM.PreviewVM.SelectionSummaryDisplay}. Generate is not run here — it can open a warning dialog and freeze the recording."
                    : "Preview ran against the live tax invoices. Generate is skipped so a warning dialog cannot freeze the camera.",
                $"matches={sales.GeneratorVM.MatchCount} selected={sales.GeneratorVM.PreviewVM.SelectedCount} step={sales.GeneratorVM.CurrentStep}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            await WpfUiDriver.DwellAsync(600, ct).ConfigureAwait(false);
            return events;
        }

        private static Task<SalesReportViewModel?> ResolveAsync(CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var main = System.Windows.Application.Current?.MainWindow?.DataContext
                    as ViewModels.MainViewModel;
                return main?.SalesReportVM;
            }, ct);
    }
}
