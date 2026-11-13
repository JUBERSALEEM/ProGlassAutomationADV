using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProGlassAutomation.ThemeLiquidGlass;

public enum LiquidRank { Chip, Control, Card }

/// <summary>
/// Liquid Glass material: translucent face, top specular, hairline rim, card lift.
/// Matches the glass-ui / iOS 26 recipe (rgba face, white edge, soft shadow) in native WPF.
/// Not a sine-wave lining, not Fluent acrylic, not Clay, not CSS, not desktop capture.
/// </summary>
public sealed class LiquidGlassSurface : ContentControl
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(LiquidGlassSurface),
        new FrameworkPropertyMetadata(new CornerRadius(18), OnClipChanged));
    public static readonly DependencyProperty ConcaveProperty = DependencyProperty.Register(
        nameof(Concave), typeof(bool), typeof(LiquidGlassSurface),
        new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty LiftedProperty = DependencyProperty.Register(
        nameof(Lifted), typeof(bool), typeof(LiquidGlassSurface),
        new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty RankProperty = DependencyProperty.Register(
        nameof(Rank), typeof(LiquidRank), typeof(LiquidGlassSurface),
        new FrameworkPropertyMetadata(LiquidRank.Card));
    public static readonly DependencyProperty ShowFaceProperty = DependencyProperty.Register(
        nameof(ShowFace), typeof(bool), typeof(LiquidGlassSurface),
        new FrameworkPropertyMetadata(true));
    public static readonly DependencyProperty ReduceTransparencyProperty = DependencyProperty.Register(
        nameof(ReduceTransparency), typeof(bool), typeof(LiquidGlassSurface),
        new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty FaceBrushProperty = DependencyProperty.Register(
        nameof(FaceBrush), typeof(Brush), typeof(LiquidGlassSurface));
    public static readonly DependencyProperty SpecularBrushProperty = DependencyProperty.Register(
        nameof(SpecularBrush), typeof(Brush), typeof(LiquidGlassSurface));
    public static readonly DependencyProperty RimBrushProperty = DependencyProperty.Register(
        nameof(RimBrush), typeof(Brush), typeof(LiquidGlassSurface));
    public static readonly DependencyProperty StrokeBrushProperty = DependencyProperty.Register(
        nameof(StrokeBrush), typeof(Brush), typeof(LiquidGlassSurface));

    public LiquidGlassSurface()
    {
        Focusable = false;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        ClipToBounds = false;
        SizeChanged += (_, _) => ClipFace();
        Loaded += (_, _) => ClipFace();
    }

    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => null;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        ClipFace();
    }

    private static void OnClipChanged(DependencyObject sender, DependencyPropertyChangedEventArgs _) =>
        ((LiquidGlassSurface)sender).ClipFace();

    /// <summary>
    /// WPF Border.ClipToBounds clips a rectangle, not the rounded face. Charts and sheen then
    /// paint square corners. Clip the Face to the card radius; do not clip the lift shadow.
    /// </summary>
    private void ClipFace()
    {
        if (Template?.FindName("Face", this) is not Border face) return;
        double width = face.ActualWidth, height = face.ActualHeight;
        if (width < 1 || height < 1)
        {
            face.Clip = null;
            return;
        }
        double radius = Math.Min(Math.Max(0, CornerRadius.TopLeft), Math.Min(width, height) / 2d);
        face.Clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
    }

    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public bool Concave { get => (bool)GetValue(ConcaveProperty); set => SetValue(ConcaveProperty, value); }
    public bool Lifted { get => (bool)GetValue(LiftedProperty); set => SetValue(LiftedProperty, value); }
    public LiquidRank Rank { get => (LiquidRank)GetValue(RankProperty); set => SetValue(RankProperty, value); }
    public bool ShowFace { get => (bool)GetValue(ShowFaceProperty); set => SetValue(ShowFaceProperty, value); }
    public bool ReduceTransparency { get => (bool)GetValue(ReduceTransparencyProperty); set => SetValue(ReduceTransparencyProperty, value); }
    public Brush FaceBrush { get => (Brush)GetValue(FaceBrushProperty); set => SetValue(FaceBrushProperty, value); }
    public Brush SpecularBrush { get => (Brush)GetValue(SpecularBrushProperty); set => SetValue(SpecularBrushProperty, value); }
    public Brush RimBrush { get => (Brush)GetValue(RimBrushProperty); set => SetValue(RimBrushProperty, value); }
    public Brush StrokeBrush { get => (Brush)GetValue(StrokeBrushProperty); set => SetValue(StrokeBrushProperty, value); }
}
