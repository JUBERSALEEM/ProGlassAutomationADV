# ProGlass Design System — Architecture & Migration Roadmap

**Date:** 2026-09-13 · **Directive:** Universal Design System & UI Architecture
master prompt. One canonical system; Industrial, Monet and Crystal are
interchangeable theme/material configurations — not separate architectures.

This document is the architecture companion to the Phase A audit
(`DESIGN_SYSTEM_UNIFICATION_AUDIT_2026-09-13.md`). The audit is the
**inventory**; this document is the **map and the plan**.

---

## 1. Target architecture, mapped to the codebase

The master prompt prescribes a stack. Each layer already exists (or was added
this phase) and is pinned to real files:

| # | Layer | Canonical vocabulary | Lives in |
|---|---|---|---|
| 0 | Foundation — Colors | `Color.*` | `Themes/Styles/ConverterResources.xaml`, `Themes/LightTheme.xaml`, `Themes/DarkTheme.xaml` |
| 0 | Foundation — Typography | `TextXSmall … TextDisplayLarge` | `Themes/Styles/TypeScaleTokens.xaml` |
| 0 | Foundation — Geometry | `Radius.*`, `Border.*`, `Surface.*` | `Themes/Styles/DesignTokens.xaml` + `ThemeManager` dynamic primitives |
| 0 | Foundation — Motion | `Motion.Instant … Motion.Emphasis` | `Themes/Styles/SemanticTokens.xaml` (new) |
| 1 | **Semantic tokens** | `SurfaceWindowBrush`, `SurfaceWorkspaceBrush`, `TextMutedBrush`, `BorderSubtleBrush`, `FocusBrush`, `RadiusCard`, … | `Themes/Styles/SemanticTokens.xaml` (new) |
| 2 | **Themes (families)** | Industrial · Monet · Crystal (`CrystalFluid` / iOS 26) | `Services/Theme/VisualThemeEngine.Part1/3.cs`, `Themes/CrystalFluid/*` |
| 2 | **Modes** | Light / Dark | `Themes/LightTheme.xaml`, `Themes/DarkTheme.xaml` |
| 3 | **Material engine** | `ProGlassSurface.Solid/Elevated/Frosted/Glass/CrystalLiquid` | `Themes/Styles/MaterialTokens.xaml` (new) over `Surface*` + `LiquidGlass*` |
| 4 | **Components** | `ProGlassButton.*`, `ProGlassPill.*`, `ProGlassTabs.*`, `ProGlassInput.*` | `Themes/Styles/ComponentTokens.xaml` (new) over v3 dictionaries |
| 5 | **Layout + density** | `ProGlassLayout.*` + Workspace/Station/Rail/Deck/Grid/List/Queue compositions | `Themes/Styles/LayoutTokens.xaml` (new) + `ShellStyles`, `PiIndustrialStyles`, `ListPageStyles`, `DataGridStyles` |
| 6 | Effects | Frost/blur/water/caustics — Crystal-only, gated | `Themes/CrystalFluid/*` (environment surfaces only) |
| 7 | Module adapters | PI / JO / DN / TI business chrome | `SharedSectionStyles.xaml`, `PiIndustrialStyles.xaml`, module dictionaries |
| 8 | Print renderer | Paper-safe neutral surface, no glass/blur/water | `Services/Export/`, `Services/ProformaInvoice/Export/`, `Services/Reporting/` |

**Rule of resolution:** XAML consumes the *highest* layer that still expresses
the intent. Prefer `SurfaceWorkspaceBrush` over `ModuleBackgroundBrush` over a
raw `#AABBCC`. The legacy/material names remain published by the engine and
remain valid; they are simply no longer the *preferred* name for new code.

## 2. Dependency map (who consumes what)

- **Views** (9295 StaticResource + 11103 DynamicResource references in `Views/`)
  → the 35+ merged dictionaries of `App.xaml`, in the documented merge order.
- **Theme engine** (`VisualThemeEngine`, `ThemeManager`) publishes the concrete
  brush/metric keys (`WindowBackgroundBrush`, `ModuleBackgroundBrush`,
  `Density*`, `Radius.*`, `Border.*`, …) at runtime; the token layers re-expose
  them semantically.
- **Component tokens** → `ButtonStyles` (v3), `PillStyles`, `ModuleTabStyles`,
  `InputStyles` (all merged before `ComponentTokens.xaml`).
- **Material tokens** → `DesignTokens` surface styles + `LiquidGlassDesign` /
  `LiquidGlassStyles` surfaces (all merged before `MaterialTokens.xaml`).
