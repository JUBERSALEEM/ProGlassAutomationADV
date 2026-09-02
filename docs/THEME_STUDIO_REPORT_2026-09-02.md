# THEME STUDIO — Final Report (13-Point)

**Date:** 2026-09-02
**Scope:** Centralized Appearance/Theme Studio — live 0–100 visual customization on ProGlassAutomation WPF.
**Constraint honored:** Static analysis only in this sandbox (no compiler). Every change is source-verified: XAML well-formedness, brace/paren balance, key coverage, StaticResource audit. Windows `dotnet build` + `dotnet test` is the first step on a real machine.

---

## 1) Existing theme architecture discovered

- **Six visual families** are NOT six top-level XAML files — they are six constants in `Services/Theme/VisualThemeEngine.Part1.cs` (`Normal`, `Monet`, `Professional`, `iOS Liquid Glass`, `Ultra Transparent`, `Ocean`) layered **over** the two base dictionaries (`Themes/LightTheme.xaml`, `Themes/DarkTheme.xaml`).
- `VisualThemeEngine.Apply(family, wallpaperPath, opacity)` and `ApplyModule(moduleKey, appearance)` paint brushes **directly into `Application.Current.Resources`** (highest lookup precedence), after `ClearOverrides()` removes the previous family's keys so the base dictionary values flow back.
- **`ManagedKeys`** (Part1.cs) is the authoritative registry of overridable keys. Four accent-derived keyline keys (`OutlineBrush`, `OutlineDefaultBrush`, `BorderBrush`, `DividerBrush`) are deliberately NOT in it — `ThemeManager.ApplyAccent` derives them per accent hue (a code comment explicitly warns that listing them would re-trigger the keyline bug). **The Theme Studio respected this restriction.**
- `ThemeManager` (Part1/Part2) already provided live apply methods: `ApplyTheme`, `ApplyAccent`, `ApplyOutline`, `ApplyCardBorderThickness` (0–4 DIP), `ApplyCardCornerRadius` (clamped 0–28, writes `CardCornerRadius`/`ControlCornerRadius`/`CardCornerRadiusLarge` at the same value), `ApplyDisplayDensity`, `ApplyUiScale`.
- Persistence is `AppConfig.Appearance` → `ConfigService.Save(App.Config)` → `%AppData%\ProGlassAutomation\config.json`. VM pattern everywhere: setter → apply → save → audit → `OnPropertyChanged`.
- Startup restores the whole chain in `App.StartupCore.cs` (theme → gradient pref → family → accent → outline → border → corner → font → density → ui-scale).
- The iOS "Liquid Glass" frost builder (`FrostFill(accent, topAlpha, bottomAlpha)` → `FrostBrush`) is Light/Dark-aware (warm-white base in Light, deeper tint in Dark) — the six families genuinely respond to the base theme.

## 2) Themes discovered / preserved

All six families are untouched — their identity, colors and glass-vs-solid behavior are preserved:

| Family | Glass nature (as designed) |
|---|---|
| Normal | Solid base dictionaries (opaque surfaces) |
| Monet | Material-You glass, accent-derived gradient surfaces |
| Professional | Mostly solid, restrained borders |
| iOS Liquid Glass | Frosted accent-tinted glass, iOS liquid ramp |
| Ultra Transparent | Highly transparent minimal glass |
| Ocean | Frosted glass with strong tint |

The studio layer only **modulates** what each family already paints (alpha pairs, tint strength, shadow parameters, corner/border tokens). Family switch order was verified: `ClearOverrides` → family apply → wallpaper → font pref → `GuaranteeCriticalResources` — the studio hooks in **after** this, so removal of the studio leaves the original visuals byte-identical.

## 3) Files created

| File | Purpose |
|---|---|
| `Services/Theme/ThemeStudioSettings.cs` | 0–100 profile model, per-family defaults, 5 centralized presets (Crystal / LightGlass / Balanced / Frosted / DenseGlass) |
| `Services/Theme/ThemeStudioService.cs` | Snapshot-transform engine: captures family-painted brushes, clones + parametrically transforms them, writes fresh instances under the same keys (live, idempotent, no disk IO) |
| `ViewModels/Settings/SettingsViewModel.ThemeStudio.cs` | VM partial: 16 slider properties, preset picker, Reset-Current / Reset-All commands, 600 ms debounced save (DispatcherTimer) |
| `Tests/Agent/ThemeStudioSourceTests.cs` | Source-level regression pins (repo convention) |

