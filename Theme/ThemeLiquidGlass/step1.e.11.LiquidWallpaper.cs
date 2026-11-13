using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Theming;

namespace ProGlassAutomation.ThemeLiquidGlass;

/// <summary>
/// One Liquid-owned Light/Dark sky. Smooth wash plus a small in-bounds sun/moon.
/// No PNG, no catalog, no ellipse larger than the drawing (clipped blooms read as slabs).
/// </summary>
public static class LiquidWallpaper
{
    private const double Width = 800;
    private const double Height = 500;

    public static void Install(ResourceDictionary package, Appearance appearance)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (!Enum.IsDefined(appearance)) throw new ArgumentOutOfRangeException(nameof(appearance));
        if (!Replace(package, "LiquidCanvasBrush", Create(appearance)))
            throw new InvalidOperationException("LiquidCanvasBrush must exist in Liquid extras.");
    }

    public static DrawingBrush Create(Appearance appearance)
    {
        var group = new DrawingGroup();
        if (appearance == Appearance.Dark) PaintNight(group);
        else PaintDay(group);
        group.Freeze();
        return new DrawingBrush(group)
        {
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center,
            TileMode = TileMode.None
        };
    }

    private static void PaintDay(DrawingGroup group)
    {
        // Light.xaml ApplicationBackground #62B4E8 — continuous day sky, not clipped orbs.
        Fill(group, Sky(
            Color.FromRgb(232, 244, 252),
            Color.FromRgb(181, 216, 240),
            Color.FromRgb(98, 180, 232),
            Color.FromRgb(74, 156, 212),
            Color.FromRgb(58, 142, 200)));
        Bloom(group, 620, 88, 88, 88, Color.FromArgb(200, 255, 248, 230), Color.FromArgb(0, 181, 216, 240));
    }

    private static void PaintNight(DrawingGroup group)
    {
        Fill(group, Sky(
            Color.FromRgb(10, 24, 48),
            Color.FromRgb(14, 40, 78),
            Color.FromRgb(18, 52, 96),
            Color.FromRgb(12, 40, 80),
            Color.FromRgb(4, 12, 28)));
        Bloom(group, 600, 90, 70, 70, Color.FromArgb(160, 198, 220, 240), Color.FromArgb(0, 14, 40, 78));
    }

    private static LinearGradientBrush Sky(Color top, Color high, Color mid, Color water, Color deep)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0.2, 0), EndPoint = new Point(0.8, 1) };
        brush.GradientStops.Add(new GradientStop(top, 0));
        brush.GradientStops.Add(new GradientStop(high, 0.28));
        brush.GradientStops.Add(new GradientStop(mid, 0.52));
        brush.GradientStops.Add(new GradientStop(water, 0.76));
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
            throw new InvalidOperationException("Wallpaper bloom must sit fully inside the drawing.");
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
        radial.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(inner.A / 3), inner.R, inner.G, inner.B), 0.45));
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
