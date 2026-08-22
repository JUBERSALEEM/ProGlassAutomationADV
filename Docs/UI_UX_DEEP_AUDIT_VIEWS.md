# Global UI/UX Deep Audit — all Views (133 XAML files)

**Scope:** `Views/**` (133 files) + shared `Styles/DashboardStyles.xaml`, theme keys
**Method:** scripted scans for sharpness flags, hardcoded colours/radii/fonts, emoji
icons, micro fonts, low opacity, effect overuse + targeted deep-reads of the
dashboard system, glass/auth, placeholders, and shared chrome.
**Verdict:** **6.2 / 10** — strong bones, two real design systems already in place
(Dashboard + list chrome), but the "dull / blurred" feel is real and traceable.

---

## 1. Why the UI reads "dull and blurred" — root causes

| # | Cause | Evidence | Fix |
|---|---|---|---|
| B1 | `TextFormattingMode="Display"` on **86 files** | Display uses ideal-width metrics tuned for *large* headings; on body text, table cells and 9–12px labels it produces slightly-off spacing and a soft look | Use `Ideal` for forms/tables/lists; keep `Display` only on hero titles |
| B2 | `SnapsToDevicePixels` missing on **14 files** (list below) | 1px borders land on half-pixels → blurry hairlines | Add `SnapsToDevicePixels="True"` + `UseLayoutRounding` |
| B3 | `TextHintingMode="Fixed"` set in only **7** files | default hinting can blur static text on some renderers | Add `Fixed` (or `Animated` only where text animates) |
| B4 | **58 `DropShadowEffect`/`BlurEffect`** (27 in StatementGenerator) | pixel-shader re-run each frame → softness + scroll jank | Keep one frozen shadow per card tier; remove from rows/tiles |
| B5 | Glass/translucent surfaces without a backdrop | About/Login/Splash lay light text on translucent white without enough backing contrast in Light theme | Raise glass opacity + strengthen foreground on light surfaces |
| B6 | Emoji as icons (~35 sites) | render via colour font, ignore `Foreground`, look like clip-art vs MDL2 | MDL2 only; emoji acceptable only in user-authored text |

**Files missing `SnapsToDevicePixels`:** JobOrderPickerDialog, OptimizationView,
all 8 Production views, SavedInvoicesListDialog, InvoiceMainSummaryView,
ComingSoonView, SheetStoreUseDialog.

---

## 2. Visibility & contrast issues

| # | Issue | Count / where | Fix |
|---|---|---|---|
| C1 | **138 hardcoded hex colours** | PrintableInvoice (17), StatementGenerator Preview (9), Splash (7), SettingsAppearance (7), AboutStyles (6), RecordOverlay (4), SalesReport (4), DnOverview (4), AiChatView (4) | Move to theme brushes; keep hex only in fixed dark stages + print paper |
| C2 | **70 text elements ≤ 9px** | PrintableInvoice (20), DnListView (5), TiFamily (4), DnHistory (4), AiChat (4) | Floor at 10px (print views excepted); 9px is sub-4.5pt-equivalent |
| C3 | Hardcoded light-grey text on light surfaces | `#CBD5E1`, `#E0E0FF` used as text in a few spots | Replace with `SecondaryForegroundBrush` |
| C4 | `Foreground="#FFFFFF"` on `AccentBrush` (white-on-accent) | common in buttons/pills | OK for dark accents, but fails on light accents (e.g. Amber) — use `OnAccentForegroundBrush` |
| C5 | ComingSoonView gradient `#1E88E5→#1565C0` hardcoded | 9 modules (Optimization + 8 Production) | Theme-tint the placeholder card |

---

## 3. Consistency issues

| # | Issue | Count / where | Fix |
|---|---|---|---|
| D1 | **187 hardcoded `CornerRadius="N"`** | across all module views | route through `ControlCornerRadius`/`CardCornerRadius` so the global slider applies |
| D2 | **10 hardcoded `FontFamily`** (non-MDL2) | Login/Reset/ForcePassword, dialog internals | `{DynamicResource AppFontFamily}` |
| D3 | Mixed icon language | MDL2 glyphs **and** emoji (🤖💬🎤📄📋📈⚙🔴🟢🕓⚠️🛠) **and** text chars (✓✕➤) | one icon system: MDL2 for chrome, ✓/✕/⚠ only as semantic status marks |
| D4 | Two dashboard systems coexist | `DashCardStyle` (radius 10) vs `SoftCardStyle` (radius from SoftCardRadius) | keep one; deprecate the unused set |
| D5 | `DashCardRadius`/`SoftCardRadius` are `StaticResource` | dashboards ignore the global radius slider | make dynamic (fallback token) |
| D6 | Micro-type scale inconsistency | 9px pills vs 10px captions vs 11px labels ad hoc | adopt the DashboardStyles 6-step scale app-wide |

