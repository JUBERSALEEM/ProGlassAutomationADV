// ═══════════════════════════════════════════════════════════════════════
// FILE: GlassAssemblyShowcase.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Controls.GlassShowcase
{
        public partial class GlassAssemblyShowcase : FrameworkElement
    {
            private static void DrawSpacerBar(
            DrawingContext dc, double left, double y, double width, double depth, double thick, Color outline, Color accent)
        {
            // Professional metallic aluminum spacer bar
            double skew = depth * 0.62;
            double rise = depth * 0.48;

            Point fl = new(left, y);
            Point fr = new(left + width, y);
            Point br = new(left + width + skew, y - rise);
            Point bl = new(left + skew, y - rise);

            Point flb = new(fl.X, fl.Y + thick);
            Point frb = new(fr.X, fr.Y + thick);
            Point brb = new(br.X, br.Y + thick);

            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            
            // Metallic aluminum gradient for top face
            var topGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            if (dark)
            {
                topGradient.GradientStops.Add(new GradientStop(Color.FromArgb(220, 90, 95, 105), 0));
                topGradient.GradientStops.Add(new GradientStop(Color.FromArgb(240, 140, 145, 155), 0.3));
                topGradient.GradientStops.Add(new GradientStop(Color.FromArgb(255, 180, 185, 195), 0.5));
                topGradient.GradientStops.Add(new GradientStop(Color.FromArgb(240, 140, 145, 155), 0.7));
                topGradient.GradientStops.Add(new GradientStop(Color.FromArgb(220, 90, 95, 105), 1));
            }
            else
            {
                topGradient.GradientStops.Add(new GradientStop(Color.FromArgb(220, 140, 145, 155), 0));
                topGradient.GradientStops.Add(new GradientStop(Color.FromArgb(240, 190, 195, 205), 0.3));
                topGradient.GradientStops.Add(new GradientStop(Color.FromArgb(255, 220, 225, 235), 0.5));
                topGradient.GradientStops.Add(new GradientStop(Color.FromArgb(240, 190, 195, 205), 0.7));
                topGradient.GradientStops.Add(new GradientStop(Color.FromArgb(220, 140, 145, 155), 1));
            }
            topGradient.Freeze();

            // Front face - darker metallic
            Color frontColor = dark ? Color.FromArgb(200, 70, 75, 85) : Color.FromArgb(200, 120, 125, 135);
            var frontBrush = new SolidColorBrush(frontColor);
            frontBrush.Freeze();

            // Side face - medium metallic
            Color sideColor = dark ? Color.FromArgb(210, 80, 85, 95) : Color.FromArgb(210, 130, 135, 145);
            var sideBrush = new SolidColorBrush(sideColor);
            sideBrush.Freeze();

            var pen = new Pen(new SolidColorBrush(Color.FromArgb(180, outline.R, outline.G, outline.B)), 1.0);
            pen.Freeze();

            // Draw three faces
            dc.DrawGeometry(topGradient, pen, Quad(fl, fr, br, bl));
            dc.DrawGeometry(frontBrush, pen, Quad(fl, fr, frb, flb));
            dc.DrawGeometry(sideBrush, pen, Quad(fr, br, brb, frb));

            // Add metallic highlight on top
            var highlight = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            highlight.GradientStops.Add(new GradientStop(Color.FromArgb(120, 255, 255, 255), 0));
            highlight.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.5));
            highlight.Freeze();
            dc.DrawGeometry(highlight, null, Quad(fl, fr, br, bl));

            // Add "ALUMINUM SPACER" text if there's room
            if (width > 120)
            {
                var text = Format("AL SPACER", 7, FontWeights.SemiBold, 
                    dark ? Color.FromRgb(220, 220, 230) : Color.FromRgb(60, 60, 70));
                double textX = left + width * 0.5 - text.Width * 0.5;
                double textY = y - rise * 0.5 - text.Height * 0.5;
                dc.DrawText(text, new Point(textX, textY));
            }
        }

        private static StreamGeometry Quad(Point a, Point b, Point c, Point d)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(a, true, true);
                ctx.LineTo(b, true, false);
                ctx.LineTo(c, true, false);
                ctx.LineTo(d, true, false);
            }
            geo.Freeze();
            return geo;
        }

        private static void DrawQuad(DrawingContext dc, Point a, Point b, Point c, Point d, Color fill, Color outline, double opacity)
        {
            fill.A = (byte)Math.Clamp((int)(opacity * 255), 40, 255);
            var brush = new SolidColorBrush(fill);
            brush.Freeze();
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(150, outline.R, outline.G, outline.B)), 0.9);
            pen.Freeze();
            dc.DrawGeometry(brush, pen, Quad(a, b, c, d));
        }

        private static void DrawSideLabel(DrawingContext dc, double x, double y, string text, Color color, bool rightAlign)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var ft = Format(text.ToUpperInvariant(), 9, FontWeights.Bold, color);
            double px = rightAlign ? x - ft.Width : x;
            if (px < 4) px = 4;
            dc.DrawText(ft, new Point(px, y));
        }

        private static FormattedText Format(string text, double size, FontWeight weight, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return new FormattedText(
                text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
                size, brush, 1.25);
        }

        private string InterlayerLabel()
        {
            string type = string.IsNullOrWhiteSpace(InterlayerType) ? "PVB" : InterlayerType;
            return $"{InterlayerThickness} {type}".Trim();
        }

        private static string BuildPaneLabel(string? thk, string? color, string? treatment)
        {
            string t = (treatment ?? "").Replace(" Glass", "", StringComparison.OrdinalIgnoreCase).Trim();
            return $"{thk} {color} {t}".Trim();
        }

        public static Color GlassTint(string? name)
        {
            string n = (name ?? "").Trim().ToLowerInvariant();
            if (n.Contains("bronze") || n.Contains("brown"))
                return Color.FromArgb(210, 176, 126, 72);
            if (n.Contains("green"))
                return Color.FromArgb(205, 86, 168, 122);
            if (n.Contains("ford") || n.Contains("blue"))
                return Color.FromArgb(205, 72, 132, 196);
            if (n.Contains("dark grey") || n.Contains("dark gray") || n.Contains("hd grey") || n.Contains("hd gray"))
                return Color.FromArgb(215, 90, 102, 116);
            if (n.Contains("grey") || n.Contains("gray"))
                return Color.FromArgb(210, 130, 142, 156);
            if (n.Contains("silver") || n.Contains("reflect"))
                return Color.FromArgb(200, 176, 190, 204);
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            return dark
                ? Color.FromArgb(200, 140, 188, 210)
                : Color.FromArgb(210, 168, 210, 222);
        }

        public static Color InterlayerTint(string? color, string? type)
        {
            string t = (type ?? "").ToLowerInvariant();
            string c = (color ?? "").ToLowerInvariant();
            if (c.Contains("bronze") || c.Contains("brown"))
                return Color.FromArgb(230, 196, 132, 48);
            if (c.Contains("green"))
                return Color.FromArgb(220, 196, 168, 48);
            if (t.Contains("sgp") || t.Contains("ionoplast"))
                return Color.FromArgb(210, 214, 196, 120);
            return Color.FromArgb(230, 214, 154, 48);
        }

        public static double ParseMm(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;
            var span = raw.AsSpan().Trim();
            int i = 0;
            while (i < span.Length && !char.IsDigit(span[i]) && span[i] != '.') i++;
            int start = i;
            while (i < span.Length && (char.IsDigit(span[i]) || span[i] == '.')) i++;
            if (start == i) return 0;
            return double.TryParse(span[start..i], NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                ? v : 0;
        }

        private static double VisualThickness(string? raw, double minPx, double maxPx)
        {
            double mm = ParseMm(raw);
            if (mm <= 0) return (minPx + maxPx) * 0.5;
            return Math.Clamp(5 + mm * 1.2, minPx, maxPx);
        }

        private static Color Mix(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromArgb(
                (byte)(a.A + (b.A - a.A) * t),
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }

        private static Point Lerp(Point a, Point b, double t)
            => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        private static Color ThemeColor(string key, Color fallback)
        {
            var res = Application.Current?.TryFindResource(key);
            return res is SolidColorBrush b ? b.Color : fallback;
        }
    }
}
