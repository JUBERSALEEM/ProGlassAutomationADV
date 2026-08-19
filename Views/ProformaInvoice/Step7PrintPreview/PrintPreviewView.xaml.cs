using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation.Views.ProformaInvoice.Step7PrintPreview
{
    public partial class PrintPreviewView : UserControl
    {
        private bool _actionsMenuOpen;

        public PrintPreviewView()
        {
            InitializeComponent();
        }

        // Same contract as Sheet Store: left click toggles, outside click closes.
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
            bool insideButton = source != null && IsDescendantOf(source, ActionsMenuButton);
            bool insidePopup = source != null && ActionsMenuPopup.Child != null
                               && IsDescendantOf(source, ActionsMenuPopup.Child);

            if (!insideButton && !insidePopup)
            {
                _actionsMenuOpen = false;
                ActionsMenuPopup.IsOpen = false;
            }
        }

        private static bool IsDescendantOf(DependencyObject element, DependencyObject root)
            => ProGlassAutomation.Utilities.UiTreeHelper.IsDescendantOf(element, root);
    }
}
