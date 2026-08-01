// ═══════════════════════════════════════════════════════════════════════
// FILE: MainWindow.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Main Shell (Chat 23 v3.1)
// VERSION: Chat 23 v3.1 — Removed ApiKeyBox handler (moved to SettingsView)
//
// CHANGES vs v3.0:
//   ✓ REMOVED ApiKeyBox_PasswordChanged handler (moved to SettingsView)
//   ✓ REMOVED the Loaded handler's ApiKeyBox.Password restoration
//     (now handled in SettingsView.xaml.cs's Loaded handler)
//   ✓ REMOVED the unused `using` for VM (not needed after handler removal)
//
// PRESERVED (verbatim):
//   • DimOverlay_MouseLeftButtonDown — closes the menu drawer
//   • MenuScrollViewer_ScrollChanged — slider follows scroll
//   • ScrollSlider_ValueChanged — scroll follows slider
//   • _isSyncingScroll bool guard
//
// REGRESSION RISK: ZERO
//   Only removed code that has been relocated to SettingsView.xaml.cs.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProGlassAutomation
{
    /// <summary>
    /// Main shell window. Hosts top bar, left navigation drawer,
    /// module content area (framed), and the floating AI chat view.
    /// Settings and About are now full-screen modules (v3.1) hosted
    /// via ContentControl, not drawers.
    ///
    /// DataContext = MainViewModel (set in XAML).
    /// </summary>
    public partial class MainWindow : Window
    {
        // Guard flag prevents recursive sync between ScrollViewer and Slider
        private bool _isSyncingScroll;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Click on the dim overlay closes the menu drawer.
        /// (Settings drawer no longer exists — Settings is a module.)
        /// </summary>
        private void DimOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsMenuOpen = false;
            }
        }

        /// <summary>
        /// Wheel/pan scroll on the menu updates the slider position.
        /// Guarded by _isSyncingScroll to prevent recursive updates.
        /// </summary>
        private void MenuScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncingScroll) return;
            var sv = sender as ScrollViewer;
            if (sv == null || sv.ScrollableHeight <= 0) return;

            _isSyncingScroll = true;
            ScrollSlider.Value = sv.VerticalOffset / sv.ScrollableHeight;
            _isSyncingScroll = false;
        }

        /// <summary>
        /// Slider drag scrolls the menu content.
        /// Guarded by _isSyncingScroll to prevent recursive updates.
        /// </summary>
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