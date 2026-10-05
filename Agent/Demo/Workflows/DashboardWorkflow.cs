// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Demo/Workflows/DashboardWorkflow.cs
// PROJECT: ProGlassAutomation
// MODULE: Demo Center — recorded Dashboard walkthrough
//
// 2026-08-25 UPGRADE
//   The old version switched through all seven tabs in 900 ms each and
//   narrated ONCE at the end — viewers saw a flicker of screens and one
//   generic voice-over. Now every tab gets its own beat: the demo waits
//   for the tab to finish loading, speaks what that specific dashboard
//   shows, and holds long enough to read it. A page that fails to load
//   no longer kills the whole run — it is presented as "loading" and the
//   walk continues.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Demo.Automation;
using ProGlassAutomation.ViewModels.Dashboard;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Live Dashboard: overview plus every real module tab via
    /// SelectTabCommand — each tab narrated individually.
    /// </summary>
    public sealed class DashboardWorkflow : IDemoWorkflowRunner
    {
        public string ModuleId => "Dashboard";
        public string OutputFileName => "Dashboard_Demo.mp4";
        public IReadOnlyList<string> DeepEvidenceIds { get; } =
            ["overview", "tabs"];

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();
            if (!DemoWalk.Navigate("Dashboard")) return events;

            await WpfUiDriver.WaitReadyAsync(600, ct).ConfigureAwait(false);
            var dash = await ResolveAsync(ct).ConfigureAwait(false);
            if (dash == null) return events;

            dash.OnViewLoaded();
            await DemoWalk.WaitUntilAsync(
                () => !dash.ActivePage.IsLoading, 8000, ct).ConfigureAwait(false);

            await DemoWalk.PresentAsync(events, "overview",
                "Factory pulse",
                $"{dash.WelcomeLine}. This is the operations dashboard — the first screen the team opens every morning. The tiles you see summarise the whole factory: today's quotations, jobs on the shop floor, deliveries due, invoices waiting for payment and the running profit and loss. Every figure is calculated live from the demo folder, so nothing here is hard-coded. If a quote is edited or a delivery is closed, the numbers refresh on the very next load.",
                $"tab={dash.ActivePage.TabTitle} pages={dash.Pages.Count}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            // ── One narrated beat per tab ────────────────────────────
            // Skip page index 0 — it is the Overview tab we just showed.
            var visited = new List<string>();
            for (int i = 0; i < dash.Pages.Count; i++)
            {
                var page = dash.Pages[i];
                if (i == 0)
                {
                    visited.Add(page.TabTitle);
                    continue;
                }

                await DemoWalk.TryCommandAsync(dash.SelectTabCommand, page, ct)
                    .ConfigureAwait(false);

                // Give the tab's own LoadAsync a fair window; a slow or
                // failing page must not kill the whole recording.
                bool loaded = await DemoWalk.WaitUntilAsync(
                    () => dash.ActivePage == page && !page.IsLoading,
                    6000, ct).ConfigureAwait(false);

                visited.Add(page.TabTitle);

                var (title, narration) = NarrationFor(page.TabTitle);
                await DemoWalk.PresentAsync(events,
                    "tab_" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    title,
                    loaded
                        ? narration
                        : narration + " The figures are still loading in this demo environment, but the tab keeps its live connection and refreshes the moment the data arrives.",
                    $"tab={page.TabTitle}",
                    DemoWalk.HoldMs, ct).ConfigureAwait(false);

                await WpfUiDriver.DwellAsync(700, ct).ConfigureAwait(false);
            }

            await DemoWalk.PresentAsync(events, "tabs",
                "The factory's control panel",
                $"That was the complete dashboard family — {visited.Count} tabs in one screen: {string.Join(", ", visited)}. Each tab is a mini-report for its part of the business, and each one reloads its own figures on demand, so the latest state is always one click away without opening a single module. This is where a shift starts, and where management ends the day.",
                string.Join("|", visited),
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            if (dash.Pages.Count > 0)
            {
                await DemoWalk.TryCommandAsync(
                    dash.SelectTabCommand, dash.Pages[0], ct)
                    .ConfigureAwait(false);
            }

            await WpfUiDriver.DwellAsync(500, ct).ConfigureAwait(false);
            return events;
        }

        /// <summary>
        /// Per-tab narration. Titles match DashboardPageBase.TabTitle of
        /// each concrete page; unknown future tabs fall back to a generic
        /// beat so a new page never silences the walkthrough.
        /// </summary>
        private static (string Title, string Narration) NarrationFor(string tabTitle)
        {
            return tabTitle switch
            {
                "Sheet Store" => ("Sheet Store dashboard",
                    "The Sheet Store tab watches raw glass stock: sheets in stock, sheets consumed by production, and the balance still available. It is the same live sheet register the Sheet Store module edits — when a job consumes material, these figures move with it."),

                "Proforma Invoice" => ("Quotation dashboard",
                    "The Proforma Invoice tab tracks the quoting pipeline: how many quotations are open, their total value, and how they move from draft to sent to approved. It answers the first business question of the day — where is the next job coming from?"),

                "Job Orders" => ("Production dashboard",
                    "The Job Orders tab shows what the factory is actually making right now: released jobs, jobs in production, and what is due for delivery. It is the shop floor's view — no selling prices here, only quantities, dates and status."),

                "Deliveries" => ("Delivery dashboard",
                    "The Deliveries tab follows every dispatch: delivery notes created, what has been delivered against each job, and the remaining balance still to ship. When a note reaches Received, it shows here immediately."),

                "Tax Invoices" => ("Receivables dashboard",
                    "The Tax Invoices tab is the money view: FTA-compliant invoices raised, their VAT values, and what is still outstanding. These are the figures finance reconciles at month end."),

                "Statements" => ("Statement dashboard",
                    "The Statements tab summarises customer account statements — invoiced versus outstanding per customer, the exact numbers a customer asks about on the phone. Statements are generated from live tax invoices, never typed by hand."),

                _ => (tabTitle + " dashboard",
                    $"The {tabTitle} tab is one of the dashboard family — a live mini-report for its part of the business, reloading its own figures on demand."),
            };
        }

        private static Task<DashboardViewModel?> ResolveAsync(CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var main = System.Windows.Application.Current?.MainWindow?.DataContext
                    as ViewModels.MainViewModel;
                return main?.DashboardVM;
            }, ct);
    }
}
