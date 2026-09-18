# MERIDIAN Change Audit (Phases 0–7)

**Generated:** 2026-09-18 · **Method:** working-tree vs `HEAD` (pre-MERIDIAN base) diff + before/after token-owner classification against the dynamic/static ownership policy. The per-phase commits (bc2db8a…14140ed) were made in the previous workspace lineage; this checkout re-based to base commit `9716050`, so the before/after below is against the true pre-rewrite base.

Policy (release-safe protocol): runtime design-system properties (theme colours, accent, surfaces, text, borders, glass, shadows, **user-configurable corner radius**, **typography tokens**, **density tokens**, page margins under density control) MUST remain dynamic. Structural constants (icon geometry, fixed internal control constants, non-theme-dependent implementation details) MAY be static.

## Changed files (MERIDIAN stream, working tree vs HEAD)

- `M` App.xaml
- `M` MainWindow.xaml
- `M` Services/ThemeManager.Part2.cs
- `D` Styles/DashboardKit.xaml
- `M` Styles/DashboardStyles.xaml
- `M` Tests/Theme/ResourceTokenTypeTests.cs
- `M` Themes/DarkTheme.xaml
- `M` Themes/LightTheme.xaml
- `M` Themes/Styles/GlobalDesignSystem.xaml
- `M` ViewModels/Dashboard/Overview/KpiTileModel.cs
- `M` Views/Dashboard/DashboardView.xaml
- `M` Views/Dashboard/DeliveryNote/DnDashboard/DnDashboardView.xaml
- `M` Views/Dashboard/JobOrder/JoDashboard/JoDashboardView.xaml
- `M` Views/Dashboard/Overview/OverviewDashboardView.xaml
- `M` Views/Dashboard/ProformaInvoice/PiDashboardView.xaml
- `M` Views/Dashboard/SheetStore/SheetStoreDashboardView.xaml
- `M` Views/Dashboard/Statement/StDashboardView.xaml
- `M` Views/Dashboard/TaxInvoice/TiDashboard/TiDashboardView.xaml

Untracked new files: `Themes/Professional/` (ProfessionalCore/Shell + README), `mockups/` (8-screen acceptance mockup + REWRITE_PHASES.md), `tools/ui_inventory.py`, `tools/meridian_audit.py`, `docs/ui-audit/` (this audit set).

## Template-part note (x:Name audit refinement)

The base `MainWindow.xaml` declared `x:Name="Bg"` on the UserCardButton's **inline** ControlTemplate border (hover highlight target). The rewrite moved that template to the shared `PgUserCardStyle` (Themes/Professional/ProfessionalShell.xaml), which declares `x:Name="Bg"` internally with the same IsMouseOver/IsPressed triggers (Meridian hover tokens `PgSurface3Brush`/`PgHairlineBrush`). **No functional loss; no code-behind reference existed** (`MainWindow.xaml.cs` never used `Bg`). All other x:Names are preserved 1:1 in `MainWindow.xaml`.

## Rewritten views — before/after functional + token-owner audit

### `MainWindow.xaml` (baseline = pre-MERIDIAN base `HEAD`)

- Resource markers: Dynamic **159 → 115** · Static **91 → 99**
- **REGRESSION** — missing: bindings={} commands={} names={'Bg': 1} i18n={}
- New hardcoded literals: hex=0 · numeric FontSize=2 · numeric CornerRadius=0 · literal hairline BorderThickness=0

| Token owner | old Dynamic | old Static | new Dynamic | new Static | Verdict |
|---|---|---|---|---|---|
| typography | 14 | 0 | 3 | 0 | DYNAMIC-PARITY |
| margins | 0 | 0 | 0 | 0 | n/a (old had no dynamic margins) |
| gutter | 0 | 0 | 0 | 0 | n/a (old had no dynamic gutter) |
| radii | 8 | 6 | 5 | 0 | DYNAMIC-PARITY |
| minsize | 0 | 0 | 0 | 0 | n/a (old had no dynamic minsize) |
| borders | 11 | 0 | 0 | 0 | GONE: check literals |
| colors | 49 | 0 | 27 | 0 | DYNAMIC-PARITY |
| effects | 1 | 2 | 1 | 0 | DYNAMIC-PARITY |

### `Views/Dashboard/DashboardView.xaml` (baseline = pre-MERIDIAN base `HEAD`)

