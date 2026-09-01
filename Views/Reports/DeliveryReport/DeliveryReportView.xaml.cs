using ProGlassAutomation.Animations;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.DeliveryReport;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace ProGlassAutomation.Views.Reports.DeliveryReport
{
    /// <summary>
    /// Delivery report — Sheet Store pattern. Clicking a row (MouseLeftButtonDown
    /// on the row Border) slides in the right detail panel with that row's
    /// quantities + delivery history; clicking again collapses it.
    /// </summary>
    public partial class DeliveryReportView : UserControl
    {
        private const double PanelOpenWidth = 370;

        public DeliveryReportView()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => SubscribeToVm();
        }

        private DeliveryReportViewModel? _vm;

        private void SubscribeToVm()
        {
            if (_vm != null)
                _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = DataContext as DeliveryReportViewModel;
            if (_vm != null)
                _vm.PropertyChanged += OnVmPropertyChanged;
        }

        /// <summary>
        /// Sheet Store pattern — click a row to open the right panel; click
        /// the same row again to close it. Uses the Border's Tag (not the
        /// ListBox SelectedItem, whose ItemContainerStyle disables focus).
        /// </summary>
        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DeliveryReportViewModel.HasSelectedRow)) return;
            if (DetailPanel == null) return;

            if (_vm?.HasSelectedRow == true)
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
                    if (_vm?.HasSelectedRow != true)
                        DetailPanel.Visibility = Visibility.Collapsed;
                };
                DetailColumn.BeginAnimation(ColumnDefinition.WidthProperty, close);
            }
        }
    }
}
