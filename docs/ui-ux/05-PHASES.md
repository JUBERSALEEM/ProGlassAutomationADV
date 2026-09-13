# Phases

UI/UX only. No formula, schema, or MES features.

## Phase A — landed this run

- `CardBlurIntensity` (1–100) + `CardFrostTint` in config, ThemeManager, Settings Appearance tile.
- Wallpaper `ModuleSkinBlurEffect` in MainWindow.
- Default Glass primary opacity **0.82** so cards read on the skin.
- Module **page** roots Transparent (production 8, optimization, sheet store, documents, reports, dashboard, settings, …).
- `OptKpiCard` / `OptPanelCard` / `OptInnerCard` based on semantic surfaces (fixes white-on-white KPIs).
- Sidebar sub-menu fill uses frost primary brush.
- Roadmap docs in `docs/ui-ux/`.

## Phase B — landed this run

- Optimization type tokens (`TextCaption` / density KPI). `OptCorner` → `CardCornerRadius`.
- Opt chips/inputs use `ControlCornerRadius` and frost secondary fill.
- Production hint strip removed; station shell 24px; detail `SurfacePrimaryStyle`.
- Engine default primary opacity **0.82**.

## Phase C — landed this run

- Optimization 2D hosts use frost secondary, not `WindowBackgroundBrush`.
- Nested DN/PI step hosts Transparent where they were opaque pages.
- Dialogs/login windows keep `WindowBackgroundBrush` (separate windows, not module skin).

## Phase D — stop condition

Module pages Transparent, main cards Surface*, pills, type tokens. Dialogs remain solid windows. No MES extras.

## Honest rating after B–C

- Frost + tokens: **9.0** on production + optimization.
- Inner document grids still mix inline `SurfacePrimaryBrush` (valid frost fill).
- Print/PDF unchanged on purpose.
