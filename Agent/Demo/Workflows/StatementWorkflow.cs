using ProGlassAutomation.Agent.Demo.Automation;
using ProGlassAutomation.ViewModels.Reports;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Live statement generator: new statement, customer filter, preview,
    /// select all, then run the pipeline (Execute) and land on Review.
    /// In demo mode the missing-invoices decision auto-resolves so the
    /// pipeline never blocks on a modal dialog.
    /// </summary>
    public sealed class StatementWorkflow : IDemoWorkflowRunner
    {
        public string ModuleId => "SalesReport";
        public string OutputFileName => "Statement_Demo.mp4";
        public IReadOnlyList<string> DeepEvidenceIds { get; } =
            ["wizard", "filtered", "preview", "execute", "review"];

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();
            if (!DemoWalk.Navigate("StatementGenerator")) return events;

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
                "The statement generator opens with four steps: filter which invoices to include, preview the selection, execute the pipeline and review the result. It works the same way whether you are generating a statement for one customer or grouping multiple customers into a single statement run.",
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

            await DemoWalk.PresentAsync(events, "preview",
                "Preview invoices",
                sales.GeneratorVM.HasPreview
                    ? $"The preview shows which invoices match our filter — {sales.GeneratorVM.MatchCount} invoices found. We can select or deselect individual invoices before generating the statement."
                    : "The preview step lists every matching invoice. From here you select the ones to include before generating the statement.",
                $"matches={sales.GeneratorVM.MatchCount} selected={sales.GeneratorVM.PreviewVM.SelectedCount} step={sales.GeneratorVM.CurrentStep}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            // ── Step 3: Execute (pipeline auto-runs on entry) ──────────────
            if (sales.GeneratorVM.NextStepCommand.CanExecute(null))
            {
                await DemoWalk.TryCommandAsync(
                    sales.GeneratorVM.NextStepCommand, null, ct)
                    .ConfigureAwait(false);
            }

            bool executed = await DemoWalk.WaitUntilAsync(
                () => sales.GeneratorVM.IsStep3Complete
                    || sales.GeneratorVM.HasLastPdf,
                60_000, ct).ConfigureAwait(false);

            await WpfUiDriver.DwellAsync(1400, ct).ConfigureAwait(false);

            await DemoWalk.PresentAsync(events, "execute",
                "Execute pipeline",
                "The execute step runs an eight-step workflow — reading job orders and delivery notes, validating quantities, cross-matching tax invoices, and merging the statement PDF — while the processing log streams every step live. Any uninvoiced deliveries are flagged as warnings so nothing is silently dropped.",
                $"completed={sales.GeneratorVM.IsStep3Complete} step={sales.GeneratorVM.CurrentStep}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            // ── Step 4: Review (auto-advances when the pipeline finishes) ──
            bool reviewed = await DemoWalk.WaitUntilAsync(
                () => sales.GeneratorVM.CurrentStep == 4,
                10_000, ct).ConfigureAwait(false);

            await WpfUiDriver.DwellAsync(1000, ct).ConfigureAwait(false);

            if (reviewed)
            {
                await DemoWalk.PresentAsync(events, "review",
                    "Review statement",
                    sales.GeneratorVM.HasLastPdf
                        ? "The review step confirms the merged PDF is ready. You can download it, copy its file path, or browse the sections inside the report — merged pages, balance, and any alerts — before finishing."
                        : "The review step shows the generation result, with the merged PDF, its sections, and any warnings produced during the run.",
                    $"hasPdf={sales.GeneratorVM.HasLastPdf} step={sales.GeneratorVM.CurrentStep}",
                    DemoWalk.HoldMs, ct).ConfigureAwait(false);
            }

            await WpfUiDriver.DwellAsync(600, ct).ConfigureAwait(false);
            return events;
        }

        private static Task<StatementModuleViewModel?> ResolveAsync(CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var main = System.Windows.Application.Current?.MainWindow?.DataContext
                    as ViewModels.MainViewModel;
                return main?.StatementModuleVM;
            }, ct);
    }
}
