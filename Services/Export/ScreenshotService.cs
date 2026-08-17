// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Export/ScreenshotService.cs
// PROJECT: ProGlassAutomation
// MODULE: Export — window screenshots
//
// Captures the main application window as a high-quality PNG at a chosen
// resolution (720p / 1080p / 2K / 4K), renders the visual tree to a bitmap
// at the requested size, and saves it under
//   %AppData%\ProGlassAutomation\Screenshots\<Module>_yyyy-MM-dd_HH-mm-ss.png
//
// The filename embeds the current module name (from the shell's
// CurrentModuleTitle) plus a UTC-ish local date/time stamp, so each capture
// is unique and traceable to the screen that produced it.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProGlassAutomation.Services.Export
{
    public static class ScreenshotService
    {
        /// <summary>Target resolutions offered to the user.</summary>
        public static (string Label, int Width, int Height)[] Resolutions { get; } = new[]
        {
            ("720p",   1280, 720),
            ("1080p",  1920, 1080),
            ("2K",     2560, 1440),
            ("4K",     3840, 2160)
        };

        private static string ScreenshotsFolder
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "ProGlassAutomation", "Screenshots");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        /// <summary>
        /// Captures the main window as a PNG at the given width/height and
        /// returns the saved file path, or null on failure.
        /// </summary>
        public static string? CaptureMainWindow(int width, int height)
        {
            var window = Application.Current?.MainWindow;
            if (window == null) return null;

            try
            {
                var bitmap = RenderToBitmap(window, width, height);
                if (bitmap == null) return null;

                string module = SafeModuleName();
                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string fileName = $"{module}_{stamp}.png";
                string path = Path.Combine(ScreenshotsFolder, fileName);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using (var fs = new FileStream(
                    path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    encoder.Save(fs);
                }

                return path;
            }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Error(
                    "Screenshot capture failed.", ex,
                    new Services.Logging.LogContext
                    {
                        Module = "Export",
                        Class = nameof(ScreenshotService),
                        Operation = "Capture"
                    });
                return null;
            }
        }

        /// <summary>
        /// Renders the element's visual tree to a WriteableBitmap at the
        /// target size. Scales the bitmap up/down to the requested
        /// resolution while keeping the layout's aspect ratio.
        /// </summary>
        private static BitmapSource? RenderToBitmap(
            FrameworkElement element, int targetWidth, int targetHeight)
        {
            var size = new Size(
                Math.Max(1, element.ActualWidth),
                Math.Max(1, element.ActualHeight));

            if (size.Width <= 0 || size.Height <= 0) return null;

            // Preserve the element's aspect ratio, scaled to fit the target.
            double scale = Math.Min(
                targetWidth / size.Width,
                targetHeight / size.Height);

            int w = Math.Max(1, (int)Math.Round(size.Width * scale));
            int h = Math.Max(1, (int)Math.Round(size.Height * scale));

            var rtb = new RenderTargetBitmap(
                w, h, 96, 96, PixelFormats.Pbgra32);

            // Wrap the element in a Viewbox so the captured window scales up
            // to the target resolution while keeping its aspect ratio. The
            // Border inside provides the background (Viewbox has none).
            var container = new System.Windows.Controls.Viewbox
            {
                Stretch = Stretch.Uniform,
                Width = w,
                Height = h
            };

            var surface = new System.Windows.Controls.Border
            {
                Background = Brushes.White,
                Child = element
            };

            container.Child = surface;

            container.Measure(new Size(w, h));
            container.Arrange(new Rect(0, 0, w, h));
            container.UpdateLayout();

            rtb.Render(container);
            rtb.Freeze();
            return rtb;
        }

        /// <summary>Module name for the filename, sanitised for the file system.</summary>
        private static string SafeModuleName()
        {
            string name = Application.Current?.MainWindow?.DataContext is
                ViewModels.MainViewModel vm
                ? vm.CurrentModuleTitle
                : "MODULE";

            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return string.IsNullOrWhiteSpace(name) ? "MODULE" : name.Trim();
        }
    }
}
