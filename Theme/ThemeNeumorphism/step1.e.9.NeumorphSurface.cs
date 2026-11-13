using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ProGlassAutomation.ThemeNeumorphism;

public enum NeumorphRank { Chip, Control, Card }

/// <summary>
/// Soft extrusion: dual glow, 145-degree face, one-element inset ring.
/// Motion duration is read live (not a frozen Storyboard resource).
/// Neumorph-owned. Not Fluent acrylic, not Clay hue-matched plump extras.
/// </summary>
public sealed class NeumorphSurface : ContentControl
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(NeumorphSurface),
        new FrameworkPropertyMetadata(new CornerRadius(16)));
    public static readonly DependencyProperty ConcaveProperty = DependencyProperty.Register(
        nameof(Concave), typeof(bool), typeof(NeumorphSurface),
        new FrameworkPropertyMetadata(false, OnConcaveChanged));
    public static readonly DependencyProperty LiftedProperty = DependencyProperty.Register(
        nameof(Lifted), typeof(bool), typeof(NeumorphSurface),
        new FrameworkPropertyMetadata(false));
    public static readonly DependencyProperty RankProperty = DependencyProperty.Register(
        nameof(Rank), typeof(NeumorphRank), typeof(NeumorphSurface),
        new FrameworkPropertyMetadata(NeumorphRank.Card));
    public static readonly DependencyProperty LightBrushProperty = DependencyProperty.Register(
        nameof(LightBrush), typeof(Brush), typeof(NeumorphSurface));
    public static readonly DependencyProperty DarkBrushProperty = DependencyProperty.Register(
        nameof(DarkBrush), typeof(Brush), typeof(NeumorphSurface));
    public static readonly DependencyProperty RimLightBrushProperty = DependencyProperty.Register(
        nameof(RimLightBrush), typeof(Brush), typeof(NeumorphSurface));
    public static readonly DependencyProperty RimShadeBrushProperty = DependencyProperty.Register(
        nameof(RimShadeBrush), typeof(Brush), typeof(NeumorphSurface));

    private UIElement? _inset;

    public NeumorphSurface()
    {
        Focusable = false;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        ClipToBounds = false;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _inset = GetTemplateChild("InsetHost") as UIElement;
        AnimateInset(Concave);
    }

    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => null;

    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public bool Concave { get => (bool)GetValue(ConcaveProperty); set => SetValue(ConcaveProperty, value); }
    public bool Lifted { get => (bool)GetValue(LiftedProperty); set => SetValue(LiftedProperty, value); }
    public NeumorphRank Rank { get => (NeumorphRank)GetValue(RankProperty); set => SetValue(RankProperty, value); }
    public Brush LightBrush { get => (Brush)GetValue(LightBrushProperty); set => SetValue(LightBrushProperty, value); }
    public Brush DarkBrush { get => (Brush)GetValue(DarkBrushProperty); set => SetValue(DarkBrushProperty, value); }
    public Brush RimLightBrush { get => (Brush)GetValue(RimLightBrushProperty); set => SetValue(RimLightBrushProperty, value); }
    public Brush RimShadeBrush { get => (Brush)GetValue(RimShadeBrushProperty); set => SetValue(RimShadeBrushProperty, value); }

    private static void OnConcaveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NeumorphSurface surface)
            surface.AnimateInset((bool)e.NewValue);
    }

    private void AnimateInset(bool on)
    {
        if (_inset is null) return;
        Duration duration = NeumorphCustomization.MotionDuration();
        _inset.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(on ? 1d : 0d, duration));
    }
}
