using ProGlassAutomation.ViewModels.Production;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProGlassAutomation.Views.Production
{
    /// <summary>
    /// Shared stage-queue screen hosted by all eight production module views.
    /// Triggers the initial async load when the view appears (the VMs never
    /// run async work in their constructors) and resolves the per-stage
    /// icon-tile tint (a DynamicResource key cannot be bound directly).
    /// </summary>
    public partial class ProductionStageView : UserControl
    {
        public ProductionStageView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            ApplyStageTint();
            if (DataContext is ProductionStageViewModelBase vm)
            {
                Utilities.AsyncUtility.SafeFireAndForget(
                    vm.LoadAsync, "Production stage view load");
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ApplyStageTint();
        }

        /// <summary>
        /// Resolves the VM's StageTintBrushKey against the theme dictionaries.
        /// Theme-aware: re-applied whenever the DataContext changes; brushes
        /// are DynamicResource lookups so theme switches refresh naturally on
        /// the next navigation.
        /// </summary>
        private void ApplyStageTint()
        {
            if (DataContext is ProductionStageViewModelBase vm
                && Application.Current?.TryFindResource(vm.StageTintBrushKey) is Brush brush)
            {
                StageIconTile.Background = brush;
            }
        }
    }
}
