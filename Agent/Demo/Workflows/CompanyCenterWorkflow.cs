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
                $"Company cards print on every quotation. {cc.Companies.Count} profiles in masterdata.",
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
                    $"{cc.CompanyName}. TRN {cc.CompanyTrn}. {cc.CompanyLocation}. This is the same card Proforma Step 1 reads.",
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
                    ? "Gulf Facade Works LLC saved through SaveCompanyCommand. Logo picker was not opened."
                    : $"Editor ran. Status: {cc.Status}. Logo picker was not opened.",
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
