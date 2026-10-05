# Dashboard 2050 kit — shared across 7 dashboards

Do **not** rewrite each dashboard layout. Consume these shared pieces.

## Modules
Overview · Sheet Store · Proforma · Job Order · Delivery Note · Tax Invoice · Statement

## Tokens (`Styles/DashboardStyles.xaml`)
- Cards / hero: **12 px** (`SoftCardRadius`, `SoftHeroRadius`) — same language as `CardCornerRadius`
- KPI: `SoftKpiTileTemplate` / `SoftKpiCardTemplate`
- Chart well: `SoftChartWellStyle`
- Pills: `SoftPillButtonStyle` (not 999)

## Charts (no LiveCharts / no NuGet)
| Control | Use |
|---|---|
| `Controls/TrendChart/TrendChartControl` | Bar / line / area trend |
| `Controls/MiniBarChart/MiniBarChartControl` | Spark bars inside a KPI |
| `Controls/DonutChart/DonutChartControl` | Mix / share ring |

Bind donut to `IEnumerable<ChartSliceModel>`:

```xml
<donut:DonutChartControl
    Height="180"
    Slices="{Binding DeliveryMixSlices}"
    HoleLabel="MIX"
    EmptyMessage="No movement yet."
    Thickness="20"/>
```

`ChartSliceModel.Accent` = `Green | Blue | Amber | Red` (theme brushes). Do not hardcode hex on the slice if Dark theme must work.

## Pilot
Overview now hosts the shared donut (`DeliveryMixSlices`). Copy that card into the other six when ready — same style keys, same control.
