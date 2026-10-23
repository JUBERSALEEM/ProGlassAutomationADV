namespace ProGlassAutomation.Analytics;

public enum ChartKind { Columns, HorizontalBars, GroupedColumns, StackedColumns, Lines, Area, Donut, Pie, Sparkline }
public enum ChartState { Ready, Loading, Error }

/// <summary>Immutable presentation data; callers perform business calculations.</summary>
public sealed class ChartSeriesData
{
    public string Name { get; }
    public IReadOnlyList<double?> Values { get; }
    public ChartSeriesData(string name, IEnumerable<double?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Name = name;
        Values = Array.AsReadOnly(values.ToArray());
    }
}

public sealed class ChartDataset
{
    public string Title { get; }
    public string Unit { get; }
    public string Summary { get; }
    public string CenterText { get; }
    public ChartKind Kind { get; }
    public ChartState State { get; }
    public IReadOnlyList<string> Categories { get; }
    public IReadOnlyList<ChartSeriesData> Series { get; }
    public ChartDataset(string title, ChartKind kind, IEnumerable<string> categories,
        IEnumerable<ChartSeriesData> series, string unit, string summary, string centerText = "", ChartState state = ChartState.Ready)
    {
        ArgumentNullException.ThrowIfNull(categories); ArgumentNullException.ThrowIfNull(series);
        Title = title; Kind = kind; Unit = unit; Summary = summary; CenterText = centerText; State = state;
        Categories = Array.AsReadOnly(categories.ToArray()); Series = Array.AsReadOnly(series.Select(s => s ?? throw new ArgumentException("Series cannot contain null entries.", nameof(series))).ToArray());
    }
    public string? ValidationError()
    {
        if (string.IsNullOrWhiteSpace(Summary)) return "Supply a textual chart summary.";
        if (string.IsNullOrWhiteSpace(Title)) return "Provide a chart title for accessible identification.";
        if (!Enum.IsDefined(Kind) || !Enum.IsDefined(State)) return "Unsupported chart configuration.";
        if (Categories.Count > 1000) return "Limit each chart to 1,000 categories; aggregate larger datasets before rendering.";
        if (Series.Count > 4) return "Use at most four series per chart; split larger comparisons into separate panels.";
        if (Series.Any(s => string.IsNullOrWhiteSpace(s.Name)) || Series.Select(s => s.Name).Distinct().Count() != Series.Count)
            return "Series names must be nonempty and unique.";
        if (Series.SelectMany(s => s.Values).Any(v => v.HasValue && !double.IsFinite(v.Value)))
            return "Chart values must be finite numbers or null for missing data.";
        if (Kind is ChartKind.Donut or ChartKind.Pie)
        {
            if (Series.Any(s => s.Values.Count != 1 || s.Values[0] is null or < 0))
                return "Pie/donut segments require one nonnegative value each.";
        }
        else if (Categories.Any(string.IsNullOrWhiteSpace) || Series.Any(s => s.Values.Count != Categories.Count))
            return "Each series must have one value (or null) per category.";
        return null;
    }
    public bool IsEmpty => Series.Count == 0 || !Series.SelectMany(s => s.Values).Any(v => v.HasValue) ||
        ((Kind is ChartKind.Donut or ChartKind.Pie) && Series.All(s => s.Values.All(v => v is null or 0)));
}
