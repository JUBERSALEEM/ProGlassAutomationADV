using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Theming;

namespace ProGlassAutomation.ThemeGlassmorphism;

/// <summary>
/// Glass-owned studio canvas: layered radial color fields, not a Liquid sky, not sun/moon, not PNG.
/// Wallpaper stays behind glass faces. WPF BlurEffect is not backdrop blur and is not used here.
/// </summary>
public static class GlassWallpaper
{
    private const double Width = 800;
    private const double Height = 500;

    public static void Install(ResourceDictionary package, Appearance appearance)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (!Enum.IsDefined(appearance)) throw new ArgumentOutOfRangeException(nameof(appearance));
        if (!Replace(package, "GlassCanvasBrush", Create(appearance)))
            throw new InvalidOperationException("GlassCanvasBrush must exist in Glass extras.");
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
        Fill(group, Wash(
            Color.FromRgb(96, 168, 216),
            Color.FromRgb(140, 120, 216),
            Color.FromRgb(72, 176, 200),
            Color.FromRgb(120, 156, 220)));
        Bloom(group, 200, 160, 140, 120, Color.FromArgb(220, 168, 96, 232), Color.FromArgb(0, 140, 120, 216));
        Bloom(group, 620, 180, 140, 140, Color.FromArgb(230, 32, 168, 236), Color.FromArgb(0, 96, 168, 216));
        Bloom(group, 400, 380, 160, 100, Color.FromArgb(200, 64, 212, 176), Color.FromArgb(0, 72, 176, 200));
    }

    private static void PaintDark(DrawingGroup group)
    {
        Fill(group, Wash(
            Color.FromRgb(6, 10, 22),
            Color.FromRgb(8, 40, 72),
            Color.FromRgb(48, 16, 72),
            Color.FromRgb(4, 8, 16)));
        Bloom(group, 640, 140, 130, 130, Color.FromArgb(230, 0, 196, 255), Color.FromArgb(0, 8, 40, 72));
        Bloom(group, 160, 360, 150, 120, Color.FromArgb(220, 160, 64, 255), Color.FromArgb(0, 48, 16, 72));
        Bloom(group, 400, 200, 100, 100, Color.FromArgb(180, 64, 240, 255), Color.FromArgb(0, 6, 10, 22));
    }

    private static LinearGradientBrush Wash(Color top, Color high, Color mid, Color deep)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        brush.GradientStops.Add(new GradientStop(top, 0));
        brush.GradientStops.Add(new GradientStop(high, 0.35));
        brush.GradientStops.Add(new GradientStop(mid, 0.7));
        brush.GradientStops.Add(new GradientStop(deep, 1));
        brush.Freeze();
        return brush;
    }

    private static void Fill(DrawingGroup group, Brush brush)
    {
        group.Children.Add(new GeometryDrawing(brush, null, new RectangleGeometry(new Rect(0, 0, Width, Height))));
    }

    private static void Bloom(DrawingGroup group, double x, double y, double radiusX, double radiusY, Color inner, Color outer)
    {
        if (x - radiusX < 0 || y - radiusY < 0 || x + radiusX > Width || y + radiusY > Height)
            throw new InvalidOperationException("Glass wallpaper bloom must sit fully inside the drawing.");
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
        radial.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(inner.A / 3), inner.R, inner.G, inner.B), 0.5));
        radial.GradientStops.Add(new GradientStop(outer, 1));
        radial.Freeze();
        group.Children.Add(new GeometryDrawing(radial, null, new EllipseGeometry(center, radiusX, radiusY)));
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
