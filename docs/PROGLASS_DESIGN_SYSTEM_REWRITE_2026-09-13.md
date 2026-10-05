# ProGlass Design System Rewrite — Architecture Report

**Date:** 2026-09-13  
**Scope:** Theme / design-system architecture only. No PI/JO/DN/TI, schema, costing, licensing, or auth changes.

---

## A. Architecture

### Old (fragmented)

App.xaml merged 35+ dictionaries: Light/Dark, LiquidGlassDesign, DesignTokens, CrystalFluid (9 files), ButtonStyles, PiIndustrial, DashboardKit, plus alias layers (`SemanticTokens`, `ComponentTokens`, `MaterialTokens`). Visual families were painted by `VisualThemeEngine` onto **legacy** keys (`GlassUltraClearBrush`, `ModuleBackgroundBrush`, `Liquid*`).

Three generations competed: Liquid Glass (2026-08-29), DesignTokens v2, Crystal Fluid (Ios-17pm).

### New (authoritative)

```
Themes/ProGlassDesignSystem/
  ProGlassDesignSystem.xaml          ← single DS entry
  Tokens/
    SemanticColors.xaml
    SemanticTypography.xaml
    SemanticGeometry.xaml
    SemanticSpacing.xaml
    SemanticElevation.xaml
    SemanticMotion.xaml
    SemanticDensity.xaml
  Materials/
    ProGlassMaterials.xaml           ← Solid / Soft / Frosted / Crystal
  Compatibility/
    LegacyAliases.xaml               ← MIGRATION ONLY
```

Runtime:

1. Light/Dark dictionary swap (`ThemeManager.ApplyTheme`)
2. Family painter (`VisualThemeEngine` — Industrial / Monet / Crystal Fluid)
3. **`ThemeManager.PublishProGlassDesignSystem()`** copies live engine brushes into semantic keys (`SurfacePrimaryBrush`, `TextTertiaryBrush`, `InputBorderBrush`, `TabActiveBrush`, report brushes, …)
4. Density (`Compact` / `Normal` / `Comfortable` / **`Spacious`**) writes `Density.*`, `TypographyBodySize`, `CardPadding`

Theme family, appearance mode, material, and density remain **independent settings**.

### Deleted dictionaries

**None.** CrystalFluid / LiquidGlass / module chrome are still loaded because thousands of views still bind those keys. Removal is Phase J after a zero-reference sweep on Windows.

### Compatibility resources

| New (authoritative) | Still filled from (engine) |
|---|---|
| `SurfacePrimaryBrush` | `GlassUltraClearBrush` |
| `SurfaceSecondaryBrush` | `ElevatedSurfaceBrush` |
| `SurfaceInputBrush` | `ModuleBackgroundBrush` |
| `TextTertiaryBrush` | `MutedForegroundBrush` |
| `BorderDefaultBrush` | `OutlineDefaultBrush` |
| `InputFocusBorderBrush` | `AccentBrush` |
| `TabActiveBrush` | `AccentBrush` |
| `Report*` | `Print*` / text / outline |

Old names remain valid (migration bridge). New screens must use semantic names.

### Component hierarchy (canonical)

`ProGlassMaterial.{Solid,Soft,Frosted,Crystal}` → `ProGlassCard` / `ProGlassKpiCard`  
`ProGlassButton.*` / `ProGlassInput.*` / `ProGlassPill.*` / `ProGlassTabs.*` / `ProGlassToggle` / `ProGlassDialog.*` (ComponentTokens)

PI station (`PiStationRail`, `PiWorkDeck`, `PiProductViewport`) stays a **module layout composition**, not a theme.

### Runtime switching

Unchanged public API: `ThemeManager.ApplyTheme`, `ApplyAccent`, `ApplyDisplayDensity`, `VisualThemeEngine.Apply`. Post-processing now always republishes semantic keys.

---

## B. Resource inventory (authoritative)

- `Themes/ProGlassDesignSystem/ProGlassDesignSystem.xaml`
- `Tokens/SemanticColors.xaml`
- `Tokens/SemanticTypography.xaml`
- `Tokens/SemanticGeometry.xaml`
- `Tokens/SemanticSpacing.xaml`
- `Tokens/SemanticElevation.xaml`
- `Tokens/SemanticMotion.xaml`
- `Tokens/SemanticDensity.xaml`
- `Materials/ProGlassMaterials.xaml`
- `Compatibility/LegacyAliases.xaml`
- Existing (still required): `Themes/LightTheme.xaml`, `DarkTheme.xaml`, `Themes/Styles/*`, `Themes/CrystalFluid/*`

---

## C. Migration report

| Area | Status |
|---|---|
| Token layer + material primitive | **Migrated** |
| Theme engine dual-write | **Migrated** |
| Density including Spacious | **Migrated** |
| Print-safe semantic brushes | **Migrated** (PrintTokens + Report*) |
| Component aliases (buttons/KPI/tabs/inputs) | **Migrated (aliases)** — templates still live in v3 dictionaries |
| Views still using Glass*/Liquid*/Crystal* | **Compatibility** — do not delete |
| Design System Gallery screen | **Needs manual review** — not DI-registered (startup risk, no local .NET toolchain) |
| CrystalFluid / LiquidGlass dictionaries | **Unused for new work; not removed** |
| Per-view StaticResource audit | **Needs manual review** |

---

## D. Regression

| Check | Result |
|---|---|
| Build | Not executed in this sandbox (no .NET Windows toolchain) |
| Theme switching | Engine path unchanged + semantic republish |
| Light/Dark | Same dictionaries; semantic copy after swap |
| Controls | No template replacement of implicit TextBox/DataGrid |
| DataGrid virtualization | Untouched |
| Navigation / dialogs | Untouched |
| Settings density | Options should include Spacious |
| Performance | No per-row effects added; water remains Crystal environment |

---

## E. Remaining issues (do not hide)

1. **Views are not bulk-rewritten.** Doing so in one pass would break production XAML. Compatibility dual-write is the safe bridge.
2. **CrystalFluid and LiquidGlass cannot be deleted yet** — they still own templates and environment (water/caustics).
3. **No Design System Gallery** was registered in DI/nav (would require constructor tests this environment cannot run).
4. **True backdrop blur** is still not available in native WPF; Crystal frost remains the existing engine approximation, isolated in CrystalFluid + material Crystal level.
5. **Grid/List/Queue layout modes** remain DataTemplate concerns on module screens; density is global, layout is not confused with theme.
6. Duplicate KPI/tab/button **templates** still exist; they are aliased, not deleted, until zero-reference.
)
