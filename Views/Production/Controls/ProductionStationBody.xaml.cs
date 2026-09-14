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
            if (MasterGrid?.Columns == null || MasterGrid.Columns.Count < 13) return;

            if (vm.IsCuttingStation)
            {
                // 2026-09-14 (owner directive): the cutting sidebar keeps
                // only the flow totals — JOB NO · TOTAL · DONE · BALANCE ·
                // STATUS(COMPLETED). The per-glass detail (size / glass /
                // treatment) and the OUTER/INNER work grids with
                // SAVE DONE / COMPLETE ALL live in the full-page detail
                // (double-click a row).
                // Indices: 0 #, 1 JOB NO, 2 CUSTOMER, 3 W×H, 4 GLASS,
                // 5 TREATMENT, 6 LM, 7 LOAD, 8 MATCH, 9 TOTAL, 10 DONE,
                // 11 BAL, 12 STATUS.
                for (int i = 0; i < MasterGrid.Columns.Count; i++)
                {
                    bool keep = i == 0 || i == 1 || i >= 9;
                    MasterGrid.Columns[i].Visibility =
                        keep ? Visibility.Visible : Visibility.Collapsed;
                }
                return;
            }

            // Other stations keep their per-stage columns.
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
