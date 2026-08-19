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
                $"Live delivery notes. {dn.ListVM.TotalCount} notes. Columns are previous, this DN and balance — never rate or total.",
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
                        $"{dn.DetailVM.DeliveryNoteId} for {dn.DetailVM.CustomerName}. Ordered {dn.DetailVM.TotalOrderedQty}, previous {dn.DetailVM.TotalPreviouslyDeliveredQty}, this DN {dn.DetailVM.TotalDeliveredQty}, balance {dn.DetailVM.TotalBalanceQty}.",
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
                        $"Select all remaining filled This-DN. Driver Salim Khan, vehicle SHJ-48211. Previous, this DN and balance stay on the grid. Rate is not a delivery column.",
                        $"this={dn.DetailVM.TotalDeliveredQty} bal={dn.DetailVM.TotalBalanceQty} driver={dn.DetailVM.DriverName}",
                        DemoWalk.HoldMs, ct).ConfigureAwait(false);

                    await DemoWalk.TryCommandAsync(
                        dn.DetailVM.SwitchDetailTabCommand, "History", ct)
                        .ConfigureAwait(false);
                    await DemoWalk.PresentAsync(events, "history",
                        "Delivery history",
                        $"History for {dn.DetailVM.JobOrderId}. Ordered {dn.DetailVM.HistoryTotalOrdered}, delivered {dn.DetailVM.HistoryTotalDelivered}, remaining {dn.DetailVM.HistoryTotalBalance}.",
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
                "God View lists every delivery note. Master DNs stay internal — this is the clerk overview.",
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
                $"Activity is the live Delivery Note audit feed. {dn.ActivityVM.Rows.Count} entries.",
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
