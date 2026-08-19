// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/GlassShowcase/GlassAssemblyShowcase.cs
// PROJECT: ProGlassAutomation
// MODULE: Proforma Invoice — Step 4 product visualization
//
// Isometric glass-assembly drawing for SGU / DGU / LAM / DGU+LAM.
// Presentation only: every input is a bindable string/number from the
// existing Step 4 parameter VMs. No pricing, persistence or navigation.
//
// Material tints (clear / grey / bronze / green / blue) are product
// colours, not brand chrome. Theme tokens drive outline, text, shadow
// and the showcase well so Light / Dark / Monet restyle the frame.
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
            MinHeight = 140;
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

        // ── Kind ──────────────────────────────────────────────────────

        public static readonly DependencyProperty KindProperty =
            DependencyProperty.Register(
                nameof(Kind), typeof(GlassAssemblyKind), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata(GlassAssemblyKind.Sgu, FrameworkPropertyMetadataOptions.AffectsRender));

        public GlassAssemblyKind Kind
        {
            get => (GlassAssemblyKind)GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        // ── Outer / single pane ───────────────────────────────────────

        public static readonly DependencyProperty OuterColorProperty =
            DependencyProperty.Register(nameof(OuterColor), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("Clear", FrameworkPropertyMetadataOptions.AffectsRender));

        public string OuterColor
        {
            get => (string)GetValue(OuterColorProperty);
            set => SetValue(OuterColorProperty, value);
        }

        public static readonly DependencyProperty OuterThicknessProperty =
            DependencyProperty.Register(nameof(OuterThickness), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("6mm", FrameworkPropertyMetadataOptions.AffectsRender));

        public string OuterThickness
        {
            get => (string)GetValue(OuterThicknessProperty);
            set => SetValue(OuterThicknessProperty, value);
        }

        public static readonly DependencyProperty OuterTreatmentProperty =
            DependencyProperty.Register(nameof(OuterTreatment), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

        public string OuterTreatment
        {
            get => (string)GetValue(OuterTreatmentProperty);
            set => SetValue(OuterTreatmentProperty, value);
        }

        // ── Inner pane ────────────────────────────────────────────────

        public static readonly DependencyProperty InnerColorProperty =
            DependencyProperty.Register(nameof(InnerColor), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("Clear", FrameworkPropertyMetadataOptions.AffectsRender));

        public string InnerColor
        {
            get => (string)GetValue(InnerColorProperty);
            set => SetValue(InnerColorProperty, value);
        }

        public static readonly DependencyProperty InnerThicknessProperty =
            DependencyProperty.Register(nameof(InnerThickness), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("6mm", FrameworkPropertyMetadataOptions.AffectsRender));

        public string InnerThickness
        {
            get => (string)GetValue(InnerThicknessProperty);
            set => SetValue(InnerThicknessProperty, value);
        }

        public static readonly DependencyProperty InnerTreatmentProperty =
            DependencyProperty.Register(nameof(InnerTreatment), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

        public string InnerTreatment
        {
            get => (string)GetValue(InnerTreatmentProperty);
            set => SetValue(InnerTreatmentProperty, value);
        }

        // ── Spacer ────────────────────────────────────────────────────

        public static readonly DependencyProperty SpacerWidthProperty =
            DependencyProperty.Register(nameof(SpacerWidth), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("12mm", FrameworkPropertyMetadataOptions.AffectsRender));

        public string SpacerWidth
        {
            get => (string)GetValue(SpacerWidthProperty);
            set => SetValue(SpacerWidthProperty, value);
        }

        public static readonly DependencyProperty SpacerMaterialProperty =
            DependencyProperty.Register(nameof(SpacerMaterial), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

        public string SpacerMaterial
        {
            get => (string)GetValue(SpacerMaterialProperty);
            set => SetValue(SpacerMaterialProperty, value);
        }

        // ── Interlayer / PVB ──────────────────────────────────────────

        public static readonly DependencyProperty InterlayerColorProperty =
            DependencyProperty.Register(nameof(InterlayerColor), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("Clear", FrameworkPropertyMetadataOptions.AffectsRender));

        public string InterlayerColor
        {
            get => (string)GetValue(InterlayerColorProperty);
            set => SetValue(InterlayerColorProperty, value);
        }

        public static readonly DependencyProperty InterlayerThicknessProperty =
            DependencyProperty.Register(nameof(InterlayerThickness), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("1.52mm", FrameworkPropertyMetadataOptions.AffectsRender));

        public string InterlayerThickness
        {
            get => (string)GetValue(InterlayerThicknessProperty);
            set => SetValue(InterlayerThicknessProperty, value);
        }

        public static readonly DependencyProperty InterlayerTypeProperty =
            DependencyProperty.Register(nameof(InterlayerType), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("PVB", FrameworkPropertyMetadataOptions.AffectsRender));

        public string InterlayerType
        {
            get => (string)GetValue(InterlayerTypeProperty);
            set => SetValue(InterlayerTypeProperty, value);
        }

        // ── Laminated pair (DGU+LAM) ──────────────────────────────────

        public static readonly DependencyProperty LamOuterColorProperty =
            DependencyProperty.Register(nameof(LamOuterColor), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("Clear", FrameworkPropertyMetadataOptions.AffectsRender));

        public string LamOuterColor
        {
            get => (string)GetValue(LamOuterColorProperty);
            set => SetValue(LamOuterColorProperty, value);
        }

        public static readonly DependencyProperty LamOuterThicknessProperty =
            DependencyProperty.Register(nameof(LamOuterThickness), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("6mm", FrameworkPropertyMetadataOptions.AffectsRender));

        public string LamOuterThickness
        {
            get => (string)GetValue(LamOuterThicknessProperty);
            set => SetValue(LamOuterThicknessProperty, value);
        }

        public static readonly DependencyProperty LamInnerColorProperty =
            DependencyProperty.Register(nameof(LamInnerColor), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("Clear", FrameworkPropertyMetadataOptions.AffectsRender));

        public string LamInnerColor
        {
            get => (string)GetValue(LamInnerColorProperty);
            set => SetValue(LamInnerColorProperty, value);
        }

        public static readonly DependencyProperty LamInnerThicknessProperty =
            DependencyProperty.Register(nameof(LamInnerThickness), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("6mm", FrameworkPropertyMetadataOptions.AffectsRender));

        public string LamInnerThickness
        {
            get => (string)GetValue(LamInnerThicknessProperty);
            set => SetValue(LamInnerThicknessProperty, value);
        }

        public static readonly DependencyProperty CaptionProperty =
            DependencyProperty.Register(nameof(Caption), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

        public string Caption
        {
            get => (string)GetValue(CaptionProperty);
            set => SetValue(CaptionProperty, value);
        }

        // ── Render ────────────────────────────────────────────────────

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 40 || h < 40) return;

            var outline = ThemeColor("OutlineBrush", Color.FromRgb(0x94, 0xA3, 0xB8));
            var well = ThemeColor("MenuHoverBrush", Color.FromRgb(0xF1, 0xF5, 0xF9));
            var text = ThemeColor("PrimaryForegroundBrush", Color.FromRgb(0x0F, 0x17, 0x2A));
            var muted = ThemeColor("SecondaryForegroundBrush", Color.FromRgb(0x64, 0x74, 0x8B));
            var accent = ThemeColor("AccentBrush", Color.FromRgb(0x1D, 0x4E, 0xD8));

            var wellBrush = new SolidColorBrush(well);
            wellBrush.Freeze();
            dc.DrawRoundedRectangle(wellBrush, null, new Rect(0, 0, w, h), 8, 8);

            var wellStroke = new Pen(new SolidColorBrush(outline) { Opacity = 0.55 }, 1);
            wellStroke.Freeze();
            dc.DrawRoundedRectangle(null, wellStroke, new Rect(0.5, 0.5, w - 1, h - 1), 8, 8);

            switch (Kind)
            {
                case GlassAssemblyKind.Sgu:
                    DrawSgu(dc, w, h, text, muted, outline);
                    break;
                case GlassAssemblyKind.Dgu:
                    DrawDgu(dc, w, h, text, muted, outline, accent);
                    break;
                case GlassAssemblyKind.Lam:
                    DrawLam(dc, w, h, text, muted, outline);
                    break;
                case GlassAssemblyKind.DguLam:
                    DrawDguLam(dc, w, h, text, muted, outline, accent);
                    break;
            }
        }

        private void DrawSgu(DrawingContext dc, double w, double h, Color text, Color muted, Color outline)
        {
            double paneW = Math.Min(w * 0.72, 520);
            double paneD = Math.Min(h * 0.38, 88);
            double thick = VisualThickness(OuterThickness, 10, 22);
            var origin = new Point((w - paneW) * 0.5, h * 0.42);
            DrawSlab(dc, origin, paneW, paneD, thick, GlassTint(OuterColor), outline, exploded: false);
            DrawCaption(dc, w, h, BuildPaneLabel(OuterThickness, OuterColor, OuterTreatment), text);
        }

        private void DrawDgu(DrawingContext dc, double w, double h, Color text, Color muted, Color outline, Color accent)
        {
            double paneW = Math.Min(w * 0.62, 440);
            double paneD = Math.Min(h * 0.26, 64);
            double outerT = VisualThickness(OuterThickness, 8, 16);
            double innerT = VisualThickness(InnerThickness, 8, 16);
            double gap = Math.Clamp(ParseMm(SpacerWidth) * 0.7, 10, 22);

            double stack = outerT + gap + innerT + paneD;
            var origin = new Point((w - paneW) * 0.5, (h - stack) * 0.42 + paneD);

            // Back / inner first so outer sits on top.
            var innerOrigin = new Point(origin.X + 10, origin.Y + gap + outerT);
            DrawSlab(dc, innerOrigin, paneW - 8, paneD, innerT, GlassTint(InnerColor), outline, exploded: false);

            DrawSpacerChannel(dc, new Point(origin.X + 6, origin.Y + outerT + 2), paneW - 12, paneD * 0.55, gap - 4, outline, accent);

            DrawSlab(dc, origin, paneW, paneD, outerT, GlassTint(OuterColor), outline, exploded: false);

            DrawSideLabel(dc, origin.X - 8, origin.Y - 4, "OUTER", muted, rightAlign: true);
            DrawSideLabel(dc, origin.X - 8, origin.Y + outerT + gap * 0.3, SpacerWidth ?? "ASP", muted, rightAlign: true);
            DrawSideLabel(dc, origin.X - 8, innerOrigin.Y + innerT * 0.2, "INNER", muted, rightAlign: true);
            DrawCaption(dc, w, h, CaptionOrDefault(), text);
        }

        private void DrawLam(DrawingContext dc, double w, double h, Color text, Color muted, Color outline)
        {
            double paneW = Math.Min(w * 0.52, 380);
            double paneD = Math.Min(h * 0.22, 56);
            double outerT = VisualThickness(OuterThickness, 7, 14);
            double innerT = VisualThickness(InnerThickness, 7, 14);
            double filmT = Math.Clamp(ParseMm(InterlayerThickness) * 2.2, 5, 10);
            double explode = 16;

            double cx = (w - paneW) * 0.42;
            double topY = h * 0.28;

            DrawSlab(dc, new Point(cx + 28, topY + explode * 2 + outerT + filmT), paneW, paneD, innerT, GlassTint(InnerColor), outline, exploded: true);
            DrawSlab(dc, new Point(cx + 14, topY + explode + outerT), paneW, paneD, filmT, InterlayerTint(InterlayerColor, InterlayerType), outline, exploded: true, isFilm: true);
            DrawSlab(dc, new Point(cx, topY), paneW, paneD, outerT, GlassTint(OuterColor), outline, exploded: true);

            DrawSideLabel(dc, cx - 10, topY + 4, "OUTER", muted, rightAlign: true);
            DrawSideLabel(dc, cx + 4, topY + explode + outerT + 2, InterlayerLabel(), muted, rightAlign: true);
            DrawSideLabel(dc, cx + 18, topY + explode * 2 + outerT + filmT + 2, "INNER", muted, rightAlign: true);
            DrawCaption(dc, w, h, CaptionOrDefault(), text);
        }

        private void DrawDguLam(DrawingContext dc, double w, double h, Color text, Color muted, Color outline, Color accent)
        {
            double paneW = Math.Min(w * 0.48, 340);
            double paneD = Math.Min(h * 0.18, 48);
            double outerT = VisualThickness(OuterThickness, 6, 12);
            double lamOutT = VisualThickness(LamOuterThickness, 6, 12);
            double lamInT = VisualThickness(LamInnerThickness, 6, 12);
            double filmT = Math.Clamp(ParseMm(InterlayerThickness) * 1.8, 4, 8);
            double gap = Math.Clamp(ParseMm(SpacerWidth) * 0.55, 8, 16);
            double explode = 10;

            double cx = (w - paneW) * 0.38;
            double y = h * 0.22;

            // Bottom-most: lam inner
            DrawSlab(dc, new Point(cx + explode * 3, y + explode * 3 + outerT + gap + lamOutT + filmT),
                paneW, paneD, lamInT, GlassTint(LamInnerColor), outline, exploded: true);

            DrawSlab(dc, new Point(cx + explode * 2, y + explode * 2 + outerT + gap + lamOutT),
                paneW, paneD, filmT, InterlayerTint(InterlayerColor, InterlayerType), outline, exploded: true, isFilm: true);

            DrawSlab(dc, new Point(cx + explode, y + explode + outerT + gap),
                paneW, paneD, lamOutT, GlassTint(LamOuterColor), outline, exploded: true);

            DrawSpacerChannel(dc,
                new Point(cx + explode * 0.5, y + outerT + 2),
                paneW - 8, paneD * 0.5, gap - 2, outline, accent);

            DrawSlab(dc, new Point(cx, y), paneW, paneD, outerT, GlassTint(OuterColor), outline, exploded: true);

            DrawSideLabel(dc, cx - 8, y + 2, "1 OUTER", muted, rightAlign: true);
            DrawSideLabel(dc, cx - 8, y + outerT + 2, "2 ASP", muted, rightAlign: true);
            DrawSideLabel(dc, cx + explode - 8, y + explode + outerT + gap + 2, "3 LAM OUT", muted, rightAlign: true);
            DrawSideLabel(dc, cx + explode * 2 - 8, y + explode * 2 + outerT + gap + lamOutT, "4 " + InterlayerLabel(), muted, rightAlign: true);
            DrawSideLabel(dc, cx + explode * 3 - 8, y + explode * 3 + outerT + gap + lamOutT + filmT, "5 LAM IN", muted, rightAlign: true);
            DrawCaption(dc, w, h, CaptionOrDefault(), text);
        }

        private void DrawSlab(
            DrawingContext dc,
            Point origin,
            double width,
            double depth,
            double thick,
            Color glass,
            Color outline,
            bool exploded,
            bool isFilm = false)
        {
            // Cabinet-style isometric: top parallelogram, front face, right face.
            double skew = depth * 0.55;
            double drop = depth * 0.42;

            Point t0 = origin;                                      // top-front-left
            Point t1 = new(origin.X + width, origin.Y);             // top-front-right
            Point t2 = new(origin.X + width + skew, origin.Y - drop);
            Point t3 = new(origin.X + skew, origin.Y - drop);

            Point f0 = new(t0.X, t0.Y + thick);
            Point f1 = new(t1.X, t1.Y + thick);
            Point s1 = new(t2.X, t2.Y + thick);

            var top = Mix(glass, Colors.White, isFilm ? 0.08 : 0.22);
            var front = Mix(glass, Colors.Black, isFilm ? 0.12 : 0.18);
            var side = Mix(glass, Colors.Black, isFilm ? 0.20 : 0.30);

            DrawQuad(dc, t0, t1, t2, t3, top, outline, isFilm ? 0.92 : 0.78);
            DrawQuad(dc, t0, t1, f1, f0, front, outline, isFilm ? 0.88 : 0.72);
            DrawQuad(dc, t1, t2, s1, f1, side, outline, isFilm ? 0.86 : 0.70);

            // Specular stripe on the top face.
            if (!isFilm)
            {
                var hi = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
                hi.Freeze();
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(Lerp(t0, t3, 0.18), true, true);
                    ctx.LineTo(Lerp(t1, t2, 0.18), true, false);
                    ctx.LineTo(Lerp(t1, t2, 0.34), true, false);
                    ctx.LineTo(Lerp(t0, t3, 0.34), true, false);
                }
                geo.Freeze();
                dc.DrawGeometry(hi, null, geo);
            }

            // Leading edge highlight.
            var edge = new Pen(new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)), 1.1);
            edge.Freeze();
            dc.DrawLine(edge, t0, t1);
        }

        private void DrawSpacerChannel(
            DrawingContext dc, Point origin, double width, double depth, double thick, Color outline, Color accent)
        {
            var alu = ThemeManager.CurrentTheme == AppTheme.Dark
                ? Color.FromRgb(0x6B, 0x72, 0x80)
                : Color.FromRgb(0x9C, 0xA3, 0xAF);
            var cavity = Mix(accent, alu, 0.55);
            DrawSlab(dc, origin, width, depth, Math.Max(thick, 6), cavity, outline, exploded: false, isFilm: true);
        }

        private static void DrawQuad(
            DrawingContext dc, Point a, Point b, Point c, Point d, Color fill, Color outline, double opacity)
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

            fill.A = (byte)Math.Clamp((int)(opacity * 255), 40, 255);
            var brush = new SolidColorBrush(fill);
            brush.Freeze();
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(160, outline.R, outline.G, outline.B)), 0.8);
            pen.Freeze();
            dc.DrawGeometry(brush, pen, geo);
        }

        private void DrawCaption(DrawingContext dc, double w, double h, string text, Color color)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var ft = Format(text, 11, FontWeights.SemiBold, color);
            double x = Math.Max(12, (w - ft.Width) * 0.5);
            double y = h - ft.Height - 10;
            dc.DrawText(ft, new Point(x, y));
        }

        private void DrawSideLabel(DrawingContext dc, double x, double y, string text, Color color, bool rightAlign)
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
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
                size,
                brush,
                1.25);
        }

        private string CaptionOrDefault()
        {
            if (!string.IsNullOrWhiteSpace(Caption)) return Caption;
            return Kind switch
            {
                GlassAssemblyKind.Sgu => BuildPaneLabel(OuterThickness, OuterColor, OuterTreatment),
                GlassAssemblyKind.Dgu =>
                    $"{BuildPaneLabel(OuterThickness, OuterColor, OuterTreatment)} + {SpacerWidth} ASP + {BuildPaneLabel(InnerThickness, InnerColor, InnerTreatment)}",
                GlassAssemblyKind.Lam =>
                    $"{BuildPaneLabel(OuterThickness, OuterColor, OuterTreatment)} + {InterlayerThickness} {InterlayerColor} {InterlayerType} + {BuildPaneLabel(InnerThickness, InnerColor, InnerTreatment)}",
                _ => Caption ?? ""
            };
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
            // Clear / extra-clear / default — slightly cyan glass.
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            return dark
                ? Color.FromArgb(195, 140, 188, 210)
                : Color.FromArgb(200, 176, 214, 226);
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
            // PVB — safety amber.
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
                ? v
                : 0;
        }

        private static double VisualThickness(string? raw, double minPx, double maxPx)
        {
            double mm = ParseMm(raw);
            if (mm <= 0) return (minPx + maxPx) * 0.5;
            return Math.Clamp(4 + mm * 1.15, minPx, maxPx);
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
