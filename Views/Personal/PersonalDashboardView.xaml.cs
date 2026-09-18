// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Personal/PersonalDashboardView.xaml.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// GB-P4 (2026-09-18) — Glass Blush Personal dashboard.
// Declarative only: binds to the existing OverviewDashboardViewModel
// (same bindings/commands/i18n as the Professional OverviewDashboardView).
// No business logic, no handlers, no state.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Personal
{
    /// <summary>
    /// Glass Blush Personal dashboard (glass-01 reference).
    /// References the SAME OverviewDashboardViewModel as the
    /// Professional dashboard — different composition, same contract.
    /// </summary>
    public partial class PersonalDashboardView : UserControl
    {
        public PersonalDashboardView()
        {
            InitializeComponent();
        }
    }
}
