// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/GlassShowcase/GlassAssemblyShowcase.cs
// PROJECT: ProGlassAutomation
// MODULE: Proforma Invoice — Step 4 product visualization
//
// Manufacturing drawing for SGU / DGU / LAM / DGU+LAM.
//   SGU     — wide single pane (the mockup hero sheet)
//   DGU     — packed outer / spacer / inner sandwich
//   LAM     — exploded outer / PVB / inner stack
//   DGU+LAM — cutaway: vertical outer + cavity + laminated pair
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Controls.GlassShowcase
{
    public enum GlassAssemblyKind
    {
        Sgu,
        Dgu,
        Lam,
        DguLam
    }

    public class GlassAssemblyShowcase : FrameworkElement
    {
        public GlassAssemblyShowcase()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            ClipToBounds = true;
            MinHeight = 160;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ThemeManager.ThemeChanged += OnThemeChanged;
            ThemeManager.AccentChanged += OnThemeChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            ThemeManager.AccentChanged -= OnThemeChanged;
        }

        private void OnThemeChanged(object? sender, EventArgs e)
            => InvalidateVisual();

        public static readonly DependencyProperty KindProperty =
            DependencyProperty.Register(nameof(Kind), typeof(GlassAssemblyKind), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata(GlassAssemblyKind.Sgu, FrameworkPropertyMetadataOptions.AffectsRender));

        public GlassAssemblyKind Kind
        {
            get => (GlassAssemblyKind)GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        public static readonly DependencyProperty OuterColorProperty =
            DependencyProperty.Register(nameof(OuterColor), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("Clear", FrameworkPropertyMetadataOptions.AffectsRender));
        public string OuterColor { get => (string)GetValue(OuterColorProperty); set => SetValue(OuterColorProperty, value); }

        public static readonly DependencyProperty OuterThicknessProperty =
            DependencyProperty.Register(nameof(OuterThickness), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("6mm", FrameworkPropertyMetadataOptions.AffectsRender));
        public string OuterThickness { get => (string)GetValue(OuterThicknessProperty); set => SetValue(OuterThicknessProperty, value); }

        public static readonly DependencyProperty OuterTreatmentProperty =
            DependencyProperty.Register(nameof(OuterTreatment), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
        public string OuterTreatment { get => (string)GetValue(OuterTreatmentProperty); set => SetValue(OuterTreatmentProperty, value); }

        public static readonly DependencyProperty InnerColorProperty =
            DependencyProperty.Register(nameof(InnerColor), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("Clear", FrameworkPropertyMetadataOptions.AffectsRender));
        public string InnerColor { get => (string)GetValue(InnerColorProperty); set => SetValue(InnerColorProperty, value); }

        public static readonly DependencyProperty InnerThicknessProperty =
            DependencyProperty.Register(nameof(InnerThickness), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("6mm", FrameworkPropertyMetadataOptions.AffectsRender));
        public string InnerThickness { get => (string)GetValue(InnerThicknessProperty); set => SetValue(InnerThicknessProperty, value); }

        public static readonly DependencyProperty InnerTreatmentProperty =
            DependencyProperty.Register(nameof(InnerTreatment), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
        public string InnerTreatment { get => (string)GetValue(InnerTreatmentProperty); set => SetValue(InnerTreatmentProperty, value); }

        public static readonly DependencyProperty SpacerWidthProperty =
            DependencyProperty.Register(nameof(SpacerWidth), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("12mm", FrameworkPropertyMetadataOptions.AffectsRender));
        public string SpacerWidth { get => (string)GetValue(SpacerWidthProperty); set => SetValue(SpacerWidthProperty, value); }

        public static readonly DependencyProperty SpacerMaterialProperty =
            DependencyProperty.Register(nameof(SpacerMaterial), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
        public string SpacerMaterial { get => (string)GetValue(SpacerMaterialProperty); set => SetValue(SpacerMaterialProperty, value); }

        public static readonly DependencyProperty InterlayerColorProperty =
            DependencyProperty.Register(nameof(InterlayerColor), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("Clear", FrameworkPropertyMetadataOptions.AffectsRender));
        public string InterlayerColor { get => (string)GetValue(InterlayerColorProperty); set => SetValue(InterlayerColorProperty, value); }

        public static readonly DependencyProperty InterlayerThicknessProperty =
            DependencyProperty.Register(nameof(InterlayerThickness), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("1.52mm", FrameworkPropertyMetadataOptions.AffectsRender));
        public string InterlayerThickness { get => (string)GetValue(InterlayerThicknessProperty); set => SetValue(InterlayerThicknessProperty, value); }

        public static readonly DependencyProperty InterlayerTypeProperty =
            DependencyProperty.Register(nameof(InterlayerType), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("PVB", FrameworkPropertyMetadataOptions.AffectsRender));
        public string InterlayerType { get => (string)GetValue(InterlayerTypeProperty); set => SetValue(InterlayerTypeProperty, value); }

        public static readonly DependencyProperty LamOuterColorProperty =
            DependencyProperty.Register(nameof(LamOuterColor), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("Clear", FrameworkPropertyMetadataOptions.AffectsRender));
        public string LamOuterColor { get => (string)GetValue(LamOuterColorProperty); set => SetValue(LamOuterColorProperty, value); }

        public static readonly DependencyProperty LamOuterThicknessProperty =
            DependencyProperty.Register(nameof(LamOuterThickness), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("6mm", FrameworkPropertyMetadataOptions.AffectsRender));
        public string LamOuterThickness { get => (string)GetValue(LamOuterThicknessProperty); set => SetValue(LamOuterThicknessProperty, value); }

        public static readonly DependencyProperty LamInnerColorProperty =
            DependencyProperty.Register(nameof(LamInnerColor), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("Clear", FrameworkPropertyMetadataOptions.AffectsRender));
        public string LamInnerColor { get => (string)GetValue(LamInnerColorProperty); set => SetValue(LamInnerColorProperty, value); }

        public static readonly DependencyProperty LamInnerThicknessProperty =
            DependencyProperty.Register(nameof(LamInnerThickness), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("6mm", FrameworkPropertyMetadataOptions.AffectsRender));
        public string LamInnerThickness { get => (string)GetValue(LamInnerThicknessProperty); set => SetValue(LamInnerThicknessProperty, value); }

        public static readonly DependencyProperty CaptionProperty =
            DependencyProperty.Register(nameof(Caption), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
        public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

        public static readonly DependencyProperty ShowWellProperty =
            DependencyProperty.Register(nameof(ShowWell), typeof(bool), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
        public bool ShowWell { get => (bool)GetValue(ShowWellProperty); set => SetValue(ShowWellProperty, value); }

        public static readonly DependencyProperty ShowCaptionProperty =
            DependencyProperty.Register(nameof(ShowCaption), typeof(bool), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
        public bool ShowCaption { get => (bool)GetValue(ShowCaptionProperty); set => SetValue(ShowCaptionProperty, value); }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 40 || h < 40) return;

            var outline = ThemeColor("OutlineBrush", Color.FromRgb(0x94, 0xA3, 0xB8));
            var well = ThemeColor("MenuHoverBrush", Color.FromRgb(0xEE, 0xF2, 0xF7));
            var text = ThemeColor("PrimaryForegroundBrush", Color.FromRgb(0x0F, 0x17, 0x2A));
            var muted = ThemeColor("SecondaryForegroundBrush", Color.FromRgb(0x64, 0x74, 0x8B));
            var accent = ThemeColor("AccentBrush", Color.FromRgb(0x1D, 0x4E, 0xD8));

            if (ShowWell)
            {
                var wellBrush = new SolidColorBrush(well);
                wellBrush.Freeze();
                dc.DrawRoundedRectangle(wellBrush, null, new Rect(0, 0, w, h), 10, 10);
            }

            switch (Kind)
            {
                case GlassAssemblyKind.Sgu:
                    DrawSguSheet(dc, w, h, text, outline);
                    break;
                case GlassAssemblyKind.Dgu:
                    DrawDguSandwich(dc, w, h, muted, outline, accent);
                    break;
                case GlassAssemblyKind.Lam:
                    DrawLamExploded(dc, w, h, muted, outline);
                    break;
                case GlassAssemblyKind.DguLam:
                    DrawDguLamCutaway(dc, w, h, muted, outline, accent);
                    break;
            }
        }

        // Wide hero pane — matches the SGU mockup sheet.
        private void DrawSguSheet(DrawingContext dc, double w, double h, Color text, Color outline)
        {
            double paneW = w * 0.86;
            double depth = Math.Min(h * 0.42, 110);
            double thick = VisualThickness(OuterThickness, 14, 28);
            double left = (w - paneW) * 0.5;
            double frontY = h * 0.52;

            DrawWidePane(dc, left, frontY, paneW, depth, thick, GlassTint(OuterColor), outline);

            if (ShowCaption)
            {
                string label = string.IsNullOrWhiteSpace(Caption)
                    ? BuildPaneLabel(OuterThickness, OuterColor, OuterTreatment)
                    : Caption;
                var ft = Format(label, 13, FontWeights.SemiBold, text);
                dc.DrawText(ft, new Point((w - ft.Width) * 0.5, frontY - depth * 0.38 - ft.Height * 0.5));
            }
        }

        private void DrawDguSandwich(DrawingContext dc, double w, double h, Color muted, Color outline, Color accent)
        {
            double paneW = w * 0.70;
            double depth = Math.Min(h * 0.22, 70);
            double outerT = VisualThickness(OuterThickness, 10, 18);
            double innerT = VisualThickness(InnerThickness, 10, 18);
            double gap = Math.Clamp(ParseMm(SpacerWidth) * 1.1, 14, 28);
            double left = (w - paneW) * 0.5;
            double y = h * 0.38;

            DrawWidePane(dc, left + 14, y + gap + outerT, paneW - 16, depth, innerT, GlassTint(InnerColor), outline);
            DrawSpacerBar(dc, left + 8, y + outerT + 3, paneW - 10, depth * 0.55, gap - 4, outline, accent);
            DrawWidePane(dc, left, y, paneW, depth, outerT, GlassTint(OuterColor), outline);

            DrawSideLabel(dc, left - 8, y + 2, "OUTER", muted, true);
            DrawSideLabel(dc, left - 8, y + outerT + gap * 0.25, SpacerWidth ?? "ASP", muted, true);
            DrawSideLabel(dc, left - 8, y + gap + outerT + 2, "INNER", muted, true);
        }

        private void DrawLamExploded(DrawingContext dc, double w, double h, Color muted, Color outline)
        {
            double paneW = Math.Min(w * 0.46, 420);
            double depth = Math.Min(h * 0.20, 68);
            double outerT = VisualThickness(OuterThickness, 10, 16);
            double innerT = VisualThickness(InnerThickness, 10, 16);
            double filmT = Math.Clamp(ParseMm(InterlayerThickness) * 3.2, 8, 14);
            double explode = Math.Min(h * 0.12, 28);
            double cx = (w - paneW) * 0.42;
            double top = h * 0.22;

            DrawWidePane(dc, cx + explode * 1.6, top + explode * 2 + outerT + filmT, paneW, depth, innerT, GlassTint(InnerColor), outline);
            DrawWidePane(dc, cx + explode * 0.8, top + explode + outerT, paneW, depth, filmT, InterlayerTint(InterlayerColor, InterlayerType), outline, film: true);
            DrawWidePane(dc, cx, top, paneW, depth, outerT, GlassTint(OuterColor), outline);

            DrawSideLabel(dc, cx - 10, top + 4, "OUTER", muted, true);
            DrawSideLabel(dc, cx + explode * 0.4, top + explode + outerT + 2, InterlayerLabel(), muted, true);
            DrawSideLabel(dc, cx + explode * 1.2, top + explode * 2 + outerT + filmT + 2, "INNER", muted, true);
        }

        // Cutaway: standing outer lite + air cavity + laminated pair.
        private void DrawDguLamCutaway(DrawingContext dc, double w, double h, Color muted, Color outline, Color accent)
        {
            double cx = w * 0.18;
            double baseY = h * 0.78;
            double liteH = h * 0.58;
            double liteW = Math.Min(w * 0.10, 36);
            double cavity = Math.Clamp(ParseMm(SpacerWidth) * 1.4, 18, 34);

            // Standing outer lite (slight perspective).
            DrawStandingLite(dc, cx, baseY, liteW, liteH, GlassTint(OuterColor), outline);

            // Cavity / spacer.
            var cavityFill = Mix(accent, Color.FromRgb(0x9C, 0xA3, 0xAF), 0.55);
            cavityFill.A = 90;
            dc.DrawRectangle(new SolidColorBrush(cavityFill),
                new Pen(new SolidColorBrush(outline), 0.8),
                new Rect(cx + liteW + 2, baseY - liteH * 0.92, cavity, liteH * 0.88));

            // Laminated pair lying as a stack in front/right.
            double stackX = cx + liteW + cavity + 16;
            double paneW = Math.Min(w * 0.42, 260);
            double depth = Math.Min(h * 0.16, 48);
            double lamOutT = VisualThickness(LamOuterThickness, 8, 14);
            double filmT = Math.Clamp(ParseMm(InterlayerThickness) * 2.4, 6, 11);
            double lamInT = VisualThickness(LamInnerThickness, 8, 14);
            double y = baseY - 18;

            DrawWidePane(dc, stackX + 18, y, paneW, depth, lamInT, GlassTint(LamInnerColor), outline);
            DrawWidePane(dc, stackX + 9, y - filmT - 6, paneW, depth, filmT, InterlayerTint(InterlayerColor, InterlayerType), outline, film: true);
            DrawWidePane(dc, stackX, y - filmT - lamOutT - 12, paneW, depth, lamOutT, GlassTint(LamOuterColor), outline);

            DrawSideLabel(dc, cx, baseY - liteH - 4, "OUTER", muted, false);
        }

        private static void DrawStandingLite(
            DrawingContext dc, double x, double baseY, double thickness, double height, Color glass, Color outline)
        {
            Point bl = new(x, baseY);
            Point br = new(x + thickness, baseY - 8);
            Point tr = new(x + thickness + 10, baseY - height);
            Point tl = new(x + 8, baseY - height + 10);

            DrawQuad(dc, tl, tr, br, bl, Mix(glass, Colors.White, 0.18), outline, 0.72);

            var hi = new LinearGradientBrush(
                Color.FromArgb(90, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                new Point(0, 0), new Point(1, 1));
            hi.Freeze();
            var geo = Quad(tl, Lerp(tl, tr, 0.35), Lerp(bl, br, 0.35), bl);
            dc.DrawGeometry(hi, null, geo);
        }

        private static void DrawWidePane(
            DrawingContext dc, double left, double frontY, double width, double depth, double thick, Color glass, Color outline, bool film = false)
        {
            double skew = depth * 0.62;
            double rise = depth * 0.48;

            Point fl = new(left, frontY);
            Point fr = new(left + width, frontY);
            Point br = new(left + width + skew, frontY - rise);
            Point bl = new(left + skew, frontY - rise);

            Point flb = new(fl.X, fl.Y + thick);
            Point frb = new(fr.X, fr.Y + thick);
            Point brb = new(br.X, br.Y + thick);

            var top = Mix(glass, Colors.White, film ? 0.06 : 0.28);
            var front = Mix(glass, Colors.Black, film ? 0.10 : 0.16);
            var side = Mix(glass, Colors.Black, film ? 0.18 : 0.28);

            DrawQuad(dc, fl, fr, br, bl, top, outline, film ? 0.94 : 0.80);
            DrawQuad(dc, fl, fr, frb, flb, front, outline, film ? 0.90 : 0.74);
            DrawQuad(dc, fr, br, brb, frb, side, outline, film ? 0.88 : 0.70);

            if (!film)
            {
                // Specular wash across the top face.
                var hi = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0.35)
                };
                hi.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0));
                hi.GradientStops.Add(new GradientStop(Color.FromArgb(120, 255, 255, 255), 0.22));
                hi.GradientStops.Add(new GradientStop(Color.FromArgb(20, 255, 255, 255), 0.55));
                hi.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
                hi.Freeze();
                dc.DrawGeometry(hi, null, Quad(fl, fr, br, bl));

                var edge = new Pen(new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), 1.4);
                edge.Freeze();
                dc.DrawLine(edge, fl, fr);
            }
        }

        private static void DrawSpacerBar(
            DrawingContext dc, double left, double y, double width, double depth, double thick, Color outline, Color accent)
        {
            var alu = ThemeManager.CurrentTheme == AppTheme.Dark
                ? Color.FromRgb(0x6B, 0x72, 0x80)
                : Color.FromRgb(0x9C, 0xA3, 0xAF);
            DrawWidePane(dc, left, y, width, depth, Math.Max(thick, 8), Mix(accent, alu, 0.45), outline, film: true);
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
