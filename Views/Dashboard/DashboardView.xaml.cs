using ProGlassAutomation.ViewModels.Dashboard;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Dashboard
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Fires every time the user navigates to Dashboard.
        /// Delegates to the shell VM which refreshes the active sub-page.
        /// </summary>
        private void OnLoaded(object sender, global::System.Windows.RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
                vm.OnViewLoaded();
        }
    }
}