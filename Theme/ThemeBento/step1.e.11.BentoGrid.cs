using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.ThemeBento;

/// <summary>
/// Enterprise Bento mosaic: equal-height rows, explicit column/row spans, packed gutters.
/// Wide modules (hero, analytics, tables) sit beside compact KPI/status tiles.
/// Compact widths reflow in document order without mutating attached slots.
/// </summary>
public sealed class BentoGrid : Panel
{
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(int), typeof(BentoGrid),
        new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsMeasure), v => (int)v >= 1);
    public static readonly DependencyProperty RowHeightProperty = DependencyProperty.Register(
        nameof(RowHeight), typeof(double), typeof(BentoGrid),
        new FrameworkPropertyMetadata(168d, FrameworkPropertyMetadataOptions.AffectsMeasure), v => (double)v >= 0);
    public static readonly DependencyProperty GutterProperty = DependencyProperty.Register(
        nameof(Gutter), typeof(double), typeof(BentoGrid),
        new FrameworkPropertyMetadata(8d, FrameworkPropertyMetadataOptions.AffectsMeasure), v => (double)v >= 0);
    public static readonly DependencyProperty CompactWidthProperty = DependencyProperty.Register(
        nameof(CompactWidth), typeof(double), typeof(BentoGrid),
        new FrameworkPropertyMetadata(1100d, FrameworkPropertyMetadataOptions.AffectsMeasure), v => (double)v >= 0);
    public static readonly DependencyProperty FillAvailableProperty = DependencyProperty.Register(
        nameof(FillAvailable), typeof(bool), typeof(BentoGrid),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty ColumnProperty = DependencyProperty.RegisterAttached(
        "Column", typeof(int), typeof(BentoGrid),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsParentMeasure), v => (int)v >= 0);
    public static readonly DependencyProperty RowProperty = DependencyProperty.RegisterAttached(
        "Row", typeof(int), typeof(BentoGrid),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsParentMeasure), v => (int)v >= 0);
    public static readonly DependencyProperty ColumnSpanProperty = DependencyProperty.RegisterAttached(
        "ColumnSpan", typeof(int), typeof(BentoGrid),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsParentMeasure), v => (int)v >= 1);
    public static readonly DependencyProperty RowSpanProperty = DependencyProperty.RegisterAttached(
        "RowSpan", typeof(int), typeof(BentoGrid),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsParentMeasure), v => (int)v >= 1);

    private Slot[] slots = [];
    private double cellWidth, rowSize, gutterSize;
    private int usedColumns = 4;

    public BentoGrid()
    {
        ClipToBounds = true;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
    }

    public int Columns { get => (int)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    public double RowHeight { get => (double)GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    public double Gutter { get => (double)GetValue(GutterProperty); set => SetValue(GutterProperty, value); }
    public double CompactWidth { get => (double)GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    public bool FillAvailable { get => (bool)GetValue(FillAvailableProperty); set => SetValue(FillAvailableProperty, value); }
    public int EffectiveColumns { get; private set; } = 4;

    public static void SetColumn(UIElement e, int v) => e.SetValue(ColumnProperty, v);
    public static int GetColumn(UIElement e) => (int)e.GetValue(ColumnProperty);
    public static void SetRow(UIElement e, int v) => e.SetValue(RowProperty, v);
    public static int GetRow(UIElement e) => (int)e.GetValue(RowProperty);
    public static void SetColumnSpan(UIElement e, int v) => e.SetValue(ColumnSpanProperty, v);
    public static int GetColumnSpan(UIElement e) => (int)e.GetValue(ColumnSpanProperty);
    public static void SetRowSpan(UIElement e, int v) => e.SetValue(RowSpanProperty, v);
    public static int GetRowSpan(UIElement e) => (int)e.GetValue(RowSpanProperty);

    protected override Size MeasureOverride(Size available)
    {
        gutterSize = Gutter;
        int maxColumns = Math.Max(1, Columns);
        double width = double.IsInfinity(available.Width) ? maxColumns * 240 + (maxColumns - 1) * gutterSize : Math.Max(0, available.Width);
        usedColumns = width >= CompactWidth ? maxColumns : width >= 720 ? Math.Min(2, maxColumns) : 1;
        EffectiveColumns = usedColumns;
        slots = usedColumns == maxColumns ? PlaceExplicit(usedColumns) : PlacePacked(usedColumns);
        int rows = slots.Length == 0 ? 1 : slots.Max(s => s.Row + s.RowSpan);
        cellWidth = usedColumns == 0 ? 0 : Math.Max(0, (width - gutterSize * (usedColumns - 1)) / usedColumns);
        bool fill = FillAvailable && !double.IsInfinity(available.Height);
        double minimum = RowHeight > 0 ? RowHeight : 168;
        rowSize = fill ? Math.Max(minimum, (Math.Max(0, available.Height) - gutterSize * (rows - 1)) / rows) : minimum;
        foreach (var slot in slots)
            slot.Child.Measure(new Size(SpanSize(slot.ColumnSpan, cellWidth), SpanSize(slot.RowSpan, rowSize)));
        return new Size(width, SpanSize(rows, rowSize));
    }

    protected override Size ArrangeOverride(Size final)
    {
        int rows = slots.Length == 0 ? 1 : slots.Max(s => s.Row + s.RowSpan);
        cellWidth = usedColumns == 0 ? 0 : Math.Max(0, (Math.Max(0, final.Width) - gutterSize * (usedColumns - 1)) / usedColumns);
        if (FillAvailable)
            rowSize = Math.Max(RowHeight > 0 ? RowHeight : 168, (Math.Max(0, final.Height) - gutterSize * (rows - 1)) / rows);
        foreach (var slot in slots)
        {
            double x = slot.Column * (cellWidth + gutterSize);
            double y = slot.Row * (rowSize + gutterSize);
            slot.Child.Arrange(new Rect(x, y, SpanSize(slot.ColumnSpan, cellWidth), SpanSize(slot.RowSpan, rowSize)));
        }
        return final;
    }

    private Slot[] PlaceExplicit(int columns)
    {
        var result = new List<Slot>();
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            int span = Math.Clamp(GetColumnSpan(child), 1, columns);
            int column = Math.Clamp(GetColumn(child), 0, columns - span);
            result.Add(new Slot(child, column, Math.Max(0, GetRow(child)), span, Math.Max(1, GetRowSpan(child))));
        }
        return [.. result];
    }

    private Slot[] PlacePacked(int columns)
    {
        var used = new HashSet<(int C, int R)>();
        var result = new List<Slot>();
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            int spanC = Math.Clamp(GetColumnSpan(child), 1, columns);
            int spanR = Math.Max(1, GetRowSpan(child));
            var slot = FirstFree(used, columns, spanC, spanR);
            Occupy(used, slot.Column, slot.Row, spanC, spanR);
            result.Add(slot with { Child = child });
        }
        return [.. result];
    }

    private static Slot FirstFree(HashSet<(int C, int R)> used, int columns, int spanC, int spanR)
    {
        for (int row = 0; row < 64; row++)
            for (int column = 0; column <= columns - spanC; column++)
                if (IsFree(used, column, row, spanC, spanR))
                    return new Slot(null!, column, row, spanC, spanR);
        return new Slot(null!, 0, used.Count == 0 ? 0 : used.Max(c => c.R) + 1, spanC, spanR);
    }

    private static bool IsFree(HashSet<(int C, int R)> used, int column, int row, int spanC, int spanR)
    {
        for (int c = column; c < column + spanC; c++)
            for (int r = row; r < row + spanR; r++)
                if (used.Contains((c, r))) return false;
        return true;
    }

    private static void Occupy(HashSet<(int C, int R)> used, int column, int row, int spanC, int spanR)
    {
        for (int c = column; c < column + spanC; c++)
            for (int r = row; r < row + spanR; r++)
                used.Add((c, r));
    }

    private double SpanSize(int span, double cell) => Math.Max(0, span * cell + Math.Max(0, span - 1) * gutterSize);
    private readonly record struct Slot(UIElement Child, int Column, int Row, int ColumnSpan, int RowSpan);
}
