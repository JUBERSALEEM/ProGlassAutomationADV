using ProGlassAutomation.Agent.Demo.Automation;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.ProformaInvoice;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Walks the live Proforma Invoice wizard the same way a user does:
    /// New Invoice command, Continue button, bound fields, draft save.
    /// Only successful actions are returned as verified events.
    /// </summary>
    public sealed class ProformaInvoiceWorkflow : IDemoWorkflowRunner
    {
        public const string Customer = "Demo Glass Contractor";
        public const string Project = "Demo Tower Fit-Out";
        public const string Company = "ProGlass Automation Demo";
        public const string CompanyTrn = "100234567800003";
        public const string CompanyLocation = "Sharjah, UAE";
        public const string GlassRef = "CLR-06";
        public const int WidthMm = 1200;
        public const int HeightMm = 900;
        public const int Quantity = 4;
        public const decimal Rate = 150m;

        public string ModuleId => "ProformaInvoice";
        public string OutputFileName => "ProformaInvoice_Demo.mp4";

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();

            if (!Navigate("ProformaInvoice"))
            {
                return events;
            }
            await WpfUiDriver.WaitReadyAsync(500, ct).ConfigureAwait(false);
            events.Add(Ok(
                "opened_list",
                "Proforma Invoice list",
                "The Proforma Invoice module is open on the live list.",
                "AgentHost.Navigate(ProformaInvoice)"));

            var pi = await ResolvePiAsync(ct).ConfigureAwait(false);
            if (pi == null) return events;

            bool started = await WpfUiDriver
                .ClickAsync("ProformaInvoice.NewInvoiceButton", ct)
                .ConfigureAwait(false);
            if (!started)
            {
                started = await OnUiAsync(() =>
                {
                    if (pi.NewInvoiceCommand.CanExecute(null))
                    {
                        pi.NewInvoiceCommand.Execute(null);
                        return true;
                    }
                    return false;
                }, ct).ConfigureAwait(false);
            }

            bool wizard = await WaitUntilAsync(
                () => pi.IsWizardView, 10_000, ct).ConfigureAwait(false);
            if (!started || !wizard) return events;

            await WpfUiDriver.WaitReadyAsync(400, ct).ConfigureAwait(false);
            events.Add(Ok(
                "new_invoice",
                "New invoice",
                "A new proforma was started with the same New Invoice command the button uses.",
                "NewInvoiceCommand / ProformaInvoice.NewInvoiceButton"));

            if (await FillCompanyAsync(pi, ct).ConfigureAwait(false))
            {
                events.Add(Ok(
                    "company",
                    "Company details",
                    $"Company is set to {Company} in Sharjah, with the demo TRN.",
                    $"Step1VM.CompanyName={pi.Step1VM.CompanyName}"));
            }

            if (!await ContinueToAsync(pi, 2, ct).ConfigureAwait(false)) return events;
            events.Add(Ok(
                "step2",
                "Client step",
                "Continue moved the wizard to the client profile step.",
                "NextStepCommand / ProformaInvoice.ContinueButton → step 2"));

            if (await FillCustomerAsync(pi, ct).ConfigureAwait(false))
            {
                events.Add(Ok(
                    "customer",
                    "Customer",
                    $"The customer is {Customer}.",
                    $"Step2VM.CustomerName={pi.Step2VM.CustomerName}"));
            }

            if (!await ContinueToAsync(pi, 3, ct).ConfigureAwait(false)) return events;
            events.Add(Ok(
                "step3",
                "Project step",
                "Continue opened the project specifications step.",
                "NextStepCommand → step 3"));

            if (await FillProjectAsync(pi, ct).ConfigureAwait(false))
            {
                events.Add(Ok(
                    "project",
                    "Project",
                    $"The project is {Project}.",
                    $"Step3VM.ProjectIdentifierName={pi.Step3VM.ProjectIdentifierName}"));
            }

            if (!await ContinueToAsync(pi, 4, ct).ConfigureAwait(false)) return events;
            await WpfUiDriver.WaitReadyAsync(350, ct).ConfigureAwait(false);
            events.Add(Ok(
                "step4",
                "Glass type step",
                "The glass type and estimator step is on screen.",
                "NextStepCommand → step 4"));

            if (!await ContinueToAsync(pi, 5, ct).ConfigureAwait(false)) return events;
            events.Add(Ok(
                "step5",
                "Data grid",
                "The specification grid is open. Rate and value stay on a proforma line.",
                "NextStepCommand → step 5"));

            if (await FillLineAsync(pi, ct).ConfigureAwait(false))
            {
                events.Add(Ok(
                    "line",
                    "Glass line",
                    $"{GlassRef}: {WidthMm} by {HeightMm} millimetres, quantity {Quantity}, rate {Rate:0}.",
                    $"W1={WidthMm} H1={HeightMm} Qty={Quantity} Rate={Rate} Ref={GlassRef}"));
            }

            if (!await ContinueToAsync(pi, 6, ct).ConfigureAwait(false)) return events;
            await WpfUiDriver.WaitReadyAsync(300, ct).ConfigureAwait(false);
            events.Add(Ok(
                "step6",
                "Other charges",
                "Other charges are left empty for this quotation.",
                "NextStepCommand → step 6"));

            if (!await ContinueToAsync(pi, 7, ct).ConfigureAwait(false)) return events;
            await WpfUiDriver.WaitReadyAsync(500, ct).ConfigureAwait(false);
            events.Add(Ok(
                "preview",
                "Print preview",
                "Print preview shows the live quotation the rest of the chain will read.",
                "NextStepCommand → step 7"));

            bool saved = await SaveDraftAsync(pi, ct).ConfigureAwait(false);

            if (saved)
            {
                events.Add(Ok(
                    "saved",
                    "Draft saved",
                    "The proforma was persisted with SilentDraftSave — the same save the wizard uses when leaving the form.",
                    $"ProformaId={pi.Step1VM.ProformaId}"));
            }

            return events;
        }

        private static async Task<bool> FillCompanyAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            bool typed = await WpfUiDriver
                .SetTextAsync("ProformaInvoice.CompanyName", Company, ct)
                .ConfigureAwait(false);
            await OnUiAsync(() =>
            {
                if (!typed || string.IsNullOrWhiteSpace(pi.Step1VM.CompanyName))
                    pi.Step1VM.CompanyName = Company;
                pi.Step1VM.CompanyTrn = CompanyTrn;
                pi.Step1VM.CompanyLocation = CompanyLocation;
                return true;
            }, ct).ConfigureAwait(false);
            return string.Equals(pi.Step1VM.CompanyName, Company, StringComparison.Ordinal);
        }

        private static async Task<bool> FillCustomerAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            bool typed = await WpfUiDriver
                .SetTextAsync("ProformaInvoice.CustomerName", Customer, ct)
                .ConfigureAwait(false);
            await OnUiAsync(() =>
            {
                if (!typed || string.IsNullOrWhiteSpace(pi.Step2VM.CustomerName))
                    pi.Step2VM.CustomerName = Customer;
                return true;
            }, ct).ConfigureAwait(false);
            return string.Equals(pi.Step2VM.CustomerName, Customer, StringComparison.Ordinal);
        }

        private static async Task<bool> FillProjectAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            bool typed = await WpfUiDriver
                .SetTextAsync("ProformaInvoice.ProjectName", Project, ct)
                .ConfigureAwait(false);
            await OnUiAsync(() =>
            {
                if (!typed || string.IsNullOrWhiteSpace(pi.Step3VM.ProjectIdentifierName))
                    pi.Step3VM.ProjectIdentifierName = Project;
                return true;
            }, ct).ConfigureAwait(false);
            return string.Equals(
                pi.Step3VM.ProjectIdentifierName, Project, StringComparison.Ordinal);
        }

        private static async Task<bool> FillLineAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            return await OnUiAsync(() =>
            {
                var group = pi.Step5VM.SpecGroups.FirstOrDefault();
                if (group == null) return false;

                if (group.ItemRows.Count == 0
                    && group.AddRowCommand.CanExecute(null))
                {
                    group.AddRowCommand.Execute(null);
                }

                var row = group.ItemRows.FirstOrDefault();
                if (row == null) return false;

                row.GlassRef = GlassRef;
                row.W1 = WidthMm;
                row.H1 = HeightMm;
                row.Qty = Quantity;
                row.Rate = Rate;
                return row.W1 == WidthMm
                    && row.H1 == HeightMm
                    && row.Qty == Quantity
                    && string.Equals(row.GlassRef, GlassRef, StringComparison.Ordinal);
            }, ct).ConfigureAwait(false);
        }

        private static async Task<bool> ContinueToAsync(
            ProformaInvoiceViewModel pi, int step, CancellationToken ct)
        {
            if (pi.CurrentStepNumber == step) return true;

            bool clicked = await WpfUiDriver
                .ClickAsync("ProformaInvoice.ContinueButton", ct)
                .ConfigureAwait(false);
            if (!clicked)
            {
                clicked = await OnUiAsync(() =>
                {
                    ICommand cmd = pi.NextStepCommand;
                    if (cmd.CanExecute(null))
                    {
                        cmd.Execute(null);
                        return true;
                    }
                    var target = pi.WizardSteps.FirstOrDefault(s => s.StepNumber == step);
                    if (target != null && pi.GoToStepCommand.CanExecute(target))
                    {
                        pi.GoToStepCommand.Execute(target);
                        return true;
                    }
                    return false;
                }, ct).ConfigureAwait(false);
            }

            bool reached = await WaitUntilAsync(
                () => pi.CurrentStepNumber >= step, 4_000, ct).ConfigureAwait(false);
            await WpfUiDriver.WaitReadyAsync(280, ct).ConfigureAwait(false);
            return clicked && reached;
        }

        private static bool Navigate(string target)
        {
            try
            {
                if (ProGlassAutomation.Agent.Core.AgentHost.Navigate == null) return false;
                ProGlassAutomation.Agent.Core.AgentHost.Navigate(target);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Task<ProformaInvoiceViewModel?> ResolvePiAsync(CancellationToken ct) =>
            OnUiAsync(() =>
            {
                var dc = Application.Current?.MainWindow?.DataContext;
                return dc is MainViewModel main ? main.ProformaInvoiceVM : null;
            }, ct);

        private static async Task<bool> WaitUntilAsync(
            Func<bool> test, int timeoutMs, CancellationToken ct)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < until)
            {
                ct.ThrowIfCancellationRequested();
                if (await OnUiAsync(test, ct).ConfigureAwait(false)) return true;
                await Task.Delay(120, ct).ConfigureAwait(false);
            }
            return await OnUiAsync(test, ct).ConfigureAwait(false);
        }

        private static async Task<bool> SaveDraftAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp == null) return false;
            if (disp.CheckAccess())
                return await pi.Step7VM.PrintableInvoice.SilentDraftSaveAsync()
                    .ConfigureAwait(true);

            var op = disp.InvokeAsync(
                () => pi.Step7VM.PrintableInvoice.SilentDraftSaveAsync(),
                System.Windows.Threading.DispatcherPriority.Normal,
                ct);
            return await op.Task.Unwrap().ConfigureAwait(false);
        }

        private static Task<T> OnUiAsync<T>(Func<T> work, CancellationToken ct)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp == null) return Task.FromResult(default(T)!);
            if (disp.CheckAccess()) return Task.FromResult(work());
            return disp.InvokeAsync(work, System.Windows.Threading.DispatcherPriority.Normal, ct).Task;
        }

        private static VerifiedEvent Ok(string id, string title, string narration, string evidence) =>
            new()
            {
                Id = id,
                Title = title,
                Narration = narration,
                Evidence = evidence
            };
    }
}
