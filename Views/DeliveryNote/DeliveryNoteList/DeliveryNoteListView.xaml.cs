using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ProGlassAutomation.Views.DeliveryNote.DeliveryNoteList
{
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
                TabAll, TabPrepared, TabDelivered,
                TabPending, TabRework, TabReturned
            };
        }

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
                _tabButtons[i].Style = i == newIndex ? activeStyle : inactiveStyle;
        }

        // ═════════════════════════════════════════════════════════════════
        //  Ship 8b — JO GROUP ROW HANDLERS
        // ═════════════════════════════════════════════════════════════════

        private void OnGroupRowLeftClick(
            object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element) return;
            if (element.DataContext is not JobOrderGroupRowViewModel group)
                return;
            if (DataContext is not DeliveryNoteListViewModel vm) return;

            // CRASH FIX: e.OriginalSource can be a Run (FrameworkContentElement)
            // which is NOT a Visual. FindAncestor now handles this safely.
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindAncestor<Button>(dep) != null) return;
            }

            vm.OpenGroupCommand.Execute(group);
            e.Handled = true;
        }

        private void OnGroupRowRightClick(
            object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element) return;
            if (element.DataContext is not JobOrderGroupRowViewModel group)
                return;
            if (DataContext is not DeliveryNoteListViewModel vm) return;

            var menu = new ContextMenu();

            var openItem = new MenuItem
            {
                Header = $"📂  Open latest DN ({group.LatestDnId})"
            };
            openItem.Click += (_, __) =>
                vm.OpenGroupCommand.Execute(group);
            menu.Items.Add(openItem);

            menu.Items.Add(new Separator());

            var masterInvItem = new MenuItem
            {
                Header = "🧾  Generate Master Tax Invoice",
                IsEnabled = group.AllDelivered
            };
            masterInvItem.Click += (_, __) =>
                vm.GenerateMasterInvoiceCommand.Execute(group);
            if (!group.AllDelivered)
            {
                masterInvItem.ToolTip =
                    "Available only when every split DN is " +
                    "Delivered / Signed / Received.";
            }
            menu.Items.Add(masterInvItem);

            menu.IsOpen = true;
        }

        // ═════════════════════════════════════════════════════════════════
        //  DEPRECATED — per-DN row handlers (Ship 8b transition)
        //  Safe to delete AFTER confirming no XAML uses DnRowTemplate.
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

            var deleteItem = new MenuItem { Header = "🗑  Delete" };
            deleteItem.Click += (_, __) => vm.DeleteCommand.Execute(row);
            menu.Items.Add(deleteItem);

            menu.IsOpen = true;
        }

        // ═════════════════════════════════════════════════════════════════
        //  CRASH FIX — FindAncestor safe for FrameworkContentElement
        //
        //  VisualTreeHelper.GetParent() only works on Visual/Visual3D.
        //  When user clicks on a <Run> inside a <TextBlock>, the
        //  e.OriginalSource is a Run (FrameworkContentElement), which
        //  crashes VisualTreeHelper.GetParent().
        //
        //  Fix: If the starting element is not a Visual, walk the LOGICAL
        //  tree first until we find a Visual (usually the parent TextBlock),
        //  then continue up the VISUAL tree from there.
        // ═════════════════════════════════════════════════════════════════

        private static T? FindAncestor<T>(DependencyObject current)
            where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;

                // If current is not a Visual (e.g. Run, Span),
                // walk the logical tree until we find a Visual
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