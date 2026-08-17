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

            // X-6: refuse to close while a multi-step write is running.
            Closing += MainWindow_Closing;
        }

        // ══════════════════════════════════════════════════════════════
        //  SHUTDOWN SAFETY — audit finding X-6
        //
        //  There was no Closing handler at all. Clicking X during a save,
        //  export or backup killed the process mid-write.
        //
        //  Each SQLite file is individually safe — a killed transaction
        //  rolls back. The real exposure is a MULTI-DATABASE business
        //  operation: creating a tax invoice writes taxinvoices.db and
        //  then updates deliverynotes.db, and no transaction can span two
        //  database files (audit finding D-4). Dying between those two
        //  writes leaves a delivery note flagged as invoiced with no
        //  invoice behind it.
        //
        //  This asks the user rather than deciding for them: their data
        //  is their business, and silently blocking a close is as rude as
        //  silently corrupting it. Cancel is the DEFAULT so an accidental
        //  Enter keypress keeps the data safe.
        // ══════════════════════════════════════════════════════════════
        private void MainWindow_Closing(
            object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (!Services.Safety.CriticalOperationTracker.IsBusy)
                    return;

                var running = string.Join(
                    "\n   • ",
                    Services.Safety.CriticalOperationTracker.ActiveOperations);

                var answer = MessageBox.Show(
                    "ProGlass is still finishing work that must not be " +
                    "interrupted:\n\n   • " + running + "\n\n" +
                    "Closing now could leave a document half-written.\n\n" +
                    "Close anyway?",
                    "Operation In Progress",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);   // safe default

                if (answer != MessageBoxResult.Yes)
                {
                    e.Cancel = true;

                    Services.Logging.AppLogService.Info(
                        "Close cancelled — critical operation in progress.",
                        new Services.Logging.LogContext
                        {
                            Module = "Safety",
                            Operation = "Shutdown"
                        });
                }
                else
                {
                    Services.Logging.AppLogService.Warning(
                        "User forced close during: " + running,
                        new Services.Logging.LogContext
                        {
                            Module = "Safety",
                            Operation = "Shutdown"
                        });
                }
            }
            catch
            {
                // A fault in the shutdown guard must never trap the user
                // inside the application. Allow the close.
            }
        }

        // ── top-bar user card → non-blocking user-switch popup ──────────
        // Single click opens the popup; double-click closes it. The popup
        // is non-modal (StaysOpen=False), so it never blocks the module
        // content behind it.

        private void UserCardButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.IsUserSwitchOpen = !vm.IsUserSwitchOpen;
        }

        private void UserCardButton_PreviewMouseLeftButtonDown(
            object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2 && DataContext is MainViewModel vm)
            {
                vm.IsUserSwitchOpen = false;
                e.Handled = true;
            }
        }

        private void UserSwitchPopup_Opened(object sender, System.EventArgs e)
        {
            if (UserSwitchPanelHost != null)
                UserSwitchPanelHost.RefreshAsync();
        }

        private void UserSwitchPopup_Closed(object sender, System.EventArgs e)
        {
            // If the user switched identities, refresh the top-bar pill.
            if (DataContext is MainViewModel vm)
                vm.RefreshCurrentUserPublic();
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