using System.Windows.Controls;

namespace ProGlassAutomation.Views.Reports.CompanyReport
{
    /// <summary>
    /// Company Book &amp; Receivables report view. All data comes from the
    /// ViewModel (SavedCompany master data merged with live analytics); this
    /// control is pure presentation with no code-behind logic.
    /// </summary>
    public partial class CompanyReportView : UserControl
    {
        public CompanyReportView() => InitializeComponent();
    }
}
