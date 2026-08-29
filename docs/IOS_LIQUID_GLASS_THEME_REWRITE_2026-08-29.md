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

---

## Completion sweep — 2026-08-29 (single follow-up commit)

A second full-repo audit (run as if the rewrite never happened) found the
old family still half-entrenched under the new one, plus one user-facing
leftover. All of it is resolved in ONE commit — no incremental patching.

### What the audit found

1. **Appearance-page wording** — the Font Color tile caption still said
   "(Glassy uses the About palette)" — visible to the user in Settings.
2. **26 old `Glass*` resource tokens** still carried the new family in
   BOTH theme dictionaries, the engine, ThemeManager, the About pages,
   login, splash, dashboard styles and the TrendChart code-behind.
3. **GlassChromeService** — the DWM accent-policy service that produced
   the original "full fade white" bug was still attached to the window,
   and `SyncGlassChrome` still applied a translucent tint for the iOS
   family. Dead residue of the deleted veil path.
4. **Old-family names on the new control** — `GlassSurface` enum,
   `GlassIntensity` DP; engine helpers `GlassSurface`, `SolidGlass`,
   `HueGlass`, `AccentGlass`, `FrozenGlassText`; the `glassyActive`
   variable in ThemeManager; `AuthGlassStyles.xaml`; splash keys
   `GlassPlate`/`GlassGlow`; `GlassComboBoxStyle`/`GlassDatePickerStyle`/
   `GlassFieldLabelStyle` in FilterStep.xaml; the test file/class name.

### What this sweep did

| Area | Change |
|---|---|
| UI wording | Appearance caption now: "iOS Liquid Glass uses the About palette … over frosted surfaces" — zero legacy family text on the page |
| Tokens | Full `Liquid*` surface: `GlassIce→LiquidIce`, `GlassFrost→LiquidFrost`, `GlassCerulean→LiquidCerulean`, `GlassSnow→LiquidSnow`, `GlassAccent(Deep)→LiquidAccent(Deep)`, `GlassMuted→LiquidMuted`, `GlassCard→LiquidCard`, `GlassSheen→LiquidSheen`, `GlassOrbA/B→LiquidOrbA/B`, `DashGlass*→DashLiquid*`, `ShellGlass*→ShellLiquid*`, `GlassEdge/InnerHighlight/Reflection/Control*/Focus/Selection/PrimaryButton/Overlay/ShadowColor→Liquid*` |
| DWM machinery | **`GlassChromeService.cs` DELETED** (class, file, `MainWindow.Attach`, engine `SyncGlassChrome`/`ReapplyWindowChrome` and all 3 call sites). No OS-backdrop code path exists — the white-fade failure class is structurally impossible |
| Control API | `GlassSurface→LiquidSurface`, `GlassIntensity→LiquidIntensity` (LiquidGlassCard + LiquidGlassStyles triggers) |
| Engine helpers | `GlassSurface→FrostSurface`, `SolidGlass→SolidFrost`, `HueGlass→HueFrost`, `AccentGlass→AccentCrystal`, `FrozenGlassText→FrozenLiquidText` |
| Files | `AuthGlassStyles.xaml→AuthSurfaceStyles.xaml` (4 merge refs updated); `GlassThemeSourceTests.cs→LiquidGlassThemeSourceTests.cs` |
| Login keys | `GlassCard` (local override), `DangerOnGlass→DangerOnLiquid`, `WarningOnGlass→WarningOnLiquid` |
| Splash | `GlassPlate→SplashPlate`, `GlassGlow→SplashGlow`; glassmorphism comments → crystal |
| FilterStep | `Glass*Style` keys → `Liquid*Style` |
| Comments/docs | Every stale "Glass/Glassy family" reference rewired; the ONLY remaining legacy-name mentions are the engine's `Normalize` migration map, the engine header migration note, and the tests that pin the removal |

### Verification

- Repo-wide word-boundary scan: **0** occurrences of any of the 40 legacy
  identifiers (tokens, DP, enum, helpers, service, file names).