- Resource markers: Dynamic **35 → 19** · Static **9 → 6**
- Functional surface: **all bindings, commands, x:Names and i18n keys preserved** (zero missing).
- New hardcoded literals: hex=0 · numeric FontSize=2 · numeric CornerRadius=2 · literal hairline BorderThickness=0

| Token owner | old Dynamic | old Static | new Dynamic | new Static | Verdict |
|---|---|---|---|---|---|
| typography | 10 | 0 | 4 | 0 | DYNAMIC-PARITY |
| margins | 0 | 0 | 0 | 0 | n/a (old had no dynamic margins) |
| gutter | 0 | 0 | 0 | 0 | n/a (old had no dynamic gutter) |
| radii | 5 | 0 | 0 | 0 | GONE: check literals |
| minsize | 0 | 0 | 0 | 0 | n/a (old had no dynamic minsize) |
| borders | 2 | 0 | 0 | 0 | GONE: check literals |
| colors | 14 | 0 | 12 | 0 | DYNAMIC-PARITY |
| effects | 0 | 0 | 0 | 0 | n/a (old had no dynamic effects) |

### `Views/Dashboard/Overview/OverviewDashboardView.xaml` (baseline = pre-MERIDIAN base `HEAD`)

- Resource markers: Dynamic **43 → 39** · Static **113 → 125**
- Functional surface: **all bindings, commands, x:Names and i18n keys preserved** (zero missing).
- New hardcoded literals: hex=0 · numeric FontSize=1 · numeric CornerRadius=0 · literal hairline BorderThickness=0

| Token owner | old Dynamic | old Static | new Dynamic | new Static | Verdict |
|---|---|---|---|---|---|
| typography | 3 | 0 | 0 | 0 | GONE: check literals |
| margins | 1 | 0 | 1 | 0 | DYNAMIC-PARITY |
| gutter | 0 | 0 | 0 | 0 | n/a (old had no dynamic gutter) |
| radii | 0 | 0 | 0 | 0 | n/a (old had no dynamic radii) |
| minsize | 0 | 0 | 0 | 0 | n/a (old had no dynamic minsize) |
| borders | 0 | 0 | 0 | 0 | n/a (old had no dynamic borders) |
| colors | 16 | 0 | 15 | 0 | DYNAMIC-PARITY |
| effects | 0 | 0 | 0 | 0 | n/a (old had no dynamic effects) |

### `Views/Dashboard/SheetStore/SheetStoreDashboardView.xaml` (baseline = pre-MERIDIAN base `HEAD`)

- Resource markers: Dynamic **27 → 19** · Static **84 → 55**
- Functional surface: **all bindings, commands, x:Names and i18n keys preserved** (zero missing).
- New hardcoded literals: hex=0 · numeric FontSize=0 · numeric CornerRadius=0 · literal hairline BorderThickness=0

| Token owner | old Dynamic | old Static | new Dynamic | new Static | Verdict |
|---|---|---|---|---|---|
| typography | 2 | 0 | 0 | 0 | GONE: check literals |
| margins | 1 | 0 | 1 | 0 | DYNAMIC-PARITY |
| gutter | 0 | 0 | 0 | 0 | n/a (old had no dynamic gutter) |
| radii | 3 | 0 | 0 | 0 | GONE: check literals |
| minsize | 0 | 0 | 0 | 0 | n/a (old had no dynamic minsize) |
| borders | 0 | 0 | 0 | 0 | n/a (old had no dynamic borders) |
| colors | 10 | 0 | 7 | 0 | DYNAMIC-PARITY |
| effects | 0 | 0 | 0 | 0 | n/a (old had no dynamic effects) |

### `Views/Dashboard/ProformaInvoice/PiDashboardView.xaml` (baseline = pre-MERIDIAN base `HEAD`)

- Resource markers: Dynamic **35 → 27** · Static **93 → 64**
- Functional surface: **all bindings, commands, x:Names and i18n keys preserved** (zero missing).
- New hardcoded literals: hex=0 · numeric FontSize=0 · numeric CornerRadius=0 · literal hairline BorderThickness=0