## 4) Files modified

| File | Change |
|---|---|
| `Services/AI/AIConfig/AppConfig.cs` | `AppearanceSettings.Studio` (`ThemeStudioSettings?`) — persisted inside existing config.json |
| `App.StartupCore.cs` | `ThemeStudioService.Restore()` at the end of the 9-step theme chain (studio values win; no profile = family defaults) |
| `Services/Settings/AppearanceService.cs` | `ThemeStudioService.OnFamilyApplied()` after every theme apply (Light/Dark toggle never drops customization) |
| `ViewModels/Settings/SettingsViewModel.ThemeWallpaper.cs` | `OnFamilyApplied()` in `ApplyVisualTheme()` (family/wallpaper switches) + `StudioFamilyName` refresh |
| `ViewModels/Settings/SettingsViewModel.Appearance.cs` | Classic border/corner sliders mirror into the studio draft when the studio layer is active (no competing settings) |
| `ViewModels/Settings/SettingsViewModel.AppearanceSection.cs` | `InitializeThemeStudio()` in `InitializeAppearanceSection()` |
| `ViewModels/Settings/SettingsViewModel.Helpers.cs` | `DisposeThemeStudio()` in `Dispose()` |
| `Services/ThemeManager.Part2.cs` | `ApplyDisplayDensity` re-snapshots/re-applies the studio layer (density and studio padding share the same token) |
| `Services/Performance/PerformanceProfileService.cs` | `PublishResources` re-snapshots/re-applies the studio layer (animation slider shares `MotionDuration*`) |
| `Views/Settings/Sections/SettingsAppearanceSection.xaml` | Full-width "Theme Studio" tile: preset combo, Reset buttons, Basic row (5 sliders), Advanced grid (11 sliders) |
| `Themes/Languages/en-US.xaml`, `ar-AE.xaml` | 26 new keys (`Settings.ThemeStudio*`, `Settings.Studio*`, `Msg.ThemeStudioResetAll`); other languages fall back to English via `LocalizationService.T` |
| `Views/SheetStore/SheetStoreView.xaml` | **Bug fix:** selected-sheet tile `StaticResource` → `DynamicResource` for `LiquidSelectionBrush` + `AccentBrush` (see §13) |
| `Tests/Agent/LiquidGlassThemeSourceTests.cs`, `Tests/TestSupport/SourceFamily.cs` | App-family read (`App.xaml.cs` + `App.*.cs`) for source assertions |

## 5) Centralized resources

- **Registry:** the studio reuses the engine's own keys (`ManagedKeys` + engine-written keys + `ThemeManager` tokens). No duplicate key universe, no competing dictionary.
- New keys published: **`DensityCardPadding`** (scaled — the token the card styles already consume) and **`MotionDurationFast/Norm/Slow`** (scaled — consumed by GodViews/SectionActivityView). No new standalone resources.
- All studio writes are clones of snapshotted instances → the base family brushes are never mutated, and `ResetAll` re-runs the engine chain for exact restoration.

## 6) Theme settings

- Persisted as `AppearanceSettings.Studio` inside the **existing** `config.json` (no second settings database — requirement honored).
- Restore-on-startup: `ThemeStudioService.Restore()` runs **last** in the startup chain, so the saved 0–100 profile wins over family defaults, and an uncustomized profile (`IsCustomized == false`) is skipped entirely.
- The `IsCustomized` flag distinguishes "user touched the studio" from "family defaults rule"; `Reset Current Theme` clears the profile back to family defaults.

## 7) Slider / property mappings (0–100 → WPF)

