using ProGlassAutomation.Agent.Demo.Automation;
using ProGlassAutomation.ViewModels.TaxInvoice;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Live Tax Invoice: list, open or import from PI (same InitNew path
    /// as Create after the picker), show Rate and Value, family, God, Activity.
    /// No file picker, no Issue/Paid MessageBox.
    /// </summary>
    public sealed class TaxInvoiceWorkflow : IDemoWorkflowRunner
    {
        public string ModuleId => "TaxInvoice";
        public string OutputFileName => "TaxInvoice_Demo.mp4";
        public IReadOnlyList<string> DeepEvidenceIds { get; } =
            ["opened", "rates", "family", "god", "activity"];

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();
            if (!DemoWalk.Navigate("TaxInvoice")) return events;

            await WpfUiDriver.WaitReadyAsync(500, ct).ConfigureAwait(false);
            var ti = await ResolveAsync(ct).ConfigureAwait(false);
            if (ti == null) return events;

            await WpfUiDriver.OnUiAsync(() =>
            {
                ti.ActiveTab = TaxInvoiceViewModel.TabWorkspace;
                ti.IsListView = true;
                return true;
            }, ct).ConfigureAwait(false);

            await ti.ListVM.LoadAsync().ConfigureAwait(true);
            await DemoWalk.WaitUntilAsync(
                () => !ti.ListVM.IsLoading, 8000, ct).ConfigureAwait(false);

            await DemoWalk.PresentAsync(events, "opened_list",
                "Tax Invoice list",
                $"Live tax invoices. {ti.ListVM.AllCount} families. An FTA invoice uses dispatched pieces and keeps rate and value as separate columns.",
                $"families={ti.ListVM.AllCount} invoiced={ti.ListVM.TotalInvoicedAmount:0.00}",
                DemoWalk.BeatMs, ct).ConfigureAwait(false);

            var family = await WpfUiDriver.OnUiAsync(() =>
                ti.ListVM.AllFamilies.FirstOrDefault(), ct).ConfigureAwait(false);

            bool opened = false;
            if (family != null && family.LatestInvoiceId > 0)
            {
                await WpfUiDriver.OnUiAsync(() =>
                {
                    ti.ListVM.SelectedFamily = family;
                    ti.ListVM.OnInvoiceOpenRequested?.Invoke(family.LatestInvoiceId);
                    return true;
                }, ct).ConfigureAwait(false);

                opened = await DemoWalk.WaitUntilAsync(
                    () => ti.IsDetailView && !ti.DetailVM.IsLoading
                          && !ti.DetailVM.IsNewInvoice,
                    10_000, ct).ConfigureAwait(false);
            }

            if (!opened)
            {
                string yearPi = $"PI-{System.DateTime.Now.Year}-001";
                await ti.DetailVM.InitNewAsync(yearPi).ConfigureAwait(true);
                await WpfUiDriver.OnUiAsync(() =>
                {
                    ti.IsListView = false;
                    return true;
                }, ct).ConfigureAwait(false);
                opened = await DemoWalk.WaitUntilAsync(
                    () => ti.IsDetailView && !ti.DetailVM.IsLoading
                          && ti.DetailVM.SpecGroups.Count > 0,
                    10_000, ct).ConfigureAwait(false);
            }

            if (opened)
            {
                await DemoWalk.PresentAsync(events, "opened",
                    "Open tax invoice",
                    $"{ti.DetailVM.PageTitle} for {ti.DetailVM.CustomerName}. Project {ti.DetailVM.ProjectIdentifierName}. Same open path as the family row.",
                    $"inv={ti.DetailVM.EditingInvoiceNumber} pi={ti.DetailVM.ProformaId} status={ti.DetailVM.Status}",
                    DemoWalk.HoldMs, ct).ConfigureAwait(false);

                await WpfUiDriver.OnUiAsync(() =>
                {
                    ti.DetailVM.RecalculateFinancials();
                    return true;
                }, ct).ConfigureAwait(false);

                var rateLine = await WpfUiDriver.OnUiAsync(() =>
                {
                    var row = ti.DetailVM.SpecGroups
                        .SelectMany(g => g.Rows)
                        .FirstOrDefault();
                    return row == null
                        ? "none"
                        : $"{row.GlassRef} qty {row.Qty} rate {row.Rate:0.00}";
                }, ct).ConfigureAwait(false);

                await DemoWalk.PresentAsync(events, "rates",
                    "Rate and value",
                    $"FTA layout keeps rate and value. {rateLine}. Subtotal {ti.DetailVM.Subtotal:0.00}, VAT {ti.DetailVM.VatAmount:0.00}, grand {ti.DetailVM.GrandTotal:0.00}.",
                    $"line={rateLine} grand={ti.DetailVM.GrandTotal:0.00}",
                    DemoWalk.HoldMs, ct).ConfigureAwait(false);

                await DemoWalk.TryCommandAsync(
                    ti.DetailVM.SwitchDetailTabCommand, "Family", ct)
                    .ConfigureAwait(false);
                await DemoWalk.PresentAsync(events, "family",
                    "Invoice family",
                    $"Family of {ti.DetailVM.FamilyMemberCount}. Invoiced {ti.DetailVM.FamilyInvoicedTotal:0.00}, balance {ti.DetailVM.FamilyBalanceTotal:0.00}.",
                    $"members={ti.DetailVM.FamilyMemberCount}",
                    DemoWalk.HoldMs, ct).ConfigureAwait(false);

                await DemoWalk.TryCommandAsync(
                    ti.DetailVM.SwitchDetailTabCommand, "Attachments", ct)
                    .ConfigureAwait(false);
                await WpfUiDriver.DwellAsync(600, ct).ConfigureAwait(false);
                await DemoWalk.TryCommandAsync(
                    ti.DetailVM.SwitchDetailTabCommand, "Overview", ct)
                    .ConfigureAwait(false);
            }

            await DemoWalk.TryCommandAsync(
                ti.SwitchTabCommand, TaxInvoiceViewModel.TabGodView, ct)
                .ConfigureAwait(false);
            await DemoWalk.WaitUntilAsync(() => ti.IsGodViewTab, 4000, ct)
                .ConfigureAwait(false);
            await DemoWalk.PresentAsync(events, "god",
                "God View",
                "God View is every tax invoice in the isolated demo folder.",
                $"tab={ti.ActiveTab}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            await DemoWalk.TryCommandAsync(
                ti.SwitchTabCommand, TaxInvoiceViewModel.TabActivity, ct)
                .ConfigureAwait(false);
            await DemoWalk.WaitUntilAsync(() => ti.IsActivityTab, 4000, ct)
                .ConfigureAwait(false);
            ti.ActivityVM.LoadAsync();
            await WpfUiDriver.DwellAsync(700, ct).ConfigureAwait(false);
            await DemoWalk.PresentAsync(events, "activity",
                "Tax Invoice activity",
                $"Activity is the live Tax Invoice audit feed. {ti.ActivityVM.Rows.Count} entries.",
                $"rows={ti.ActivityVM.Rows.Count}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            await DemoWalk.TryCommandAsync(
                ti.SwitchTabCommand, TaxInvoiceViewModel.TabWorkspace, ct)
                .ConfigureAwait(false);
            await WpfUiDriver.DwellAsync(400, ct).ConfigureAwait(false);
            return events;
        }

        private static Task<TaxInvoiceViewModel?> ResolveAsync(CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var main = System.Windows.Application.Current?.MainWindow?.DataContext
                    as ViewModels.MainViewModel;
                return main?.TaxInvoiceVM;
            }, ct);
    }
}
