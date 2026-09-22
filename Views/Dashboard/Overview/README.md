# Indigo Studio Overview

Production WPF implementation of the approved Indigo Studio design, based on the requested reset to `61a9ce1e4e6892ca6f48ac5989831ecfc7ad57b3`. No generated concept images, preview generators, sample records, or new chart dependencies are included.

## Scope

- `OverviewDashboardView.xaml`: financial panel and sparkline, four unit-labelled delivery counters, three operational stats, area trend, complete recent-document feed, five-stage rail, and four full KPI cards.
- `OverviewDashboardView.xaml.cs`: view-only responsive layout; never changes the DataContext or data collections.
- `IndigoStudioResources.xaml`: explicitly keyed `Ov.*` styles and warm-white/lilac/plum palette, 18-DIP cards, focus/hover/disabled states, row templates and loading skeleton.
- `IndigoStatusBrushConverter.cs`: preserves existing semantic status keys but resolves them into the page's palette rather than picking incompatible global dark-theme badge colours.
- `EmptyDeliverySnapshotConverter.cs`: zero-movement messaging without unsafe collection-index bindings or treating unavailable data as zero.
- `../DashboardView.xaml`: coordinated ProGlass header and seven-module navigation; original page mappings, selection identity, commands, user and refresh bindings retained. Other module bodies and application theme dictionaries are unchanged.

## Data contract

The redesign retains all **23 distinct original Overview binding paths**, all **9 distinct shell shorthand binding paths**, and the shell's explicit active-page/item identity MultiBinding. KPI, document and pipeline templates retain every original row-model binding.

All **17 KPI rows** are supplied by the original collections: Sales 4, Production 4, Inventory 4, Delivery 5. Stacked KPI item panels have no fixed height or maximum height. The complete page scrolls vertically; the complete recent-document feed retains its bounded inner scroller and smart wheel bubbling. The existing feed limit of 20 is not reduced to the concept's six examples.

Metric services, view models, models, charts, optimizer, persistence and application settings are unchanged from the reset baseline. In particular:

- Proforma values include VAT; they are **not collected revenue**.
- The four delivery counters are independent numbers with notes/pieces units, not a mixed-unit percentage donut.
- Sent-out notes include delivered/signed/received notes. The pipeline's delivery count retains its different current-work scope.
- Payment is the paid/part-paid **invoice issue-month cohort**, not receipts this month.
- Missing pending-piece/tax data continues to display an em dash, not an invented zero.
- Loading, errors, empty chart/feed and zero-delivery states remain explicit. Failed loads do not show a healthy-looking zero dashboard.
- The header says “Snapshot” and retains the actual refresh timestamp; no synthetic live or sample-data claim is made.
- New proforma, Jobs, Deliveries and module tabs use the original commands. No fake export/filter/action is added.

## Responsive layout (available view width, DIPs)

| Section | Wide | Narrow |
|---|---|---|
| Brand / signed-in user | Single row at 720+ | Brand above subtitle/user row |
| Header actions | Side-by-side at 1100+ | Below heading |
| Financial / delivery | Side-by-side at 880+ | Stacked |
| Delivery counters | Four when the card can fit them | Two-by-two |
| Operational stats | Three at 600+ | One per row |
| Stats + trend / recent feed | Feed spans both left-hand rows at 1050+ | Stats, chart, feed stacked |
| Pipeline | Five at 950+ | Vertical, downward connectors |
| KPI groups | Four at 1180+, two at 680+ | One below 680 |

Large formatted amounts use down-only Viewboxes, labels/footnotes wrap, and every bottom KPI remains scroll-reachable. Module tabs scroll horizontally when the available width cannot fit all seven.

## Verification — 2026-09-22

| Check actually executed | Result |
|---|---|
| `python3 scripts/verify-indigo-overview.py` | PASS — 12 focused source tests |
| `python3 scripts/verify-dashboard.py` | PASS — 338/338 XAML files well-formed; no unresolved dashboard resources/binding heads or incorrect style target types |
| `python3 scripts/xaml-xref-audit.py` | PASS — no reported forward resource references/duplicate keys |
| `git diff --check` | PASS |
| Baseline comparison of Models, ViewModels, Services, Controls, App.xaml and project file | Unchanged |
| `python3 scripts/audit-xaml.py --quiet` | FAIL — same 234 pre-existing violations as baseline; six additional **warnings** for superseded shared theme resources now unused by Overview |
| Native application build, xUnit execution, visual/runtime verification | **NOT EXECUTED — TOOLCHAIN UNAVAILABLE** |

The generic dashboard checker now distinguishes explicitly ancestor-bound view properties from VM properties, verifying the former against the code-behind instead of whitelisting arbitrary names. Missing resource/binding heads now produce a failing exit code. Existing modernization assertions were updated for the scoped indigo area/hero design and complete KPI collections, and for the already-existing shared `PgKpiTileTemplate` name.

The general checker reports three literal Overview font sizes for review: the financial total, monthly value and empty-state icon. Palette values live in the scoped resource dictionary. Shared legacy styles are left intact rather than deleting resources other code may still resolve at runtime.

### Continuation checks

- Re-applies responsive layout on `Loaded` as well as `SizeChanged`, with guards for every named Overview layout element. The shell's existing refresh-on-load behavior remains intact.
- Reflows the brand/subtitle/user header below 720 DIPs without removing the bound signed-in user or active-page subtitle.
- Collapses the entire Snapshot badge during loading/error, rather than leaving an empty coloured badge behind.
- Added source tests for lifecycle wiring, narrow-shell layout, badge state and normal-text palette contrast. The contrast test exposed muted text on lilac at **4.35:1**; the muted brush was darkened to pass **4.5:1** across the tested text/background pairs. This is a colour calculation, not a claim of full UI accessibility certification.
- Added a native xUnit test for semantic status-key palette mapping; like the other native tests it is **not executed** here.
- On resuming, Git HEAD pointed to the older checkout. Git-normalized hashes confirmed every non-dashboard baseline file matched `61a9ce1`; a mixed reset restored that requested branch pointer without overwriting working files.
- SDK acquisition was also attempted; `https://dot.net/v1/dotnet-install.sh` failed at the TLS connection. The Windows build/runtime gate remains open.

### Windows release gate still required

```powershell
dotnet build ProGlassAutomation.csproj -c Release
dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release --filter "FullyQualifiedName~Dashboard"
```

Then check the actual app at several view widths and 100%, 125%, 150% and 200% DPI:

1. All seven tabs and three header actions navigate correctly; keyboard focus and active-tab selection are visible.
2. Compare all 17 KPI values, five pipeline values/footnotes and chart hover values against the existing data sources.
3. Inspect empty data, loading, failed load, unavailable pending/tax figures and long document/customer names.
4. Scroll the complete document feed and continue into the outer page; verify the fifth Delivery KPI is reachable.
5. Resize through each breakpoint and change global themes; the scoped palette must stay readable without restyling other module bodies.

Source checks are not a WPF compilation, rendering proof or performance benchmark. No native pass or runtime-regression guarantee is claimed. Publishing this change does not satisfy the Windows release gate above.