| Slider | Mapping | Notes |
|---|---|---|
| Frostness | surface alpha boost `0.12×v` + raises readability floor `0.38 + 0.25×v` | never below floor |
| Transparency | surface alpha scale `1.0 → 0.45` (i.e. `1 − 0.55×v`), clamped to floor | never unreadable |
| Blur | frost-density boost on glass keys (`0.10×v`), since WPF backgrounds are alpha-composited, not GPU-blurred | |
| Tint | glass keys blended toward `AccentColor` (solid: full strength; gradient stops: ×0.6) | |
| Corner Roundness | `28 × v` DIP → same `CardCornerRadius`/`ControlCornerRadius`/`CardCornerRadiusLarge` via `ThemeManager` | engine clamp 0–28; 100 ≈ pill |
| Card Padding | `DensityCardPadding` scaled `0.75× → 1.25×` around the density base | density changes re-snapshot the base |
| Shadow Intensity | `DropShadowEffect.Opacity × (0.35 + 1.35×v)` | |
| Shadow Softness | `BlurRadius = 2 + 62×v` | |
| Shadow Depth | `ShadowDepth × (0.2 + 1.3×v)` | |
| Border Visibility | border alpha × `(0.12 + 0.88×v)` | |
| Border Opacity | border alpha × `(0.20 + 0.80×v)` | combined with visibility |
| Border Thickness | `4 × v` DIP via `ThemeManager.ApplyCardBorderThickness` | same range as classic slider |
| Background Visibility | wallpaper/window keys alpha `0.15 → 1.0` | integrates with existing wallpaper support |
| Background Glow | glow keys alpha `0.20 → 1.0` | |
| Highlight | sheen keys alpha `0.15 → 1.0` | |
| Animation Intensity | `MotionDuration*` scaled `0.5× → 1.5×` | consumed by GodViews |

**Sanity requirement honored:** transparency/opacity and corner radius are *not* naive `value/100` — transparency has a readability floor that rises with frost; corners map 0–100 onto the engine's 0–28 DIP clamp; border thickness reuses the classic 0–4 DIP range so the two controls can never disagree.

## 8) Persistence method

- `ThemeStudioService` is **disk-free** by design — sliders mutate only live resources.
- The VM debounces `ConfigService.Save` with a 600 ms `DispatcherTimer` (restarted on every change) → **zero disk writes while dragging**.
- On settle: `AppearanceSettings.Studio = draft.Clone()`, save, `AuditService.Log("Theme Studio adjusted", family · profile)`.
- Restored at startup exactly as left (see §6). Presets and resets also audit-log.

## 9) Presets

Centralized per-profile presets in `ThemeStudioSettings.Preset(name)`, applied on top of **any** family:

- **Crystal** — clear, highly transparent, minimal shadow
- **LightGlass** — airy bright glass
- **Balanced** — modern default (aligns with the family defaults)
- **Frosted** — dense readable frost
- **DenseGlass** — maximum readability (high frost, low transparency)

Each family also has its own **`DefaultsFor(family)`** profile (per-theme defaults for reset): glass families start transparent/frosted, solid families start opaque with stronger borders. Preset picker selection clears as soon as a slider is moved (the profile is no longer the preset identity).

## 10) Reset behavior

- **Reset Current Theme** (`ResetStudioThemeCommand`): restores the active family's `DefaultsFor` and clears the saved profile (`Studio = null`). No confirmation — it is live and undoable via presets.
- **Reset All** (`ResetStudioAllCommand`): **Yes/No confirmation** (`Msg.ThemeStudioResetAll`), then clears the studio layer for all six families and re-runs the engine chain (`ThemeStudioService.ResetAll()` → `AppearanceService.ApplyTheme`), so every family returns to its exact designed look.
- No silent user-setting changes: `IsCustomized` is only set when the user interacts; an untouched family never shows "Customized".

## 11) Performance

- Live apply per slider tick = ~40 `Freezable.Clone()` + parameter writes + resource assignments — the same order of work the engine itself does per apply; no view recreation, no layout passes beyond what WPF does for a resource change.
- No disk IO during drag (debounce), no timers running between changes (timer stops on tick).
- Studio is a no-op when `Active == null` (snapshot only), so an uncustomized session costs one dictionary scan at startup.

## 12) Testing

Static verification performed (no compiler in sandbox):

