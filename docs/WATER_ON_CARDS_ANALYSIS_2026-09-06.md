# Why cards don't get the Ios-17pm "water fill" globally — root-cause analysis

**Date:** 2026-09-06 · **Scope:** analysis + implementation (v2 global water material
+ ultra-clear visual recalibration)
**Question:** Why does switching to Ios-17pm restyle the glass but leave every card
around the app without the animated water/liquid-fill effect, even though the
water tokens are global `DynamicResource`s — and how to make every card accept
it globally?

## TL;DR

The water effect is **not a brush and not a resource** — it is a templated
**control** (`CrystalWaterControl`). The app's cards are plain **`Border`**
elements, and a `Border` is a `Decorator` with **no `Template` property**: its
visual is exactly *one background brush + one border + one child*. You cannot
inject a multi-layer animated water composition into it by changing a
`DynamicResource`, because there is no water in a `Border`'s visual tree to
revalue. The `DynamicResource` plumbing is working correctly — it is revaluing
glass brushes, which is why the cards *do* change when you switch families.
They just never contain water in the first place.

## 1. What "water" actually is

`Controls/CrystalFluid/Controls/CrystalWaterControl.cs` is a `Control` whose
template (`Themes/CrystalFluid/CrystalFluidControls.xaml:38–118`) lays out, inside
a `Canvas` (`PART_WaterCanvas`):

| Template part | Element | Fill / purpose |
|---|---|---|
| `PART_WaterBody` | `Border` | translucent aqua body, anchored at the waterline |
| `PART_CausticLayer` | `Rectangle` | `CrystalCausticBrush`, drifting caustic light |
| `PART_WavePath` | `Path` | `CrystalWaterSurfaceLineBrush`, the surface wave |
| bubbles | generated in code | `CreateBubbleStoryboard` (lines 673–760) |

Motion is **storyboard-animated transforms** (wave translate/scale at 542–560,
caustic at 583–594, bubbles at 687+), not a brush fill. This is inherently a
**multi-layer, multi-child, animated composition**. WPF's `Brush` primitives
(`SolidColorBrush`, `LinearGradientBrush`, …) paint a single fill; there is no
"animated water" brush type. That is the first fundamental reason a
`Background="{DynamicResource …}"` can never *become* water.

## 2. What the cards actually are

`Themes/Styles/CardStyles.xaml` defines every shared card style with
`TargetType="Border"` (`BaseCardStyle`, `ElevatedCardStyle`, `StatCardStyle`,
`TaskCardStyle`, `Interactive*`, …). Across `Views/` there are **~1,689
`<Border>` elements**, consuming dozens of card style keys:

```
SoftCardStyle ×43 · ModuleSectionCardStyle ×21 · KpiCardStyle ×17 ·
AboutCardStyle ×9 · TgtCardStyle ×7 · SectionCardStyle ×6 · … (many more)
```

Each one paints:

```xml
<Setter Property="Background"  Value="{DynamicResource GlassUltraClearBrush}"/>
<Setter Property="BorderBrush" Value="{DynamicResource GlassBorderGradientCrystal}"/>
```

`GlassUltraClearBrush` resolves to `ModuleBackgroundBrush`, a `FrostFill(…)`
gradient published by `PublishSectionGlass`
(`Services/Theme/VisualThemeEngine.Part2.cs:154–162`). In Crystal Fluid it is a
**static frosted-white glass** — not water.

A `Border` inherits from `Decorator`; it has **no `ControlTemplate`**. So even a
perfectly crafted implicit `Style` for `Border` can only revalue `Background`,
`BorderBrush`, `Effect`, etc. — it can never add a water layer behind the
content. This is the hard architectural blocker.

## 3. The `DynamicResource` tokens exist — but only the water control reads them

`ApplyCrystalFluid` (`Services/Theme/VisualThemeEngine.Part3.cs`) publishes the
family gate and the live axes:

```csharp
resources["CrystalWaterDefaultEnabled"] = true;      // line 207–208
ThemeCustomization.Apply(resources);                  // opacity/speed/caustics/bubbles/wave
resources["CrystalReducedMotion"] = App.Config?.Appearance?.ReducedMotion == true;
```

`CrystalWaterControl`'s style consumes those tokens
(`CrystalFluidControls.xaml:39–60`): `CrystalWaterOpacity`, `CrystalWaterSpeed`,
`CrystalBubbleOpacity`, `CrystalReducedMotion`, `CrystalWaveAmplitude`.

But a `Border` card has **no `CrystalWaterControl` child**, so those keys have
nothing to act on. The resources are not "rejected" — they are simply
**unconsumed** by cards. The glass keys (`GlassUltraClearBrush`, …) *are*
consumed and *do* update, which is exactly the "glass changed but no water"
behaviour being reported.

## 4. Where water is instantiated today (three places only)

