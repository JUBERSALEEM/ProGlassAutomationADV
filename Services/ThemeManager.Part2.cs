// ═══════════════════════════════════════════════════════════════════════
// FILE: ThemeManager.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Theme;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Services
{
        public static partial class ThemeManager
    {
            public static double CardBorderThickness => _cardBorderThickness;

        /// <summary>
        /// Applies a uniform border thickness to every card / panel via the
        /// shared CardBorderThickness theme resource. Cards reference it with
        /// {StaticResource CardBorderThickness}, so one call restyles the whole
        /// app instead of editing hundreds of individual card declarations.
        /// </summary>
        public static void ApplyCardBorderThickness(double thickness)
        {
            if (thickness < 0.0) thickness = 0.0;
            _cardBorderThickness = thickness;

            var resources = Application.Current?.Resources;
            if (resources == null) return;

            resources["CardBorderThickness"] = new Thickness(thickness);
        }

        /// <summary>
        /// Re-applies the saved card border thickness. Called after a theme
        /// swap because loading a new dictionary restores that theme's default.
        /// </summary>
        public static void ReapplyCardBorderThickness()
            => ApplyCardBorderThickness(_cardBorderThickness);

        private static double _cardCornerRadius = 12;

        /// <summary>Current global card corner radius in DIPs.</summary>
        public static double CardCornerRadius => _cardCornerRadius;

        /// <summary>
        /// Applies one corner radius to every card, input and button via
        /// CardCornerRadius / ControlCornerRadius / CardCornerRadiusLarge.
        /// All three are the SAME value so nothing looks "more round"
        /// than the Appearance slider (default 12).
        /// </summary>
        public static void ApplyCardCornerRadius(double radius)
        {
            if (radius < 0.0) radius = 0.0;
            if (radius > 28.0) radius = 28.0;
            _cardCornerRadius = radius;

            var resources = Application.Current?.Resources;
            if (resources == null) return;

            var cr = new CornerRadius(radius);
            resources["CardCornerRadius"] = cr;
            resources["ControlCornerRadius"] = cr;
            resources["CardCornerRadiusLarge"] = cr;
        }

        /// <summary>
        /// Re-applies the saved card corner radius after a theme swap.
        /// </summary>
        public static void ReapplyCardCornerRadius()
            => ApplyCardCornerRadius(_cardCornerRadius);

        private static string _displayDensity = "Compact";

        public static string DisplayDensity => _displayDensity;

        /// <summary>
        /// One switch restyles shared chrome for laptop vs desktop:
        /// input/button height, title size, KPI size, card padding.
        /// Views bind these tokens — do not edit each screen.
        /// </summary>
        public static void ApplyDisplayDensity(string density)
        {
            string key = (density ?? "Compact").Trim();
            if (key is not ("Compact" or "Normal" or "Comfortable"))
                key = "Compact";
            _displayDensity = key;

            var resources = Application.Current?.Resources;
            if (resources == null) return;

            double inputH, buttonH, rowH, title, kpi, body, buttonMin;
            Thickness cardPad, stripPad, pageMargin;

            switch (key)
            {
                case "Comfortable":
                    inputH = 38; buttonH = 40; rowH = 34;
                    title = 22; kpi = 20; body = 14; buttonMin = 110;
                    cardPad = new Thickness(16, 12, 16, 12);
                    stripPad = new Thickness(18, 12, 18, 12);
                    pageMargin = new Thickness(18, 12, 18, 12);
                    break;
                case "Normal":
                    inputH = 34; buttonH = 36; rowH = 30;
                    title = 20; kpi = 18; body = 13; buttonMin = 96;
                    cardPad = new Thickness(12, 9, 12, 9);
                    stripPad = new Thickness(14, 9, 14, 9);
                    pageMargin = new Thickness(14, 10, 14, 10);
                    break;
                default: // Compact — default, slightly tighter than before
                    inputH = 28; buttonH = 30; rowH = 26;
                    title = 17; kpi = 16; body = 13; buttonMin = 84;
                    cardPad = new Thickness(8, 6, 8, 6);
                    stripPad = new Thickness(10, 6, 10, 6);
                    pageMargin = new Thickness(10, 6, 10, 6);
                    break;
            }

            resources["DensityInputHeight"] = inputH;
            resources["DensityButtonHeight"] = buttonH;
            resources["DensityRowHeight"] = rowH;
            resources["DensityTitleFontSize"] = title;
            resources["DensityKpiValueFontSize"] = kpi;
            resources["DensityBodyFontSize"] = body;
            resources["DensityButtonMinWidth"] = buttonMin;
            resources["DensityCardPadding"] = cardPad;
            resources["DensityStripPadding"] = stripPad;
            resources["DensityPageMargin"] = pageMargin;

            // Theme Studio: the Card Padding slider scales the density
            // token, so a density change must re-snapshot and re-apply
            // the studio layer on top (otherwise the studio's padding
            // silently vanishes after the user picks a new density).
            Theme.ThemeStudioService.OnFamilyApplied();
        }

        public static void ReapplyDisplayDensity()
            => ApplyDisplayDensity(_displayDensity);

        private static double _uiScale = 1.0;

        /// <summary>User zoom 0.80–1.25. 1.0 = native layout (reflow).</summary>
        public static double UiScale => _uiScale;

        public static void ApplyUiScale(double scale)
        {
            scale = Viewport.ViewportService.ClampUiScale(scale);
            if (Math.Abs(_uiScale - scale) < 0.001) return;
            _uiScale = scale;

            var resources = Application.Current?.Resources;
            if (resources != null)
                resources["UiScale"] = scale;

            UiScaleChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void FreezeAndSet(
            ResourceDictionary resources, string key, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();                 // frozen = thread-safe + fast
            resources[key] = brush;
        }

        private static void FreezeAndSetBrush(
            ResourceDictionary resources, string key, Brush brush)
        {
            if (brush.CanFreeze) brush.Freeze();
            resources[key] = brush;
        }
    }
}
