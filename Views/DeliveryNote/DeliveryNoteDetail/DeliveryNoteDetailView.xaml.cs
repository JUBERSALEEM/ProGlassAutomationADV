using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Utilities;

namespace ProGlassAutomation.Views.DeliveryNote.DeliveryNoteDetail
{
    public partial class DeliveryNoteDetailView
    {
        private bool _actionsMenuOpen;

        public DeliveryNoteDetailView()
        {
            InitializeComponent();
        }

        private void ActionsMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (ActionsMenuPopup == null) return;
            _actionsMenuOpen = !_actionsMenuOpen;
            ActionsMenuPopup.IsOpen = _actionsMenuOpen;
            e.Handled = true;
        }

        private void MenuActionClicked(object sender, RoutedEventArgs e)
        {
            _actionsMenuOpen = false;
            if (ActionsMenuPopup != null)
                ActionsMenuPopup.IsOpen = false;
        }

        private void Root_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_actionsMenuOpen) return;

            var source = e.OriginalSource as DependencyObject;
            bool insideButton = source != null && UiTreeHelper.IsDescendantOf(source, ActionsMenuButton);
            bool insidePopup = source != null && ActionsMenuPopup.Child != null
                               && UiTreeHelper.IsDescendantOf(source, ActionsMenuPopup.Child);

            if (!insideButton && !insidePopup)
            {
                _actionsMenuOpen = false;
                ActionsMenuPopup.IsOpen = false;
            }
        }
    }
}
