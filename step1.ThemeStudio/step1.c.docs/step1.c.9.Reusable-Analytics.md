# Reusable Bento analytics — implementation and evidence

Date:2026-10-20. Existing root `ProGlassAutomation` WPF application, branch `arena/bd154b4b-proglassautomation`; inspected baseline `11ff42b5759d589624fa6f7025283bca3f56d287`. **Implementation is in source; native Windows compilation/rendered acceptance remains unverified.** No parallel app, legacy module/database, future theme, workflow activation, release or PR.

## What changed

- One maintained chart dependency: **LiveChartsCore.SkiaSharpView.WPF2.0.4**, MIT. Inspected pinned upstream source/API/license and compared ScottPlot/OxyPlot releases. Native WPF host with Skia rendering, no browser or custom plotting engine. Exact dependency evaluation and transitive licensing caveats are in the [reuse guide](../../Theme/step1.f.Analytics/step1.f.6.Analytics-README.md).
- Reusable immutable typed datasets/series, validation and WPF host. Real columns, horizontal/grouped/stacked bars, multi-series lines/area, pie/donut and sparklines. Native scaling/axes/ticks/tooltips; null gaps, negative/zero values and zero-donut handling. Four-series/1,000-category bounds prevent accidental unbounded panels.
- Keyboard-accessible native CheckBoxes actually toggle series visibility, with matching swatches. Text summaries and toggleable source values provide non-color/pointer-only inspection. Explicit absent/empty/loading/error/invalid states; no fake networking or timer.
- Bento-owned semantic Light/Dark paints adapt through dependency properties, coalesced on the UI dispatcher. Palette updates retain native series/legend identity and visibility; no global brush lookup/cached palette in shared controls. Axis/grid/tooltip/fonts and DataGrid states use active resources. Actual switching assertions still need Windows execution.
- Reusable KPI control/template, four purpose-based plates, chart cards and a native sortable/selectable/virtualizing DataGrid with themed row/cell/header/gripper/sort/focus treatment.
- Business Reference opens first: hero/context, four compact varied KPIs, asymmetric revenue/donut and trend/target panels, nested sparkline, table/activity/attention, plus existing functional local worklist/review form. Demo source values are deterministic and explicitly labeled, not live ERP data.
- Separate Studio retains real customization and controls/state previews, and adds an interactive chart laboratory (nine kinds × seven scenarios) plus grouped gallery. Normal, zero/negative, missing, empty, loading, error and invalid data are selectable. Reset/retry resets local demo state only.
- **Recommendation implemented:** internal validated resource contract stays; the intrusive main contract dump is now optional/collapsed diagnostics. The old illustrative Geometry plot/resource is removed.

## Ownership and file map

| Files | Purpose |
| --- | --- |
| `Theme/step1.f.Analytics/step1.f.1–5.*` | Immutable model/validation, semantic DP adapter, native host/interactions, library series/axis/paint adapter, reusable KPI control; no sample/business dependency |
| `Theme/step1.f.Analytics/step1.f.6.Analytics-README.md` / `step1.f.7.LiveCharts-LICENSE.txt` | Repeatable future-module binding example, constraints/API/dependency evaluation and upstream MIT notice |
| `Theme/ThemeBento/ThemeBento.Light.xaml` / `Dark.xaml` | 17 new purpose-based plate/chart brush and size/opacity roles, complete in both modes |
| `Theme/ThemeBento/step1.e.Components/step1.e.10.Analytics.xaml` | All new chart/KPI/table/plate styling and header sort/resize template; native WPF only |
| `step1.e.1.Components.xaml`, `step1.e.7.BentoResources.cs`, `step1.e.9.Workspace.xaml` | Compose/cache sealed styles, remove old fixed chart geometry |
| `Theme/step1.a.ThemeEngine/step1.a.1.ThemeContract.cs`, `step1.a.5.ThemeContract-README.md` | Typed nonvisual contract:99roles/46exact style targets; engine switching implementation unchanged |
| `step1.b.19.AnalyticsSamples.cs` | Deterministic demo snapshots, typed scenario/kind controls, table rows; sample calculation/formatting stays here |
| `step1.b.20.ChartGallery.xaml/.cs` | Live chart laboratory and responsive chart gallery |
| `step1.b.21.AnalyticsOverview.xaml/.cs` | Responsive asymmetric analytics/KPI/table/targets/activity composition |
| Studio b.1/b.11, sample b.6/b.18 and b.17 customization | Integrate galleries/overview, optional diagnostics, remove fake path and restore Business Reference startup selection |
| `MainWindow.xaml`, `ProGlassAutomation.csproj` | Accurate workspace title, include analytics sources, pin one chart dependency; TFM unchanged |
| `Theme/step1.d.tests/step1.d.8.AnalyticsChecks.cs`, d.1/d.2/d.7 | Authored compiled data/scenario/contrast and native configuration/state/mode/identity/table-selection checks; update startup/retired-geometry assertions |
| d.3–d.6 portable validators | Allow only approved dependency/source inclusion; verify new contract/types/contrast/isolation and chart ownership; retain reason-checked mutations |
| AGENTS, root README, docs00–06, c.1, engine README, Bento spec and this report | Current ownership/allocation/dependency/reuse/coverage/evidence; older checkpoint reports remain historical |

