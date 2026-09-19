// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Personal/PersonalSalesReportView.xaml.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// GB-P11 (2026-09-19) — Glass Blush Personal reports hub (glass-08 family).
// Binds to the SAME SalesReportViewModel (NavItems/NavigateReportCommand/
// ActiveKey/ActiveViewModel). InitializeComponent only.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Personal
{
    /// <summary>
    /// Glass Blush Personal reports hub: glass report pill strip over the
    /// same SalesReportViewModel; the Executive page renders glass-08.
    /// </summary>
    public partial class PersonalSalesReportView : UserControl
    {
        public PersonalSalesReportView()
        {
            InitializeComponent();
        }
    }
}
