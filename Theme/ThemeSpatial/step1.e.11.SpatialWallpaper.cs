using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Theming;

namespace ProGlassAutomation.ThemeSpatial;

/// <summary>
/// Far-plane depth field. Receding architectural planes, not Glass blooms, not Liquid sky, not PNG.
/// Opaque bands; depth comes from plane scale, luminance, and vanishing edges — not transparency.
/// </summary>
public static class SpatialWallpaper
{
    private const double Width = 800;
    private const double Height = 500;

    public static void Install(ResourceDictionary package, Appearance appearance)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (!Enum.IsDefined(appearance)) throw new ArgumentOutOfRangeException(nameof(appearance));
        if (!Replace(package, "SpatialCanvasBrush", Create(appearance)))
            throw new InvalidOperationException("SpatialCanvasBrush must exist in Spatial extras.");
    }

    public static DrawingBrush Create(Appearance appearance)
    {
        var group = new DrawingGroup();
        if (appearance == Appearance.Dark) PaintDark(group);
        else PaintLight(group);
        group.Freeze();
        return new DrawingBrush(group)
        {
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center,
            TileMode = TileMode.None
        };
    }

    private static void PaintLight(DrawingGroup group)
    {
        Fill(group, Wash(Color.FromRgb(206, 218, 232), Color.FromRgb(184, 200, 218), Color.FromRgb(154, 174, 196), Color.FromRgb(122, 146, 170)));
        RectBand(group, 0, 96, Color.FromRgb(214, 224, 236));
        RectBand(group, 96, 84, Color.FromRgb(190, 204, 220));
        Plane(group,
            P(0, 0), P(168, 0), P(220, 180), P(0, 180),
            Color.FromRgb(176, 192, 210), Color.FromRgb(120, 140, 162));
        Plane(group,
            P(632, 0), P(800, 0), P(800, 180), P(580, 180),
            Color.FromRgb(176, 192, 210), Color.FromRgb(120, 140, 162));
        Plane(group,
            P(0, 180), P(220, 180), P(160, 236), P(70, 328), P(0, 430),
            Color.FromRgb(132, 152, 174), Color.FromRgb(96, 118, 142));
        Plane(group,
            P(580, 180), P(800, 180), P(800, 430), P(730, 328), P(640, 236),
            Color.FromRgb(132, 152, 174), Color.FromRgb(96, 118, 142));
        Plane(group,
            P(220, 180), P(580, 180), P(640, 236), P(160, 236),
            Color.FromRgb(168, 184, 204), Color.FromRgb(120, 140, 162));
        Plane(group,
            P(160, 236), P(640, 236), P(730, 328), P(70, 328),
            Color.FromRgb(148, 168, 190), Color.FromRgb(104, 126, 150));
        Plane(group,
            P(70, 328), P(730, 328), P(800, 430), P(0, 430),
            Color.FromRgb(122, 146, 170), Color.FromRgb(82, 106, 132));
        Plane(group,
            P(0, 430), P(800, 430), P(800, 500), P(0, 500),
            Color.FromRgb(94, 120, 146), Color.FromRgb(70, 94, 118));
        Ceiling(group, 400, 88, 260, 64, Color.FromArgb(64, 255, 255, 255), Color.FromArgb(0, 206, 218, 232));
    }

    private static void PaintDark(DrawingGroup group)
    {
        Fill(group, Wash(Color.FromRgb(7, 11, 20), Color.FromRgb(10, 18, 32), Color.FromRgb(16, 28, 48), Color.FromRgb(8, 12, 20)));
        RectBand(group, 0, 96, Color.FromRgb(6, 10, 18));
        RectBand(group, 96, 84, Color.FromRgb(10, 16, 28));
        Plane(group,
            P(0, 0), P(168, 0), P(220, 180), P(0, 180),
            Color.FromRgb(14, 22, 36), Color.FromRgb(8, 12, 22));
        Plane(group,
            P(632, 0), P(800, 0), P(800, 180), P(580, 180),
            Color.FromRgb(14, 22, 36), Color.FromRgb(8, 12, 22));
        Plane(group,
            P(0, 180), P(220, 180), P(160, 236), P(70, 328), P(0, 430),
            Color.FromRgb(12, 20, 34), Color.FromRgb(6, 10, 18));
        Plane(group,
            P(580, 180), P(800, 180), P(800, 430), P(730, 328), P(640, 236),
            Color.FromRgb(12, 20, 34), Color.FromRgb(6, 10, 18));
        Plane(group,
            P(220, 180), P(580, 180), P(640, 236), P(160, 236),
            Color.FromRgb(16, 26, 42), Color.FromRgb(8, 14, 24));
        Plane(group,
            P(160, 236), P(640, 236), P(730, 328), P(70, 328),
            Color.FromRgb(20, 32, 50), Color.FromRgb(10, 18, 30));
        Plane(group,
            P(70, 328), P(730, 328), P(800, 430), P(0, 430),
            Color.FromRgb(26, 40, 62), Color.FromRgb(12, 20, 34));
        Plane(group,
            P(0, 430), P(800, 430), P(800, 500), P(0, 500),
            Color.FromRgb(32, 48, 72), Color.FromRgb(14, 22, 36));
        Ceiling(group, 400, 88, 260, 64, Color.FromArgb(50, 96, 140, 196), Color.FromArgb(0, 10, 18, 32));
    }

    private static Point P(double x, double y)
    {
        if (x < 0 || y < 0 || x > Width || y > Height)
            throw new InvalidOperationException("Spatial plane vertex must sit inside the drawing.");
        return new Point(x, y);
    }

    private static LinearGradientBrush Wash(Color top, Color high, Color mid, Color deep)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        brush.GradientStops.Add(new GradientStop(top, 0));
        brush.GradientStops.Add(new GradientStop(high, 0.32));
        brush.GradientStops.Add(new GradientStop(mid, 0.68));
        brush.GradientStops.Add(new GradientStop(deep, 1));
        brush.Freeze();
        return brush;
    }

    private static void Fill(DrawingGroup group, Brush brush)
    {
        group.Children.Add(new GeometryDrawing(brush, null, new RectangleGeometry(new Rect(0, 0, Width, Height))));
    }

    private static void RectBand(DrawingGroup group, double y, double h, Color color)
    {
        if (y < 0 || h <= 0 || y + h > Height)
            throw new InvalidOperationException("Spatial sky band must sit fully inside the drawing.");
        group.Children.Add(new GeometryDrawing(Solid(color), null, new RectangleGeometry(new Rect(0, y, Width, h))));
    }

    private static void Plane(DrawingGroup group, Point a, Point b, Point c, Point d, Color fill, Color edge) =>
        Plane(group, new[] { a, b, c, d }, fill, edge);

    private static void Plane(DrawingGroup group, Point a, Point b, Point c, Point d, Point e, Color fill, Color edge) =>
        Plane(group, new[] { a, b, c, d, e }, fill, edge);

    private static void Plane(DrawingGroup group, Point[] vertices, Color fill, Color edge)
    {
        var figure = new PathFigure { StartPoint = vertices[0], IsClosed = true, IsFilled = true };
        for (int i = 1; i < vertices.Length; i++)
            figure.Segments.Add(new LineSegment(vertices[i], true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        var pen = new Pen(Solid(edge), 1) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        group.Children.Add(new GeometryDrawing(Solid(fill), pen, geometry));
    }

    private static void Ceiling(DrawingGroup group, double x, double y, double radiusX, double radiusY, Color inner, Color outer)
    {
        if (x - radiusX < 0 || y - radiusY < 0 || x + radiusX > Width || y + radiusY > Height)
            throw new InvalidOperationException("Spatial ceiling light must sit fully inside the drawing.");
        var center = new Point(x, y);
        var radial = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = center,
            GradientOrigin = center,
            RadiusX = radiusX,
            RadiusY = radiusY
        };
        radial.GradientStops.Add(new GradientStop(inner, 0));
        radial.GradientStops.Add(new GradientStop(outer, 1));
        radial.Freeze();
        group.Children.Add(new GeometryDrawing(radial, null,
            new RectangleGeometry(new Rect(x - radiusX, y - radiusY, radiusX * 2, radiusY * 2))));
    }

    private static SolidColorBrush Solid(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static bool Replace(ResourceDictionary dictionary, string key, object value)
    {
        foreach (object existing in dictionary.Keys)
        {
            if (!Equals(existing, key)) continue;
            dictionary[key] = value;
            return true;
        }
        foreach (ResourceDictionary child in dictionary.MergedDictionaries)
            if (Replace(child, key, value)) return true;
        return false;
    }
}
