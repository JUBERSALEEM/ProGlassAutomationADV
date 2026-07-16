using ProGlassAutomation.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation
{
    public partial class MainWindow : Window
    {
        private bool _isSyncingScroll;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void DimOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsMenuOpen = false;
                vm.IsSettingsOpen = false;
            }
        }

        // Scroll changes (mouse wheel etc.) -> update slider
        private void MenuScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncingScroll) return;
            var sv = sender as ScrollViewer;
            if (sv == null || sv.ScrollableHeight <= 0) return;

            _isSyncingScroll = true;
            ScrollSlider.Value = sv.VerticalOffset / sv.ScrollableHeight;
            _isSyncingScroll = false;
        }

        // Slider drag -> scroll menu
        private void ScrollSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingScroll) return;
            if (MenuScrollViewer == null || MenuScrollViewer.ScrollableHeight <= 0) return;

            _isSyncingScroll = true;
            MenuScrollViewer.ScrollToVerticalOffset(e.NewValue * MenuScrollViewer.ScrollableHeight);
            _isSyncingScroll = false;
        }
    }
}