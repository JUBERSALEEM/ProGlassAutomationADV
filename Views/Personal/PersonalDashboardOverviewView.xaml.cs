// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Personal/PersonalDashboardOverviewView.xaml.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// GB-P4 (2026-09-18) / GB-P10 split (2026-09-19) — the glass-01 overview
// surface, extracted to its own view so PersonalDashboardView can be the
// dashboard SHELL (page pills + content host). Binds to the SAME
// OverviewDashboardViewModel (DataContext inherited from the host).
// InitializeComponent only.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Personal
{
    /// <summary>
    /// Glass Blush Personal dashboard — overview page (glass-01).
    /// </summary>
    public partial class PersonalDashboardOverviewView : UserControl
    {
        public PersonalDashboardOverviewView()
        {
            InitializeComponent();
        }
    }
}
