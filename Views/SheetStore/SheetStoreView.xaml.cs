using ProGlassAutomation.Animations;
using ProGlassAutomation.ViewModels.SheetStore;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace ProGlassAutomation.Views.SheetStore
{
    public partial class SheetStoreView : UserControl
    {
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
            if (e.PropertyName != nameof(SheetStoreViewModel.IsRowSelected) &&
                e.PropertyName != nameof(SheetStoreViewModel.IsItemEditorOpen))
                return;

            SyncRightColumn();
        }

        /// <summary>
        /// Drives the shared right-hand column. The consumable editor wins
        /// over the glass stats rail when both would want the space.
        /// </summary>
        private void SyncRightColumn()
        {
            if (DetailPanel == null || ItemEditorPanel == null) return;

            bool itemEditorOpen = _vm?.IsItemEditorOpen == true;
            bool statsOpen = _vm?.IsRowSelected == true && !itemEditorOpen;
            bool anyOpen = statsOpen || itemEditorOpen;

            ItemEditorPanel.Visibility = itemEditorOpen ? Visibility.Visible : Visibility.Collapsed;
            DetailPanel.Visibility = statsOpen ? Visibility.Visible : Visibility.Collapsed;

            if (anyOpen)
            {
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
                    if (_vm?.IsRowSelected != true && _vm?.IsItemEditorOpen != true)
                    {
                        DetailPanel.Visibility = Visibility.Collapsed;
                        ItemEditorPanel.Visibility = Visibility.Collapsed;
                    }
                };
                DetailColumn.BeginAnimation(ColumnDefinition.WidthProperty, close);
            }
        }

        // Close the dropdown when the user clicks anywhere that is not the
        // ACTIONS button and not inside the popup.
        }
}