---

## 4. Bugs / structural risks

| # | Issue | Detail |
|---|---|---|
| R1 | **9 modules are placeholders** | Optimization + Cutting/Polish/Edge/Mitring/Tempering/Pairing/Doubling/Holes all render `ComingSoonView` — no real content |
| R2 | ComingSoonView has no `SnapsToDevicePixels`/`TextRenderingMode` | blurry glyph + soft title |
| R3 | About image tiles lack `BitmapScalingMode="HighQuality"` | AboutView + DeveloperSection app-icon can look soft at non-integer sizes |
| R4 | Low-opacity empty-state glyph `Opacity="0.4"` (HistoryView) | intentional, but on light theme it nearly vanishes — use muted brush instead |
| R5 | Fixed widths on rows (130/150/170px) in list views | can clip long driver/vehicle/project text on 1366px screens |

---

## 5. What's already good (do NOT regress)

- `DashboardStyles.xaml` is a genuinely senior design system (6-step type scale, 4px grid, perf-aware shadow removal, theme-correct brushes).
- List-page chrome (`ListPageStyles`, `DataGridStyles`, `InputStyles`, `DialogStyles`) is centralized and theme-aware.
- Glass/Auth/Splash are internally consistent dark stages with frozen brushes.
- The font engine, radius token, and active-nav work from earlier passes are solid.

---

## 6. Redesign plan (phased, low → high risk)

1. **Sharpness pass (global, low risk)** — add `SnapsToDevicePixels`+`UseLayoutRounding` to the 14 files; flip `TextFormattingMode` to `Ideal` on forms/tables (keep Display on titles); add `TextHintingMode=Fixed`; HighQuality on image brushes.
2. **Visibility pass** — raise ≤9px text to 10px (print excepted); replace low-opacity glyphs with muted brushes; fix white-on-accent → OnAccentForeground.
3. **Consistency pass** — convert 187 hardcoded radii → tokens; 10 fonts → AppFontFamily; retire the unused dashboard style set; unify the icon language (emoji → MDL2).
4. **Contrast/theme pass** — move remaining 138 hardcoded hex into theme brushes (except fixed dark stages + print); theme-tint ComingSoonView.
5. **Module-by-module redesign** (after the global passes): Placeholders → real ComingSoon (better visual) or module shells; then PI wizard steps, DN/JO/TI details, SheetStore, Reports, Settings.

**Target after phases 1–4:** 6.2 → **8.5/10**; after module redesign → **9.5/10**.

---

## Suggested first commit (sharpness + visibility, pure win)

The 14 missing-snapping files + TextFormattingMode correction + ≤9px floor + emoji→MDL2 in Shared dialogs is entirely low-risk and directly attacks the "blurred/dull" complaint.

Say **"go"** and I'll implement phases 1–2 first, build-check, and push.

---

## Phase 3 — consistency pass (DONE)

Committed as **"Phase-3 consistency pass (Views)"** on `arena/01a02595-proglassautomationadv`.

### D5 — dashboards respect the global radius slider ✅
- `Styles/DashboardStyles.xaml`: `DashCardRadius`/`DashTileRadius`/`DashPillRadius` and
  `SoftCardRadius`/`SoftHeroRadius`/`SoftPillRadius`/`SoftBadgeRadius` retired (they were
  hardcoded `StaticResource`, so dashboards ignored the Settings→Appearance radius slider).
- All 8 setters now resolve through the theme tokens `CardCornerRadius` /
  `ControlCornerRadius` / `CardCornerRadiusLarge` (dynamic) → every dashboard/tile/pill
  follows the global slider.
- Both style families (`DashCardStyle` *and* `SoftCardStyle`) are actively used across the
  13 dashboards, so **neither was retired wholesale** (D4 deferred to the module pass);
  the two systems are now radius-consistent.

### D1 — hardcoded radii → tokens (card surfaces) ✅
- 10 report **page cards** (`CornerRadius="10" Margin="16" Padding="20"`) across
  Company/Delivery/GlassSpec/Jo/Pi/Profitability/Project/Salesman/Statement/TaxInvoice
  reports → `CardCornerRadius`.
