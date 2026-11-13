using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using ProGlassAutomation.Customization;

namespace ProGlassAutomation.Controls.Theme;

/// <summary>Live application icon mark backed by the imported icon catalog.</summary>
public sealed class ImportedIconMark : Path
{
    public static readonly DependencyProperty IconNameProperty = DependencyProperty.Register(
        nameof(IconName), typeof(string), typeof(ImportedIconMark),
        new FrameworkPropertyMetadata(string.Empty, OnNameChanged));

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(ImportedIconMark),
        new FrameworkPropertyMetadata(Brushes.Gray, OnNameChanged));

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
        Stretch = Stretch.Uniform;
        Width = 14;
        Height = 14;
        StrokeLineJoin = PenLineJoin.Round;
        StrokeStartLineCap = PenLineCap.Round;
        StrokeEndLineCap = PenLineCap.Round;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        UpdateMark();
    }

    private static void OnNameChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((ImportedIconMark)sender).UpdateMark();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        IconService.Changed += OnIconStyleChanged;
        UpdateMark();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => IconService.Changed -= OnIconStyleChanged;
    private void OnIconStyleChanged(object? sender, EventArgs e) => UpdateMark();

    private void UpdateMark()
    {
        IconMarkStyle style = IconService.Active.Style;
        IconRecipe recipe = IconService.Recipe(style);
        IconKind kind = IconService.Resolve(IconName, fallback: IconKind.Theme);
        Data = IconService.Mark(kind, style);
        StrokeThickness = recipe.Stroke;
        Fill = recipe.Filled || recipe.Duotone ? Foreground : null;
        Stroke = recipe.Filled && !recipe.Duotone ? null : Foreground;
        Opacity = recipe.Scale;
        if (recipe.Plate)
        {
            // Preserve shape legibility on the soft-chip styles without a
            // hard dependency on a specific theme's plate brush.
            Opacity = Math.Min(1d, recipe.Scale + 0.12d);
        }
    }
}
