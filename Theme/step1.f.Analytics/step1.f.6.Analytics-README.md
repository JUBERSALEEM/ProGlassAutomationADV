# step1.f — reusable native WPF analytics presentation

This branch belongs to the approved Theme Studio/Bento analytics expansion, **not a new ERP module**. The controls have no dependency on Studio, sample data, business totals, databases or theme identity. Bento owns all palettes/templates in `ThemeBento/step1.e.Components/step1.e.10.Analytics.xaml` and both appearance dictionaries. The shared theme engine only validates typed semantic roles/style targets and orchestrates replacement.

## Files

- f.1: immutable chart/series snapshots, kinds, presentation states and validation.
- f.2: chart dependency-property adapter for external data and theme-owned paints/sizes.
- f.3: native WPF chart host, keyboard legend toggles, summaries, source-value inspection and explicit states.
- f.4: LiveCharts series/axis/tooltip adapter; no domain calculations or hardcoded palette.
- f.5: small reusable KPI control with theme-owned template and an automation-readable name.
- f.6: this usage/decision guide.
- f.7: upstream MIT notice (retain with distributed applications).

## One selected chart dependency

`LiveChartsCore.SkiaSharpView.WPF` is pinned to **2.0.4**, MIT. Compared actual GitHub latest releases on 2026-10-20: [LiveCharts2 2.0.4](https://github.com/Live-Charts/LiveCharts2/releases/tag/2.0.4), published May 16, 2026; [ScottPlot 5.1.59](https://github.com/ScottPlot/ScottPlot/releases/tag/5.1.59), June 22, 2026; [OxyPlot 2.2.0](https://github.com/oxyplot/oxyplot/releases/tag/v2.2.0), September 20, 2024. Release dates alone are not a quality score. LiveCharts was chosen for its maintained WPF host, typed nullable series, grouped/stacked bars, pies and existing pointer inspection; no custom charting engine or second chart package is justified.

Inspected the pinned [WPF project](https://github.com/Live-Charts/LiveCharts2/blob/2.0.4/src/skiasharp/LiveChartsCore.SkiaSharp.WPF/LiveChartsCore.SkiaSharpView.WPF.csproj), [license](https://github.com/Live-Charts/LiveCharts2/blob/2.0.4/LICENSE), [null-points sample](https://github.com/Live-Charts/LiveCharts2/blob/2.0.4/samples/ViewModelsSamples/General/NullPoints/ViewModel.cs), [axis implementation](https://github.com/Live-Charts/LiveCharts2/blob/2.0.4/src/LiveChartsCore/CoreAxis.cs), interfaces/series/paint and WPF rendering source. WPF package targets include .NET 8 Windows; the retained product TFM is `net8.0-windows7.0`, not a production Windows 7 support promise. Upstream source uses a newer C# compiler; the application consumes the compiled package, not that source. **Package restore and actual assembly compatibility are still unverified here.**

LiveCharts brings its core/SkiaSharp view dependencies, SkiaSharp.Views.WPF/native rendering assets, and HarfBuzz shaping transitively. No paid/Geared extension is selected. SkiaSharp is MIT-licensed with native Skia/HarfBuzz third-party notices. Before packaging, inspect the actual restored dependency graph (`dotnet list ProGlassAutomation.csproj package --include-transitive`) and retain every applicable upstream/native license notice; the f.7 MIT copy alone is not a complete transitive distribution audit. No binaries/releases are published by this work.

## Future page: supply data, not drawing code

A page view model supplies an immutable snapshot (calculate currency conversions, totals and metrics in the module, not in this control):

```csharp
public ChartDataset Volume { get; } = new(
    "Order volume", ChartKind.GroupedColumns, new[] { "May", "June" },
    new[] {
        new ChartSeriesData("Actual", new double?[] { 12, 18 }),
        new ChartSeriesData("Target", new double?[] { 15, 16 })
    }, "orders", "Actual orders rose from 12 to 18; June target was 16.");
```

```xml
<!-- xmlns:analytics="clr-namespace:ProGlassAutomation.Analytics" -->
<Border Style="{DynamicResource ChartCard}">
 <StackPanel>
  <TextBlock Text="Order volume" Style="{DynamicResource SectionHeading}" />
  <analytics:BentoChart Data="{Binding Volume}" />
 </StackPanel>
</Border>
```

Use `BentoKpi Label="Orders" Value="128" Detail="Demo snapshot"` with Tag Positive/Feature/Attention or the default Information variant. Supply values already formatted by the page. Tables use the themed native `DataGrid`/row/cell/header styles and module-defined columns. Header/caption/legend/tooltip are shared parts, not bespoke controls per card variation. A future registered theme must supply its own complete styles/paints for the same typed targets; no page rewrite or Bento fallback is required.

## Rules and behavior

- Columns/GroupedColumns use native `ColumnSeries`; multiple columns group automatically. Horizontal uses `RowSeries`; stacked uses `StackedColumnSeries`. Line/Area/Sparkline use native multi-series lines; area is overlapping translucent fill, **not stacked area**. Pie/Donut use `PieSeries`, with inner radius only for donut.
- Up to four distinct nonempty series names and 1,000 categories per panel. Aggregate larger data in the module; no downsampling/business calculations here. Arrays are snapshotted so callers cannot silently mutate a chart behind its binding. Replace `Data` and notify the binding for updates.
- Every cartesian series has exactly one finite number or `null` per category. Zero and negatives are valid; nullable values remain gaps, never invented zeros. Native library bounds/ticks and baseline handle scaling; sparklines omit visible axes. No fixed path/rectangle chart approximations remain.
- Pie/donut: one nonnegative finite number per segment; `null`/negative segments reject rather than mislead. All-zero segments produce an empty state. Center text, units and accessible summary are supplied by the caller; the renderer never computes business totals.
- Empty/all-null, Loading, Error, invalid shape/numbers/limits and absent data have explicit textual states. Pages own retry commands (the gallery resets its deterministic demo; it does not pretend to retry a backend). No autonomous fetching or fake loading timer.
- Pointer tooltips inspect native points. Keyboard CheckBoxes toggle real native series; labels/swatches, supplied summary and expandable plain source values provide non-color/pointer-only alternatives. Screen-reader/high-contrast certification remains pending Windows testing.
- Semantic Brush DPs adapt DynamicResource into Skia paints. Palette changes are dispatcher-coalesced; native series and legend controls are **not** rebuilt for a paint change. Visibility is keyed by series name. Theme style objects remain sealed/stable; DataGrid selections and external bindings are not replaced. Axes re-create on repaint; zoom/pan is intentionally not exposed, and data replacement may reset bounds.
- Axis/grid/tooltip/legend colors, text size/font, line/marker size, area opacity and donut radius all come from active resources. CPU rendering and zero animation are deliberate accessibility/runner choices. Native library handles canvas load/unload; there are no global component event subscriptions, timers, continuous custom animations or layout transforms. Native-paint lifetime/stress/performance still require measurement.

## Verification and remaining acceptance

Portable XML/source/isolation/contrast/mutation checks are not a C#/XAML compilation. `step1.d.8.AnalyticsChecks.cs` authors 63 kind/scenario combinations, negative/zero/null/invalid/limits/immutable-data tests, and native configuration/state, six-mode-switch, series/legend identity and table selection checks. They run through existing contract/runtime entry points **on Windows**, not here. Workflow remains inactive at `cols/ci/windows-wpf.yml`.

Required before acceptance: restore/build both actual projects; run compiled contracts and STA harness; launch the WPF app and inspect both modes, keyboard and open tooltips, category-label clipping, legend wrapping, 720–2560 widths, 100/150/200% DPI, screen readers/high contrast and sustained memory/render behavior. No rendered screenshot or performance proof exists from this Linux environment.
