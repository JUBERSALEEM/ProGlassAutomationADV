# iOS Liquid Glass — Complete Theme-System Rewrite (2026-08-29)

Directive: remove "Glass" and "Glassy" from the appearance themes and
completely rewrite a NEW iOS-named theme that adapts to Light/Dark;
make the glass theme the default. Complete rewrite — not a patch.

## 1. Senior analysis — issues found and removed

| # | Issue | Severity | Action |
|---|---|---|---|
| 1 | **Two overlapping glass families** (`Glass` frosted-stage + `Glassy` About-stage) with different surface ramps, text palettes, keylines and window stages — confusing and internally inconsistent | Critical | Both deleted; ONE coherent family now exists |
| 2 | **Full-white fade**: both families veiled the window surface (alpha 0x30/0x40) expecting the DWM backdrop to show through an opaque WPF surface — it cannot. The veil alpha-blended against the white clear color (Win11 / transparency-off / bright desktop) | Critical | Stage is now always opaque and painted INSIDE the window |
| 3 | **Per-machine inconsistency**: the look depended on OS blur support, Windows version and settings — no single verifiable result | High | Self-contained scene: identical on every machine |
| 4 | **Not the default**: glass was an opt-in option buried in Settings | High | iOS Liquid Glass is the default on fresh installs AND on settings reset |
| 5 | **Legacy-config trap**: saved "Glass"/"Glassy" values had no migration path | Medium | `Normalize` maps both to the new family |
| 6 | Two text-palette strategies (surface-key-coupled in light, About keys in dark) — recoloring surfaces could white-out text | Medium | Per-mode palettes, decoupled from surface keys |
| 7 | chrome/no-chrome surface split doubled every ramp and diverged across machines | Medium | One ramp per mode |

## 2. The rewrite

### Engine (`Services/Theme/VisualThemeEngine.cs`)

- `public const string LiquidGlass = "iOS Liquid Glass";` replaces the
  `Glass` and `Glassy` constants (both deleted everywhere).
- `ApplyGlass` + `ApplyGlassy` deleted (~6,700 chars); ONE
  `ApplyLiquidGlass` builds the whole scene:
  1. **Window** — `BuildLiquidStageBrush(dark, accent)`: opaque
     aurora stage (deep navy + orbs in Dark, sky-ice `#D7E5F9` +
     accent-tinted `#AECBF0` + orbs in Light). No alpha veil, no
     `WithAlpha` on stage colors — the white-fade failure mode is
     structurally gone.
  2. **Surfaces** — one ramp per mode:
     Light (white water glass): Module 0.84→0.54, Elevated 0.90→0.62,
     SideMenu 0.94→0.72, Hover 0.50→0.30, SidebarCard 0.82→0.52,
     Shell 0.88→0.58, DashGlass 0.84→0.56.
     Dark (deep frost): 0.20→0.06, 0.26→0.09, 0.32→0.11, 0.22→0.11,
     0.24→0.09, 0.28→0.09, 0.22→0.09.
  3. **Dashboard ambient** — per-mode glow blobs.
  4. **Status tints** — hue preserved, fill made glass.
  5. **Typography** — `SetLiquidTextPalette`: About-page palette in
     Dark, fixed ink/slate in Light.
  6. **Shape/keyline** + **`ApplyLiquidGlassTokens`** — crystal edge,
     reflections, control states, selection/focus/primary glass, so
     the whole LiquidGlassStyles control system re-tints live.
- `Normalize()` maps legacy `"Glass"`/`"Glassy"` → `LiquidGlass`.
- `OnAccentChanged`, both family switches, `ApplyFontPreference`,
  `SyncGlassChrome`, `ThemeManager.ApplyAccent`'s dashboard-ownership
  guard — all switched to the single family.
- Renames for a clean namespace: `FrostFill`, `LiquidOrb`,
  `SetLiquidTextPalette`, `BuildLiquidStageBrush`.

### Appearance & defaults

- `SettingsViewModel.VisualThemeOptions`: **Normal / Monet /
  Professional / iOS Liquid Glass / Ocean** — Glass and Glassy gone.
- Defaults to the glass family everywhere:
  `AppConfig.Appearance.VisualTheme = "iOS Liquid Glass"`,
  `App.xaml.cs` fallback `?? "iOS Liquid Glass"`,
  `SettingsViewModel` getter fallback, and
  `ResetAppearanceToDefaults`.
- Settings hint text updated to describe the new family.

## 3. Light/Dark adaptation

One family, two intentional material sets — no inversion:
- **Light**: clear white water glass panes over a sky-ice aurora
  stage, slate ink text, subtle blue refraction edges.
- **Dark**: deep frost panes over a navy aurora stage, About-page
  white/cerulean text, controlled crystal highlights.
Switching base theme re-derives the entire scene through the existing
`ThemeManager.ApplyTheme` + `VisualThemeEngine.Apply` path.

## 4. Verification

- Engine lexer-clean; no `ApplyGlass`/`ApplyGlassy`/`GlassFill`/
  `GlassOrb`/`BuildGlassStageBrush`/`SetGlassTextPalette` remain.
- No `VisualThemeEngine.Glass/.Glassy` references remain anywhere
  (only the legacy `Normalize` string mapping).
- All XAML parses; no resource keys removed.
- `GlassThemeSourceTests` re-pinned (13 facts) incl. a new test:
  *Glass_and_glassy_are_removed_and_ios_liquid_glass_is_the_default_family*.
- DynamicResource-only tokens unchanged; LiquidGlassStyles.xaml
  untouched and fully compatible.
