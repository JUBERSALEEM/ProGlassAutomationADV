# ProGlass Theme Engine — Complete Rewrite Phases

**Status:** PLAN ONLY. No formulas, costing, VAT, PI/JO/DN/TI bindings, EF schemas, or workflow code in scope.  
**Goal:** One professional `ProGlassThemeEngine` owning Light/Dark × Industrial/Monet/Crystal. Current LightTheme/DarkTheme/CrystalFluid/LiquidGlass/VisualThemeEngine **retired after** each phase’s exit criteria.  
**Invariant:** Screens keep working the entire time. Bindings to *data* never change. Only resource *keys* and the engine that publishes them change.

---

## Hard rules (every phase)

1. **Do not touch** ViewModels’ business properties, converters that compute money/qty, EF models, PDF number layouts, licensing, auth.
2. **Do not change** `{Binding …}` paths. Only `{DynamicResource}` / `{StaticResource}` *visual* keys may change, and only via a compatibility map until Phase 12.
3. **Do not remove virtualization**, `AutoGenerateColumns=False`, or DataGrid column bindings.
4. **Do not add** a fourth theme family. Exactly: Industrial, Monet, Crystal × Light, Dark.
5. **Do not ship** a phase that cannot round-trip: Light↔Dark, family switch, density switch, accent change — without restart.
6. **Delete only** after `rg` shows zero XAML/C# references **and** a Windows build + smoke of PI list + Dashboard + Settings.
7. Crystal water/caustics remain **environment**, never per-row / per-card.

---

## Target engine (replace VisualThemeEngine + ThemeManager theme parts)

```
ProGlassThemeEngine
├── TokenStore          // one ResourceDictionary of semantic keys
├── Palette            // Industrial | Monet | Crystal  ×  Light | Dark
├── MaterialResolver   // Solid | Soft | Frosted | Crystal  (independent)
├── DensityResolver    // Compact | Normal | Comfortable | Spacious
├── AccentResolver     // one hue → Accent/Hover/Pressed/Soft/OnAccent
├── OutlineResolver    // defined-edge toggle (already exists)
├── PrintResolver      // Report* / paper-safe (never glass)
└── CompatibilityHub   // OLD key → TokenStore  (deleted in Phase 12)
```

**Public API (replace current scatter):**

```text
ThemeEngine.SetFamily(Industrial|Monet|Crystal)
ThemeEngine.SetMode(Light|Dark)
ThemeEngine.SetMaterial(Solid|Soft|Frosted|Crystal)
ThemeEngine.SetDensity(Compact|Normal|Comfortable|Spacious)
ThemeEngine.SetAccent(Color?)
ThemeEngine.SetReducedMotion(bool)
ThemeEngine.Publish()          // writes TokenStore into Application.Resources
```

`ThemePipeline.Rebuild` becomes a thin wrapper around `Publish()`.  
`AppearanceSettings.VisualTheme` stores `Industrial|Monet|Crystal` only (migrate `"Normal"`, `"Normal (Industrial)"`, `"Crystal Fluid"`).

**TokenStore keys (only these are legal in new XAML):**

- Surfaces: `WindowBackgroundBrush`, `SurfacePrimaryBrush`, `SurfaceSecondaryBrush`, `SurfaceTertiaryBrush`, `SurfaceElevatedBrush`, `SurfaceInputBrush`, `SurfaceOverlayBrush`, `SurfacePopupBrush`
- Text: `TextPrimaryBrush`, `TextSecondaryBrush`, `TextTertiaryBrush`, `TextDisabledBrush`, `TextPlaceholderBrush`, `OnAccentForegroundBrush`
- Border: `BorderSubtleBrush`, `BorderDefaultBrush`, `BorderStrongBrush`, `BorderFocusBrush`, `DividerBrush`
- Accent/status: `AccentBrush`, `AccentHoverBrush`, `AccentPressedBrush`, `AccentSoftBrush`, `SuccessBrush`, `WarningBrush`, `DangerBrush`, `InfoBrush` + `*SoftBrush`
- Input: `InputBorderBrush`, `InputFocusBorderBrush`, `InputPlaceholderBrush`, `InputValidationBrush`
- Data: `SelectionBrush`, `SelectionForegroundBrush`
- Type/geometry/spacing/elevation/motion/density as already defined under `Themes/ProGlassDesignSystem/Tokens/`
- Print: `ReportPrimaryBrush` … `ReportAccentBrush`

