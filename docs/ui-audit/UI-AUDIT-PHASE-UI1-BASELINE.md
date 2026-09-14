# ProGlass Automation — Master Industrial ERP UI Audit
## PHASE UI-1 — Audit + Baseline Report

Generated: 2026-09-14 · HEAD before work: `db64d23` (clean tree, 0 modified)
Method: static repository audit (268 XAML, 163 user-facing views). **No source modified in this phase.**
Verification basis: static only — **no Windows/WPF build or runtime was available**, so all scores are structural, not visual. Do not treat as visual confirmation.

---

## A. GLOBAL SCORE

| | |
|---|---|
| **Current overall** | **6.8 / 10** (68/100) |
| **Target overall** | **8.5 / 10** (85/100) |
| Strongest foundations | Color + typography are 100% token-driven (0 literal colors, 0 literal FontSize). Margins sit on a clean 4px grid (0 off-grid). One dominant button family (Pill, 388 uses). Mature density + surface + module-style systems already exist. |
| Weakest foundations | Spacing is grid-consistent but **not tokenized** (`Space.*` used in 0 views). 7 parallel button families. 161 hardcoded labels bypass localization. 126 fixed-height containers. Density switching does not reflow page spacing. |

---

## B. MODULE SCORE TABLE

Score model: Layout hierarchy, IA, Spacing, Typography, Buttons, Icons, Surfaces, Data density, Workflow, Industrial suitability — each /10 → /100 → /10.

| Module | Current | Target | Priority | Main issue |
|---|---|---|---|---|
| Dashboard | 7.2 | 8.8 | Medium | Good; KPI strip uses `Db*` density tokens. Verify KPI reflow at Compact. |
| Inquiry | 6.6 | 8.5 | Medium | List + editor; workflow steps fine, editor card-heavy (23 SurfacePrimary). |
| Customers / Company | 6.8 | 8.3 | Low | Simple, consistent; low information density. |
| Sheet Store | 7.0 | 8.6 | Medium | Strong button consistency (53 Pill); 17 fixed heights, 7 detail windows. |
| Proforma Invoice | 6.8 | 8.7 | **High** | 9-step wizard well-structured but heavy; 16 module-local buttons; 31 literal `Bold`; its own `Pi*` button family. |
| PI Specifications (Step 4/5) | 6.2 | 8.4 | **High** | 14 literal `Bold`; 6-col UniformGrid; DataGrid density. |
| Specifications Center | 7.0 | 8.5 | Medium | KPI row now guttered; 6-col KPI; table + detail OK. |
| Job Orders | 7.2 | 8.8 | Medium | Good detail action bar + GodView; 10 SurfaceDetail. |
| Delivery Notes | 7.2 | 8.8 | Medium | Good detail action bar; 38 SurfacePrimary. |
| Tax Invoices | 7.2 | 8.8 | Medium | Good detail action bar + row buttons. |
| Statements | 6.6 | 8.5 | Medium | List OK; generator wizard uses its own `NavButton*` family; 21 fixed heights. |
| Reports | 6.4 | 8.6 | **High** | **Card bloat (296 SurfacePrimary) + 42 legacy `SecondaryButtonStyle`** + 15 WrapPanels. Good list shell (KPI strip/filter/states). |
| Profit & Loss | 6.0 | 8.3 | Medium | Custom; 2 negative margins; non-standard surfaces. |
| Job Profitability | 6.0 | 8.3 | Medium | 21 SurfacePrimary; 14 literal `Bold`; heavy ALL-CAPS labels; `JpChip`/`JpCompact` local styles. |
| Production | 6.4 | 8.6 | **High** | 50+22 surfaces; 16 fixed heights; control center uses WrapPanel of 12 tiles + legacy buttons. Industrial but card-heavy. |
| Optimization | 5.8 | 8.4 | **High** | **Own parallel KPI style family** (`OptKpiCard/Badge/Header/Value`) + hardcoded "OPTIMIZATION"/"CONFIRMED JOBS" labels. |
| Settings | 6.6 | 8.5 | **High** | Left-nav + content is right, but **143× SurfacePrimary→SurfaceDetail nesting** (card-in-card-in-card) + 7 WrapPanels. |
| About | 7.4 | 8.6 | Low | Recently polished; 11 fixed heights; local `AboutPillButtonStyle`. |
| User / Profile | 6.8 | 8.3 | Low | Consistent, low density. |
| Global Search | 7.0 | 8.4 | Low | Control is clean; 1 literal `Bold`. |
| Dialogs (Shared) | 7.0 | 8.4 | Low | Consistent `ThemedDialogWindow`; 4 legacy `SecondaryButtonStyle`. |
| Attachments / Documents | 6.8 | 8.3 | Low | Row-action buttons consistent. |
| Login | 7.0 | 8.4 | Low | Glass auth surface; 31 SurfacePrimary; 6 fixed heights. |
| Splash | 7.2 | 8.3 | Low | Fine; 6 fixed heights. |

