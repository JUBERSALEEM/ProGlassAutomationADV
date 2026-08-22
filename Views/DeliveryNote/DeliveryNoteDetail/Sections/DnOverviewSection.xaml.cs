using ProGlassAutomation.Animations;
using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ProGlassAutomation.Views.DeliveryNote.DeliveryNoteDetail.Sections
{
    /// <summary>
    /// Delivery Note overview — Sheet Store pattern for row selection.
    /// Clicking a glass row slides in the right stats panel with THAT row's
    /// quantities; clicking again (or the close button) collapses it and the
    /// top KPI strip returns to totals. The selected row is highlighted via
    /// the row's IsSelected DataTrigger.
    /// </summary>
    public partial class DnOverviewSection : UserControl
    {
        // Reserved width of the slide-in panel column (360 panel + 10 margin).
        private const double PanelOpenWidth = 370;

        public DnOverviewSection()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => SubscribeToVm();
        }

        private DeliveryNoteDetailViewModel? _vm;

        private void SubscribeToVm()
        {
            if (_vm != null)
                _vm.PropertyChanged -= OnVmPropertyChanged;

            _vm = DataContext as DeliveryNoteDetailViewModel;
            if (_vm != null)
                _vm.PropertyChanged += OnVmPropertyChanged;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DeliveryNoteDetailViewModel.IsRowSelected)) return;
            if (DetailPanel == null) return;

            if (_vm?.IsRowSelected == true)
            {
                DetailPanel.Visibility = Visibility.Visible;
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
            // Clicking into the editable Delivered-Qty TextBox types, not
            // toggles selection — let that cell handle its own input.
            if (e.OriginalSource is DependencyObject src &&
                (src is TextBox || FindAncestor<TextBox>(src) != null))
                return;

            if (sender is not Border row ||
                row.Tag is not DnItemRowViewModel item ||
                DataContext is not DeliveryNoteDetailViewModel vm)
                return;

            // Toggle: click the same row again to deselect.
            vm.SelectedItemRow = ReferenceEquals(vm.SelectedItemRow, item)
                ? null
                : item;
        }

        private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
        {
            while (start != null)
            {
                if (start is T match) return match;
                start = VisualTreeHelper.GetParent(start);
            }
            return null;
        }
    }
}
