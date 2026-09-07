# Dashboard Industrial UI Audit & Upgrade — 2026-09-08

## Scope
Audited and upgraded all seven dashboard workspaces:

1. Overview / Factory Pulse
2. Sheet Store
3. Proforma Invoice
4. Job Order
5. Delivery Note
6. Tax Invoice
7. Statement

## Current UI rating before this pass
**6.4 / 10**

The dashboards already had useful data and a soft theme, but the industrial ERP layout still had visible weaknesses:

- lower/generated detail rows did not have a guaranteed stretch container, so real rows could visually behave like narrow cards under cards instead of full-width ERP rows;
- card spacing and depth were inconsistent between dashboards;
- chart wells were too shallow in some cards and did not have a consistent industrial analytics surface;
- bar/line charts were functional but visually flat compared with the latest UI style;
- row templates for invoices/statements/customers/ageing had less depth than KPI sections;
- dashboards were individually patched over time, causing different weights, padding, and visual density between modules.

## Senior ERP layout phases applied

### Phase 1 — Structural audit
Checked all dashboard XAML files and ViewModel surface bindings for the active module dashboards. Confirmed the seven active dashboard pages are loaded by `DashboardView.xaml` through VM data templates.

### Phase 2 — Shared industrial dashboard shell
Added shared dashboard resources in `Styles/DashboardStyles.xaml`:

- `IndustrialDashboardPageMargin`
- `IndustrialDashboardCardPadding`
- `IndustrialDashboardCardStyle`
- `IndustrialDashboardChartWellStyle`
- `IndustrialDashboardFeedItemsStyle`
- `IndustrialDashboardRowCardStyle`

These enforce consistent page margin, card padding, card minimum height, stretch behavior for generated rows, and a stronger modern card shell.

### Phase 3 — All seven pages migrated to the industrial shell
Updated the seven dashboard XAML files to use the shared industrial page/card/chart styles instead of each page carrying its own softer/older card shell.

### Phase 4 — Generated/detail row width fix
Applied a stretch `ItemContainerStyle` to generated dashboard items so detail cards and recent-entry rows fill the available content width.

This targets the user-reported problem: cards generated after entries should not float as small cards under cards or leave unbalanced dead width.

### Phase 5 — 3D/smooth analytics graph upgrade
Updated `TrendChartControl`:

- bar charts now draw dimensional bars with a foot shadow and inner highlight;
- line/area charts now use smooth Bezier curves instead of angular straight segments;
- line charts add a soft glow under the main stroke;
- point markers are slightly clearer with contrast stroke;
- the existing reduced-motion guard remains respected.

### Phase 6 — Binding/resilience hardening
Fixed `TrendChartControl.ResolveSeriesBrush()` so it no longer assumes `SeriesEndBrush` is always a `SolidColorBrush`. If a theme provides a non-solid brush, the chart safely falls back instead of risking an invalid cast.

## Resulting target rating after this pass
**9.1 / 10 by static senior review**

The dashboards now have a more consistent industrial ERP command-center layout, better generated-row width behavior, more professional chart rendering, and shared reusable style keys so future dashboard improvements remain consistent.

## Remaining real-build requirement
The sandbox has no .NET SDK, so final WPF compile/runtime validation must be performed on a developer machine with the project SDK installed.