- **Layout tokens** → runtime `Density*` keys (fallbacks in `LightTheme` /
  `DarkTheme`; live values from `ThemeManager.ApplyDisplayDensity`).

### Compatibility policy (non-destructive)
1. Legacy brush names (`GlassUltraClearBrush`, `ModuleBackgroundBrush`,
   `LiquidGlass*`, `Crystal*`, `Dash*`, `Db*`, …) are **kept published**.
   They are not deleted, not renamed, not re-templated.
2. New/refactored XAML adopts the canonical names.
3. A legacy name is removed **only** after a verified zero-reference sweep and
   a passing Windows build — never up front.

## 3. Canonical naming tables (shipped layers)

### Semantic surfaces/text/border/state — `SemanticTokens.xaml`
| Canonical | Resolves to (legacy/material) |
|---|---|
| `SurfaceWindowBrush` | `WindowBackgroundBrush` |
| `SurfaceWorkspaceBrush` | `ModuleBackgroundBrush` |
| `SurfaceCardBrush` | `GlassUltraClearBrush` |
| `SurfaceElevatedBrush` | `ElevatedSurfaceBrush` |
| `SurfaceInputBrush` | `ModuleBackgroundBrush` |
| `SurfacePopupBrush` | `PopupSurfaceBrush` |
| `SurfaceSidebarBrush` | `SideMenuBackgroundBrush` |
| `TextMutedBrush` | `MutedForegroundBrush` |
| `BorderSubtleBrush` | `OutlineDefaultBrush` |
| `BorderStrongBrush` | `OutlineStrongBrush` |
| `FocusBrush` | `LiquidFocusBrush` |
| `AccentPressedBrush` | `AccentDarkBrush` |
| `RadiusCompact / Control / Card / Large / Dialog` | `Radius.Small` / `ControlCornerRadius` / `CardCornerRadius` / `CardCornerRadiusLarge` |
| `RadiusPill` | literal `999` capsule |
| `Motion.Instant / Fast / Standard / Slow / Emphasis` | literal durations |

### Material levels — `MaterialTokens.xaml`
| Canonical | Base |
|---|---|
| `ProGlassSurface.Solid` | `SurfacePrimaryStyle` |
| `ProGlassSurface.Elevated` | `SurfaceSecondaryStyle` |
| `ProGlassSurface.Detail` | `SurfaceDetailStyle` |
| `ProGlassSurface.Frosted` | `LiquidGlassSubtleCardStyle` |
| `ProGlassSurface.Glass` | `LiquidGlassCard` |
| `ProGlassSurface.CrystalLiquid` | `LiquidGlassStrongCardStyle` |
| `ProGlassSurface.Panel` | `LiquidGlassPanel` |

### Component variants — `ComponentTokens.xaml`
- `ProGlassButton.{Base,Primary,Secondary,Success,Danger,Tab}` +
  `Row{Primary,Secondary,Success,Danger}` + `Report/Export{Primary,Secondary}`
- `ProGlassPill.Status.{Accent,Success,Warning,Danger,Info,Neutral}`,
  `ProGlassPill.Priority.{High,Medium,Low}`, `ProGlassPill.{Tag,Outline}`
- `ProGlassTabs.{Strip,Button,Pill}`
- `ProGlassInput.{TextBox,PasswordBox,ComboBox,EditableComboBox,Int,Decimal}`

### Layout/density — `LayoutTokens.xaml`
`ProGlassLayout.{RowHeight,InputHeight,ButtonHeight,ButtonMinWidth,
TitleFontSize,KpiFontSize,BodyFontSize,CardPadding,SectionPadding,PageMargin}`
→ runtime `Density*` keys (Compact · Normal · Comfortable).

## 4. Layout composition map (Phase F — already present, kept as-is)

| Composition | Canonical owner | Notes |
|---|---|---|
| Workspace shell / sidebar | `ShellStyles.xaml` | stays the shell |
| PI Station Rail / Header Bar | `PiStationRailStyle`, `PiStationHeaderBarStyle`, `PiRailBarStyle` | **kept** — industrial station concept |
| PI Work Deck | `PiWorkDeckStyle` | kept |
| PI Zone plate / header | `PiZonePlate*` (module view) | kept |
| PI Product Viewport | `PiProductViewportStyle` | kept |
| PI Command Footer | `PiCommandFooter` (module view) | kept |
| PI Layer Strip / Cost Tile | `PiLayerStrip`, `PiCostTile` | kept |
| List (PI/JO/DN/TI) | `ModuleListShellStyle`, `ModuleListRowStyle`, `PiListRowStyle` | kept + density tokens |
| DataGrid (line items) | `DataGridStyles.xaml`, `ErpDataGrid*`, `LiquidGlassDataGrid*` | clarity-first; no whole-grid glass |
| Module tabs | `ModuleTabStyles.xaml` | kept |

