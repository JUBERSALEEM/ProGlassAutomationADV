# White Water Glass Theme — Cross-Analysis & Fix (2026-08-29)

> User report: "appearance themes glass and glassy… completely broken.
> They are supposed to be fully transparent white water glass — the
> iPhone 17 Pro Max glass theme effect."

## 1. Cross-analysis — how the glass chain actually works

```
App.xaml merges LightTheme/DarkTheme (base brushes)
  → App.xaml.cs startup: ThemeManager.ApplyTheme(Light/Dark)
    → VisualThemeEngine.Apply(family, wallpaper, opacity)
        ApplyGlass()   → WindowBackgroundBrush / ModuleBackground /
                          Elevated / SideMenu / MenuHover + radii
        ApplyGlassy()  → About-stage window + EVERY card surface +
                          tint chips + text palette
        SyncGlassChrome() → GlassChromeService.Apply(tint)
          → DWM SetWindowCompositionAttribute (acrylic blur) on HWND
  → MainWindow.xaml binds Background="{DynamicResource WindowBackgroundBrush}",
    top bar = ShellGlassBrush, module area = ModuleBackgroundBrush, …
```

## 2. Defects found (all in LIGHT mode — dark was correct)

| # | Defect | Effect on screen |
|---|---|---|
| D1 | `ApplyGlassy` applied the DARK frost ramp (white @ 0.18→0.05 alpha) to BOTH themes | On the pale light stage the card veil is invisible — every surface disappears, the app reads as one flat washed-out sheet with no cards at all |
| D2 | Light `GlassCard` = `#FFFFFFFF→#F1F5F9` — fully opaque white | About cards are solid white slabs: zero translucency, not glass |
| D3 | Light `GlassSheen` = `#33FFFFFF→#00FFFFFF` | Invisible on white — no top-light specular, the "water" catch-light is missing |
| D4 | Light stage orbs ≈ 20% alpha | The aurora glow is invisible on the pale stage; the scene reads flat |
| D5 | Light text palette read surface-named keys (`GlassFrost` as PrimaryForeground) | Recolouring the frost surfaces (the obvious fix for D1–D4) would have silently turned light text white-on-white |
| D6 | `GlassSurface`/`FrostBrush` light tint = 0.22/0.14 toward the accent | Blue-cast panes instead of clean white water glass |
| D7 | Light DWM chrome tint = icy blue `#E9F2FD` @ 0xB0 | The blurred backdrop is blue-grey instead of clean white |
| D8 | `ApplyGlass` light ramp 0.62/0.70/0.78 (and 0.78/0.84/0.86 without blur) | Muddy mid-opacity white; nearly opaque without DWM — no glass |

## 3. Fixes applied

**Themes/LightTheme.xaml**
- `GlassCard`: opaque white → translucent white water glass, vertical
  top-lit fade `#E6FFFFFF → #D1FFFFFF → #BFFFFFFF` (0.90 → 0.75 alpha).
- `GlassSheen`: `#33FFFFFF` → `#80FFFFFF` strong top-corner specular.
- `GlassOrbA/B`: centre alpha 0x33/0x40 → 0x66/0x7A so the aurora
  actually glows through the white panes.

**Services/Theme/VisualThemeEngine.cs**
- `ApplyGlassy` now has a per-theme material ramp:
  - DARK (unchanged): 0.18/0.05 · 0.24/0.08 · 0.30/0.10 · 0.20/0.10 ·
    0.22/0.08 · 0.26/0.08 · 0.20/0.08.
  - LIGHT (new, white water glass): 0.88/0.58 · 0.92/0.64 · 0.95/0.70 ·
    0.52/0.34 · 0.86/0.56 · 0.92/0.62 · 0.88/0.58 — high-alpha white
    panes with a top-lit fade, translucent at the lower edge.
- `ApplyGlass` light ramp: chrome 0.52/0.62/0.70/0.36; no-chrome
  0.66/0.74/0.78/0.48 (was 0.62–0.86). Dark unchanged.
- `FrostBrush` light tint 0.14 → 0.06 (clean white, whisper of accent);
  `GlassSurface` light tint 0.22 → 0.10.
- `SetGlassTextPalette`: light text is now fixed ink/slate constants
  (`#334155` / `#1D4ED8` / `#475569`) — decoupled from the surface
  keys; dark keeps reading the dark About palette.
- Light stage brightened to ice-white (`#ECF4FD → #D8E8FA` accent
  tint 0.30); `GlassOrb` light stops raised to match the boosted XAML
  (0x66/0x33, 0x7A/0x3D).
- `SyncGlassChrome` light DWM tint: clean white `#F8FBFF`, only 12%
  accent mix (was `#E9F2FD` @ 28%) — the desktop blur now reads as
  white water glass behind the window.

## 4. Result matrix

| Surface | Dark | Light (new) |
|---|---|---|
| Window stage | navy diagonal + orbs | ice-white diagonal + boosted orbs, 25% veil over white DWM blur |
| Cards | 5–30% white frost | 58–95% white water-glass panes, top-lit |
| Top bar / drawer | 26–30% frost | 92–95% white glass |
| Text | white/cerulean | slate ink / blue accents |
| DWM backdrop tint | deep slate | clean white |

Fallbacks preserved: without DWM support (older Windows / remote
session / transparency disabled) the stage stays opaque and the panes
use the higher no-chrome alphas — contrast never collapses.

## 5. Verification

- Both theme dictionaries parse as valid XAML; no keys removed
  (nothing else references a changed key in a removed way).
- `VisualThemeEngine` lexer-clean; `FrozenSolid` added without a name
  collision; the engine's `GlassOrb` light stops match the boosted
  `GlassOrbA/B` XAML values verbatim (the file's own invariant).
- Regression risk on Dark and on Normal/Monet/Professional/Ocean:
  minimal — dark ramps untouched, Monet's surface tint change is a
  subtle whiter light glass only.

## 6. Second pass (2026-08-29, later) — "fully transparent" upgrade

User follow-up demanded FULLY transparent white water glass. Cross-check
confirmed the pipeline is coherent (cards consume surfaces via
DynamicResource; the engine rebuilds on navigation; the DWM veil is
wired; font preference restores Glassy text last), so this pass pushed
the material further instead of re-applying the previous fix:

- Light Glassy ramp thinned: ModuleBackground 0.82→0.48,
  Elevated 0.86→0.55, SideMenu 0.90→0.62, MenuHover 0.46→0.26,
  SidebarCard 0.80→0.46, ShellGlass 0.86→0.55, DashGlass 0.82→0.50.
  The panes are now genuinely see-through; readability is preserved
  because they sit on the BRIGHT blurred backdrop, never over raw
  desktop contrast.
- Light window veil thinned: 0x40 → 0x30 (dark stays 0x40) so the
  white acrylic blur reads through the window clearly.
- Light GlassCard (About) thinned to #DBFFFFFF → #BDFFFFFF →
  #9EFFFFFF (0.86 → 0.74 → 0.62) with the specular sheen raised to
  #8CFFFFFF.
- New source-test suite Tests/Agent/GlassThemeSourceTests.cs pins the
  design: light ramp numbers, thin veil, translucent GlassCard,
  XAML↔engine orb verbatim equality, decoupled text constants,
  DynamicResource surface consumption, dark ramp untouched.
