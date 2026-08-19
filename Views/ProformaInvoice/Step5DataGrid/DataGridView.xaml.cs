using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ProGlassAutomation.Views.ProformaInvoice.Step5DataGrid
{
    public partial class DataGridView : UserControl
    {
        public DataGridView()
        {
            InitializeComponent();
        }

        private void ExportMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.ContextMenu == null)
                return;

            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = PlacementMode.Bottom;
            button.ContextMenu.VerticalOffset = 4;
            button.ContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }
}
