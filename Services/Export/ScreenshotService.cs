// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Export/ScreenshotService.cs
// PROJECT: ProGlassAutomation
// MODULE: Export — window screenshots
//
// Captures the main application window as a high-quality image at a chosen
// resolution (720p / 1080p / 2K / 4K) and saves it under
//   %AppData%\ProGlassAutomation\Screenshots\<Module>_yyyy-MM-dd_HH-mm-ss.png
//
// CLARITY TECHNIQUE (supersampling, adapted from the reference):
//   1. The visual tree is rendered at a higher pixel density than the
//      target (2× per axis) so text and vector edges are captured crisply.
//   2. That high-res render is downscaled to the exact target resolution
//      with high-quality (bilinear/linear) interpolation, which removes
//      aliasing and "jaggies" that plain Viewbox scaling produces.
//   3. The result is composited centred on a white canvas sized to the
//      exact requested resolution, so 720p/1080p/2K/4K are always the true
//      output dimensions.
//
// The filename embeds the current module name (from the shell's
// CurrentModuleTitle) plus a local date/time stamp, so each capture is
// unique and traceable to the screen that produced it.
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
            ("720p",   1280,  720),
            ("1080p",  1920, 1080),
            ("2K",     2560, 1440),
            ("4K",     3840, 2160)
        };

        /// <summary>Oversample factor per axis for extra sharpness.</summary>
        private const int SuperSample = 2;

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
        /// Renders the element to an exact target resolution using a
        /// supersampling + high-quality downscale pipeline for maximum
        /// clarity.
        /// </summary>
        private static BitmapSource? RenderToBitmap(
            FrameworkElement element, int targetWidth, int targetHeight)
        {
            var size = new Size(
                Math.Max(1, element.ActualWidth),
                Math.Max(1, element.ActualHeight));

            if (size.Width <= 0 || size.Height <= 0) return null;

            // ── 1. Supersampled high-DPI render of the visual tree ──────
            int renderW = Math.Max(1, (int)Math.Round(size.Width * SuperSample));
            int renderH = Math.Max(1, (int)Math.Round(size.Height * SuperSample));
            int renderDpi = 96 * SuperSample;

            var highRes = new RenderTargetBitmap(
                renderW, renderH, renderDpi, renderDpi, PixelFormats.Pbgra32);

            // Wrap in a Viewbox so the tree scales to the render size while
            // keeping its aspect ratio; the inner Border provides the
            // background (Viewbox has none).
            var surface = new System.Windows.Controls.Border
            {
                Background = Brushes.White,
                Child = element
            };
            var container = new System.Windows.Controls.Viewbox
            {
                Stretch = Stretch.Uniform,
                Width = renderW,
                Height = renderH,
                Child = surface
            };

            container.Measure(new Size(renderW, renderH));
            container.Arrange(new Rect(0, 0, renderW, renderH));
            container.UpdateLayout();

            highRes.Render(container);
            highRes.Freeze();

            // ── 2. Downscale to fit the target with high-quality sampling ──
            double fitScale = Math.Min(
                targetWidth / (double)renderW,
                targetHeight / (double)renderH);

            int fitW = Math.Max(1, (int)Math.Round(renderW * fitScale));
            int fitH = Math.Max(1, (int)Math.Round(renderH * fitScale));

            BitmapSource fitted = highRes;
            if (fitW != renderW || fitH != renderH)
            {
                var scaled = new TransformedBitmap(
                    highRes,
                    new ScaleTransform(fitW / (double)renderW, fitH / (double)renderH));
                scaled.Freeze();
                fitted = scaled;
            }

            // ── 3. Compose centred on an exact-resolution white canvas ────
            // (ensures the output is exactly targetWidth x targetHeight even
            //  when the window's aspect ratio differs from the target).
            if (fitted.PixelWidth == targetWidth && fitted.PixelHeight == targetHeight)
                return fitted;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null,
                    new Rect(0, 0, targetWidth, targetHeight));

                double x = (targetWidth - fitW) / 2.0;
                double y = (targetHeight - fitH) / 2.0;
                dc.DrawImage(fitted, new Rect(x, y, fitW, fitH));
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
}
