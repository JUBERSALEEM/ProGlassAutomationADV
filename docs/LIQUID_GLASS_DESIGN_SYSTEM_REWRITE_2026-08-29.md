# Liquid Glass Design System — Global Theme Rewrite (2026-08-29)

Per the master prompt: audit → document → rebuild from the foundation,
preserving all functionality and the existing dynamic-theme architecture.

## 1. Files analyzed

| File | Role | Verdict |
|---|---|---|
| `App.xaml` | Merge order: theme → type scale → shared styles → v2 design system → industrial styles → wizard | Merge order correct; one new dictionary added |
| `Themes/LightTheme.xaml`, `Themes/DarkTheme.xaml` | Per-mode surface/text/accent brushes (swapped at runtime by `ThemeManager.ApplyTheme`) | Correct architecture — extended with the 14 liquid-glass tokens |
| `Themes/Styles/*` (TypeScaleTokens, TypographyStyles, IconBadgeStyles, PillStyles, CardStyles, DesignTokens, ListPageStyles, DataGridStyles, PiIndustrialStyles, ModuleTabStyles, InputStyles, DialogStyles) | Shared control chrome | Kept; new `LiquidGlassStyles.xaml` added alongside |
| `Styles/ButtonStyles.xaml`, `Styles/ContextMenuStyles.xaml`, `Styles/DashboardStyles.xaml`, `Styles/Step4SectionStyles.xaml` | Button/pill/context/dashboard chrome | Kept (existing button hierarchy preserved) |
| `Services/ThemeManager.cs` | Light/Dark swap + accent engine + outline/density | Preserved; swap mechanism reused |
| `Services/Theme/VisualThemeEngine.cs` | Six visual families (Normal/Monet/Professional/Glass/Glassy/Ocean) overriding app-level keys | Glass/Glassy rebuilt on the new token system |
| `Services/Theme/GlassChromeService.cs` | DWM acrylic window backdrop | Preserved |
| `Controls/LiquidGlassCard.cs` | NEW layered crystal card control | Added |
| All `Views/**` (76 local `<UserControl.Resources>` blocks, 177 hardcoded color literals) | Page chrome | Classified; curated migration applied |
| `Tools/LicenseGenerator/*` | Separate app, separate resource scope | Untouched (own Theme.xaml) |

## 2. Broken theme issues discovered

1. **Glassy used the DARK frost ramp for BOTH modes** — light cards were
   invisible (fixed in the earlier passes; the ramp is now per-mode).
2. **Light `GlassCard` was fully opaque white** — solid slabs, not glass.
3. **Light `GlassSheen` (`#33FFFFFF`) invisible on white** — no specular.
4. **Light stage orbs ≈20% alpha** — aurora invisible on the pale stage.
5. **Light glass text read surface-named keys** — recoloring surfaces
   would white-out text (decoupled to fixed ink constants).
6. **Old glassmorphism style**: the old material was a flat translucent
   fill (`Background="#33FFFFFF"` style) — exactly the "ugly traditional
   glassmorphism" the prompt rejects. No crystal edges, no internal
   reflection, no layered construction.
7. **Hardcoded glass hexes scattered in views** (login chrome
   `#1CFFFFFF`/`#66BFDBFE`, delivery-note `#22FFFFFF`/`#33FFFFFF`,
   AI chat chips `#33FFFFFF`) — theme-dependent values outside the theme.
8. **No shared crystal-surface primitive** — Borders cannot host
   ControlTemplates, so every glass surface had to be rebuilt by hand.
9. **No switch/input/grid/nav/dialog glass vocabulary** — the glass
   families recolored surfaces but controls stayed in the old ERP idiom.

## 3. Hardcoded values migrated (curated, classified)

Classified all 177 XAML color literals and 153 C# brush/color uses:
- **Migrated to dynamic tokens** (theme-dependent glass): login
  text/password/checkbox chrome (3 blocks, `AuthGlassStyles.xaml`),
  delivery-note button chrome + banner progress track/fill
  (`DeliveryNoteDetailView.xaml` — fill now `AccentBrush`, readable in
  both modes), AI chat chip (`AiChatView.xaml`).
