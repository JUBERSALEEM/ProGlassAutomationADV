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
    public partial class GlassShell : UserControl, System.ComponentModel.INotifyPropertyChanged
    {
        public GlassShell()
        {
            InitializeComponent();
        }

        // ── 2026-09-19: sidebar hamburger (rail 140 ⇄ 56 icon-only) ─────
        // Shell-local UI state (not user config yet — the phase plan
        // persists it). The nav styles in GlassShellStyles.xaml hide their
        // label elements while this is true, and the rail Border collapses
        // to the 56px icon column; ClipToBounds keeps any open accordion
        // sub-list inside the rail instead of spilling onto the content.
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        private bool _sidebarCollapsed;
        public bool SidebarCollapsed
        {
            get => _sidebarCollapsed;
            set
            {
                if (_sidebarCollapsed == value) return;
                _sidebarCollapsed = value;
                OnPropertyChanged(nameof(SidebarCollapsed));
            }
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
