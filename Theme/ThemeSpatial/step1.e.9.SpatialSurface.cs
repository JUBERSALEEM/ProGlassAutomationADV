using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProGlassAutomation.ThemeSpatial;

/// <summary>
/// Spatial material: opaque near-plane face, layered lift, receding far plane behind.
/// Not GlassSurface, not LiquidGlassSurface, not FluentSurface, no BlurEffect on charts.
/// </summary>
public sealed class SpatialSurface : ContentControl
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(SpatialSurface),
        new FrameworkPropertyMetadata(new CornerRadius(16)));
    public static readonly DependencyProperty ConcaveProperty = DependencyProperty.Register(
        nameof(Concave), typeof(bool), typeof(SpatialSurface),
        new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty LiftedProperty = DependencyProperty.Register(
        nameof(Lifted), typeof(bool), typeof(SpatialSurface),
        new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty TintBrushProperty = DependencyProperty.Register(
        nameof(TintBrush), typeof(Brush), typeof(SpatialSurface));
    public static readonly DependencyProperty StrokeBrushProperty = DependencyProperty.Register(
        nameof(StrokeBrush), typeof(Brush), typeof(SpatialSurface));
    public static readonly DependencyProperty LuminosityBrushProperty = DependencyProperty.Register(
        nameof(LuminosityBrush), typeof(Brush), typeof(SpatialSurface));
    public static readonly DependencyProperty ShowTintProperty = DependencyProperty.Register(
        nameof(ShowTint), typeof(bool), typeof(SpatialSurface),
        new FrameworkPropertyMetadata(false));

    public SpatialSurface()
    {
        Focusable = false;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        ClipToBounds = false;
    }

    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => null;

    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public bool Concave { get => (bool)GetValue(ConcaveProperty); set => SetValue(ConcaveProperty, value); }
    public bool Lifted { get => (bool)GetValue(LiftedProperty); set => SetValue(LiftedProperty, value); }
    public bool ShowTint { get => (bool)GetValue(ShowTintProperty); set => SetValue(ShowTintProperty, value); }
    public Brush TintBrush { get => (Brush)GetValue(TintBrushProperty); set => SetValue(TintBrushProperty, value); }
    public Brush StrokeBrush { get => (Brush)GetValue(StrokeBrushProperty); set => SetValue(StrokeBrushProperty, value); }
    public Brush LuminosityBrush { get => (Brush)GetValue(LuminosityBrushProperty); set => SetValue(LuminosityBrushProperty, value); }
}
