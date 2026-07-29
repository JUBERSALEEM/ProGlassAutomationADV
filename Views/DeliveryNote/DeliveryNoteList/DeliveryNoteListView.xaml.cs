using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

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
        private Button[] _tabButtons = null!;

        public DeliveryNoteListView()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _tabButtons = new[]
            {
                TabAll,       // 0
                TabPrepared,  // 1
                TabDelivered, // 2
                TabPending,   // 3
                TabRework,    // 4
                TabReturned   // 5
            };
        }

        // ═════════════════════════════════════════════════════════════════
        //  TAB CARD CLICK
        // ═════════════════════════════════════════════════════════════════

        private void OnTabCardClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button clicked) return;
            if (DataContext is not DeliveryNoteListViewModel vm) return;
            if (_tabButtons == null) return;

            int newIndex = -1;
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] == clicked)
                {
                    newIndex = i;
                    break;
                }
            }

            if (newIndex < 0) return;

            vm.SelectedTabIndex = newIndex;

            var activeStyle = (Style)FindResource("TabCardActiveStyle");
            var inactiveStyle = (Style)FindResource("TabCardInactiveStyle");

            for (int i = 0; i < _tabButtons.Length; i++)
                _tabButtons[i].Style = i == newIndex
                    ? activeStyle
                    : inactiveStyle;
        }

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

            var menu = new ContextMenu();

            // ── Open latest DN ────────────────────────────────────────
            var openItem = new MenuItem
            {
                Header = $"📂  Open latest DN ({group.LatestDnId})"
            };
            openItem.Click += (_, __) =>
                vm.OpenGroupCommand.Execute(group);
            menu.Items.Add(openItem);

            menu.Items.Add(new Separator());

            // ── Change Status submenu ─────────────────────────────────
            var statusMenu = new MenuItem { Header = "🔄  Change Status" };
            foreach (var status in new[]
            {
                "Draft", "Dispatched", "Delivered", "Signed", "Received"
            })
            {
                var s = status;
                var statusItem = new MenuItem { Header = s };
                statusItem.Click += async (_, __) =>
                {
                    try
                    {
                        if (App.DeliveryNotes == null) return;

                        var latestDn = await App.DeliveryNotes
                            .GetByIdAsync(group.LatestDnDbId);
                        if (latestDn == null) return;

                        latestDn.Status = s;
                        latestDn.UpdatedAt = DateTime.Now;
                        await App.DeliveryNotes.UpdateAsync(latestDn);
                        await vm.LoadAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Failed to change status:\n\n{ex.Message}",
                            "Update Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                };
                statusMenu.Items.Add(statusItem);
            }
            menu.Items.Add(statusMenu);

            menu.Items.Add(new Separator());

            // ── Generate Master Tax Invoice ───────────────────────────
            var masterInvItem = new MenuItem
            {
                Header = "🧾  Generate Master Tax Invoice",
                IsEnabled = group.AnyDelivered
            };
            masterInvItem.Click += (_, __) =>
                vm.GenerateMasterInvoiceCommand.Execute(group);
            if (!group.AnyDelivered)
            {
                masterInvItem.ToolTip =
                    "Available when at least one split DN is dispatched.";
            }
            menu.Items.Add(masterInvItem);

            menu.Items.Add(new Separator());

            // ── Delete latest DN ──────────────────────────────────────
            var deleteItem = new MenuItem
            {
                Header = $"🗑  Delete latest DN ({group.LatestDnId})",
                Foreground = Brushes.Firebrick
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
                    MessageBox.Show(
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

            var menu = new ContextMenu();

            var openItem = new MenuItem { Header = "📂  Open" };
            openItem.Click += (_, __) => vm.OpenCommand.Execute(row);
            menu.Items.Add(openItem);

            menu.Items.Add(new Separator());

            var statusMenu = new MenuItem { Header = "🔄  Change Status" };
            foreach (var status in new[]
            {
                "Draft", "Dispatched", "Delivered", "Signed", "Received"
            })
            {
                var s = status;
                var statusItem = new MenuItem { Header = s };
                statusItem.Click += (_, __) =>
                    vm.ChangeStatusCommand.Execute((row, s));
                statusMenu.Items.Add(statusItem);
            }
            menu.Items.Add(statusMenu);

            menu.Items.Add(new Separator());

            var deleteItem = new MenuItem
            {
                Header = "🗑  Delete",
                Foreground = Brushes.Firebrick
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