using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ProGlassAutomation.ViewModels.Optimization;

namespace ProGlassAutomation.Views.Optimization.Tabs
{
    public partial class JobsTabView : UserControl
    {
        public JobsTabView()
        {
            InitializeComponent();
        }

        // Only translate view gestures to the same commands as the buttons.
        // Bubbling events deliberately leave handled checkbox/text-editor/button
        // clicks alone; double-clicking Qty must not import or navigate a row.
        private void PaneRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled || e.ClickCount != 2 || DataContext is not OptimizationViewModel vm
                || sender is not FrameworkElement { DataContext: OptimizationViewModel.BuildUpLayerVm pane }) return;
            if (!vm.ImportPaneSizesCommand.CanExecute(pane)) return;
            vm.ImportPaneSizesCommand.Execute(pane);
            e.Handled = true;
        }

        private void CuttingRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled || e.ClickCount != 2 || DataContext is not OptimizationViewModel vm
                || sender is not FrameworkElement { DataContext: OptimizationViewModel.PieceEntryVm piece }) return;
            if (!vm.OpenPieceSizesCommand.CanExecute(piece)) return;
            vm.OpenPieceSizesCommand.Execute(piece);
            e.Handled = true;
        }
    }
}
