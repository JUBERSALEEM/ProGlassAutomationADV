# Personal UI — Architecture (glass / Monet / Crystal Fluid / experimental families)

**Phase 12 of the release-safe UI engineering protocol · 2026-09-18**

This document defines how the app's personal/experimental visual layer — the
glass aesthetics, the Monet painting family, Crystal Fluid, and any future
experimental family — is built, so that it can **never** fork the business
views.

## 1. The invariant (non-negotiable)

> Personal UI is a **theme plane**, not a view layer.

A personal family = resource dictionaries (tokens + control styles) + a
runtime family painter + an optional wallpaper scene. Every business view
remains the *same* view, consuming family-managed keys via
**DynamicResource**. There is exactly one XAML per business surface in this
repository (verified: the 320-row inventory has no forked duplicates of any
view). A new personal family adds **zero** view files.

## 2. The planes (who owns what)

| Plane | Keys | Owner / runtime authority | Scope |
|---|---|---|---|
| Semantic base | `Surface*`, `Material*`, `ProGlass*`, `Radius*`, `Motion.*` | `ThemeManager` dual-writes; `DesignTokens`/`SemanticTokens`/`MaterialTokens` | everything |
| **Family plane** (personal UI lives here) | `LiquidGlass*`, `CrystalFluid*`/`Crystal*`, family-managed brush set, wallpaper scenes | `VisualThemeEngine` family painters re-publish at runtime | re-tints every consumer that references family-managed keys |
| Light/Dark | `AppTheme.Light`/`AppTheme.Dark` dictionary swap | `ThemePipeline` step 1 | mode swap only |
| **MERIDIAN industrial plane** | `Pg*` | `ThemeManager` (font/density/radius republish) + `LightTheme`/`DarkTheme` (Pg brush tokens, mode-locked) | MainWindow shell + 7 module dashboards |

Key namespacing (`Pg*`, `CrystalFluid*`, `LiquidGlass*`, `Surface*`,
`ProGlass*`) guarantees zero cross-plane collisions; `App.xaml` merge order
(annotated inline, ~40 dictionaries) is load-bearing for `BasedOn` chains and
is part of the Phase 5–8 audit surface.

## 3. The personal families

### 3.1 Crystal Fluid (iOS water-glass, light-only)
- Owner doc: `Themes/CrystalFluid/CRYSTAL_FLUID_DESIGN.md` (frozen spec,
  user-locked scope decisions).
- Token plane: `CrystalFluidColors → Brushes → Metrics → Tokens` (load order
  per master), then additive control styles / navigation / DataGrid.
- One reusable water implementation (`CrystalWater`), no per-card timers;
  storyboard transforms only; animations paused on `IsVisibleChanged`;
  frozen brushes/geometries. Performance laws are binding.
- Light-only family: `ModePairFor → false` (engine-owned, like
  IndustrialWhite/Grey) — the dark toggle deliberately does not apply.
- Theme Studio: the 19-axis machinery (with undo) is repointed onto Crystal
  Fluid tokens; runtime overrides go through the engine, never hardcoded.

### 3.2 Liquid Glass
- Canonical frosted-glass reference: `Themes/Styles/LiquidGlassDesign.xaml`
  (brushes, shadows, typography, panel/card/button/switch/input) merged early
  so every later dictionary can `BasedOn`/reference it;
  `Themes/Styles/LiquidGlassStyles.xaml` + `Controls/LiquidGlassCard` carry
  the crystal components. Every colour resolves via DynamicResource so Light/
  Dark and the active family re-tint live.

### 3.3 Monet and the remaining families
- Monet, Ocean, Normal, Professional, Industrial ×3 are VisualThemeEngine
  visual families: each publishes its own managed key set (including its own
  `CardCornerRadius` baseline) and a wallpaper scene
  (`Services/Theme/BuiltinWallpaperScenes.cs` → `WallpaperOrchestrator`).
- They were locked as "untouched" by the Crystal Fluid scope decision
  (2026-09-05) — adding a family never edits an existing one.

### 3.4 Pipeline (order is contractual)
`Services/Theme/ThemePipeline.cs` → `Rebuild`/`ApplyAppearance`:
1. `ThemeManager.ApplyTheme` — Light/Dark dictionary swap (the ONLY mode
   swap point),
2. family painter (Crystal Fluid / Monet / …) republishes managed keys,
3. accent re-derivation (mid-pipeline),
4. Theme Studio axes,
5. `WallpaperOrchestrator.RefreshScene()`.

Everything keeps Light/Dark untouched except step 1; consumers see only the
final re-published values through DynamicResource.

## 4. Coexistence with the MERIDIAN industrial plane

- The 7 dashboards + shell consume `Pg*` tokens. Their **colour** brushes
  (`PgSurfaceBrush` … `PgDangerBrush`) are defined in `LightTheme.xaml` /
  `DarkTheme.xaml` — deliberately **mode-locked**: the industrial plane keeps
  a stable palette per mode and does not follow the personal family accent
  (design decision: workflow screens must not re-skin per wallpaper mood).
- Its **typography, density geometry and radii** are runtime-owned
  (`ThemeManager.ApplyDisplayDensity` / `ApplyCardCornerRadius`) and dynamic
  end-to-end (2026-09-18 root-cause fix, `aebf21f`).
- Business modules outside the dashboard suite still ride the family plane:
  switching to Monet or Crystal Fluid re-tints them live, unchanged views.
- The two planes never share keys, so a family repaint can never break the
  industrial plane and vice versa.

## 5. Rules for any new personal family

1. Additive dictionaries only — new key namespace, merge at the family plane
   position in `App.xaml`; never redefine an existing key (W3/W6 gates).
2. Consumers reference family-managed keys **dynamically only** (W16 gate for
   runtime-owned tokens; no hardcoded hex in views — W17).
3. One painter in `VisualThemeEngine` publishing the family's key set;
   wallpaper scene registered in `BuiltinWallpaperScenes`.
4. If light/dark-limited, declare it via `ModePairFor` (engine-owned).
5. No per-element timers; transforms on the composition thread; freeze
   everything that can be frozen.
6. Document scope decisions in a master doc next to the token plane
   (pattern: `CRYSTAL_FLUID_DESIGN.md`), then extend this file's table.

## 6. Verification status

- Structural: 0 parse errors / 0 duplicate keys / 0 undeclared refs across
  all 320 XAML (Phase 17 gate, `XAML_VALIDATION_REPORT.md`).
- Merge order: audited (Phase 5–8), order load-bearing per inline comments.
- Runtime re-tint behaviour: **NOT VERIFIED in this environment** (no .NET
  SDK — BUILD NOT EXECUTED, app not runnable). The pipeline order above is
  read from source, not observed running.
