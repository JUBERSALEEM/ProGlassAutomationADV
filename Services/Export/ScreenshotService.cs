// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Export/ScreenshotService.cs
// PROJECT: ProGlassAutomation
// MODULE: Export — high-clarity window / UI capture
//
// Captures the application's active module UI as a professional image at a
// requested output resolution (720p / 1080p / 2K / 4K) and saves it under
//   %AppData%\ProGlassAutomation\Screenshots\<Module>_yyyy-MM-dd_HH-mm-ss.png
//
// RENDERING METHOD (correct WPF approach)
//   The existing live visual is rendered DIRECTLY with RenderTargetBitmap
//   at a computed pixel scale. There is NO re-parenting of the element into
//   a Viewbox/Border (that breaks the visual tree and yields a tiny/partial
//   capture), and there is NO upscaling of a low-resolution bitmap. The
//   vector UI (text, borders, icons, charts, rounded cards, gradients) is
//   re-rasterized at the target pixel size, so output is genuinely crisp.
//
//   scale = targetOutputPixels / sourceVisualDips
//   pixelWidth  = sourceVisualWidth  * scale
//   pixelHeight = sourceVisualHeight * scale
//   dpiX = dpiY = 96 * scale
//
//   The bitmap is then composited centred on an exact-resolution white
//   canvas so 720p/1080p/2K/4K always yield the true output dimensions,
//   regardless of the window's aspect ratio.
//
// SHARPENING: supersampling is NOT a resize-up. We render at a supersampled
// DPI so vector edges are rasterized more densely, then downscale to the
// exact target only when the aspect ratio differs. No "AI" upscaling, no
// external services — everything stays local (privacy-safe).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProGlassAutomation.Services.Export
{
    /// <summary>
    /// Generates high-clarity screenshots of the application UI at an exact
    /// output resolution by re-rasterizing the live WPF visual directly.
    /// </summary>
    public static class ScreenshotService
    {
        /// <summary>Target output resolutions offered to the user.</summary>
        public static (string Label, int Width, int Height)[] Resolutions { get; } = new[]
        {
            ("720p",   1280,  720),
            ("1080p",  1920, 1080),
            ("2K",     2560, 1440),
            ("4K",     3840, 2160)
        };

        /// <summary>
        /// Extra DPI oversampling per axis for sharper vector/text edges.
        /// Rendering at 2× the target DPI and downscaling removes aliasing
        /// without enlarging the output dimensions.
        /// </summary>
        private const double Oversample = 2.0;

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

        /// <summary>Capture result carrying the path + diagnostics.</summary>
        public sealed class CaptureResult
        {
            public bool Success { get; init; }
            public string? Path { get; init; }
            public int Width { get; init; }
            public int Height { get; init; }
            public int Dpi { get; init; }
            public long FileSizeBytes { get; init; }
            public string? Error { get; init; }
        }

        /// <summary>
        /// Captures the main window's UI at the given output width/height and
        /// returns the saved path + diagnostics, or a failed result.
        /// </summary>
        public static CaptureResult CaptureMainWindow(int width, int height)
        {
            var window = Application.Current?.MainWindow;
            if (window == null)
                return Fail("Application window is not available.");

            try
            {
                // Ensure the current layout is fully realized before render.
                window.UpdateLayout();
                DispatcherHelper.WaitForRender(window);

                var bitmap = RenderToBitmap(window, width, height);
                if (bitmap == null)
                    return Fail("The visual could not be rendered.");

                string module = SafeModuleName();
                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string fileName = $"{module}_{stamp}.png";
                string path = Path.Combine(ScreenshotsFolder, fileName);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                long sizeBytes;
                using (var fs = new FileStream(
                    path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    encoder.Save(fs);
                    sizeBytes = fs.Length;
                }

                return new CaptureResult
                {
                    Success = true,
                    Path = path,
                    Width = bitmap.PixelWidth,
                    Height = bitmap.PixelHeight,
                    Dpi = 96,
                    FileSizeBytes = sizeBytes
                };
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
                return Fail(ex.Message);
            }
        }

        private static CaptureResult Fail(string error) => new()
        {
            Success = false,
            Error = error
        };

        /// <summary>
        /// Renders the existing <paramref name="element"/> directly to a
        /// bitmap of the requested output size. Re-rasterizes vector content
        /// at the target resolution — no low-res upscale, no re-parenting.
        /// </summary>
        private static BitmapSource? RenderToBitmap(
            FrameworkElement element, int targetWidth, int targetHeight)
        {
            double sourceW = element.ActualWidth;
            double sourceH = element.ActualHeight;
            if (sourceW <= 0 || sourceH <= 0) return null;

            // ── Compute the DPI-aware render scale ───────────────────────
            // Render at an oversampled DPI so edges are crisp, then scale to
            // the exact target. scale maps source DIPs -> output pixels.
            double scale = Math.Min(
                targetWidth / sourceW,
                targetHeight / sourceH) * Oversample;

            int renderDpi = Math.Max(96, (int)Math.Round(96 * scale));
            int pixelW = Math.Max(1, (int)Math.Round(sourceW * scale));
            int pixelH = Math.Max(1, (int)Math.Round(sourceH * scale));

            // ── Render the LIVE visual directly (no clone / re-parent) ──
            var highRes = new RenderTargetBitmap(
                pixelW, pixelH, renderDpi, renderDpi, PixelFormats.Pbgra32);
            highRes.Render(element);
            highRes.Freeze();

            // ── Downscale the oversampled render to the exact target ─────
            // (only when the aspect ratio differs from the target).
            double fit = Math.Min(
                targetWidth / (double)highRes.PixelWidth,
                targetHeight / (double)highRes.PixelHeight);

            BitmapSource fitted = highRes;
            if (Math.Abs(fit - 1.0) > 0.001)
            {
                int fw = Math.Max(1, (int)Math.Round(highRes.PixelWidth * fit));
                int fh = Math.Max(1, (int)Math.Round(highRes.PixelHeight * fit));
                var scaled = new TransformedBitmap(
                    highRes,
                    new ScaleTransform(fw / (double)highRes.PixelWidth,
                                       fh / (double)highRes.PixelHeight));
                scaled.Freeze();
                fitted = scaled;
            }

            // ── Exact-resolution composition (centred on white canvas) ───
            if (fitted.PixelWidth == targetWidth && fitted.PixelHeight == targetHeight)
                return fitted;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null,
                    new Rect(0, 0, targetWidth, targetHeight));

                double x = (targetWidth - fitted.PixelWidth) / 2.0;
                double y = (targetHeight - fitted.PixelHeight) / 2.0;
                dc.DrawImage(fitted, new Rect(x, y, fitted.PixelWidth, fitted.PixelHeight));
            }

            var final = new RenderTargetBitmap(
                targetWidth, targetHeight, 96, 96, PixelFormats.Pbgra32);
            final.Render(visual);
            final.Freeze();
            return final;
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

    /// <summary>Small helpers for correct UI-thread / render-cycle waits.</summary>
    internal static class DispatcherHelper
    {
        /// <summary>
        /// Pumps the dispatcher through a Loaded-priority frame so the current
        /// layout/render pass completes before we snapshot it. Avoids capturing
        /// a half-rendered frame and does not block the UI with Sleep.
        /// </summary>
        public static void WaitForRender(FrameworkElement element)
        {
            var dispatcher = element.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted) return;

            try
            {
                // Process queued operations up to the Render priority so
                // bindings/layout settle before the RenderTargetBitmap render.
                var frame = new System.Windows.Threading.DispatcherFrame();
                dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Render,
                    new Action(() => frame.Continue = false));
                System.Windows.Threading.Dispatcher.PushFrame(frame);
            }
            catch
            {
                // A failed render-wait must never break the screenshot.
            }
        }
    }
}