Everything else is **compatibility or dead**.

---

## Phase 0 — Freeze contract (0.5 day)

**Do:** Inventory every key `VisualThemeEngine.ManagedKeys` + Light/Dark `x:Key`. Classify: Token / Compatibility / Delete-candidate. Freeze TokenStore names.  
**Do not:** Edit views.  
**Exit:** Spreadsheet/markdown of ~N keys with owner. This document’s TokenStore list signed off.

---

## Phase 1 — TokenStore is the only write target (1–2 days)

**Do:** Implement `ProGlassThemeEngine.Publish()` that writes **only** TokenStore keys (copy from current engine output). Call it at the end of today’s `ApplyTheme` / `ApplyAccent` / `ApplySemanticSurfaces` / `ApplyDisplayDensity` (already started as `PublishProGlassDesignSystem`). Expand until **every** TokenStore key is assigned every Publish.  
**Do not:** Remove LightTheme.xaml.  
**Exit:** Debugger/watch: after family switch, `Application.Current.Resources["SurfacePrimaryBrush"]` changes; `TextPrimaryBrush` contrast OK Light+Dark.

---

## Phase 2 — Palettes replace LightTheme/DarkTheme as *data* (2–3 days)

**Do:** Author six **data** palettes (not 2000-line XAML clones):

- `Palettes/Industrial.Light.json` (or `.cs` frozen Color tables)
- `Industrial.Dark`, `Monet.Light`, `Monet.Dark`, `Crystal.Light`, `Crystal.Dark`

Engine paints TokenStore from the table + accent. LightTheme/DarkTheme remain merged **only** so old keys still exist.  
**Do not:** Delete LightTheme yet.  
**Exit:** Switching family changes TokenStore without depending on CrystalFluid XAML for those keys.

---

## Phase 3 — CompatibilityHub (1–2 days)

**Do:** Single C# map, not 10 XAML alias files:

```text
GlassUltraClearBrush        → SurfacePrimaryBrush
ModuleBackgroundBrush       → SurfacePrimaryBrush   // or Window/workspace — pick ONE role and stick
ElevatedSurfaceBrush        → SurfaceSecondaryBrush
MutedForegroundBrush        → TextTertiaryBrush
OutlineDefaultBrush         → BorderDefaultBrush
LiquidFocusBrush            → BorderFocusBrush
Crystal* / Liquid* / Glass* → documented TokenStore role
```

On `Publish()`, write **both** TokenStore and every mapped legacy key to the **same frozen brush instance**.  
**Do not:** Change view XAML yet.  
**Exit:** Old views still theme-switch because legacy keys are overwritten from TokenStore (direction: **new owns old**).

---

## Phase 4 — Kill dictionary swap for Light/Dark (1–2 days)

**Today:** `ThemeManager.ApplyTheme` add/removes `LightTheme.xaml` / `DarkTheme.xaml` (parse/cache).  
**Do:** Mode switch only changes `ThemeEngine.SetMode` + `Publish()`. Leave LightTheme merged once at startup as a **fallback dump**, never swapped.  
**Do not:** Remove the files from disk yet.  
**Exit:** Light↔Dark with no MergedDictionaries add/remove. No blank frame. Accent + family survive.

---

## Phase 5 — Material independent of family (1 day)

**Do:** `SetMaterial(Solid|Soft|Frosted|Crystal)` only changes material tokens (`ProGlassMaterial.*` backgrounds/opacity/effect). Family does **not** force Glass. Industrial+Crystal material is legal; Crystal family + Solid material is legal (no water).  
Map persisted `SurfaceMaterialMode` `Glass|Solid|Transparent` → `Frosted|Solid|Soft`.  
**Exit:** Settings can set family and material separately; water still gated on Crystal family **and** water toggle.

---

## Phase 6 — Component styles consume TokenStore only (3–5 days)

Order (one dictionary per PR):

