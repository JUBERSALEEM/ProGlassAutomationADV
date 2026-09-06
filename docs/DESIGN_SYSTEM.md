# ProGlass Automation — UI Design System (v2)

Goal: one consistent, professional, smooth visual language across every
module — without rewriting working screens.

## The problem this solves
The app grew module-by-module, so each screen invented its own styles:
20+ different corner radii (3…22), 40+ local styles in a single view,
inconsistent title sizes (24/26/28/36). That reads as unprofessional even
when each screen looks fine alone.

## Canonical tokens (single source of truth)

### Radius scale — defined in `Themes/LightTheme.xaml` / `DarkTheme.xaml`
| Token | Default | Use |
|---|---|---|
| `ControlCornerRadius` | **12** (same as card) | inputs, chips, compact buttons |
| `CardCornerRadius` | **12** | cards, panels, **all action buttons** |
| `CardCornerRadiusLarge` | **12** (same as card) | dialogs, heroes |
| `PillCornerRadius` | 999 | status chips only — not buttons |

`CardCornerRadius` is a **runtime setting** (Settings → Appearance →
Card Corner Radius, default 12). ThemeManager writes all three card
tokens to that one value so nothing looks rounder than the slider.

For one-sided corners (table headers) use the matching literal, e.g.
`12,12,0,0` for a `CardCornerRadius` top edge.

### Type scale — `Themes/Styles/TypeScaleTokens.xaml` (canonical)
One coherent font-size scale, merged in `App.xaml` right after the base
theme. Every `FontSize` below 30 DIPs references these tokens —
`{StaticResource Text…}` — never a hard-coded number (2,185 attributes
migrated 2026-08-29).

| Token | Size | Use |
|---|---|---|
| `TextXSmall` | 11 | fine print / legend glyphs |
| `TextCaption` | 12 | column headers / labels / hints / pills |
| `TextBody` | 13 | body / inputs / buttons / table cells |
| `TextBodyLarge` | 14 | list-row primary text / menu items |
| `TextSection` | 15 | card titles / section headers |
| `TextSubhead` | 16 | emphasis sub-headings |
| `TextTitle` | 18 | page titles |
| `TextTitleLarge` | 20 | dashboard / hero titles |
| `TextHeadline` | 22 | big section headlines |
| `TextDisplay` | 24 | large numbers |
| `TextDisplayLarge` | 28 | hero KPI numbers |

Density presets (Compact / Normal / Comfortable) keep driving
`DensityTitleFontSize` / `DensityKpiValueFontSize` /
`DensityBodyFontSize` for density-bound chrome. The shared named styles
(`TypographyStyles.xaml`) continue to exist for headers/numbers above
the body range.

### Color
Never hardcode a brand color in a view. Use theme brushes:
`AccentBrush`, `PrimaryForegroundBrush`, `SecondaryForegroundBrush`,
`MutedForegroundBrush`, `BorderBrush`, `DividerBrush`, `MenuHoverBrush`,
`ModuleBackgroundBrush`, semantic `SuccessBrush / WarningBrush / DangerBrush /
InfoBrush`, and tints `TintGreenBrush / TintAmberBrush / TintRedBrush /
TintBlueBrush / TintIndigoBrush`. These flip automatically between the
Light/Dark themes and the Normal/Monet/Ios-17pm families.

## Shared primitives (`Themes/Styles/DesignTokens.xaml`)
- `ModuleSectionCardStyle` — the one section-card shape.
- `ModuleToolbarPrimaryButtonStyle` / `ModuleToolbarSecondaryButtonStyle`.
- `ModuleTabPillStyle` — in-page tab pills.
- `EmptyStateControl` (`Controls/EmptyState`) — every empty list uses this.
- `StatCardControl` (`Controls/StatCard`) — KPI/stat tiles.

## Rules for touching a module
1. **Replace hardcoded `CornerRadius`** with the radius tokens above.
2. **Replace local header/toolbar styles** with the shared primitives.
3. **Keep module-specific data templates** (row/cell layouts) — only the
   chrome standardizes. Don't restructure working layouts.
4. **Empty states** must use `EmptyStateControl` and explain what belongs
   in the section plus what to do.
5. **Never** introduce a new hardcoded brand color or a new radius value.

## Migration status
- ✅ Tokens + primitives defined and registered (App.xaml).
- ✅ Sheet Store — pilot migrated (title, subtitle, row/table/toolbar radii,
  PDF button themed).
- ✅ Job Order / Delivery Note / Tax Invoice — page titles + subtitles on
  the shared styles (consistent 24/12 scale).
- ✅ Splash + About — version bound to `AppInfo.Version` (no stale strings).
- ✅ Phase 9 header/wizard rewire (this increment):
  - Shared chrome added: `ModuleDetailTitleStyle`,
    `ModuleDetailActionBarStyle`, `ModuleWizardStepIconStyle`,
    `ModuleErrorBannerStyle`, `ModuleSuccessBannerStyle`,
    `ModuleFieldLabelStyle`, `ModuleInlineWarningStyle`.
  - JO/DN/TI detail action bars + error banners consume those primitives.
  - PI list title and wizard steps 1–3 / 5–8 headers use
    `ModulePageTitleStyle` + `ModuleWizardStepIconStyle`.
  - Global TextBox focus ring and Step 4 inputs no longer hardcode indigo
    (`#4338CA` / `#6366F1`) — they follow `AccentBrush`.
- ✅ Views chrome pass (2026-08-18): 70+ view files now bind
  Foreground/Background/BorderBrush/CornerRadius/card 1.5 borders to
  theme tokens. Statement wizard local palette (IndigoPrimary,
  TextPrimary, ChipCheckedBg, …) follows AccentColor / tint Color
  keys so Light/Dark + the Appearance border slider actually reach
  those screens. Printable invoice, Splash/Login brand surfaces, and
  Settings accent swatches left as designed.
- ✅ Document lists (2026-08-18): PI / JO / DN / TI share
  `Themes/Styles/ListPageStyles.xaml` — compact KPI strip, one filter
  row, underline tabs, pale table header, 12 px tokens. Bindings and
  code-behind names unchanged.
- ✅ Line-item grids (2026-08-18): `DataGridStyles.xaml` registered.
  Step 5 consumes it; JO overview drops per-row Rate (production);
  DN keeps qty/prev/this/balance; TI keeps Rate/Value. Arrow keys
  no longer stolen by SmoothScroll.
- 🔲 Remaining: print-surface hex, Splash/Login art, a few decorative
  white-alpha overlays, Production/Optimization ComingSoon modules.

## Why pilot-first
A global find-and-replace across 89 XAML files is how you break a working
app. Pilot → approve → replicate keeps the build green and the style
decisions reviewed once, not per-module.
