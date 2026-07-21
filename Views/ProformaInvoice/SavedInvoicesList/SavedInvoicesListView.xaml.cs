using System.Windows;
using System.Windows.Controls;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.ViewModels.ProformaInvoice.SavedInvoicesList;

namespace ProGlassAutomation.Views.ProformaInvoice.SavedInvoicesList
{
    public partial class SavedInvoicesListView : UserControl
    {
        public SavedInvoicesListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles right-click → Change Status → specific status click.
        /// Uses code-behind because passing both the row item AND the
        /// status string through XAML-only command binding is complex
        /// and error-prone (x:Array / MultiBinding issues).
        /// </summary>
        private async void OnChangeStatusClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem) return;
            if (menuItem.Tag is not string newStatus) return;

            // Walk up the context menu to find the data item
            var contextMenu = menuItem.Parent as MenuItem;
            if (contextMenu?.Parent is not ContextMenu cm) return;

            if (cm.PlacementTarget is not FrameworkElement target) return;
            if (target.DataContext is not SavedInvoiceListItem item) return;

            if (DataContext is not SavedInvoicesListViewModel vm) return;

            if (App.Invoices == null) return;

            try
            {
                var entity = await App.Invoices.GetByIdAsync(item.Id);
                if (entity == null) return;

                entity.Status = newStatus;
                await App.Invoices.UpdateAsync(entity);
                await vm.LoadInvoicesAsync();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Failed to change status: {ex.Message}",
                    "Status Change Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}