// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Shared/ScreenshotPanel.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared — screenshot capture panel
//
// A dedicated, self-contained screenshot control (separate from the About
// panel). Lists the reference-quality presets (HD / Full HD / 2K / 4K / 8K),
// an optional full-page mode for long lists, and captures via
// ScreenshotService. The shell hosts this in its own popup.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Export;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation.Views.Shared
{
    public partial class ScreenshotPanel : UserControl
    {
        public ScreenshotPanel()
        {
            InitializeComponent();
            Loaded += (_, _) => BuildQualityList();
        }

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
                QualityList.Children.Add(BuildQualityButton(icon, quality, textColor, badgeColor));
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

            var iconText = new TextBlock { Text = icon, FontSize = 20, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(iconText, 0);
            grid.Children.Add(iconText);

            var textStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = ScreenshotService.GetLabel(quality),
                FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("PrimaryForegroundBrush")
            });
            textStack.Children.Add(new TextBlock
            {
                Text = ScreenshotService.GetResolutionText(quality),
                FontSize = 10, Foreground = (Brush)FindResource("MutedForegroundBrush")
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
                FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor))
            };
            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);

            button.Content = grid;
            button.Click += (_, _) => Capture(quality);
            return button;
        }

        private void Capture(ScreenshotQuality quality)
        {
            bool fullPage = FullPageCheck?.IsChecked == true;
            CaptureStatus.Text =
                $"Capturing {(fullPage ? "full page · " : "")}{ScreenshotService.GetLabel(quality)}…";

            Dispatcher.BeginInvoke(new Action(() =>
            {
                bool ok = fullPage
                    ? ScreenshotService.CaptureFullPageAndSave(quality)
                    : ScreenshotService.CaptureAndSave(quality);
                CaptureStatus.Text = ok
                    ? $"{ScreenshotService.GetLabel(quality)} saved."
                    : "Capture cancelled or failed.";
            }));
        }
    }
}
