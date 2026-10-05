# Buttons, type, colour — one language

## Buttons (pills)

Canonical: `Styles/ButtonStyles.xaml` + `PillStyles.xaml`.

| Role | Style |
|---|---|
| Primary action | `PrimaryPillButton` |
| Secondary | `OutlinedPillButton` |
| Success / confirm | `SuccessPillButton` |
| Danger | `DangerButton` / `RowDangerButton` |
| Row | `RowOutlinedButton`, `RowPrimaryButton`, `RowSuccessButton` |
| Toolbar | `ModuleToolbarPrimaryButtonStyle` / Secondary / Success |
| Top bar icon | `TopIconButton` (36×36, `TopPillCornerRadius`) |

Radius = `CardCornerRadius` (Appearance slider). Height = `DensityButtonHeight`. Do not add a fifth radius for Optimization (`OptCorner` should follow `CardCornerRadius` in a later pass).

## Type

`TypeScaleTokens.xaml`: TextXSmall 11 → TextDisplayLarge 28. Body UI uses `{StaticResource TextBody}` etc. Density tokens for titles/KPIs. Font family/weight/style = Appearance. Icons = Segoe MDL2 only.

Optimization still has literal 11/12/25 in `OptHeader` / `OptKpiValue` — **phase B** retokenize.

## Colour

- Accent plate + custom hex (existing).
- Frost plate = same `AccentPresets.All` (new).
- Card edge plate (existing).
- Semantic: Success / Warning / Danger / Info + `Tint*Brush`.
- Text: `TextPrimaryBrush` / Secondary / Tertiary. Font Color override in Appearance.
- Never hard-code White on a card that must follow frost.

## Consistency debt (roadmap, not all this commit)

1. `OptCorner` vs `CardCornerRadius`.
2. Report filter cards vs `ModuleListFilterBarStyle`.
3. God-view grids vs list pages.
4. PI step local styles vs DesignTokens.
5. `LiquidGlassWindow` / leftover Glass* keys — aliases only; no new Liquid runtime.
