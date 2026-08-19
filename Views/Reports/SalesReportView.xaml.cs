using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Reports
{
    public partial class SalesReportView : UserControl
    {
        public SalesReportView()
        {
            InitializeComponent();
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string key)
            {
                if (DataContext is ViewModels.Reports.SalesReportViewModel vm)
                {
                    vm.ActiveKey = key;
                    // Sync statement legacy view
                    if (key == "Statement")
                        vm.ShowList = true;
                }
            }
        }
    }
}
