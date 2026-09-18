# DESIGN SYSTEM LOCK — Phase 1 (2026-09-18)

The authoritative pattern standard for modernizing all 320 XAML files.
**One ERP, one vocabulary.** Every pattern below maps to existing shared
styles/tokens; where a pattern had no shared home, Phase 1 added it
centrally in `Themes/Styles/PagePatterns.xaml` (merged in `App.xaml` after
`SharedSectionStyles.xaml`).

## The law (restated, non-negotiable)

- Theme-controlled values stay `DynamicResource` (colours, surfaces, accent,
  borders, radii, typography, density, shadows, glass).
- Only structural/immutable constants may be static (icon geometry, fixed
  control internals, non-themeable values defined by the design system).
- Business surface (bindings, commands, x:Name, handlers, converters,
  templates, i18n, automation) is frozen — modernization is a styling-layer
  change only.
- No per-screen style dictionaries where a shared one exists. Module-local
  styles are allowed ONLY for genuinely module-specific grammar (documented
  in this file's §Exemptions).

## Token families (existing, authoritative — reuse, do not fork)

| Concern | Canonical tokens (all DynamicResource) |
|---|---|
| Surfaces | `SurfacePrimaryBrush`, `SurfaceSecondaryBrush`, `Surface.Detail.BackgroundBrush` (+ v2 dotted `Surface.Primary/Secondary/Detail.*` aliases) |
| Foreground | `PrimaryForegroundBrush`, `SecondaryForegroundBrush`, `MutedForegroundBrush`, `OnAccentForegroundBrush` |
| Accent / status | `AccentBrush`, `SuccessBrush`, `WarningBrush`, `DangerBrush`, `InfoBrush` |
| Tints (soft fills) | `TintGreen/Amber/Red/Blue/Gray/Indigo/Purple/Teal Brush` |
| Hairlines | `BorderDefaultBrush` + `Border.HairlineThickness` (and directional `Border.*Thickness`) |
| Typography | `FontSize.*` (Badge 10 · Caption 10 · Chip 11 · Label 11 · Body 13 · BodySmall 12 · Button 13 · Input 13 · Tab 13 · Subtitle 13 · Title 14 · DataGrid 12 · Icon 16 · Kpi 24 · Money 24 · Display 32) · `Weight.*` · legacy `Text*`/`Type.*` ladder also dynamic — prefer `FontSize.*` in business screens |
| Radii | `CardCornerRadius`, `ControlCornerRadius`, `Radius.Small/Medium/Large/Hero/XSmall` (+ asymmetric) |
| Density (runtime-published by `ThemeManager.ApplyDisplayDensity`) | `DensityInputHeight`, `DensityButtonHeight`, `DensityRowHeight`, `DensityCardPadding`, `DensityPageMargin`, `DensityTitleFontSize`, `DensityKpiValueFontSize`, `DensityBodyFontSize`, `DensityStripPadding` |
| Shadows | `CardShadow` (ShellStyles), `Surface.Primary.Effect` |
| MERIDIAN plane | `Pg*` (dashboards + shell; see `Themes/Professional/README.md`) |

## The 14 standard patterns → canonical implementation

| # | Pattern | Use (style / tokens) |
|---|---|---|
| 1 | **Page surface** | Root `Grid` `Style="{StaticResource PageSurfaceStyle}"`; content in `Margin="{DynamicResource DensityPageMargin}"` (or `PageContentMargin` fallback). One surface colour per page — no nested full-bleed surfaces. |
| 2 | **Page header** | `PageHeaderStyle` + `PageHeaderTitleTextStyle` (FontSize.Title, SemiBold) + `PageHeaderSubtitleTextStyle` + `PageHeaderActionsStyle`. List pages instead use `ModuleListHeaderTextStyle` (ListPageStyles). |
| 3 | **Toolbar** | `ToolbarBandStyle` band; items spaced `ToolbarItemMargin`; search = `ModernTextBoxStyle`/`ModuleListFilterInputStyle`; filters = `ModernComboBoxStyle`/`ModuleListFilterComboStyle`; actions = §12 buttons. |
| 4 | **Section** | `SectionHeaderStyle` (SharedSectionStyles); grouped content in `SpecGroupCardStyle` or `BaseCardStyle`. |
| 5 | **Card** | `BaseCardStyle` / `ElevatedCardStyle` / `InteractiveCardStyle` (CardStyles); dashboards use `PgCard*` (ProfessionalCore). |
| 6 | **KPI** | `CompactStatTileStyle` (SharedSectionStyles) or `ModuleListKpi*` (ListPageStyles) or `PgFontKpi*` tiles (dashboards). Values: `FontSize.Kpi`/`FontSize.Money` + `Type.Money`. |
| 7 | **Form field** | `FieldLabelStyle` (SharedSectionStyles) or `DialogFieldLabelStyle`; inputs `ModernTextBoxStyle` / `ModernComboBoxStyle` / `IntInputStyle` / `DecimalInputStyle`; heights `DensityInputHeight`. |
| 8 | **Data table** | `ErpDataGridStyle` family or `LineItemsTableStyle` family (DataGridStyles); list rows `ModuleListRowStyle`; KPI-strip lists `ModuleListShellStyle`. |
| 9 | **Dialog** | `Dialog*` styles (DialogStyles): Title/Subtitle/Body/Hint/FieldLabel/Footer/CloseButton/SectionCard; `AppToolTipStyle`. |
| 10 | **Empty state** | Shared `Controls/EmptyState/EmptyStateControl` (icon + title + hint + optional action). No ad-hoc "no data" borders. |
| 11 | **Loading state** | `LoadingPanelStyle` + `LoadingProgressBarStyle` (thin indeterminate bar) + `LoadingLabelStyle` (`Common.Loading` i18n). Industrial: no spinners/blur. |
| 12 | **Action buttons** | Pill family: `PrimaryPillButton` / `OutlinedPillButton` / `SuccessPillButton` / `DangerButton` / `TabPillButton`; row actions: `RowPrimary/Outlined/Success/DangerButton`; reports: `ReportToolbar*/ReportExport*`; exports: `ExportButtonStyle`. |
| 13 | **Status indicators** | `StatusPill{Accent,Success,Warning,Danger,Info,Neutral}Style`, `PriorityPill*`, `TagPillStyle`, `TrendingBadge*` (PillStyles). Colour = pill; never bare coloured text. |
| 14 | **Navigation** | `ModuleTabStripStyle`/`ModuleTabButtonStyle` or `PillTabControlStyle`/`PillTabItemStyle` (ModuleTabStyles); list tabs `ModuleListTabInactiveStyle`; shell nav = MainWindow/Pg shell. |

## Phase 1 additions (new, central, DynamicResource-only)

`Themes/Styles/PagePatterns.xaml` — the patterns that previously had **no
shared implementation**:

- `PageSurfaceStyle`, `PageContentMargin` (page surface)
- `PageHeaderStyle`, `PageHeaderTitleTextStyle`, `PageHeaderSubtitleTextStyle`,
  `PageHeaderActionsStyle` (page header)
- `ToolbarBandStyle`, `ToolbarItemMargin` (toolbar)
- `LoadingPanelStyle`, `LoadingProgressBarStyle`, `LoadingLabelStyle` (loading)
- `ErrorBannerStyle`, `ErrorBannerTextStyle` (inline error strip; full-page
  error states use EmptyState with an error glyph)

All 13 keys verified unused before adding (W3 gate); all values are
`DynamicResource` to existing tokens, so Light/Dark, Monet and Crystal Fluid
re-tint them live. No `BasedOn` — purely additive, zero risk to existing
style chains.

## Exemptions (module-local styles that are legitimate)

| File | Why local |
|---|---|
| `Views/DeliveryNote/.../DnSectionStyles.xaml` (9) | DN detail section grammar specific to DN's tab structure — shared Section styles don't cover the DN rail layout |
| `Views/TaxInvoice/.../TiSectionStyles.xaml` (3) | TI detail family-section variants |
| `Views/Reports/StatementGenerator/Styles/WizardStyles.xaml` (5) | 4-step wizard shell (numbered steps) — no shared wizard pattern; candidate for P5 evaluation |
| `Views/Settings/SettingsCardStyles.xaml` (47) + `SettingsSectionStyles.xaml` (12) | Settings card-grid is a distinct surface (theme/appearance cards with live previews); P3 evaluates dedup against shared Card styles |
| `Views/About/AboutIos26Theme.xaml` (12) | About's "Ios26" showcase section — intentionally personal-styled (P5 documents) |
| `Views/Login/AuthSurfaceStyles.xaml` (5) | Login is the one pre-shell surface with its own glass treatment |
| `PrintableInvoiceView.xaml` (18 radii literals) | **Print surface**: geometry is print-locked pt-derived values, not theme chrome — intentionally static |

Anything else that looks local will be evaluated in Phases 2–6 and either
migrated to the shared pattern or documented here with a reason.

## Modernization recipe (per screen)

1. Read the screen; list its local styles and literals (census columns
   `st/fs/cr` in `PATTERN_CENSUS.json`).
2. Replace local styles with the canonical pattern (§table) where a 1:1
   shared equivalent exists. Where none exists, keep the local style and add
   it to §Exemptions with a reason.
3. Tokenize literals: `FontSize="N"` → nearest `FontSize.*` token (10→
   Caption/Badge, 11→Label/Chip, 12→BodySmall/DataGrid, 13→Body, 14→Title,
   24→Kpi/Money); `CornerRadius="N"` → `ControlCornerRadius` (small) /
   `CardCornerRadius` (cards) / `Radius.*` (explicit tiers); margins/padding
   → `Density*` tokens where density-controlled, `PageContentMargin` /
   `ToolbarItemMargin` where structural.
4. Apply page grammar top-down: §1 surface → §2 header → §3 toolbar →
   §8/§4 content.
5. Freeze check: diff must show ONLY styling-attribute changes (no binding /
   command / x:Name / handler / template / i18n line touched).
6. Validate: `python3 tools/xaml_validator.py` (0 ERROR required), XML parse,
   modified-file diff review, contract check on modified files.

## Validation status (Phase 1)

- `tools/xaml_validator.py`: **321 files, 0 ERROR / 22 WARN / 2 NOTE** — the
  new dictionary contributes zero findings; the 24 pre-existing legacy
  findings are untouched (per protocol §13).
- All PagePatterns keys resolve (verified against repo declarations).
- BUILD NOT EXECUTED — .NET SDK unavailable. Runtime appearance NOT VERIFIED.
