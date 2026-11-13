using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Customization;

namespace ProGlassAutomation.Controls.Theme;

/// <summary>Live application icon mark backed by the imported icon catalog.</summary>
public sealed class ImportedIconMark : FrameworkElement
{
    public static readonly DependencyProperty IconNameProperty = DependencyProperty.Register(
        nameof(IconName), typeof(string), typeof(ImportedIconMark),
        new FrameworkPropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(ImportedIconMark),
        new FrameworkPropertyMetadata(Brushes.Gray, OnVisualPropertyChanged));

    public string IconName
    {
        get => (string)GetValue(IconNameProperty);
        set => SetValue(IconNameProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public ImportedIconMark()
    {
        Width = 14;
        Height = 14;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static void OnVisualPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((ImportedIconMark)sender).InvalidateVisual();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        IconService.Changed += OnIconStyleChanged;
        InvalidateVisual();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => IconService.Changed -= OnIconStyleChanged;
    private void OnIconStyleChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        IconMarkStyle style = IconService.Active.Style;
        IconRecipe recipe = IconService.Recipe(style);
        Geometry geometry = IconService.Mark(IconService.Resolve(IconName, fallback: IconKind.Theme), style);
        Rect bounds = geometry.Bounds;
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return;

        double scale = Math.Min(ActualWidth / bounds.Width, ActualHeight / bounds.Height) * recipe.Scale;
        double x = (ActualWidth - bounds.Width * scale) / 2 - bounds.Left * scale;
        double y = (ActualHeight - bounds.Height * scale) / 2 - bounds.Top * scale;
        drawingContext.PushTransform(new TransformGroup
        {
            Children =
            {
                new ScaleTransform(scale, scale),
                new TranslateTransform(x, y)
            }
        });

        if (recipe.Plate)
        {
            var plate = new SolidColorBrush(Color.FromArgb(0x24, 128, 128, 128));
            plate.Freeze();
            drawingContext.DrawEllipse(plate, null,
                new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2),
                bounds.Width * 0.55, bounds.Height * 0.55);
        }

        Brush? fill = null;
        if (recipe.Filled || recipe.Duotone)
        {
            if (recipe.Duotone)
            {
                var tint = Foreground is SolidColorBrush solid
                    ? new SolidColorBrush(Color.FromArgb(0x55, solid.Color.R, solid.Color.G, solid.Color.B))
                    : Foreground;
                if (tint?.CanFreeze == true) tint.Freeze();
                fill = tint;
            }
            else fill = Foreground;
        }

        Pen? pen = recipe.Stroke > 0
            ? new Pen(Foreground, recipe.Stroke / Math.Max(scale, 0.01))
            : null;
        if (pen?.CanFreeze == true) pen.Freeze();
        drawingContext.DrawGeometry(fill, pen, geometry);
        drawingContext.Pop();
    }
}
