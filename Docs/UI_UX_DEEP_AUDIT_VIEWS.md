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