1. Buttons (`Styles/ButtonStyles.xaml`) — one template, variants via token brushes  
2. Inputs (`InputStyles` + App.xaml implicit TextBox) — `SurfaceInputBrush` / `Input*`, never button brushes  
3. Toggle / CheckBox / Slider  
4. Tabs (`ModuleTabStyles`)  
5. Pills / KPI (`PillStyles`, `ErpKpiSurfaceStyle`, `PiKpiCell` BasedOn KPI)  
6. Dialogs / Tooltip  
7. DataGrid (`DataGridStyles`) — **clarity-first**, no glass on rows, keep virtualization  
8. Navigation / Shell  
9. List page chrome  

**Rule:** Replace `Glass*` / `Liquid*` inside templates with TokenStore keys. Leave `BasedOn` chains. Do **not** duplicate templates.  
**Exit:** Those dictionaries grep-clean of `Crystal`/`LiquidGlass`/`GlassUltra` except comments.

---

## Phase 7 — Screen migration, visual keys only (ongoing, module batches)

**Order:** Settings Appearance (dogfood) → Dashboard → PI list → PI steps chrome → JO → DN → TI → remaining.

**Allowed edit in XAML:** `Background`/`Foreground`/`BorderBrush`/`CornerRadius`/`FontSize` resource keys.  
**Forbidden:** `Text="{Binding …}"`, `ItemsSource`, converters, command bindings, column `Binding`, visibility that encodes workflow.

Script: replace known legacy keys using CompatibilityHub reverse map. Manual review for hardcoded `#hex` and `FontSize="12"`.

**Exit per module:** Theme switch live; no formula/regression in that module’s happy path.

---

## Phase 8 — Settings / Theme Studio rewrite (1–2 days)

Expose **only**: Family, Mode, Material, Density, Accent, Radius, Frost (Crystal), Shadow, Crystal Water, Reduced Motion.  
Advanced: opacity, overlay (already exist).  
Persist `VisualTheme` as `Industrial|Monet|Crystal`. One-time migrate:

| Saved | Becomes |
|---|---|
| `Normal`, cream SaaS | `Industrial` (or keep cream as Industrial Light palette) |
| `Normal (Industrial)` | `Industrial` |
| `Monet` | `Monet` |
| `Crystal Fluid` | `Crystal` |

**Do not** add shader playgrounds for factory users.

---

## Phase 9 — Environment isolation (Crystal only) (1–2 days)

Move water/caustics/ambient from being “always merged CrystalFluid XAML” to:

- Host control on Window/shell **only** when `Family==Crystal && WaterOn && !ReducedMotion`
- Unload visual tree on family leave (engine already removes some token keys — make it remove the control)

**Exit:** Industrial/Monet process has **zero** running water animation.

---

## Phase 10 — Print/export theme (1 day)

Confirm PDF/Excel/CSV use `Report*` / `PdfTheme` only. Grep export code for `Glass`/`Blur`/`Opacity` on document surfaces. No engine change to amounts/VAT.  
**Exit:** Printed PI/JO/DN/TI look paper-safe in all 6 theme combos.

---

## Phase 11 — Stop merging obsolete dictionaries (1 day)

`App.xaml` target:

```text
Converters
ProGlassDesignSystem (tokens + materials + components)
CompatibilityHub (generated ResourceDictionary from C# map)  // temporary
Languages
```

Un-merge (after Phase 6–7 zero-ref):

- `Themes/CrystalFluid/*.xaml` (9 files)
- `LiquidGlassDesign.xaml`, `LiquidGlassStyles.xaml`
- Duplicate Dash/Soft KPI if DashboardKit is sole consumer
- `SemanticTokens.xaml` / `LayoutTokens.xaml` / old alias files if superseded by TokenStore

Keep until Phase 12: LightTheme/DarkTheme **or** delete if Phase 4 proven.

**Exit:** App.xaml merged list ≤ ~12 dictionaries. Startup still resolves every DynamicResource used in MainWindow.

---

## Phase 12 — Delete the old engine (1–2 days)

**Delete or hollow out:**

