# CRYSTAL FLUID — iOS Water-Glass Design System
**Replaces:** `Ultra Transparent` family + `Crystal Sea` wallpaper scene (retired 2026-09-05)
**Scope decisions (user-locked, same day):**
| Decision | Locked answer |
|---|---|
| Legacy removal | Ultra Transparent + Crystal Sea scene ONLY; the other 8 families (Liquid Glass, Ocean, Normal, Monet, Professional, Industrial ×3) remain untouched |
| Visual spec | The implementation prompt's inline spec IS the frozen source of truth (no external HTML file exists) |
| Theme Studio | Keep the 19-axis machinery + undo; axes repointed onto Crystal Fluid tokens (Milestone 3) |
| Dark mode | Toggle remains for remaining families; Crystal Fluid is a **light-only family** (`ModePairFor → false`, engine-owned like IndustrialWhite/Grey) |

---

## 1. Frozen spec (verbatim from the implementation prompt)
**Palette (§8):** ink `#102938` · muted `#68808F` · accent `#55C9EE` · environment `#EDFAFF / #DFF5FF / #F8FDFF` · water = soft cyan/transparent aqua · positive = soft green.
**Glass (§10):** 135° white gradient `rgba(255,255,255,.43)` → `rgba(255,255,255,.14)`; never opaque; background stays perceptible.
**Border (§11):** ~1 px, bright translucent white, inner highlight; no solid/neon/blue outline.
**Shadow (§12):** large blur, low opacity, cyan/blue-gray ink; never black/hard.
**Reflection (§13):** diagonal ~115°, white, strong near one corner → transparent, subtle secondary highlight.
**Radii (§24):** cards 28–32 · top bar 24–26 · buttons 14–16 · items 16–19 · icons 12–16 · sidebar ~32 — all centralized.
**Motion (§42):** hover 150–250 ms · cards 250–400 ms · water level 1200–1800 ms · ambient 15–25 s · caustics 8–15 s · bubbles 6–10 s.
**Water level (§15):** normalized 0..1, animates ~1.5 s cubic ease; ONE reusable implementation.
**Typography (§23):** Segoe UI stack; semibold dark-blue ink; muted blue-gray secondary; large bold KPI values.
**Environment (§22):** pale base, white highlight upper-left, cyan atmosphere upper-right, soft cyan lower-right; ambient motion almost imperceptible.

## 2. Layer stack (§9) — every crystal surface
`CardBase(glass .43→.14 @135°) → Water(body→surface→caustics→bubbles) → Content → Specular(115°) → BorderHighlight(1 px white) → SoftShadow`
Content stays dominant (§63); reflection never reduces readability.

## 3. Resource architecture (§51 adapted to existing repo)
```
Themes/CrystalFluid/CrystalFluidTokens.xaml   ← colors · brushes · metrics · motion · material doubles
Controls/CrystalFluid/CrystalWater.xaml(.cs)  ← the ONE water implementation (Milestone 2)
Services/Theme/VisualThemeEngine.Part3.cs     ← ApplyCrystalFluid (38 managed keys, same key-set as retired painter)
Services/Theme/BuiltinWallpaperScenes.cs      ← "Crystal Fluid" scene = §22 environment
```
App.xaml merges tokens after `DesignTokens.xaml`. Consuming styles use **DynamicResource** (family painters re-publish managed keys at runtime).

## 4. Component map (§58) → existing infrastructure (Milestones 2–3)
| Reference element | WPF landing |
|---|---|
| `.glass` card | `CardStyles` hosts + `CrystalCard` treatment on the shared card style (water-optional) |
| `.kpi` | `Controls/StatCard` — template gains `CrystalWater` layer; new DPs `WaterEnabled/WaterLevel` |
| `water/water::before/::after, .bubble` | `CrystalWater` internal layers (body, surface wave path, caustic band, 2–4 bubble ellipses) |
| `.btn / .pill / progress / table rows` | `ButtonStyles`, `PillStyles`, ProgressBar + DataGrid styles (token revalue, no structural rewrite) |
| `.sidebar / .topbar` | Main-window shell styles (floating crystal panels via family tokens) |
| §43 reduced motion | `CrystalReducedMotion` token consumed by `CrystalWater` + ambient (static water when true) |

## 5. Performance laws (§20/§68/§70) — binding on all milestones
No per-card timers; storyboard transforms only (composition thread); pause animations on `IsVisibleChanged`; frozen brushes; frozen geometries; virtualization untouched; waterfall priority §98: functionality → build → perf → readability → consistency → material → water realism → polish.

---

## 6. SHIPPED STATUS (2026-09-05)
| Milestone | Commit | Content |
|---|---|---|
| M1 — Legacy retirement + foundation | `c7ddb3d` | Ultra Transparent family + Crystal Sea scene retired → `CrystalFluid` family (42 painter keys), tokens file, §22 pale scene, water-forward studio defaults, light-only ModePair, registry/source tests repointed |
| M2 — Water material | `64fd8b6` | `CrystalWater` control (body/surface/caustics/bubbles), family gate pipeline, StatCard = liquid KPI host |
| M3a — Studio repoint + §43 | `0574695` | WaterFlow = water master (opacity/speed/caustics/bubbles), HighlightStrength → reflection, Reduced Motion toggle (persisted + published), leak-proof token lifecycle |
| M3b — Closure | this commit | Settings studio preview is a live water reservoir (WaterFlow slider moves the level, §60); stale value-pin tests repointed to the new family (`Crystal_fluid_*`); full legacy/duplicate/tag↔class sweep green |

## 7. Documented deviations (§90 — limitations chosen, not hidden)
1. **`backdrop-filter`** has no WPF equivalent: reproduced as layered `FrostFill` translucency + elevated-shelf stacking (existing proven mechanism). §21 honored ("appearance, not the CSS property").
2. **±3° 3D card tilt (§31)** deliberately NOT implemented: per-pointer-plane transforms on an ERP's card grids + screens without GPU headroom is exactly the "expensive architecture" §20 forbids. The premium feel is carried by specular reflection + live water. Reversible decision — one attached behavior away if ever approved.
3. **Sidebar/topbar chrome**: material fully revalued to floating crystal (§25/§28 look), but their STRUCTURE (margins/layout) stays shared across the surviving 8 families — the prompt allowed adapting architecture instead of restructuring (§51).
4. **§46 semantic water**: the `WaterLevel` DP is the ready hook; bound today only on the studio preview (WaterFlow itself). Binding real ERP ratios (delivery %, stock %) belongs to module owners — the door is open, no business math was invented (§46's own rule).
5. **Dark variant of Crystal Fluid**: not built per locked decision (light-only family; toggle remains for other families).
6. **`Crystal` / `LightGlass` / `Balanced` studio presets** keep working unchanged — they are studio profiles across the 19 axes, not legacy families; the axis repoint gives them crystal meaning automatically.
