// ═══════════════════════════════════════════════════════════════════════
// FILE: MainWindow.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Main Shell (Chat 24 v4.1 Bugfix)
// VERSION: Chat 24 v4.1 — Fixed Screen Recording ComboBox & Dropdown Close Bug
//
// FIXES:
//   ✓ IsDescendantOf now bridges WPF Popup & LogicalTree boundaries
//     (fixes ScreenRecord ComboBox items closing the parent popup on click)
//   ✓ Click-outside guard in PreviewMouseLeftButtonDown updated to check
//     Popup targets safely across nested controls
//   ✓ Hover timer state race condition fixed in PopupContent_MouseLeave
//     (ScreenRecord popup now relies on click-outside dismiss, not hover timer)
//   ✓ UiTreeHelper also patched centrally for other views
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ProGlassAutomation
{
    /// <summary>
    /// Main shell window code-behind.
    /// Hosts top bar, left navigation drawer, module content area,
    /// and popups (Screen Record, User Switch, Quick Settings, Notifications, Screenshots).
    /// </summary>
    public partial class MainWindow : Window
    {
        // Guard flag prevents recursive sync between ScrollViewer and Slider
        private bool _isSyncingScroll;

        // ── Floating widget drag state (AI Agent pill + Screen Record) ──
        private bool _widgetDragging;
        private bool _widgetDragMoved;
        private Point _widgetDragStart;
        private double _widgetDragStartLeft;
        private double _widgetDragStartTop;
        private FrameworkElement? _widgetDragTarget;
        private bool _widgetsPositioned;

        public MainWindow()
        {
            InitializeComponent();

            _hoverTimer.Tick += HoverTimer_Tick;

            // Position the draggable AI + screen-record widgets at their
            // original top-right spot once the shell is laid out.
            Loaded += MainWindow_Loaded;

            // Shutdown safety guard
            Closing += MainWindow_Closing;

            // Close top-bar popups when the main window deactivates (app loses focus)
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

            // Non-blocking popup click-outside dismiss handler
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

                // Screen Recording Popup: check if click originated inside button, host panel, or popup
                // IMPORTANT: ComboBox dropdown items live in a separate Popup visual tree.
                // IsDescendantOf now bridges Popup.PlacementTarget -> ComboBox -> PanelHost,
                // so clicking a ComboBox item is correctly recognized as inside.
                if (vm.IsScreenRecordOpen)
                {
                    bool insideRecordUI = In(RecordButton)
                                       || In(ScreenRecordPanelHost)
                                       || In(ScreenRecordPopup);

                    if (!insideRecordUI)
                    {
                        vm.IsScreenRecordOpen = false;
                    }
                }
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  SMART VISUAL & LOGICAL TREE TRAVERSER
        //  Bridges WPF Popup & ComboBox boundaries so clicks on dropdown
        //  items trace back to their parent host control.
        //
        //  WPF Popup creates a separate visual tree. VisualTreeHelper.GetParent
        //  returns null at the root of that tree. To bridge:
        //    - Try Visual parent
        //    - Try Logical parent
        //    - Try FrameworkElement.Parent / TemplatedParent
        //    - Try FrameworkContentElement.Parent
        //    - If current is Popup, follow PlacementTarget
        //  This allows ComboBoxItem (inside inner Popup) -> inner Popup ->
        //  ComboBox (PlacementTarget) -> StackPanel -> ScreenRecordPanelHost
        //  to be detected as descendant.
        // ══════════════════════════════════════════════════════════════
        private static bool IsDescendantOf(DependencyObject? element, DependencyObject? root)
        {
            if (element == null || root == null) return false;

            var current = element;
            while (current != null)
            {
                if (ReferenceEquals(current, root)) return true;

                DependencyObject? parent = null;

                // 1. Visual Tree Parent (most common)
                try { parent = VisualTreeHelper.GetParent(current); }
                catch { parent = null; }

                // 2. Logical Tree Parent (for non-Visuals like ColumnDefinition)
                if (parent == null)
                {
                    try { parent = LogicalTreeHelper.GetParent(current); }
                    catch { parent = null; }
                }

                // 3. FrameworkElement fallbacks
                if (parent == null && current is FrameworkElement fe)
                {
                    parent = fe.Parent ?? fe.TemplatedParent as DependencyObject;
                }

                // 4. FrameworkContentElement fallback
                if (parent == null && current is FrameworkContentElement fce)
                {
                    parent = fce.Parent ?? fce.TemplatedParent as DependencyObject;
                }

                // 5. Popup boundary — follow PlacementTarget to bridge separate visual trees
                //    This is the critical fix for ComboBox dropdowns inside ScreenRecord popup.
                if (parent == null && current is Popup popup)
                {
                    parent = popup.PlacementTarget;
                }

                current = parent;
            }

            return false;
        }

        // ══════════════════════════════════════════════════════════════
        //  SHUTDOWN SAFETY
        // ══════════════════════════════════════════════════════════════
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
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
                    MessageBoxResult.No);

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
                // Allow close on error
            }
        }

        // ── top-bar dropdowns ─────────────────────────────────────────────
        private DispatcherTimer? _dropdownCloseTimer;
        private bool _isMouseOverPopup;

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

        // Phase 3: Thread-affinity guard — uses CreateDispatcherTimerWithHandler
        private void StartCloseTimer()
        {
            StopCloseTimer();
            _dropdownCloseTimer = AsyncUtility.CreateDispatcherTimerWithHandler(
                TimeSpan.FromMilliseconds(1200),
                (_, _) =>
                {
                    _dropdownCloseTimer!.Stop();
                    _dropdownCloseTimer = null;

                    if (_isMouseOverPopup) return;

                    if (DataContext is MainViewModel vm)
                    {
                        vm.IsSettingsOpen = false;
                        vm.IsNotificationsOpen = false;
                        vm.IsUserSwitchOpen = false;
                        vm.IsScreenshotOpen = false;
                        vm.IsScreenRecordOpen = false;
                    }
                },
                "MainWindow DropdownClose");
            _dropdownCloseTimer.Start();
        }

        // Hover a button: open its popup
        private void TopBarButton_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseOverPopup = false;
            StopCloseTimer();
            OpenDropdown(sender);
        }

        // Leave a button: schedule close
        private void TopBarButton_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseOverPopup = false;
            StartCloseTimer();
        }

        // Click toggles popup
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

        // Entering popup cancels close timer
        private void PopupContent_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseOverPopup = true;
            StopCloseTimer();
        }

        // Leaving popup — special handling for ScreenRecord to avoid closing
        // when interacting with ComboBox dropdowns (which live in separate Popup)
        private void PopupContent_MouseLeave(object sender, MouseEventArgs e)
        {
            // Screen recording dropdown: rely on click-outside dismiss
            // to avoid closing when interacting with ComboBox dropdowns
            if (ReferenceEquals(sender, ScreenRecordPanelHost))
            {
                _isMouseOverPopup = false;
                StopCloseTimer();
                return;
            }

            var source = Mouse.DirectlyOver as DependencyObject;
            if (source != null && IsDescendantOf(source, ScreenRecordPanelHost))
            {
                _isMouseOverPopup = true;
                StopCloseTimer();
                return;
            }

            _isMouseOverPopup = false;
            StartCloseTimer();
        }

        private void Logo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsSettingsOpen = false;
                vm.IsNotificationsOpen = false;
                vm.IsScreenshotOpen = false;
                vm.IsUserSwitchOpen = false;
                vm.IsScreenRecordOpen = false;
                StopCloseTimer();
                vm.NavigateFromAgent("About");
            }
        }

        // ── sidebar groups ────────────────────────────────────────────────
        private StackPanel? _pinnedGroupHost;
        private readonly DispatcherTimer _hoverTimer =
            new() { Interval = TimeSpan.FromMilliseconds(300) };
        private StackPanel? _hoverPending;

        private void SidebarGroup_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not StackPanel host || host.Children.Count == 0) return;
            if (host.Children[0] is not ToggleButton) return;

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
            _pinnedGroupHost = null;
        }

        private void SidebarGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton tb) return;
            if (tb.Parent is not StackPanel host) return;

            if (tb.IsChecked == true)
            {
                _pinnedGroupHost = host;
                CloseOtherSidebarGroups(host);
            }
            else
            {
                if (ReferenceEquals(_pinnedGroupHost, host))
                    _pinnedGroupHost = null;
            }
        }

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

        // Phase 3: No async void — use central SafeFireAndForget
        private void UserSwitchPopup_Opened(object sender, System.EventArgs e)
        {
            if (UserSwitchPanelHost != null)
                AsyncUtility.SafeFireAndForget(() => UserSwitchPanelHost.RefreshAsync(), "MainWindow UserSwitch Refresh");
        }

        private void UserSwitchPopup_Closed(object sender, System.EventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.RefreshCurrentUserPublic();
        }

        private void SettingsPopup_Opened(object sender, System.EventArgs e) { }
        private void SettingsPopup_Closed(object sender, System.EventArgs e) { }

        private void NotificationsPopup_Opened(object sender, System.EventArgs e)
        {
            if (ActivityPanelHost != null)
                AsyncUtility.SafeFireAndForget(() => ActivityPanelHost.LoadAsync(), "MainWindow Activity Load");
        }
        private void NotificationsPopup_Closed(object sender, System.EventArgs e) { }

        private void ScreenshotPopup_Opened(object sender, System.EventArgs e) { }
        private void ScreenshotPopup_Closed(object sender, System.EventArgs e) { }

        private void ScreenRecordPopup_Opened(object sender, System.EventArgs e) { }
        private void ScreenRecordPopup_Closed(object sender, System.EventArgs e) { }

        private void DimOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsMenuOpen = false;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  FLOATING WIDGETS — drag to move, click (no drag) to act
        //
        //  The AI Agent pill and the Screen Record button live on a
        //  full-window Canvas (FloatingWidgetLayer) so they can be dragged
        //  out of the way instead of covering content behind them.
        //
        //  Click vs drag: on press we record the start point and capture
        //  the mouse; if the pointer moves beyond a small threshold it is
        //  a drag (the widget follows); otherwise the release is a click
        //  and the widget's normal action runs (open agent / toggle
        //  screen recording).
        // ══════════════════════════════════════════════════════════════

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Anchor the widgets once, at their original top-right spot
            // (just left of the fixed user card). After that the user fully
            // controls their position for the session — never re-anchor.
            if (_widgetsPositioned) return;
            if (FloatingWidgetLayer == null || AiAgentPill == null || RecordButton == null)
                return;

            try
            {
                double recordLeft;
                if (UserCardButton != null)
                {
                    // Place the record button just left of the user card.
                    var userOrigin = UserCardButton.TranslatePoint(new Point(0, 0), FloatingWidgetLayer);
                    recordLeft = userOrigin.X - RecordButton.ActualWidth - 12;
                }
                else
                {
                    double width = FloatingWidgetLayer.ActualWidth;
                    double recordW = RecordButton.ActualWidth > 0 ? RecordButton.ActualWidth : 36;
                    recordLeft = Math.Max(0, width - recordW - 130);
                }

                Canvas.SetLeft(RecordButton, Math.Max(0, recordLeft));
                Canvas.SetTop(RecordButton, 6);

                double aiW = AiAgentPill.ActualWidth > 0 ? AiAgentPill.ActualWidth : 120;
                Canvas.SetLeft(AiAgentPill, Math.Max(0, recordLeft - aiW - 10));
                Canvas.SetTop(AiAgentPill, 6);

                _widgetsPositioned = true;
            }
            catch
            {
                // Positioning is cosmetic — never break the shell.
            }
        }

        private void FloatingWidget_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement el || FloatingWidgetLayer == null) return;

            _widgetDragTarget = el;
            _widgetDragging = true;
            _widgetDragMoved = false;

            _widgetDragStart = e.GetPosition(FloatingWidgetLayer);
            _widgetDragStartLeft = Canvas.GetLeft(el);
            _widgetDragStartTop = Canvas.GetTop(el);
            if (double.IsNaN(_widgetDragStartLeft)) _widgetDragStartLeft = 0;
            if (double.IsNaN(_widgetDragStartTop)) _widgetDragStartTop = 0;

            // Bring the grabbed widget to the front of the floating layer.
            Panel.SetZIndex(el, 300);

            try { el.CaptureMouse(); } catch { /* capture is best-effort */ }

            // Own the press so the Button / pill click machinery never fires
            // while we decide between click and drag.
            e.Handled = true;
        }

        private void FloatingWidget_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_widgetDragging || _widgetDragTarget == null || FloatingWidgetLayer == null) return;

            var pos = e.GetPosition(FloatingWidgetLayer);
            double dx = pos.X - _widgetDragStart.X;
            double dy = pos.Y - _widgetDragStart.Y;

            const double threshold = 3;
            if (!_widgetDragMoved && (Math.Abs(dx) > threshold || Math.Abs(dy) > threshold))
                _widgetDragMoved = true;

            if (_widgetDragMoved)
            {
                double maxLeft = Math.Max(0, FloatingWidgetLayer.ActualWidth - _widgetDragTarget.ActualWidth);
                double maxTop = Math.Max(0, FloatingWidgetLayer.ActualHeight - _widgetDragTarget.ActualHeight);
                Canvas.SetLeft(_widgetDragTarget, Clamp(_widgetDragStartLeft + dx, 0, maxLeft));
                Canvas.SetTop(_widgetDragTarget, Clamp(_widgetDragStartTop + dy, 0, maxTop));
            }

            e.Handled = true;
        }

        private void FloatingWidget_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_widgetDragging || _widgetDragTarget == null) return;

            var el = _widgetDragTarget;
            _widgetDragging = false;
            _widgetDragTarget = null;

            try { el.ReleaseMouseCapture(); } catch { /* not captured */ }

            if (!_widgetDragMoved)
            {
                // Clean click (no drag) → perform the widget's action.
                if (ReferenceEquals(el, AiAgentPill))
                    ToggleAgentChat();
                else if (ReferenceEquals(el, RecordButton))
                    DropdownButton_Click(el, new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            }

            e.Handled = true;
        }

        private void ToggleAgentChat()
        {
            if (DataContext is MainViewModel vm
                && vm.ToggleAgentChatCommand.CanExecute(null))
            {
                vm.ToggleAgentChatCommand.Execute(null);
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void MenuScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncingScroll) return;
            var sv = sender as ScrollViewer;
            if (sv == null || sv.ScrollableHeight <= 0) return;

            _isSyncingScroll = true;
            ScrollSlider.Value = sv.VerticalOffset / sv.ScrollableHeight;
            _isSyncingScroll = false;
        }

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
