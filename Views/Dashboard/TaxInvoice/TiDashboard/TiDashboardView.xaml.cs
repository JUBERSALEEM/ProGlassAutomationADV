// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Dashboard/TaxInvoice/TiDashboard/TiDashboardView.xaml.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Views.Dashboard.TaxInvoice.TiDashboard
//
// PURPOSE:
//   Code-behind for TiDashboardView.xaml.
//   Minimal — all logic is in TiDashboardViewModel (MVVM pattern).
//   DataContext is set via DashboardView.xaml DataTemplate mapping.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Dashboard.TaxInvoice.TiDashboard
{
    /// <summary>
    /// Code-behind for the Tax Invoice dashboard tab view.
    /// DataContext = TiDashboardViewModel (set by DataTemplate in DashboardView.xaml).
    /// </summary>
    public partial class TiDashboardView : UserControl
    {
        public TiDashboardView()
        {
            InitializeComponent();
        }
    }
}