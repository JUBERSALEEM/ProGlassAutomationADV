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

        public static readonly DependencyProperty OuterPolishProperty =
            DependencyProperty.Register(nameof(OuterPolish), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
        public string OuterPolish { get => (string)GetValue(OuterPolishProperty); set => SetValue(OuterPolishProperty, value); }

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

        public static readonly DependencyProperty InnerPolishProperty =
            DependencyProperty.Register(nameof(InnerPolish), typeof(string), typeof(GlassAssemblyShowcase),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
        public string InnerPolish { get => (string)GetValue(InnerPolishProperty); set => SetValue(InnerPolishProperty, value); }

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

            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var outline = ThemeColor("OutlineBrush", Color.FromRgb(0x94, 0xA3, 0xB8));
            var well = ThemeColor("MenuHoverBrush", Color.FromRgb(0xEE, 0xF2, 0xF7));
            var text = ThemeColor("PrimaryForegroundBrush", Color.FromRgb(0x0F, 0x17, 0x2A));
            var muted = ThemeColor("SecondaryForegroundBrush", Color.FromRgb(0x64, 0x74, 0x8B));
            var accent = ThemeColor("AccentBrush", Color.FromRgb(0x1D, 0x4E, 0xD8));

            // ── AutoCAD-style background ──────────────────────────────
            if (ShowWell)
            {
                // Blueprint grid background
                Color gridBg = dark ? Color.FromRgb(0x1A, 0x23, 0x32) : Color.FromRgb(0xF8, 0xFA, 0xFC);
                Color gridLine = dark ? Color.FromArgb(30, 100, 160, 220) : Color.FromArgb(25, 50, 100, 180);
                Color gridMajor = dark ? Color.FromArgb(50, 100, 160, 220) : Color.FromArgb(40, 50, 100, 180);

                var bgBrush = new SolidColorBrush(gridBg);
                bgBrush.Freeze();
                dc.DrawRoundedRectangle(bgBrush, null, new Rect(0, 0, w, h), 8, 8);

                // Minor grid (20px)
                var minorPen = new Pen(new SolidColorBrush(gridLine), 0.3);
                minorPen.Freeze();
                for (double x = 20; x < w; x += 20)
                    dc.DrawLine(minorPen, new Point(x, 0), new Point(x, h));
                for (double y = 20; y < h; y += 20)
                    dc.DrawLine(minorPen, new Point(0, y), new Point(w, y));

                // Major grid (100px)
                var majorPen = new Pen(new SolidColorBrush(gridMajor), 0.6);
                majorPen.Freeze();
                for (double x = 100; x < w; x += 100)
                    dc.DrawLine(majorPen, new Point(x, 0), new Point(x, h));
                for (double y = 100; y < h; y += 100)
                    dc.DrawLine(majorPen, new Point(0, y), new Point(w, y));

                // Border frame
                var framePen = new Pen(new SolidColorBrush(outline), 1.2);
                framePen.Freeze();
                dc.DrawRoundedRectangle(null, framePen, new Rect(2, 2, w - 4, h - 4), 6, 6);
            }

            // ── Title block ────────────────────────────────────────────
            DrawTitleBlock(dc, w, h, text, muted, outline, dark);

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

        // ── AutoCAD Title Block ─────────────────────────────────────────
        private void DrawTitleBlock(DrawingContext dc, double w, double h, Color text, Color muted, Color outline, bool dark)
        {
            double tbH = 28;
            double tbY = h - tbH - 4;
            var tbBg = dark ? Color.FromArgb(180, 26, 35, 50) : Color.FromArgb(200, 241, 245, 249);
            var brush = new SolidColorBrush(tbBg);
            brush.Freeze();
            var pen = new Pen(new SolidColorBrush(outline), 0.8);
            pen.Freeze();
            dc.DrawRectangle(brush, pen, new Rect(4, tbY, w - 8, tbH));

            string kindLabel = Kind switch
            {
                GlassAssemblyKind.Sgu => "SGU — SINGLE GLAZING UNIT",
                GlassAssemblyKind.Dgu => "DGU — DOUBLE GLAZING UNIT (IGU)",
                GlassAssemblyKind.Lam => "LAM — LAMINATED GLASS",
                GlassAssemblyKind.DguLam => "DGU+LAM — IGU WITH LAMINATED INNER",
                _ => "GLASS ASSEMBLY"
            };

            var ft = Format(kindLabel, 9, FontWeights.Bold, text);
            dc.DrawText(ft, new Point(12, tbY + 4));

            string scale = "SCALE: NTS   |   ISOMETRIC VIEW   |   ALL DIMS IN mm";
            var ft2 = Format(scale, 8, FontWeights.Normal, muted);
            dc.DrawText(ft2, new Point(12, tbY + 16));
        }

        // ── Dimension line with arrows and label ────────────────────────
        private static void DrawDimensionLine(DrawingContext dc, Point start, Point end, string label, Color color, bool horizontal = true)
        {
            var pen = new Pen(new SolidColorBrush(color), 0.8);
            pen.Freeze();

            // Main line
            dc.DrawLine(pen, start, end);

            // Arrow heads
            double arrowSize = 4;
            if (horizontal)
            {
                // Left arrow
                dc.DrawLine(pen, start, new Point(start.X + arrowSize, start.Y - arrowSize * 0.6));
                dc.DrawLine(pen, start, new Point(start.X + arrowSize, start.Y + arrowSize * 0.6));
                // Right arrow
                dc.DrawLine(pen, end, new Point(end.X - arrowSize, end.Y - arrowSize * 0.6));
                dc.DrawLine(pen, end, new Point(end.X - arrowSize, end.Y + arrowSize * 0.6));
            }
            else
            {
                // Top arrow
                dc.DrawLine(pen, start, new Point(start.X - arrowSize * 0.6, start.Y + arrowSize));
                dc.DrawLine(pen, start, new Point(start.X + arrowSize * 0.6, start.Y + arrowSize));
                // Bottom arrow
                dc.DrawLine(pen, end, new Point(end.X - arrowSize * 0.6, end.Y - arrowSize));
                dc.DrawLine(pen, end, new Point(end.X + arrowSize * 0.6, end.Y - arrowSize));
            }

            // Label centered on line
            var ft = Format(label, 8, FontWeights.SemiBold, color);
            double midX = (start.X + end.X) * 0.5;
            double midY = (start.Y + end.Y) * 0.5;
            dc.DrawText(ft, new Point(midX - ft.Width * 0.5, midY - ft.Height - 2));
        }

        // ── Leader line with annotation ─────────────────────────────────
        private static void DrawLeaderAnnotation(DrawingContext dc, Point from, Point to, string label, Color color)
        {
            var pen = new Pen(new SolidColorBrush(color), 0.7);
            pen.Freeze();

            // Leader line
            dc.DrawLine(pen, from, to);

            // Dot at start
            var dotBrush = new SolidColorBrush(color);
            dotBrush.Freeze();
            dc.DrawEllipse(dotBrush, null, from, 2.5, 2.5);

            // Label at end
            var ft = Format(label, 8, FontWeights.SemiBold, color);
            double textX = to.X > from.X ? to.X + 4 : to.X - ft.Width - 4;
            dc.DrawText(ft, new Point(textX, to.Y - ft.Height * 0.5));
        }

        // ── Cross-hatching for glass sections ───────────────────────────
        private static void DrawCrossHatch(DrawingContext dc, Point a, Point b, Point c, Point d, Color color, double spacing = 4)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(60, color.R, color.G, color.B)), 0.4);
            pen.Freeze();

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                // Diagonal lines across the quad
                double minX = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
                double maxX = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
                double minY = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
                double maxY = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));

                bool first = true;
                for (double offset = minX + minY; offset < maxX + maxY; offset += spacing)
                {
                    double x1 = Math.Max(minX, offset - maxY);
                    double y1 = offset - x1;
                    double x2 = Math.Min(maxX, offset - minY);
                    double y2 = offset - x2;

                    if (y1 > maxY) { y1 = maxY; x1 = offset - y1; }
                    if (y1 < minY) { y1 = minY; x1 = offset - y1; }
                    if (y2 > maxY) { y2 = maxY; x2 = offset - y2; }
                    if (y2 < minY) { y2 = minY; x2 = offset - y2; }

                    if (x1 >= minX && x1 <= maxX && x2 >= minX && x2 <= maxX)
                    {
                        if (first) { ctx.BeginFigure(new Point(x1, y1), false, false); first = false; }
                        ctx.LineTo(new Point(x2, y2), true, false);
                        ctx.BeginFigure(new Point(x1, y1), false, false);
                    }
                }
            }
            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }

        // Wide hero pane — matches the SGU mockup sheet.
        private void DrawSguSheet(DrawingContext dc, double w, double h, Color text, Color outline)
        {
            double paneW = w * 0.82;
            double depth = Math.Min(h * 0.38, 100);
            double thick = VisualThickness(OuterThickness, 14, 28);
            double left = (w - paneW) * 0.5;
            double frontY = h * 0.50;

            DrawWidePane(dc, left, frontY, paneW, depth, thick, GlassTint(OuterColor), outline, polish: OuterPolish);

            // AutoCAD dimension lines
            Color dimColor = ThemeManager.CurrentTheme == AppTheme.Dark
                ? Color.FromRgb(0x60, 0xA5, 0xFA) : Color.FromRgb(0x25, 0x63, 0xEB);

            // Width dimension
            DrawDimensionLine(dc,
                new Point(left, frontY + thick + 20),
                new Point(left + paneW, frontY + thick + 20),
                "WIDTH", dimColor, horizontal: true);

            // Thickness dimension (vertical)
            DrawDimensionLine(dc,
                new Point(left - 20, frontY),
                new Point(left - 20, frontY + thick),
                OuterThickness ?? "6mm", dimColor, horizontal: false);

            // Leader annotations
            DrawLeaderAnnotation(dc,
                new Point(left + paneW * 0.5, frontY - depth * 0.3),
                new Point(left + paneW * 0.5 + 40, frontY - depth * 0.3 - 20),
                BuildPaneLabel(OuterThickness, OuterColor, OuterTreatment), dimColor);

            if (ShowCaption && !string.IsNullOrWhiteSpace(Caption))
            {
                var ft = Format(Caption, 13, FontWeights.SemiBold, text);
                dc.DrawText(ft, new Point((w - ft.Width) * 0.5, frontY - depth * 0.38 - ft.Height * 0.5));
            }
        }

        // Packed IGU — two lites + visible air cavity, isometric product shot.
        private void DrawDguSandwich(DrawingContext dc, double w, double h, Color muted, Color outline, Color accent)
        {
            double paneW = Math.Min(w * 0.74, 480);
            double depth = Math.Min(h * 0.32, 88);
            double outerT = VisualThickness(OuterThickness, 12, 22);
            double innerT = VisualThickness(InnerThickness, 12, 22);
            
            // Proportional spacer width scaling (no harsh clamps)
            double spacerMm = ParseMm(SpacerWidth);
            double gap = Math.Max(8, spacerMm * 2.0); // 2px per mm, minimum 8px
            
            double left = (w - paneW) * 0.52;
            double y = h * 0.30;

            // Draw layers from back to front
            DrawWidePane(dc, left + 22, y + gap + outerT + 6, paneW - 10, depth, innerT, GlassTint(InnerColor), outline, polish: InnerPolish);
            DrawSpacerBar(dc, left + 10, y + outerT + 4, paneW - 8, depth * 0.62, gap - 2, outline, accent);
            DrawWidePane(dc, left, y, paneW, depth, outerT, GlassTint(OuterColor), outline, polish: OuterPolish);

            // AutoCAD dimension lines
            Color dimColor = ThemeManager.CurrentTheme == AppTheme.Dark
                ? Color.FromRgb(0x60, 0xA5, 0xFA) : Color.FromRgb(0x25, 0x63, 0xEB);

            // Total thickness dimension (vertical)
            double totalThick = outerT + gap + innerT;
            DrawDimensionLine(dc,
                new Point(left - 24, y),
                new Point(left - 24, y + totalThick + 6),
                $"{ParseMm(OuterThickness) + spacerMm + ParseMm(InnerThickness):0.#}mm", dimColor, horizontal: false);

            // Leader annotations for each layer
            DrawLeaderAnnotation(dc,
                new Point(left + 10, y + outerT * 0.5),
                new Point(left - 30, y - 10),
                $"OUTER: {BuildPaneLabel(OuterThickness, OuterColor, OuterTreatment)}", dimColor);

            DrawLeaderAnnotation(dc,
                new Point(left + paneW * 0.5, y + outerT + gap * 0.5),
                new Point(left + paneW + 20, y + outerT + gap * 0.5),
                $"SPACER: {SpacerWidth} AL", dimColor);

            DrawLeaderAnnotation(dc,
                new Point(left + 30, y + gap + outerT + 6 + innerT * 0.5),
                new Point(left - 30, y + totalThick + 30),
                $"INNER: {BuildPaneLabel(InnerThickness, InnerColor, InnerTreatment)}", dimColor);
        }

        // Exploded LAM — three distinct panes with a large air gap, amber interlayer.
        private void DrawLamExploded(DrawingContext dc, double w, double h, Color muted, Color outline)
        {
            double paneW = Math.Min(w * 0.68, 360);
            double depth = Math.Min(h * 0.20, 66);
            double outerT = VisualThickness(OuterThickness, 11, 18);
            double innerT = VisualThickness(InnerThickness, 11, 18);
            
            // Proportional interlayer thickness (more visible)
            double interlayerMm = ParseMm(InterlayerThickness);
            double filmT = Math.Max(6, interlayerMm * 5.0); // 5px per mm for visibility, minimum 6px
            
            double explode = Math.Max(h * 0.18, 32);
            double cx = (w - paneW) * 0.28;
            double top = h * 0.10;

            // Draw layers from back to front
            DrawWidePane(dc, cx + explode * 0.55, top + explode * 2 + 8, paneW, depth, innerT, GlassTint(InnerColor), outline, polish: InnerPolish);
            DrawInterlayer(dc, cx + explode * 0.28, top + explode + 4, paneW, depth, filmT,
                InterlayerTint(InterlayerColor, InterlayerType), outline, InterlayerType);
            DrawWidePane(dc, cx, top, paneW, depth, outerT, GlassTint(OuterColor), outline, polish: OuterPolish);

            // AutoCAD dimension lines
            Color dimColor = ThemeManager.CurrentTheme == AppTheme.Dark
                ? Color.FromRgb(0x60, 0xA5, 0xFA) : Color.FromRgb(0x25, 0x63, 0xEB);

            // Leader annotations for each layer
            DrawLeaderAnnotation(dc,
                new Point(cx + 10, top + outerT * 0.5),
                new Point(cx - 40, top - 10),
                $"OUTER: {BuildPaneLabel(OuterThickness, OuterColor, OuterTreatment)}", dimColor);

            DrawLeaderAnnotation(dc,
                new Point(cx + explode * 0.28 + 10, top + explode + 4 + filmT * 0.5),
                new Point(cx - 40, top + explode),
                InterlayerLabel(), dimColor);

            DrawLeaderAnnotation(dc,
                new Point(cx + explode * 0.55 + 10, top + explode * 2 + 8 + innerT * 0.5),
                new Point(cx - 40, top + explode * 2 + 20),
                $"INNER: {BuildPaneLabel(InnerThickness, InnerColor, InnerTreatment)}", dimColor);

            // Total assembly thickness
            double totalY = top + explode * 2 + 8 + innerT;
            DrawDimensionLine(dc,
                new Point(cx + paneW + 20, top),
                new Point(cx + paneW + 20, totalY),
                $"{ParseMm(OuterThickness) + interlayerMm + ParseMm(InnerThickness):0.##}mm", dimColor, horizontal: false);
        }

        private static void DrawInterlayer(
            DrawingContext dc, double left, double frontY, double width, double depth, double thick, Color interlayer, Color outline, string type)
        {
            // Professional PVB/SGP interlayer rendering
            double skew = depth * 0.62;
            double rise = depth * 0.48;

            Point fl = new(left, frontY);
            Point fr = new(left + width, frontY);
            Point br = new(left + width + skew, frontY - rise);
            Point bl = new(left + skew, frontY - rise);

            Point flb = new(fl.X, fl.Y + thick);
            Point frb = new(fr.X, fr.Y + thick);
            Point brb = new(br.X, br.Y + thick);

            // Interlayer is translucent with proper amber/tint color
            Color topColor = Color.FromArgb(180, interlayer.R, interlayer.G, interlayer.B);
            Color frontColor = Color.FromArgb(200, interlayer.R, interlayer.G, interlayer.B);
            Color sideColor = Color.FromArgb(210, interlayer.R, interlayer.G, interlayer.B);

            var topBrush = new SolidColorBrush(topColor);
            topBrush.Freeze();
            var frontBrush = new SolidColorBrush(frontColor);
            frontBrush.Freeze();
            var sideBrush = new SolidColorBrush(sideColor);
            sideBrush.Freeze();

            var pen = new Pen(new SolidColorBrush(Color.FromArgb(150, outline.R, outline.G, outline.B)), 0.8);
            pen.Freeze();

            // Draw three faces
            dc.DrawGeometry(topBrush, pen, Quad(fl, fr, br, bl));
            dc.DrawGeometry(frontBrush, pen, Quad(fl, fr, frb, flb));
            dc.DrawGeometry(sideBrush, pen, Quad(fr, br, brb, frb));

            // Add subtle texture/pattern to indicate it's an interlayer
            var texturePen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 0.5);
            texturePen.Freeze();
            
            // Horizontal lines on front face to show it's a film
            for (double y = flb.Y + 2; y < fl.Y - 2; y += 3)
            {
                double t = (y - flb.Y) / (fl.Y - flb.Y);
                Point lineStart = new(flb.X, y);
                Point lineEnd = new(frb.X, y);
                dc.DrawLine(texturePen, lineStart, lineEnd);
            }

            // Add type label if there's room
            if (width > 100)
            {
                bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
                var text = Format(type.ToUpperInvariant(), 7, FontWeights.Bold, 
                    dark ? Color.FromRgb(255, 240, 200) : Color.FromRgb(120, 80, 20));
                double textX = left + width * 0.5 - text.Width * 0.5;
                double textY = frontY - rise * 0.5 - text.Height * 0.5;
                dc.DrawText(text, new Point(textX, textY));
            }
        }

        // Standing IGU cutaway: outer lite + cavity + laminated pair, all vertical.
        private void DrawDguLamCutaway(DrawingContext dc, double w, double h, Color muted, Color outline, Color accent)
        {
            double baseY = h * 0.85;
            double liteH = h * 0.68;
            double x = w * 0.10;
            double tOuter = Math.Clamp(w * 0.07, 14, 26);
            
            // Proportional spacer width
            double spacerMm = ParseMm(SpacerWidth);
            double cavity = Math.Max(10, spacerMm * 2.0);
            
            double tLamOut = Math.Clamp(w * 0.055, 10, 18);
            
            // Proportional interlayer thickness
            double interlayerMm = ParseMm(InterlayerThickness);
            double tFilm = Math.Max(4, interlayerMm * 4.0);
            
            double tLamIn = Math.Clamp(w * 0.055, 10, 18);

            DrawStandingLite(dc, x, baseY, tOuter, liteH, GlassTint(OuterColor), outline);

            // Draw cavity with metallic spacer appearance
            var cavityFill = Mix(accent, Color.FromRgb(0x9C, 0xA3, 0xAF), 0.50);
            cavityFill.A = 70;
            double cavX = x + tOuter + 3;
            
            // Metallic gradient for cavity/spacer
            var cavityGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            if (dark)
            {
                cavityGradient.GradientStops.Add(new GradientStop(Color.FromArgb(180, 80, 85, 95), 0));
                cavityGradient.GradientStops.Add(new GradientStop(Color.FromArgb(200, 120, 125, 135), 0.5));
                cavityGradient.GradientStops.Add(new GradientStop(Color.FromArgb(180, 80, 85, 95), 1));
            }
            else
            {
                cavityGradient.GradientStops.Add(new GradientStop(Color.FromArgb(180, 130, 135, 145), 0));
                cavityGradient.GradientStops.Add(new GradientStop(Color.FromArgb(200, 170, 175, 185), 0.5));
                cavityGradient.GradientStops.Add(new GradientStop(Color.FromArgb(180, 130, 135, 145), 1));
            }
            cavityGradient.Freeze();
            
            dc.DrawRectangle(cavityGradient,
                new Pen(new SolidColorBrush(outline), 0.8),
                new Rect(cavX, baseY - liteH * 0.94, cavity, liteH * 0.90));

            double lx = cavX + cavity + 4;
            DrawStandingLite(dc, lx, baseY - 4, tLamOut, liteH * 0.92, GlassTint(LamOuterColor), outline);
            
            // Draw interlayer with proper visibility
            DrawStandingInterlayer(dc, lx + tLamOut + 2, baseY, tFilm, liteH * 0.88,
                InterlayerTint(InterlayerColor, InterlayerType), outline, InterlayerType);
            
            DrawStandingLite(dc, lx + tLamOut + tFilm + 4, baseY + 4, tLamIn, liteH * 0.84,
                GlassTint(LamInnerColor), outline);

            // AutoCAD dimension lines
            Color dimColor = ThemeManager.CurrentTheme == AppTheme.Dark
                ? Color.FromRgb(0x60, 0xA5, 0xFA) : Color.FromRgb(0x25, 0x63, 0xEB);

            // Total assembly width dimension (horizontal)
            double totalWidth = tOuter + 3 + cavity + 4 + tLamOut + 2 + tFilm + 4 + tLamIn;
            DrawDimensionLine(dc,
                new Point(x, baseY + 16),
                new Point(x + totalWidth, baseY + 16),
                $"{ParseMm(OuterThickness) + spacerMm + ParseMm(LamOuterThickness) + interlayerMm + ParseMm(LamInnerThickness):0.#}mm", dimColor, horizontal: true);

            // Leader annotations for each layer
            DrawLeaderAnnotation(dc,
                new Point(x + tOuter * 0.5, baseY - liteH * 0.5),
                new Point(x - 30, baseY - liteH * 0.7),
                $"OUTER: {BuildPaneLabel(OuterThickness, OuterColor, OuterTreatment)}", dimColor);

            DrawLeaderAnnotation(dc,
                new Point(cavX + cavity * 0.5, baseY - liteH * 0.5),
                new Point(cavX + cavity * 0.5, baseY - liteH - 10),
                $"SPACER: {SpacerWidth} AL", dimColor);

            DrawLeaderAnnotation(dc,
                new Point(lx + tLamOut * 0.5, baseY - liteH * 0.45),
                new Point(lx - 30, baseY - liteH * 0.3),
                $"LAM OUTER: {LamOuterThickness} {LamOuterColor}", dimColor);

            DrawLeaderAnnotation(dc,
                new Point(lx + tLamOut + 2 + tFilm * 0.5, baseY - liteH * 0.42),
                new Point(lx + tLamOut + tFilm + 20, baseY - liteH * 0.6),
                InterlayerLabel(), dimColor);

            DrawLeaderAnnotation(dc,
                new Point(lx + tLamOut + tFilm + 4 + tLamIn * 0.5, baseY - liteH * 0.40),
                new Point(lx + totalWidth + 20, baseY - liteH * 0.4),
                $"LAM INNER: {LamInnerThickness} {LamInnerColor}", dimColor);
        }

        private static void DrawStandingInterlayer(
            DrawingContext dc, double x, double baseY, double thickness, double height, Color interlayer, Color outline, string type)
        {
            // Professional standing interlayer for DGU+LAM cutaway
            Point bl = new(x, baseY);
            Point br = new(x + thickness, baseY - 8);
            Point tr = new(x + thickness + 10, baseY - height);
            Point tl = new(x + 8, baseY - height + 10);

            Color fillColor = Color.FromArgb(190, interlayer.R, interlayer.G, interlayer.B);
            DrawQuad(dc, tl, tr, br, bl, fillColor, outline, 0.85);

            // Add texture lines to show it's an interlayer
            var texturePen = new Pen(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 0.4);
            texturePen.Freeze();
            
            for (double y = bl.Y; y > tl.Y; y -= 4)
            {
                double t = (bl.Y - y) / (bl.Y - tl.Y);
                Point lineStart = Lerp(bl, tl, t);
                Point lineEnd = Lerp(br, tr, t);
                dc.DrawLine(texturePen, lineStart, lineEnd);
            }

            // Add type label if there's room
            if (height > 80)
            {
                bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
                var text = Format(type.ToUpperInvariant(), 6, FontWeights.Bold, 
                    dark ? Color.FromRgb(255, 240, 200) : Color.FromRgb(120, 80, 20));
                double textX = x + thickness * 0.5 - text.Width * 0.5 + 5;
                double textY = baseY - height * 0.5;
                dc.DrawText(text, new Point(textX, textY));
            }
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
            DrawingContext dc, double left, double frontY, double width, double depth, double thick, Color glass, Color outline, bool film = false, string polish = "")
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

                // Draw polish indicators on edges if polish is specified
                if (!string.IsNullOrWhiteSpace(polish))
                {
                    DrawPolishIndicator(dc, fl, fr, frb, flb, polish, outline);
                }
            }
        }

        private static void DrawPolishIndicator(
            DrawingContext dc, Point fl, Point fr, Point frb, Point flb, string polish, Color outline)
        {
            // Polish types: Flat Polish, Pencil Polish, Bevel Polish
            string polishLower = polish.ToLowerInvariant();
            bool isFlat = polishLower.Contains("flat");
            bool isPencil = polishLower.Contains("pencil") || polishLower.Contains("round");
            bool isBevel = polishLower.Contains("bevel");

            if (!isFlat && !isPencil && !isBevel) return;

            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            Color polishColor = dark ? Color.FromRgb(180, 220, 255) : Color.FromRgb(100, 160, 220);
            var polishPen = new Pen(new SolidColorBrush(polishColor), 2.0);
            polishPen.Freeze();

            // Draw polish indicator on the front edge (bottom edge of front face)
            if (isFlat)
            {
                // Flat polish: straight line with small squares at corners
                dc.DrawLine(polishPen, flb, frb);
                
                // Corner indicators
                var cornerBrush = new SolidColorBrush(polishColor);
                cornerBrush.Freeze();
                dc.DrawRectangle(cornerBrush, null, new Rect(flb.X - 2, flb.Y - 2, 4, 4));
                dc.DrawRectangle(cornerBrush, null, new Rect(frb.X - 2, frb.Y - 2, 4, 4));
            }
            else if (isPencil)
            {
                // Pencil polish: rounded edge (draw arc at corners)
                var arcGeo = new StreamGeometry();
                using (var ctx = arcGeo.Open())
                {
                    ctx.BeginFigure(flb, false, false);
                    ctx.LineTo(frb, true, false);
                }
                arcGeo.Freeze();
                dc.DrawGeometry(null, polishPen, arcGeo);

                // Rounded corners
                dc.DrawEllipse(null, polishPen, flb, 3, 3);
                dc.DrawEllipse(null, polishPen, frb, 3, 3);
            }
            else if (isBevel)
            {
                // Bevel polish: angled edge
                double bevelSize = 3;
                Point bevelFl = new(flb.X, flb.Y - bevelSize);
                Point bevelFr = new(frb.X, frb.Y - bevelSize);
                
                var bevelGeo = new StreamGeometry();
                using (var ctx = bevelGeo.Open())
                {
                    ctx.BeginFigure(flb, true, true);
                    ctx.LineTo(bevelFl, true, false);
                    ctx.LineTo(bevelFr, true, false);
                    ctx.LineTo(frb, true, false);
                }
                bevelGeo.Freeze();

                var bevelBrush = new SolidColorBrush(Color.FromArgb(80, polishColor.R, polishColor.G, polishColor.B));
                bevelBrush.Freeze();
                dc.DrawGeometry(bevelBrush, polishPen, bevelGeo);
            }

            // Add "POLISH" label if there's room
            double edgeWidth = Math.Abs(frb.X - flb.X);
            if (edgeWidth > 80)
            {
                var text = Format(polish.ToUpperInvariant(), 6, FontWeights.SemiBold, polishColor);
                double textX = (flb.X + frb.X) * 0.5 - text.Width * 0.5;
                double textY = flb.Y + 4;
                dc.DrawText(text, new Point(textX, textY));
            }
        }

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
