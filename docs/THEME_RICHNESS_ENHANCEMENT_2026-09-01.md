# Theme System — Senior Richness & Contrast Pass (2026-09-01)

> Directive: "pages are dull — enhance ALL themes; iOS Liquid Glass and
> Ultra Clear need richer, deeper frosted glass with readable text on
> every card; frosted glass more saturated; Normal should be pure cream
> white like the latest SaaS UI; the industrial look needs more contrast
> and pure colour; read all the code, fix bugs, don't break anything."

## 1. Architecture — how the theme system actually flows

```
App.xaml
 ├─ Themes/LightTheme.xaml | DarkTheme.xaml   (base tokens + per-mode glass)
 ├─ Themes/Styles/LiquidGlassDesign.xaml      (templates ONLY — no colour tokens)
 └─ …shared style dictionaries (Card, Shell, PiIndustrial, LiquidGlassStyles…)
App.xaml.cs startup
 ├─ ThemeManager.ApplyTheme(Light|Dark)  → swaps dictionary + PUBLISHES the
 │    runtime "liquid reference" palette (AppBackgroundBrush, GlassSurface*,
 │    GlassBorder*, GlassGradientIdle, Text*Brush)
 ├─ VisualThemeEngine.Apply(family, …)   → ClearOverrides + family scene
 │    (Normal / Monet / Professional / iOS Liquid Glass / Ultra Transparent /
 │     Ocean) + wallpaper + font preference
 └─ ThemeManager.ApplyAccent(preset)     → accent family + Outline/Border/
      Divider keylines (OWNED here, not by families)
MainWindow → Background="{DynamicResource WindowBackgroundBrush}"
Cards/KPI tiles → ModuleBackgroundBrush / GlassSurfaceElevated /
                  GlassBorderSoft / OutlineBrush (all DynamicResource)
```

Key insight: **everything the user sees is token-driven.** No view file
pins a surface colour — so one token pass upgrades every page (PI
wizard, Job Profitability, dashboards, settings, lists) at once.

## 2. Senior analysis — issues found

| # | Issue | Where | Severity | Resolution |
|---|---|---|---|---|
| 1 | **Dull Normal (Light)**: window was an icy blue-white (`#D7F0FF` family), surfaces almost invisible (glass fills 3–8% alpha) — flat, washed-out pages | LightTheme §12/§5 | High | Pure cream-white SaaS canvas (`#F6F1E6 → #FEFDFA → #F1EBDE`), warm ink (`#1C1917`), warm borders, richer glass fills (7–13% alpha) |
| 2 | **Runtime override fought the theme**: `PublishLiquidReference` repainted `AppBackgroundBrush` icy blue on EVERY Light/Dark swap, so Normal could never stay cream | ThemeManager | High | Light reference now cream (`#F8F4EB/#FEFCF8/#F2ECDF`), synced with the theme file |
| 3 | **iOS Liquid Glass frost not saturated**: light frost carried a 6% accent "whisper", surfaces 84–94% white → read as plain white sheets, not frosted glass | Engine `FrostBrush` + ramps | High | Accent kiss 0.16 light / 0.32 dark; ramps denser (light 0.88→0.60 … modules, dark 0.26→0.10 …) — richer, deeper frost |
| 4 | **Ultra Clear (Ultra Transparent) text readability**: cards were 100% transparent over the wallpaper (crystal edge 24% white only) — text could vanish, cards invisible on busy wallpapers | Engine `ApplyUltraTransparent` | High | 6–12% card frost added; crystal edge 24% → 36% white; hover/pressed/divider strengthened; richer theme Glass* fills flow through |
| 5 | **BUG — green tint was BLUE**: `TintGreenColor` was `#EFF6FF` (sky blue) light / `#1E2B44` (navy) dark — success chips/cards rendered blue | LightTheme §2, DarkTheme §2 | Critical (visible) | `#E8F6EC` (emerald) / `#14301F` (deep emerald); dark blue tint now a real blue `#16324C` |
| 6 | **Industrial (Professional) too pale**: near-white slate window/surfaces — low contrast, low saturation | Engine `ApplyProfessional` | Medium | Saturated steel-blue window (`#ECF2F9 → #D9E5F2`, dark `#0A1326 → #16233E`), stronger cool surfaces, crisper hover |
| 7 | **Monet comment/code drift**: doc said light tint 0.22, code used 0.10 | Engine `FrostSurface` | Low | Code 0.16 (visible accent adaptation), comment fixed |
| 8 | **Ocean dull**: pale teal | Engine `ApplyOcean` | Low | Deeper teal window/surfaces |
| 9 | **Stale/incorrect test pins** (still failing a Windows run): MainWindow pins `AppBackgroundBrush` (binds `WindowBackgroundBrush`); `#DBFFFFFF/#BDFFFFFF/#9EFFFFFF` never existed; token loop wrongly required colour tokens inside the templates-only `LiquidGlassDesign.xaml` | LiquidGlassThemeSourceTests | Medium | All re-pinned to the real design; test intent preserved |
| 10 | **Dark secondary text** (`#94A3B8`) blends against the dark frost — low contrast | DarkTheme §1 | Medium | `#A9B8CC` secondary, `#7C8CA3` muted |

## 3. Value matrix (per family, after this pass)

