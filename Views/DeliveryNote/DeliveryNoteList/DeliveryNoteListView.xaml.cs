using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList;
using System;
using ProGlassAutomation.Utilities;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ProGlassAutomation.Services.Dialogs;

namespace ProGlassAutomation.Views.DeliveryNote.DeliveryNoteList
{
    /// <summary>
    /// Code-behind for DeliveryNoteListView.xaml.
    ///
    /// CHAT 19: Master DN Tab 6 removed. 6 tabs only.
    /// FIX: OnGroupRowRightClick now includes full menu:
    ///   📂 Open latest DN
    ///   🔄 Change Status (submenu)
    ///   🧾 Generate Master Tax Invoice
    ///   🗑 Delete latest DN
    /// </summary>
    public partial class DeliveryNoteListView : UserControl
    {
        public DeliveryNoteListView()
        {
            InitializeComponent();
        }

        // ═════════════════════════════════════════════════════════════════
        //  TAB CARD CLICK
        // ═════════════════════════════════════════════════════════════════

        // ═════════════════════════════════════════════════════════════════
        //  JO GROUP ROW — LEFT CLICK
        // ═════════════════════════════════════════════════════════════════

        private void OnGroupRowLeftClick(
            object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element) return;
            if (element.DataContext is not JobOrderGroupRowViewModel group)
                return;
            if (DataContext is not DeliveryNoteListViewModel vm) return;

            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindAncestor<Button>(dep) != null) return;
            }

            vm.OpenGroupCommand.Execute(group);
            e.Handled = true;
        }

        // ═════════════════════════════════════════════════════════════════
        //  JO GROUP ROW — RIGHT CLICK (full menu restored)
        //
        //  Menu items:
        //    📂 Open latest DN
        //    🔄 Change Status → submenu per status
        //    🧾 Generate Master Tax Invoice
        //    🗑 Delete latest DN
        // ═════════════════════════════════════════════════════════════════

        private void OnGroupRowRightClick(
            object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element) return;
            if (element.DataContext is not JobOrderGroupRowViewModel group)
                return;
            if (DataContext is not DeliveryNoteListViewModel vm) return;

            var menu = new ContextMenu
            {
                PlacementTarget = element,
                Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
                Style = (Style)FindResource("ModernContextMenuStyle")
            };

            // ── Open latest DN — Dispatching only. Dispatched opens from God View.
            var openItem = new MenuItem
            {
                Header = $"📂  Open latest DN ({group.LatestDnId})",
                FontWeight = FontWeights.SemiBold,
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            openItem.Click += (_, __) =>
            {
                if (ProGlassAutomation.Services.DeliveryNote.DeliveryNoteWorkflow
                    .IsOpenForDispatch(group.LatestStatus))
                {
                    vm.OnDeliveryNoteOpenRequested?.Invoke(group.LatestDnDbId);
                    return;
                }

                Prompts.Show(
                    "Dispatched delivery notes open from God View.\n\n" +
                    "Use NEW DN on this row for remaining balance.",
                    "Open from God View",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            };
            menu.Items.Add(openItem);

            var masterItem = new MenuItem
            {
                Header = "📋  View Master DN (original from Job Order)",
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            masterItem.Click += (_, __) =>
                vm.OpenMasterCommand.Execute(group);
            menu.Items.Add(masterItem);

            menu.Items.Add(new Separator
            {
                Style = (Style)FindResource("ModernMenuSeparatorStyle")
            });

            // ── Change Status submenu ─────────────────────────────────
            var statusMenu = new MenuItem
            {
                Header = "🔄  Change Status",
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            foreach (var status in new[]
            {
                "Dispatching", "Dispatched", "Signed", "Received"
            })
            {
                var s = status;
                var statusItem = new MenuItem
                {
                    Header = s,
                    Style = (Style)FindResource("ModernCheckableMenuItemStyle"),
                    Padding = new Thickness(8, 6, 20, 6)
                };
                statusItem.Click += async (_, __) =>
                {
                    try
                    {
                        if (App.DeliveryNotes == null) return;

                        var latestDn = await App.DeliveryNotes
                            .GetByIdAsync(group.LatestDnDbId);
                        if (latestDn == null) return;

                        // Phase 3: enforce centralized status-transition rules.
                        var transition = ProGlassAutomation.Services.Workflow
                            .StatusTransitions.Validate(
                                ProGlassAutomation.Services.Workflow
                                    .StatusTransitions.Kind.DeliveryNote,
                                latestDn.Status, s);
                        if (!transition.IsValid)
                        {
                            Prompts.Show(
                                transition.Reason ?? "Invalid status change.",
                                "Status Change Not Allowed",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        latestDn.Status = s;
                        latestDn.UpdatedAt = DateTime.Now;
                        await App.DeliveryNotes.UpdateAsync(latestDn);
                        await vm.LoadAsync();
                    }
                    catch (Exception ex)
                    {
                        Prompts.Show(
                            $"Failed to change status:\n\n{ExceptionMessages.Deep(ex)}",
                            "Update Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                };
                statusMenu.Items.Add(statusItem);
            }
            menu.Items.Add(statusMenu);

            menu.Items.Add(new Separator
            {
                Style = (Style)FindResource("ModernMenuSeparatorStyle")
            });

            // ── Generate Master Tax Invoice ───────────────────────────
            var masterInvItem = new MenuItem
            {
                Header = "🧾  Generate Master Tax Invoice",
                IsEnabled = group.AnyDelivered,
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            masterInvItem.Click += (_, __) =>
                vm.GenerateMasterInvoiceCommand.Execute(group);
            if (!group.AnyDelivered)
            {
                masterInvItem.ToolTip =
                    "Available when at least one split DN is dispatched.";
            }
            menu.Items.Add(masterInvItem);

            menu.Items.Add(new Separator
            {
                Style = (Style)FindResource("ModernMenuSeparatorStyle")
            });

            // ── Delete latest DN ──────────────────────────────────────
            var deleteItem = new MenuItem
            {
                Header = $"🗑  Delete latest DN ({group.LatestDnId})",
                Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush"),
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            deleteItem.Click += (_, __) =>
            {
                // Find the row in AllItems and fire DeleteCommand
                // VM.DeleteCommand guards against Master DN deletion
                var latestRow = vm.AllItems
                    .FirstOrDefault(r => r.Id == group.LatestDnDbId);

                if (latestRow != null)
                    vm.DeleteCommand.Execute(latestRow);
                else
                    Prompts.Show(
                        "Could not find the delivery note to delete.\n\n" +
                        "Please refresh the list and try again.",
                        "Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
            };
            menu.Items.Add(deleteItem);

            menu.IsOpen = true;
        }

        // ═════════════════════════════════════════════════════════════════
        //  LEGACY DN ROW — LEFT CLICK
        // ═════════════════════════════════════════════════════════════════

        private void OnRowLeftClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element) return;
            if (element.DataContext is not DeliveryNoteRowViewModel row)
                return;
            if (DataContext is not DeliveryNoteListViewModel vm) return;

            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindAncestor<Button>(dep) != null) return;
            }

            vm.SelectRowCommand.Execute(row);
            e.Handled = true;
        }

        // ═════════════════════════════════════════════════════════════════
        //  LEGACY DN ROW — RIGHT CLICK
        // ═════════════════════════════════════════════════════════════════

        private void OnRowRightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element) return;
            if (element.DataContext is not DeliveryNoteRowViewModel row)
                return;
            if (DataContext is not DeliveryNoteListViewModel vm) return;

            var menu = new ContextMenu
            {
                PlacementTarget = element,
                Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
                Style = (Style)FindResource("ModernContextMenuStyle")
            };

            var openItem = new MenuItem
            {
                Header = "📂  Open Delivery Note",
                FontWeight = FontWeights.SemiBold,
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            openItem.Click += (_, __) => vm.OpenCommand.Execute(row);
            menu.Items.Add(openItem);

            menu.Items.Add(new Separator
            {
                Style = (Style)FindResource("ModernMenuSeparatorStyle")
            });

            var statusMenu = new MenuItem
            {
                Header = "🔄  Change Status",
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            foreach (var status in new[]
            {
                "Dispatching", "Dispatched", "Signed", "Received"
            })
            {
                var s = status;
                var statusItem = new MenuItem
                {
                    Header = s,
                    IsCheckable = true,
                    IsChecked = string.Equals(row.Status, s, StringComparison.OrdinalIgnoreCase),
                    Style = (Style)FindResource("ModernCheckableMenuItemStyle"),
                    Padding = new Thickness(8, 6, 20, 6)
                };
                statusItem.Click += (_, __) =>
                    vm.ChangeStatusCommand.Execute((row, s));
                statusMenu.Items.Add(statusItem);
            }
            menu.Items.Add(statusMenu);

            menu.Items.Add(new Separator
            {
                Style = (Style)FindResource("ModernMenuSeparatorStyle")
            });

            var deleteItem = new MenuItem
            {
                Header = "🗑  Delete Delivery Note",
                Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush"),
                Style = (Style)FindResource("ModernMenuItemStyle"),
                Padding = new Thickness(8, 6, 20, 6)
            };
            deleteItem.Click += (_, __) => vm.DeleteCommand.Execute(row);
            menu.Items.Add(deleteItem);

            menu.IsOpen = true;
        }

        // ═════════════════════════════════════════════════════════════════
        //  CRASH FIX — FindAncestor safe for FrameworkContentElement
        // ═════════════════════════════════════════════════════════════════

        private static T? FindAncestor<T>(DependencyObject current)
            where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;

                if (current is not Visual and not Visual3D)
                {
                    current = LogicalTreeHelper.GetParent(current);
                    continue;
                }

                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}