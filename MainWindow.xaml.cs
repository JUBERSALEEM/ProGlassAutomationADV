using ProGlassAutomation.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Clicking the dim overlay closes any open drawer
        private void DimOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsMenuOpen = false;
                vm.IsSettingsOpen = false;
            }
        }
    }
}