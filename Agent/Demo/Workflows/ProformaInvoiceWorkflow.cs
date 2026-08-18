using ProGlassAutomation.Agent.Demo.Automation;
using ProGlassAutomation.ViewModels;
using ProGlassAutomation.ViewModels.ProformaInvoice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Full Proforma Invoice product walk: list → new invoice → company →
    /// client → project → glass type → grid line → charges → preview → save.
    /// Each beat dwells long enough to film and presents live narration.
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

        public const int BeatMs = 2400;
        public const int HoldMs = 2800;

        public string ModuleId => "ProformaInvoice";
        public string OutputFileName => "ProformaInvoice_Demo.mp4";

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();

            if (!Navigate("ProformaInvoice"))
                return events;

            await WpfUiDriver.WaitReadyAsync(400, ct).ConfigureAwait(false);
            await WpfUiDriver.WaitForIdAsync(
                "ProformaInvoice.NewInvoiceButton", 8000, ct).ConfigureAwait(false);

            await PresentAsync(events, "opened_list",
                "Proforma Invoice list",
                "This is the live Proforma Invoice list. Saved quotations sit here until we convert them to job orders.",
                "AgentHost.Navigate(ProformaInvoice)",
                BeatMs, ct).ConfigureAwait(false);

            var pi = await ResolvePiAsync(ct).ConfigureAwait(false);
            if (pi == null) return events;

            bool started = await StartNewInvoiceAsync(pi, ct).ConfigureAwait(false);
            bool wizard = await WaitUntilAsync(() => pi.IsWizardView, 15_000, ct)
                .ConfigureAwait(false);
            if (!started || !wizard)
            {
                DemoStage.Now("New Invoice did not open the wizard.");
                return events;
            }

            await WaitUntilAsync(() => pi.CurrentStepNumber == 1, 4000, ct)
                .ConfigureAwait(false);

            await PresentAsync(events, "new_invoice",
                "New invoice",
                "I pressed New Invoice — the same command as the button. A fresh quotation is open on company details.",
                "TriggerNewInvoice / NewInvoiceCommand",
                BeatMs, ct).ConfigureAwait(false);

            if (await FillCompanyAsync(pi, ct).ConfigureAwait(false))
            {
                await PresentAsync(events, "company",
                    "Company details",
                    $"Company is {Company}, Sharjah, TRN ending 003. This is the letterhead the print preview will use.",
                    $"Step1VM.CompanyName={pi.Step1VM.CompanyName}",
                    HoldMs, ct).ConfigureAwait(false);
            }

            if (!await AdvanceToAsync(pi, 2, ct).ConfigureAwait(false)) return events;
            await PresentAsync(events, "step2",
                "Client step",
                "Continue opened the client profile. This is who the quotation is billed to.",
                "NextStepCommand → step 2",
                BeatMs, ct).ConfigureAwait(false);

            if (await FillCustomerAsync(pi, ct).ConfigureAwait(false))
            {
                await PresentAsync(events, "customer",
                    "Customer",
                    $"The customer is {Customer}. Name, TRN and billing address are the same fields a clerk types.",
                    $"Step2VM.CustomerName={pi.Step2VM.CustomerName}",
                    HoldMs, ct).ConfigureAwait(false);
            }

            if (!await AdvanceToAsync(pi, 3, ct).ConfigureAwait(false)) return events;
            await PresentAsync(events, "step3",
                "Project step",
                "Project specifications are on screen — site, contract and LPO.",
                "NextStepCommand → step 3",
                BeatMs, ct).ConfigureAwait(false);

            if (await FillProjectAsync(pi, ct).ConfigureAwait(false))
            {
                await PresentAsync(events, "project",
                    "Project",
                    $"The project is {Project}. This name follows the job order and delivery note later in the chain.",
                    $"Step3VM.ProjectIdentifierName={pi.Step3VM.ProjectIdentifierName}",
                    HoldMs, ct).ConfigureAwait(false);
            }

            if (!await AdvanceToAsync(pi, 4, ct).ConfigureAwait(false)) return events;
            await PresentAsync(events, "step4",
                "Spec generator",
                "Glass type and spec generator. I will run the SGU calculator, then the DGU calculator, then apply the SGU spec.",
                "NextStepCommand → step 4",
                BeatMs, ct).ConfigureAwait(false);

            var sgu = await RunSguCalculatorAsync(pi, ct).ConfigureAwait(false);
            if (sgu.Ok)
            {
                await PresentAsync(events, "sgu_calc",
                    "SGU calculator",
                    $"SGU spec generated: {sgu.SpecName}. Live rate {sgu.Rate:0.00} dirhams per square metre from yield, cutting, tempering and margin.",
                    $"SGU {sgu.SpecName} @ {sgu.Rate:0.00}",
                    HoldMs, ct).ConfigureAwait(false);
            }

            var dgu = await RunDguCalculatorAsync(pi, ct).ConfigureAwait(false);
            if (dgu.Ok)
            {
                await PresentAsync(events, "dgu_calc",
                    "DGU calculator",
                    $"DGU spec generated: {dgu.SpecName}. Outer and inner 6mm Clear FT with 12mm spacer. Rate {dgu.Rate:0.00}.",
                    $"DGU {dgu.SpecName} @ {dgu.Rate:0.00}",
                    HoldMs, ct).ConfigureAwait(false);
            }

            await RestoreSguAndApplyAsync(pi, ct).ConfigureAwait(false);

            if (!await AdvanceToAsync(pi, 5, ct).ConfigureAwait(false)) return events;
            await PresentAsync(events, "step5",
                "Specification grid",
                "The data grid is open. I will attach the generated SGU spec and enter a real glass line with rate and value.",
                "NextStepCommand → step 5",
                BeatMs, ct).ConfigureAwait(false);

            if (await FillLineAsync(pi, sgu.SpecName, sgu.Rate, ct).ConfigureAwait(false))
            {
                decimal usedRate = sgu.Rate > 0 ? sgu.Rate : Rate;
                await PresentAsync(events, "line",
                    "Glass line",
                    $"{sgu.SpecName}: {WidthMm} by {HeightMm} millimetres, quantity {Quantity}, rate {usedRate:0.00}. Value follows quantity times area times rate.",
                    $"W1={WidthMm} H1={HeightMm} Qty={Quantity} Rate={usedRate} Spec={sgu.SpecName}",
                    HoldMs, ct).ConfigureAwait(false);
            }

            if (!await AdvanceToAsync(pi, 6, ct).ConfigureAwait(false)) return events;
            var charges = await SelectChargesAsync(pi, ct).ConfigureAwait(false);
            await PresentAsync(events, "step6",
                "Other charges",
                charges.Ok
                    ? $"I switched on {charges.Names}. Other-charges total {charges.Total:0.00}."
                    : "Other charges are on screen. I could not enable a predefined row.",
                charges.Ok ? charges.Names : "NextStepCommand → step 6",
                HoldMs, ct).ConfigureAwait(false);

            if (!await AdvanceToAsync(pi, 7, ct).ConfigureAwait(false)) return events;
            await PresentAsync(events, "preview",
                "Print preview",
                "Print preview is the live quotation. Rate and value stay on the document. This snapshot is what the rest of the chain reads.",
                "NextStepCommand → step 7",
                HoldMs, ct).ConfigureAwait(false);

            bool saved = await SaveDraftAsync(pi, ct).ConfigureAwait(false);
            if (saved)
            {
                await PresentAsync(events, "saved",
                    "Draft saved",
                    $"Draft {pi.Step1VM.ProformaId} is saved with SilentDraftSave — the same persist the wizard uses when you leave the form.",
                    $"ProformaId={pi.Step1VM.ProformaId}",
                    HoldMs, ct).ConfigureAwait(false);
            }

            await WpfUiDriver.DwellAsync(800, ct).ConfigureAwait(false);
            return events;
        }

        private static async Task PresentAsync(
            List<VerifiedEvent> events,
            string id, string title, string narration, string evidence,
            int dwellMs, CancellationToken ct)
        {
            DemoStage.Now(title + " — " + narration);
            SpeakLive(narration);
            events.Add(new VerifiedEvent
            {
                Id = id,
                Title = title,
                Narration = narration,
                Evidence = evidence
            });
            await WpfUiDriver.DwellAsync(dwellMs, ct).ConfigureAwait(false);
        }

        private static void SpeakLive(string text)
        {
            try { App.TextToSpeech?.SpeakAsync(text); }
            catch { /* live voice is optional; WAV is written later */ }
        }

        private static async Task<bool> StartNewInvoiceAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            bool clicked = await WpfUiDriver
                .ClickAsync("ProformaInvoice.NewInvoiceButton", ct)
                .ConfigureAwait(false);

            await WpfUiDriver.OnUiAsync(() =>
            {
                try { pi.TriggerNewInvoice(); }
                catch { /* same public entry the dashboard uses */ }
                if (!clicked && pi.NewInvoiceCommand.CanExecute(null))
                    pi.NewInvoiceCommand.Execute(null);
                return true;
            }, ct).ConfigureAwait(false);

            return true;
        }

        private static async Task<bool> FillCompanyAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            await WpfUiDriver.SetTextAsync("ProformaInvoice.CompanyName", Company, ct)
                .ConfigureAwait(false);
            return await WpfUiDriver.OnUiAsync(() =>
            {
                pi.Step1VM.CompanyName = Company;
                pi.Step1VM.CompanyTrn = CompanyTrn;
                pi.Step1VM.CompanyLocation = CompanyLocation;
                return string.Equals(pi.Step1VM.CompanyName, Company, StringComparison.Ordinal);
            }, ct).ConfigureAwait(false);
        }

        private static async Task<bool> FillCustomerAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            await WpfUiDriver.SetTextAsync("ProformaInvoice.CustomerName", Customer, ct)
                .ConfigureAwait(false);
            return await WpfUiDriver.OnUiAsync(() =>
            {
                pi.Step2VM.CustomerName = Customer;
                pi.Step2VM.CustomerTrn = "100987654300003";
                pi.Step2VM.BillingAddress = "PO Box 12345, Sharjah, UAE";
                pi.Step2VM.SalesRepresentative = "A. Rahman";
                pi.Step2VM.DocumentPreparedBy = "Demo User";
                pi.Step2VM.ClientContactPhone = "+971 50 123 4567";
                return string.Equals(pi.Step2VM.CustomerName, Customer, StringComparison.Ordinal);
            }, ct).ConfigureAwait(false);
        }

        private static async Task<bool> FillProjectAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            await WpfUiDriver.SetTextAsync("ProformaInvoice.ProjectName", Project, ct)
                .ConfigureAwait(false);
            return await WpfUiDriver.OnUiAsync(() =>
            {
                pi.Step3VM.ProjectIdentifierName = Project;
                pi.Step3VM.ProjectContractNo = "CTR-2026-11";
                pi.Step3VM.SiteDeliveryLocation = "Sharjah Industrial Area";
                pi.Step3VM.LpoNumber = "LPO-889";
                return string.Equals(
                    pi.Step3VM.ProjectIdentifierName, Project, StringComparison.Ordinal);
            }, ct).ConfigureAwait(false);
        }

        private readonly struct CalcResult
        {
            public bool Ok { get; init; }
            public string SpecName { get; init; }
            public decimal Rate { get; init; }
        }

        private readonly struct ChargeResult
        {
            public bool Ok { get; init; }
            public string Names { get; init; }
            public decimal Total { get; init; }
        }

        private static async Task<CalcResult> RunSguCalculatorAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            DemoStage.Now("Running the SGU spec calculator.");
            var result = await WpfUiDriver.OnUiAsync(() =>
            {
                var step4 = pi.Step4VM;
                var sguCard = step4.GlassTypes.FirstOrDefault(g => g.Code == "SGU");
                if (sguCard != null && step4.SelectGlassTypeCommand.CanExecute(sguCard))
                    step4.SelectGlassTypeCommand.Execute(sguCard);

                var sgu = step4.SguVM;
                sgu.SelectedThickness = "6mm";
                sgu.GlassColor = "Clear";
                sgu.SelectedTreatment = "FT Glass";
                sgu.BasePricePerSqm = 46m;
                sgu.CuttingCostPerSqm = 15m;
                sgu.TemperingCostPerSqm = 25m;
                sgu.YieldFactor = 0.85m;
                sgu.TargetMarginPercent = 15m;
                sgu.SelectedPolishOption = "None (Standard)";
                step4.RefreshSheetStoreData();
                return new CalcResult
                {
                    Ok = step4.EstimatedRatePerSqm > 0m
                         && !string.IsNullOrWhiteSpace(sgu.GeneratedSpecName),
                    SpecName = sgu.GeneratedSpecName ?? "",
                    Rate = step4.EstimatedRatePerSqm
                };
            }, ct).ConfigureAwait(false);

            await WpfUiDriver.DwellAsync(900, ct).ConfigureAwait(false);
            return result;
        }

        private static async Task<CalcResult> RunDguCalculatorAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            DemoStage.Now("Running the DGU spec calculator.");
            var result = await WpfUiDriver.OnUiAsync(() =>
            {
                var step4 = pi.Step4VM;
                var dguCard = step4.GlassTypes.FirstOrDefault(g => g.Code == "DGU");
                if (dguCard != null && step4.SelectGlassTypeCommand.CanExecute(dguCard))
                    step4.SelectGlassTypeCommand.Execute(dguCard);

                var dgu = step4.DguVM;
                dgu.OuterThickness = "6mm";
                dgu.OuterGlassColor = "Clear";
                dgu.OuterTreatment = "FT";
                dgu.OuterBasePrice = 46m;
                dgu.SpacerWidth = "12mm ASP";
                dgu.SpacerMaterial = "Normal Mill Aluminum";
                dgu.InnerThickness = "6mm";
                dgu.InnerGlassColor = "Clear";
                dgu.InnerTreatment = "FT";
                dgu.InnerBasePrice = 46m;
                dgu.YieldFactor = 0.85m;
                dgu.MarginProfitPercent = 15m;
                dgu.PanelExtra = "With Argon Gas";
                step4.RefreshSheetStoreData();
                return new CalcResult
                {
                    Ok = step4.EstimatedRatePerSqm > 0m
                         && !string.IsNullOrWhiteSpace(dgu.GeneratedSpecName),
                    SpecName = dgu.GeneratedSpecName ?? "",
                    Rate = step4.EstimatedRatePerSqm
                };
            }, ct).ConfigureAwait(false);

            await WpfUiDriver.DwellAsync(HoldMs, ct).ConfigureAwait(false);
            return result;
        }

        private static async Task RestoreSguAndApplyAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            DemoStage.Now("Returning to the SGU spec for the data grid.");
            await WpfUiDriver.OnUiAsync(() =>
            {
                var step4 = pi.Step4VM;
                var sguCard = step4.GlassTypes.FirstOrDefault(g => g.Code == "SGU");
                if (sguCard != null && step4.SelectGlassTypeCommand.CanExecute(sguCard))
                    step4.SelectGlassTypeCommand.Execute(sguCard);
                return true;
            }, ct).ConfigureAwait(false);
            await WpfUiDriver.DwellAsync(800, ct).ConfigureAwait(false);
        }

        private static Task<bool> FillLineAsync(
            ProformaInvoiceViewModel pi, string specName, decimal calcRate, CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var group = pi.Step5VM.SpecGroups.FirstOrDefault();
                if (group == null) return false;

                group.SelectedGlassType = "SGU";
                if (!string.IsNullOrWhiteSpace(specName))
                    group.SelectedSpecName = specName;
                if (calcRate > 0m)
                    group.BaseRate = calcRate;

                if (group.ItemRows.Count == 0
                    && group.AddRowCommand.CanExecute(null))
                    group.AddRowCommand.Execute(null);

                var row = group.ItemRows.FirstOrDefault();
                if (row == null) return false;

                row.GlassRef = string.IsNullOrWhiteSpace(specName) ? GlassRef : specName;
                row.W1 = WidthMm;
                row.H1 = HeightMm;
                row.Qty = Quantity;
                row.Rate = calcRate > 0m ? calcRate : Rate;
                return row.W1 == WidthMm
                    && row.H1 == HeightMm
                    && row.Qty == Quantity;
            }, ct);

        private static Task<ChargeResult> SelectChargesAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var names = new List<string>();
                foreach (var row in pi.Step6VM.ChargeRows)
                {
                    if (row.TemplateKey is "DELIVERY" or "SILICON_BED")
                    {
                        row.IsActive = true;
                        names.Add(row.ChargeName);
                    }
                }
                return new ChargeResult
                {
                    Ok = names.Count > 0,
                    Names = string.Join(" and ", names),
                    Total = pi.Step6VM.TotalOtherCharges
                };
            }, ct);

        private static async Task<bool> AdvanceToAsync(
            ProformaInvoiceViewModel pi, int step, CancellationToken ct)
        {
            if (await CurrentStepAsync(pi, ct).ConfigureAwait(false) >= step)
                return true;

            DemoStage.Now($"Continue to step {step}.");
            bool clicked = await WpfUiDriver
                .ClickAsync("ProformaInvoice.ContinueButton", ct)
                .ConfigureAwait(false);

            if (!clicked || await CurrentStepAsync(pi, ct).ConfigureAwait(false) < step)
            {
                await WpfUiDriver.OnUiAsync(() =>
                {
                    if (pi.NextStepCommand.CanExecute(null))
                        pi.NextStepCommand.Execute(null);
                    if (pi.CurrentStepNumber < step)
                    {
                        var target = pi.WizardSteps.FirstOrDefault(s => s.StepNumber == step);
                        if (target != null && pi.GoToStepCommand.CanExecute(target))
                            pi.GoToStepCommand.Execute(target);
                    }
                    return true;
                }, ct).ConfigureAwait(false);
            }

            bool reached = await WaitUntilAsync(
                () => pi.CurrentStepNumber >= step, 6000, ct).ConfigureAwait(false);
            await WpfUiDriver.WaitReadyAsync(350, ct).ConfigureAwait(false);
            return reached;
        }

        private static Task<int> CurrentStepAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() => pi.CurrentStepNumber, ct);

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
            WpfUiDriver.OnUiAsync(() =>
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
                if (await WpfUiDriver.OnUiAsync(test, ct).ConfigureAwait(false))
                    return true;
                await Task.Delay(150, ct).ConfigureAwait(false);
            }
            return await WpfUiDriver.OnUiAsync(test, ct).ConfigureAwait(false);
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
    }
}
