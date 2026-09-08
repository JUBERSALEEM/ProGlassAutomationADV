# Dashboard Module

Multi-page dashboard shell with a segmented module picker. Seven module
dashboards, one design grammar.

```
Views/Dashboard/
    DashboardView.xaml            shell: segmented module picker + ContentPresenter
    Overview/                     business overview + order-to-cash pipeline
    SheetStore/                   stock, value, category mix
    ProformaInvoice/              quotes, status, top customers
    JobOrder/                     production workload, priority, recent jobs
    DeliveryNote/                 dispatch, delivery type, recent movements
    TaxInvoice/                   revenue, ageing, collections
    Statement/                    customer balances
    README.md
```

## The grammar — Styles/DashboardKit.xaml

**One design language, prefixed `Db`.** A dashboard page either uses `Db*`
keys everywhere or it is not on the grammar.

This replaced three stacked generations in `Styles/DashboardStyles.xaml`
(`Dash*` → `Soft*` → `IndustrialDashboard*`) that gave the app 4 card
styles, 4 KPI tile templates, 3 page paddings and 2 type scales at once.
Each page picked a different subset, so switching tabs changed the page
margin, card weight, KPI tile shape and type scale together — which is
what made the set read as unfinished rather than any one screen being
badly drawn. 28 keys became dead in the move and were deleted.

### The contract every page follows

```
row 0   hero          title · subtitle · live chip · refresh stamp
row 1   states        loading band, error band
row 2   KPI band      UniformGrid of DbKpiTileTemplate
row 3   primary       DbChartWellStyle + chart, with an empty state
row 4   detail        DbRowCardStyle feed, table or secondary charts
```

All four states — **loading, error, empty, data** — exist on every page.
A page that can show data must also be able to say "there is none yet"
instead of rendering a blank card, which reads as a crash.

### Key groups

| Group | Keys |
|---|---|
| Rhythm | `DbPageMargin` `DbCardPadding` `DbTilePadding` `DbGutter` `DbGutterHalf` `DbKpiTileMinHeight` `DbChartMinHeight` |
| Type | `DbTitleStyle` `DbSubtitleStyle` `DbSectionTitleStyle` `DbSectionSubStyle` `DbMetaStyle` `DbKpiLabelStyle` `DbKpiValueStyle` `DbKpiFootStyle` `DbGlyphStyle` |
| Surfaces | `DbCardStyle` `DbTileStyle` `DbChartWellStyle` `DbRowCardStyle` `DbSectionHeadStyle` |
| Controls | `DbChipStyle` `DbBadgeStyle` `DbPillStyle` `DbPillTextStyle` `DbActionButtonStyle` `DbActionButtonAccentStyle` |
| KPI | `DbKpiTileTemplate` `DbKpiBandPanel` `DbKpiBandStyle` `DbKpiRowPanel` |
| Feed | `DbFeedRowTemplate` `DbFeedItemsStyle` |
| States | `DbStateBandStyle` `DbStateTextStyle` `DbErrorTextStyle` `DbEmptyGlyphStyle` `DbEmptyTextStyle` |
| Pipeline | `DbPipelineStageTemplate` `DbPipelinePanel` `DbPipelineItemsStyle` `DbPipelineCardStyle` `DbPipelineConnectorStyle` `DbPipelineNameStyle` `DbPipelineValueStyle` `DbPipelineFootStyle` |

### Rules

- One type scale: the app's `Text*` tokens. No literal `FontSize` in a page.
- Every colour is a `DynamicResource`, so Light/Dark swaps live.
- No cross-system leaks. The old feed row borrowed
  `GlassBorderGradientCrystal` from the Crystal design system, so a
  soft/industrial card had a crystal edge.
- KPI values sit in a `Viewbox` with `StretchDirection="DownOnly"`: a
  short count renders full size, a long currency value scales down to
  fit. Money must never be truncated.

## One KPI tile, two models

