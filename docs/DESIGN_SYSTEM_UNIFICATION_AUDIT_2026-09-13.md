# ProGlass Design System — Unification Audit (Phase A)

**Date:** 2026-09-13 · **Status:** Audit complete · Implementation begins at Phase B
**Directive:** one canonical `ProGlassDesignSystem` in which Industrial, Monet
and Crystal are interchangeable material/theme configurations — not separate
architectures. Non-destructive: working business logic, virtualization, and
the PI industrial station concept are preserved.

---

## A. Existing resource dictionaries

Merged in `App.xaml` in this order (order matters for `BasedOn`/`StaticResource`):

| # | Dictionary | Ownership | Purpose | Duplicates noted |
|---|---|---|---|---|
| 1 | `ConverterResources.xaml` | Global foundation | converters (parse-time) | — |
| 2 | `Themes/LightTheme.xaml` | Mode (Light) | ~99 semantic brushes + `Color` keys | canonical base |
| 3 | `LiquidGlassDesign.xaml` | Material | 2026-08-29 canonical frosted-glass set | overlaps `LiquidGlassStyles.xaml` |
| 4 | `TypeScaleTokens.xaml` | Foundation | `TextXSmall…TextDisplayLarge` | — |
| 5 | `DesignTokens.xaml` | Semantic v2 | `Surface.Primary/Secondary/Detail.*`, `Radius.*`, `Border.*`, `Module*` styles | overlaps `CardStyles` |
| 6 | `SemanticTokens.xaml` (new) | Semantic | canonical aliases (surface/text/border/radius/motion) | additive |
| 7 | `ShellStyles.xaml` | Component | shell/sidebar | — |
| 8 | `TypographyStyles.xaml` | Foundation | header/number styles | overlaps `TypeScaleTokens` |
| 9 | `IconBadgeStyles.xaml` | Component | icon tiles | — |
| 10 | `PillStyles.xaml` | Component | status pills | — |
| 11 | `CardStyles.xaml` | Component | `Base/Task/Stat/KPI…` cards | overlaps `DesignTokens`, `DashboardKit` |
| 12 | `SharedSectionStyles.xaml` | Shared business | `FieldLabel`, `SpecGroupCard`, `Grid*Cell` (JO/DN/TI) | keep |
| 13–21 | `Themes/CrystalFluid/*.xaml` (9 files) | Theme (Crystal) | colors→brushes→metrics→tokens→controls→styles→nav→datagrid→scrollbar | overlaps `LiquidGlass*` |
| 22 | `ListPageStyles.xaml` | Shared business | PI/JO/DN/TI list chrome | — |
| 23 | `DataGridStyles.xaml` | Component | line-item table chrome | overlaps `CrystalFluidDataGrid` |
| 24 | `Styles/ButtonStyles.xaml` | Component (v3) | canonical button system | overlaps `LiquidGlass*Button*`, `Crystal*Button*`, `ModuleToolbar*` |
| 25 | `Styles/Step4SectionStyles.xaml` | Module (PI) | Step 4 panels | — |
| 26 | `PiIndustrialStyles.xaml` | Module (PI) | industrial station/KPI | keep (station concept) |
| 27 | `ModuleTabStyles.xaml` | Component | tabs (DN/JO/TI) | overlaps `LiquidGlassTab*`, `ModuleTabPillStyle` |
| 28 | `OptimizationStyles.xaml` | Module | optimization chrome | — |
| 29 | `InputStyles.xaml` | Component | TextBox/PasswordBox/ComboBox | overlaps global implicit styles + `LiquidGlass*Input*` |
| 30 | `DialogStyles.xaml` | Component | dialog card/footer/title | — |
| 31 | `LiquidGlassStyles.xaml` | Material | crystal card/button/switch/input/nav/dialog | overlaps #3, #13–21 |
| 32 | `Styles/ContextMenuStyles.xaml` | Component | context menus | — |
| 33 | `Styles/DashboardStyles.xaml` | Module | dashboards (44 legacy hex moved here) | overlaps `DashboardKit` |
| 34 | `Styles/DashboardKit.xaml` | Component | `Db*` grammar (7 dashboards) | supersedes `Dash*`, `Soft*`, `IndustrialDashboard*` |
| 35 | `App.xaml` inline global implicit styles | Component | TextBox/ComboBox/etc. | overlaps `InputStyles`, `LiquidGlass*` |
| — | `Themes/DarkTheme.xaml` | Mode (Dark) | mirrors LightTheme keys | — |
| — | `Themes/Languages/*.xaml` (15) | Localization | strings | — |

