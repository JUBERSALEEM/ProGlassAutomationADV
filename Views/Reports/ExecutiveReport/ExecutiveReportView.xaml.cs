using System.Windows.Controls;

namespace ProGlassAutomation.Views.Reports.ExecutiveReport
{
    /// <summary>
    /// Executive Summary report view — the cross-module order-to-cash funnel.
    /// All data comes from the ViewModel (GetExecutiveSummaryAsync); this
    /// control is pure presentation with no code-behind logic.
    /// </summary>
    public partial class ExecutiveReportView : UserControl
    {
        public ExecutiveReportView() => InitializeComponent();
    }
}