The app has two KPI POCOs — `KpiCardModel` (Title, SubLabel, BadgeColor)
and `KpiTileModel` (Label, Accent) — which forced two templates with
different margins, so KPI bands did not line up between tabs.

`KpiTileModel` now exposes `Title`, `SubLabel` and `BadgeColor` as
computed aliases over its own members, so **`DbKpiTileTemplate` renders
both**. No ViewModel construction site changed. The models should
eventually be merged into one; the aliases are the bridge until then.

## Order-to-cash pipeline (Overview only)

`Quote → Production → Delivery → Invoice → Payment`, rendered as one
connected rail of stage cards (`WorkflowStageModel`).

- Stages 1–3 come from `ExecutiveMetrics`, which Overview already loads.
- Stages 4–5 come from `ITaxInvoiceMetricsService`. It is an optional
  constructor dependency: if it is unavailable the rail renders those
  stages with an em dash rather than a zero — a zero asserts "no work
  here", which is a different claim from "could not read it".
- Each stage counts **live** work in that handoff, not a lifetime total.
- A stage escalates to a danger chip only when it is actually carrying
  something overdue; a healthy rail is not red.

Only the Overview carries the rail. It is the whole book of work, so it
belongs where the whole book is summarised.

## Shell navigation

`DashboardViewModel` holds `Pages` (7 × `DashboardPageBase`) and
`ActivePage`. `SelectTabCommand` sets `ActivePage`; its setter calls
`LoadAsync()` on the newly selected page, and the `ContentPresenter`
swaps to the matching `DataTemplate`. Selection is decided by
`ReferenceEqualityConverter`, so there is no `IsSelected` flag to keep in
sync. Sub-pages load lazily, on first selection.

`DashboardPageBase` provides `IsLoading`, `HasError`, `ErrorMessage`,
`LastRefreshed`, `TabTitle`, `TabIcon`, `TabSubtitle` and a
reentrancy-guarded `LoadAsync()`.

**`LastRefreshed` has exactly one writer: `DashboardPageBase.LoadAsync`.**
JobOrder and DeliveryNote used to shadow it with `new` to get their own
format, which made the base's write dead code, showed the refresh time
three different ways across the seven tabs, and left any
`DashboardPageBase` reference reading an empty string. Do not redeclare
it — override the format in the base instead.

## Charts (pure WPF, no NuGet)

`Controls/TrendChart/TrendChartControl`, `Controls/DonutChart/DonutChartControl`,
`Controls/MiniBarChart/MiniBarChartControl`. Chart bars take
`ChartBarModel`; donuts take `ChartSliceModel` (`Accent` =
Green|Blue|Amber|Red).

## Data boundary

```
View → ViewModel → IDashboardMetricsService → DbContext → SQLite
```

A ViewModel asks for aggregate DTOs and never receives an EF entity, so
it cannot hold a table in memory or reach a `FullSnapshotJson` blob.
Every metrics method takes a `DashboardDateRange` and a
`CancellationToken`.

## Adding a module dashboard

1. `ViewModels/Dashboard/YourModule/YourModuleDashboardViewModel.cs`
   extending `DashboardPageBase` — override `TabTitle`, `TabIcon`,
   `TabSubtitle`, `LoadCoreAsync()`.
2. `Views/Dashboard/YourModule/YourModuleDashboardView.xaml` + `.cs`,
   following the five-row contract above and using `Db*` keys only.
3. Add a `DataTemplate` in `DashboardView.xaml` Resources.
4. Add an instance to `DashboardViewModel.Pages`.

## Verification

There is no .NET SDK in the sandbox this was written in, so nothing here
has been compiled. `scripts/verify-dashboard.py` is the static stand-in and
checks, across the repo: XAML well-formedness; every
`StaticResource`/`DynamicResource` resolves; every binding head resolves
to the page VM or a known row model; `Style=` is never applied to an
element of the wrong type; legacy-key / hex / literal-FontSize discipline
per page; and brace balance on touched C#. Run it before committing and
build on Windows before shipping.
