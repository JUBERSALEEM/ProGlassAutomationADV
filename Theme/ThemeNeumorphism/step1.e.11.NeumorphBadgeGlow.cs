using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ProGlassAutomation.ThemeNeumorphism;

/// <summary>
/// Second shadow for StatusBadge. Wraps the Border in a Grid so light and dark
/// share one cell (no ActualWidth drift). Contract stays Border.
/// </summary>
public static class NeumorphBadgeGlow
{
    public static readonly DependencyProperty LightGlowProperty = DependencyProperty.RegisterAttached(
        "LightGlow",
        typeof(bool),
        typeof(NeumorphBadgeGlow),
        new PropertyMetadata(false, OnLightGlowChanged));

    public static void SetLightGlow(DependencyObject element, bool value) => element.SetValue(LightGlowProperty, value);
    public static bool GetLightGlow(DependencyObject element) => (bool)element.GetValue(LightGlowProperty);

    private static readonly DependencyProperty WrappedProperty = DependencyProperty.RegisterAttached(
        "Wrapped",
        typeof(bool),
        typeof(NeumorphBadgeGlow));

    private static void OnLightGlowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Border badge) return;
        if (e.NewValue is true)
            badge.Loaded += OnLoaded;
        else
            badge.Loaded -= OnLoaded;
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Border badge) return;
        badge.Loaded -= OnLoaded;
        if (Equals(badge.GetValue(WrappedProperty), true)) return;

        Border light = new()
        {
            IsHitTestVisible = false,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        light.SetBinding(Border.BackgroundProperty, new Binding(nameof(Border.Background)) { Source = badge });
        light.SetBinding(Border.CornerRadiusProperty, new Binding(nameof(Border.CornerRadius)) { Source = badge });
        DropShadowEffect effect = new()
        {
            BlurRadius = 12,
            ShadowDepth = 6,
            Direction = 125,
            RenderingBias = RenderingBias.Quality,
        };
        if (badge.TryFindResource("NeumorphLightColor") is Color color)
            effect.Color = color;
        if (badge.TryFindResource("NeumorphLightOpacity") is double opacity)
            effect.Opacity = opacity;
        light.Effect = effect;

        Grid wrap = new() { ClipToBounds = false };
        wrap.Children.Add(light);

        DependencyObject parent = badge.Parent;
        switch (parent)
        {
            case Panel panel:
            {
                int index = panel.Children.IndexOf(badge);
                if (index < 0) return;
                CopyLayout(badge, wrap);
                panel.Children.RemoveAt(index);
                wrap.Children.Add(badge);
                panel.Children.Insert(index, wrap);
                break;
            }
            case ContentControl content:
                content.Content = null;
                wrap.Children.Add(badge);
                content.Content = wrap;
                break;
            case Decorator decorator:
                decorator.Child = null;
                wrap.Children.Add(badge);
                decorator.Child = wrap;
                break;
            default:
                return;
        }

        badge.SetValue(WrappedProperty, true);
    }

    private static void CopyLayout(FrameworkElement from, FrameworkElement to)
    {
        to.HorizontalAlignment = from.HorizontalAlignment;
        to.VerticalAlignment = from.VerticalAlignment;
        to.Margin = from.Margin;
        from.Margin = new Thickness(0);
        Grid.SetColumn(to, Grid.GetColumn(from));
        Grid.SetRow(to, Grid.GetRow(from));
        Grid.SetColumnSpan(to, Grid.GetColumnSpan(from));
        Grid.SetRowSpan(to, Grid.GetRowSpan(from));
        DockPanel.SetDock(to, DockPanel.GetDock(from));
    }
}
