using ProGlassAutomation.ViewModels.Production;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Production
{
    /// <summary>
    /// Shared stage-queue screen hosted by all eight production module views.
    /// Triggers the initial async load when the view appears (the VMs never
    /// run async work in their constructors).
    /// </summary>
    public partial class ProductionStageView : UserControl
    {
        public ProductionStageView()
        {
            InitializeComponent();
        }

        private void OnViewLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ProductionStageViewModelBase vm)
            {
                Utilities.AsyncUtility.SafeFireAndForget(
                    vm.LoadAsync, "Production stage view load");
            }
        }
    }
}