### Light base (Normal) — "pure cream white, latest SaaS"
| Token | Before | After |
|---|---|---|
| WindowBackground | `#D7F0FF…#DCEEFA` icy | `#F6F1E6 → #FBF8F1 → #FEFDFA → #F7F2E8 → #F1EBDE` cream |
| Ink / secondary / muted | `#020617 / #475569 / #64748B` | `#1C1917 / #57534E / #78716C` (warm) |
| Borders / outlines | `#E5E7EB / #CBD5E1` | `#EBE3D6 / #D8D0C2` (warm, still soft) |
| Success/Warning/Danger | `#10B981 / #F59E0B / #EF4444` | `#059669 / #D97706 / #DC2626` (contrast on cream) |

### Glass material tokens (both themes — richer frost, crisper edges)
| Token | Light before → after | Dark before → after |
|---|---|---|
| GlassSurfaceUltra | `08` → `0E` | `08` → `0E` |
| GlassSurfaceStandard | `0C` → `17` | `0C` → `12` |
| GlassSurfaceElevated | `14` → `22` | `14` → `1E` |
| GlassBorderSoft | `1A` → `2E` | `14` → `22` |
| GlassBorderPrimary | `33` → `40` | `1F` → `2E` |
| GlassBorderBright | `4D` → `5C` | `2B` → `3C` |
| LiquidGlassBrush ramp | `D9/C4/F2/B3/87/80/8C` → `E4/CE/F6/BC/92/88/96` | `26/20/30/1C/12/0D/11` → `34/2C/40/28/1C/16/1C` |
| LiquidGlassFrostedBrush | `E8/D6/ED/C8/BF/CC` → `F2/E4/F6/D6/CE/D8` | `3D/30/40/26/1A/1F` → `4E/3E/50/32/24/2A` |

### iOS Liquid Glass (engine)
- Frost saturation: light 0.06 → **0.16** accent kiss; dark 0.22 → **0.32**.
- Light ramp: Module `0.84/0.54` → `0.88/0.60`, Elevated `0.90/0.62` → `0.93/0.68`,
  SideMenu `0.94/0.72` → `0.96/0.76`, Hover `0.50/0.30` → `0.56/0.36`,
  Shell `0.88/0.58` → `0.91/0.63`, DashGlass `0.84/0.56` → `0.87/0.60`.
- Dark ramp: Module `0.20/0.06` → `0.26/0.10`, Elevated `0.26/0.09` → `0.32/0.13`,
  SideMenu `0.32/0.11` → `0.38/0.15`, Hover `0.22/0.11` → `0.28/0.16`,
  Shell `0.28/0.09` → `0.34/0.13`, DashGlass `0.22/0.09` → `0.28/0.13`.
- Status tints HueFrost `0.30/0.14` → `0.36/0.18` (more saturated see-through hue).
- Control states: hover `0.30/0.60` → `0.36/0.66`, pressed `0.22/0.70` → `0.28/0.76`,
  disabled `0.14/0.35` → `0.18/0.42`.
- Opaque aurora stage, orb palette, watery ramp (`dip`/`meniscus`) — **unchanged**
  (the 2026-08-29 rewrite pins stay valid).

### Ultra Transparent (engine)
- Card frost: `ModuleBackgroundBrush` = `FrostFill(accent, 0.14/0.07 dark, 0.12/0.06 light)`.
- Crystal edge: `0x3D` → `0x5C` (36% white); gradient `0x50/0x3D/0x30` → `0x6E/0x5C/0x4A`.
- Hover `0x1A` → `0x24`, pressed `0x26` → `0x2E`, disabled `0x08` → `0x0C`,
  divider `0x20` → `0x2C`.

### Professional (industrial) / Monet / Ocean
- Professional: steel-blue saturated window + cooler/stronger surfaces + deeper hover.
- Monet: accent saturation caps raised (light 0.70→0.78 / 0.62→0.70; dark 0.42→0.46 / 0.38→0.44), light frost tint 0.10 → 0.16.
- Ocean: deeper teal window/surfaces.

## 4. Files changed

- `Themes/LightTheme.xaml` — cream SaaS canvas, warm ink, status contrast,
  richer glass tokens, green-tint bug fix.
- `Themes/DarkTheme.xaml` — richer deep frost, higher text contrast,
  green/blue tint bug fix.
- `Services/Theme/VisualThemeEngine.cs` — LiquidGlass saturation/depth,
  UltraTransparent readability, Professional/Monet/Ocean contrast,
  FrostSurface comment drift fix.
- `Services/ThemeManager.cs` — light reference palette synced to cream.
- `Tests/Agent/LiquidGlassThemeSourceTests.cs` — re-pins + new facts;
  stale pins corrected (MainWindow `WindowBackgroundBrush`, token loop
  moved to the theme dictionaries, card translucency re-pinned).
- `docs/THEME_RICHNESS_ENHANCEMENT_2026-09-01.md` — this document.

## 5. Verification

Static (no .NET SDK in this workspace):
- Both theme files + App.xaml parse as well-formed XML.
- VisualThemeEngine.cs / ThemeManager.cs brace-balanced, script-applied
  with strict "must-match-exactly" transforms (no silent misses).
- Every edited token is consumed via DynamicResource; no style
  dictionary, view or control file needed changes — the upgrade is
  purely token-level, so nothing that binds an existing key can break.
- No resource key added/removed → the Light/Dark liquid-token key-set
  equality test stays valid.

Required on the user's Windows build:
- `dotnet test` (LiquidGlassThemeSourceTests re-pinned to the new values).
- Visual smoke: each family in Light + Dark; wallpaper set; KPI tiles,
  dashboards, wizard steps and dialogs; verify text contrast in the
  Ultra Transparent family over a bright wallpaper.
