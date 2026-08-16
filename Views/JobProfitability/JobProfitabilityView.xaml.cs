// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/JobProfitability/JobProfitabilityView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Job Profitability (PHASE C)
//
// Pure MVVM: no event handlers. Filtering, totals and refresh all live on
// JobProfitabilityViewModel; this file only runs InitializeComponent().
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.JobProfitability
{
    /// <summary>
    /// Portfolio view of per-invoice profitability. DataContext is
    /// <c>JobProfitabilityViewModel</c>, supplied by the shell through the
    /// MainWindow DataTemplate mapping.
    /// </summary>
    public partial class JobProfitabilityView : UserControl
    {
        public JobProfitabilityView()
        {
            InitializeComponent();
        }
    }
}
