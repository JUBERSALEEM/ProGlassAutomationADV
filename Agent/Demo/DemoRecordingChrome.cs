using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProGlassAutomation.Agent.Demo
{
    /// <summary>
    /// Lightweight recording banner on the live main window.
    /// Does not edit MainWindow.xaml.
    /// </summary>
    public static class DemoRecordingChrome
    {
        private const string BannerName = "ProGlassDemoRecordBanner";

        public static void Show(string text)
        {
            var win = Application.Current?.MainWindow;
            if (win == null) return;
            void Work()
            {
                var banner = Find(win) ?? Create(win);
                if (banner.Child is TextBlock tb)
                    tb.Text = text ?? "Recording demo…";
                banner.Visibility = Visibility.Visible;
            }
            if (win.Dispatcher.CheckAccess()) Work();
            else win.Dispatcher.Invoke(Work);
        }

        public static void Hide()
        {
            var win = Application.Current?.MainWindow;
            if (win == null) return;
            void Work()
            {
                var banner = Find(win);
                if (banner != null) banner.Visibility = Visibility.Collapsed;
            }
            if (win.Dispatcher.CheckAccess()) Work();
            else win.Dispatcher.Invoke(Work);
        }

        private static Border? Find(Window win)
        {
            if (win.Content is Panel panel)
            {
                foreach (var child in panel.Children)
                    if (child is Border b && b.Name == BannerName) return b;
            }
            return null;
        }

        private static Border Create(Window win)
        {
            var tb = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(16, 8, 16, 8),
                Text = "Recording demo…"
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "OnAccentForegroundBrush");

            var banner = new Border
            {
                Name = BannerName,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 64, 0, 0),
                Padding = new Thickness(4, 0, 4, 0),
                CornerRadius = new CornerRadius(12),
                IsHitTestVisible = false,
                Child = tb
            };
            banner.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
            Panel.SetZIndex(banner, 400);

            if (win.Content is Panel panel)
                panel.Children.Add(banner);
            return banner;
        }
    }
}
