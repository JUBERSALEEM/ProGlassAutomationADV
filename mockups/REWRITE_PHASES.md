# MERIDIAN — Full UI Rewrite: Phase Plan (0 → 100)

**Scope:** complete 0→100 rewrite of the UI for **all 7 dashboard modules + the MainWindow
shell (top bar + sidebar)**. Every view is rewritten from scratch on the new design language.
**What survives:** the *dynamic* layer only — every `Binding`, `Command`, `x:Name` referenced by
code-behind, event handler, `DataTemplate`, converter and behavior. No ViewModel, model, service,
code-behind logic or i18n string is edited.

**Companion preview:** `mockups/proglass-pro-mockup.html` — 8 screens (Shell + all 7 dashboards),
Light/Dark toggle. This mockup is the acceptance reference: the shipped views must match it.

---

## The rules of the rewrite (why it is safe at "0 to 100")

1. **Parallel design system.** All new styles/tokens live in new files under new `Pg*` keys
   (Phase 0). The old `Db*`/`Soft*`/`Industrial*` keys are NOT touched during Phases 1–6, so the
   app is fully functional (in the old look) after every single commit.
2. **Binding contract per view.** Before each view is rewritten, its complete binding inventory
   is extracted (done — see `mockups/binding-inventory.md` inside each phase commit). After the
   rewrite, the inventory is re-extracted from the new file and diffed: every path, command,
   converter, behavior and x:Name must appear at least once. Any missing item blocks the commit.
3. **Code-behind is frozen.** `MainWindow.xaml.cs`, `DashboardView.xaml.cs` and all dashboard
   code-behind are not edited. Everything they touch by name stays.
4. **Theme-safe.** Every colour is a `DynamicResource Pg*` token defined in BOTH theme
   dictionaries — the runtime Light/Dark swap keeps working (the house rule documented in
   `DashboardStyles.xaml`).
5. **One commit per phase, pushed immediately.** Each phase is independently revertable.

## Token & style map (mockup → WPF)

| Mockup CSS | New WPF key | Purpose |
|---|---|---|
| `--bg / --sf / --sf2 / --sf3` | `PgPageBrush`, `PgSurfaceBrush`, `PgSurface2Brush`, `PgSurface3Brush` | page / card / tile / hover |
| `--ln / --ln2` | `PgHairlineBrush`, `PgHairlineStrongBrush` | 1px borders |
| `--t1 / --t2 / --t3` | `PgText1Brush`, `PgText2Brush`, `PgText3Brush` | type scale colours |
| `--ac / --ach / --aci / --acs` | `PgAccentBrush`, `PgAccentHoverBrush`, `PgAccentInkBrush`, `PgAccentSoftBrush` | brand |
| `--ok/--oks, --wn/--wns, --dg/--dgs` | `PgOkBrush/Soft`, `PgWarnBrush/Soft`, `PgDangerBrush/Soft` | status (status-only colour policy) |
| radii / spacing | `PgCardRadius` 12 · `PgTileRadius` 9 · `PgCtrlRadius` 8 · `PgGutter` 16 · `PgPagePadding` 24 | geometry |
| type | `PgFontPage` 20 · `PgFontCard` 13 · `PgFontKpi` 26 · `PgFontBody` 13 · `PgFontLabel` 11 · `PgFontCaption` 11.5 | 6-step scale, tabular figures |

Style keys: `PgCard`, `PgTile`, `PgWell`, `PgRowCard`, `PgSectionTitle`, `PgCardTitle`,
`PgKpiValue/Label/Foot`, `PgHeroTitle/Subtitle`, `PgChip`, `PgLiveChip`, `PgBtnPrimary`,
`PgBtnGhost`, `PgGlyph`, `PgKpiTile` (template), `PgPipelineStage` (template), `PgFeedRow`
(template), `PgSkeletonBlock/Circle/Band/Chart`, `PgStateBand`, `PgEmptyState`, `PgStatCard` —
shell: `PgTopIconButton`, `PgTopPill`, `PgBrand`, `PgSearchFrame`, `PgSidebarGroupLabel`,
`PgSidebarItem`, `PgSidebarGroupToggle`, `PgSidebarSubItem`, `PgSidebarSubCard`, `PgDrawer`.

---

## Phase 0 — Foundation (new files only, zero visual change)
- `Themes/Professional/PgTokens` block added to `LightTheme.xaml` + `DarkTheme.xaml` (both themes,
  frozen brushes, `po:Freeze`).
- `Themes/Professional/ProfessionalCore.xaml` — every `Pg*` style + template for dashboards.
- `Themes/Professional/ProfessionalShell.xaml` — top bar + sidebar styles (keeps the
  `StringEqualsMultiConverter` active-module mechanism and `BounceHover` behavior from the old
  sidebar styles).
- Registered in `App.xaml` merged dictionaries (new entries only).
- **Verify:** XML well-formed ×3, all `Pg*` keys resolvable, app compiles (tokens unused yet).
- **Commit + push.**

## Phase 1 — Shell: MainWindow top bar + sidebar (full rewrite)
- `MainWindow.xaml`: rewrite the top-panel block (hamburger, brand, search frame, theme toggle,
  AI bot, AI agent pill, date/time, connectivity, notifications, settings, screenshot, record,
  user card) and the LEFT DRAWER (header, 8 groups, all sub-items, storage footer) in the new
  `Pg*` language.
