// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/ColorMath.cs
// PROJECT: ProGlassAutomation
// MODULE: Dynamic Theme Engine — colour intelligence
//
// Small dependency-free colour maths used by the ThemeEngine to derive
// whole palettes from a single accent colour instead of hard-coding
// dozens of hex values. Everything is pure static math — trivially
// testable, no WPF types leaked in except System.Windows.Media.Color.
//
// Conversions:
//   • RGB ↔ HSL (hue/saturation/lightness — the "wise" space for
//     deriving hover/pressed/tint variants: shift L, keep H and S).
//   • Contrast ratio (WCAG relative luminance) to pick readable text
//     colours automatically.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>Pure colour math helpers for the dynamic theme engine.</summary>
    public static class ColorMath
    {
        // ══════════════════════════════════════════════════════════════
        //  HSL CONVERSION
        // ══════════════════════════════════════════════════════════════

        /// <summary>Converts an sRGB colour to HSL (h:0-360, s/l:0-1).</summary>
        public static (double H, double S, double L) ToHsl(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h = 0;
            if (delta > 0.0001)
            {
                if (max == r)
                    h = 60 * (((g - b) / delta) % 6);
                else if (max == g)
                    h = 60 * (((b - r) / delta) + 2);
                else
                    h = 60 * (((r - g) / delta) + 4);
            }
            if (h < 0) h += 360;

            double l = (max + min) / 2;
            double s = delta == 0
                ? 0
                : delta / (1 - Math.Abs(2 * l - 1));

            return (h, s, l);
        }

        /// <summary>Converts HSL back to sRGB (h:0-360, s/l:0-1).</summary>
        public static Color FromHsl(double h, double s, double l, byte alpha = 255)
        {
            h = ((h % 360) + 360) % 360;
            s = Clamp01(s);
            l = Clamp01(l);

            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = l - c / 2;

            double r, g, b;
            switch (h)
            {
                case < 60:  (r, g, b) = (c, x, 0); break;
                case < 120: (r, g, b) = (x, c, 0); break;
                case < 180: (r, g, b) = (0, c, x); break;
                case < 240: (r, g, b) = (0, x, c); break;
                case < 300: (r, g, b) = (x, 0, c); break;
                default:    (r, g, b) = (c, 0, x); break;
            }

            return Color.FromArgb(
                alpha,
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }

        // ══════════════════════════════════════════════════════════════
        //  DERIVATION — the "adapt colour wisely" toolbox
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Lightens (amount>0) or darkens (amount&lt;0) by shifting HSL
        /// lightness. Keeps hue + saturation, which is what makes the
        /// derived colours feel like the same brand colour.
        /// </summary>
        public static Color Lighten(Color c, double amount)
        {
            var (h, s, l) = ToHsl(c);
            return FromHsl(h, s, l + amount);
        }

        /// <summary>Blends two colours in RGB space (t: 0→a, 1→b).</summary>
        public static Color Mix(Color a, Color b, double t)
        {
            t = Clamp01(t);
            return Color.FromArgb(
                (byte)Math.Round(a.A + (b.A - a.A) * t),
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        /// <summary>Colour with a new alpha value.</summary>
        public static Color WithAlpha(Color c, byte alpha) =>
            Color.FromArgb(alpha, c.R, c.G, c.B);

        /// <summary>
        /// WCAG relative luminance (0=black … 1=white).
        /// </summary>
        public static double RelativeLuminance(Color c)
        {
            static double Channel(byte v)
            {
                double x = v / 255.0;
                return x <= 0.03928
                    ? x / 12.92
                    : Math.Pow((x + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Channel(c.R)
                 + 0.7152 * Channel(c.G)
                 + 0.0722 * Channel(c.B);
        }

        /// <summary>
        /// WCAG contrast ratio between two colours (1 … 21).
        /// 4.5+ is readable for normal text, 3+ for large text.
        /// </summary>
        public static double ContrastRatio(Color a, Color b)
        {
            double la = RelativeLuminance(a);
            double lb = RelativeLuminance(b);
            double lighter = Math.Max(la, lb);
            double darker = Math.Min(la, lb);
            return (lighter + 0.05) / (darker + 0.05);
        }

        /// <summary>
        /// Picks the most readable text colour (black or white family)
        /// for a given background — used for OnAccentForegroundBrush.
        /// </summary>
        public static Color BestTextOn(Color background)
        {
            var white = Color.FromRgb(255, 255, 255);
            var ink = Color.FromRgb(15, 23, 42);   // slate-900

            return ContrastRatio(background, white) >=
                   ContrastRatio(background, ink)
                ? white
                : ink;
        }

        /// <summary>
        /// Soft tint of an accent over a surface — the recipe behind
        /// TintBlueBrush etc. (accent mixed heavily toward the surface).
        /// </summary>
        public static Color Tint(Color accent, Color surface, double strength = 0.10) =>
            Mix(surface, accent, strength);

        private static double Clamp01(double v) =>
            v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
