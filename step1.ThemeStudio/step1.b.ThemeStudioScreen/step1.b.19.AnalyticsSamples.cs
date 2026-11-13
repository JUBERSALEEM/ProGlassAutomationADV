using System.ComponentModel;
using System.Windows.Input;
using ProGlassAutomation.Analytics;

namespace ProGlassAutomation.ThemeStudio;

/// <summary>Deterministic demonstration only. Never represents live ERP records.</summary>
public sealed class AnalyticsSamples : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private static readonly string[] Months = ["May", "Jun", "Jul", "Aug", "Sep", "Oct"];
    private static ChartSeriesData S(string name, params double?[] values) => new(name, values);
    public IReadOnlyList<ChartKind> ChartKinds { get; } = Enum.GetValues<ChartKind>();
    public IReadOnlyList<string> Scenarios { get; } = ["Normal", "Zero and negative", "Missing points", "Empty", "Loading", "Error", "Invalid data"];
    private ChartKind selectedKind = ChartKind.GroupedColumns;
    private string selectedScenario = "Normal";
    public ChartKind SelectedKind { get => selectedKind; set { selectedKind = value; Changed(nameof(SelectedKind)); Changed(nameof(Preview)); } }
    public string SelectedScenario { get => selectedScenario; set { selectedScenario = value; Changed(nameof(SelectedScenario)); Changed(nameof(Preview)); } }
    public ICommand Reset { get; }
    public AnalyticsSamples() => Reset = new DelegateCommand(() => { SelectedScenario = "Normal"; SelectedKind = ChartKind.GroupedColumns; });
    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public ChartDataset Preview
    {
        get
        {
            bool circular = SelectedKind is ChartKind.Donut or ChartKind.Pie;
            var series = circular ? new[] { S("Standard", 52), S("Premium", 34), S("Specialty", 26), S("Other", 16) } :
                new[] { S("Orders", 22, 30, 27, 41, 38, 52), S("Target", 25, 28, 32, 36, 40, 46) };
            if (SelectedScenario == "Zero and negative") series = circular ? [S("Zero", 0), S("Positive", 24)] : [S("Balance", -12, 0, 15, -4, 20, 8)];
            if (SelectedScenario == "Missing points") series = circular ? [S("Known", 22), S("Other", 14)] : [S("Observations", 10, null, 16, null, 22, 18)];
            if (SelectedKind == ChartKind.Columns) series = series.Take(1).ToArray();
            if (SelectedScenario == "Empty") series = [];
            if (SelectedScenario == "Invalid data") series = [S("Invalid", double.NaN)];
            ChartState state = SelectedScenario == "Loading" ? ChartState.Loading : SelectedScenario == "Error" ? ChartState.Error : ChartState.Ready;
            return new("Interactive chart preview", SelectedKind, Months, series, "orders",
                $"Demo / {SelectedScenario}. Toggle series below; inspect values with the keyboard. Pie charts accept nonnegative segments only; missing-segment scenarios show known values, not invented values.", (SelectedScenario == "Zero and negative" ? "24" : SelectedScenario == "Missing points" ? "36" : "128") + "\norders", state);
        }
    }
    public ChartDataset Revenue { get; } = new("Revenue and target", ChartKind.GroupedColumns, Months,
        [S("Revenue", 186, 214, 198, 252, 267, 284), S("Target", 200, 210, 225, 240, 260, 280)], "AED thousands",
        "Demo: October revenue AED 284k versus AED 280k target. May–October snapshot.");
    public ChartDataset Trend { get; } = new("Order momentum", ChartKind.Lines, Months,
        [S("Orders", 68, 74, 70, 91, 108, 128), S("Completed", 62, 71, 66, 85, 101, 120)], "orders",
        "Demo: orders increased from 68 in May to 128 in October; completed orders reached 120.");
    public ChartDataset Mix { get; } = new("Order mix", ChartKind.Donut, [],
        [S("Standard", 52), S("Premium", 34), S("Specialty", 26), S("Other", 16)], "orders",
        "Demo: 128 orders — Standard 52, Premium 34, Specialty 26, Other 16.", "128\norders");
    public ChartDataset Channels { get; } = new("Channel performance", ChartKind.HorizontalBars,
        ["Trade", "Direct", "Projects", "Partners"], [S("Orders", 42, 36, 31, 19)], "orders",
        "Demo: Trade 42, Direct 36, Projects 31, Partners 19 orders.");
    public ChartDataset Capacity { get; } = new("Capacity allocation", ChartKind.StackedColumns, Months,
        [S("Scheduled", 40, 42, 35, 48, 52, 55), S("Available", 20, 18, 25, 12, 8, 5)], "slots",
        "Demo: total capacity 60 slots each month; October has five slots available.");
    public ChartDataset Area { get; } = new("Workload outlook", ChartKind.Area, Months,
        [S("Confirmed", 28, 35, 32, 48, 45, 58), S("Forecast", 32, 39, 42, 51, 57, 64)], "jobs",
        "Demo: confirmed workload 58 jobs in October; forecast 64. Shaded areas are not stacked.");
    public ChartDataset Spark { get; } = new("Conversion trend", ChartKind.Sparkline, Months,
        [S("Conversion", 56, 58, 54, 64, 69, 72)], "percent", "Demo: conversion improved from 56% to 72%.");
    public ChartDataset KpiRevenueSpark { get; } = new("Revenue spark", ChartKind.Sparkline, Months,
        [S("Revenue", 186, 214, 198, 252, 267, 284)], "AED thousands", "Demo: October revenue AED 284k.");
    public ChartDataset KpiOrdersBars { get; } = new("Orders bars", ChartKind.Columns, Months,
        [S("Orders", 68, 74, 70, 91, 108, 128)], "orders", "Demo: orders reached 128 in October.");
    public ChartDataset KpiOnTimeSpark { get; } = new("On-time spark", ChartKind.Sparkline, Months,
        [S("On-time", 91, 93, 92, 95, 96, 98)], "percent", "Demo: on-time delivery 98%.");
    public IReadOnlyList<AnalyticsRow> Rows { get; } = [
        new("PG-1042", "Trade / North", "Ready", "AED 18,400", "Today"),
        new("PG-1041", "Direct / Central", "In review", "AED 12,800", "Tomorrow"),
        new("PG-1039", "Projects / West", "Scheduled", "AED 31,600", "24 Oct"),
        new("PG-1038", "Partners / East", "On hold", "AED 8,200", "26 Oct")];
}
public sealed record AnalyticsRow(string Reference, string Account, string Status, string Amount, string Due);