- **0** `x:Key="…Glass…"` outside the new `LiquidGlass*` family; **0**
  non-Liquid `{Static/DynamicResource …Glass…}` references.
- Liquid token surface identical across LightTheme/DarkTheme (29 keys each);
  every engine-written Liquid key is defined in the themes; every
  DynamicResource in LiquidGlassStyles.xaml resolves.
- All changed XAML parses; all changed C# balances; all existing test
  anchors (hex stops, ramps, decoupling, defaults, migration map) intact.
- Tests: `Tests/Agent/LiquidGlassThemeSourceTests.cs` now pins **17 facts**
  (was 12) — including no-glass-prefixed-keys, no-legacy-identifiers,
  no-legacy-wording, and light/dark token-surface equality.

---

## Deep runtime analysis — 2026-08-29 (all-themes-broken report)

A second senior pass after the user reported EVERY theme broken and the UI
stuck. Method: every resource key, merge scope, event chain and startup
order was traced end-to-end; the resource system was proven consistent
(0 missing StaticResource/DynamicResource keys, 0 duplicate dictionary
keys, identical Liquid token surface across Light/Dark). The runtime
defects that remained were in WINDOW policy and APPLICATION ORDER —
invisible to XAML-level checks. All six are fixed in one commit.

### Root causes fixed

1. **Splash was a layered window** (`AllowsTransparency="True"` + `WindowStyle="None"` + Maximized + Topmost). Layered windows are software-rendered on Windows 11 and this codebase's own comments document repeated "phantom splash" / failed fade-foreground failures from it — the same failure class as the original white sheet. The splash content is fully opaque by design; the flag is removed. `SplashView.xaml`.
2. **Theme-swap vacuum.** `ThemeManager.ApplyTheme` removed the active theme dictionary BEFORE adding the replacement: a gap where every DynamicResource bound to a theme key resolves to nothing, and a permanent themeless scope if the new dictionary load throws ("all themes broken"). Now: load → ADD → remove-old → then report the new mode; a failed load keeps the old theme and logs the failure. `ThemeManager.cs`.
3. **Accent re-derive clobbered the iOS family's tints.** `ApplyAccent` (runs after the engine on every family switch) repainted `TintBlueBrush`/`TintIndigoBrush` opaque over the family's see-through hue frost. Guarded exactly like the existing dashboard-surface guard. `ThemeManager.cs`.
4. **`GetThemeColor` accepted only SolidColorBrush** — now also accepts `Color` entries (switch expression). `ThemeManager.cs`.
5. **No last-line integrity guarantee.** The engine now runs `GuaranteeCriticalResources` in a `finally` after every `Apply`/`ApplyModule`: it verifies each critical key (window, surfaces, text, outlines, accent, corners, tints, liquid tokens) against the EFFECTIVE application scope and injects an opaque, theme-correct fallback — while logging the missing key BY NAME. Any machine-specific residual gap is now self-healed AND diagnosable from the log. `VisualThemeEngine.cs`.
6. **Reset order.** `ResetAppearanceToDefaults` applied the default accent before the default family, causing the AccentChanged event to re-apply the stale previous family once. Family now applies first. `SettingsViewModel.cs`.

### Verification

- XAML parse + C# structural checks on every touched file
- 0 legacy identifiers, 0 unresolved resource references repo-wide
- New pins: splash must not be layered; theme swap adds-before-removes;
  tint guard precedes the TintBlueBrush write; integrity pass present in
  both Apply and ApplyModule (21 facts total in
  `LiquidGlassThemeSourceTests.cs`)

### If anything still looks broken on a specific machine

The integrity pass logs every injected fallback as
"Critical theme resource missing — injected fallback: <key>".
The log file (AppLogService output / `logs\` folder, plus
`logs\crash-latest.txt` for unhandled exceptions) now names the exact
resource or exception — paste those lines and the root cause is
immediately identifiable.
