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
                $"Now we are on the Tax Invoice desk — the module the accountant uses to convert delivered glass into money. There are {ti.ListVM.AllCount} invoice families on file. The word 'family' matters here: for one job we usually issue several tax invoices — one for the first shipment, one for the second, and so on — and the system groups them under the parent proforma so nothing goes missing. Every invoice you see here follows the UAE FTA layout: rate per square metre and total value are shown as separate columns, five percent VAT is calculated line by line, and each invoice is billed only against pieces that were actually dispatched.",
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
                    $"Here is {ti.DetailVM.PageTitle}, raised for {ti.DetailVM.CustomerName} against the project {ti.DetailVM.ProjectIdentifierName}. Notice the company letterhead at the top — the same one we saw in Company Center — followed by the customer's tax registration number, the invoice number in the FTA-compliant format, and the invoice date. Every field required by the UAE VAT law is present and locked in the correct format. This is not a Word template — it is the real system record, so the numbers you see are the numbers the government will see on the return.",
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
                    $"Now look at how each line reads: the FTA layout keeps rate and value as separate columns so an auditor can immediately see what unit price we charged. This line shows {rateLine}, meaning we are billing that many square metres at that price. At the bottom the system computes the subtotal at {ti.DetailVM.Subtotal:0.00}, adds five percent VAT giving {ti.DetailVM.VatAmount:0.00}, and lands on a grand total of {ti.DetailVM.GrandTotal:0.00}. Nothing here is hand-typed — every figure comes from the delivery notes we already dispatched, so under-billing or over-billing is prevented at source.",
                    $"line={rateLine} grand={ti.DetailVM.GrandTotal:0.00}",
                    DemoWalk.HoldMs, ct).ConfigureAwait(false);

                await DemoWalk.TryCommandAsync(
                    ti.DetailVM.SwitchDetailTabCommand, "Family", ct)
                    .ConfigureAwait(false);
                await DemoWalk.PresentAsync(events, "family",
                    "Invoice family",
                    $"The Family tab is one of the reasons this module was built. For this job we have {ti.DetailVM.FamilyMemberCount} invoices under the same parent proforma. Together they have billed {ti.DetailVM.FamilyInvoicedTotal:0.00}, and {ti.DetailVM.FamilyBalanceTotal:0.00} of the original quote is still to be invoiced. This is what tells the accountant, without a spreadsheet, whether we have under-billed, over-billed or completed the invoicing for a project.",
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
                "God View is the finance manager's screen — every tax invoice we have ever issued, sortable by customer, project, date and status. From here you spot at a glance who is overdue, which invoices are still awaiting payment and which have been cleared. In this demo you are looking at the isolated demo folder so no real customer data is exposed.",
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
                $"The Activity tab is the audit trail for finance — {ti.ActivityVM.Rows.Count} entries recording every time an invoice was issued, edited, marked paid or cancelled. Because tax invoices are legal documents, this log is treated as evidence: no one can delete or hide an entry from the interface. During a VAT audit this is the first screen the accountant opens.",
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
