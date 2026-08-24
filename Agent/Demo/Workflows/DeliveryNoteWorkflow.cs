using ProGlassAutomation.Agent.Demo.Automation;
using ProGlassAutomation.ViewModels.DeliveryNote;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Live Delivery Note: list, open, fill This-DN qty (Prev / This / Balance),
    /// driver fields, History tab, God View, Activity. No Make DN, no picker.
    /// </summary>
    public sealed class DeliveryNoteWorkflow : IDemoWorkflowRunner
    {
        public string ModuleId => "DeliveryNote";
        public string OutputFileName => "DeliveryNote_Demo.mp4";
        public IReadOnlyList<string> DeepEvidenceIds { get; } =
            ["opened", "quantities", "history", "god", "activity"];

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();
            if (!DemoWalk.Navigate("DeliveryNote")) return events;

            await WpfUiDriver.WaitReadyAsync(500, ct).ConfigureAwait(false);
            var dn = await ResolveAsync(ct).ConfigureAwait(false);
            if (dn == null) return events;

            await WpfUiDriver.OnUiAsync(() =>
            {
                dn.ActiveTab = DeliveryNoteViewModel.TabWorkspace;
                dn.IsListView = true;
                return true;
            }, ct).ConfigureAwait(false);

            await dn.ListVM.LoadAsync().ConfigureAwait(true);
            await DemoWalk.WaitUntilAsync(
                () => !dn.ListVM.IsLoading, 8000, ct).ConfigureAwait(false);

            await DemoWalk.PresentAsync(events, "opened_list",
                "Delivery Note list",
                $"This is the Delivery Note desk — where the factory dispatches finished glass to the site. Every row is one dispatch. There are {dn.ListVM.TotalCount} delivery notes on file right now. Notice the three quantity columns: 'previous' shows what was already delivered on earlier trips for this job, 'this DN' is what is going on today's truck, and 'balance' is what is still left in the workshop. Prices are deliberately not on this screen — a delivery note is a proof of movement, not an invoice, and mixing money with dispatch is exactly the mistake that causes disputes.",
                $"dns={dn.ListVM.TotalCount}",
                DemoWalk.BeatMs, ct).ConfigureAwait(false);

            await WpfUiDriver.OnUiAsync(() =>
            {
                dn.ListVM.SearchText = "Skyline";
                return true;
            }, ct).ConfigureAwait(false);
            await WpfUiDriver.DwellAsync(600, ct).ConfigureAwait(false);

            var row = await WpfUiDriver.OnUiAsync(() =>
            {
                return dn.ListVM.AllItems.FirstOrDefault(r =>
                           string.Equals(r.Status, "Dispatching",
                               System.StringComparison.OrdinalIgnoreCase)
                           || string.Equals(r.Status, "Draft",
                               System.StringComparison.OrdinalIgnoreCase))
                       ?? dn.ListVM.AllItems.FirstOrDefault();
            }, ct).ConfigureAwait(false);

            if (row != null)
            {
                await dn.OpenDeliveryNoteAsync(row.Id).ConfigureAwait(true);
                bool opened = await DemoWalk.WaitUntilAsync(
                    () => dn.IsDetailView && !dn.DetailVM.IsLoading
                          && !string.IsNullOrWhiteSpace(dn.DetailVM.DeliveryNoteId),
                    10_000, ct).ConfigureAwait(false);

                if (opened && !dn.DetailVM.HasError)
                {
                    await DemoWalk.PresentAsync(events, "opened",
                        "Open delivery note",
                        $"Here is delivery note {dn.DetailVM.DeliveryNoteId} for {dn.DetailVM.CustomerName}. Look at the running totals at the bottom — the customer originally ordered {dn.DetailVM.TotalOrderedQty} pieces, we have already delivered {dn.DetailVM.TotalPreviouslyDeliveredQty} on previous trips, today's DN is scheduled to deliver {dn.DetailVM.TotalDeliveredQty}, and after this trip {dn.DetailVM.TotalBalanceQty} will still be pending. The system keeps this three-way running tally automatically for every glass reference on the job, so the storekeeper never has to add anything on a calculator.",
                        $"dn={dn.DetailVM.DeliveryNoteId} prev={dn.DetailVM.TotalPreviouslyDeliveredQty} this={dn.DetailVM.TotalDeliveredQty} bal={dn.DetailVM.TotalBalanceQty}",
                        DemoWalk.HoldMs, ct).ConfigureAwait(false);

                    if (!dn.DetailVM.IsMasterDn
                        && dn.DetailVM.FillRemainingCommand.CanExecute(null))
                    {
                        await DemoWalk.TryCommandAsync(
                            dn.DetailVM.FillRemainingCommand, null, ct)
                            .ConfigureAwait(false);
                    }

                    await WpfUiDriver.OnUiAsync(() =>
                    {
                        dn.DetailVM.DriverName = "Salim Khan";
                        dn.DetailVM.VehicleNumber = "SHJ-48211";
                        dn.DetailVM.ReceiverName = "Site storekeeper";
                        dn.DetailVM.Notes = "Podium glass first drop. Leave balance for lobby.";
                        return true;
                    }, ct).ConfigureAwait(false);

                    await DemoWalk.PresentAsync(events, "quantities",
                        "This DN quantities",
                        $"Now the storekeeper fills the trip. One click on 'Fill remaining' loads today's DN with everything still in balance — no more manual retyping. We also enter the driver's name, Salim Khan, and the vehicle plate SHJ-48211, so the site can match the delivery on arrival. The three quantity columns update instantly as we type, but you will still not see any rate or amount here — that stays on the tax invoice, where it belongs. This clean separation is what keeps our production and finance data honest.",
                        $"this={dn.DetailVM.TotalDeliveredQty} bal={dn.DetailVM.TotalBalanceQty} driver={dn.DetailVM.DriverName}",
                        DemoWalk.HoldMs, ct).ConfigureAwait(false);

                    await DemoWalk.TryCommandAsync(
                        dn.DetailVM.SwitchDetailTabCommand, "History", ct)
                        .ConfigureAwait(false);
                    await DemoWalk.PresentAsync(events, "history",
                        "Delivery history",
                        $"The History tab is a life story of this job on the loading dock. For job {dn.DetailVM.JobOrderId} we can see the original order was {dn.DetailVM.HistoryTotalOrdered} pieces, of which {dn.DetailVM.HistoryTotalDelivered} have been delivered across earlier trips, leaving {dn.DetailVM.HistoryTotalBalance} still in the workshop. Every earlier DN is listed with its date, driver and vehicle, so when the customer calls asking 'where is my glass' the answer is one click away — no chasing paperwork through the office.",
                        $"entries={dn.DetailVM.HistoryEntries.Count}",
                        DemoWalk.HoldMs, ct).ConfigureAwait(false);

                    await DemoWalk.TryCommandAsync(
                        dn.DetailVM.SwitchDetailTabCommand, "Attachments", ct)
                        .ConfigureAwait(false);
                    await WpfUiDriver.DwellAsync(700, ct).ConfigureAwait(false);
                    await DemoWalk.TryCommandAsync(
                        dn.DetailVM.SwitchDetailTabCommand, "Overview", ct)
                        .ConfigureAwait(false);
                }
            }

            await DemoWalk.TryCommandAsync(
                dn.SwitchTabCommand, DeliveryNoteViewModel.TabGodView, ct)
                .ConfigureAwait(false);
            await DemoWalk.WaitUntilAsync(() => dn.IsGodViewTab, 4000, ct)
                .ConfigureAwait(false);
            await DemoWalk.PresentAsync(events, "god",
                "God View",
                "God View is what a supervisor opens to see everything at once — every delivery note across every job and every customer, sortable and filterable. Behind the scenes there are also 'Master DNs' which are internal batching records, but those stay hidden from the clerk view to keep the everyday screen clean. This is the tab a warehouse manager checks first thing in the morning to see what left the yard yesterday.",
                $"tab={dn.ActiveTab}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            await DemoWalk.TryCommandAsync(
                dn.SwitchTabCommand, DeliveryNoteViewModel.TabActivity, ct)
                .ConfigureAwait(false);
            await DemoWalk.WaitUntilAsync(() => dn.IsActivityTab, 4000, ct)
                .ConfigureAwait(false);
            dn.ActivityVM.LoadAsync();
            await WpfUiDriver.DwellAsync(700, ct).ConfigureAwait(false);
            await DemoWalk.PresentAsync(events, "activity",
                "Delivery activity",
                $"The Activity tab is the audit trail — a permanent log of every action taken on any delivery note. Right now there are {dn.ActivityVM.Rows.Count} entries showing who created, edited or cancelled a DN, when they did it and from which machine. This is what auditors and finance managers ask for during month-end review. Nothing here can be deleted from the interface — it is written directly to the audit database and kept forever.",
                $"rows={dn.ActivityVM.Rows.Count}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            await DemoWalk.TryCommandAsync(
                dn.SwitchTabCommand, DeliveryNoteViewModel.TabWorkspace, ct)
                .ConfigureAwait(false);
            await WpfUiDriver.DwellAsync(400, ct).ConfigureAwait(false);
            return events;
        }

        private static Task<DeliveryNoteViewModel?> ResolveAsync(CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var main = System.Windows.Application.Current?.MainWindow?.DataContext
                    as ViewModels.MainViewModel;
                return main?.DeliveryNoteVM;
            }, ct);
    }
}
