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
        /// (long lists) instead of only the visible viewport.
        ///
        /// WHY NOT RENDER THE ScrollViewer: a ScrollViewer only ever renders
        /// its viewport, and its ActualHeight is clamped by the shell's
        /// fixed-size content card — so the old approach of "expand the
        /// ScrollViewer and render it" still produced only the visible page.
        ///
        /// CORRECT APPROACH: a ScrollViewer MEASURES its content with
        /// unbounded height (that is how scrolling works), so the content
        /// element is already laid out at its FULL extent. We therefore render
        /// the CONTENT element (not the clipped viewer) at the full page size.
        /// The layout is restored in a finally block.
        /// </summary>
        public static BitmapSource? CaptureFullPage(ScreenshotQuality quality)
        {
            var window = Application.Current?.MainWindow;
            if (window == null) return null;

            System.Windows.Controls.ScrollViewer? sv = null;
            System.Windows.FrameworkElement? content = null;
            bool changed = false;
            double? savedHeight = null;
            var savedScrollBar = System.Windows.Controls.ScrollBarVisibility.Auto;

            try
            {
                // Find the active view (ContentControl hosts CurrentViewModel).
                if (window.Content is System.Windows.Controls.Border root
                    && root.Child is System.Windows.Controls.Grid grid)
                {
                    sv = FindMainVerticalScrollViewer(grid);
                }

                if (sv == null || sv.ScrollableHeight <= 0)
                {
                    // Nothing actually scrolls — a normal capture is equivalent.
                    return CaptureMainWindow(quality);
                }

                content = sv.Content as System.Windows.FrameworkElement;
                if (content == null)
                {
                    // The scroll host is an internal presenter (e.g. a DataGrid)
                    // with no directly reachable content — fall back safely.
                    return CaptureMainWindow(quality);
                }

                // The content is measured unbounded vertically, so its
                // ExtentHeight already equals the full page height.
                double fullW = Math.Max(1, content.ActualWidth);
                double fullH = Math.Max(1, sv.ExtentHeight);

                // Pump layout so ExtentHeight/ActualWidth are current.
                window.UpdateLayout();

                // Force the content to its full extent explicitly so the
                // rendered bitmap is as tall as the whole page, and hide the
                // scrollbar so it never appears inside the frame.
                savedHeight = content.Height;
                content.Height = fullH;
                savedScrollBar = sv.VerticalScrollBarVisibility;
                sv.VerticalScrollBarVisibility =
                    System.Windows.Controls.ScrollBarVisibility.Hidden;
                changed = true;

                var frame = new DispatcherFrame();
                window.Dispatcher.BeginInvoke(
                    DispatcherPriority.Render,
                    new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);

                // RENDER THE CONTENT, NOT THE ScrollViewER: rendering the
                // viewer clips to its viewport; rendering the content captures
                // the whole page (vectors/text are re-rasterised at scale).
                var (targetWidth, targetHeight) = GetTargetSize(quality);
                var (width, height, dpi) =
                    ComputeRaster(fullW, fullH, targetWidth, targetHeight);

                var rtb = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);
                rtb.Render(content);
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
                if (changed && content != null && sv != null)
                {
                    try
                    {
                        content.Height = savedHeight.GetValueOrDefault();
                        sv.VerticalScrollBarVisibility = savedScrollBar;
                        window.UpdateLayout();
                    }
                    catch { /* restore is best-effort */ }
                }
            }
        }

        /// <summary>
        /// Computes the raster dimensions/DPI for a content box using the
        /// reference scale formula (target ÷ actual, with a 4× supersample
        /// floor), then clamps so neither output dimension exceeds WPF's
        /// practical texture limit (16384). Without the clamp a very long
        /// page could be requested at tens of thousands of pixels tall, which
        /// WPF cannot rasterise (it throws or renders blank).
        /// </summary>
        private static (int Width, int Height, int Dpi) ComputeRaster(
            double contentW, double contentH, double targetW, double targetH)
        {
            double scale = Math.Max(targetW / contentW, targetH / contentH);
            if (scale < 4.0) scale = 4.0;   // reference supersample floor

            // Cap to the WPF texture limit, scaling the supersample down only
            // when the page is exceptionally tall/wide.
            const double maxDim = 16384.0;
            double scaleX = maxDim / (contentW * scale);
            double scaleY = maxDim / (contentH * scale);
            double cap = Math.Min(scaleX, scaleY);
            if (cap < 1.0) scale *= cap;

            int width = Math.Max(1, (int)(contentW * scale));
            int height = Math.Max(1, (int)(contentH * scale));
            int dpi = Math.Max(1, (int)(96 * scale));
            return (width, height, dpi);
        }

        /// <summary>
        /// Depth-first search for the module's main vertical scroll region.
        /// Prefers a ScrollViewer that (a) actually scrolls vertically and
        /// (b) has directly reachable content (its Content is a
        /// FrameworkElement we can render), choosing the one with the largest
        /// ExtentHeight. Nested, non-reachable scroll hosts (e.g. a DataGrid's
        /// internal presenter) are skipped so we don't grab a viewer whose
        /// content we cannot render.
        /// </summary>
        private static System.Windows.Controls.ScrollViewer? FindMainVerticalScrollViewer(
            System.Windows.DependencyObject parent)
        {
            System.Windows.Controls.ScrollViewer? best = null;
            double bestExtent = 0;

            void Walk(System.Windows.DependencyObject node)
            {
                if (node is System.Windows.Controls.ScrollViewer sv
                    && sv.Content is System.Windows.FrameworkElement
                    && sv.ScrollableHeight > 0
                    && sv.ExtentHeight > bestExtent)
                {
                    best = sv;
                    bestExtent = sv.ExtentHeight;
                }
                // Keep walking into it — there may be a deeper scrolling
                // list that holds more content than the wrapper.
                int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < count; i++)
                    Walk(System.Windows.Media.VisualTreeHelper.GetChild(node, i));
            }

            Walk(parent);
            return best;
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
                FileName = $"{SafeModuleName()}_{label}_{DateTime.Now:yyyyMMdd_HHmmss}.png",
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
