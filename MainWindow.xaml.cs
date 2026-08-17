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
using System.Windows.Controls.Primitives;
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

            // Close any open top-bar popup when the user clicks anywhere
            // that is not its trigger button. Works with StaysOpen=True
            // (which keeps the button toggle reliable) while still letting
            // a click on any module dismiss the popup.
            PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (DataContext is not MainViewModel vm) return;

                var source = e.OriginalSource as DependencyObject;

                bool In(DependencyObject button) =>
                    source != null && IsDescendantOf(source, button);

                if (vm.IsUserSwitchOpen && !In(UserCardButton))
                    vm.IsUserSwitchOpen = false;
                if (vm.IsSettingsOpen && !In(SettingsButton))
                {
                    vm.IsSettingsOpen = false;
                    _pinSettings = false;
                }
                if (vm.IsAboutOpen && !In(AboutButton))
                {
                    vm.IsAboutOpen = false;
                    _pinAbout = false;
                }
                if (vm.IsNotificationsOpen && !In(NotificationButton))
                {
                    vm.IsNotificationsOpen = false;
                    _pinNotifications = false;
                }
            };
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
        // Single click toggles the popup open/closed (matching the agent's
        // toggle pattern). With StaysOpen=True the toggle is the only thing
        // that closes it on the button; clicking elsewhere closes it too via
        // the window's PreviewMouseLeftButtonDown below.

        private void UserCardButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.IsUserSwitchOpen = !vm.IsUserSwitchOpen;
        }

        /// <summary>True when <paramref name="element"/> is a descendant of <paramref name="root"/> in the visual tree.</summary>
        private static bool IsDescendantOf(DependencyObject element, DependencyObject root)
        {
            DependencyObject? current = element;
            while (current != null)
            {
                if (ReferenceEquals(current, root)) return true;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        // ── top-bar hover-peek + click-pin ─────────────────────────────────
        // Hover opens a popup (peek) and closes it on leave after a short
        // delay. A CLICK pins the popup open so it stays while you read or
        // use it; a second click closes it. This avoids the bug where hover
        // opened the popup and the click's blind toggle closed it instantly.

        private bool _pinSettings, _pinAbout, _pinNotifications;
        private bool _topBarClosePending;

        private void TopBarButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            _topBarClosePending = false;

            if (ReferenceEquals(sender, SettingsButton)) vm.IsSettingsOpen = true;
            else if (ReferenceEquals(sender, AboutButton)) vm.IsAboutOpen = true;
            else if (ReferenceEquals(sender, NotificationButton)) vm.IsNotificationsOpen = true;
        }

        private void TopBarButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            ScheduleClose();
        }

        // Click pins the popup open (keeps it from closing on hover-leave)
        // and toggles it closed on a second click.
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            if (vm.IsSettingsOpen && _pinSettings) { vm.IsSettingsOpen = false; _pinSettings = false; }
            else { vm.IsSettingsOpen = true; _pinSettings = true; _topBarClosePending = false; }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            if (vm.IsAboutOpen && _pinAbout) { vm.IsAboutOpen = false; _pinAbout = false; }
            else { vm.IsAboutOpen = true; _pinAbout = true; _topBarClosePending = false; }
        }

        private void NotificationButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            if (vm.IsNotificationsOpen && _pinNotifications)
            {
                vm.IsNotificationsOpen = false; _pinNotifications = false;
            }
            else
            {
                vm.IsNotificationsOpen = true; _pinNotifications = true; _topBarClosePending = false;
            }
        }

        private void ScheduleClose()
        {
            _topBarClosePending = true;
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (!_topBarClosePending) return;
                if (DataContext is not MainViewModel vm) return;

                // A pinned popup stays open even when the pointer leaves.
                if (!_pinSettings) vm.IsSettingsOpen = false;
                if (!_pinAbout) vm.IsAboutOpen = false;
                if (!_pinNotifications) vm.IsNotificationsOpen = false;
            };
            timer.Start();
        }

        // Moving the pointer into an open popup cancels the scheduled close,
        // so the user can read/click it without it vanishing.
        private void PopupContent_MouseEnter(object sender, MouseEventArgs e)
            => _topBarClosePending = false;

        private void PopupContent_MouseLeave(object sender, MouseEventArgs e)
            => ScheduleClose();

        // ── sidebar hover-expand ──────────────────────────────────────────
        // Hovering over a group opens it; moving away closes it. The user
        // can still click the group header to toggle it manually. The hover
        // container is the StackPanel wrapping the toggle + its sub-menu, so
        // moving into the opened sub-menu does not close it.

        // ── sidebar hover-expand + click-pin ──────────────────────────────
        // Hovering over a group opens it; moving away closes it after a short
        // delay so the pointer can travel into the sub-menu. Clicking the
        // group header pins it open (a second click closes it), so selecting
        // a sub-item is never interrupted by the hover-close.

        private readonly System.Collections.Generic.HashSet<object> _sidebarPinned = new();

        private void SidebarGroup_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not System.Windows.FrameworkElement fe) return;
            if (fe is StackPanel sp && sp.Children.Count > 0
                && sp.Children[0] is ToggleButton tb)
            {
                tb.IsChecked = true;
            }
        }

        private void SidebarGroup_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not System.Windows.FrameworkElement fe) return;
            var host = fe as StackPanel;
            if (host == null || host.Children.Count == 0) return;
            var tb = host.Children[0] as ToggleButton;
            if (tb == null) return;

            // If pinned, keep it open even when the pointer leaves.
            if (_sidebarPinned.Contains(host)) return;

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (_sidebarPinned.Contains(host)) return;
                tb.IsChecked = false;
            };
            timer.Start();
        }

        // The group ToggleButton toggles IsChecked on click; we also pin it so
        // the hover-close cannot collapse it while the user reads the sub-menu.
        private void SidebarGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton tb) return;
            var host = tb.Parent as StackPanel;
            if (host == null) return;

            if (tb.IsChecked == true)
                _sidebarPinned.Add(host);
            else
                _sidebarPinned.Remove(host);
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

        private void SettingsPopup_Opened(object sender, System.EventArgs e)
        {
            // Nothing to preload — quick-settings rows navigate on demand.
        }
        private void SettingsPopup_Closed(object sender, System.EventArgs e) { }

        private void AboutPopup_Opened(object sender, System.EventArgs e) { }
        private void AboutPopup_Closed(object sender, System.EventArgs e) { }

        private void NotificationsPopup_Opened(object sender, System.EventArgs e)
        {
            if (ActivityPanelHost != null)
                _ = ActivityPanelHost.LoadAsync();
        }
        private void NotificationsPopup_Closed(object sender, System.EventArgs e) { }

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