**Headline:** 35+ merged dictionaries across three overlapping generations
(Liquid Glass 2026-08-29 → DesignSystem v2 → Crystal Fluid 2026-09-05 →
Dashboard grammar 2026-09-08). The merge order already resolves collisions,
so the app *works* — the debt is conceptual duplication, not runtime breakage.

## B. Existing components — duplication census

| Component | Duplicated implementations |
|---|---|
| **Button** | `LiquidGlassButton/Primary/Icon/Link`, `LiquidGlass*ButtonStyle` (6), `CrystalButtonStyle`, `CrystalPrimaryButtonStyle`, `ModuleToolbarPrimary/Secondary/SuccessButtonStyle`, `ActionButtonStyle`, `DashActionButtonStyle`, `DbActionButton*Style`, `ExportButtonStyle`, `DangerButton` |
| **Card** | `BaseCardStyle`, `CardStyle`, `ElevatedCardStyle`, `InteractiveCardStyle`, `InteractiveElevatedCardStyle`, `InteractiveListRowCardStyle`, `InteractiveStatCardStyle`, `LiquidGlassCard`, `LiquidGlassStrong/SubtleCardStyle`, `DashCardStyle`, `DbCardStyle`, `DbRowCardStyle`, `DbPipelineCardStyle`, `IndustrialDashboardCardStyle`, `IndustrialDashboardRowCardStyle`, `ListRowCardStyle`, `HeroCard*` (5), `SurfacePrimary/Secondary/DetailStyle`, `Primary/Secondary/DetailSurfaceCardStyle`, `ModuleSectionCardStyle`, `DialogSectionCardStyle` |
| **KPI** | `CrystalKpi*`, `DashKpi*`, `DbKpi*`, `ErpKpiSurfaceStyle`, `GodKpiCardTemplate`, `KpiTemplate`, `KpiTileHoverStyle`, `ModuleListKpi*`, `CompactStatTileStyle` |
| **Tabs** | `LiquidGlassTabActive/Inactive/Container`, `ModuleTabPillStyle`, `ModuleTabStyles` (underline), `Pi*` tab chrome |
| **Toggle/Switch** | `LiquidGlassSwitch`, `LiquidGlassSwitchStyle`, `LiquidGlassToggleSwitch` |
| **Input** | global implicit `TextBox`, `InputStyles` (`DecimalInputStyle`/`IntInputStyle`), `LiquidGlassTextBox/Style`, `CrystalTextBoxStyle`, `LiquidGlassComboBox/Style/Editable`, `CrystalComboBoxStyle`, `LiquidGlassDatePickerStyle` |
| **Navigation** | `CrystalNav*` (full set), `LiquidGlassNavItemStyle`, shell `SidebarNavItem` |
| **Dialog** | `DialogStyles`, `LiquidGlassDialogCardStyle`, `Crystal*` dialog chrome |

**Verdict:** ~90 duplicate component keys across 7 families. These are the
canonical-components / compatibility-adapters of the target architecture.

## C. Hardcoded design values — classification

Measured across `Views/` (252 XAML total):

