# Job Profitability — Single-Click Sidebar + Double-Click Full Detail

**Date:** 2026-09-06
**Scope:** `Job Profitability` module row navigation
**Status:** implemented (static-validated; WPF build must run on `windows-latest`)

## Requirement

| Gesture | Behaviour |
|---|---|
| Single click a row | Open the existing 420px detail **sidebar** (`JobDetailPanel`) — unchanged. |
| Double click a row | Open a **full-screen** detail page, same pattern as the Step 9 Profit & Loss module's full-screen spec report (STATE 2), with a back affordance to the jobs list. |

The full page must show the same detailed content and professional styling as
`ProfitLossStepView` STATE 2, and must never disagree with the sidebar's
figures.

## What changed

| File | Change |
|---|---|
| `ViewModels/JobProfitability/JobProfitabilityViewModel.cs` | Added `OpenFullDetailCommand` (double-click), `CloseFullDetailCommand` (back), and `IsFullDetailOpen` / `HasFullDetail` state. `SetSelectedRow(null)` now also closes the full page. |
| `Views/JobProfitability/JobProfitabilityView.xaml` | Root surface now switches between two layers: LAYER 1 (existing two-column list + sidebar, `Visibility=HasFullDetail` inverted) and LAYER 2 (full-screen detail, `Visibility=HasFullDetail`). Row template gained a `LeftDoubleClick` `MouseBinding` → `OpenFullDetailCommand`. |
| `Views/JobProfitability/JobFullDetailView.xaml` (+ `.xaml.cs`) | **New** full-page view: accent header (PI identity + target status pill), 4-card KPI strip, two-column sections (job info, target model, revenue, cost, margin), full-width P&L line-item table. |
| `ProGlassAutomation.csproj` | Registered `JobFullDetailView.xaml` as `Page` + code-behind. |

## Design decisions

1. **One VM, two surfaces.** Both the sidebar and the full page bind the
   *same* `JobDetailViewModel` (`JobDetail`). The full page is a
   `local:JobFullDetailView` with `DataContext="{Binding JobDetail}"` — no
   duplicate data path, so the numbers can never drift.

2. **Double-click re-loads the detail.** WPF delivers a double-click as two
   single clicks followed by one double-click, so the single-click handler
   toggles the sidebar first. `OpenFullDetailCommand` therefore re-runs
   `JobDetail.LoadJobDetail(row.ProformaId)` before showing the page, which
   guarantees the data is present regardless of the toggle side-effects
   (the full page covers the whole module anyway).

3. **Back returns to the list.** `CloseFullDetailCommand` only clears
   `IsFullDetailOpen`; the list, filters and top KPI cards are untouched
   underneath. If the selected PI is filtered out while the page is open,
   `SetSelectedRow(null)` (which runs in `ApplyFilters`) closes the page
   automatically so it can never linger for a row that no longer exists.

4. **No new features invented.** The full page reuses the sidebar's exact
   `JobDetailViewModel` members — every binding was cross-checked against
   the VM (`ProformaId`, `TargetSpecs`, `LineItems`, margin/cost/revenue
   displays, `TargetStatusLabel` state, etc.). It adds a layout, not new
   calculations.

## Post-implementation hardening (2026-09-06)

Three issues found during follow-up review were fixed:

1. **Missing-target note bound the wrong type.** The "target data missing"
   note TextBlocks (sidebar + full page) fed `TargetPanelVisibility` — a
   `System.Windows.Visibility` — through `InverseBoolToVisibilityConverter`,
   which only inverts actual `bool`s and otherwise falls through to
   `Visible`. The note therefore showed even when target data existed.
   Both now bind `HasTargetData` (a real bool) instead.

2. **Double-click could start several identical DB reads.** WPF delivers a
   double-click as click → click → double-click, and `JobDetail.LoadJobDetail`
   is `async void`, so one gesture could start up to three concurrent
   `GetByProformaIdAsync` calls for the same invoice. `LoadJobDetail` now
   skips a re-entrant call while the same `ProformaId` is already loading.

3. **Double-click gesture moved off the `Border`.** `Border` is not a
   `Control` and has no `MouseDoubleClick` class handler; every other
   double-click surface in the app attaches the gesture to a `Control`
   (e.g. the GodView `DataGrid`s). The row template now wraps its visual
   `Border` in a `ContentControl` and carries the `LeftClick` /
   `LeftDoubleClick` `MouseBinding`s there, keeping the row appearance
   identical while matching the proven input pattern.

## Verification

- Python DOM scan: both touched XAML files are well-formed with no
  duplicate `Style` attributes and no element that sets `Style` as both an
  attribute and a property element.
- C# brace/paren/bracket balance: clean.
- 52 binding paths in `JobFullDetailView.xaml` all resolve against
  `JobDetailViewModel` / `JobDetailLineItem` / `JobTargetSpecLine`.
- `Tests/Agent/JobProfitabilityNavigationTests.cs` pins the contract at
  source level (asserts the commands, the two `MouseBinding`s, the back
  button and the shared `JobDetail` DataContext).
- The WPF build (`net8.0-windows`) still must be confirmed on
  `windows-latest` via `docs/ci/dotnet-desktop.yml` (the sandbox cannot
  compile WPF).
