using ProGlassAutomation.Agent.Demo.Automation;
using ProGlassAutomation.ViewModels.UserCenter;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Live User Center: load cards, select, create Fatima Hassan through
    /// the same CreateUserAsync the form uses. No password switch.
    /// </summary>
    public sealed class UserCenterWorkflow : IDemoWorkflowRunner
    {
        public string ModuleId => "UserCenter";
        public string OutputFileName => "UserCenter_Demo.mp4";
        public IReadOnlyList<string> DeepEvidenceIds { get; } =
            ["listed", "selected", "created"];

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();
            if (!DemoWalk.Navigate("UserCenter")) return events;

            await WpfUiDriver.WaitReadyAsync(500, ct).ConfigureAwait(false);
            var users = await ResolveAsync(ct).ConfigureAwait(false);
            if (users == null) return events;

            await users.LoadAsync().ConfigureAwait(true);
            await DemoWalk.WaitUntilAsync(
                () => users.Users.Count > 0, 8000, ct).ConfigureAwait(false);

            await DemoWalk.PresentAsync(events, "listed",
                "User Center",
                $"User Center is where we manage every person who logs into ProGlass. There are {users.Users.Count} accounts on file, each with a role — Administrator, Manager, Accountant or Operator — that decides which modules and buttons they can see. Whoever is signed in appears automatically as the 'Document Prepared By' name on every new quotation, so responsibility is always traceable. There is no anonymous editing in this system.",
                $"users={users.Users.Count} current={users.CurrentUserName}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            var card = await WpfUiDriver.OnUiAsync(() =>
                users.Users.FirstOrDefault(), ct).ConfigureAwait(false);
            if (card != null)
            {
                await DemoWalk.TryCommandAsync(users.SelectUserCommand, card, ct)
                    .ConfigureAwait(false);
                await DemoWalk.PresentAsync(events, "selected",
                    "Select a user",
                    $"Clicking a card opens that user's profile. This one is {card.DisplayName}, assigned the {card.Role} role, and the account is currently {(card.IsActive ? "active" : "inactive")}. From here an administrator can change the role, deactivate the account, reset the password or view the login history. The Select action you just saw is the same command wired to the user card — no separate admin panel, no hidden path.",
                    $"id={card.Id} user={card.Username} role={card.Role}",
                    DemoWalk.HoldMs, ct).ConfigureAwait(false);
            }

            await WpfUiDriver.OnUiAsync(() =>
            {
                users.Username = "fatima.hassan";
                users.DisplayName = "Fatima Hassan";
                users.SelectedRole = "Operator";
                users.ReasonNote = "Estimator for the Al Zahia podium quote.";
                return true;
            }, ct).ConfigureAwait(false);

            await WpfUiDriver.DwellAsync(800, ct).ConfigureAwait(false);
            await users.CreateUserAsync("DemoPass12", "DemoPass12").ConfigureAwait(true);
            await users.LoadAsync().ConfigureAwait(true);

            bool created = await WpfUiDriver.OnUiAsync(() =>
                users.Users.Any(u => u.Username == "fatima.hassan"), ct)
                .ConfigureAwait(false);

            await DemoWalk.PresentAsync(events, "created",
                created ? "Create Fatima Hassan" : "Create user form",
                created
                    ? "And here is Fatima Hassan, freshly added as an Operator. Notice we did not open a separate popup — the create form is the same live editor you see, and it went straight through the same save path as production, so the audit trail records who created her and when. From this point she can log in, prepare quotations, and her name will appear on the documents she saves."
                    : $"You are looking at the live user editor — the same form the administrator uses. Right now the save reported: {users.Status}. This is the real User Center, not a mock-up, so any name entered here is committed the same way a real hire would be.",
                created
                    ? $"user=fatima.hassan users={users.Users.Count}"
                    : $"status={users.Status}",
                DemoWalk.HoldMs, ct).ConfigureAwait(false);

            return events;
        }

        private static Task<UserCenterViewModel?> ResolveAsync(CancellationToken ct) =>
            WpfUiDriver.OnUiAsync(() =>
            {
                var main = System.Windows.Application.Current?.MainWindow?.DataContext
                    as ViewModels.MainViewModel;
                return main?.UserCenterVM;
            }, ct);
    }
}