- **XAML well-formedness:** all modified XAML parsed (xml.dom) — Settings section, SheetStore, both language files.
- **Brace/paren/bracket balance** on every modified/new C# file (string/comment-aware scanner).
- **Key coverage:** every managed key verified present in BOTH `LightTheme.xaml` and `DarkTheme.xaml` (25 × 2) and in the engine's family writers; the one missing key (`GlassInnerHighlightBrush`) was corrected to `GlassInnerHighlight` during review.
- **StaticResource audit:** all `StaticResource` references in Views/Controls/MainWindow/App intersected with every runtime-written key (engine + ThemeManager + studio + density + motion) → **1 real hit found and fixed** (SheetStore selected tile, see §13), then re-audited to **0**.
- **New regression tests:** `Tests/Agent/ThemeStudioSourceTests.cs` (8 facts) pins the architecture; `SilentCatchAuditTests`/`SourceFamily` family-read conventions verified for the `.PartN.cs` and `App.*.cs` split conventions.
- **Manual test matrix for the Windows machine** (included below): 6 themes × load/switch/cards/dialogs/DataGrids/navigation; slider extremes 0/50/100; repeated switching order; restart-restore; persistence; accent + family + Light/Dark combos.

### Windows manual test matrix

1. Start app → Settings → Appearance → Theme Studio tile renders (Basic + Advanced rows, preset combo, Reset buttons).
2. Drag each slider to 0, 50, 100 — UI updates live, no freeze, no disk write during drag (check config.json mtime).
3. Switch visual family while customized — customization survives (family identity + studio layer).
4. Toggle Light/Dark while customized — profile survives.
5. Change accent while customized — glass tint follows the new accent.
6. Apply each preset on each family — sane, readable results.
7. Reset Current → family defaults; Reset All (confirm) → all six families pristine.
8. Restart → exactly as left; delete `Studio` from config.json → family defaults, no errors.
9. SheetStore → select a sheet → change accent/family → selected tile recolors live (the fixed defect).
10. Extreme transparency (100) + dense frost (100) — text stays readable (floor logic).
11. Print preview (PrintableInvoice), Login/Splash — unchanged (intentionally preserved ink/brand).
12. Business flows: PI → Job Order → DN → Tax Invoice → reports → inventory — unaffected.

## 13) Intentionally preserved hardcoded styles (and why)

The full-app hex census (127 colors in Views/Controls/Behaviors/Agent/MainWindow/App.xaml, Languages excluded) was triaged; **no search-and-replace was performed** (per the safe-migration rule):

| Cluster | Count | Why preserved |
|---|---|---|
| `PrintableInvoiceView.xaml` | 40 | Print ink — PDF output must stay fixed regardless of on-screen theme |
| `AuthSurfaceStyles.xaml` + `SplashView.xaml` | 46 | Brand aurora gradients (login/splash identity) |
| `MainWindow.xaml` amber warning block | 6 | Deliberate high-visibility warning colors, not theme surfaces |
| `SettingsSectionStyles.xaml` ProviderDot | 2 (+2 dims) | Provider brand colors; dims are scrims |
| Black `#000000` dim overlays (~15) | — | Modal scrims — must dim regardless of theme |
| `RecordOverlayWindow` `#E6101824` | 1 | Recorder overlay must stay dark in every theme |
| Scattered dialog accents (charts, status, text) | ~10 | Single-purpose status/ink colors, not theme surfaces |

**Found & fixed during the cross-check (the "iOS sidebar not accepting theme" root cause):** `Views/SheetStore/SheetStoreView.xaml` bound the selected-sheet tile to `LiquidSelectionBrush` + `AccentBrush` with **`StaticResource`** — both are repainted live by the engine/accent manager, so the tile froze at load-time values and ignored theme/family/accent switches. Converted to `DynamicResource` and pinned by a regression test. An exhaustive sidebar audit (MainWindow drawer, ShellStyles sidebar nav styles, SettingsView nav rail, AiChatView side menu, StatementGenerator shell, Controls/) found every other sidebar surface already `DynamicResource` with all keys present in both base dictionaries — the SheetStore tile was the only real violation.

---

## Carryover (unchanged commitments)

- No compile/run in this sandbox — first step on Windows: `dotnet build ProGlassAutomation.sln` then `dotnet test`.
- 3.8 publish commitments (RELEASE_GATE §1 amber / §8 audits) remain tracked in `RELEASE_GATE.md`.