- `Services/Theme/VisualThemeEngine*.cs` (replace with `ProGlassThemeEngine`)
- `ThemeManager` theme-swap / `PublishLiquidReference` / `PublishAboutPalette` liquid orbs (About uses TokenStore)
- `Themes/LightTheme.xaml`, `DarkTheme.xaml` if unused
- `Themes/CrystalFluid/`
- Crystal/Liquid resource names from CompatibilityHub
- `ManagedKeys` arrays

**Keep:** `ThemePipeline` as façade, `AppearanceSettings` fields (renamed), density, accent, outline, wallpaper **if** wallpaper is a TokenStore window background.

**Exit:** Solution grep `GlassUltraClearBrush`, `LiquidCard`, `CrystalFluid` → **zero** (except docs + this file). Windows Release build. PI 9 steps, JO/DN/TI list+detail, Dashboard, Settings theme matrix.

---

## Phase 13 — Hardening (1 day)

- Reduced motion: `MotionSpeedRatio=0`, skip water  
- Contrast check 6 palettes on DataGrid selection (use `SelectionForegroundBrush`, never White)  
- Keyboard focus: no new `FocusVisualStyle={x:Null}`  
- Perf: theme switch < existing; DataGrid scroll unchanged  
- No “100K lag-free” claims without a test

---

## Suggested calendar

| Week | Phases |
|---|---|
| 1 | 0, 1, 2, 3 |
| 2 | 4, 5, 6 (buttons/inputs/toggle) |
| 3 | 6 (grid/nav/dialog), 8, 9 |
| 4 | 7 (PI + Dashboard), 10 |
| 5 | 7 (JO/DN/TI), 11 |
| 6 | 12, 13 |

Slip is expected on Phase 7 (volume of XAML). Do **not** start Phase 12 early.

---

## Execution log (this run)

| Phase | Done in this run? | Notes |
|---|---|---|
| 0 Freeze contract | Yes | TokenStore + hub lists in `ThemeCompatibilityHub` |
| 1 TokenStore Publish | Yes | `ProGlassThemeEngine.Publish()` |
| 2 Palettes as data | Yes | `ProGlassPalettes` — fallbacks only when source brush missing |
| 3 CompatibilityHub | Yes | Semantic sources + DualWriteLegacy (same brush instance) |
| 4 Stop Light/Dark swap | Yes | Overlay cached Light/Dark onto Application.Resources; LightTheme stays merged as fallback; GetThemeColor reads cache |
| 5 Material independent | Yes | Parse/Persist Glass→Frosted, Transparent→Soft; Solid overrides family frost; water off unless Crystal family and not Solid |
| 6 Component TokenStore | Yes (chrome) | Buttons, inputs, dialogs, pills, shell, list, PI industrial, DashboardKit, module tabs, DataGrid |
| 7 Screen key migration | Yes (chrome keys) | Plus leftover GlassBorder/LiquidCard/Overlay in 24 views; About Liquid* and LiquidGlass *styles* remain |
| 8 Settings contract | Yes | Settings lists Industrial/Monet/Crystal only; saved iOS 26 → Crystal; Ios26 painter not deleted |
| 9 Water isolation | Yes | Water/ambient/preview off unless Crystal family + non-Solid material + motion; KPI cards bind gate, not force true |
| 10 Print tokens | Yes | Report* filled from Print* |
| 11 Un-merge CrystalFluid | Partial | Unused ScrollBar dict removed; Ios26 no longer a live family (Normalize → Crystal) |
| 12 Delete VisualThemeEngine | **No** | Still required; Ios26 painter unreachable via Normalize |
| 13 Hardening | Partial | DataGrid selected text = SelectionForegroundBrush; water already motion-gated |

**Safe single-run outcome:** one publisher (`ProGlassThemeEngine`) after every pipeline tick; old dictionaries remain until Phase 11–12 on a Windows build.

## Next phase to execute **now** (when implementation resumes)

**Phase 11–12 blocked** until Windows smoke + `rg` zero-ref on CrystalFluid/LiquidGlass. Do not un-merge or delete engines yet. Phase 13 hardening after that.

---

## Explicitly out of scope forever in this program

- Inquiry/PI/JO/DN/TI formulas, VAT, margins, sheet allocation  
- Changing `{Binding}` paths or converter math  
- Database schema  
- Licensing / login  
- “Make every screen Liquid Glass”
