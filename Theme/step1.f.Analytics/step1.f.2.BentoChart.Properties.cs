using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace ProGlassAutomation.Analytics;

public sealed partial class BentoChart : UserControl
{
    private static DependencyProperty PaintProperty(string name) => DependencyProperty.Register(name, typeof(Brush), typeof(BentoChart), new PropertyMetadata(null, PaintChanged));
    private static DependencyProperty SizeProperty(string name) => DependencyProperty.Register(name, typeof(double), typeof(BentoChart), new PropertyMetadata(0d, PaintChanged));
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data), typeof(ChartDataset), typeof(BentoChart), new PropertyMetadata(null, DataChanged));
    public ChartDataset? Data { get => (ChartDataset?)GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public static readonly DependencyProperty Series1Property = PaintProperty(nameof(Series1));
    public static readonly DependencyProperty Series2Property = PaintProperty(nameof(Series2));
    public static readonly DependencyProperty Series3Property = PaintProperty(nameof(Series3));
    public static readonly DependencyProperty Series4Property = PaintProperty(nameof(Series4));
    public static readonly DependencyProperty AxisBrushProperty = PaintProperty(nameof(AxisBrush));
    public static readonly DependencyProperty GridBrushProperty = PaintProperty(nameof(GridBrush));
    public static readonly DependencyProperty TooltipSurfaceProperty = PaintProperty(nameof(TooltipSurface));
    public static readonly DependencyProperty TooltipTextProperty = PaintProperty(nameof(TooltipText));
    public static readonly DependencyProperty PlotHeightProperty = SizeProperty(nameof(PlotHeight));
    public static readonly DependencyProperty LineWidthProperty = SizeProperty(nameof(LineWidth));
    public static readonly DependencyProperty PointSizeProperty = SizeProperty(nameof(PointSize));
    public static readonly DependencyProperty DonutRadiusProperty = SizeProperty(nameof(DonutRadius));
    public static readonly DependencyProperty CenterFontSizeProperty = SizeProperty(nameof(CenterFontSize));
    public double CenterFontSize { get => (double)GetValue(CenterFontSizeProperty); set => SetValue(CenterFontSizeProperty, value); }
    public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(nameof(ShowGrid), typeof(bool), typeof(BentoChart), new PropertyMetadata(true, PaintChanged));
    public static readonly DependencyProperty ShowAxisProperty = DependencyProperty.Register(nameof(ShowAxis), typeof(bool), typeof(BentoChart), new PropertyMetadata(true, PaintChanged));
    public static readonly DependencyProperty ShowLegendProperty = DependencyProperty.Register(nameof(ShowLegend), typeof(bool), typeof(BentoChart), new PropertyMetadata(true, PaintChanged));
    public static readonly DependencyProperty BarCornerProperty = DependencyProperty.Register(nameof(BarCorner), typeof(double), typeof(BentoChart), new PropertyMetadata(6d, PaintChanged));
    public static readonly DependencyProperty AreaOpacityProperty = SizeProperty(nameof(AreaOpacity));
    public Brush Series1 { get => (Brush)GetValue(Series1Property); set => SetValue(Series1Property, value); }
    public Brush Series2 { get => (Brush)GetValue(Series2Property); set => SetValue(Series2Property, value); }
    public Brush Series3 { get => (Brush)GetValue(Series3Property); set => SetValue(Series3Property, value); }
    public Brush Series4 { get => (Brush)GetValue(Series4Property); set => SetValue(Series4Property, value); }
    public Brush AxisBrush { get => (Brush)GetValue(AxisBrushProperty); set => SetValue(AxisBrushProperty, value); }
    public Brush GridBrush { get => (Brush)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public Brush TooltipSurface { get => (Brush)GetValue(TooltipSurfaceProperty); set => SetValue(TooltipSurfaceProperty, value); }
    public Brush TooltipText { get => (Brush)GetValue(TooltipTextProperty); set => SetValue(TooltipTextProperty, value); }
    public double PlotHeight { get => (double)GetValue(PlotHeightProperty); set => SetValue(PlotHeightProperty, value); }
    public double LineWidth { get => (double)GetValue(LineWidthProperty); set => SetValue(LineWidthProperty, value); }
    public double PointSize { get => (double)GetValue(PointSizeProperty); set => SetValue(PointSizeProperty, value); }
    public double DonutRadius { get => (double)GetValue(DonutRadiusProperty); set => SetValue(DonutRadiusProperty, value); }
    public double AreaOpacity { get => (double)GetValue(AreaOpacityProperty); set => SetValue(AreaOpacityProperty, value); }
    public bool ShowGrid { get => (bool)GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }
    public bool ShowAxis { get => (bool)GetValue(ShowAxisProperty); set => SetValue(ShowAxisProperty, value); }
    public bool ShowLegend { get => (bool)GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    public double BarCorner { get => (double)GetValue(BarCornerProperty); set => SetValue(BarCornerProperty, value); }
    private static void PaintChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((BentoChart)sender).Schedule(false);
    private static void DataChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((BentoChart)sender).Schedule(true);
}
