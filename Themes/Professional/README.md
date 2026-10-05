# MERIDIAN — the ProGlass design system (Pg*)

The full 0→100 UI rewrite (2026-09) of the MainWindow shell + all 7 module
dashboards runs on this design system. Everything here is prefixed **`Pg*`**
so it can never collide with the app's other token layers.

## Files

| File | Contents |
|---|---|
| `ProfessionalCore.xaml` | Geometry, type scale, card/KPI/feed/row grammar, skeletons, empty states, buttons. Merged first. |
| `ProfessionalShell.xaml` | Top bar + sidebar styles (reads the core tokens). Merged second. |
| `LightTheme.xaml` / `DarkTheme.xaml` | The `Pg*` **brush** tokens (surfaces, hairlines, text, brand, status) — one block per theme so the runtime Light/Dark swap re-tints the system live. |

Both core/shell dictionaries are merged in `App.xaml` after the shared style
dictionaries and before `ComponentTokens.xaml` — the order is load-bearing for
the `BasedOn` chains.

## Token families

- **Geometry** — `PgPageMargin`, `PgCardPadding`, `PgTilePadding`,
  `PgRowPadding`, `PgGutter`, `PgGutterHalfMargin` (a `Thickness`, never a
  Double — the ERR-F4C120 lesson), `PgCardRadius` 12, `PgTileRadius` 9,
  `PgCtrlRadius` 8, `PgKpiTileMinHeight`, `PgChartMinHeight`.
- **Type** — the scale, tabular figures: `PgFontHero` 20 · `PgFontHeroSub`
  12.5 · `PgFontCardTitle` 13 · `PgFontCardSub` 11.5 · `PgFontKpiValue` 22 ·
  `PgFontKpiLabel` 10.5 · `PgFontKpiFoot` 11 · `PgFontStatValue` 24 ·
  `PgFontStatLabel` 10.5 · `PgFontStatFoot` 11 · `PgFontBody` 13 ·
  `PgFontBodySm` 11 · `PgFontMeta` 11.5 · `PgFontBtn` 12 · `PgFontGlyph` 15 ·
  `PgFontGlyphSm` 13 (plus the `PgFontStage*` rail scale).
- **Surfaces / hairlines** (theme files) — `PgPageBrush`, `PgSurfaceBrush`,
  `PgSurface2Brush`, `PgSurface3Brush`, `PgHairlineBrush`,
  `PgHairlineStrongBrush`.
- **Text** (theme files) — `PgText1Brush` … `PgText3Brush`.
- **Brand** (theme files) — `PgAccentBrush`, `PgAccentHoverBrush`,
  `PgAccentInkBrush`, `PgAccentSoftBrush`.
- **Status (status-only colour policy)** (theme files) — `PgOkBrush`/
  `PgOkSoftBrush`, `PgWarnBrush`/`PgWarnSoftBrush`, `PgDangerBrush`/
  `PgDangerSoftBrush`, `PgChartBrush`/`PgChart2Brush` (categorical,
  non-status).

## Density

`ThemeManager.ApplyDisplayDensity` republishes `PgPageMargin`, `PgGutter`,
`PgGutterHalfMargin`, `PgKpiTileMinHeight`, `PgChartMinHeight` on the
application resources for the four Density settings. The dashboards read
`{DynamicResource PgPageMargin}` so the setting reflows them live.

## Old-kit retirement (Phase 7, 2026-09-18)

The previous dashboard grammar is gone:

- `Styles/DashboardKit.xaml` (`Db*`) — **deleted**, zero consumers.
- `Styles/DashboardStyles.xaml` (`Dash*`/`Soft*`/`Industrial*`) — reduced to
  the 2 keys three non-dashboard views still consume (`SectionTitle`,
  `SoftCardStyle`); delete it when those views migrate.
- `Themes/Styles/GlobalDesignSystem.xaml` — dead `Db*` aliases removed.

Rule: dashboards, the module bar and the shell reference **only** `Pg*` keys.
New dashboard surfaces get a `Pg*` token — never a local style, never an
unprefixed key.