- **Preserved verbatim:** all 23+ DataTemplates, every `x:Name` (LogoPanel, DarkModeButton,
  AiBotButton, NotificationButton, SettingsButton, ScreenshotButton, RecordButton, UserCardButton,
  MenuScrollViewer, the 7 group hosts/toggles, StorageCard, ReadOnlyBanner, LicenseOverlay,
  LicenseActivationHost, all 6 popups + panels), all commands/bindings incl. `x:Static
  LicenseStatusViewModel` sources, all 3 event handlers, `ContentControl` + transition behavior,
  AI/Agent overlays, read-only banner.
- **Verify:** binding-inventory diff vs old file = zero missing; XML well-formed.
- **Commit + push.**

## Phase 2 — Module bar (DashboardView full rewrite)
- `DashboardView.xaml`: rewrite the command bar (subtitle, segmented 7-module picker, refresh
  chip, user chip) in the `Pg*` language.
- **Preserved:** the 7 VM→View DataTemplates, `Pages`/`SelectTabCommand`/`ActivePage`
  (TabTitle/TabSubtitle/LastRefreshed), `CurrentUserDisplay`, `ReferenceEqualityConverter`
  selection mechanism, `OnLoaded` handler.
- **Commit + push.**

## Phase 3 — Overview dashboard (full rewrite)
- `OverviewDashboardView.xaml`: hero, pipeline rail, pulse + mix + stat stack, revenue trend +
  recent documents, 4 KPI bands — all rebuilt on `Pg*` templates.
- **Binding contract (40):** PulseDisplay, PulseHint, PulseSpark, InProductionDisplay,
  OverdueDisplay, HasOverdueJobs, PendingPcsDisplay, DeliveryMixSlices, MonthlySalesBars,
  ChartTitle, RecentActivity, SalesTiles, ProductionTiles, InventoryTiles, DeliveryTiles,
  WorkflowStages, NewInvoiceCommand, ViewJobsCommand, ViewDeliveriesCommand, IsLoading, HasError,
  ErrorMessage, LastRefreshed.
- **Commit + push.**

## Phase 4 — Sheet Store + Proforma (full rewrite)
- `SheetStoreDashboardView.xaml`: hero, KPI band (`KpiCards`), `CategoryBars` + `CategoryMixSlices`.
- `PiDashboardView.xaml`: hero, KPI band, `StatusBars` + `StatusMixSlices`, `TopCustomerBars`.
- Both keep the 4-state contract (loading skeleton, error band, empty states, data) and
  `HasChartData`.
- **Commit + push.**

## Phase 5 — Job Order + Delivery Note (full rewrite)
- `JoDashboardView.xaml`: KPI band, `StatusBars`/`StatusMixSlices`, `PriorityBars`, `RecentFeed`
  (+ `HasStatusData`, `HasRecentFeed`).
- `DnDashboardView.xaml`: KPI band, `StatusBars`/`StatusMixSlices`, `TypeBars`, `RecentFeed`.
- **Commit + push.**

## Phase 6 — Tax Invoice + Statement (full rewrite)
- `TiDashboardView.xaml`: hero + New Invoice/View buttons, overdue alert band, `KpiTiles`,
  `AgingMixSlices`, OverdueCount/OverdueAmountDisplay, `RevenueBars` + `RevenueChartTitle`,
  `RecentInvoices` (+ local `TiRecentInvoiceTemplate` rebuilt: Status DataTriggers, TintColor/
  StatusColor via `BrushKeyConverter`, `OpenInvoiceCommand`), `TopCustomers` (`TiTopCustomerTemplate`
  with `ColorStringConverter` rank discs + `FractionToStarConverter` bars), `AgingBrackets`
  (`TiAgingRowTemplate` with SeverityColor bars).
- `StDashboardView.xaml`: hero + New Statement/View, `KpiCards`, `BalanceMixSlices`,
  OverdueAmountDisplay/HasOverdue, `RecentStatements` (`StStatementRowTemplate` rebuilt: Index
  disc, StatementNumber, SubTitle, HasAttachments, the invoiced/outstanding/overdue runs,
  `OpenPdfCommand` + `ShareCommand` with `IsEnabled={Binding HasPdf}`).
- **Commit + push.**

## Phase 7 — Consolidation & cleanup
- Grep-verify zero remaining consumers of the old `Db*`/`Soft*`/`Industrial*` keys outside
  non-dashboard views (Tax Invoice detail screens keep their own `CardStyle` — untouched).
- Retire the dead old dashboard keys from `DashboardKit.xaml` / `DashboardStyles.xaml` (only the
  keys with zero consumers; aliased consumers get migrated first).
- Final consistency pass: radii/gutters/type uniform, skeleton states match the new cards,
  `README` note in `Themes/Professional/` documenting the system.
- **Commit + push.**

---

## Rollback & risk
- Every phase is one commit → `git revert <sha>` restores the previous look with zero code risk.
- The old design system stays intact until Phase 7, so at no point do the app's two looks
  depend on each other.
- No .NET SDK in this sandbox: verification is (a) XAML well-formedness parse, (b) per-phase
  binding-inventory diff (zero missing paths/commands/x:Names), (c) grep proof that every `Pg*`
  key used is defined, (d) `dotnet build` + `dotnet test` recommended on a dev machine after
  each phase (the agent test suite pins dashboard behavior).
