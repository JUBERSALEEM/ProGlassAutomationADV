using ProGlassAutomation.ViewModels.Production;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Production
{
    /// <summary>
    /// Shared stage body hosted inside all eight production module views
    /// (each module view owns its own stage-identity header). Triggers the
    /// initial async load when the view appears — the VMs never run async
    /// work in their constructors.
    ///
    /// PERFORMANCE: the master grid is virtualized (rows recycled, columns
    /// virtualized) and rows load in pages of 100 — scrolling near the
    /// bottom renders the next page, so very long jobs never lag.
    /// </summary>
    public partial class ProductionStageView : UserControl
    {
        public ProductionStageView()
        {
            InitializeComponent();
        }

        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ProductionStageViewModelBase vm)
            {
                Utilities.AsyncUtility.SafeFireAndForget(
                    vm.LoadAsync, "Production stage view load");
            }
        }

        /// <summary>
        /// Bubbled from the grid's internal ScrollViewer — forwards the
        /// scroll geometry so the VM can render the next 100 rows when
        /// the floor scrolls near the bottom.
        /// </summary>
        private void MasterGrid_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (DataContext is ProductionStageViewModelBase vm)
                vm.OnMasterListScrolled(e.VerticalOffset, e.ViewportHeight, e.ExtentHeight);
        }
    }
}
