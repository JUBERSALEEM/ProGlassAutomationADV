// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Shared/AboutPanel.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared — About popup + screenshot capture
//
// Shows real app identity from AppInfo, and a screenshot capture control.
// Clicking the Capture button opens a resolution dropdown (720p / 1080p /
// 2K / 4K); choosing one captures the main window at that resolution and
// saves it under %AppData%\ProGlassAutomation\Screenshots with the current
// module name and a date/time stamp in the filename.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Export;
using ProGlassAutomation.Services.Updates;
using System;
using System.Windows;
using System.Windows.Controls;

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
            };
        }

        /// <summary>
        /// Opens a resolution dropdown anchored to the Capture button. Each
        /// option captures at that resolution.
        /// </summary>
        private void CaptureMenuButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu { FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 12 };

            foreach (var (label, width, height) in ScreenshotService.Resolutions)
            {
                var item = new MenuItem
                {
                    Header = label,
                    Padding = new Thickness(4, 6, 20, 6)
                };
                int w = width, h = height;
                item.Click += (_, _) => Capture(w, h);
                menu.Items.Add(item);
            }

            menu.PlacementTarget = CaptureMenuButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void Capture(int width, int height)
        {
            CaptureStatus.Text = "Capturing…";

            // Render on the UI thread (the visual tree must be read there).
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var result = ScreenshotService.CaptureMainWindow(width, height);

                if (!result.Success)
                {
                    CaptureStatus.Text = "Capture failed:\n" + result.Error;
                    return;
                }

                string size = result.FileSizeBytes >= 1024 * 1024
                    ? $"{result.FileSizeBytes / (1024.0 * 1024.0):F2} MB"
                    : $"{result.FileSizeBytes / 1024.0:F1} KB";

                CaptureStatus.Text =
                    $"Saved {result.Width} × {result.Height} · {size}\n" +
                    result.Path;

                // Offer to open the folder.
                var msg = MessageBox.Show(
                    $"Screenshot saved:\n\n{result.Path}\n\n" +
                    $"{result.Width} × {result.Height} · {size} · PNG (lossless)\n\n" +
                    "Open the Screenshots folder?",
                    "Screenshot Saved",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (msg == MessageBoxResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = System.IO.Path.GetDirectoryName(result.Path),
                                UseShellExecute = true
                            });
                    }
                    catch { /* opening the folder is best-effort */ }
                }
            }));
        }
    }
}
