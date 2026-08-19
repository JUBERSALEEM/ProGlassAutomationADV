using System.Windows;
using System.Windows.Controls;
namespace ProGlassAutomation.Views.Reports.GlassSpecReport
{
    public partial class GlassSpecReportView : UserControl
    {
        public GlassSpecReportView() => InitializeComponent();
        private void Detail_Click(object sender, RoutedEventArgs e) { if(DataContext is ViewModels.Reports.GlassSpecReport.GlassSpecReportViewModel vm) vm.ShowAggregation = false; }
        private void Agg_Click(object sender, RoutedEventArgs e) { if(DataContext is ViewModels.Reports.GlassSpecReport.GlassSpecReportViewModel vm) vm.ShowAggregation = true; }
    }
}
