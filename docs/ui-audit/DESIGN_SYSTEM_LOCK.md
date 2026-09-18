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
| 11 | **Loading state** | Canonical: shared `Controls/LoadingState/LoadingStateControl` (Message property; used across core ERP lists/details). Lightweight inline alternative: `LoadingPanelStyle` + `LoadingProgressBarStyle` + `LoadingLabelStyle` (`Common.Loading` i18n). Industrial: no spinners/blur. |
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

MERIDIAN dashboard literals (verified 2026-09-18, P2 — kept static, structural):
`TiDashboardView` severity bars `CornerRadius=4/2.5` = half the bar height
(8px/5px capsule — proportional control internals, not theme radii) ·
`OverviewDashboardView` Factory Pulse `FontSize=26` = pre-scale design size
inside a `Viewbox` (rendered size is layout-driven) · `DashboardView` tab
pill `CornerRadius=7` and user-chip tile `CornerRadius=6` = custom template
control internals. **Tokenized in P2** (exact-value, zero visual change,
now density-responsive): `DashboardView` tab label → `PgFontMeta` (11.5),
module subtitle → `PgFontBody` (13).

## Phase 2 convergence record (core ERP, 2026-09-18)

Census evidence that the core ERP modules (Inquiry, PI, JO, DN, TI,
SheetStore, Statements/Reports) were **already converged by the prior
clean-list pass** and were verified, not re-worked:

- 10+ screens use the shared `EmptyStateControl`; `LoadingStateControl` is
  the canonical loading surface (PI lists, JO/DN details, …).
- `DnSectionStyles`/`TiSectionStyles` are thin module layers **BasedOn the
  shared bases** (`GridNumericCell`, `Button.Ghost`, `Typography.Label`,
  `SurfaceDetailStyle`) — correct architecture, kept.
- Module hub pages (JO/PI/TI/DN) carry their title in the MERIDIAN module
  bar (DashboardView); tab strips use the shared pill/tab styles;
  list/detail chrome uses ListPage/DataGrid/Dialog shared styles.
- P2 implementation delta: 2 typography tokenizations in
  `DashboardView.xaml` (see above). Everything else verified compliant or
  exempt.

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

## Phase 3 convergence record (masters/data management, 2026-09-18)

Scope: CompanyCenter, UserCenter, SpecificationsCenter, SystemCenter,
Settings (53 files), Shared panels.

Implementation delta — **22 literals tokenized, all exact-value (zero
visual change)**, across 6 files:

| File | Changes |
|---|---|
| CompanyCenterView | chip radii 10 → `Radius.Medium` ×2 · chip labels 10 → `FontSize.Badge` ×2 · status 11 → `FontSize.Label` |
| UserCenterView | same pattern ×4 · status 11 → `FontSize.Label` |
| SettingsThemesSection | chip radii 12 → `ControlCornerRadius` ×2 · chip labels 11 → `FontSize.Chip` ×2 |
| AppearancePreviewTile | radii 12 → `ControlCornerRadius` ×2 · labels 11 → `FontSize.Chip` ×2 |
| AppearanceSplashCard | radius 12 → `ControlCornerRadius` · label 11 → `FontSize.Chip` |
| QuickSettingsPanel | kicker 11 → `FontSize.Label` · sub 10 → `FontSize.Badge` |

Structural keeps (documented, not theme chrome):
- Avatar circles `CornerRadius=22` (CC/UC, 44px = h/2) · status dots
  5/6 (h/2) · Theme Studio icon tile 15 (30px = h/2) — circles are
  proportional control internals.
- Icon-glyph sizes 13/14 (`&#xE73E;`/`&#xE790;` checkmarks) — no
  13/14-px icon token exists in `FontSize.*` (`FontSize.Icon`=16);
  forcing `Body` would be semantically wrong and `Icon` a visual change.

Settings module evaluation (no change, documented):
`SettingsCardStyles` (47 keys) + `SettingsSectionStyles` (12) are a
self-contained module design layer — the bulk are spacing constants
(`SettingsGap*/StackGap*/InlineGap*`) plus a card/tile/chip/segment
grammar with its own internal BasedOn chain (zero BasedOn on app-shared
styles; values are DynamicResource). Merging its 50 consumer sections
onto app-shared Card/Pill styles is a large refactor that is NOT safe
without build verification — kept as a documented future candidate
(needs explicit approval + SDK).

