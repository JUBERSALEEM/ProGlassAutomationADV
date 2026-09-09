using System.Windows.Controls;

namespace ProGlassAutomation.Views.Reports.SalesmanReport
{
    /// <summary>
    /// Salesman Performance report view. All data comes from the ViewModel
    /// (invoice-derived analytics merged with the saved-salesman roster);
    /// this control is pure presentation with no code-behind logic.
    /// </summary>
    public partial class SalesmanReportView : UserControl
    {
        public SalesmanReportView() => InitializeComponent();
    }
}