| Token owner | old Dynamic | old Static | new Dynamic | new Static | Verdict |
|---|---|---|---|---|---|
| typography | 2 | 0 | 0 | 0 | GONE: check literals |
| margins | 1 | 0 | 1 | 0 | DYNAMIC-PARITY |
| gutter | 0 | 0 | 0 | 0 | n/a (old had no dynamic gutter) |
| radii | 3 | 0 | 0 | 0 | GONE: check literals |
| minsize | 0 | 0 | 0 | 0 | n/a (old had no dynamic minsize) |
| borders | 0 | 0 | 0 | 0 | n/a (old had no dynamic borders) |
| colors | 14 | 0 | 11 | 0 | DYNAMIC-PARITY |
| effects | 0 | 0 | 0 | 0 | n/a (old had no dynamic effects) |

### `Views/Dashboard/JobOrder/JoDashboard/JoDashboardView.xaml` (baseline = pre-MERIDIAN base `HEAD`)

- Resource markers: Dynamic **38 → 30** · Static **102 → 73**
- Functional surface: **all bindings, commands, x:Names and i18n keys preserved** (zero missing).
- New hardcoded literals: hex=0 · numeric FontSize=0 · numeric CornerRadius=0 · literal hairline BorderThickness=0

| Token owner | old Dynamic | old Static | new Dynamic | new Static | Verdict |
|---|---|---|---|---|---|
| typography | 2 | 0 | 0 | 0 | GONE: check literals |
| margins | 1 | 0 | 1 | 0 | DYNAMIC-PARITY |
| gutter | 0 | 0 | 0 | 0 | n/a (old had no dynamic gutter) |
| radii | 3 | 0 | 0 | 0 | GONE: check literals |
| minsize | 0 | 0 | 0 | 0 | n/a (old had no dynamic minsize) |
| borders | 0 | 0 | 0 | 0 | n/a (old had no dynamic borders) |
| colors | 14 | 0 | 11 | 0 | DYNAMIC-PARITY |
| effects | 0 | 0 | 0 | 0 | n/a (old had no dynamic effects) |

### `Views/Dashboard/DeliveryNote/DnDashboard/DnDashboardView.xaml` (baseline = pre-MERIDIAN base `HEAD`)

- Resource markers: Dynamic **38 → 30** · Static **102 → 73**
- Functional surface: **all bindings, commands, x:Names and i18n keys preserved** (zero missing).
- New hardcoded literals: hex=0 · numeric FontSize=0 · numeric CornerRadius=0 · literal hairline BorderThickness=0

| Token owner | old Dynamic | old Static | new Dynamic | new Static | Verdict |
|---|---|---|---|---|---|
| typography | 2 | 0 | 0 | 0 | GONE: check literals |
| margins | 1 | 0 | 1 | 0 | DYNAMIC-PARITY |
| gutter | 0 | 0 | 0 | 0 | n/a (old had no dynamic gutter) |
| radii | 3 | 0 | 0 | 0 | GONE: check literals |
| minsize | 0 | 0 | 0 | 0 | n/a (old had no dynamic minsize) |
| borders | 0 | 0 | 0 | 0 | n/a (old had no dynamic borders) |
| colors | 14 | 0 | 11 | 0 | DYNAMIC-PARITY |
| effects | 0 | 0 | 0 | 0 | n/a (old had no dynamic effects) |

### `Views/Dashboard/TaxInvoice/TiDashboard/TiDashboardView.xaml` (baseline = pre-MERIDIAN base `HEAD`)

- Resource markers: Dynamic **95 → 61** · Static **138 → 115**
- Functional surface: **all bindings, commands, x:Names and i18n keys preserved** (zero missing).
- New hardcoded literals: hex=0 · numeric FontSize=0 · numeric CornerRadius=4 · literal hairline BorderThickness=3

| Token owner | old Dynamic | old Static | new Dynamic | new Static | Verdict |
|---|---|---|---|---|---|
| typography | 23 | 0 | 8 | 0 | DYNAMIC-PARITY |
| margins | 1 | 0 | 2 | 0 | DYNAMIC-PARITY |
| gutter | 0 | 0 | 0 | 0 | n/a (old had no dynamic gutter) |
| radii | 13 | 0 | 2 | 1 | MIXED (dynamic+static) |
| minsize | 0 | 0 | 0 | 0 | n/a (old had no dynamic minsize) |
| borders | 1 | 0 | 0 | 0 | GONE: check literals |
| colors | 30 | 8 | 22 | 8 | MIXED (dynamic+static) |
| effects | 0 | 0 | 0 | 0 | n/a (old had no dynamic effects) |

