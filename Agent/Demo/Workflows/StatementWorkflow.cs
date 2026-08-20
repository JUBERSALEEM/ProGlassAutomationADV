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
                "The statements section stores every account statement we have generated for customers. A statement is a summary of issued tax invoices, payments received and outstanding balances — essentially a customer's account history. Creating a new statement walks through a four-step generator wizard.",
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
                "The statement generator opens with four steps: filter which invoices to include, preview the selection, execute the statement and review the result. It works the same way whether you are generating a statement for one customer or grouping multiple customers into a single statement run.",
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
                "Statement filters",
                "We set which invoices to include — by customer, by company, or by date range. The status chips at the top show the current mix of issued, paid, partially paid and overdue invoices so you can see at a glance what the statement will cover before generating it.",
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
                    ? $"The preview shows which invoices match our filter — {sales.GeneratorVM.MatchCount} invoices found. We can select or deselect individual invoices before generating the statement. The generate step is skipped in this demo to avoid confirmation dialogs, but in a real session you would click Generate and the statement PDF would be created automatically."
                    : "The preview step lists every matching invoice. From here you select the ones to include and click Generate to produce the statement PDF.",
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
