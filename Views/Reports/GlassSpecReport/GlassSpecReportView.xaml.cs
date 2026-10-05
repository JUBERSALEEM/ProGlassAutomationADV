using System.Windows.Controls;

namespace ProGlassAutomation.Views.Reports.GlassSpecReport
{
    /// <summary>
    /// Glass Specifications &amp; Traceability report view. All data comes from
    /// the ViewModel (row-level PI snapshot extraction + spec aggregation);
    /// this control is pure presentation with no code-behind logic.
    /// </summary>
    public partial class GlassSpecReportView : UserControl
    {
        public GlassSpecReportView() => InitializeComponent();
    }
}
