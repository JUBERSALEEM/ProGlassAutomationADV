// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Export/ScreenshotService.cs
// PROJECT: ProGlassAutomation
// MODULE: Export — high-clarity window / UI capture
//
// Faithful implementation of the reference screenshot pipeline.
//
// CAPTURE METHOD (as in the reference):
//   - The requested output resolution is the QUALITY preset target.
//   - scale = max(targetWidth / ActualWidth, targetHeight / ActualHeight)
//   - if scale < 4.0  ->  scale = 4.0   (supersample floor for sharpness)
//   - width  = ActualWidth  * scale
//   - height = ActualHeight * scale
//   - dpi    = (int)(96 * scale)
//   - The LIVE window is rendered directly with RenderTargetBitmap at that
//     pixel size + DPI, then frozen (GPU-friendly). No upscaling of a
//     low-resolution bitmap — vector/text is re-rasterized at scale.
//
// SAVE:
//   The user picks the location/format via a SaveFileDialog
//   (PNG / BMP / JPEG). The default filename embeds the module name, the
//   quality label and a date/time stamp:
//     <Module>_<Quality>_yyyyMMdd_HHmmss.png
//
// PRIVACY: everything stays local. No uploads, no AI upscaling, no
// telemetry. Screenshots are treated as sensitive business data.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ProGlassAutomation.Services.Export
{
    /// <summary>Quality presets, exactly as in the reference.</summary>
    public enum ScreenshotQuality
    {
        HD,        // 2560 × 1440   (label "HD 720p")
        FullHD,    // 3840 × 2160   (label "Full HD 1080p")
        QHD,       // 5120 × 2880   (label "2K QHD")
        UltraHD,   // 7680 × 4320   (label "4K UHD")
        UHD8K      // 10240 × 5760  (label "8K UHD")
    }

    public static class ScreenshotService
    {
        /// <summary>
        /// Returns the quality preset's target output dimensions, exactly as
        /// in the reference switch.
        /// </summary>
        public static (int Width, int Height) GetTargetSize(ScreenshotQuality quality) => quality switch
        {
            ScreenshotQuality.HD      => (2560, 1440),
            ScreenshotQuality.QHD     => (5120, 2880),
            ScreenshotQuality.UltraHD => (7680, 4320),
            ScreenshotQuality.UHD8K   => (10240, 5760),
            _                         => (3840, 2160)   // FullHD / default
        };

        /// <summary>Friendly label per quality preset (reference wording).</summary>
        public static string GetLabel(ScreenshotQuality quality) => quality switch
        {
            ScreenshotQuality.HD      => "HD 720p",
            ScreenshotQuality.FullHD  => "Full HD 1080p",
            ScreenshotQuality.QHD     => "2K QHD",
            ScreenshotQuality.UltraHD => "4K UHD",
            ScreenshotQuality.UHD8K   => "8K UHD",
            _                         => "Full HD 1080p"
        };

        /// <summary>Resolution string shown under each quality button.</summary>
        public static string GetResolutionText(ScreenshotQuality quality)
        {
            var (w, h) = GetTargetSize(quality);
            return $"{w} × {h}";
        }

        /// <summary>
        /// Renders the main window using the reference's scale + DPI formula
        /// and returns the frozen bitmap (or null on failure).
        /// </summary>
        public static BitmapSource? CaptureMainWindow(ScreenshotQuality quality)
        {
            var window = Application.Current?.MainWindow;
            if (window == null) return null;

            try
            {
                // Reference: force layout + render-priority pump before capture.
                window.UpdateLayout();
                var frame = new DispatcherFrame();
                window.Dispatcher.BeginInvoke(
                    DispatcherPriority.Render,
                    new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);

                var (targetWidth, targetHeight) = GetTargetSize(quality);

                double actualW = Math.Max(1, window.ActualWidth);
                double actualH = Math.Max(1, window.ActualHeight);

                // Reference formula.
                double scaleX = targetWidth / actualW;
                double scaleY = targetHeight / actualH;
                double scale = Math.Max(scaleX, scaleY);
                if (scale < 4.0) scale = 4.0;

                int width = (int)(actualW * scale);
                int height = (int)(actualH * scale);
                int dpi = (int)(96 * scale);

                // Render the LIVE window directly (GPU-accelerated path).
                var rtb = new RenderTargetBitmap(
                    width, height, dpi, dpi, PixelFormats.Pbgra32);
                rtb.Render(window);
                rtb.Freeze();
                return rtb;
            }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Error(
                    "Screenshot render failed.", ex,
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
        /// Captures the FULL content of the active view's main scroll region
        /// (for long lists). Temporarily expands the ScrollViewer to its full
        /// ExtentHeight so nothing is clipped, renders, then restores the
        /// layout in a finally block.
        /// </summary>
        public static BitmapSource? CaptureFullPage(ScreenshotQuality quality)
        {
            var window = Application.Current?.MainWindow;
            if (window == null) return null;

            System.Windows.Controls.ScrollViewer? sv = null;
            bool svChanged = false;
            double svHeight = 0;

            try
            {
                // Find the active view (ContentControl hosts CurrentViewModel).
                if (window.Content is System.Windows.Controls.Border root
                    && root.Child is System.Windows.Controls.Grid grid)
                {
                    sv = FindDescendant<System.Windows.Controls.ScrollViewer>(grid);
                }
                if (sv == null)
                {
                    // No scroll region — fall back to a normal window capture.
                    return CaptureMainWindow(quality);
                }

                // Remember state and expand to the full content extent.
                svHeight = sv.Height;
                sv.Height = sv.ExtentHeight + 24;   // a little breathing room
                sv.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Hidden;
                svChanged = true;

                // Pump layout so the expanded height is realized.
                window.UpdateLayout();
                var frame = new DispatcherFrame();
                window.Dispatcher.BeginInvoke(
                    DispatcherPriority.Render,
                    new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);

                var (targetWidth, targetHeight) = GetTargetSize(quality);
                double actualW = Math.Max(1, window.ActualWidth);
                double actualH = Math.Max(1, window.ActualHeight);
                double scale = Math.Max(targetWidth / actualW, targetHeight / actualH);
                if (scale < 4.0) scale = 4.0;

                int width = (int)(actualW * scale);
                int height = (int)(actualH * scale);
                int dpi = (int)(96 * scale);

                var rtb = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);
                rtb.Render(window);
                rtb.Freeze();
                return rtb;
            }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Error(
                    "Full-page screenshot render failed.", ex,
                    new Services.Logging.LogContext
                    {
                        Module = "Export",
                        Class = nameof(ScreenshotService),
                        Operation = "CaptureFullPage"
                    });
                return null;
            }
            finally
            {
                // Always restore the layout so the app is never left broken.
                if (sv != null && svChanged)
                {
                    try
                    {
                        sv.Height = svHeight;
                        sv.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
                        window.UpdateLayout();
                    }
                    catch { /* restore is best-effort */ }
                }
            }
        }

        /// <summary>Depth-first search for a descendant of type T.</summary>
        private static T? FindDescendant<T>(System.Windows.DependencyObject parent)
            where T : System.Windows.DependencyObject
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match) return match;
                var deeper = FindDescendant<T>(child);
                if (deeper != null) return deeper;
            }
            return null;
        }

        /// <summary>
        /// Runs the reference's capture + SaveFileDialog flow. Returns true
        /// when a file was saved.
        /// </summary>
        public static bool CaptureAndSave(ScreenshotQuality quality)
            => SaveBitmap(CaptureMainWindow(quality), GetLabel(quality));

        /// <summary>
        /// Captures the full scrollable page (long lists) then prompts for a
        /// save location. Returns true when a file was saved.
        /// </summary>
        public static bool CaptureFullPageAndSave(ScreenshotQuality quality)
            => SaveBitmap(CaptureFullPage(quality), GetLabel(quality) + "_FullPage");

        private static bool SaveBitmap(BitmapSource? bitmap, string label)
        {
            if (bitmap == null)
            {
                System.Windows.MessageBox.Show(
                    "The screenshot could not be rendered. See the log for details.",
                    "Screenshot Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG Image|*.png|BMP Image|*.bmp|JPEG Image|*.jpg",
                FileName = $"{SafeModuleName()}_{GetLabel(quality)}_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                Title = "Save Screenshot",
                DefaultExt = ".png",
                AddExtension = true
            };

            if (sfd.ShowDialog() != true) return false;

            try
            {
                string filePath = sfd.FileName;
                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                BitmapEncoder encoder = extension switch
                {
                    ".bmp" => new BmpBitmapEncoder(),
                    ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 100 },
                    _ => new PngBitmapEncoder()   // default lossless PNG
                };
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using var fs = File.Create(filePath);
                encoder.Save(fs);

                var info = new FileInfo(filePath);
                string size = info.Length >= 1024 * 1024
                    ? $"{info.Length / (1024.0 * 1024.0):F2} MB"
                    : $"{info.Length / 1024.0:F1} KB";

                System.Windows.MessageBox.Show(
                    $"Screenshot saved!\n\nFile: {filePath}\n" +
                    $"Resolution: {bitmap.PixelWidth} × {bitmap.PixelHeight}\n" +
                    $"Size: {size}",
                    "Screenshot",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Error(
                    "Screenshot save failed.", ex,
                    new Services.Logging.LogContext
                    {
                        Module = "Export",
                        Class = nameof(ScreenshotService),
                        Operation = "Save"
                    });
                System.Windows.MessageBox.Show(
                    $"Error saving screenshot:\n\n{ex.Message}",
                    "Screenshot Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>Module name for the default filename, sanitised.</summary>
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
