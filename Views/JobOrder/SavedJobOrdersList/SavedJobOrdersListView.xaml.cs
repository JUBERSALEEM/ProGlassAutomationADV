using ProGlassAutomation.ViewModels.JobOrder.SavedJobOrdersList;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.JobOrder.SavedJobOrdersList
{
    public partial class SavedJobOrdersListView : UserControl
    {
        public SavedJobOrdersListView()
        {
            InitializeComponent();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SavedJobOrdersListViewModel vm)
                await vm.LoadJobOrdersAsync();
        }

        /// <summary>
        /// Builds the right-click context menu in code-behind because
        /// XAML ContextMenu bindings are unreliable in WPF DataTemplates.
        /// Same pattern used in SavedInvoicesListView.xaml.cs.
        ///
        /// Phase 9.5a-r3: adds "Export PDF" and "Export Excel" items
        /// that call SavedJobOrdersListViewModel.ExportPdfAsync/ExcelAsync.
        /// </summary>
        private void OnRowRightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element) return;
            if (element.DataContext is not JobOrderRowViewModel row) return;
            if (DataContext is not SavedJobOrdersListViewModel vm) return;

            var menu = new ContextMenu
            {
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontSize = 12
            };

            // Open
            var openItem = new MenuItem
            {
                Header = "📂  Open",
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(4, 6, 20, 6)
            };
            openItem.Click += (_, __) => vm.OpenJobOrderCommand.Execute(row);
            menu.Items.Add(openItem);

            menu.Items.Add(new Separator());

            // ── Export sub-menu (Phase 9.5a-r3) ─────────────────────
            var exportMenu = new MenuItem
            {
                Header = "📤  Export",
                Padding = new Thickness(4, 6, 20, 6)
            };

            var exportPdfItem = new MenuItem
            {
                Header = "📄  PDF — Production Sheet",
                Padding = new Thickness(4, 4, 16, 4)
            };
            exportPdfItem.Click += async (_, __) =>
            {
                try
                {
                    await vm.ExportPdfAsync(row);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed:\n{ex.Message}", "Export Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            exportMenu.Items.Add(exportPdfItem);

            var exportExcelItem = new MenuItem
            {
                Header = "📊  Excel — Multi-Sheet Workbook",
                Padding = new Thickness(4, 4, 16, 4)
            };
            exportExcelItem.Click += async (_, __) =>
            {
                try
                {
                    await vm.ExportExcelAsync(row);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed:\n{ex.Message}", "Export Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            exportMenu.Items.Add(exportExcelItem);

            menu.Items.Add(exportMenu);

            menu.Items.Add(new Separator());

            // Change Status sub-menu
            var statusMenu = new MenuItem
            {
                Header = "📊  Change Status",
                Padding = new Thickness(4, 6, 20, 6)
            };
            // Status and priority go through the ViewModel commands so the
            // update keeps the indexed column AND the embedded snapshot in
            // sync (the detail view and production sheet read the snapshot),
            // and so the error handling + audit trail live in one place.
            // Same Tuple parameter convention as the Proforma Invoice list.
            foreach (var status in new[] { "Draft", "Released", "InProduction", "Completed", "Delivered" })
            {
                string label = status switch
                {
                    "InProduction" => "In Production",
                    _ => status
                };
                statusMenu.Items.Add(new MenuItem
                {
                    Header = label,
                    IsChecked = string.Equals(row.StatusRaw, status, StringComparison.OrdinalIgnoreCase),
                    Padding = new Thickness(4, 4, 16, 4),
                    Command = vm.ChangeStatusCommand,
                    CommandParameter = Tuple.Create(row, status)
                });
            }
            menu.Items.Add(statusMenu);

            // Change Priority sub-menu
            var priorityMenu = new MenuItem
            {
                Header = "🏷️  Change Priority",
                Padding = new Thickness(4, 6, 20, 6)
            };
            foreach (var priority in new[] { "High", "Medium", "Low" })
            {
                priorityMenu.Items.Add(new MenuItem
                {
                    Header = priority,
                    IsChecked = string.Equals(row.PriorityRaw, priority, StringComparison.OrdinalIgnoreCase),
                    Padding = new Thickness(4, 4, 16, 4),
                    Command = vm.ChangePriorityCommand,
                    CommandParameter = Tuple.Create(row, priority)
                });
            }
            menu.Items.Add(priorityMenu);

            menu.Items.Add(new Separator());

            // Delete
            var deleteItem = new MenuItem
            {
                Header = "🗑️  Delete",
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44)),
                Padding = new Thickness(4, 6, 20, 6)
            };
            deleteItem.Click += (_, __) => vm.DeleteJobOrderCommand.Execute(row);
            menu.Items.Add(deleteItem);

            element.ContextMenu = menu;
            menu.IsOpen = true;
            e.Handled = true;
        }
    }
}