- `SalesReportView`: brand header, footer, 4 stat cards, overview hero, workflow banner → tokens.
- `SettingsDemoSection`: 7 redundant `CornerRadius="12"` overrides → `CardCornerRadius`.
- `RenameAttachmentDialog` close-button → `ControlCornerRadius`.
- **Intentional exceptions left hardcoded:** circular icon tiles (28–38px, r7–r10), status
  dots (4–10px), and capsule pills (r20) — those radii are size-proportional and mapping them
  to the 8–18px tokens would visually round them into wrong shapes.

### D3 — icon language unified (emoji → Segoe MDL2) ✅
- `AiChatView`: send (E724), robot/bot (E99A), chat (E8BD), mute (E74F), volume (E767),
  mic (E720), stop (E71A), processing-sync (E895), close (E711) — all with `Segoe MDL2 Assets`.
- Statement Generator: document (E8A5), attach (E723), download (E896), clipboard (E8C8),
  library (E8F1), merged-tab (E8A5 run), chart (E9D9), line-chart (E9D2).
- `GradientHeroCard` icon badge: MDL2 font + neutral Page fallback (was ✨).
- Kept as semantic marks per the audit: `✓`/`✕`/`⚠`; kept `⚖` (no MDL2 equivalent) and the
  `🥇🥈🥉` medals (no MDL2 equivalent, and the view comments confirm the model `StatusIcon`/
  `MedalIcon` emoji are **never rendered** in the TiDashboard).

### D2 — fonts ✅
- The only remaining hardcoded `FontFamily` is `Consolas` for intentional monospace
  (account number / proforma id / pricing / terminal) — correct, left in place.

### Pre-existing (NOT from this pass)
- `verify_all.sh` reports a `using_check` FAIL on `Views/Reports/SalesReportView.xaml.cs`
  (`'EasingMode' needs 'using System.Windows.Media.Animation;'`). This is a **tool false
  positive** — the file already uses the fully-qualified
  `System.Windows.Media.Animation.EasingMode.EaseOut`, so no `using` is required. It fails
  identically at HEAD (pre-existing), unrelated to these changes.

---

## Phase 4 — contrast/theme pass (DONE)

Scope from §6: move remaining hardcoded hex into theme brushes (except fixed dark
stages + print paper) and theme-tint `ComingSoonView`. Applied to the four files
where hardcoded hex actually caused theme breaks or contrast regressions.

### C5 — ComingSoonView theme-tinted ✅
- `Views/Shared/ComingSoonView.xaml`: the hardcoded blue gradient `#1E88E5 → #1565C0`
  (9 placeholder modules) now resolves through `AccentColor → AccentDarkColor`, so every
  placeholder tile follows the active accent preset and Light/Dark swap.
- C4 also applied here: the glyph and `COMING SOON` pill now use
  `OnAccentForegroundBrush` instead of `Foreground="White"` (was white-on-accent).

### C1 — hardcoded hex → theme brushes (contrast fixes) ✅
- **Statement Generator `PreviewStep.xaml`:**
  - KPI / reconciliation value text was hardcoded dark hues — `#3B0764` (VAT), `#052E16`
    (PAID/Delivered), `#451A03` (Outstanding/Balance), `#450A0A` (Overdue). On dark theme
    these nearly-vanished against the dark tint cards. Now `PrimaryForegroundBrush`,
    matching the INVOICED card which already used it.
  - `#B91C1C` (Deselect hover border) → `DangerBrush`.
  - `#D1D5DB` (diff-pill border) → `BorderBrush`.
- **`AiChatView.xaml`:** error-bubble text `#991B1B` → `DangerBrush` (dark theme: dark-red
  on `TintRedBrush` was unreadable; `DangerBrush` lifts to `#F87171`).
- **`SettingsSectionStyles.xaml`:** `ModernToggleSwitch` no longer animates to fixed
  `#1D4ED8` / `#CBD5E1` — the ON track now animates to `AccentColor` (follows the accent
  preset) and the OFF track uses the neutral `BorderBrush`/`BorderColor` (follows theme).

### Intentional exceptions left hardcoded (NOT migrated)
- **Fixed dark stages** (per §6 carve-out): `SplashView`, `Login/*`, `About/*` — internally
  consistent dark stages with frozen brushes; not re-themed.
