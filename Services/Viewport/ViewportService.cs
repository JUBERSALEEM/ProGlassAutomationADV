// Adaptive display policy. Layouts reflow in native pixels.
// User zoom is a separate multiplier (Ctrl+Wheel), not a locked
// 1440×900 Viewbox. Splash is the only poster that uses Viewbox.

using System;
using System.Windows;

namespace ProGlassAutomation.Services.Viewport
{
    public static class ViewportService
    {
        public const double MinUiScale = 0.80;
        public const double MaxUiScale = 1.25;
        public const double DefaultUiScale = 1.0;
        public const double UiScaleStep = 0.05;

        public static Rect WorkArea
        {
            get
            {
                try { return SystemParameters.WorkArea; }
                catch { return new Rect(0, 0, 1440, 900); }
            }
        }

        public static double ClampUiScale(double scale)
        {
            if (double.IsNaN(scale) || double.IsInfinity(scale))
                return DefaultUiScale;
            if (scale < MinUiScale) return MinUiScale;
            if (scale > MaxUiScale) return MaxUiScale;
            return Math.Round(scale / UiScaleStep) * UiScaleStep;
        }

        /// <summary>
        /// Comfortable chrome on a short laptop overflows. Only
        /// downgrade — never upgrade a user who chose Compact.
        /// </summary>
        public static string RecommendDensity(string? preferred, double workHeight)
        {
            string key = (preferred ?? "Compact").Trim();
            if (key is not ("Compact" or "Normal" or "Comfortable"))
                key = "Compact";

            if (workHeight < 780)
                return "Compact";
            if (workHeight < 900 && key == "Comfortable")
                return "Normal";
            return key;
        }

        public static void CapWindowToWorkArea(Window window)
        {
            if (window == null) return;

            var wa = WorkArea;
            double maxW = Math.Max(320, wa.Width - 16);
            double maxH = Math.Max(240, wa.Height - 16);

            if (window.MinWidth > maxW)
                window.MinWidth = maxW;
            if (window.MinHeight > maxH)
                window.MinHeight = maxH;

            if (window.WindowState == WindowState.Maximized)
                return;

            if (!double.IsNaN(window.Width) && window.Width > maxW)
                window.Width = maxW;
            if (!double.IsNaN(window.Height) && window.Height > maxH)
                window.Height = maxH;

            if (double.IsNaN(window.MaxWidth) || window.MaxWidth > maxW)
                window.MaxWidth = maxW;
            if (double.IsNaN(window.MaxHeight) || window.MaxHeight > maxH)
                window.MaxHeight = maxH;
        }
    }
}
