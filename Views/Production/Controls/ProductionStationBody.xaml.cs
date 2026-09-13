using ProGlassAutomation.ViewModels.Production;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Production.Controls
{
    public partial class ProductionStationBody : UserControl
    {
        public ProductionStationBody()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => ApplyStationColumns();
            Loaded += (_, _) => ApplyStationColumns();
        }

        private void ApplyStationColumns()
        {
            if (DataContext is not ProductionStageViewModelBase vm) return;
            if (MasterGrid?.Columns == null || MasterGrid.Columns.Count < 9) return;
            // 3 Size, 4 Glass, 5 Treat, 6 Lm, 7 Load, 8 Match
            MasterGrid.Columns[3].Visibility = Vis(vm.ShowSizeColumn);
            MasterGrid.Columns[4].Visibility = Vis(vm.ShowGlassColumn);
            MasterGrid.Columns[5].Visibility = Vis(vm.ShowTreatmentColumn);
            MasterGrid.Columns[6].Visibility = Vis(vm.ShowLmColumn);
            MasterGrid.Columns[7].Visibility = Vis(vm.ShowLoadColumn);
            MasterGrid.Columns[8].Visibility = Vis(vm.ShowMatchColumn);
        }

        private static Visibility Vis(bool on) =>
            on ? Visibility.Visible : Visibility.Collapsed;

        private void MasterGrid_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (DataContext is ProductionStageViewModelBase vm)
                vm.OnMasterListScrolled(e.VerticalOffset, e.ViewportHeight, e.ExtentHeight);
        }
    }
}