| Host | File | Gate |
|---|---|---|
| `StatCardControl` (KPI card, 5 uses in `Views/`) | `Controls/StatCard/StatCardControl.xaml:124` | `WaterEnabled` DP ← `CrystalWaterDefaultEnabled`, read **once on `Loaded`** (`StatCardControl.xaml.cs:26–33`) |
| Settings Appearance live preview | `Views/Settings/Sections/SettingsAppearanceSection.xaml:284–310` | `CrystalKpiCard` (`WaterEnabled=True` in its style) |
| Settings Themes "water reservoir" preview | `Views/Settings/Sections/SettingsThemesSection.xaml:346` | `WaterEnabled="{DynamicResource CrystalWaterDefaultEnabled}"` |

`CrystalCard` (the water-optional card, `WaterEnabled=false` default) is used in
**no production view**.

## 5. A second, subtler defect: live family switches don't retint KPI water

`StatCardControl` snapshots the gate only in `OnStatCardLoaded`:

```csharp
if (ReadLocalValue(WaterEnabledProperty) == DependencyProperty.UnsetValue)
    WaterEnabled = TryFindResource("CrystalWaterDefaultEnabled") is bool on && on;
```

If a `StatCardControl` is already on screen under Normal/Monet and the user
switches to Ios-17pm at runtime, `CrystalWaterDefaultEnabled` flips to `true`
but the card never re-reads it — so even the KPI water stays off until the view
is reloaded. (The Settings previews bind `WaterEnabled` via
`{DynamicResource CrystalWaterDefaultEnabled}` and therefore *do* update live —
which makes the preview look correct while the rest of the app looks broken.)

## 6. Why it was designed this way (deliberate, not an accident)

`Themes/CrystalFluid/CRYSTAL_FLUID_DESIGN.md`:

- **§5/§20/§68 performance laws:** *"No per-card timers; storyboard transforms
  only; pause on `IsVisibleChanged`."* Water on ~1,689 live borders means ~1,689
  storyboard hosts — the exact "expensive architecture" the spec forbids.
- **Deviation #4:** the `WaterLevel` DP was wired only to the studio preview;
  *"binding real ERP ratios belongs to module owners — the door is open."*
- **§30:** KPI cards are the designated "liquid reservoirs"; ordinary cards were
  never specified to carry water.

So the gap is a **scoped-out feature**, not broken plumbing.

## 7. Why "one global shared style" cannot reach the cards (the WPF truth)

The natural first answer — "just make ONE implicit style and every Border
inherits it" — does not work in WPF, and it is important to say exactly why:

1. **Explicit `Style=` suppresses the implicit style.** A keyed card style
   (`<Border Style="{StaticResource SoftCardStyle}">`) means the implicit
   `<Style TargetType="Border">` is *not* applied to that element at all
   (explicit style replaces implicit; they never merge). The census counts
   **598 explicitly-styled Borders** — an implicit style would silently miss
   all of them.
2. **An implicit `Border` style is a loaded gun.** It *would* fire on the
   ~899 unstyled Borders that are **not** cards: divider rails, icon chips,
   accent strips, grid cell lines, backdrop fills. Water behind every one of
   those would be noise, not design. (Measured: 1,689 Borders in `Views/` →
   598 styled, **192 unstyled glass-surface cards**, 382 unstyled other-fill,
   517 unstyled no-background.)
3. **"Card" is a convention, not a type.** WPF has no "all cards" selector
   because a card is just a `Border` wearing glass. The deterministic signal
   that a Border *is* a card is its **`Background` being one of the glass
   SURFACE brushes the engine repaints per family** — that is exactly what the
   implementation below keys on, so it cannot misfire on rails/chips/tints.

So the correct "global" mechanism is: one **class-level `Loaded` handler on
`Border`** (fires for every Border, styled or not, present or future) plus a
**deterministic glass-surface test**. No per-style setters, no per-file edits,
no opt-in list to keep in sync.

## 8. Implemented v2: global auto-detect water material (2026-09-06)

- **`Controls/CrystalFluid/CrystalWaterGlobal.cs`** — `Install()` registers one
  `EventManager.RegisterClassHandler(typeof(Border), LoadedEvent, …)` (the same
  pattern the repo already uses in `ViewportFitBehavior.Install()`), invoked
  once at startup in `App.StartupCoreAsync`. Every `Border` that loads is
  offered the material.