Names remain in existing step1 allocation; no feature number reused. Paired XAML/code-behind keeps the documented WPF tooling exception. Shared engine remains strictly nonvisual. A future registered theme supplies its own styles and semantic paints for the same controls—no screen replacement or Bento fallback.

## Executed verification

Linux container, no .NET SDK/Windows runtime installed (none installed by the agent).

| Command / evidence | Actual result |
| --- | --- |
| `python3 Theme/step1.d.tests/step1.d.3.verify_sources.py` | PASS:99typed roles×2;342measured text contrast combinations;26XML documents;46required styles; no duplicate/cross-theme keys/engine visuals; real-library renderer/source boundary/startup guards |
| `python3 Theme/step1.d.tests/step1.d.4.CheckerRegression.py` | PASS:14reason-checked mutation rejections, including chart-card/resource/style/contrast/preset/readonly-binding regressions |
| `python3 Theme/step1.d.tests/step1.d.6.verify_foundation.py` | PASS:35Compile/25Page existing unique items;47legacy paths absent; approved package only; separate pages; workflow inactive |
| `dotnet build ProGlassAutomation.csproj --configuration Release` | BLOCKED:exit127, `dotnet: command not found` |
| Package restore / compiled tests / native WPF runtime | NOT RUN:requires Windows/.NET toolchain; workflow intentionally inactive |
| Screenshots, rendered comparison, real DPI/screen-reader/performance tests | NOT RUN; no mockup/source parser/AI image substituted |

The current C# tests author63kind/scenario contracts, additional negative/zero/all-null/nonfinite/mismatch/duplicate/negative-pie/zero-donut/limits/immutable-data cases, and analytics text contrast. Native smoke authors all nine configurations, explicit states, six palette replacements preserving series/legend objects/visibility and table selection. **Authored tests are not passing execution evidence.** Existing108customization combinations remain authored/unrun.

## Acceptance still required

On a supported Windows10/11 developer machine with .NET8 SDK:

```powershell
dotnet restore ProGlassAutomation.csproj
dotnet build ProGlassAutomation.csproj --configuration Release
dotnet build Theme/step1.d.tests/step1.d.ThemeTests.csproj --configuration Release
dotnet run --project Theme/step1.d.tests/step1.d.ThemeTests.csproj --configuration Release -- --contracts-only
dotnet run --project Theme/step1.d.tests/step1.d.ThemeTests.csproj --configuration Release
dotnet run --project ProGlassAutomation.csproj --configuration Release
```

Confirm published-package compatibility/native asset loading first. Inspect both modes and all customization combinations on actual WPF: 720/1024/1440/1920/2560 widths, 100/150/200% DPI, tooltip/theme changes, sort/resize/selection, keyboard series/source values, open dialogs/popups and expanded diagnostics. Inspect axis/category clipping, legend wrapping, narrow/dense layout, high contrast/screen readers and sustained memory/idle rendering performance. Axes rebuild on repaint; zoom/pan is intentionally not exposed. Area is overlapping, not stacked. Pie/null/negative segment rejection is deliberate. No deployment license audit is claimed before restoring actual transitive packages.

Workflow stays under `cols/ci/windows-wpf.yml`; no workflow activation or run is claimed. No screenshot, release, executable/public artifact or PR created. Standing authorization covers committing and immediately pushing this approved work to the current session branch; PR creation still requires two separate explicit confirmations. Next gate is Windows verification/correction and user review, **not another theme**.
