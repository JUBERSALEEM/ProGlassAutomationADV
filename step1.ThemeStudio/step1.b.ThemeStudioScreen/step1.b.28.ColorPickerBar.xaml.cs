using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ProGlassAutomation.Customization;

namespace ProGlassAutomation.ThemeStudio;

public partial class ColorPickerBar : UserControl
{
    public static readonly DependencyProperty SelectedHexProperty = DependencyProperty.Register(
        nameof(SelectedHex), typeof(string), typeof(ColorPickerBar),
        new FrameworkPropertyMetadata("#2F5AA8", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    private static readonly string[] Basic =
    [
        "#000000", "#800000", "#008000", "#808000", "#000080", "#800080", "#008080", "#C0C0C0",
        "#808080", "#FF0000", "#00FF00", "#FFFF00", "#0000FF", "#FF00FF", "#00FFFF", "#FFFFFF",
        "#1B2430", "#8A2410", "#144632", "#6E4F22", "#1E3C72", "#452868", "#1F6B4A", "#445060",
        "#2F5AA8", "#C43C1E", "#3D9A6A", "#A67C3A", "#4D82D6", "#6B3FA0", "#C4954A", "#8B63C7",
        "#8EB0EE", "#F08A72", "#86C8A4", "#D4B48A", "#B89EE3", "#A9B4C4", "#F8C9BE", "#EAD9C3",
        "#E8F0FE", "#FDECE8", "#E6F4EC", "#F8F1E8", "#F3EEFA", "#EEF1F5", "#C5D7F7", "#DDD0F2"
    ];

    private bool dragging;

    public ColorPickerBar()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            BuildBasic();
            BuildSpectrum();
        };
    }

    public string SelectedHex
    {
        get => (string)GetValue(SelectedHexProperty);
        set => SetValue(SelectedHexProperty, value);
    }

    private void BuildSpectrum()
    {
        var hue = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        hue.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 0));
        hue.GradientStops.Add(new GradientStop(Color.FromRgb(255, 255, 0), 0.17));
        hue.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 0), 0.33));
        hue.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 255), 0.5));
        hue.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0, 255), 0.67));
        hue.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 255), 0.83));
        hue.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 1));
        hue.Freeze();
        HueStrip.Background = hue;
        var veil = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        veil.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0));
        veil.GradientStops.Add(new GradientStop(Color.FromRgb(255, 255, 255), 1));
        veil.Freeze();
        WhiteVeil.Background = veil;
    }

    private void BuildBasic()
    {
        if (BasicGrid.Children.Count != 0) return;
        foreach (string hex in Basic)
        {
            var brush = new SolidColorBrush(ColorPlateService.ParseHex(hex) ?? Colors.Gray);
            brush.Freeze();
            var swatch = new RepeatButton
            {
                Background = brush,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                Tag = hex
            };
            swatch.SetResourceReference(MinHeightProperty, "ChoiceMarkSize");
            swatch.SetResourceReference(MinWidthProperty, "ChoiceMarkSize");
            System.Windows.Automation.AutomationProperties.SetName(swatch, hex);
            swatch.Click += (_, _) => SelectedHex = (string)swatch.Tag;
            BasicGrid.Children.Add(swatch);
        }
    }

    private void OnSpectrumDown(object sender, MouseButtonEventArgs e)
    {
        dragging = true;
        Spectrum.CaptureMouse();
        SampleSpectrum(e.GetPosition(Spectrum));
        e.Handled = true;
    }

    private void OnSpectrumMove(object sender, MouseEventArgs e)
    {
        if (!dragging || e.LeftButton != MouseButtonState.Pressed) return;
        // Commit on release so a drag does not rebuild resources on every pixel.
    }

    private void OnSpectrumUp(object sender, MouseButtonEventArgs e)
    {
        if (dragging) SampleSpectrum(e.GetPosition(Spectrum));
        dragging = false;
        if (Spectrum.IsMouseCaptured) Spectrum.ReleaseMouseCapture();
    }

    private void SampleSpectrum(Point point)
    {
        if (Spectrum.ActualWidth <= 0 || Spectrum.ActualHeight <= 0) return;
        double hue = 360d * Math.Clamp(point.X / Spectrum.ActualWidth, 0, 1);
        double value = 1d - Math.Clamp(point.Y / Spectrum.ActualHeight, 0, 1);
        SelectedHex = ColorPlateService.ToHex(FromHsv(hue, 1, value));
    }

    private static Color FromHsv(double hue, double saturation, double value)
    {
        double chroma = value * saturation;
        double h = hue / 60d;
        double x = chroma * (1 - Math.Abs(h % 2 - 1));
        double r = 0, g = 0, b = 0;
        if (h < 1) { r = chroma; g = x; }
        else if (h < 2) { r = x; g = chroma; }
        else if (h < 3) { g = chroma; b = x; }
        else if (h < 4) { g = x; b = chroma; }
        else if (h < 5) { r = x; b = chroma; }
        else { r = chroma; b = x; }
        double m = value - chroma;
        return Color.FromRgb(
            (byte)Math.Clamp((r + m) * 255, 0, 255),
            (byte)Math.Clamp((g + m) * 255, 0, 255),
            (byte)Math.Clamp((b + m) * 255, 0, 255));
    }
}
