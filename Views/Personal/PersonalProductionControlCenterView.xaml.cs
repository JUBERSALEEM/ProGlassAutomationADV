// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Personal/PersonalProductionControlCenterView.xaml.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// GB-P7 (2026-09-18) — Glass Blush Personal Production Control Center.
// Declarative only: binds to the existing ProductionControlCenterViewModel
// (same bindings/commands/columns/filters as the Professional view).
// No business logic, no handlers, no state.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Personal
{
    /// <summary>
    /// Glass Blush Personal Production Control Center (glass-04 reference):
    /// night-palette control room on the SAME production VM.
    /// </summary>
    public partial class PersonalProductionControlCenterView : UserControl
    {
        public PersonalProductionControlCenterView()
        {
            InitializeComponent();
        }
    }
}
