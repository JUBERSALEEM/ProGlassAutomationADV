// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Personal/GlassShell.xaml.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// GB-P2 (2026-09-18) — Glass Blush Personal UI shell.
// Pure composition control: DataContext, commands and view models are all
// inherited from MainViewModel (assigned by the window in GB-P14). No
// business logic, no state, no handlers — the shell is declarative XAML
// only, by design (keeps the WPF structural + MVVM contracts clean).
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

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
    }
}
