# Theme Reduction — 2026-09-06

Owner decision: keep a tight, professional glass-ERP design system. The
experimental/extra visual families are removed in favour of **three**
families, with **Normal** as the new default.

## Final family catalogue

| Persisted key    | Display name | Light/Dark | Notes |
|------------------|--------------|------------|-------|
| `Normal`         | Normal       | both       | New **default**. No surface overrides (base theme values). |
| `Monet`          | Monet        | both       | Accent-derived Material-You palette (re-derives live on accent change). |
| `Crystal Fluid`  | **Ios-17pm** | **Light-only** | `ModePairFor(CrystalFluid) → false`; identity const stays `"Crystal Fluid"` (persisted key) while the display name is view-only via `FamilyDisplayName`. |

## Removed families

- **Professional** — cool corporate slate (consts + `ApplyProfessional`).
- **iOS Liquid Glass** — the 2026-08-29 default (`ApplyLiquidGlass`,
  `ApplyLiquidGlassTokens`, `BuildLiquidStageBrush`, `LiquidOrb`/`LiquidCaustic`,
  `FrostFill`/`SolidFrost`/`HueFrost` ramp authoring — the shared
  `FrostBrush`/`FrostFill`/`HueFrost` helpers were retained for Crystal Fluid).
- **Ocean** — deep teal corporate (`ApplyOcean`).
- **Industrial White / Grey / Dark** — the plain factory-floor trio
  (`VisualThemeEngine.Industrial.cs`, deleted outright, plus `IsIndustrial`
  and the settings rail's `IsIndustrialLook` visibility doors).

## Migration (existing installs)

`VisualThemeEngine.Normalize` now maps **every removed family and any unknown
saved value to `Normal`** — the new default. There is **no nearest-family
mapping** (e.g. an old `"Ocean"` or `"iOS Liquid Glass"` save does not resolve
to Monet/Crystal Fluid; it resets to Normal).

## Defaults changed

- `AppConfig.Appearance.VisualTheme` → `"Normal"` (was `"iOS Liquid Glass"`).
- `App.StartupCore` startup fallback → `VisualThemeEngine.Normal`.
- Settings `SelectedVisualTheme` fallback, wallpaper-scene fallbacks
  (`WallpaperOrchestrator`, `WallpaperStudio`), and the factory-reset path
  (`Typography`) → `Normal`.
- `VisualThemeEngine.Current` already defaulted to `Normal`; the family
  catalogue in `SettingsViewModel.ThemeWallpaper` now ships exactly three rows.

## Engine / service changes

- `VisualThemeEngine.Industrial.cs` deleted; `Part1/Part2/Part3` retain the
  Normal/Monet/Crystal-Fluid painters plus the shared glass/palette helpers
  (`PublishSectionGlass`, crystal edge/sheen brushes, `FrozenSolid`,
  `FrostBrush`/`FrostFill`/`HueFrost`, font palette, `FrozenAndSet`).
- `ThemeManager.ApplyAccent` guards re-own the tint brushes and dashboard
  ambient to Crystal Fluid (previously iOS Liquid Glass); the dashboard
  ambient now always applies.
- `BuiltinWallpaperScenes` ships **Studio** (Normal), **Golden Hour** (Monet)
  and **Crystal Fluid** (Ios-17pm) scenes.
- `SettingsView.xaml` rail sheen/glare layers are always visible (the
  `IsIndustrialLook` inverse-visibility doors are gone).

## Localization

- Family caption re-keyed `Settings.NormalMonetProfessionalIosLiquidGlassOcean`
  → `Settings.NormalMonetIos17pm` across all 15 language dictionaries, the
  `scripts/i18n/*.py` sources and the `scripts/i18n/translations/*.json`
  intermediates.
- Font-colour caption wording updated (Ios-17pm owns the About text palette,
  no longer "iOS Liquid Glass").

## Tests

- `ThemeRegistryParityTests` — family catalogue and `ModePairFor` pins now
  cover the three families; scene-catalogue coverage pinned to three.
- `LiquidGlassThemeSourceTests` — rewritten: removed-family inventory
  (`Removed_families_are_gone_and_normal_is_the_default_family`), Crystal
  Fluid token/tint pins, and the surviving shared design-system / cream /
  readability invariants (still-shipping Liquid* tokens, Normal cream canvas,
  no-blue-cast, Crystal Fluid readability, etc.).
- `CrystalFluidSettingsBridgeTests` — identity/display pins now assert Normal
  and Monet surface verbatim (Ocean removed).
- `CrystalFluidNavAdoptionTests` — "every family owns MenuHoverBrush" now
  covers Monet (Part1) + Crystal Fluid (Part3); Industrial read removed.

## Theme customization (Settings → Themes)

The Theme Studio's one legitimate capability — tuning the Crystal Fluid
material — was rebuilt as a **bounded, persisted, family-aware** surface
under a new top-level Settings **`Themes`** section (never the 0–100
playground).

- **Surface (six live axes only)** — water opacity, water speed, wave
  amplitude, caustic intensity, bubble intensity, reflection opacity. The
  glass / glass-border / shadow opacity axes stay **parked** in
  `CrystalFluidSettings` (no live consumer key) and are deliberately not
  surfaced.
- **Named presets** — **Default / Calm / Intense** (`ThemeCustomization.Presets`;
  Default == the authored baselines). Plus a **per-family Reset**: Crystal →
  baselines; Normal/Monet → default card shape (corner 12, border 1.5,
  gradients on).
- **Family-aware** — the Crystal group renders only while **Ios-17pm** is the
  active family (`SettingsViewModel.IsCrystalThemeActive`); Normal/Monet keep
  their existing Appearance controls (`CardCornerRadius`, `CardBorderThickness`,
  `UseGradientSurfaces`).
- **Persistence** — `AppConfig.Appearance.ThemeCustomization`
  (`ThemeCustomizationSettings`: the six axes + `CrystalPreset` identity,
  defaults = `CrystalFluidSettings.Baseline*`).
- **Single writer** — `ThemeCustomization.Apply(resources)` is the only place
  that writes the six `Crystal*` tokens; `ApplyCrystalFluid` delegates to it.
  `CrystalReducedMotion` remains a separate config-backed §43 flag.
- **UI / files** — `Views/Settings/Sections/SettingsThemesSection.xaml` +
  `SettingsViewModel.ThemeCustomization.cs`; rail row `Themes` →
  `Settings.SecThemes`; live preview reuses `CrystalWaterControl`.
- **Localization** — 19 new `Settings.*` keys added across all 15 language
  dictionaries, `scripts/i18n/*.py` sources and `translations/*-settings.json`
  intermediates.

## Static validation note

The sandbox has no .NET SDK, so this change was validated statically
(grep/diff/brace-balance) only; a `dotnet build` + `dotnet test` on a Windows
runner should confirm before release.

## Known pre-existing cosmetic wart (left as-is, out of scope)

The Crystal Fluid row in Settings shows **“Ios-17pm — Ios-17pm”**: its
wallpaper-scene label and its family display name are both authored
`Ios-17pm` (pinned by `CrystalFluidSettingsBridgeTests`), so the option row
duplicates the name. This predates the reduction (2026-09-05); changing the
scene label would require updating that pin.
