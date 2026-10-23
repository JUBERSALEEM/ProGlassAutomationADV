using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace ProGlassAutomation.Analytics;

public sealed partial class BentoChart
{
    private ISeries[] BuildSeries(ChartDataset data) => data.Series.Select(source =>
    {
        double?[] values = source.Values.ToArray();
        ISeries series = data.Kind switch
        {
            ChartKind.HorizontalBars => new RowSeries<double?> { Values = values },
            ChartKind.StackedColumns => new StackedColumnSeries<double?> { Values = values },
            ChartKind.Lines or ChartKind.Area or ChartKind.Sparkline => new LineSeries<double?> { Values = values, LineSmoothness = 0 },
            ChartKind.Donut or ChartKind.Pie => new PieSeries<double?> { Values = values },
            _ => new ColumnSeries<double?> { Values = values }
        };
        series.Name = source.Name;
        return series;
    }).ToArray();
    private static SKColor ColorOf(Brush? brush)
    {
        if (brush is not SolidColorBrush solid) return new SKColor(0, 0, 0, 0);
        var c = solid.Color;
        return new SKColor(c.R, c.G, c.B, c.A);
    }
    private static SolidColorPaint Paint(Brush? brush, double thickness = 1) => new(ColorOf(brush)) { StrokeThickness = (float)thickness };
    private SolidColorPaint TextPaint(Brush? brush)
    {
        string family = FontFamily?.Source ?? "Segoe UI";
        return new SolidColorPaint(ColorOf(brush)) { SKTypeface = SKTypeface.FromFamilyName(family) };
    }
    private void ApplyPaints(ChartDataset data)
    {
        if (Series1 is null) return;
        double plotH = cartesian.ActualHeight;
        double plotW = cartesian.ActualWidth;
        bool known = plotH > 1 && plotW > 1;
        // Nested dashboard cells (~100px) cannot host unit names + padding + a 0 baseline.
        // Sparkline kind, ShowAxis=false, or a short plot all use compact chrome so the series fills the cell.
        bool spark = data.Kind == ChartKind.Sparkline || !ShowAxis;
        bool tight = spark || (known && plotH < 160);
        bool bars = data.Kind is ChartKind.Columns or ChartKind.GroupedColumns or ChartKind.StackedColumns or ChartKind.HorizontalBars;
        Brush[] palette = [Series1, Series2, Series3, Series4];
        for (int i = 0; i < rendered.Length; i++)
        {
            switch (rendered[i])
            {
                case ColumnSeries<double?> s:
                    s.Fill = Paint(palette[i]); s.Stroke = null;
                    s.MaxBarWidth = tight ? 12 : 22;
                    break;
                case StackedColumnSeries<double?> s: s.Fill = Paint(palette[i]); s.Stroke = null; break;
                case RowSeries<double?> s: s.Fill = Paint(palette[i]); s.Stroke = null; break;
                case LineSeries<double?> s:
                    s.Stroke = Paint(palette[i], LineWidth);
                    s.Fill = data.Kind == ChartKind.Area ? new SolidColorPaint(ColorOf(palette[i]).WithAlpha((byte)(Math.Clamp(AreaOpacity, 0, 1) * 255))) : null;
                    s.GeometrySize = spark || tight ? 0 : PointSize;
                    s.GeometryFill = Paint(palette[i]); s.GeometryStroke = Paint(palette[i]);
                    break;
                case PieSeries<double?> s:
                    s.Fill = Paint(palette[i]); s.Stroke = Paint(GridBrush, LineWidth);
                    double hole = DonutRadius;
                    double plotSize = Math.Min(pie.ActualWidth, pie.ActualHeight);
                    if (plotSize > 0) hole = Math.Min(hole, plotSize * 0.38);
                    s.InnerRadius = data.Kind == ChartKind.Donut ? hole : 0;
                    break;
            }
        }
        cartesian.TooltipBackgroundPaint = Paint(TooltipSurface); cartesian.TooltipTextPaint = TextPaint(TooltipText);
        pie.TooltipBackgroundPaint = Paint(TooltipSurface); pie.TooltipTextPaint = TextPaint(TooltipText);
        cartesian.TooltipTextSize = FontSize; pie.TooltipTextSize = FontSize;
        center.Foreground = AxisBrush; message.Foreground = AxisBrush; summary.Foreground = AxisBrush; values.Foreground = AxisBrush;
        bool horizontal = data.Kind == ChartKind.HorizontalBars;
        var category = new Axis
        {
            Labels = data.Categories.ToArray(),
            LabelsPaint = tight ? null : TextPaint(AxisBrush),
            SeparatorsPaint = null, TextSize = FontSize, MinStep = 1,
            TicksPaint = null, SubseparatorsPaint = null, SubticksPaint = null, ZeroPaint = null,
            Padding = tight ? new LiveChartsCore.Drawing.Padding(0) : new LiveChartsCore.Drawing.Padding(4, 0, 4, 8)
        };
        var numeric = new Axis
        {
            Name = tight ? null : data.Unit,
            NameTextSize = FontSize, TicksPaint = null, SubseparatorsPaint = null, SubticksPaint = null, ZeroPaint = null,
            NamePaint = tight ? null : TextPaint(AxisBrush),
            LabelsPaint = tight ? null : TextPaint(AxisBrush),
            SeparatorsPaint = tight || !ShowGrid ? null : Paint(GridBrush),
            TextSize = FontSize,
            // Bars keep a zero baseline. Tight line/area zooms to the data so the shape is readable.
            MinLimit = bars && data.Series.SelectMany(s => s.Values).Where(v => v.HasValue).All(v => v >= 0) ? 0 : null,
            Padding = tight ? new LiveChartsCore.Drawing.Padding(0) : new LiveChartsCore.Drawing.Padding(8, 8, 4, 8),
            NamePadding = tight ? new LiveChartsCore.Drawing.Padding(0) : new LiveChartsCore.Drawing.Padding(0, 12)
        };
        if (!ShowGrid) { numeric.SeparatorsPaint = null; category.SeparatorsPaint = null; }
        if (!ShowAxis) { numeric.LabelsPaint = null; numeric.NamePaint = null; numeric.Name = null; category.LabelsPaint = null; }
        cartesian.DrawMargin = tight ? new Margin(4) : null;
        foreach (var ser in rendered) { if (ser is ColumnSeries<double?> cs) { cs.Rx = (float)Math.Clamp(BarCorner, 0, 12); cs.Ry = (float)Math.Clamp(BarCorner, 0, 12); } }
        cartesian.XAxes = [horizontal ? numeric : category]; cartesian.YAxes = [horizontal ? category : numeric];
    }
}
