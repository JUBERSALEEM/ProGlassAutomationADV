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

        }
}
