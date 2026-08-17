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
                string? path = ScreenshotService.CaptureMainWindow(width, height);

                if (path == null)
                {
                    CaptureStatus.Text = "Capture failed — see the log.";
                    return;
                }

                CaptureStatus.Text = "Saved:\n" + path;

                // Offer to open the folder.
                var result = MessageBox.Show(
                    "Screenshot saved:\n\n" + path +
                    "\n\nOpen the Screenshots folder?",
                    "Screenshot Saved",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = System.IO.Path.GetDirectoryName(path),
                                UseShellExecute = true
                            });
                    }
                    catch { /* opening the folder is best-effort */ }
                }
            }));
        }
    }
}
