// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Personal/PersonalExecutiveReportView.xaml.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// GB-P11 (2026-09-19) — Glass Blush Personal executive report (glass-08).
// Binds to the SAME ExecutiveReportViewModel (DataContext inherited from
// the PersonalSalesReportView hub's content host). InitializeComponent only.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Personal
{
    /// <summary>
    /// Glass Blush Personal executive report (glass-08 reference): filter
    /// bar, summary/health bar cards, glowing order-to-cash funnel, top
    /// specifications/customers mix rows — all on the SAME
    /// ExecutiveReportViewModel.
    /// </summary>
    public partial class PersonalExecutiveReportView : UserControl
    {
        public PersonalExecutiveReportView()
        {
            InitializeComponent();
        }
    }
}