---

## C. GLOBAL (SYSTEMIC) PROBLEMS — evidence-based

1. **Spacing is not tokenized (highest-leverage issue).** `Space.Xs/Sm/Md/Lg/Xl/2Xl/3Xl` and `Space.Page/Card/Tile/Gutter/Section/Control/Toolbar/Dialog` are all defined in `GlobalDesignSystem.xaml`, yet are referenced in **0** view files. Every view hardcodes raw `Margin`/`Padding`. Consequence: the existing density engine (`ThemeManager.ApplyDisplayDensity`) republishes `Density.*`/`Db*`/`ProGlassLayout.*` but **view spacing never reflows with density** — Compact does not become genuinely compact at the page level.
   - Evidence: `Space.*` usage in views = 0; raw padding values observed: `8,3`, `10,7`, `10,3`, `12,10`, `14,12`, `16,14` (off the semantic scale), vs defined `Space.Card=12,9` / `Density.CardPadding`.
2. **Seven parallel button families.** Pill (388, dominant), legacy GDS `PrimaryButtonStyle`/`SecondaryButtonStyle` (19, still live in Reports 42×, Production, CrashReports, Shared, AI, Splash), Report-Wizard `NavButton*`/`StepBadge` (23), module-local (40: `Pi*`, `Jp*`, `Crash*`, `Hud*`, `Terms*`, `AboutPill`…), and a **dead `ProGlassButton.*` family** (`ComponentTokens.xaml`, **0** usages). Inconsistent geometry/hierarchy across modules.
3. **161 hardcoded ALL-CAPS English labels** bypass the `en-US.xaml` localization resource (Optimization, JobProfitability, PI, Reports, Production, SheetStore — e.g. `"OVERALL MARGIN"`, `"CONFIRMED JOBS"`, `"TOTAL SQM"`, `"PI VALUE"`). Not localizable; visually shouts; breaks the typography hierarchy (everything-caps).
4. **126 fixed-height containers (≥40px)** on layout surfaces (Reports 21, SheetStore 17, Production 16, Dashboard 12, About 11). Clipping/overflow risk at 1280×720 and in Compact density.
5. **47 WrapPanels** in toolbars/KPI strips (Reports 15, About 7, Settings 7, TI 4, DN 3, Production 3, Inquiry 2, JO 2). Uneven reflow on resize; not an industrial control surface.
6. **Card-in-card nesting in Settings** — 147 `SurfacePrimary` wrapping 143 `SurfaceDetail` (L1→L2 everywhere, frequently L3 inside). Violates the 3-level surface rule; looks like a "pile of floating boxes."
7. **Reports over-carded** — 296 `SurfacePrimary` borders + 42 legacy `SecondaryButtonStyle`. Data-heavy reports rendered as card soup; old button geometry.
8. **Module header/toolbar standard only partially adopted** — `ModulePageTitleStyle` (29), `ModulePageSubtitleStyle` (30), `ModuleDetailTitleStyle` (3), `ModuleListKpiLabelStyle` (79) exist and are good, but Optimization uses raw `"OPTIMIZATION"` text and Production uses raw `Title` bindings instead of the standard.
9. **Optimization has its own KPI system** — `OptKpiCard`/`OptKpiBadge`/`OptHeader`/`OptKpiValue` (`OptimizationStyles.xaml`) instead of the global surface/KPI system. A second card system.
10. **Empty/loading/error states under-adopted** — `EmptyStateControl` (17) and `LoadingStateControl` (16) exist and are good, but only **26/202** files reference them. Most data views lack professional states.
11. **Density switching is partial** — control heights + `Db*`/`Kpi`/`Chart` reflow, but page margins/section gaps/card padding in views are hardcoded, so density has limited effect on overall page rhythm.
12. **18 hardcoded `CornerRadius`** (values 6/3/4/0, mostly small chips) — minor; the rest correctly use `Radius.*`/`CardCornerRadius`/`ControlCornerRadius`.
13. **Icon system is now consistent** (from prior rounds) — Tag-glyph Pill/Row templates + `FontFamily.Icon`; only 2 stray non-Unicode symbols remain (`WastagesSectionView` ✓ `&#x2713;` / ⚠ `&#x26A0;`).