- **Dark hero/brand headers inside themed views**: the dark gradient heroes in
  `DeliveryNoteDetailView` / `TaxInvoiceDetailView` / `DnSectionStyles` / `TiFamilySection`
  and the `SalesReportView` brand header use white-alpha text (`#CCFFFFFF`, `#E0E7FF`, …)
  that is correct *on* the fixed dark surface — not a Light-theme issue.
- **Print paper**: `PrintableInvoiceView`, `PrintPreviewView` — fixed white paper + print
  inks by design.
- **Statement Generator violet brand**: the violet/pink wizard gradient
  (`#8B5CF6`/`#7C3AED`/`#EC4899`/`#06B6D4`), glass white-alphas (`#FFFFFF33`…) and the
  violet selected-chip (`#FAF5FF`/`#DDD6FE`) are the wizard's identity — deferred to the
  §6 module redesign (phase 5), not a contrast fix.
- **Literal palettes / brand marks**: the accent-picker swatches in
  `SettingsAppearanceSection`, provider logo dots (`#4285F4` Google / `#8B5CF6`),
  the always-dark `RecordOverlayWindow` HUD, and `Color="#000000"` drop-shadows.
- **AiChat dark header** (`#E0E0FF` subtitle, `#33FFFFFF` icon bg, `#FFFFFFAA` timestamp)
  — white-alpha on the fixed dark chat header, correct as-is.

---

## Phase 5 — module-by-module redesign (IN PROGRESS)

§6 item 5: "Placeholders → real ComingSoon (better visual) or module shells; then
PI wizard steps, DN/JO/TI details, SheetStore, Reports, Settings."

### 5.1 — Placeholders → better ComingSoon ✅
Chose the **"better visual ComingSoon"** route (not module shells yet — those need
per-module product decisions on real fields/columns, a separate effort).

- **`Views/Shared/ComingSoonView.xaml` rebuilt** from a bare centered stack into a
  full-page hero:
  - **R2 fixed** — `SnapsToDevicePixels` / `UseLayoutRounding` / `TextRenderingMode=
    ClearType` / `TextHintingMode=Fixed` added (the glyph + title were soft).
  - `TextFormattingMode=Ideal` (B1) with `ClearType` on the body/description.
  - Layout: accent-gradient icon badge (96px, soft shadow) → tinted "COMING SOON"
    status chip → 28px module title → wrapped description → muted roadmap hint.
  - Ambient `DashAmbientBrush` wash behind the hero so placeholders sit on the same
    soft tinted stage as the dashboards.
  - Fully theme-correct (tokens only); C4/C5 carried over from Phase 4.
- **D3 — placeholder icons emoji → Segoe MDL2** across the 9 `ComingSoonViewModelBase`
  subclasses. Glyphs match the canonical set already used by `AboutViewModel`'s
  production stages (single source of truth), so nav → About → placeholder are
  glyph-consistent:
  | Module | Glyph | Module | Glyph |
  |---|---|---|---|
  | Optimization (⚙) | `E713` | Mitring (🔺) | `E7E8` |
  | Cutting (✂) | `E8C6` | Tempering (🔥) | `E728` |
  | Holes & Cutouts (🕳) | `E71D` | Pairing (🔗) | `E71B` |
  | Polish (✨) | `E895` | Doubling/Lamination (📚) | `E81C` |
  | Edge Deletion (📐) | `E774` | | |

### 5.2 — remaining visibility/quality findings (R3, R4, C2) ✅
Folded the last open global polish items into the module pass (they were deferred
by the earlier sharpness/visibility sweeps but are concrete and low-risk):

- **R3 — HighQuality image scaling** ✅
  `RenderOptions.BitmapScalingMode="HighQuality"` added to the app-icon `ImageBrush`
  in `AboutView.xaml` + `About/Sections/DeveloperSection.xaml` (was soft at
  non-integer sizes).
- **R4 — low-opacity empty-state glyphs → muted brush** ✅
  `DnAttachmentsSection.xaml` + `TiAttachmentsSection.xaml` "No attachments yet"
  glyphs: `Opacity="0.4"` (nearly vanished on light theme) → `Foreground=
  {DynamicResource MutedForegroundBrush}`.
- **C2 — micro-text floor at 10px** ✅
  Bumped `FontSize="9"` → `10` on **43 elements / 19 files** (non-print views):
  AiChat (TRY ASKING, timestamps, provider line), DN/TI status badges + history +
  family chips, SheetStore STOCK/USED/BALANCE, Settings toggles, ChangesDialog,
  Diagnostics, UserCenter "SIGNED IN", CompanyCenter "DEFAULT", etc.
  **Kept at 9px:** the two MDL2 *icon* glyphs (AiChat stop-button `E711`,
  `OtherChargesView` chevron `E70D`) — those size an icon, not text.

