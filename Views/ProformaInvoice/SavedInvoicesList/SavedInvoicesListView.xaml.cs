using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.ViewModels.ProformaInvoice.SavedInvoicesList;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.ProformaInvoice.SavedInvoicesList
{
    public partial class SavedInvoicesListView : UserControl
    {
        public SavedInvoicesListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Builds a styled right-click context menu dynamically.
        /// Uses code-behind because ContextMenu has its own visual tree
        /// and WPF XAML bindings for commands don't resolve correctly.
        /// </summary>
        private void OnRowRightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element) return;
            if (element.DataContext is not SavedInvoiceListItem item) return;
            if (DataContext is not SavedInvoicesListViewModel vm) return;

            var menu = new ContextMenu
            {
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontSize = 12
            };

            menu.Items.Add(CreateMenuItem("📂  Open & Edit", vm.LoadInvoiceCommand, item, bold: true));
            menu.Items.Add(CreateMenuItem("📋  Duplicate", vm.DuplicateInvoiceCommand, item));
            menu.Items.Add(new Separator());

            menu.Items.Add(CreateMenuItem("📄  Export PDF", vm.ExportPdfFromListCommand, item));
            menu.Items.Add(CreateMenuItem("📊  Export Excel", vm.ExportExcelFromListCommand, item));
            menu.Items.Add(new Separator());

            // Change Status sub-menu
            var statusMenu = new MenuItem { Header = "🔄  Change Status" };
            statusMenu.Items.Add(CreateStatusItem("Draft", item, vm));
            statusMenu.Items.Add(CreateStatusItem("Sent", item, vm));
            statusMenu.Items.Add(CreateStatusItem("Approved", item, vm));
            statusMenu.Items.Add(CreateStatusItem("Rejected", item, vm));
            statusMenu.Items.Add(CreateStatusItem("Expired", item, vm));
            menu.Items.Add(statusMenu);

            menu.Items.Add(new Separator());

            var jobOrderItem = new MenuItem
            {
                Header = "📦  Convert to Job Order",
                IsEnabled = false,
                FontStyle = FontStyles.Italic
            };
            jobOrderItem.ToolTip = "Coming in Job Order module";
            menu.Items.Add(jobOrderItem);

            menu.Items.Add(new Separator());

            var deleteItem = CreateMenuItem("🗑  Delete", vm.DeleteInvoiceCommand, item);
            deleteItem.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
            menu.Items.Add(deleteItem);

            element.ContextMenu = menu;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private static MenuItem CreateMenuItem(string header, ICommand command, object parameter, bool bold = false)
        {
            var item = new MenuItem
            {
                Header = header,
                Command = command,
                CommandParameter = parameter,
                Padding = new Thickness(4, 6, 20, 6)
            };
            if (bold) item.FontWeight = FontWeights.SemiBold;
            return item;
        }

        private MenuItem CreateStatusItem(string status, SavedInvoiceListItem item, SavedInvoicesListViewModel vm)
        {
            var menuItem = new MenuItem
            {
                Header = status,
                Tag = status,
                Padding = new Thickness(4, 4, 20, 4)
            };

            menuItem.Click += async (s, e) =>
            {
                if (App.Invoices == null) return;
                try
                {
                    var entity = await App.Invoices.GetByIdAsync(item.Id);
                    if (entity == null) return;

                    entity.Status = status;
                    await App.Invoices.UpdateAsync(entity);
                    await vm.LoadInvoicesAsync();
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Failed to change status: {ex.Message}",
                        "Status Change Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            return menuItem;
        }
    }
}