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

## Phase B — token cleanup (next UI sitting)

- Retokenize Optimization typography (11/12/25 → Text*).
- `OptCorner` → `CardCornerRadius`.
- Remaining UserControl.Background opaque shells.
- Station body leftover hint strip if still present vs inventory header.

## Phase C — view-by-view (owner paced)

Redesign one module at a time against Sheet Store: 24px page margin, 360 detail, pills, no competing card species. Order suggestion: Optimization tabs → Production detail grids → PI steps → Reports hub.

## Phase D — stop

When every module page is Transparent, every main card is SurfacePrimary, buttons are pills, type is tokens. Do **not** add racks/hold/scrap to “finish” the look.

## Honest rating after Phase A

- Frost architecture: **8.5** (real blur on wallpaper, tinted cards, settings).
- Production/Optimization white-on-white: **fixed at the style/root layer**.
- Not 10: leftover inner `SurfacePrimaryBrush` rectangles, Opt type literals, some dialogs still solid.
