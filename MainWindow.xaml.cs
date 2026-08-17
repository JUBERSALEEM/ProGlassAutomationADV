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
using System.Windows.Threading;

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
            // that is not its trigger button. Keeps each popup's StaysOpen
            // reliable while letting a click on a module dismiss it.
            PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (DataContext is not MainViewModel vm) return;

                var source = e.OriginalSource as DependencyObject;

                bool In(DependencyObject button) =>
                    source != null && IsDescendantOf(source, button);

                if (vm.IsUserSwitchOpen && !In(UserCardButton))
                    vm.IsUserSwitchOpen = false;
                if (vm.IsSettingsOpen && !In(SettingsButton))
                    vm.IsSettingsOpen = false;
                if (vm.IsAboutOpen && !In(AboutButton))
                    vm.IsAboutOpen = false;
                if (vm.IsNotificationsOpen && !In(NotificationButton))
                    vm.IsNotificationsOpen = false;
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
        // Uses the shared DropdownButton_Click handler (hover-peek +
        // click-toggle) like the other top-bar popups.

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

        // ── top-bar dropdowns (reference pattern) ─────────────────────────
        // One shared close-timer + a single "pointer over popup" flag.
        //  - Hover a button opens its popup.
        //  - Moving away starts a short close-timer.
        //  - Moving INTO the popup cancels it (so it stays open).
        //  - Clicking toggles: same button closes, another opens.

        private DispatcherTimer? _dropdownCloseTimer;
        private bool _isMouseOverPopup;

        // Maps a trigger button to its popup-open VM property.
        private void SetOpen(object sender, bool open)
        {
            if (DataContext is not MainViewModel vm) return;
            if (ReferenceEquals(sender, SettingsButton)) vm.IsSettingsOpen = open;
            else if (ReferenceEquals(sender, AboutButton)) vm.IsAboutOpen = open;
            else if (ReferenceEquals(sender, NotificationButton)) vm.IsNotificationsOpen = open;
            else if (ReferenceEquals(sender, UserCardButton)) vm.IsUserSwitchOpen = open;
        }

        private void OpenDropdown(object sender)
        {
            _isMouseOverPopup = false;
            StopCloseTimer();
            SetOpen(sender, true);
        }

        private void CloseDropdown(object sender)
        {
            StopCloseTimer();
            SetOpen(sender, false);
        }

        private void StopCloseTimer()
        {
            if (_dropdownCloseTimer == null) return;
            _dropdownCloseTimer.Stop();
            _dropdownCloseTimer = null;
        }

        private void StartCloseTimer()
        {
            StopCloseTimer();
            _dropdownCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _dropdownCloseTimer.Tick += (_, _) =>
            {
                if (_isMouseOverPopup) return;   // pointer reached the popup
                // Close whichever is open.
                if (DataContext is MainViewModel vm)
                {
                    vm.IsSettingsOpen = false;
                    vm.IsAboutOpen = false;
                    vm.IsNotificationsOpen = false;
                    vm.IsUserSwitchOpen = false;
                }
                _dropdownCloseTimer!.Stop();
                _dropdownCloseTimer = null;
            };
            _dropdownCloseTimer.Start();
        }

        // Hover a button: open its popup.
        private void TopBarButton_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseOverPopup = false;
            StopCloseTimer();
            OpenDropdown(sender);
        }

        // Leave a button: schedule a close (cancelled if we enter the popup).
        private void TopBarButton_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseOverPopup = false;
            StartCloseTimer();
        }

        // Click toggles: same button open -> close; otherwise open.
        private void DropdownButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            bool isOpen = (ReferenceEquals(sender, SettingsButton) && vm.IsSettingsOpen)
                       || (ReferenceEquals(sender, AboutButton) && vm.IsAboutOpen)
                       || (ReferenceEquals(sender, NotificationButton) && vm.IsNotificationsOpen)
                       || (ReferenceEquals(sender, UserCardButton) && vm.IsUserSwitchOpen);

            if (isOpen)
            {
                CloseDropdown(sender);
            }
            else
            {
                // Close the others, open this one.
                if (DataContext is MainViewModel m2)
                {
                    m2.IsSettingsOpen = ReferenceEquals(sender, SettingsButton);
                    m2.IsAboutOpen = ReferenceEquals(sender, AboutButton);
                    m2.IsNotificationsOpen = ReferenceEquals(sender, NotificationButton);
                    m2.IsUserSwitchOpen = ReferenceEquals(sender, UserCardButton);
                }
                StopCloseTimer();
            }
        }

        // Entering an open popup cancels the close.
        private void PopupContent_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseOverPopup = true;
            StopCloseTimer();
        }

        // Leaving the popup schedules a close.
        private void PopupContent_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseOverPopup = false;
            StartCloseTimer();
        }

        // ── sidebar groups: hover-open + click-pin (reference-friendly) ───
        // Hover opens a group. Moving away closes it after a short delay so
        // the pointer can travel into the sub-menu. Clicking the header pins
        // it open so sub-items stay reachable; a second click closes it.

        private readonly System.Collections.Generic.HashSet<object> _sidebarPinned = new();

        private void SidebarGroup_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is StackPanel sp && sp.Children.Count > 0
                && sp.Children[0] is ToggleButton tb)
            {
                tb.IsChecked = true;
            }
        }

        private void SidebarGroup_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not StackPanel host || host.Children.Count == 0) return;
            var tb = host.Children[0] as ToggleButton;
            if (tb == null) return;
            if (_sidebarPinned.Contains(host)) return;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (_sidebarPinned.Contains(host)) return;
                tb.IsChecked = false;
            };
            timer.Start();
        }

        // Pin/unpin on click so the hover-close cannot collapse a chosen group.
        private void SidebarGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton tb) return;
            if (tb.Parent is not StackPanel host) return;
            if (tb.IsChecked == true) _sidebarPinned.Add(host);
            else _sidebarPinned.Remove(host);
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