using System.Windows;
using System.Windows.Controls;
namespace ProGlassAutomation.Views.Reports.CompanyReport
{
    public partial class CompanyReportView : UserControl
    {
        public CompanyReportView() => InitializeComponent();
        private void Customer_Click(object sender, RoutedEventArgs e)
        {
            if(DataContext is ViewModels.Reports.CompanyReport.CompanyReportViewModel vm) vm.ShowCustomers = true;
        }
        private void Company_Click(object sender, RoutedEventArgs e)
        {
            if(DataContext is ViewModels.Reports.CompanyReport.CompanyReportViewModel vm) vm.ShowCustomers = false;
        }
    }
}
