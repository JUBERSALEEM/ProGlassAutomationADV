using System.ComponentModel;
using ProGlassAutomation.Analytics;

namespace ProGlassAutomation.Customization;

public sealed class ChartCustomizationViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private ChartDisplayOptions options = new();
    private string chartTitle = "Custom chart";
    private ChartKind kind = ChartKind.GroupedColumns;
    private ChartDataset? selectedDataset;
    public IReadOnlyList<ChartKind> KindChoices { get; } = Enum.GetValues<ChartKind>();
    public IReadOnlyList<LegendPlacement> LegendChoices { get; } = Enum.GetValues<LegendPlacement>();
    public IReadOnlyList<ChartDataset> AvailableDatasets { get; }
    public ChartCustomizationViewModel()
    {
        var samples = new ProGlassAutomation.ThemeStudio.AnalyticsSamples();
        AvailableDatasets = [samples.Revenue, samples.Trend, samples.Mix, samples.Channels, samples.Capacity, samples.Area, samples.Spark];
        selectedDataset = AvailableDatasets[0];
    }
    public ChartKind SelectedKind { get => kind; set { kind = value; Notify(nameof(SelectedKind)); Notify(nameof(IsBar)); Notify(nameof(IsLine)); Notify(nameof(IsDonut)); Notify(nameof(PreviewDataset)); } }
    public string ChartTitle { get => chartTitle; set { chartTitle=value; Notify(nameof(ChartTitle)); Notify(nameof(PreviewDataset)); } }
    public ChartDataset? SelectedDataset { get => selectedDataset; set { selectedDataset=value; Notify(nameof(SelectedDataset)); Notify(nameof(PreviewDataset)); } }
    public double ChartHeight { get => options.Height; set { options = options with { Height=value }; Notify(nameof(ChartHeight)); Notify(nameof(PreviewDataset)); } }
    public LegendPlacement LegendPosition { get => options.Legend; set { options = options with { Legend=value }; Notify(nameof(LegendPosition)); } }
    public bool ShowGrid { get => options.ShowGrid; set { options = options with { ShowGrid=value }; Notify(nameof(ShowGrid)); Notify(nameof(PreviewDataset)); } }
    public bool ShowAxis { get => options.ShowAxis; set { options = options with { ShowAxis=value }; Notify(nameof(ShowAxis)); Notify(nameof(PreviewDataset)); } }
    public bool ShowValueLabels { get => options.ShowValueLabels; set { options = options with { ShowValueLabels=value }; Notify(nameof(ShowValueLabels)); } }
    public bool ShowTooltip { get => options.ShowTooltip; set { options = options with { ShowTooltip=value }; Notify(nameof(ShowTooltip)); } }
    public bool IsBar => kind is ChartKind.Columns or ChartKind.GroupedColumns or ChartKind.StackedColumns or ChartKind.HorizontalBars;
    public bool IsLine => kind is ChartKind.Lines or ChartKind.Area or ChartKind.Sparkline;
    public bool IsDonut => kind is ChartKind.Donut or ChartKind.Pie;
    public ChartDisplayOptions Options => options;
    public ChartDataset PreviewDataset
    {
        get
        {
            var baseData = selectedDataset;
            if (baseData is null) return new ChartDataset(string.IsNullOrWhiteSpace(chartTitle)? "Custom chart" : chartTitle, kind, ["May","Jun"], [new ChartSeriesData("Series", [12,18])], "units", "Custom preview");
            // keep source values, only reflect kind/title overrides for display — never mutate business totals
            return new ChartDataset(string.IsNullOrWhiteSpace(chartTitle)? baseData.Title : chartTitle, kind, baseData.Categories, baseData.Series, baseData.Unit, baseData.Summary, baseData.CenterText, baseData.State);
        }
    }
    private void Notify(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
