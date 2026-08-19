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
                $"User Center is the live people list. {users.Users.Count} accounts. The signed-in name fills Document Prepared By on a new invoice.",
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
                    $"Selected {card.DisplayName}, role {card.Role}, {(card.IsActive ? "active" : "inactive")}. Same Select command as the card.",
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
                    ? "Fatima Hassan was created as Operator through CreateUserAsync — the same persist as the form. She can prepare the next quotation."
                    : $"Create form ran. Status: {users.Status}. The editor is the live User Center, not a mock.",
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
