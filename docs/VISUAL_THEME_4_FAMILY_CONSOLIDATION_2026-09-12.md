# VISUAL THEME — 4-FAMILY CONSOLIDATION (2026-09-12)

Owner ask: "keep only 4" visual styles in the global switcher, with **iOS 26**
and **iPhone 17 Pro Max** among them, fully implemented (not names-only), and
one coherent UI applied globally.

## 1. Deep-analysis findings (whole-app UI pass)

### 1.1 The catalogue had drifted to FIVE families

| Engine const | Persisted identity | Switcher label (before) | Status |
|---|---|---|---|
| `Normal` | `"Normal"` | Normal — Studio | cream SaaS default |
| `NormalIndustrial` | `"Normal (Industrial)"` | Normal (Industrial) — Studio | restored industrial look; was the config default |
| `Monet` | `"Monet"` | Monet — Golden Hour | accent-derived |
| `CrystalFluid` | `"Crystal Fluid"` | Ios-17pm — Ios-17pm | water-glass, light-only |
| `Ios26` | `"iOS 26"` | iOS 26 — Aurora | Liquid Glass, app-wide |

Three hand-maintained lists had to agree and did not:

- `SettingsViewModel.ThemeWallpaper.cs` `VisualThemeOptions` — **5 rows**.
- `BuiltinWallpaperScenes.All` — **5 rows**.
- `ThemeRegistryParityTests.AllFamilies` — **4 rows** (silently missing iOS 26).

### 1.2 Why "iOS 26 did 0%" — a one-line Normalize gap

`VisualThemeEngine.Normalize()` (Part3) is the single gate every saved /
selected family value passes through. Its switch handled `Monet`,
`CrystalFluid` and `NormalIndustrial` but had **no `Ios26` case**, so any
value of `"iOS 26"` fell through the `_ => Normal` default. Selecting iOS 26
in the switcher therefore applied **Normal** — the whole 68-key Liquid Glass
token table never ran. The user's "iOS 26 is only a name, its work is 0%"
was literally correct. Fixed by registering `Ios26 => Ios26` in the switch.

### 1.3 "Ios-17pm" vs "iPhone 17 Pro Max"

`FamilyDisplayName(CrystalFluid)` returned `"Ios-17pm"`, and the wallpaper
scene catalogue also labelled the Crystal Fluid scene `FamilyDisplayName(...)`,
so the theme picker rendered `Ios-17pm — Ios-17pm`. Renamed the display to
**iPhone 17 Pro Max** and gave the scene its own label **Crystal Waters**.

### 1.4 Hard-coded hex in View XAML (8 files) — audited, NOT churned

The 8 View XAML files that contain `#RRGGBB` literals were all classified:

- **Decorative alpha-white sheens** — `AboutStyles.xaml`, `AuthSurfaceStyles.xaml`
  (glass reflection/glow gradient stops; theme-independent by design).
- **Data-viz semantic tints** — `GlassTypeComparisonView.xaml`,
  `ServiceProfitabilityView.xaml`, `SheetProfitabilityView.xaml`
  (profit = green / loss = red / utilisation legend; universal semantics,
  not UI chrome).
- **Comment-only hex** — `PrintPreviewView.xaml` ("Paper stays #FFFFFF"),
  `WizardStyles.xaml` (documentation comments).
- **Brand splash screen** — `SplashView.xaml` uses its own local resource
  keys (`SplashPillBg`, `SplashTrackBg`, gradient stops) rendered before the
  theme engine settles; deliberately self-contained.

No View XAML hard-codes a literal `FontFamily=` (0 hits) — typography is
centralised. None of the 8 files were edited: forcing them onto theme tokens
would change the splash/gradient/data-viz visuals for no correctness gain and
with build-verification unavailable.

## 2. Decisions (documented as permitted)

1. **The 4 retained families:** Normal · Monet · iPhone 17 Pro Max (Crystal
   Fluid) · iOS 26.
2. **Companions chosen:** Normal (the mature cream default, light+dark) and
   Monet (accent-derived, light+dark). They are the two most distinct,
   fully-implemented non-iPhone families.
3. **Retired:** `Normal (Industrial)` — it duplicated Normal's role as the
   default and was the odd one out for a 4-style switch. Its const is kept
   (persisted configs can hold the string) but it now normalizes to Normal,
   the migrated default.

## 3. Changes

| File | Change |
|---|---|
| `Services/Theme/VisualThemeEngine.Part3.cs` | `Normalize`: `Ios26 => Ios26`; `NormalIndustrial` + legacy industrial names → `Normal` |
| `Services/Theme/VisualThemeEngine.Part1.cs` | `FamilyDisplayName(CrystalFluid)` → `"iPhone 17 Pro Max"`; mode-pair docs updated |
| `Services/Theme/BuiltinWallpaperScenes.cs` | `All` reduced to 4; Crystal Fluid scene label → `"Crystal Waters"` |
| `ViewModels/Settings/SettingsViewModel.ThemeWallpaper.cs` | `VisualThemeOptions` → 4 rows; fallback → `Normal` |
| `Services/AI/AIConfig/AppConfig.cs` | default `VisualTheme` → `"Normal"`; migration docs updated |
| `Services/AI/AIConfig/ConfigService.cs` | one-time migration now folds `Normal (Industrial)` → `Normal` |
| `App.StartupCore.cs` | startup fallback → `Normal` |
| `ViewModels/Settings/SettingsViewModel.Typography.cs` | factory reset → `Normal` |
| `Tests/Theme/ThemeRegistryParityTests.cs` | `AllFamilies` + mode-pair theory → the 4-family catalogue |
| `Tests/Agent/LiquidGlassThemeSourceTests.cs` | source assertions → 4-family catalogue, `Normal` default |
| `Tests/Agent/CrystalFluidSettingsBridgeTests.cs` | display-name assertion → `"iPhone 17 Pro Max"` |
| `Themes/Languages/*.xaml` (15) | helper strings updated (en-US rewritten; others token-swapped) |

## 4. Not done / deferred

- No .NET toolchain in the workspace — edits validated by static resource
  reading + a Python catalogue-parity check; tests updated but not executed.
- The QuestPDF `conflicting size constraints` export investigation is a
  separate workstream (see `PdfExportEngine` `EnableDebugging` flag).
