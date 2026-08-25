using ProGlassAutomation.Agent.Demo.Automation;
using ProGlassAutomation.ViewModels.JobOrder;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Live Job Order: list, open a seeded job, fill production (no Rate),
    /// then the real God View and Activity tabs. No picker, no save MessageBox.
    /// </summary>
    public sealed class JobOrderWorkflow : IDemoWorkflowRunner
    {
        public string ModuleId => "JobOrder";
        public string OutputFileName => "JobOrder_Demo.mp4";
        public IReadOnlyList<string> DeepEvidenceIds { get; } =
            ["opened", "production", "god", "activity"];

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();
            if (!DemoWalk.Navigate("JobOrder")) return events;

            await WpfUiDriver.WaitReadyAsync(500, ct).ConfigureAwait(false);
            var jo = await ResolveAsync(ct).ConfigureAwait(false);
            if (jo == null) return events;

            await WpfUiDriver.OnUiAsync(() =>
            {
                jo.ActiveTab = JobOrderViewModel.TabWorkspace;
                jo.IsListView = true;
                return true;
            }, ct).ConfigureAwait(false);

            await jo.ListVM.LoadJobOrdersAsync().ConfigureAwait(true);
            await DemoWalk.WaitUntilAsync(
                () => !jo.ListVM.IsLoading, 8000, ct).ConfigureAwait(false);

            await DemoWalk.PresentAsync(events, "opened_list",
                "Job Order list",
                $"You are now on the Job Order desk — the shop floor's view of the business. There are {jo.ListVM.TotalCount} jobs currently in production. A Job Order is what a Proforma quotation becomes the moment the customer confirms and pays advance. The important thing to notice is that the floor sheet has no selling price on it — this is deliberate. The cutter and the polisher should focus on what to make, not what the customer paid. Money stays in the Proforma and the Tax Invoice; the workshop only sees quantities, sizes and deadlines.",
                $"jobs={jo.ListVM.TotalCount}",
                DemoWalk.BeatMs, ct).ConfigureAwait(false);

            await WpfUiDriver.OnUiAsync(() =>
            {
                jo.ListVM.SearchText = "Skyline";
                return true;
            }, ct).ConfigureAwait(false);
            await WpfUiDriver.DwellAsync(700, ct).ConfigureAwait(false);

            var row = await WpfUiDriver.OnUiAsync(() =>
                jo.ListVM.DisplayedJobs.FirstOrDefault()
                ?? jo.ListVM.DisplayedJobs.FirstOrDefault(), ct)
                .ConfigureAwait(false);

            if (row != null)
            {
                await WpfUiDriver.OnUiAsync(() =>
                {
                    jo.ListVM.OnJobOrderOpenRequested?.Invoke(row.Id);
                    return true;
                }, ct).ConfigureAwait(false);

                bool opened = await DemoWalk.WaitUntilAsync(
                    () => jo.IsDetailView && !jo.DetailVM.IsLoading
                          && !string.IsNullOrWhiteSpace(jo.DetailVM.JobOrderId),
                    10_000, ct).ConfigureAwait(false);

                if (opened && !jo.DetailVM.HasError)
                {
                    await DemoWalk.PresentAsync(events, "opened",
                        "Open job",
                        $"This is job {jo.DetailVM.JobOrderId}, generated automatically from proforma {jo.DetailVM.SourceProformaId} when it was approved — the whole specification was copied across so nothing has to be retyped. On the left you see the customer and the project name, on the right the quantities per glass reference. There is no rate, no VAT and no grand total anywhere on this sheet — that is by design. This is a manufacturing document, not a financial one.",
                        $"jo={jo.DetailVM.JobOrderId} pi={jo.DetailVM.SourceProformaId}",
                        DemoWalk.HoldMs, ct).ConfigureAwait(false);

                    // The detail has a separate OVERVIEW / PRODUCTION button
                    // tab switch. Move to the Production tab so the narration
                    // shows the editable factory plan, then the workflow ends
                    // and returns to the module shell tabs.
                    await DemoWalk.TryCommandAsync(
                        jo.DetailVM.SwitchDetailTabCommand, "Production", ct)
                        .ConfigureAwait(false);

                    await WpfUiDriver.OnUiAsync(() =>
                    {
                        var d = jo.DetailVM;
                        d.AssignedTo = "Cutting bay 2 — Hassan";
                        d.SiteDeliveryContact = "Eng. Mariam Al Suwaidi";
                        d.SiteDeliveryPhone = "+971 55 320 7781";
                        d.SiteDeliveryAddress = "Al Zahia Tower podium, Sharjah";
                        d.CuttingInstructions =
                            "Polish all edges. Temper 6 millimetre partitions before delivery.";
                        d.DrawingReference = "AZT-POD-26-GL-01";
                        d.InternalNotes =
                            "Podium curtain wall first. Lobby partitions after site measure.";
                        d.DeliveryDate = System.DateTime.Today.AddDays(10);
                        return true;
                    }, ct).ConfigureAwait(false);

                    await DemoWalk.PresentAsync(events, "production",
                        "Production sheet",
                        "The Production tab is where the factory manager plans the work. We switched to it with the separate tab button up top. We assigned this job to Hassan on cutting bay 2 — that name will appear on his daily task list. We entered the site engineer Mariam as the delivery contact so the driver can reach her when the truck arrives. The cutting instruction 'polish all edges, temper 6 mm partitions before delivery' becomes the operator's cheat-sheet on the shop floor. The drawing reference links the physical shop drawing to this job, and the delivery date sets the deadline the whole floor works against. Nowhere on this page do we mention money.",
                        $"assigned={jo.DetailVM.AssignedTo} drawing={jo.DetailVM.DrawingReference}",
                        DemoWalk.HoldMs, ct).ConfigureAwait(false);
                }
            }

            await DemoWalk.TryCommandAsync(
                jo.SwitchTabCommand, JobOrderViewModel.TabGodView, ct)
                .ConfigureAwait(false);
            await DemoWalk.WaitUntilAsync(() => jo.IsGodViewTab, 4000, ct)
                .ConfigureAwait(false);
            await WpfUiDriver.DwellAsync(600, ct).ConfigureAwait(false);
            await DemoWalk.PresentAsync(events, "god",
                "God View",
                "God View shows every job in the factory on a single screen — perfect for a production meeting. You can sort by delivery date to see what is due this week, or filter by cutting bay to see one operator's load. Nothing here is a screenshot or a mock; each row is a live database record you can double-click straight into.",
                $"tab={jo.ActiveTab}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            await DemoWalk.TryCommandAsync(
                jo.SwitchTabCommand, JobOrderViewModel.TabActivity, ct)
                .ConfigureAwait(false);
            await DemoWalk.WaitUntilAsync(() => jo.IsActivityTab, 4000, ct)
                .ConfigureAwait(false);
            jo.ActivityVM.LoadAsync();
            await WpfUiDriver.DwellAsync(800, ct).ConfigureAwait(false);
            await DemoWalk.PresentAsync(events, "activity",
                "Job Order activity",
                $"The Activity tab is the shop-floor audit log — {jo.ActivityVM.Rows.Count} entries showing every time someone opened, edited, reassigned or closed a job. Each line records the user, the machine and the timestamp. This is the trail an owner walks when a dispute happens on the floor: who changed the cutting instruction, when the delivery date moved, why a job was reassigned overnight. It is written directly to the audit database and cannot be edited from the interface.",
                $"rows={jo.ActivityVM.Rows.Count}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            await DemoWalk.TryCommandAsync(
                jo.SwitchTabCommand, JobOrderViewModel.TabWorkspace, ct)
                .ConfigureAwait(false);
            await WpfUiDriver.DwellAsync(500, ct).ConfigureAwait(false);
            return events;
        }

        private static Task<JobOrderViewModel?> ResolveAsync(CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var main = System.Windows.Application.Current?.MainWindow?.DataContext
                    as ViewModels.MainViewModel;
                return main?.JobOrderVM;
            }, ct);
    }
}
