using System.Windows.Controls;

namespace ProGlassAutomation.Views.Reports.CustomerReport
{
    /// <summary>
    /// Customer Directory &amp; Receivables report view. All data comes from
    /// the ViewModel (merged master-data roster + live analytics); this
    /// control is pure presentation with no code-behind logic.
    /// </summary>
    public partial class CustomerReportView : UserControl
    {
        public CustomerReportView() => InitializeComponent();
    }
}