| Value class | Count | Verdict |
|---|---|---|
| `CornerRadius` literals | 73 (then 38 tokenized) | **must tokenize** → DONE for functional views (commit `03e4255`); remaining: About/Splash decorative + two allowed one-sided `12,12,0,0` |
| `FontSize` literals < 30 DIP | 43 (then 35 tokenized) | **must tokenize** → DONE for functional views; 44/60/26 display sizes are above/below scale by design |
| Hex colours | 8 files | **safe to keep** — decorative alpha-white sheens, profit/loss data-viz tints, comment-only, brand splash |
| `FontFamily=` literals | 0 in Views | already centralized via `AppFontFamily` |
| Motion durations | many `0:0:0.2`-style literals | **component-specific / technical** today → migrate to `Motion.*` over time |

## D. StaticResource risks

Views use **~2,900 DynamicResource** vs **~615 StaticResource** references.
StaticResource is acceptable for stable structural/geometry values, but the
following must stay Dynamic (they do): every brush (surfaces, borders,
accent, status, tint), `CornerRadius`, `Density*` metrics, and the new
`SemanticTokens.xaml` aliases. StaticResource is retained for the fixed
`Text*` type scale, converter references, and geometry that the Appearance
Studio does not re-target — an intentional decision, not a miss.

## E. Performance risks

- Production unit list and all four document lists keep `VirtualizingStackPanel`
  + row recycling (verified untouched — no virtualization removed).
- Crystal water/ambient layers are gated to `CrystalWaterDefaultEnabled` /
  `CrystalAmbientHostVisibility`, published only under Crystal Fluid and
  removed on family switch — water theatre never runs under Industrial/Monet.
- No new per-row `DropShadowEffect`/`BlurEffect` was added to grids.
- The global TextBox focus-ring uses a single BlurEffect scoped to the
  focused control, not per-row.

## F. Accessibility risks

- The global TextBox/ComboBox templates carry explicit Hover/Focus/Pressed/
  Disabled/ReadOnly triggers with a visible focus ring — `FocusVisualStyle`
  is not blanket-nulled on functional controls.
- Keyboard navigation is preserved (list/DataGrid selection, tabs, buttons
  remain standard controls).
- Reduced-motion is honoured by the Crystal family (`CrystalReducedMotion`).

## G. Migration plan (staged — non-destructive)

| Phase | Work | Status |
|---|---|---|
| **A** | Audit (this document) | ✅ done |
| **B** | Foundation — Colors/Typography/Spacing/Radius/Elevation/Motion | ✅ existing + `Motion.*` added |
| **C** | Semantic layer — Surface/Text/Border/Accent/Status/Focus | ✅ `SemanticTokens.xaml` aliases added |
| **D** | Material engine — Solid/Elevated/Frosted/Glass/Crystal | ⏭ next |
| **E** | Components — unify Button/KPI/Toggle/Input/Tabs/Pill/Dialog/Nav | ⏭ next |
| **F** | Layout — Workspace/Station/Rail/Deck/Grid/List/Queue + density | ⏭ next |
| **G** | Reference screen (PI or Dashboard) demonstrating every component × 3 themes × light/dark | ⏭ next |
| **H** | Module migration PI → JO → DN → TI | ⏭ next |
| **I** | Remaining modules | ⏭ next |
| **J** | Legacy cleanup — only after verification | ⏭ next |

### Guiding invariants (no regressions allowed)
1. Nine PI steps, JO/DN/TI workflows, EF Core schemas, and virtualization are untouched.
2. The PI industrial **station/deck/rail** language (`PiStationRail`,
   `PiWorkDeck`, `PiProductViewport`, `PiCostTile`, `PiLayerStrip`,
   `PiCommandFooter`) is preserved as module-level compositions, not flattened.
3. The three theme families remain *configurations* of one system —
   no `CrystalDesignSystem`/`MonetDesignSystem` parallel architectures.
4. Crystal water/reflection stays out of normal business cards.
5. DataGrid remains clarity-first (glass outer container, clear inner grid).
