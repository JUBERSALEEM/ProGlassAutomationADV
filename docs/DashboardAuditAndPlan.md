# Dashboard Audit & Implementation Plan

Audit performed against the master prompt "Premium Multi-Dashboard System".
Findings are from reading the actual solution, not assumptions.

Status at time of audit: HEAD `9a29c13`, working tree clean, 124 XAML files
pass well-formedness / stray-attribute / property-element-order checks.

---

## 1. What already exists (reuse, do not rebuild)

| Layer | State | Verdict |
|---|---|---|
| Repositories (18 files, interface-backed) | Mature: `IProformaInvoiceRepository`, `IJobOrderRepository`, `IDeliveryNoteRepository`, `ITaxInvoiceRepository`, `IStatementRepository`, `ISheetStoreRepository`, `IMasterDataRepository` | **Reuse as-is** |
| `Services/Dashboard/TaxInvoiceMetrics` | Correct pattern: `AsNoTracking()` + `.Select()` projection + SQL-side `GroupBy`, returns aggregate DTOs | **Use as the template for all other modules** |
| Theme system | `DarkTheme.xaml` + `LightTheme.xaml`, semantic keys already present, runtime swap via `ThemeManager.ApplyTheme` | **Reuse** — semantic names already exist |
| Design system | `Styles/DashboardStyles.xaml`: 6-step type scale, 4px grid, card/tile/pill/KPI/button styles | **Reuse and extend** |
| Dashboard shell | `DashboardViewModel` + `DashboardPageBase` (async `LoadAsync`, reentrancy guard via `Interlocked`, `IsLoading` / `HasError` / `ErrorMessage` / `LastRefreshed`) | **Reuse** — already matches the prompt's per-widget state requirement |
| Audit / logging | `Services/Audit`, `Services/Logging` | **Reuse for the Operations dashboard** |
| Icon system | Segoe MDL2 Assets, enforced by `DashGlyphStyle` | **Reuse** — 0 emoji remain |

The prompt's requested `DashboardShell`, `DashboardEmptyState`, `DashboardLoadingState`,
`DashboardErrorState` largely exist already as `DashboardPageBase` + the styles in
`DashboardStyles.xaml`. Creating new controls with those names would be the
"second competing implementation" the prompt explicitly forbids.

---

## 2. Real gaps found

### GAP 1 — No dashboard service layer (highest impact)

The prompt requires `View → ViewModel → Dashboard Service → Repository → DB`.
Actual chain today is `View → ViewModel → Repository → DB`.

Six of seven dashboards call `GetAllAsync()` and aggregate **in memory**:

```
Overview      4 x GetAllAsync   (Invoices, Sheets, Jobs, DeliveryNotes)
DeliveryNote  2 x
Proforma      1 x
JobOrder      1 x
SheetStore    1 x
Statement     1 x
```

`ProformaInvoiceRepository.GetAllAsync` materialises every column of every row,
including `FullSnapshotJson` — a full invoice snapshot blob — purely to count
invoices and sum totals. Opening Overview loads four entire tables.

Only 2 of 18 repositories use `AsNoTracking`, so EF also builds change-tracking
entries for rows that are read-only aggregates.

This is the single biggest cause of the prompt's "dashboard must remain smooth"
concern, and it is invisible in a small dev database.

### GAP 2 — No centralised date-range / filter state

No `IDashboardFilterService`, no date-range type anywhere. Ranges are hardcoded
per widget (`monthsBack = 6`, `limit = 5`). The prompt's Today / Week / Month /
Quarter / 6M / Year / Custom selector has nothing to bind to.

### GAP 3 — Charts are hand-rolled primitives

No charting library. Charts are `Border` elements with a `Height` bound through
`ScaleConverter`. That yields no tooltips, no hover, no crosshair, no axes, no
animation, and no empty/loading state.

The prompt asks for LINE / AREA / STACKED BAR / DONUT / SPARKLINE with hover and
transitions. None of those are expressible in the current approach.

### GAP 4 — Aggregate correctness is unverified

`DnDashboardViewModel` carries the comment *"Each DN stores a point-in-time
snapshot — summing double-counts."* That is a real accounting hazard and it is
handled ad hoc inside a ViewModel rather than in a service with tests.

