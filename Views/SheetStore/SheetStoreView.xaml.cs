using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.ViewModels.SheetStore;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation.Views.SheetStore
{
    public partial class SheetStoreView : UserControl
    {
        private Border? _lastSelectedRow;

        public SheetStoreView()
        {
            InitializeComponent();
        }

        private void Row_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border row &&
                row.Tag is GlassSheet sheet &&
                DataContext is SheetStoreViewModel vm)
            {
                // Reset previous row visual
                if (_lastSelectedRow != null)
                {
                    _lastSelectedRow.BorderBrush = Brushes.Transparent;
                    _lastSelectedRow.Background = Brushes.Transparent;
                }

                // Toggle: click same row to deselect
                if (vm.SelectedSheet == sheet)
                {
                    vm.SelectedSheet = null;
                    _lastSelectedRow = null;
                }
                else
                {
                    vm.SelectedSheet = sheet;

                    // Highlight the row
                    row.Background = new SolidColorBrush(Color.FromRgb(224, 231, 255)); // Light blue
                    if (Application.Current.Resources["AccentBrush"] is SolidColorBrush accent)
                        row.BorderBrush = accent;

                    _lastSelectedRow = row;
                }
            }
        }
    }
}