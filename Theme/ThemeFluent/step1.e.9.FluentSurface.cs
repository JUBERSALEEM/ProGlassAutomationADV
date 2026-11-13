using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProGlassAutomation.ThemeFluent;

/// <summary>
/// Native Fluent material: mica content layer, acrylic tint, luminosity, reveal hover.
/// Fluent-owned. Not ClaySurface, not a Bento border, not CSS backdrop-filter.
/// </summary>
public sealed class FluentSurface : ContentControl
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(FluentSurface),
        new FrameworkPropertyMetadata(new CornerRadius(8)));
    public static readonly DependencyProperty ConcaveProperty = DependencyProperty.Register(
        nameof(Concave), typeof(bool), typeof(FluentSurface),
        new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty TintBrushProperty = DependencyProperty.Register(
        nameof(TintBrush), typeof(Brush), typeof(FluentSurface));
    public static readonly DependencyProperty StrokeBrushProperty = DependencyProperty.Register(
        nameof(StrokeBrush), typeof(Brush), typeof(FluentSurface));
    public static readonly DependencyProperty LuminosityBrushProperty = DependencyProperty.Register(
        nameof(LuminosityBrush), typeof(Brush), typeof(FluentSurface));
    public static readonly DependencyProperty ShowTintProperty = DependencyProperty.Register(
        nameof(ShowTint), typeof(bool), typeof(FluentSurface),
        new FrameworkPropertyMetadata(true));

    public FluentSurface()
    {
        Focusable = false;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
    }

    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public bool Concave { get => (bool)GetValue(ConcaveProperty); set => SetValue(ConcaveProperty, value); }
    public bool ShowTint { get => (bool)GetValue(ShowTintProperty); set => SetValue(ShowTintProperty, value); }
    public Brush TintBrush { get => (Brush)GetValue(TintBrushProperty); set => SetValue(TintBrushProperty, value); }
    public Brush StrokeBrush { get => (Brush)GetValue(StrokeBrushProperty); set => SetValue(StrokeBrushProperty, value); }
    public Brush LuminosityBrush { get => (Brush)GetValue(LuminosityBrushProperty); set => SetValue(LuminosityBrushProperty, value); }
}
