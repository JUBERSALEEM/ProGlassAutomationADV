// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/JobProfitability/JobFullDetailView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Job Profitability (PHASE C)
//
// Pure MVVM: no event handlers. The DataContext is the SAME
// JobDetailViewModel the 420px sidebar binds to, so the full-screen page
// and the sidebar always show identical figures. Back navigation lives on
// the parent JobProfitabilityView (it owns CloseFullDetailCommand).
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.JobProfitability
{
    /// <summary>
    /// Full-screen detail page for one confirmed PI, opened on double-click.
    /// </summary>
    public partial class JobFullDetailView : UserControl
    {
        public JobFullDetailView()
        {
            InitializeComponent();
        }
    }
}
