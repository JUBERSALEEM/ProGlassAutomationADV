using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            // Phase 10.5c — added TabReturned as 6th tab
            _tabButtons = new[]
            {
                TabAll, TabPrepared, TabDelivered, TabPending, TabRework, TabReturned
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

        // ─────────────────────────────────────────────────────────────────
        //  Phase 10.5c — Single left-click on a DN row selects it.
        //  Selection updates VM.SelectedRow → hero cards swap to that
        //  DN's stats (Ordered/Delivered/Balance/Driver+Vehicle+Date+Time).
        //  Clicking the OPEN button still opens detail view (button
        //  handles its own click and marks event handled).
        // ─────────────────────────────────────────────────────────────────

        private void OnRowLeftClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element) return;
            if (element.DataContext is not DeliveryNoteRowViewModel row) return;
            if (DataContext is not DeliveryNoteListViewModel vm) return;

            // Ignore clicks that originated on the OPEN button (bubbling)
            // — if the source is inside a Button, let the button handle it.
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindAncestor<Button>(dep) != null) return;
            }

            vm.SelectRowCommand.Execute(row);
            e.Handled = true;
        }

        private static T? FindAncestor<T>(DependencyObject current)
            where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void OnRowRightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element) return;
            if (element.DataContext is not DeliveryNoteRowViewModel row) return;
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
    }
}