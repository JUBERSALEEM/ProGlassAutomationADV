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
            // 2026-09-06: the EDITOR panel now shares the animated column
            // with the stats panel, so the column reacts to either one.
            if (e.PropertyName != nameof(SheetStoreViewModel.IsRowSelected) &&
                e.PropertyName != nameof(SheetStoreViewModel.IsEditorOpen))
                return;

            SyncRightColumn();
        }

        /// <summary>
        /// Drives the shared right-hand column.
        ///
        /// Exactly one of the two panels may occupy it, and the EDITOR
        /// wins: if the operator hits Edit while a row is selected, they
        /// asked for the form, not the statistics.
        /// </summary>
        private void SyncRightColumn()
        {
            if (DetailPanel == null || EditorPanel == null) return;

            bool editorOpen = _vm?.IsEditorOpen == true;
            bool statsOpen = _vm?.IsRowSelected == true && !editorOpen;
            bool anyOpen = editorOpen || statsOpen;

            EditorPanel.Visibility = editorOpen ? Visibility.Visible : Visibility.Collapsed;
            DetailPanel.Visibility = statsOpen ? Visibility.Visible : Visibility.Collapsed;

            if (anyOpen)
            {
                // Grow the column so the list reflows live rather than
                // being covered over.
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
                    // Re-test on completion: the operator can reopen a
                    // panel mid-animation, and collapsing unconditionally
                    // here would hide a panel that is now wanted.
                    if (_vm?.IsEditorOpen != true && _vm?.IsRowSelected != true)
                    {
                        DetailPanel.Visibility = Visibility.Collapsed;
                        EditorPanel.Visibility = Visibility.Collapsed;
                    }
                };
                DetailColumn.BeginAnimation(ColumnDefinition.WidthProperty, close);
            }
        }

        // Close the dropdown when the user clicks anywhere that is not the
        // ACTIONS button and not inside the popup.
        }
}
