using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.PiReport;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Reports.PiReport
{
    public partial class PiReportView : UserControl
    {
        public PiReportView() => InitializeComponent();

        /// <summary>
        /// Lazy-loads a PI's itemized specification rows + reconciliation the
        /// first time its inline expander opens (single grid — no side panel).
        /// </summary>
        private async void RowExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is not Expander expander) return;
            if (expander.DataContext is not PiReportRow row) return;
            if (DataContext is not PiReportViewModel vm) return;

            if (row.HasLoadedDetail) return;
            await vm.LoadRowDetailAsync(row);
        }
    }
}
