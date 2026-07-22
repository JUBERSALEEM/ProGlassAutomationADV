using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ProGlassAutomation.Views.DeliveryNote.DeliveryNoteList
{
    public partial class DeliveryNoteListView : UserControl
    {
        // All tab card buttons in order — used to reset styles on switch
        private Button[] _tabButtons = null!;

        public DeliveryNoteListView()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Wire up tab button array after XAML is fully initialized
            _tabButtons = new[]
            {
                TabAll, TabPrepared, TabDelivered, TabPending, TabRework
            };
        }

        /// <summary>
        /// Handles tab card button clicks.
        /// Finds which tab index was clicked, updates VM SelectedTabIndex,
        /// and swaps active/inactive styles on all card buttons.
        /// </summary>
        private void OnTabCardClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button clicked) return;
            if (DataContext is not DeliveryNoteListViewModel vm) return;
            if (_tabButtons == null) return;

            // Find index of clicked button
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

            // Update VM tab index
            vm.SelectedTabIndex = newIndex;

            // Swap styles — active gets gradient border, inactive gets ghost
            var activeStyle = (Style)FindResource("TabCardActiveStyle");
            var inactiveStyle = (Style)FindResource("TabCardInactiveStyle");

            for (int i = 0; i < _tabButtons.Length; i++)
                _tabButtons[i].Style = i == newIndex ? activeStyle : inactiveStyle;
        }

        /// <summary>
        /// Right-click context menu for DN rows (code-behind per gotcha #5).
        /// </summary>
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