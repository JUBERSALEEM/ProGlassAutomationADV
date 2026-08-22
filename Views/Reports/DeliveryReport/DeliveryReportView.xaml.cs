using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.DeliveryReport;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Reports.DeliveryReport
{
    public partial class DeliveryReportView : UserControl
    {
        public DeliveryReportView() => InitializeComponent();

        /// <summary>
        /// Lazy-loads a row's related Delivery Notes the first time its
        /// inline expander opens — keeps the single grid responsive and
        /// avoids loading history for every row up front.
        /// </summary>
        private async void RowExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is not Expander expander) return;
            if (expander.DataContext is not DeliveryReportRow row) return;
            if (DataContext is not DeliveryReportViewModel vm) return;

            if (row.HasLoadedDeliveries) return;
            await vm.LoadRowDeliveriesAsync(row);
        }
    }
}
