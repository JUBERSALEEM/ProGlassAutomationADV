using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Data;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView.WPF;

namespace ProGlassAutomation.Analytics;

/// <summary>Reusable chart presentation; no demo data, business arithmetic, palette or resource lookup.</summary>
public sealed partial class BentoChart
{
    private readonly CartesianChart cartesian = new() { AnimationsSpeed = TimeSpan.Zero, LegendPosition = LegendPosition.Hidden };
    private readonly PieChart pie = new() { AnimationsSpeed = TimeSpan.Zero, LegendPosition = LegendPosition.Hidden };
    private readonly Grid plot = new();
    private readonly Grid root = new() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
    private readonly Grid sideBody = new() { MinHeight = 0, ClipToBounds = true };
    private readonly DockPanel sideHost = new() { LastChildFill = true };
    private readonly FillSlot plotSlot = new();
    private readonly WrapPanel legend = new();
    private readonly DockPanel footer = new() { LastChildFill = true };
    private readonly TextBlock message = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock center = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center };
    private readonly CheckBox inspectToggle = new() { Content = "Show source values" };
    private readonly TextBlock values = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private readonly TextBlock summary = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Dictionary<string, bool> visibility = new();
    private ISeries[] rendered = [];
    private bool dataDirty = true;
    private bool queued;
    private bool sideLegend;
    public IReadOnlyList<ISeries> RenderedSeries => rendered;
    public string DisplayMessage => message.Text;
    static BentoChart()
    {
        // Skia GPU surfaces clear to black and BitmapCache cannot keep a transparent plot.
        LiveCharts.RenderingSettings.UseGPU = false;
        LiveCharts.RenderingSettings.TryUseVSync = false;
    }
    public BentoChart()
    {
        VerticalContentAlignment = VerticalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        cartesian.HorizontalAlignment = HorizontalAlignment.Stretch;
        cartesian.VerticalAlignment = VerticalAlignment.Stretch;
        cartesian.MinHeight = 0;
        pie.HorizontalAlignment = HorizontalAlignment.Stretch;
        pie.VerticalAlignment = VerticalAlignment.Stretch;
        pie.MinHeight = 0;
        plot.MinHeight = 0;
        cartesian.Background = System.Windows.Media.Brushes.Transparent;
        pie.Background = System.Windows.Media.Brushes.Transparent;
        plot.Background = System.Windows.Media.Brushes.Transparent;
        Background = System.Windows.Media.Brushes.Transparent;
        BorderBrush = System.Windows.Media.Brushes.Transparent;
        plot.Children.Add(cartesian);
        plot.Children.Add(pie);
        plot.Children.Add(center);
        inspectToggle.VerticalAlignment = VerticalAlignment.Center;
        inspectToggle.HorizontalAlignment = HorizontalAlignment.Left;
        inspectToggle.MinHeight = 0;
        inspectToggle.Padding = new Thickness(4, 0, 0, 0);
        values.Margin = new Thickness(0, 8, 0, 8);
        LayoutChrome(circular: false);
        inspectToggle.Checked += (_, _) => values.Visibility = Visibility.Visible;
        inspectToggle.Unchecked += (_, _) => values.Visibility = Visibility.Collapsed;
        Content = root;
        center.SetBinding(TextBlock.FontSizeProperty, new Binding(nameof(CenterFontSize)) { Source = this });
        values.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(AxisBrush)) { Source = this });
        summary.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(AxisBrush)) { Source = this });
        message.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(AxisBrush)) { Source = this });
        Loaded += (_, _) => Schedule(true);
        IsVisibleChanged += (_, _) => { if (IsVisible) Schedule(false); };
        SizeChanged += (_, _) => { if (IsLoaded) Schedule(false); };
    }

    /// <summary>
    /// Donut: ticks stay beside the pie. Toggle and source values Dock.Bottom.
    /// The pie lives in FillSlot (desired height 0) so LiveCharts min-size cannot
    /// inflate the host and clip Other on the Home-top Mix cell.
    /// Cartesian: plot fills the card; footer under the plot.
    /// </summary>
    private void LayoutChrome(bool circular)
    {
        bool side = circular && ShowLegend;
        if (side == sideLegend && root.Children.Count > 0) return;
        sideLegend = side;
        legend.Orientation = side ? Orientation.Vertical : Orientation.Horizontal;
        legend.VerticalAlignment = VerticalAlignment.Top;
        plotSlot.Child = null;
        Detach(plot); Detach(legend); Detach(inspectToggle); Detach(message); Detach(summary); Detach(values);
        Detach(footer); Detach(sideBody); Detach(plotSlot); Detach(sideHost);
        root.Children.Clear();
        root.RowDefinitions.Clear();
        root.ColumnDefinitions.Clear();
        footer.Children.Clear();
        sideBody.Children.Clear();
        sideBody.ColumnDefinitions.Clear();
        sideHost.Children.Clear();
        if (side)
        {
            inspectToggle.HorizontalAlignment = HorizontalAlignment.Left;
            inspectToggle.Margin = new Thickness(0, 8, 0, 0);
            DockPanel.SetDock(values, Dock.Bottom);
            DockPanel.SetDock(inspectToggle, Dock.Bottom);
            DockPanel.SetDock(message, Dock.Bottom);
            DockPanel.SetDock(summary, Dock.Bottom);
            sideHost.Children.Add(values);
            sideHost.Children.Add(inspectToggle);
            sideHost.Children.Add(message);
            sideHost.Children.Add(summary);
            sideBody.MinHeight = 0;
            sideBody.ClipToBounds = true;
            sideBody.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 96 });
            sideBody.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 108 });
            Grid.SetColumn(plot, 0);
            Grid.SetColumn(legend, 1);
            sideBody.Children.Add(plot);
            sideBody.Children.Add(legend);
            plotSlot.Child = sideBody;
            sideHost.Children.Add(plotSlot);
            root.Children.Add(sideHost);
        }
        else
        {
            inspectToggle.HorizontalAlignment = HorizontalAlignment.Right;
            inspectToggle.Margin = new Thickness(4, 0, 0, 0);
            DockPanel.SetDock(inspectToggle, Dock.Right);
            footer.Children.Add(inspectToggle);
            footer.Children.Add(legend);
            footer.Visibility = Visibility.Visible;
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 0 });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(plot, 0); Grid.SetColumn(plot, 0); Grid.SetColumnSpan(plot, 1);
            Grid.SetRow(footer, 1); Grid.SetColumn(footer, 0); Grid.SetColumnSpan(footer, 1);
            Grid.SetRow(message, 2); Grid.SetColumn(message, 0); Grid.SetColumnSpan(message, 1);
            Grid.SetRow(summary, 3); Grid.SetColumn(summary, 0); Grid.SetColumnSpan(summary, 1);
            Grid.SetRow(values, 4); Grid.SetColumn(values, 0); Grid.SetColumnSpan(values, 1);
            root.Children.Add(plot);
            root.Children.Add(footer);
            root.Children.Add(message);
            root.Children.Add(summary);
            root.Children.Add(values);
        }
    }

    private static void Detach(FrameworkElement child)
    {
        if (child.Parent is Panel current) current.Children.Remove(child);
        else if (child.Parent is Decorator decorator) decorator.Child = null;
    }

    /// <summary>
    /// Measures as zero height so docked Mix source lines keep their space.
    /// Arranges the pie into whatever height remains after those lines.
    /// </summary>
    private sealed class FillSlot : Decorator
    {
        protected override Size MeasureOverride(Size constraint)
        {
            Child?.Measure(constraint);
            double width = double.IsInfinity(constraint.Width) ? 0 : constraint.Width;
            return new Size(width, 0);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            Child?.Arrange(new Rect(arrangeSize));
            return arrangeSize;
        }
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty || e.Property == FontFamilyProperty || e.Property == ShowLegendProperty) Schedule(false);
        if (e.Property == DataProperty) Schedule(true);
    }

    private void Schedule(bool rebuild)
    {
        dataDirty |= rebuild;
        if (queued || !IsLoaded) return;
        queued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            queued = false;
            if (!IsLoaded) return;
            Refresh();
        }));
    }

    private void Refresh()
    {
        summary.Text = Data?.Summary ?? "No chart data supplied.";
        AutomationProperties.SetName(this, Data?.Title ?? "Chart");
        AutomationProperties.SetHelpText(this, summary.Text);
        string? error = Data?.ValidationError();
        message.Text = Data?.State == ChartState.Loading ? "Loading data…" : Data?.State == ChartState.Error
            ? "Data could not be loaded. Use the page's retry action." : error ?? (Data is null || Data.IsEmpty ? "No observations available for this selection." : "");
        bool ready = message.Text.Length == 0;
        bool circular = Data?.Kind is ChartKind.Donut or ChartKind.Pie;
        LayoutChrome(circular);
        cartesian.Visibility = ready && !circular ? Visibility.Visible : Visibility.Collapsed;
        pie.Visibility = ready && circular ? Visibility.Visible : Visibility.Collapsed;
        center.Visibility = ready && Data?.Kind == ChartKind.Donut ? Visibility.Visible : Visibility.Collapsed;
        center.Text = Data?.CenterText ?? "";
        if (!ready)
        {
            cartesian.Series = []; pie.Series = []; rendered = []; legend.Children.Clear();
            dataDirty = true;
            inspectToggle.Visibility = Visibility.Collapsed;
            legend.Visibility = Visibility.Collapsed;
            footer.Visibility = Visibility.Collapsed;
            values.Text = ""; message.Visibility = Visibility.Visible;
            return;
        }
        inspectToggle.Visibility = ShowLegend ? Visibility.Visible : Visibility.Collapsed;
        legend.Visibility = ShowLegend ? Visibility.Visible : Visibility.Collapsed;
        footer.Visibility = ShowLegend && !sideLegend ? Visibility.Visible : Visibility.Collapsed;
        message.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        if (dataDirty)
        {
            values.Text = string.Join("\n", Data!.Series.Select(s => s.Name + ": " + string.Join(" / ", s.Values.Select((v, i) => $"{(i < Data.Categories.Count ? Data.Categories[i] : s.Name)} {(v.HasValue ? v.Value.ToString("N1") : "missing")} {Data.Unit}"))));
            rendered = BuildSeries(Data!);
            cartesian.Series = circular ? [] : rendered;
            pie.Series = circular ? rendered : [];
            BuildLegend(Data!);
            dataDirty = false;
        }
        summary.Visibility = legend.Children.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        ApplyPaints(Data!);
    }

    private void BuildLegend(ChartDataset data)
    {
        legend.Children.Clear();
        foreach (string stale in visibility.Keys.Except(data.Series.Select(s => s.Name)).ToArray()) visibility.Remove(stale);
        for (int i = 0; i < rendered.Length; i++)
        {
            var series = rendered[i];
            var swatch = new Border { VerticalAlignment = VerticalAlignment.Center };
            swatch.SetBinding(BackgroundProperty, new Binding($"Series{i + 1}") { Source = this });
            swatch.SetBinding(WidthProperty, new Binding(nameof(FontSize)) { Source = this });
            swatch.SetBinding(HeightProperty, new Binding(nameof(FontSize)) { Source = this });
            var caption = new TextBlock { Text = "  " + data.Series[i].Name };
            caption.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });
            var label = new StackPanel { Orientation = Orientation.Horizontal };
            label.Children.Add(swatch); label.Children.Add(caption);
            var item = new CheckBox
            {
                Content = label,
                IsChecked = visibility.GetValueOrDefault(data.Series[i].Name, true),
                MinHeight = 0,
                FontSize = FontSize,
                Padding = new Thickness(2, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            series.IsVisible = item.IsChecked == true;
            AutomationProperties.SetName(item, $"Show series {data.Series[i].Name}");
            item.Checked += (_, _) => { series.IsVisible = true; visibility[series.Name!] = true; };
            item.Unchecked += (_, _) => { series.IsVisible = false; visibility[series.Name!] = false; };
            item.ToolTip = string.Join(", ", data.Series[i].Values.Select((v, n) => $"{(n < data.Categories.Count ? data.Categories[n] : data.Series[i].Name)}: {(v.HasValue ? v.Value.ToString("N1") : "missing")} {data.Unit}"));
            legend.Children.Add(item);
        }
    }
}
