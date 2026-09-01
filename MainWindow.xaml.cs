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
//   ✓ Top-bar popups and sidebar groups are CLICK-ONLY — hover open/close
//     and its close timers were removed entirely
//   ✓ UiTreeHelper also patched centrally for other views
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation
{
    /// <summary>
    /// Main shell window code-behind.
    /// Hosts top bar, left navigation drawer, module content area,
    /// and popups (Screen Record, User Switch, Quick Settings, Notifications, Screenshots).
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Global locale: mirror the active language direction onto
            // the shell (Arabic/Urdu → RightToLeft). The service also
            // flips this live when the user switches language.
            FlowDirection = Services.Localization.LocalizationService.Instance.IsRtl
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

            // The active visual family paints its own self-contained
            // window stage in-window (VisualThemeEngine) — no OS-
            // level chrome service to attach.

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
                    vm.IsStorageOpen = false;
                }
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

                // Storage popup — anchored to the sidebar card; clicking
                // anywhere else (including the drawer) dismisses it.
                if (vm.IsStorageOpen && !In(StorageCard) && !In(StoragePanelHost))
                    vm.IsStorageOpen = false;

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
            catch (System.Exception ex)
            {
                // Allow close on error
                ProGlassAutomation.Services.Logging.AppLogService.Debug("[MainWindow] Shutdown audit/cleanup failed: " + ex.Message);
            }
        }

        // ── top-bar dropdowns (click-only — no hover open/close) ───────────
        // Click toggles a popup; clicking outside dismisses it (the
        // PreviewMouseLeftButtonDown handler in the constructor). Hover
        // open/close was removed so the popups behave like normal menus.

        // Click toggles popup

        // ── sidebar groups (click-only accordion — no hover open/close) ────
        // Clicking a group header toggles its sub-menu. Only one group can
        // be open at a time (accordion). Hover behaviour was fully removed:
        // groups never open on mouse-enter or close on mouse-leave.

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

        private void NotificationsPopup_Opened(object sender, System.EventArgs e)
        {
            if (ActivityPanelHost != null)
                AsyncUtility.SafeFireAndForget(() => ActivityPanelHost.LoadAsync(), "MainWindow Activity Load");
        }
    }
}
