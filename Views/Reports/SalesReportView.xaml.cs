using ProGlassAutomation.Animations;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Views.Reports
{
    public partial class SalesReportView : UserControl
    {
        public SalesReportView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ViewModels.Reports.SalesReportViewModel oldVm)
                oldVm.PropertyChanged -= OnVmPropertyChanged;

            if (e.NewValue is ViewModels.Reports.SalesReportViewModel newVm)
            {
                newVm.PropertyChanged += OnVmPropertyChanged;
                // Re-apply current state (e.g. rail left collapsed after nav)
                ApplyRailState(newVm.IsRailOpen, animate: false);
            }
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.Reports.SalesReportViewModel.IsRailOpen)
                && sender is ViewModels.Reports.SalesReportViewModel vm)
            {
                ApplyRailState(vm.IsRailOpen, animate: true);
            }
        }

        /// <summary>
        /// Animates the rail column 268 ↔ 0 so the open report uses the full
        /// display width (hamburger in the module header strip reopens it).
        /// Uses the shared GridLengthAnimation — WPF has no native animation
        /// for the GridLength-typed ColumnDefinition.Width.
        /// </summary>
        private void ApplyRailState(bool open, bool animate)
        {
            if (RailColumn == null) return;

            if (!animate)
            {
                RailColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
                RailColumn.Width = open ? new GridLength(268) : new GridLength(0);
                return;
            }

            RailColumn.BeginAnimation(ColumnDefinition.WidthProperty,
                new GridLengthAnimation
                {
                    From = RailColumn.Width,
                    To = open ? new GridLength(268) : new GridLength(0),
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                });
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string key)
            {
                if (DataContext is ViewModels.Reports.SalesReportViewModel vm)
                {
                    vm.ActiveKey = key;
                    // Sync statement legacy view
                    if (key == "Statement")
                        vm.ShowList = true;
                }
            }
        }
    }
}
