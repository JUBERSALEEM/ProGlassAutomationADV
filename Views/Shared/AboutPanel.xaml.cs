// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Shared/AboutPanel.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared — About popup + screenshot capture
//
// Shows real app identity from AppInfo, and a screenshot capture control.
// Clicking the Capture button opens a quality popup (HD / Full HD / 2K / 4K /
// 8K) exactly matching the reference UI: each row shows an icon, a title, the
// output resolution and a colour badge. Choosing one runs the reference's
// capture pipeline (RenderTargetBitmap at a computed scale + SaveFileDialog
// with PNG/BMP/JPEG) via ScreenshotService.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Export;
using ProGlassAutomation.Services.Updates;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation.Views.Shared
{
    public partial class AboutPanel : UserControl
    {
        public AboutPanel()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                VersionText.Text = $"Version {AppInfo.Version}";
                DeveloperText.Text = AppInfo.Developer;
                CopyrightText.Text = AppInfo.Copyright;
                BuildQualityList();
            };
        }

        /// <summary>Reveals/hides the inline quality selector.</summary>
        private void CaptureToggle_Click(object sender, RoutedEventArgs e)
        {
            if (CaptureToggle.IsChecked == true)
            {
                QualityPanel.Visibility = Visibility.Visible;
            }
            else
            {
                QualityPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>Builds the reference-style quality rows.</summary>
        private void BuildQualityList()
        {
            QualityList.Children.Clear();

            var presets = new[]
            {
                ("📱", ScreenshotQuality.HD,      "#64748B", "#F1F5F9"),
                ("🖥️", ScreenshotQuality.FullHD,  "#2563EB", "#DBEAFE"),
                ("🎬", ScreenshotQuality.QHD,     "#D97706", "#FEF3C7"),
                ("📺", ScreenshotQuality.UltraHD, "#059669", "#D1FAE5"),
                ("🏆", ScreenshotQuality.UHD8K,   "#DC2626", "#FEE2E2")
            };

            foreach (var (icon, quality, textColor, badgeColor) in presets)
                QualityList.Children.Add(BuildQualityButton(
                    icon, quality, textColor, badgeColor));
        }

        private Button BuildQualityButton(
            string icon, ScreenshotQuality quality, string textColor, string badgeColor)
        {
            var button = new Button
            {
                Tag = quality,
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(12, 10, 12, 10),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = (Brush)FindResource("ModuleBackgroundBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 20,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconText, 0);
            grid.Children.Add(iconText);

            var textStack = new StackPanel
            {
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            textStack.Children.Add(new TextBlock
            {
                Text = ScreenshotService.GetLabel(quality),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("PrimaryForegroundBrush")
            });
            textStack.Children.Add(new TextBlock
            {
                Text = ScreenshotService.GetResolutionText(quality),
                FontSize = 10,
                Foreground = (Brush)FindResource("MutedForegroundBrush")
            });
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            var badge = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(badgeColor)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = ScreenshotService.GetLabel(quality).Split(' ')[0],
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor))
            };
            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);

            button.Content = grid;
            button.Click += (_, _) =>
            {
                QualityPanel.Visibility = Visibility.Collapsed;
                CaptureToggle.IsChecked = false;
                Capture(quality);
            };
            return button;
        }

        private void Capture(ScreenshotQuality quality)
        {
            CaptureStatus.Text = $"Capturing {ScreenshotService.GetLabel(quality)}…";

            // Render + save dialog run on the UI thread (visual tree access).
            Dispatcher.BeginInvoke(new Action(() =>
            {
                bool ok = ScreenshotService.CaptureAndSave(quality);
                CaptureStatus.Text = ok
                    ? $"{ScreenshotService.GetLabel(quality)} saved."
                    : "Capture cancelled or failed.";
            }));
        }
    }
}