### 5.3 — B2 closed: snapping on the 9 placeholder wrappers ✅
The audit listed 14 files missing `SnapsToDevicePixels`/`UseLayoutRounding`.
Re-checked: the 4 dialog/window files (JobOrderPickerDialog, SavedInvoicesListDialog,
InvoiceMainSummaryView, and the SheetStoreUse area) **already carry**
`SnapsToDevicePixels` + `UseLayoutRounding` + `ClearType`/`Fixed` hinting from the
earlier passes. `ComingSoonView` was fixed in 5.1. The remaining 9 thin wrapper views
(`OptimizationView` + 8 Production views) now declare `UseLayoutRounding` +
`SnapsToDevicePixels` on their root `UserControl`, completing the B2/R2 checklist.

### 5.4 — C4: white-on-accent → OnAccentForegroundBrush ✅
Replaced **64 hardcoded `Foreground="White"`/`#FFFFFF`** that sit directly on
`AccentBrush` with `{DynamicResource OnAccentForegroundBrush}` (theme-derived,
inverts to dark text when a light accent preset like Amber is selected — see
`Services/ThemeManager.cs` + `ColorMath.cs`). Scope verified with an XML-aware
ancestor scan (not a naive grep), so only genuine accent-surface text changed:
- KPI cards + column headers: `StatementListView`, `SheetStoreView`,
  `DiagnosticsView`, `SectionActivityView`.
- Accent buttons: God views (DN/JO/TI), `VersionsDialog`, workflow dialogs.
- Accent headers/pills: `SalesReportView` brand + overview headers, picker
  dialogs (JO/PI), shared top-bar panels (Activity/QuickSettings/Screenshot/
  UserSwitch), `HistoryView`, `DnOverviewSection`.
- **Left as-is (not C4):** white on *fixed* semantic colors (`SuccessBrush`/
  `WarningBrush`/`DangerBrush`) — those aren't accent-driven, so white on them
  is a static (if occasionally borderline) design choice, not a theme break.
  A future `OnSuccessForeground`/`OnWarningForeground` token pass could address
  the low-contrast amber case specifically.

### 5.5 — B1: TextFormattingMode Display→Ideal on body-text views ✅
Flipped `TextOptions.TextFormattingMode="Display"` → `"Ideal"` (WPF's default,
correct metrics for body/cell/9–12px text) on the **98** body-text views —
wizards, lists, details, tables, reports, dialogs, settings, sheet store,
centers, AI chat. Kept `Display` on the **17** hero-centric surfaces
(Dashboards ×8, Login ×4, About ×4, Splash ×1) where the display-optimized
metrics suit large headings/KPI figures. One root-level occurrence per file
(no per-element overrides), so the classification is clean.

### 5.6 — R5: fixed row widths — assessed, largely mitigated ✅ (no change)
The audit's "130/150/170px fixed widths clip driver/vehicle/project text" does
not reproduce: those fixed columns (`DeliveryNoteListView` etc.) hold short
values (PROGRESS / QTY / SQM / STATUS), while the long text (job order, customer,
project, driver, vehicle) already sits in star-sized (`1.5*`/`1.4*`) columns with
`TextTrimming="CharacterEllipsis"`. Driver/vehicle fields in `DnOverviewSection`
are stretch TextBoxes, not fixed-width. Leaving the column widths alone to avoid
regressing the 1366px grid budget for zero measured benefit.

### 5.7 — PI wizard audited: no redesign needed ✅ (verified clean)
Full 25-file audit of the ProformaInvoice wizard (shell + Steps 1–9 + Saved
Invoices dialogs) against every audit flag. **Already conforms to the design
system** — the earlier global phases + the wizard's own prior "approved mockup
composition" passes brought it there:
- 0 emoji; 0 non-print hardcoded hex (only 2 black drop-shadows + print-paper white).
- 2 `DropShadowEffect`s total; all 9 step roots share `Ideal`/`ClearType`/`Fixed`/
  `AppFontFamily`/`UseLayoutRounding`/`SnapsToDevicePixels`.
- Section cards / KPI strips / step navigator / footer all route through theme
  tokens and `PrimaryPillButton`/`OutlinedPillButton`/`ModuleSectionCardStyle`.
