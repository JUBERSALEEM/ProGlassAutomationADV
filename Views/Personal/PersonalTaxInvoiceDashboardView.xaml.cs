// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Personal/PersonalTaxInvoiceDashboardView.xaml.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// GB-P10 (2026-09-19) — Glass Blush Personal tax-invoice / invoice-aging
// page (glass-07). Rendered by PersonalDashboardView (the Personal
// dashboard shell) when ActivePage is a TiDashboardViewModel (DataContext
// inherited from the host). InitializeComponent only.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Personal
{
    /// <summary>
    /// Glass Blush Personal tax-invoice dashboard (glass-07 reference):
    /// filter pills, KPI stat cards, glowing invoice-aging bars, recent
    /// invoices — all on the SAME TiDashboardViewModel.
    /// </summary>
    public partial class PersonalTaxInvoiceDashboardView : UserControl
    {
        public PersonalTaxInvoiceDashboardView()
        {
            InitializeComponent();
        }
    }
}