### GAP 5 — Metrics the prompt wants that cannot be computed today

Verified absent from the data model: **payables**, **cash flow**, **purchase
trend**, **lost quotations**, **quotation conversion rate** (no quotation-vs-order
state machine), **fast/slow-moving materials** (needs consumption history over
time), **previous-period comparison** for every KPI (nothing stores or queries a
prior window).

Per the prompt's own rule — *never fabricate business data* — these must either
get a real data/service implementation or be omitted. They must not be rendered
as `AED 0`.

---

## 3. What is NOT a problem (checked, no action)

- **Theme adaptivity** — 0 non-shadow hardcoded colours in dashboard markup; every
  `DynamicResource` key verified present in both themes.
- **Icons** — 0 emoji across dashboard views and ViewModels.
- **UI-thread violations** — all loads go through `async LoadCoreAsync`;
  `DashboardPageBase` marshals theme callbacks via `Dispatcher`.
- **Event-handler leak** — `ThemeManager.ThemeChanged` is subscribed once with no
  matching `-=`, but `DashboardViewModel` is constructed exactly once in
  `MainViewModel`, so the subscription lives for the app lifetime. Not a growing
  leak. Worth an `IDisposable` for correctness, not urgency.
- **Per-widget error isolation** — already provided by `DashboardPageBase`
  (`try/catch` → `HasError` + `ErrorMessage`, guarded by `Interlocked`).

---

## 4. Implementation plan (incremental, build-verified per stage)

Ordered so that each stage is independently shippable and nothing is left
half-migrated.

### Stage 1 — Dashboard service layer *(foundation; do first)*
Add `Services/Dashboard/{Sales,Production,Inventory,Finance,Activity}Metrics`,
each modelled on the existing `TaxInvoiceMetricsService`:
`AsNoTracking()`, `.Select()` projection, SQL-side `GroupBy`/`Count`/`Sum`,
`CancellationToken`, aggregate DTO return.
Migrate the six dashboards off `GetAllAsync()` one at a time, leaving behaviour
identical. **No visual change in this stage** — it is measurable and low-risk.

### Stage 2 — Filter state service
`IDashboardFilterService` holding the selected range, raising a change event.
`DashboardPageBase` subscribes and re-queries. One filter bar in the shell.

### Stage 3 — Chart control
Decide library vs custom (see question below), then build `DashboardChartCard`
wrapping it with title / range / tooltip / empty / loading / error states.
Replace hand-rolled bars per dashboard.

### Stage 4 — KPI comparison
Extend the KPI DTO with previous-period value + delta, computed in the service.
Extend `DashKpiTileTemplate` with delta and sparkline. Communicate good/bad with
icon **and** colour, not colour alone (accessibility requirement).

### Stage 5 — Dashboard content redesign
Only now reorganise Executive / Sales / Production / Inventory / Finance /
Operations into the prompt's row structure, with real metrics behind each widget.

### Stage 6 — Verification sweep
Duplicate keys, duplicate `x:Name`, binding errors, unused namespaces,
resolution checks at 1280×720 → 4K, both themes.

---

## 5. Blocking questions (cannot be answered from the code)

1. **Chart library** — a real chart control (LiveCharts2, ScottPlot, OxyPlot) is a
   new third-party dependency in a project that currently has none for charting.
   Custom-drawn charts avoid the dependency but cost far more code for tooltips,
   axes and animation. Which is acceptable?
2. **Missing metrics** — payables, cash flow, quotation conversion and
   fast/slow-moving stock have no backing data. Build the data layer for them, or
   omit those widgets?
3. **Dashboard count** — the prompt names 6 dashboards; the app has 7 organised
   by *module* (Proforma, Job Order, Delivery Note, Tax Invoice, Statement, Sheet
   Store, Overview) rather than by *business function* (Sales, Production,
   Finance). Reorganising changes navigation and user habits. Restructure, or
   keep module alignment and deepen each?

---

## 6. Recommendation

Do Stage 1 first regardless of the answers above. It is invisible to the user,
carries the lowest risk, is independently verifiable, and fixes the one defect
that will actually bite on a real customer database — loading four full tables,
snapshot JSON included, to render a handful of numbers.

Cosmetic redesign on top of in-memory aggregation would make the dashboard look
premium and still stall on a large dataset.