- **`Controls/CrystalFluid/CrystalWaterAttach.cs`** — `TryAutoAttach(border)`
  wraps a card `Border`'s child in a `Grid` stacking **glass →
  `CrystalWaterControl` (ZIndex 1) → content (ZIndex 5)** (authored CrystalCard
  layer order), and recognises a card by **reference-comparing the Border's
  effective `Background` to the engine's glass surface brushes AND requiring
  rounded corners** (`GlassUltraClearBrush`, `GlassSurfaceBrush`,
  `ModuleBackgroundBrush`, `ElevatedSurfaceBrush`, `SideMenuBackgroundBrush`,
  `AboutInteractiveBrush`, `LiquidCard`, …). DynamicResource shares the brush
  instance and `CornerRadius` is an exact DP, so the test is deterministic —
  styled cards AND inline `<Border.Style>` cards qualify; rails/chips/tints
  never do, and rectangular glass chrome (tab strips, station rails, footer
  bars, work surfaces — radius 0) is excluded by the radius criterion.
- **Guards:** skip childless Borders (rails/dividers), Borders that already
  host a `CrystalWaterControl` (StatCard/CrystalCard — no double water), and
  Borders inside another card surface (the outermost card per region wins, so
  nested panels don't stack two water-lines).
- **Tri-state override:** `crystal:CrystalWaterAttach.Attach="False"` opts a
  specific surface out; `"True"` forces a non-glass host in. Unset = auto.
- **Live, resource-driven:** water self-gates on `CrystalWaterDefaultEnabled`
  + `CrystalDefaultWaterLevel` via `SetResourceReference` (DynamicResource
  semantics). Collapsed and fully idle under Normal/Monet/etc., turns on live
  when Ios-17pm is painted (no reload), and flows from the same six studio
  axes as the previews. This also fixes the pre-existing StatCard
  `Loaded`-snapshot defect (revalues on family switch).
- **Layout fidelity:** a `Border` insets its child by `Padding` and ignores the
  child's `Margin`; the wrapper zeroes `Border.Padding` and re-applies the exact
  inset as the content's `Margin` (honoured by the `Grid`), so content stays
  pixel-identical while the water spans the full card; the water's rounded
  self-clip follows the card's `CornerRadius`.
- **Regression pin:** `Tests/Agent/CrystalFluidGlobalWaterTests.cs` (class
  handler, deterministic detection, gate wiring).

### Perf note (known trade-off, reversible)
One extra `CrystalWaterControl` per glass card. Under non-crystal families it is
`Collapsed` with zero running storyboards; under Ios-17pm it animates but
pauses on unload/hidden (virtualized rows stop off-screen) and honours
`CrystalReducedMotion`. Opt any surface out with
`crystal:CrystalWaterAttach.Attach="False"`.

## 9. Visual recalibration — rich blue liquid glass (2026-09-06, v3)

The first "ultra-clear" pass went too far: at 15–20% alpha the water vanished
and the card frost read as pale white — so only the authored Settings-preview
KPI cards (their own template) looked "liquid" while the rest of the app
looked unthemed. v3 reverses that direction: water is now a clearly visible
RICH BLUE liquid and the card frost reads as blue glass, matching the
"glass ERP" identity (all values still in the authored token files, so the
six live studio axes sit on top unchanged):

| Token | Before (ultra-clear) | After (rich blue) | Effect |
|---|---|---|---|
| `CrystalWaterSurfaceColor` | `#268AEAF8` (15%) | `#8C7CC4F8` (55%) | visible blue meniscus |
| `CrystalWaterBodyColor` | `#2E4FC5EB` (18%) | `#7A3B82F6` (48%) | rich blue body |
| `CrystalWaterDeepColor` | `#333FB8E0` (20%) | `#8C245BD0` (55%) | deep blue depth |
| `CrystalWaterCyanTintColor` | `#2A63C8EE` (16%) | `#6654A5F0` (40%) | blue depth tint |
| `CrystalWaterHighlightColor` | `#B8FFFFFF` | `#E6FFFFFF` | stronger gloss |
| `CrystalWaterCausticColor` | `#17FFFFFF` | `#1FFFFFFF` | subtle caustic |
| Engine stage (`CrystalStageTop/Light/BaseBrush` + fallbacks) | `#F8FDFF/#EDFAFF/#DFF5FF` | `#EEF6FF/#DBEDFF/#C6E4FF` | bluer environment |
| `FrostBrush` accent kiss | light `0.16` | light `0.30` (dark `0.32→0.38`) | blue glass, not white |
| Card `FrostFill` alphas (module / rail / elevated) | `0.24/0.10 · 0.46/0.24 · 0.52/0.32` | `0.36/0.18 · 0.58/0.32 · 0.64/0.42` | richer surfaces |

**About-module adaptation (same pass):** `ThemeManager.ApplyAccent` no longer
skips the crystal family — `PublishAboutPalette` (already ultra-low-alpha
crystal glass) now runs under Ios-17pm too, so the About cards wear the same
accent-tinted transparent glass + water as every other card instead of
staying pinned to base theme-file values.

Authority-value tests were repointed to the new numbers
(`CrystalFluidShellAdoptionTests`, `LiquidGlassThemeSourceTests`) — these are
deliberate design changes, not drift. Reduced-motion still freezes the whole
water plane to a static body.