## Phase 4 convergence record (operations/production, 2026-09-18)

Scope: Production (ProcessWorkCenter, ProductionControlCenter,
ProductionStationBody), Optimization (OptimizationView, SummaryTabView).

**30 content literals tokenized, all exact-value (zero visual change)** —
the operator KPI/label layer now rides the dynamic type scale:

| Literal | Token | Sites |
|---|---|---|
| 10px kicker/chip labels | `FontSize.Badge` | 6 (PWC, PCC, OptimizationView ×4 incl. 3 OptHeader captions) |
| 16px KPI status icons | `FontSize.Icon` | 6 (PWC ×3, PCC ×3) |
| 20px KPI count values | `TextTitleLarge` | 6 (PWC ×3, PCC ×3) |
| 22px utilization % | `TextHeadline` | 2 (PWC, PCC) |
| 11px m² sub-values | `FontSize.Label` | 6 (PWC ×3, PCC ×3) |
| 24px optimization results | `FontSize.Kpi` | 4 (SummaryTabView) |

**Shared-component fix (one edit over N):** the shared `OptHeader` style
(`Themes/Styles/OptimizationStyles.xaml`) carried a hardcoded
`FontSize 11` setter — tokenized to `FontSize.Label` (11, exact), so every
OptHeader consumer is now density-aware.

Structural keeps (documented, control internals — no exact token /
proportional): 48px avatar circle (r24) + its 20px brand glyph · 4px
kicker-chip radii (no radius token between XSmall3/Small6) · 14px & 18px
section icon glyphs (no icon token at those sizes; `FontSize.Icon`=16) ·
14px progress track r7 (full pill, h/2) · `OptimizationStyles` control-
template internals (r8/r6 radii, 13px icon glyph — consistent by
construction as the same control is reused).

`ProductionStationBody` local styles = thin module layer BasedOn the
shared `GodViewHeaderStyle` (kept, correct architecture).

Phase gate: validator 321 files 0 ERROR / 22 WARN / 2 NOTE · 5-file
functional contract clean (bindings/commands/x:Name/handlers preserved;
residual literals = exactly the documented structural keeps).
BUILD NOT EXECUTED — .NET SDK unavailable; TESTS NOT RUN.

## Phase 5 convergence record (supporting UI, 2026-09-18)

Scope: wizards, dialogs, reports, attachments, AI/Agent, Login, About,
Splash, CrashReports, Shared overlays. **Review outcome: converged or
deliberate special surface everywhere; zero visual changes required.**

