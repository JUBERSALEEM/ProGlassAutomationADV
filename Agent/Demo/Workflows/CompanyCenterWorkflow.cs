using ProGlassAutomation.Agent.Demo.Automation;
using ProGlassAutomation.ViewModels.CompanyCenter;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Live Company Center: select the demo letterhead, then create a
    /// second company through SaveCompanyCommand. No logo picker.
    /// </summary>
    public sealed class CompanyCenterWorkflow : IDemoWorkflowRunner
    {
        public string ModuleId => "CompanyCenter";
        public string OutputFileName => "CompanyCenter_Demo.mp4";
        public IReadOnlyList<string> DeepEvidenceIds { get; } =
            ["selected", "saved"];

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();
            if (!DemoWalk.Navigate("CompanyCenter")) return events;

            await WpfUiDriver.WaitReadyAsync(500, ct).ConfigureAwait(false);
            var cc = await ResolveAsync(ct).ConfigureAwait(false);
            if (cc == null) return events;

            await cc.LoadAsync().ConfigureAwait(true);
            await DemoWalk.WaitUntilAsync(
                () => cc.Companies.Count > 0, 8000, ct).ConfigureAwait(false);

            await DemoWalk.PresentAsync(events, "opened_list",
                "Company Center",
                $"Company Center is where we store our own trading identities. Many glass workshops trade under two or three companies — one for retail, one for contracting, sometimes a free-zone entity — and each one has its own tax registration, bank account and letterhead. There are {cc.Companies.Count} company profiles saved here. Whichever one you pick becomes the letterhead that prints on every quotation, tax invoice and delivery note issued from that job.",
                $"companies={cc.Companies.Count}",
                DemoWalk.BeatMs, ct).ConfigureAwait(false);

            var card = await WpfUiDriver.OnUiAsync(() =>
                cc.Companies.FirstOrDefault(c => c.IsDefault)
                ?? cc.Companies.FirstOrDefault(), ct).ConfigureAwait(false);

            if (card != null)
            {
                await DemoWalk.TryCommandAsync(cc.SelectCompanyCommand, card, ct)
                    .ConfigureAwait(false);
                await WpfUiDriver.DwellAsync(800, ct).ConfigureAwait(false);
                await DemoWalk.PresentAsync(events, "selected",
                    "Open letterhead",
                    $"Here is the letterhead card for {cc.CompanyName}. The tax registration number {cc.CompanyTrn} — required under UAE VAT law — sits right at the top, the trading address is {cc.CompanyLocation}, and further down you can see the bank name, IBAN and account holder that print on the invoice footer. This is exactly the card the Proforma Invoice module reads on Step 1, so whatever we change here immediately shows on the next quotation issued.",
                    $"id={card.Id} name={cc.CompanyName} trn={cc.CompanyTrn}",
                    DemoWalk.HoldMs, ct).ConfigureAwait(false);
            }

            await DemoWalk.TryCommandAsync(cc.NewCompanyCommand, null, ct)
                .ConfigureAwait(false);

            await WpfUiDriver.OnUiAsync(() =>
            {
                cc.CompanyName = "Gulf Facade Works LLC";
                cc.CompanyTrn = "100778899000003";
                cc.CompanyLocation = "Al Quoz Industrial 3, Dubai, UAE";
                cc.Representative = "Noura Al Falasi";
                cc.RepresentativeMobile = "+971 50 441 2290";
                cc.CompanyEmail = "accounts@gulffacade.ae";
                cc.BankName = "Mashreq Bank";
                cc.AccountNumber = "AE120330001234567890123";
                cc.IsDefaultCompany = false;
                cc.ReasonNote = "Second letterhead for Dubai-site quotations.";
                return true;
            }, ct).ConfigureAwait(false);

            await DemoWalk.TryCommandAsync(cc.SaveCompanyCommand, null, ct)
                .ConfigureAwait(false);
            bool saved = await DemoWalk.WaitUntilAsync(
                () => cc.Companies.Any(c => c.CompanyName == "Gulf Facade Works LLC"),
                8000, ct).ConfigureAwait(false);
            if (!saved)
            {
                await cc.LoadAsync().ConfigureAwait(true);
                saved = await WpfUiDriver.OnUiAsync(() =>
                    cc.Companies.Any(c => c.CompanyName == "Gulf Facade Works LLC"), ct)
                    .ConfigureAwait(false);
            }

            await DemoWalk.PresentAsync(events, "saved",
                saved ? "Save second company" : "Company editor",
                saved
                    ? "And a second letterhead — Gulf Facade Works LLC — is now on file. This is a real production save, not a demo mock. From this moment, any operator preparing a Dubai-side quote can pick this company from the letterhead dropdown and its bank details, tax number and logo will flow straight onto the printed document. The logo picker was intentionally not opened so this recording never touches the file system."
                    : $"You are looking at the live Company editor — the same form the finance manager uses. The save reported: {cc.Status}. Nothing was faked; if the save had succeeded the new company would appear immediately in the list on the left.",
                saved
                    ? $"saved=Gulf Facade Works LLC count={cc.Companies.Count}"
                    : $"status={cc.Status}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            return events;
        }

        private static Task<CompanyCenterViewModel?> ResolveAsync(CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var main = System.Windows.Application.Current?.MainWindow?.DataContext
                    as ViewModels.MainViewModel;
                return main?.CompanyCenterVM;
            }, ct);
    }
}