---

## D. GLOBAL SOLUTION — mapped to EXISTING resources (no new systems)

| Concern | Use this existing resource (do NOT create new) |
|---|---|
| Page padding | `Density.PagePadding` / `ProGlassLayout.PageMargin` (live, density-aware) |
| Card-to-card gap | `Density.Gutter` / `Space.Gutter` (live) |
| Card internal padding | `Density.CardPadding` / `Space.Card` (12,9) |
| Section internal gap | `Space.Md` (12) / `Space.Lg` (16) |
| Control-to-control | `Space.Sm` (8) |
| Icon-to-text | 8 (Pill/Row templates already 6–10) |
| Control heights | `Density.ControlHeight` / `ControlHeightSmall/Medium/Large` (live) |
| Row height | `Density.RowHeight` / `ProGlassLayout.RowHeight` (live) |
| **Buttons** | **Pill/Row family only**: Primary=`PrimaryPillButton`, Secondary=`OutlinedPillButton`, Ghost=`Button.Ghost`, Destructive=`DangerButton`, Success=`SuccessPillButton`, icon-only=icon Pill, row actions=`RowPrimary/Success/Outlined/DangerButton`. Deprecate+alias dead `ProGlassButton.*` and legacy GDS `Primary/SecondaryButtonStyle`. |
| **Surfaces** | L1=`SurfacePrimaryStyle`, L2=`SurfaceDetailStyle`, L3=`SurfaceKpiStyle`. **Max 3 levels, forbid 4-deep nesting.** Prefer separators/whitespace over nested cards. |
| **Typography** | `FontSize.*` + `Weight.*` (already 100% tokenized). Module title=`ModulePageTitleStyle`, subtitle=`ModulePageSubtitleStyle`, data label=`ModuleListKpiLabelStyle`. |
| **Icons** | Existing Tag-glyph system + `FontFamily.Icon`. No raw Unicode. |
| **Data grid** | `DataGridStyles.xaml` + `Density.RowHeight`; standardize header/row/numeric-right-alignment/status. |
| **States** | `EmptyStateControl` / `LoadingStateControl` in every data view. |
| **Module shell** | `ModuleListShellStyle` + `ModuleListKpiStripStyle` + `ReportFilterCardStyle` + `ModulePageTitleStyle` across all list modules. |
| **Standard page structure** | Module Header → KPI/Context (when useful) → Toolbar/Filter/Search → Main Surface → Sections → Summary/Footer Actions. |

Three-level surface hierarchy, module families (Commercial / Factory / Analytics / Master Data), PI 4-zone structure, JO factory-execution hierarchy, Production workflow strip (Released→…→Delivered), Optimization engineering workspace (KPI strip + nesting canvas primary), Dashboard operational-not-decorative, Settings left-nav center — all implemented **on top of the existing systems above**, no rewrites.

---

## E. PHASE PLAN

