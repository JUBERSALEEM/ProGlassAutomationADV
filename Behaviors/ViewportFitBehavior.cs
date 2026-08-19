// Global adaptive viewport — NOT a Viewbox lock on the whole app.
//
// Root causes of the laptop / desktop mismatch:
//   1. Wrapping MainWindow in a 1440×900 Viewbox froze composition and
//      hid overflow instead of reflowing.
//   2. Dialogs with MinHeight=600 overflowed 768p work areas.
//   3. Density tokens existed but user zoom did not.
//
// This guard:
//   • caps every window to the live work area
//   • applies ThemeManager.UiScale as a LayoutTransform so Ctrl+Wheel
//     zoom shows MORE or LESS content (content size = client / scale)
//   • never locks a design canvas on the shell
// Splash is a poster: it uses a local Viewbox in SplashView.xaml only.

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Viewport;

namespace ProGlassAutomation.Behaviors
{
    public static class ViewportFitBehavior
    {
        public static readonly DependencyProperty SkipProperty =
            DependencyProperty.RegisterAttached(
                "Skip", typeof(bool), typeof(ViewportFitBehavior),
                new PropertyMetadata(false));

        public static readonly DependencyProperty DisableZoomProperty =
            DependencyProperty.RegisterAttached(
                "DisableZoom", typeof(bool), typeof(ViewportFitBehavior),
                new PropertyMetadata(false));

        public static bool GetSkip(DependencyObject d) => (bool)d.GetValue(SkipProperty);
        public static void SetSkip(DependencyObject d, bool v) => d.SetValue(SkipProperty, v);
        public static bool GetDisableZoom(DependencyObject d) => (bool)d.GetValue(DisableZoomProperty);
        public static void SetDisableZoom(DependencyObject d, bool v) => d.SetValue(DisableZoomProperty, v);

        private static readonly DependencyProperty HookedProperty =
            DependencyProperty.RegisterAttached(
                "Hooked", typeof(bool), typeof(ViewportFitBehavior),
                new PropertyMetadata(false));

        private static bool _installed;

        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnWindowLoaded));

            ThemeManager.UiScaleChanged += (_, _) => ApplyScaleToOpenWindows();

            try
            {
                SystemEvents.DisplaySettingsChanged += (_, _) =>
                {
                    var app = Application.Current;
                    if (app == null) return;
                    app.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        foreach (Window w in app.Windows)
                        {
                            try
                            {
                                ViewportService.CapWindowToWorkArea(w);
                                ApplyUserScale(w);
                            }
                            catch { /* closing window */ }
                        }
                    }));
                };
            }
            catch
            {
                // Session 0 / no interactive desktop.
            }
        }

        private static void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(sender, e.OriginalSource)) return;
            if (sender is not Window window) return;
            Attach(window);
        }

        public static void Attach(Window window)
        {
            if (window == null) return;
            if (GetSkip(window)) return;

            ViewportService.CapWindowToWorkArea(window);

            if ((bool)window.GetValue(HookedProperty))
            {
                ApplyUserScale(window);
                return;
            }

            window.SetValue(HookedProperty, true);
            window.PreviewMouseWheel += OnPreviewMouseWheel;
            window.PreviewKeyDown += OnPreviewKeyDown;
            window.SizeChanged += (_, _) => ApplyUserScale(window);
            window.DpiChanged += (_, _) =>
            {
                ViewportService.CapWindowToWorkArea(window);
                ApplyUserScale(window);
            };

            ApplyUserScale(window);
        }

        public static void ApplyScaleToOpenWindows()
        {
            var app = Application.Current;
            if (app == null) return;
            foreach (Window w in app.Windows)
            {
                try { ApplyUserScale(w); }
                catch { /* closing */ }
            }
        }

        public static void ApplyUserScale(Window window)
        {
            if (window == null) return;
            if (GetSkip(window) || GetDisableZoom(window)) return;
            if (window.Content is not FrameworkElement fe) return;
            if (string.Equals(window.GetType().Name, "SplashView", StringComparison.Ordinal))
                return;

            double scale = ThemeManager.UiScale;
            double availW = window.ActualWidth;
            double availH = window.ActualHeight;
            if (availW < 80 || availH < 80)
                return;

            if (Math.Abs(scale - 1.0) < 0.001)
            {
                fe.ClearValue(FrameworkElement.WidthProperty);
                fe.ClearValue(FrameworkElement.HeightProperty);
                fe.LayoutTransform = Transform.Identity;
                return;
            }

            // Zoom out (0.85) → content is measured larger, then drawn
            // smaller, so more of the UI fits. Zoom in is the reverse.
            fe.Width = availW / scale;
            fe.Height = availH / scale;
            fe.LayoutTransform = new ScaleTransform(scale, scale);
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not Window window) return;
            if (GetDisableZoom(window)) return;
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

            double next = ThemeManager.UiScale + (e.Delta > 0
                ? ViewportService.UiScaleStep
                : -ViewportService.UiScaleStep);
            CommitScale(next);
            e.Handled = true;
        }

        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            if (e.Key == Key.D0 || e.Key == Key.NumPad0)
            {
                CommitScale(ViewportService.DefaultUiScale);
                e.Handled = true;
            }
            else if (e.Key == Key.OemPlus || e.Key == Key.Add)
            {
                CommitScale(ThemeManager.UiScale + ViewportService.UiScaleStep);
                e.Handled = true;
            }
            else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
            {
                CommitScale(ThemeManager.UiScale - ViewportService.UiScaleStep);
                e.Handled = true;
            }
        }

        private static void CommitScale(double scale)
        {
            scale = ViewportService.ClampUiScale(scale);
            ThemeManager.ApplyUiScale(scale);

            try
            {
                if (Application.Current is App && App.Config?.Appearance != null)
                {
                    App.Config.Appearance.UiScale = scale;
                    ConfigService.Save(App.Config);
                }
            }
            catch
            {
                // Zoom must still work if config is mid-load.
            }
        }
    }
}
