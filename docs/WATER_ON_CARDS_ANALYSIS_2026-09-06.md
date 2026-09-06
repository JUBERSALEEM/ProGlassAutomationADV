# Why cards don't get the Ios-17pm "water fill" globally — root-cause analysis

**Date:** 2026-09-06 · **Scope:** diagnostics only (no code changed)
**Question:** Why does switching to Ios-17pm restyle the glass but leave every card
around the app without the animated water/liquid-fill effect, even though the
water tokens are global `DynamicResource`s?

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

## 7. Implemented: the global "water material" (2026-09-06)

The attached-material option was implemented (the senior choice):

- **`Controls/CrystalFluid/CrystalWaterAttach.cs`** — an attached property
  `crystal:CrystalWaterAttach.Attach`. When `True` on a `Border`, on `Loaded` it
  wraps the card's single child in a `Grid` stacking
  **glass background → `CrystalWaterControl` (ZIndex 1) → content (ZIndex 5)**,
  reproducing the authored CrystalCard layer order on every card without
  re-typing the `<Border>` element.
- **Live, resource-driven:** the water self-gates on `CrystalWaterDefaultEnabled`
  and reads the ambient level from `CrystalDefaultWaterLevel` via
  `SetResourceReference` (DynamicResource semantics). It is Collapsed + fully
  idle under Normal/Monet/etc., turns on live when Ios-17pm is painted (no
  reload), and keeps flowing from the same six studio axes as the previews.
  This also fixes the pre-existing StatCard `Loaded`-snapshot defect (the
  attached path revalues on family switch).
- **Layout fidelity:** a `Border` insets its child by `Padding` and ignores the
  child's `Margin`; the wrapper zeroes `Border.Padding` and re-applies the exact
  inset as the content's `Margin` (honoured by the `Grid`), so content is
  pixel-identical while the water spans the full card. Corner radius follows
  the card's own `CornerRadius` so the water's rounded self-clip matches.
- **Wired into every card style** (one `<Setter Property="crystal:CrystalWaterAttach.Attach"
  Value="True"/>` per style — 27 styles across 19 files): `BaseCardStyle`
  (⇒ Elevated/Stat/Interactive*), `TaskCardStyle`/`PriorityCardStyle`,
  `ListRowCardStyle`/`InteractiveListRowCardStyle`, `ModuleSectionCardStyle`,
  `DialogCardStyle`, `LiquidGlassCard`/`LiquidGlassSubtleCardStyle`,
  `LiquidGlassStrongCardStyle`, `LiquidGlassDialogCardStyle`, `SpecGroupCardStyle`
  (⇒ `JoFormCardStyle`), `DashCardStyle`/`CardStyle`, `SoftCardStyle`/
  `SoftCardInteractiveStyle`/`SoftSectionStyle`, the dashboard tiles/wells
  (`DashTileStyle`, `SoftTileStyle`, `SoftChartWellStyle`),
  `CompactStatTileStyle`, `PiCostTileStyle`, and the per-module `KpiCardStyle`
  (×3), `TgtCardStyle`, `AboutCardStyle`/`HeroCardStyle`, `SectionCardStyle`
  (×2), `PdfOptionCardStyle`, `WizardCardStyle`, `LicenseMetricCardStyle`,
  `PendingRowCardStyle`.
- **Deliberately excluded:** the opaque `HeroCard*` accent banners (solid
  signal fills — water would tint them), workspace canvases/stages
  (`PiWorkSurfaceStyle`, `PiProductViewportStyle`), and non-card chrome
  (`TabCardInactiveStyle`, `CardLabelStyle`, `WizardCardHeaderStyle`,
  `SidebarSubMenuCard`, pills/chips/rails/bars/headers/footers). Any
  additional surface opts in with the same one line.
- **Regression pin:** `Tests/Agent/CrystalFluidGlobalWaterTests.cs` asserts the
  attached material's gate wiring and the presence of the setter across the
  shared and per-module card styles (runs on the Windows `dotnet test` gate).

### Perf note (known trade-off, reversible)
One extra `CrystalWaterControl` per attached card (~1,689 borders). Under
non-crystal families it is `Collapsed` with zero running storyboards; under
Ios-17pm it animates, but the control already pauses on unload/hidden
(virtualized rows stop off-screen) and honours `CrystalReducedMotion`. If a
specific dense grid is ever too heavy, remove the single setter for that style
to opt it back out.
