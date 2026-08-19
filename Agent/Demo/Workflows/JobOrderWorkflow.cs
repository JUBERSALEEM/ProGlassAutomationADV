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
                $"The live job list. {jo.ListVM.TotalCount} jobs. A job is converted from an approved proforma — the floor sheet has no selling price.",
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
                        $"{jo.DetailVM.JobOrderId} from {jo.DetailVM.SourceProformaId}. Same Open command as the list row. Production quantities, no rate or total on the floor sheet.",
                        $"jo={jo.DetailVM.JobOrderId} pi={jo.DetailVM.SourceProformaId}",
                        DemoWalk.HoldMs, ct).ConfigureAwait(false);

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
                        "Production tab. Assigned to Hassan on cutting bay 2. Site contact Eng. Mariam. The job sheet never carries a selling rate.",
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
                "God View is the real all-jobs overview for this module — not a storyboard.",
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
                $"Activity is the live audit feed for Job Orders. {jo.ActivityVM.Rows.Count} recent entries from audit.db.",
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