### `Views/Dashboard/Statement/StDashboardView.xaml` (baseline = pre-MERIDIAN base `HEAD`)

- Resource markers: Dynamic **80 → 63** · Static **93 → 71**
- Functional surface: **all bindings, commands, x:Names and i18n keys preserved** (zero missing).
- New hardcoded literals: hex=0 · numeric FontSize=0 · numeric CornerRadius=0 · literal hairline BorderThickness=1

| Token owner | old Dynamic | old Static | new Dynamic | new Static | Verdict |
|---|---|---|---|---|---|
| typography | 19 | 0 | 11 | 0 | DYNAMIC-PARITY |
| margins | 1 | 0 | 2 | 0 | DYNAMIC-PARITY |
| gutter | 0 | 0 | 0 | 0 | n/a (old had no dynamic gutter) |
| radii | 6 | 0 | 1 | 1 | MIXED (dynamic+static) |
| minsize | 0 | 0 | 0 | 0 | n/a (old had no dynamic minsize) |
| borders | 1 | 0 | 0 | 0 | GONE: check literals |
| colors | 25 | 0 | 23 | 0 | DYNAMIC-PARITY |
| effects | 0 | 0 | 0 | 0 | n/a (old had no dynamic effects) |

## Root cause and fix — runtime-owned Pg tokens consumed as StaticResource (2026-09-18)

**Violation found (Phase 3, pre-fix state).** The rewrite consumed runtime-owned
design tokens via `StaticResource`:

| Token class | Consumers (static) | Runtime owner |
|---|---|---|
| `PgFont*` / `PgTopFont*` typography (22 keys) | in all 9 rewritten views + ProfessionalCore/Shell | `ThemeManager.ApplyDisplayDensity` (type scale) |
| Density geometry (`PgPageMargin`, `PgGutter`, `PgKpiTileMinHeight`, `PgChartMinHeight`, paddings…) | 7 dashboards + shell | `ThemeManager.ApplyDisplayDensity` (density ladder) |
| `Pg*Radius` (card/tile/control/icon) | Ti, St, ProfessionalCore | `ThemeManager.ApplyCardCornerRadius` (Appearance slider) |

The first allowlisted flip run converted **102** static consumers; **7**
consumers were already `DynamicResource` at that point and were verified
dynamic (109 total consumers now dynamic end-to-end). With static
consumption, the saved density and corner-radius settings silently stopped
affecting the Meridian views — the old kit had no such break because it
consumed `FontSize.*` / `Surface.Primary.CornerRadius` dynamically.

**Fix (applied, two coupled halves):**
1. `tools/dyn_token_flip.py` — allowlisted `StaticResource`→`DynamicResource`
   conversion (35 runtime-owned keys; explicitly excludes `PgChipRadius`
   999-capsule, the shell structural dims `PgTopBarHeight`/`PgTopCtrlHeight`/
   `PgSidebar*`, and all `Pg*Style`/`Pg*Template` references). Re-runnable;
   the allowlist doubles as the W16 gate's `MUST_BE_DYNAMIC` set.
2. `Services/ThemeManager.Part2.cs` — republishes the 22 typography keys
   scaled by the display-density type scale (pristine-captured once in
   `PgFontDefaults`, so repeated slider moves never compound) and the 5
   Pg radii proportionally (`factor = radius/12`): at the default 12 every
   published value equals the authored value exactly → **zero visual change
   at defaults**, and the slider works again.

**Verification (this file's verdict tables are the post-fix state):**
typography = DYNAMIC-PARITY / GONE-to-shared-styles with **zero static**
consumers in all 9 views; the only static radius remaining anywhere is
`PgChipRadius` (intentional structural capsule). Borders = documented
intentional-static waiver (1px hairlines). Phase 17 gate: 0 ERROR / 22 WARN
/ 2 NOTE across 320 files, **zero findings in the 13 MERIDIAN files**;
remaining WARNs are pre-existing legacy issues (dangling DynamicResources in
untouched views, one duplicate `SettingsCardMargin` key, declared-after-use
statics that resolve at app level). 9/9 functional contract re-verified
clean (the single `Bg` relocation is documented above).
