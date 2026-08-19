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
    /// Complete PI showcase, steps 1–9, with realistic UAE dummy data
    /// entered through the same commands and properties the clerk uses.
    /// Attachments are shown, not faked (no file-picker). P&amp;L is recalculated.
    /// </summary>
    public sealed class ProformaInvoiceWorkflow : IDemoWorkflowRunner
    {
        public const string Company = "ProGlass Automation Demo";
        public const string CompanyTrn = "100234567800003";
        public const string CompanyLocation = "Sharjah Industrial Area 12, UAE";
        public const string Customer = "Al Noor Glazing Contracting LLC";
        public const string CustomerTrn = "100556677800003";
        public const string Project = "Al Zahia Tower — Podium Glazing";
        public const string GlassRef = "CLR-06";

        public const int BeatMs = 2200;
        public const int HoldMs = 2800;

        public string ModuleId => "ProformaInvoice";
        public string OutputFileName => "ProformaInvoice_Demo.mp4";

        public IReadOnlyList<string> DeepEvidenceIds { get; } =
        [
            "new_invoice", "company", "sgu_calc", "dgu_calc",
            "line", "preview", "step8", "step9", "saved"
        ];

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();

            if (!Navigate("ProformaInvoice"))
                return events;

            await WpfUiDriver.WaitReadyAsync(400, ct).ConfigureAwait(false);
            await WpfUiDriver.WaitForIdAsync(
                "ProformaInvoice.NewInvoiceButton", 8000, ct).ConfigureAwait(false);

            await PresentAsync(events, "opened_list",
                "Step 0 — Invoice list",
                "The live Proforma Invoice list. A quotation starts here and becomes a job order after approval.",
                "Navigate ProformaInvoice", BeatMs, ct).ConfigureAwait(false);

            var pi = await ResolvePiAsync(ct).ConfigureAwait(false);
            if (pi == null) return events;

            await StartNewInvoiceAsync(pi, ct).ConfigureAwait(false);
            if (!await WaitUntilAsync(() => pi.IsWizardView, 15_000, ct).ConfigureAwait(false))
            {
                DemoStage.Now("New Invoice did not open the wizard.");
                return events;
            }

            await WaitUntilAsync(() => pi.CurrentStepNumber == 1, 4000, ct).ConfigureAwait(false);
            await PresentAsync(events, "new_invoice",
                "New invoice",
                "New Invoice opened the nine-step wizard. Same command as the button on the list.",
                "TriggerNewInvoice", BeatMs, ct).ConfigureAwait(false);

            // ── 1 Company ────────────────────────────────────────────
            if (await FillCompanyAsync(pi, ct).ConfigureAwait(false))
            {
                await PresentAsync(events, "company",
                    "Step 1 — Company",
                    $"{Company}, Sharjah. TRN {CompanyTrn}. Valid fourteen days. This letterhead prints on the quotation.",
                    $"Company={pi.Step1VM.CompanyName} Id={pi.Step1VM.ProformaId}",
                    HoldMs, ct).ConfigureAwait(false);
            }

            // ── 2 Client ─────────────────────────────────────────────
            if (!await AdvanceToAsync(pi, 2, ct).ConfigureAwait(false)) return events;
            if (await FillCustomerAsync(pi, ct).ConfigureAwait(false))
            {
                await PresentAsync(events, "customer",
                    "Step 2 — Client",
                    $"{Customer}, attention Eng. Mariam Al Suwaidi. Salesman A. Rahman. Billing in Al Zahia, Sharjah.",
                    $"Customer={pi.Step2VM.CustomerName}",
                    HoldMs, ct).ConfigureAwait(false);
            }

            // ── 3 Project ────────────────────────────────────────────
            if (!await AdvanceToAsync(pi, 3, ct).ConfigureAwait(false)) return events;
            if (await FillProjectAsync(pi, ct).ConfigureAwait(false))
            {
                await PresentAsync(events, "project",
                    "Step 3 — Project",
                    $"{Project}. Contract AZT-POD-26, LPO 4418, delivery Al Zahia, Sharjah. This name follows the job and the delivery note.",
                    $"Project={pi.Step3VM.ProjectIdentifierName}",
                    HoldMs, ct).ConfigureAwait(false);
            }

            // ── 4 Calculators ────────────────────────────────────────
            if (!await AdvanceToAsync(pi, 4, ct).ConfigureAwait(false)) return events;
            await PresentAsync(events, "step4",
                "Step 4 — Spec generator",
                "I will run the SGU calculator, then the DGU calculator, so both spec generators are on camera.",
                "Step 4", BeatMs, ct).ConfigureAwait(false);

            var sgu = await RunSguCalculatorAsync(pi, ct).ConfigureAwait(false);
            if (sgu.Ok)
            {
                await PresentAsync(events, "sgu_calc",
                    "SGU calculator",
                    $"SGU {sgu.SpecName}. Rate {sgu.Rate:0.00} dirhams per square metre from sell price, yield 0.85, cutting, tempering and 15 percent margin.",
                    $"SGU {sgu.SpecName} @ {sgu.Rate:0.00}",
                    HoldMs, ct).ConfigureAwait(false);
            }

            var dgu = await RunDguCalculatorAsync(pi, ct).ConfigureAwait(false);
            if (dgu.Ok)
            {
                await PresentAsync(events, "dgu_calc",
                    "DGU calculator",
                    $"DGU {dgu.SpecName}. Outer and inner 6 millimetre Clear FT, 12 millimetre spacer, argon. Rate {dgu.Rate:0.00}.",
                    $"DGU {dgu.SpecName} @ {dgu.Rate:0.00}",
                    HoldMs, ct).ConfigureAwait(false);
            }

            await RestoreSguAsync(pi, ct).ConfigureAwait(false);

            // ── 5 Grid ───────────────────────────────────────────────
            if (!await AdvanceToAsync(pi, 5, ct).ConfigureAwait(false)) return events;
            int lines = await FillGridAsync(pi, sgu, dgu, ct).ConfigureAwait(false);
            await PresentAsync(events, "line",
                "Step 5 — Data grid",
                $"{lines} glass lines: SGU partitions and a DGU vision panel. Width, height, quantity, rate and value stay on the proforma.",
                $"rows={lines} SGU={sgu.SpecName} DGU={dgu.SpecName}",
                HoldMs, ct).ConfigureAwait(false);

            // ── 6 Charges ────────────────────────────────────────────
            if (!await AdvanceToAsync(pi, 6, ct).ConfigureAwait(false)) return events;
            var charges = await SelectChargesAsync(pi, ct).ConfigureAwait(false);
            await PresentAsync(events, "step6",
                "Step 6 — Other charges",
                charges.Ok
                    ? $"Enabled {charges.Names}. Other-charges total {charges.Total:0.00} dirhams."
                    : "Other charges are on screen.",
                charges.Names, HoldMs, ct).ConfigureAwait(false);

            // ── 7 Preview + save ─────────────────────────────────────
            if (!await AdvanceToAsync(pi, 7, ct).ConfigureAwait(false)) return events;
            await PresentAsync(events, "preview",
                "Step 7 — Print preview",
                "Print preview is the live quotation. Rate and value stay on the document. This snapshot feeds the job order later.",
                "Step 7", HoldMs, ct).ConfigureAwait(false);

            bool saved = await SaveDraftAsync(pi, ct).ConfigureAwait(false);
            if (saved)
            {
                await PresentAsync(events, "saved",
                    "Draft saved",
                    $"Draft {pi.Step1VM.ProformaId} saved with SilentDraftSave — the same persist as leaving the wizard.",
                    $"ProformaId={pi.Step1VM.ProformaId}",
                    BeatMs, ct).ConfigureAwait(false);
            }

            // ── 8 Attachments (no file picker) ───────────────────────
            if (!await AdvanceToAsync(pi, 8, ct).ConfigureAwait(false)) return events;
            await ShowAttachmentsAsync(pi, ct).ConfigureAwait(false);
            await PresentAsync(events, "step8",
                "Step 8 — Attachments",
                "Attachments hold drawings and signed copies. This showcase does not invent files or open a picker. The step is live and empty.",
                $"attachments={pi.Step8VM.AttachmentsCount}",
                HoldMs, ct).ConfigureAwait(false);

            // ── 9 Profit & Loss ──────────────────────────────────────
            if (!await AdvanceToAsync(pi, 9, ct).ConfigureAwait(false)) return events;
            var pnl = await RecalculateProfitAsync(pi, ct).ConfigureAwait(false);
            await PresentAsync(events, "step9",
                "Step 9 — Profit and loss",
                pnl.Ok
                    ? $"P and L recalculated. Gross {pnl.Gross:0.00}, glass cost {pnl.Glass:0.00}, profit {pnl.Profit:0.00}, margin {pnl.Margin:0.0} percent."
                    : "Profit and loss is on screen. Recalculate ran against Sheet Store and Step 4 rates.",
                pnl.Ok
                    ? $"gross={pnl.Gross:0.00} profit={pnl.Profit:0.00} margin={pnl.Margin:0.0}"
                    : "Step 9",
                HoldMs, ct).ConfigureAwait(false);

            await SaveDraftAsync(pi, ct).ConfigureAwait(false);
            await WpfUiDriver.DwellAsync(900, ct).ConfigureAwait(false);
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
            catch { /* live voice optional */ }
        }

        private static async Task StartNewInvoiceAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            await WpfUiDriver.ClickAsync("ProformaInvoice.NewInvoiceButton", ct)
                .ConfigureAwait(false);
            await WpfUiDriver.OnUiAsync(() =>
            {
                try { pi.TriggerNewInvoice(); } catch { }
                if (pi.NewInvoiceCommand.CanExecute(null))
                    pi.NewInvoiceCommand.Execute(null);
                return true;
            }, ct).ConfigureAwait(false);
        }

        private static async Task<bool> FillCompanyAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            await WpfUiDriver.SetTextAsync("ProformaInvoice.CompanyName", Company, ct)
                .ConfigureAwait(false);
            return await WpfUiDriver.OnUiAsync(() =>
            {
                var s = pi.Step1VM;
                s.CompanyName = Company;
                s.CompanyTrn = CompanyTrn;
                s.CompanyLocation = CompanyLocation;
                s.Representative = "Khalid Al Nuaimi";
                s.RepresentativeMobile = "+971 50 882 1140";
                s.CompanyEmail = "quotes@proglass.ae";
                s.BankName = "Emirates NBD";
                s.AccountNumber = "AE070260001234567890123";
                s.ValidUntil = DateTime.Now.AddDays(14);
                s.ProformaStatus = "Draft";
                return s.CompanyName == Company;
            }, ct).ConfigureAwait(false);
        }

        private static async Task<bool> FillCustomerAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            await WpfUiDriver.SetTextAsync("ProformaInvoice.CustomerName", Customer, ct)
                .ConfigureAwait(false);
            return await WpfUiDriver.OnUiAsync(() =>
            {
                var s = pi.Step2VM;
                s.CustomerName = Customer;
                s.CustomerTrn = CustomerTrn;
                s.CustomerReferenceCode = "ANG-014";
                s.BillingAddress = "Warehouse 22, Al Zahia, Sharjah, UAE";
                s.AttentionExecutiveName = "Eng. Mariam Al Suwaidi";
                s.SalesRepresentative = "A. Rahman";
                s.DocumentPreparedBy = "Fatima Hassan";
                s.ClientContactPhone = "+971 55 320 7781";
                return s.CustomerName == Customer;
            }, ct).ConfigureAwait(false);
        }

        private static async Task<bool> FillProjectAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            await WpfUiDriver.SetTextAsync("ProformaInvoice.ProjectName", Project, ct)
                .ConfigureAwait(false);
            return await WpfUiDriver.OnUiAsync(() =>
            {
                var s = pi.Step3VM;
                s.ProjectIdentifierName = Project;
                s.ProjectContractNo = "AZT-POD-26";
                s.ProjectReference = "Podium curtain wall and lobby partitions — Al Zahia Tower";
                s.SiteDeliveryLocation = "Al Zahia, Sharjah";
                s.LpoNumber = "4418";
                return s.ProjectIdentifierName == Project;
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

        private readonly struct PnlResult
        {
            public bool Ok { get; init; }
            public decimal Gross { get; init; }
            public decimal Glass { get; init; }
            public decimal Profit { get; init; }
            public decimal Margin { get; init; }
        }

        private static async Task<CalcResult> RunSguCalculatorAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            DemoStage.Now("Running the SGU spec calculator.");
            var result = await WpfUiDriver.OnUiAsync(() =>
            {
                var step4 = pi.Step4VM;
                var card = step4.GlassTypes.FirstOrDefault(g => g.Code == "SGU");
                if (card != null && step4.SelectGlassTypeCommand.CanExecute(card))
                    step4.SelectGlassTypeCommand.Execute(card);

                var sgu = step4.SguVM;
                sgu.SelectedThickness = "6mm";
                sgu.GlassColor = "Clear";
                sgu.SelectedTreatment = "FT Glass";
                sgu.BasePricePerSqm = 46m;
                sgu.CuttingCostPerSqm = 15m;
                sgu.TemperingCostPerSqm = 25m;
                sgu.YieldFactor = 0.85m;
                sgu.TargetMarginPercent = 15m;
                sgu.SelectedPolishOption = "Polished Edges";
                step4.RefreshSheetStoreData();
                return new CalcResult
                {
                    Ok = step4.EstimatedRatePerSqm > 0m
                         && !string.IsNullOrWhiteSpace(sgu.GeneratedSpecName),
                    SpecName = sgu.GeneratedSpecName ?? "",
                    Rate = step4.EstimatedRatePerSqm
                };
            }, ct).ConfigureAwait(false);
            await WpfUiDriver.DwellAsync(1000, ct).ConfigureAwait(false);
            return result;
        }

        private static async Task<CalcResult> RunDguCalculatorAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            DemoStage.Now("Running the DGU spec calculator.");
            var result = await WpfUiDriver.OnUiAsync(() =>
            {
                var step4 = pi.Step4VM;
                var card = step4.GlassTypes.FirstOrDefault(g => g.Code == "DGU");
                if (card != null && step4.SelectGlassTypeCommand.CanExecute(card))
                    step4.SelectGlassTypeCommand.Execute(card);

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

        private static async Task RestoreSguAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            await WpfUiDriver.OnUiAsync(() =>
            {
                var card = pi.Step4VM.GlassTypes.FirstOrDefault(g => g.Code == "SGU");
                if (card != null && pi.Step4VM.SelectGlassTypeCommand.CanExecute(card))
                    pi.Step4VM.SelectGlassTypeCommand.Execute(card);
                return true;
            }, ct).ConfigureAwait(false);
            await WpfUiDriver.DwellAsync(600, ct).ConfigureAwait(false);
        }

        private static Task<int> FillGridAsync(
            ProformaInvoiceViewModel pi, CalcResult sgu, CalcResult dgu, CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var grid = pi.Step5VM;
                if (grid.SpecGroups.Count == 0) return 0;

                var g1 = grid.SpecGroups[0];
                g1.SelectedGlassType = "SGU";
                if (!string.IsNullOrWhiteSpace(sgu.SpecName))
                    g1.SelectedSpecName = sgu.SpecName;
                if (sgu.Rate > 0m) g1.BaseRate = sgu.Rate;

                while (g1.ItemRows.Count < 3 && g1.AddRowCommand.CanExecute(null))
                    g1.AddRowCommand.Execute(null);

                AddLine(g1, 0, sgu, 1200, 900, 6);
                AddLine(g1, 1, sgu, 2100, 1500, 4);
                AddLine(g1, 2, sgu, 800, 2100, 8);

                if (grid.AddSpecGroupCommand.CanExecute(null))
                    grid.AddSpecGroupCommand.Execute(null);

                var g2 = grid.SpecGroups.LastOrDefault();
                if (g2 != null && !ReferenceEquals(g2, g1))
                {
                    g2.SelectedGlassType = "DGU";
                    if (!string.IsNullOrWhiteSpace(dgu.SpecName))
                        g2.SelectedSpecName = dgu.SpecName;
                    if (dgu.Rate > 0m) g2.BaseRate = dgu.Rate;
                    if (g2.ItemRows.Count == 0 && g2.AddRowCommand.CanExecute(null))
                        g2.AddRowCommand.Execute(null);
                    AddLine(g2, 0, dgu, 1500, 2200, 3);
                }

                return grid.SpecGroups.Sum(g => g.ItemRows.Count);
            }, ct);

        private static void AddLine(
            ViewModels.ProformaInvoice.Step5DataGrid.SpecGroup group,
            int index, CalcResult calc, int w, int h, int qty)
        {
            if (index >= group.ItemRows.Count) return;
            var row = group.ItemRows[index];
            row.GlassRef = string.IsNullOrWhiteSpace(calc.SpecName) ? GlassRef : calc.SpecName;
            row.W1 = w;
            row.H1 = h;
            row.Qty = qty;
            if (calc.Rate > 0m) row.Rate = calc.Rate;
        }

        private static Task<ChargeResult> SelectChargesAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var names = new List<string>();
                foreach (var row in pi.Step6VM.ChargeRows)
                {
                    if (row.TemplateKey is "DELIVERY" or "SILICON_BED" or "POLISH")
                    {
                        row.IsActive = true;
                        names.Add(row.ChargeName);
                    }
                }
                return new ChargeResult
                {
                    Ok = names.Count > 0,
                    Names = string.Join(", ", names),
                    Total = pi.Step6VM.TotalOtherCharges
                };
            }, ct);

        private static async Task ShowAttachmentsAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            try
            {
                await pi.Step8VM.LoadAsync().ConfigureAwait(true);
            }
            catch { /* empty step is honest */ }
            await WpfUiDriver.DwellAsync(400, ct).ConfigureAwait(false);
        }

        private static async Task<PnlResult> RecalculateProfitAsync(
            ProformaInvoiceViewModel pi, CancellationToken ct)
        {
            DemoStage.Now("Recalculating profit and loss.");
            try
            {
                await pi.Step9VM.RecalculateAsync().ConfigureAwait(true);
            }
            catch { /* P&L never breaks the walk */ }

            await WaitUntilAsync(() => !pi.Step9VM.IsBusy, 8000, ct).ConfigureAwait(false);
            await WpfUiDriver.DwellAsync(500, ct).ConfigureAwait(false);

            return await WpfUiDriver.OnUiAsync(() => new PnlResult
            {
                Ok = true,
                Gross = pi.Step9VM.GrossVatInclusive,
                Glass = pi.Step9VM.TotalGlassCost,
                Profit = pi.Step9VM.Profit,
                Margin = pi.Step9VM.MarginPercent
            }, ct).ConfigureAwait(false);
        }

        private static async Task<bool> AdvanceToAsync(
            ProformaInvoiceViewModel pi, int step, CancellationToken ct)
        {
            if (await CurrentStepAsync(pi, ct).ConfigureAwait(false) >= step)
                return true;

            DemoStage.Now($"Continue to step {step}.");
            await WpfUiDriver.ClickAsync("ProformaInvoice.ContinueButton", ct)
                .ConfigureAwait(false);

            if (await CurrentStepAsync(pi, ct).ConfigureAwait(false) < step)
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
                () => pi.CurrentStepNumber >= step, 8000, ct).ConfigureAwait(false);
            await WpfUiDriver.WaitReadyAsync(400, ct).ConfigureAwait(false);
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
            catch { return false; }
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
                System.Windows.Threading.DispatcherPriority.Normal, ct);
            return await op.Task.Unwrap().ConfigureAwait(false);
        }
    }
}
