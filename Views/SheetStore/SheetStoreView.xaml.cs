using ProGlassAutomation.Animations;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.ViewModels.SheetStore;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ProGlassAutomation.Views.SheetStore
{
    public partial class SheetStoreView : UserControl
    {
        private Border? _lastSelectedRow;

        // Reserved width of the slide-in panel column (360 panel + 16 margin).
        private const double PanelOpenWidth = 376;

        public SheetStoreView()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => SubscribeToVm();
        }

        private SheetStoreViewModel? _vm;

        private void SubscribeToVm()
        {
            if (_vm != null)
                _vm.PropertyChanged -= OnVmPropertyChanged;

            _vm = DataContext as SheetStoreViewModel;
            if (_vm != null)
                _vm.PropertyChanged += OnVmPropertyChanged;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SheetStoreViewModel.IsRowSelected)) return;
            if (DetailPanel == null) return;

            if (_vm?.IsRowSelected == true)
            {
                DetailPanel.Visibility = Visibility.Visible;
                // Smoothly grow the panel column so the list reflows live —
                // no translate/timer jump, and the close is not laggy.
                DetailColumn.BeginAnimation(
                    ColumnDefinition.WidthProperty,
                    new GridLengthAnimation
                    {
                        From = DetailColumn.Width,
                        To = new GridLength(PanelOpenWidth),
                        Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    });
            }
            else
            {
                var close = new GridLengthAnimation
                {
                    From = DetailColumn.Width,
                    To = new GridLength(0),
                    Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                close.Completed += (_, _) =>
                {
                    if (_vm?.IsRowSelected != true)
                        DetailPanel.Visibility = Visibility.Collapsed;
                };
                DetailColumn.BeginAnimation(ColumnDefinition.WidthProperty, close);
            }
        }

        private void Row_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border row &&
                row.Tag is GlassSheet sheet &&
                DataContext is SheetStoreViewModel vm)
            {
                // Reset previous row visual
                if (_lastSelectedRow != null)
                {
                    _lastSelectedRow.BorderBrush = Brushes.Transparent;
                    _lastSelectedRow.Background = Brushes.Transparent;
                }

                // Toggle: click same row to deselect
                if (vm.SelectedSheet == sheet)
                {
                    vm.SelectedSheet = null;
                    _lastSelectedRow = null;
                }
                else
                {
                    vm.SelectedSheet = sheet;

                    // Highlight the row
                    row.Background = (System.Windows.Media.Brush)FindResource("LiquidSelectionBrush");
                    if (Application.Current.Resources["AccentBrush"] is SolidColorBrush accent)
                        row.BorderBrush = accent;

                    _lastSelectedRow = row;
                }
            }
        }

        private bool _actionsMenuOpen;

        // ACTIONS menu: single LEFT click opens, second LEFT click closes.
        private void ActionsMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (ActionsMenuPopup == null) return;
            _actionsMenuOpen = !_actionsMenuOpen;
            ActionsMenuPopup.IsOpen = _actionsMenuOpen;
            e.Handled = true;
        }

        private void MenuActionClicked(object sender, RoutedEventArgs e)
        {
            // Run the bound command (the Click fires alongside the Command),
            // then close the dropdown.
            _actionsMenuOpen = false;
            if (ActionsMenuPopup != null)
                ActionsMenuPopup.IsOpen = false;
        }

        // Close the dropdown when the user clicks anywhere that is not the
        // ACTIONS button and not inside the popup.
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