- **Left intentionally**: white decorative text on accent fills
  (semantic on-accent), dashboard decorative `#CCFFFFFF`/`#DDFFFFFF`
  glows (accent decor), code-built brush math inside the theme engine
  itself (that is the theme's job).

## 4. New shared resources created

14 per-mode design tokens (defined with intentional Light and Dark
values in both theme files, frozen):

| Token | Meaning |
|---|---|
| `LiquidGlassBrush` / `LiquidGlassSubtleBrush` / `LiquidGlassStrongBrush` | Nearly-transparent centers, three densities |
| `GlassEdgeBrush` | Crystal edge gradient (bright top, soft sides, deeper lower edge) |
| `GlassInnerHighlightBrush` | Internal top reflection line |
| `GlassReflectionBrush` | Diagonal specular sweep |
| `GlassControlHoverBrush` / `GlassControlPressedBrush` / `GlassControlDisabledBrush` | Interaction states |
| `GlassFocusBrush` | Focus ring (accent crystal) |
| `GlassSelectionBrush` | Selection glass (accent-tinted, still transparent) |
| `GlassPrimaryButtonBrush` | Primary glass fill (accent crystal with top-lit fade) |
| `GlassOverlayBrush` | Subtle dialog veil |
| `GlassShadowColor` | Soft shadow tint |

Plus the new `Controls/LiquidGlassCard` control (CornerRadius +
GlassIntensity DPs) and `Themes/Styles/LiquidGlassStyles.xaml`.

## 5. Global styles rewritten / created

- `LiquidGlassCard` implicit style — layered surface: crystal edge
  border → translucent center → internal top reflection → diagonal
  sweep → soft shadow; `GlassIntensity` picks Subtle/Standard/Strong.
- `LiquidGlassPanelStyle`, `LiquidGlassSubtleCardStyle`,
  `LiquidGlassStrongCardStyle` — Border adoption path for existing markup.
- `LiquidGlassPrimaryButtonStyle`, `LiquidGlassSecondaryButtonStyle`,
  `LiquidGlassGhostButtonStyle`, `LiquidGlassIconButtonStyle`,
  `LiquidGlassDangerButtonStyle` — one crystal language, five roles;
  hover lift + press compression (CubicEase, 80–180 ms).
- `LiquidGlassSwitchStyle` — iPhone-style liquid switch: water-glass
  track, floating glass thumb, accent-tinted glass when ON, spring
  travel (BackEase EaseOut, Amplitude 0.35 — no excessive bounce).
- `LiquidGlassTextBoxStyle`, `LiquidGlassPasswordBoxStyle`,
  `LiquidGlassComboBoxStyle`, `LiquidGlassEditableComboBoxStyle`,
  `LiquidGlassDatePickerStyle` — clear glass defaults, refined focus,
  hover reflection, semantic danger ring on validation error, glass
  popup dropdowns.
- `LiquidGlassDataGridStyle` + column-header/row/cell styles — liquid
  glass container, stronger glass header, subtle alternating rows,
  accent-glass selected row; zero per-row animation.
- `LiquidGlassNavItemStyle` — floating glass capsule (RadioButton).
- `LiquidGlassDialogCardStyle` / `LiquidGlassDialogOverlayStyle` —
  stronger glass + elevation + subtle veil.

The VisualThemeEngine Glass/Glassy families now publish all 14 tokens
(`ApplyLiquidGlassTokens`) accent-tinted per mode, so one family
switch re-tints the entire new system live; ManagedKeys cleanup
restores theme defaults on every other family.

## 6. Functionality preserved

No business logic, VM, command, binding, converter, validation,
DataTemplate, or navigation touched. The six-family engine API, the
Light/Dark swap (`ThemeManager.ApplyTheme`), accent engine, outline /
density / radius preferences, wallpaper layering, DWM chrome, and all
existing style keys remain byte-compatible in name. Only additive
keys, additive dictionary, additive control, and re-tinted engine
values for the Glass/Glassy families.

## 7. Dynamic Light/Dark switching

Every new token is defined in BOTH theme files (identical key sets —
verified by source test) and consumed exclusively via DynamicResource
(no StaticResource on theme colors in the new dictionary — pinned by
test). ThemeManager swaps the theme dictionary; the engine re-derives
family tokens on `AccentChanged`; controls re-resolve live. The
LiquidGlassCard rebuilds on `ThemeChanged` in code-built hosts where
applicable.

## 8. Duplicate resource keys

Automated scan across every merged dictionary: zero duplicates within
files, and the 14 new keys are unique project-wide.

## 9. XAML/WPF validity

All XAML files parse. New templates use only valid WPF: no Template on
Border, no CSS properties, no unsupported easing syntax, frozen
brushes (`po:Freeze`), correct property types (CornerRadius, Color,
Brush), standard template part names (`PART_ContentHost`,
`PART_EditableTextBox`). C# lexer-clean. Source-regression tests pin
the architecture.

## 10. Performance

No continuous animations, no per-row effects, no BitmapEffects.
Animations only on hover/press/check/focus (80–220 ms). One
DropShadowEffect per card/dialog template (bounded, not hundreds).
All engine brushes frozen. DataGrid rows use trigger Setters only —
large datasets render unchanged.
