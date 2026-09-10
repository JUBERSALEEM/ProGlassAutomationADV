using ProGlassAutomation.ViewModels.JobOrder.SavedJobOrdersList;
using System;
using ProGlassAutomation.Utilities;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ProGlassAutomation.Services.Dialogs;

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
                PlacementTarget = element,
                Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
                Style = (Style)FindResource("ModernContextMenuStyle")
            };

            // Open
            var openItem = new MenuItem
            {
                Header = "\uE8E5  Open Job Order",
                FontWeight = FontWeights.SemiBold,
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            openItem.Click += (_, __) => vm.OpenJobOrderCommand.Execute(row);
            menu.Items.Add(openItem);

            menu.Items.Add(new Separator
            {
                Style = (Style)FindResource("ModernMenuSeparatorStyle")
            });

            // ── Export sub-menu (Phase 9.5a-r3) ─────────────────────
            var exportMenu = new MenuItem
            {
                Header = "\uEDE1  Export",
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };

            var exportPdfItem = new MenuItem
            {
                Header = "\uE8A5  PDF — Production Sheet",
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            exportPdfItem.Click += async (_, __) =>
            {
                try
                {
                    await vm.ExportPdfAsync(row);
                }
                catch (Exception ex)
                {
                    Prompts.Show($"Failed:\n{ExceptionMessages.Deep(ex)}", "Export Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            exportMenu.Items.Add(exportPdfItem);

            var exportExcelItem = new MenuItem
            {
                Header = "\uE9F9  Excel — Multi-Sheet Workbook",
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            exportExcelItem.Click += async (_, __) =>
            {
                try
                {
                    await vm.ExportExcelAsync(row);
                }
                catch (Exception ex)
                {
                    Prompts.Show($"Failed:\n{ExceptionMessages.Deep(ex)}", "Export Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            exportMenu.Items.Add(exportExcelItem);

            menu.Items.Add(exportMenu);

            // Sharing Center — staged export + composer.
            var shareItem = new MenuItem
            {
                Header = "\uE72D  Share…",
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            shareItem.Click += async (_, __) =>
            {
                try
                {
                    await vm.ShareAsync(row);
                }
                catch (Exception ex)
                {
                    Prompts.Show($"Failed:\n{ExceptionMessages.Deep(ex)}", "Share Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            menu.Items.Add(shareItem);

            menu.Items.Add(new Separator
            {
                Style = (Style)FindResource("ModernMenuSeparatorStyle")
            });

            // Change Status sub-menu — friendly labels mapping to the stored
            // production statuses (which drive the production sheet + detail).
            var statusMenu = new MenuItem
            {
                Header = "\uE8C8  Change Status",
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            foreach (var (stored, label) in new[]
            {
                ("Draft", "Draft"),
                ("Released", "Released to Factory"),
                ("InProduction", "In Production"),
                ("Completed", "Completed"),
                ("Delivered", "Delivered"),
                ("Rejected", "Rejected")
            })
            {
                statusMenu.Items.Add(new MenuItem
                {
                    Header = label,
                    IsCheckable = true,
                    IsChecked = string.Equals(row.StatusRaw, stored, StringComparison.OrdinalIgnoreCase),
                    Style = (Style)FindResource("ModernCheckableMenuItemStyle"),
                    Padding = new Thickness(8, 6, 20, 6),
                    Command = vm.ChangeStatusCommand,
                    CommandParameter = Tuple.Create(row, stored)
                });
            }
            menu.Items.Add(statusMenu);

            // Change Priority sub-menu
            var priorityMenu = new MenuItem
            {
                Header = "\uE8D4  Change Priority",
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            foreach (var priority in new[] { "High", "Medium", "Low" })
            {
                priorityMenu.Items.Add(new MenuItem
                {
                    Header = priority,
                    IsCheckable = true,
                    IsChecked = string.Equals(row.PriorityRaw, priority, StringComparison.OrdinalIgnoreCase),
                    Style = (Style)FindResource("ModernCheckableMenuItemStyle"),
                    Padding = new Thickness(8, 6, 20, 6),
                    Command = vm.ChangePriorityCommand,
                    CommandParameter = Tuple.Create(row, priority)
                });
            }
            menu.Items.Add(priorityMenu);

            menu.Items.Add(new Separator
            {
                Style = (Style)FindResource("ModernMenuSeparatorStyle")
            });

            // Delete
            var deleteItem = new MenuItem
            {
                Header = "\uE74D  Delete Job Order",
                Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush"),
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            deleteItem.Click += (_, __) => vm.DeleteJobOrderCommand.Execute(row);
            menu.Items.Add(deleteItem);

            element.ContextMenu = menu;
            menu.IsOpen = true;
            e.Handled = true;
        }
    }
}