**Decision:** these are *compositions*, not tokens. They are not flattened into
the token layers — flattening would break the PI industrial language the
directive explicitly asks to preserve.

## 5. Effects policy (Level 6)

- Frost / blur / specular / water / bubbles / caustics are **Crystal-family
  environment surfaces only** (`Themes/CrystalFluid/*`), gated by the crystal
  water/ambient toggles.
- Industrial and Monet do **not** inherit water theatre — the `Frosted` /
  `Glass` / `CrystalLiquid` material levels re-tint through the family-driven
  brushes rather than forcing crystal effects.
- **Never** place an effect on a DataGrid row / cell / list item (virtualization
  + recycling cost); readability wins over decoration.
- Print resolves to paper-safe neutral surfaces (see §7).

## 6. Accessibility & keyboard (invariant)

- Every interactive canonical control keeps Hover / Pressed / Focused /
  Selected / Disabled states; focus is a visible ring (`FocusBrush`), not
  `FocusVisualStyle="{x:Null}"`.
- No new screen may remove keyboard navigation; list/DataGrid selection and
  tab order remain standard WPF controls.
- Reduced motion: Crystal family honours `CrystalReducedMotion`; `Motion.*`
  durations are consumed by storyboards, which are skipped under reduced motion.

## 7. Print renderer (Level 8)

- Print output uses paper-safe neutral surfaces — no glass, no blur, no water,
  no reflections, no excessive shadows.
- Screen may be `CrystalGlass`; the print surface stays neutral. The existing
  export/report pipelines (`Services/Export`, `ProformaInvoice/Export`,
  `Reporting`) are the print renderer and remain untouched.

## 8. Shipped vs next (B–J)

| Phase | Deliverable | Status |
|---|---|---|
| A | Audit + inventory | ✅ `DESIGN_SYSTEM_UNIFICATION_AUDIT_2026-09-13.md` |
| B | Foundation (typography/geometry/motion) | ✅ existing + `Motion.*` |
| C | Semantic layer | ✅ `SemanticTokens.xaml` |
| D | Material engine | ✅ `MaterialTokens.xaml` |
| E | Components | ✅ `ComponentTokens.xaml` |
| F | Layout + density | ✅ `LayoutTokens.xaml` |
| G | Reference screen | ⏭ planned — see §9 |
| H | Module migration (PI → JO → DN → TI) | ⏭ gradual, alias-first |
| I | Remaining modules | ⏭ |
| J | Legacy cleanup | ⏭ only after verified zero-reference + Windows build |

## 9. Next phase (G) — reference screen plan

A reference screen is a **navigable, DI-registered** module (VM + View +
`MainWindow.xaml` DataTemplate + `ServiceRegistration` + shell nav entry), and
the DI composition is test-verified
(`Tests/Agent/ConstructorInitializationTests.cs`,
`Tests/Agent/ServiceRegistrationCompositionTests.cs`). Because this workspace
has **no .NET toolchain** (no local build/test execution), a reference screen
is deferred until it can be built and its wiring verified — shipping
unverifiable DI wiring would risk breaking startup.

The reference screen will:
1. Present every canonical component (`ProGlassButton.*`, `ProGlassPill.*`,
   `ProGlassTabs.*`, `ProGlassInput.*`, `ProGlassSurface.*`, `ProGlassLayout.*`)
   in one scrollable `Workspace/Station` composition.
2. Demonstrate all three families × Light/Dark via the existing live switcher.
3. Serve as the visual regression harness for the compatibility/alias layer.

Module migration (H–I) proceeds **alias-first**: swap legacy keys for canonical
names file-by-file, verify each with the zero-reference sweep + a Windows
build, and only then consider removing a legacy key (J).

---

## 10. Hard rules (no-regression guards)

1. No DB schema, business logic, or PI/JO/DN/TI workflow change.
2. No virtualization removal; no per-row effects.
3. No `FocusVisualStyle="{x:Null}"` without a replacement.
4. No parallel design system (`CrystalDesignSystem`, `MonetDesignSystem`, …).
5. No blind `StaticResource`→`DynamicResource` conversion — DynamicResource
   only where runtime theme/appearance re-targeting is required.
6. No deletion/rewrite of an existing style "because it looks old" — dedupe
   only after equivalent behaviour is preserved.
7. No hundreds of speculative tokens — each token above aliases an existing
   implementation; nothing is invented for its own sake.