| Phase | Scope | Files/components | Expected improvement | Risk |
|---|---|---|---|---|
| **UI-1** (done) | Audit + baseline | this document | Shared truth | None |
| **UI-2** | Global spacing primitives → make density reach page level | `GlobalDesignSystem.xaml` (map `Space.*`→`Density.*`), `ThemeManager.Part2.cs` (republish `Space.*` aliases per density) | Density reflows page/card/section spacing; Compact genuinely compact | **Medium-High** — touches every surface; needs Windows visual verification per theme/density |
| **UI-3** | Button unification | `ComponentTokens.xaml` (alias/retire dead `ProGlassButton.*`), views using legacy `Secondary/PrimaryButtonStyle` (Reports, Production, CrashReports, Shared, AI, Splash) | One button grammar, consistent hierarchy | **Medium** — visual; verify per module |
| **UI-4** | Module shell standard | Header/toolbar in Optimization, Production, PI, JobProfitability (adopt `ModulePageTitleStyle`/list shell) | Consistent module headers | Medium |
| **UI-5** | Commercial (Inquiry, PI, TI, Statement, P&L) | PI 9 steps, footer, `Pi*` buttons → Pill; localize 161 labels | Professional commercial workflow | Medium |
| **UI-6** | Factory (JO, DN, Production) | Production control center (WrapPanel→UniformGrid, legacy buttons), JO | Industrial factory feel | Medium |
| **UI-7** | Analytics/Reports | Reports de-carding (296→targeted surfaces), 42 legacy buttons, WrapPanels→grids | Efficient data density | **High** — largest surface count |
| **UI-8** | Settings/About/Profile | De-nest Settings (143 L2), About restraint | Clean ERP settings center | Medium |
| **UI-9** | Production + Optimization specialized | Optimization `OptKpi*`→global KPI, nesting canvas primary | Engineering workspace | Medium |
| **UI-10** | Cross-theme + density QA | All 5 theme families × Light/Dark × 4 densities × 1280/1366/1920 | No regressions | **Requires Windows runtime** |

**Ordering rule:** complete + verify one family at a time; do not mass-edit all views simultaneously.

---

## F. IMPLEMENTATION LOG (this turn)

- **Files inspected:** all 268 XAML (163 user-facing), `App.xaml` (41-dict merge order), `Themes/` + `Styles/` (4,109 resource keys), `Services/ThemeManager.Part2.cs` (density engine), button/surface/inventory matrices.
- **Files modified:** **0** (Phase UI-1 is audit-only, per "do not start by editing XAML" + protect-first).
- **Shared resources changed:** none.
- **Business logic / bindings / navigation / commands / theme engines:** untouched.

---

## G. VERIFICATION (honest)

| Check | Result |
|---|---|
| Windows build | **NOT performed** — no Windows/WPF SDK in this environment. |
| Runtime launch | **NOT performed.** |
| Theme switching / density / resize / interaction | **NOT verified.** |
| Static audit completeness | Inventory + token + button + surface + spacing + typography + states all measured. |
| XAML validity of current tree | Unchanged from `db64d23` (already validated in prior rounds). |

No visual claim is made. Every score above is structural.

---

## H. REMAINING ISSUES / OPEN

1. All of UI-2…UI-10 are unimplemented (by design — each needs Windows visual verification before/after).
2. `ProGlassButton.*` (ComponentTokens) confirmed dead (0 uses) — candidate for alias/retire in UI-3, pending code-behind DynamicResource check.
3. 161 hardcoded labels need en-US keys (localization pass, UI-5/6/7).
4. 126 fixed heights + 47 WrapPanels need a resize pass (UI-10).
5. **Needs the user's Windows environment to verify** any visual change; none applied yet.

### Recommended first implementation (pending your go-ahead + a Windows machine to verify)
**Phase UI-2, minimal safe slice:** make the surface hierarchy density-aware by having `ThemeManager.ApplyDisplayDensity` also republish the static aliases `Space.Card`→`cardPad`, `Space.Tile`→`stripPad`, `Space.Page`→`pageMargin` (they already republish `Density.CardPadding`/`Density.PagePadding`). This is a **one-code-location, existing-system** fix (no new engine) that makes `SurfacePrimary/Detail/Kpi` padding follow density. Default density is **Compact**, so this changes default card padding (12,9→8,6) — a visible change that must be confirmed on Windows across all 5 themes before it is considered done.
