// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Personal/PersonalJobOrderView.xaml.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// GB-P6 (2026-09-18) — Glass Blush Personal Job Order.
// Declarative only: binds to the existing JobOrderViewModel (3 tabs +
// list/detail — same commands/booleans/i18n as the Professional shell).
// No business logic, no handlers, no state.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Personal
{
    /// <summary>
    /// Glass Blush Personal Job Order (glass-03 reference): glass tab
    /// strip + status timeline + the same sub-views.
    /// </summary>
    public partial class PersonalJobOrderView : UserControl
    {
        public PersonalJobOrderView()
        {
            InitializeComponent();
        }
    }
}
