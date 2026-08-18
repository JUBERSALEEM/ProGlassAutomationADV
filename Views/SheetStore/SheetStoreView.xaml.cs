using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.ViewModels.SheetStore;
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
                PanelSlide.BeginAnimation(
                    TranslateTransform.XProperty,
                    new DoubleAnimation(400, 0, TimeSpan.FromMilliseconds(220))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    });
            }
            else
            {
                PanelSlide.BeginAnimation(
                    TranslateTransform.XProperty,
                    new DoubleAnimation(400, TimeSpan.FromMilliseconds(180)));
                // Delay collapse until the slide-out finishes.
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(200)
                };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    if (_vm?.IsRowSelected != true)
                        DetailPanel.Visibility = Visibility.Collapsed;
                };
                timer.Start();
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
    }
}