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
                    row.Background = new SolidColorBrush(Color.FromRgb(224, 231, 255)); // Light blue
                    if (Application.Current.Resources["AccentBrush"] is SolidColorBrush accent)
                        row.BorderBrush = accent;

                    _lastSelectedRow = row;
                }
            }
        }

        private void ActionsMenuButton_Click(object sender, RoutedEventArgs e)
        {
            // The ContextMenu lives in resources (a ContextMenu cannot be a
            // visual child). Toggle: single click opens, second click closes.
            if (Resources["ActionsMenu"] is not ContextMenu menu) return;

            if (menu.IsOpen)
            {
                menu.IsOpen = false;
            }
            else
            {
                // A ContextMenu in resources is not in the visual tree, so it
                // does not inherit the UserControl's DataContext. Wire it
                // explicitly so the MenuItem Command bindings resolve.
                menu.DataContext = DataContext;
                menu.PlacementTarget = ActionsMenuButton;
                menu.IsOpen = true;
            }
            e.Handled = true;
        }
    }

    /// <summary>
    /// Animates a ColumnDefinition.Width between two GridLength values.
    /// WPF has no built-in GridLength animation; DoubleAnimation cannot
    /// animate GridLength, so this lightweight AnimationTimeline is used for
    /// the sheet-store slide panel (smooth open/close of the column width).
    /// </summary>
    internal sealed class GridLengthAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(GridLength);

        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register(nameof(From), typeof(GridLength), typeof(GridLengthAnimation));

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register(nameof(To), typeof(GridLength), typeof(GridLengthAnimation));

        public GridLength From
        {
            get => (GridLength)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public GridLength To
        {
            get => (GridLength)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public static readonly DependencyProperty EasingFunctionProperty =
            DependencyProperty.Register(
                nameof(EasingFunction),
                typeof(IEasingFunction),
                typeof(GridLengthAnimation));

        public IEasingFunction EasingFunction
        {
            get => (IEasingFunction)GetValue(EasingFunctionProperty);
            set => SetValue(EasingFunctionProperty, value);
        }

        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

        public override object GetCurrentValue(
            object defaultOriginValue,
            object defaultDestinationValue,
            AnimationClock animationClock)
        {
            if (animationClock.CurrentProgress == null) return From;

            double progress = animationClock.CurrentProgress.Value;
            if (EasingFunction != null)
                progress = EasingFunction.Ease(progress);

            double from = From.Value;
            double to = To.Value;
            return new GridLength(from + (to - from) * progress, GridUnitType.Pixel);
        }
    }
}