- Only 2 hardcoded radii (20×20 step-dot circle `10`; `3` on a small list item) and
  a `1` hairline on spec-group chips — all intentional size-proportional exceptions.
No changes made (forcing a redesign would be make-work + regression risk).

### 5.8 — StatementGenerator re-theme: violet brand → accent tokens ✅
The wizard's hardcoded violet/pink/cyan brand was the last visual outlier — a
multi-hue gradient that clashed with the steel-cyan design system and ignored
the accent preset. Re-themed across 5 files (`Filter/FilterStep.xaml`,
`Pipeline/ExecuteStep.xaml`, `Preview/PreviewStep.xaml`, `Review/ReviewStep.xaml`,
`Styles/WizardStyles.xaml`):
- `#8B5CF6` (violet) → `AccentColor` · `#7C3AED` (deep violet) → `AccentDarkColor`
- `#EC4899` (pink) → `AccentLightColor` · `#06B6D4` (cyan) → `AccentLightColor`
- `#0891B2` (deep cyan) → `AccentDarkColor`
- Selected-chip violet tint `#FAF5FF`/`#DDD6FE` → `TintIndigoBrush`/`AccentBrush`;
  `ReviewStep` hover border → `AccentBrush`.
Now every gradient/blob/glow/shadow in the wizard follows the accent preset and
Light/Dark swap, matching the dashboards and the rest of the app.

**Freeze-safety fix (same pass):** removed `po:Freeze="True"` from every brush/
effect that holds a `{DynamicResource}` (a Freezable referencing a deferred
resource cannot actually be frozen — see the crash note below). Applied to the
Statement Generator + `AiChatView` (`ChatBrand`, `BtnShadow`) + `SettingsSectionStyles`
(`ProviderDotAmber`). 0 instances of `po:Freeze`+`DynamicResource` remain repo-wide.
Deferred (not brand): `#34D399` emerald LED and the step-status borders
(`#BBF7D0`/`#FDE68A`/`#FECACA`) — semantic colors, not the violet brand.

### ⚠️ Runtime crash fixed (ERR-673BF4) — DynamicResource in a Storyboard
Phase 4 changed the `ModernToggleSwitch` `ColorAnimation` `To=` values to
`{DynamicResource AccentColor/BorderColor}`. **A Storyboard that references a
DynamicResource (or animates a shared frozen brush) cannot be frozen when its
containing Style is sealed** — it throws `InvalidOperationException: Cannot
freeze this Storyboard timeline tree for use across threads` at `Style.Seal()`
during layout. Reverted to the original literal colors (`#1D4ED8` on / `#CBD5E1`
off) with a guard comment. **Rule for future passes: never put `{DynamicResource}`
(or any deferred reference) inside an animation `To`/`From`/`By` in a sealed
Style/Template Storyboard — use literals (or a StaticResource) instead.**

### 5.9 — DN/TI detail: white-on-accent → OnAccentForegroundBrush ✅
The DN/TI detail views lay white (and white-alpha) text on **theme-driven accent
surfaces** (hero strip, master-DN banner, section headers, spec-group headers —
all `AccentBrush` or `AccentColor→AccentHoverColor` gradients). White was fine on
the default dark-blue accent but broke on light accent presets (Amber) and the
dark theme's light-cyan accent. These were missed by the 5.4 ancestor scan because
the backgrounds are accent *gradients*, not `Background="{...AccentBrush}"`.
- White-alpha → `OnAccentForegroundBrush` + `Opacity` preserving hierarchy:
  `#EAFFFFFF`→0.92 (hero subtitle), `#CCFFFFFF`→0.80 (strip/meta labels,
  project-ref, company, spec label, section subtitles), `#77FFFFFF`→0.47 (divider).
- Pure-white titles/values on accent → `OnAccentForegroundBrush` (hero title,
  banner title, "MASTER DN — READ ONLY" badge, section header titles, count
  badges, spec-group name, "% delivered").
- **Left white on semantic colors** (static, not accent-driven): the "EDITING"
  chips on `SuccessBrush` and list status badges.
Files: `DnSectionStyles`, `DnOverviewSection`, `DnAttachmentsSection`,
`DnHistorySection`, `DeliveryNoteDetailView`, `TiAttachmentsSection`,
`TiFamilySection` (7 files, 29 changes).

### 5.10 — remaining (not started)
SheetStore, Settings — and the "module shells vs. real content" decision for the
9 placeholder modules.
