// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Personal/GlassShell.xaml.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// GB-P2 (2026-09-18) — Glass Blush Personal UI shell.
// Composition control: DataContext, commands and view models are all
// inherited from MainViewModel (assigned by the window in GB-P14). No
// business logic, no state — the shell is declarative XAML only.
//
// GB-P15 (2026-09-19) — personal-side top-bar popups (Notifications +
// User Switch) anchor to the Personal buttons. Their Opened/Closed
// handlers mirror MainWindow's Professional-popup handlers (same shared
// panels, same load/refresh calls) so both experiences behave identically.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels;

namespace ProGlassAutomation.Views.Personal
{
    /// <summary>
    /// Glass Blush Personal UI shell (sidebar rail + top bar + content host).
    /// Binds to the same MainViewModel as the Professional chrome.
    /// </summary>
    public partial class GlassShell : UserControl
    {
        public GlassShell()
        {
            InitializeComponent();
        }

        // ── GB-P15 personal-side popups ─────────────────────────────────
        // Mirrors MainWindow's NotificationsPopup_Opened: refresh the
        // activity feed when the Personal notifications panel opens.
        private void PersonalNotificationsPopup_Opened(object sender, System.EventArgs e)
        {
            if (PersonalActivityPanelHost != null)
                AsyncUtility.SafeFireAndForget(
                    () => PersonalActivityPanelHost.LoadAsync(),
                    "GlassShell Activity Load");
        }

        // Mirrors MainWindow's UserSwitchPopup_Opened.
        private void PersonalUserSwitchPopup_Opened(object sender, System.EventArgs e)
        {
            if (PersonalUserSwitchPanelHost != null)
                AsyncUtility.SafeFireAndForget(
                    () => PersonalUserSwitchPanelHost.RefreshAsync(),
                    "GlassShell UserSwitch Refresh");
        }

        // Mirrors MainWindow's UserSwitchPopup_Closed: re-sync the
        // displayed current user after the user-switch panel closes.
        private void PersonalUserSwitchPopup_Closed(object sender, System.EventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.RefreshCurrentUserPublic();
        }
    }
}
