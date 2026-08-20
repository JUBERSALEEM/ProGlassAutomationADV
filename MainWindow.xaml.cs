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

            _hoverTimer.Tick += HoverTimer_Tick;

            // X-6: refuse to close while a multi-step write is running.
            Closing += MainWindow_Closing;

            // Close any open top-bar popup when the window deactivates, so a
            // stray popup never lingers over another app (reference behaviour).
            Deactivated += (_, _) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.IsSettingsOpen = false;
                    vm.IsNotificationsOpen = false;
                    vm.IsUserSwitchOpen = false;
                    vm.IsScreenshotOpen = false;
                    vm.IsScreenRecordOpen = false;
                }
                StopCloseTimer();
            };

            // Close any open top-bar popup when the user clicks anywhere that
            // is NOT its trigger button AND NOT inside the popup's own content.
            // (Clicking inside an open popup must not dismiss it.) Keeps each
            // popup's StaysOpen reliable while letting a click on a module
            // dismiss it.
            PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (DataContext is not MainViewModel vm) return;

                var source = e.OriginalSource as DependencyObject;

                bool In(DependencyObject root) =>
                    source != null && IsDescendantOf(source, root);

                if (vm.IsUserSwitchOpen && !In(UserCardButton) && !In(UserSwitchPanelHost))
                    vm.IsUserSwitchOpen = false;
                if (vm.IsSettingsOpen && !In(SettingsButton) && !In(SettingsPanelHost))
                    vm.IsSettingsOpen = false;
                if (vm.IsNotificationsOpen && !In(NotificationButton) && !In(ActivityPanelHost))
                    vm.IsNotificationsOpen = false;
                if (vm.IsScreenshotOpen && !In(ScreenshotButton) && !In(ScreenshotPanelHost))
                    vm.IsScreenshotOpen = false;
                bool comboOpen = Mouse.DirectlyOver is System.Windows.Controls.ComboBoxItem
                    || Mouse.DirectlyOver is System.Windows.Controls.ComboBox;
                if (vm.IsScreenRecordOpen && !comboOpen && !In(RecordButton) && !In(ScreenRecordPanelHost))
                    vm.IsScreenRecordOpen = false;
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

        private static bool IsDescendantOf(DependencyObject element, DependencyObject root)
            => ProGlassAutomation.Utilities.UiTreeHelper.IsDescendantOf(element, root);

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
            else if (ReferenceEquals(sender, NotificationButton)) vm.IsNotificationsOpen = open;
            else if (ReferenceEquals(sender, UserCardButton)) vm.IsUserSwitchOpen = open;
            else if (ReferenceEquals(sender, ScreenshotButton)) vm.IsScreenshotOpen = open;
            else if (ReferenceEquals(sender, RecordButton)) vm.IsScreenRecordOpen = open;
        }

        private void OpenDropdown(object sender)
        {
            _isMouseOverPopup = false;
            StopCloseTimer();
            // Open ONLY this one; close the others (reference behaviour).
            if (DataContext is MainViewModel vm)
            {
                vm.IsSettingsOpen = ReferenceEquals(sender, SettingsButton);
                vm.IsNotificationsOpen = ReferenceEquals(sender, NotificationButton);
                vm.IsUserSwitchOpen = ReferenceEquals(sender, UserCardButton);
                vm.IsScreenshotOpen = ReferenceEquals(sender, ScreenshotButton);
                vm.IsScreenRecordOpen = ReferenceEquals(sender, RecordButton);
            }
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
                Interval = TimeSpan.FromMilliseconds(1200)   // longer so ComboBox quality/fps selection completes before close
            };
            _dropdownCloseTimer.Tick += (_, _) =>
            {
                _dropdownCloseTimer!.Stop();
                _dropdownCloseTimer = null;
                if (_isMouseOverPopup) return;   // pointer reached the popup
                // Close whichever is open.
                if (DataContext is MainViewModel vm)
                {
                    vm.IsSettingsOpen = false;
                    vm.IsNotificationsOpen = false;
                    vm.IsUserSwitchOpen = false;
                    vm.IsScreenshotOpen = false;
                    vm.IsScreenRecordOpen = false;
                }
            };
            _dropdownCloseTimer.Start();
        }

        // Hover a button: open ITS popup (and close the others).
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
                       || (ReferenceEquals(sender, NotificationButton) && vm.IsNotificationsOpen)
                       || (ReferenceEquals(sender, UserCardButton) && vm.IsUserSwitchOpen)
                       || (ReferenceEquals(sender, ScreenshotButton) && vm.IsScreenshotOpen)
                       || (ReferenceEquals(sender, RecordButton) && vm.IsScreenRecordOpen);

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
                    m2.IsNotificationsOpen = ReferenceEquals(sender, NotificationButton);
                    m2.IsUserSwitchOpen = ReferenceEquals(sender, UserCardButton);
                    m2.IsScreenshotOpen = ReferenceEquals(sender, ScreenshotButton);
                    m2.IsScreenRecordOpen = ReferenceEquals(sender, RecordButton);
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
            // Screen recording dropdown: don't close on mouse-leave because
            // ComboBox quality/fps dropdown creates a separate Popup window
            // that triggers MouseLeave incorrectly. Rely on click-outside
            // (PreviewMouseLeftButtonDown) for closing.
            if (ReferenceEquals(sender, ScreenRecordPanelHost))
            {
                _isMouseOverPopup = false;
                return;   // no timer — click-outside closes it
            }
            var source = Mouse.DirectlyOver as DependencyObject;
            bool overCombo = source != null && source is System.Windows.Controls.ComboBoxItem;
            if ((overCombo || (source != null && IsDescendantOf(source, ScreenRecordPanelHost))) && ScreenRecordPanelHost != null)
            {
                _isMouseOverPopup = true;
                StopCloseTimer();
                return;
            }
            _isMouseOverPopup = false;
            StartCloseTimer();
        }

        /// <summary>
        /// Clicking the app logo opens the FULL, stylish About module (the
        /// rich AboutView), not a small popup. Navigation goes through the
        /// same switch the sidebar/agent use so the module wallpaper and AI
        /// screen context stay correct.
        /// </summary>
        private void Logo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Close every top-bar popup, then open the full About module.
                vm.IsSettingsOpen = false;
                vm.IsNotificationsOpen = false;
                vm.IsScreenshotOpen = false;
                vm.IsUserSwitchOpen = false;
                StopCloseTimer();
                vm.NavigateFromAgent("About");
            }
        }

        // ── sidebar groups: CLICK + MOUSE ON/OFF (integrated) ────────────
        // Both behaviors work together:
        //   • Mouse ON a group header (hover, 300ms)  -> opens + closes others
        //   • Mouse OFF the group (header + submenu)  -> closes it
        //   • Click a closed group                    -> opens it AND PINS it
        //     (stays open even after the mouse leaves)
        //   • Click the open/pinned group             -> closes it + unpins
        //
        // The header is a ToggleButton whose IsChecked drives the sub-menu
        // Visibility. NOTE: ToggleButton.OnClick() calls OnToggle() BEFORE
        // raising the Click event — so by the time this handler runs the
        // IsChecked has ALREADY flipped. We must NOT re-set it (that would
        // cancel the toggle) — only enforce accordion + pin semantics.
        private StackPanel? _pinnedGroupHost;
        private readonly DispatcherTimer _hoverTimer =
            new() { Interval = TimeSpan.FromMilliseconds(300) };
        private StackPanel? _hoverPending;

        private void SidebarGroup_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not StackPanel host || host.Children.Count == 0) return;
            if (host.Children[0] is not ToggleButton) return;

            // Delay before hover-open so the cursor passing through a
            // header while moving to a lower group does not jump menus.
            _hoverPending = host;
            _hoverTimer.Stop();
            _hoverTimer.Start();
        }

        private void SidebarGroup_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not StackPanel host) return;

            if (ReferenceEquals(_hoverPending, host))
            {
                _hoverPending = null;
                _hoverTimer.Stop();
            }

            // Hover-close unless the group is pinned open by a click.
            if (!ReferenceEquals(_pinnedGroupHost, host)
                && host.Children.Count > 0
                && host.Children[0] is ToggleButton tb)
            {
                tb.IsChecked = false;
            }
        }

        private void HoverTimer_Tick(object? sender, EventArgs e)
        {
            _hoverTimer.Stop();
            var host = _hoverPending;
            _hoverPending = null;
            if (host == null || host.Children.Count == 0) return;
            if (host.Children[0] is not ToggleButton tb) return;

            tb.IsChecked = true;
            CloseOtherSidebarGroups(host);

            // Hover never pins — and it clears any previous click-pin that
            // the hover accordion just closed, so the newly hovered group
            // closes normally on mouse-leave instead of staying open.
            _pinnedGroupHost = null;
        }

        private void SidebarGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton tb) return;
            if (tb.Parent is not StackPanel host) return;

            // IsChecked was already toggled by the control — read the new state.
            if (tb.IsChecked == true)
            {
                // Opened: pin it (survives mouse-leave) and close the others.
                _pinnedGroupHost = host;
                CloseOtherSidebarGroups(host);
            }
            else
            {
                // Closed by click: unpin.
                if (ReferenceEquals(_pinnedGroupHost, host))
                    _pinnedGroupHost = null;
            }
        }

        /// <summary>Closes every sidebar group host except <paramref name="keep"/>.</summary>
        private void CloseOtherSidebarGroups(StackPanel keep)
        {
            var all = new[]
            {
                FindName("SalesGroupHost") as StackPanel,
                FindName("ProductionGroupHost") as StackPanel,
                FindName("DispatchGroupHost") as StackPanel,
                FindName("ReportsGroupHost") as StackPanel,
                FindName("MasterDataGroupHost") as StackPanel,
                FindName("SystemGroupHost") as StackPanel
            };
            foreach (var host in all)
            {
                if (host == null || ReferenceEquals(host, keep)) continue;
                if (host.Children.Count > 0 && host.Children[0] is ToggleButton tb)
                    tb.IsChecked = false;
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

        private void SettingsPopup_Opened(object sender, System.EventArgs e)
        {
            // Nothing to preload — quick-settings rows navigate on demand.
        }
        private void SettingsPopup_Closed(object sender, System.EventArgs e) { }

        private void NotificationsPopup_Opened(object sender, System.EventArgs e)
        {
            if (ActivityPanelHost != null)
                _ = ActivityPanelHost.LoadAsync();
        }
        private void NotificationsPopup_Closed(object sender, System.EventArgs e) { }

        private void ScreenshotPopup_Opened(object sender, System.EventArgs e) { }
        private void ScreenshotPopup_Closed(object sender, System.EventArgs e) { }

        private void ScreenRecordPopup_Opened(object sender, System.EventArgs e) { }
        private void ScreenRecordPopup_Closed(object sender, System.EventArgs e) { }

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