| Surface | Verdict | Reason |
|---|---|---|
| Statement wizard (`WizardStyles`, 19 keys) | deliberate special surface | numbered-step shell (PrimaryGradient/Nav buttons/shadows); self-contained, DynamicResource values; kept per §No-cosmetic-overdesign |
| Login (`AuthSurfaceStyles`, 18 keys) | deliberate special surface | pre-shell brand glass (orbs, liquid card, frost/ice); login is the app's showcase moment |
| Splash (30 local keys + 8 hex) | deliberate special surface | cinematic brand surface; the 8 hex values are **material alpha recipe** (white/gray sheen layers #00FFFFFF…#4CFFFFFF) — identical in Light/Dark, not theme-controlled; documented exempt |
| About "Ios26" section (`AboutIos26Theme`, 12 keys) | deliberate special surface | intentionally personal-styled showcase (Ios-17 lineage); 3 avatar circles r19 = h/2 structural |
| AI chat (`AiChatView`, 3 BasedOn `SurfacePrimaryStyle` + chat grammar) | converged | bounce-dot/send-button grammar is chat-specific; 3 hex = **AI brand palette** (indigo #6366F1/#3730A3) + high-signal listening red #B91C1C — kept (tokenizing needs visual sign-off; documented) |
| Crash dialog (`CrashReportDialogStyles`, 2 buttons) | deliberate special surface | **crash dialog must be readable in unknown app state** — self-contained by design (2026-09-14 fix removed a broken implicit BasedOn); documented |
| Report hub / statements / dialogs / overlays (1–2 local styles each) | converged module layers | module-specific grammar (nav buttons, tinted KPI cards, approval radio, rate inputs, spec headers, HUD buttons), all DynamicResource values, no shared 1:1 duplicate |
| Dialog windows (22 ThemedDialogWindow) | verified | use shared Dialog*/AppToolTip styles; sizing covered in Phase 8 |

**Blocked item (documented, not worked blindly):** `InquiryListView` is the
only core list without an `EmptyStateControl` (all others have one). Adding
it requires a **new i18n key in all 13 language files** (no suitable
`Inquiry.No*` key exists; other lists use `Dashboard.No*Yet` keys) — i18n
translation work + visual verification are outside a static-safe scope.
VM already exposes `HasInquiries`/`HasActiveFilters` for the binding.

## Phase 6 convergence record (global consistency pass, 2026-09-18)

Re-ran `tools/pattern_census.py` after P2–P5. Measurements:

| Metric | Before (P1) | After P2–P5 | Status |
|---|---|---|---|
| Views hardcoded FontSize | 60 | **15** | all 15 = documented structural keeps (glyphs without exact token, Viewbox-scaled pulse) |
| Views hardcoded CornerRadius | 47 | **38** | all 38 = documented structural keeps (h/2 circles, pill h/2, chip r4 without token, print-locked 18) |
| Real hardcoded hex in Views | — | **9** | all in SplashView (material alpha recipe; the other "hex hits" in Views are XML comments) |
| Shared-chrome adoption | — | high | lists/tables/dialogs/inputs/buttons/pills/empty-states ride shared styles |

No further safe literal work remains — every residual has a documented
reason (see the per-phase records above). **No undocumented UI literal debt
exists in Views.**

Global audits performed (findings → decisions):
- **Control heights:** 208 explicit `Height` values (20–40px band) across 64
  files. The app has TWO height authorities: (a) the runtime density ladder
  (`DensityButtonHeight` 20–26 · `DensityInputHeight` 18–24 — used by the
  data-dense views and 3 shared button styles), and (b) the modern pill/input
  scale (26/32/34/40 — shared `*PillButton`/`Modern*Style` heights). Replacing
  (b) with (a) would shrink modern controls ~40% at Normal density — a
  visible regression. **Decision: keep both; reconciling them is a product +
  build-verified change (see Phase 8 decision record). Not changed blindly.**
- **Windows/MinWidth:** 32 windows audited — 24 set MinWidth explicitly; the
  8 that don't are safe: 7 dialogs inherit the `ThemedDialogWindow` base
  (MinWidth 420 / MinHeight 160 / ResizeMode NoResize default) and
  LoginWindow is NoResize fixed 1280×800. No window can collapse below its
  designed size.
- **Empty states:** 22 screens use `EmptyStateControl`; the sole gap
  (InquiryListView) is blocked on an i18n key (Phase 5 record).
- **Dialogs:** all 22 ThemedDialogWindow dialogs use shared Dialog* chrome.

## Phase 8 record (responsive / DPI / density, 2026-09-18 — static verification only)

Environment constraint: no .NET SDK → no rendered 1080p/1440p/4K or
100/125/150/200% passes possible here. What was verified statically:

- **Scaling mechanism is sound:** the app is DPI-unaware-safe by
  construction — WPF scales DIP layouts; all chrome rides DynamicResource
  tokens (brushes/radii/typography) so per-DPI rendering uses the same
  layout at scaled size. Pg dashboards additionally reflow via
  `DensityPageMargin` + the Pg type scale (runtime-published, 2026-09-18 fix).
- **Window floors verified** (Phase 6): no window collapses (base-class
  MinWidth/MinHeight + NoResize defaults cover all 32).
- **No clipping hotspots introduced by this stream** (all changes are
  token-for-value swaps; no tree restructures).
- **Density ladder re-anchoring = OPEN DECISION (not implemented):**
  the 18–26px ladder predates the 26/32/34/40 modern control scale.
  Reconciling (or re-anchoring the ladder to the modern scale) changes
  runtime appearance for the 5 density-driven views + 3 shared button
  styles and MUST be done with build + visual verification and a product
  call. Recorded here so it is not "discovered" (and blindly fixed) later.

Theme runtime / density / DPI runtime behaviour: **NOT VERIFIED** (no SDK).
