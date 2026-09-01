# Theme System — Section Adoption + Cream/SaaS + UHD Pass (2026-09-01)

**Scope:** full senior audit of every visual family after the user reported:
1. iOS Liquid Glass — Settings sidebar not properly visible / not transparent ("not getting the touch").
2. Main modules sidebar too transparent — module buttons hard to read over the stage.
3. Normal still reads BLUE — "there is some bluish theme… an industrial theme forcing it to blue".
4. Every theme still felt broken somewhere; sections did not visibly accept the active family.
5. Request: pure cream-white SaaS Normal, richer/deeper frost, UHD iOS quality, fix what's broken.

---

## Root causes found (read the whole system: themes, engine, ThemeManager, shell, sections)

| # | Root cause | Symptom | Fix |
|---|------------|---------|-----|
| 1 | **Engine only overrode Module/Elevated/SideMenu.** Settings section cards, wizard tiles, sidebars, info cards, forms and stat cards consume `GlassSurfaceBrush / GlassSurfaceUltra / Standard / Elevated / GlassUltraClearBrush / GlassBorderSoft…` — and those keys were never republished per family, so **every section kept the base-theme glass no matter which family was selected** ("sections don't accept the theme"). | Settings/wizards/dashboards looked identical in Normal/Monet/Professional/Ocean/iOS. | New `PublishSectionGlass()` mirrors the active family's material into the whole shared glass set, called by **all five** families; `ClearOverrides` restores base values on Normal. |
| 2 | **Settings rail was a 0.96-alpha white slab** on a light sky-ice stage — invisible as glass and not translucent (0.96/0.76 was *denser than the main drawer*). | "Settings sidebar not visible, not transparent, no glass touch." | Rail frost → 0.90/0.62 (stage reads through), + top catch-light (`ShellSheenBrush`), + glare band (`GlassTopReflection`), + stronger lift (shadow 16/0.12 → 20/0.20). |
| 3 | **Main shell sidebar too see-through** (0.91/0.63 light, 0.34/0.13 dark) and its submenu cards used the *base-theme* wash (52 % white), so module buttons lost contrast on the colourful stage. | Module buttons hard to read. | Shell frost → **0.95/0.80 light, 0.42/0.20 dark** (rich); SidebarCard 0.92/0.70; submenu cards now get the family's real frost via `PublishSectionGlass`. |
| 4 | **`MenuHoverBrush` was blue in both themes** (`#185FCDFF` / `#183B82F6`). It fills the top-bar icon buttons, nav chips, settings action buttons, sliders and accent chips — 68 usages. | The whole chrome read blue in **every** family. | Warm stone `#1457534E` light, warm charcoal `#203E3A34` dark. |
| 5 | **`ApplyAccent` derived the card keyline + Blue/Indigo tints from the accent hue in EVERY family** — with the default blue accent, Normal's cards/hover/tints were blue (the "industrial theme forcing blue" feel). | Normal canvas not pure cream. | In Normal the keyline = theme's warm hairline (`#D8D0C2` light / `#5C574C` dark) and tints = warm neutrals (`#F2EDE3/#EDE7DA`, dark `#232B34/#272F3A`). Accent still drives CTAs (AccentBrush family) and the colourful families still derive their own keylines. |
| 6 | **Blue-tinted shadows & glass lines**: `ShadowSoft/Elevated #1A3050`, `#1E293B`, `GlassShadowSoft #3C78A0` ×8, `GlassShadowActive #32B4FF` ×2, `DashCardShadow #1E3A5F`, `#0369A1`, card refraction `#E0E8FF`, water lines `#D7F0FF/#D0E0FF`, backdrops `#5FCDFF`, light liquid water ramp, dark navy glass surfaces, `TextSecondaryColor #991E2832` (cool slate), dark `OutlineBrush #526383`. | Blue cast on cards, popups, shadows and captions. | All neutralised to warm/white (values in tree; `git diff Themes/*.xaml`). |
| 7 | **Light liquid text palette was cool slate + BLUE secondary** (`#334155 / #1D4ED8 / #475569`), same for Ultra (slate). | Blue captions across the light iOS/Ultra UI. | Warm ink everywhere: `#1C1917 / #57534E / #78716C`. |
| 8 | **Engine `CriticalFallbacks` were still icy blue** (`#D7E5F9` window, `#334155` ink, `#C7D2E1` outline) — if any key failed to resolve on a machine, a BLUE canvas was injected into the app scope and *stayed* there. | Blue Normal on some machines. | Fallbacks now match the cream/warm palette. |
| 9 | **Ultra Transparent never reached the rail/dialogs**: SideMenu/Elevated were left at the base-theme fills (opaque cream in Light). | Ultra sections didn't go transparent; popups inconsistent. | Rail frost 0.16/0.08 (light 0.18/0.10 dark), elevated 0.22/0.12 (0.24/0.14 dark) + section glass set with pure crystal edges. |
| 10 | **Test suite was not green** (would fail on the Windows build): `Theme_dictionaries_define_no_glass_prefixed…` used `Contains("Glass")` — impossible with the adopted `GlassSurface*` design; orb-stops fact pinned hexes that no longer exist (about orbs are runtime accent-derived); `Surface_keys_are_consumed…` pinned a key no card file references; `Liquid_glass_styles_never_pin…` pinned `GlassSurfaceStandard` (styles use `GlassSurfaceElevated/GlassUltraClearBrush`); reference-adoption fact pinned `GlassGradientIdle/GlassTopReflection` in ButtonStyles (actual: `GlassTopReflectionGradient` + `GlassSurfaceBrush/Elevated`). | 6 broken/stale pins. | All re-pinned to the real relationships + 6 new facts pinning this pass. |

---

## Where each family now stands (Light / Dark)

| Family | Surfaces (ramp alphas shown as top/bottom frost) | Character |
|--------|--------------------------------------------------|-----------|
| **Normal** | Cream canvas `#F6F1E6→#FEFDFA→#F1EBDE`, warm stone surfaces, neutral hairline `#D8D0C2`, warm hover/tints, warm shadows. | Pure cream SaaS — zero blue cast; accent colour only on CTAs/active states. |
| **iOS Liquid Glass** | Light: module 0.90/0.64, cards 0.94/0.70, **shell 0.95/0.80**, rail 0.90/0.62, hover 0.62/0.42. Dark: 0.30/0.12 … **0.42/0.20**, hover 0.32/0.18. Saturated accent kiss (0.32/0.16), stronger meniscus edges (mid 0x66/0x2E, low 0x80/0x3C). | Rich deep frost; every section renders the family's real material. |
| **Ultra Transparent** | Card whisper 0.12/0.06 (0.14/0.07 dark), rail 0.16/0.08, elevated 0.22/0.12, crystal edges 36 % white. | Wallpaper shows through everything; text stays readable everywhere including rail + dialogs. |
| **Monet** | Accent-derived frosted surfaces **+ glass set published** (was missing → sections stayed base-theme). | Colour-adaptive everywhere. |
| **Professional (industrial)** | Steel-blue `#ECF2F9→#D9E5F2` / `#0A1326→#16233E`, cool surfaces **+ glass set published**. | Higher contrast + saturation; now reaches sections. |
| **Ocean** | Deep teal window/surfaces **+ glass set published**. | Deep teal corporate; reaches sections now. |

---

## Section adoption matrix (what actually consumed the family before → now)

| Consumer key | Family override before | Now |
|--------------|------------------------|-----|
| `GlassSurfaceBrush` (Settings cards, outlined/danger buttons, provider cards) | base theme only | family material |
| `GlassUltraClearBrush` (Task/stat cards) | base theme only | family material |
| `GlassSurfaceUltra/Standard/Elevated` (nav active, hover, info cards, toggles) | base theme only | family solids |
| `GlassBorderSoft/Primary/Bright` (card/input edges, focus) | base theme only | family-mixed crystal |
| `SideMenuBackgroundBrush` (settings rail) | base theme (LiquidGlass overrode) | family frost (incl. Ultra) |
| `ElevatedSurfaceBrush` (dialogs, popovers) | base theme | family frost (incl. Ultra) |
| MenuHover / tints / typography | base/blue | family + warm in Normal |

---

## Verification (static, no .NET SDK in this sandbox — Windows build still required)

- All 35 facts in `Tests/Agent/LiquidGlassThemeSourceTests.cs` re-verified programmatically against the real files (37 checks, 0 failures) — including 6 new facts for this pass.
- `git diff --check` clean; both theme XAMLs + App.xaml + SettingsView + design/style dictionaries parse as XML; engine/ThemeManager/tests brace-balanced.
- No resource key added or removed (Light/Dark key-set parity untouched); `LiquidGlassDesign.xaml` stays templates-only; DWM service stays deleted; legacy `Glass`/`Glassy` families stay gone.
- **On Windows:** build + `dotnet test`, then visual smoke: Normal (blue gone?), iOS Light/Dark (settings rail glassy + visible, main sidebar rich), Ultra over a busy wallpaper (text everywhere readable), Monet/Professional/Ocean sections, Login/About pages.

## What did NOT change
- Windows/Light stage orbs & caustics (self-contained opaque stage, engine constants — pinned).
- Accent presets & CTA accent colouring (Glass Blue remains the default accent; Normal now only uses it on CTAs/active chrome, not on neutral surfaces).
- LiquidCard/About palette stays brand/accent-tinted (About + Login surfaces only).
- The `GlassWaterBrush` line in card templates is now neutral white (was the only deliberately-kept blue from the earlier pass — the blue cast complaint overrides that).

---

## Deep-audit round 2 — the remaining root causes (same day)

After the section-adoption pass, a second debug sweep found three more
real defects behind the user's exact complaints and fixed them at the
token/style level:

### 1) Settings text was painted with a BORDER brush (invisible text)

`Views/Settings/SettingsSectionStyles.xaml` set `Foreground` to
`{DynamicResource GlassBorderSoft}` in **7 styles** — a border token
(white @ 18–40 % alpha), so headings, tile icons, hero values, group
headers and the provider "SELECT" label rendered white-on-white in
Light across EVERY family:

- `InfoCardTitleStyle`, `InfoGlyphTextStyle`, `SettingsGroupHeaderTextStyle`,
  `SettingsTileIconStyle`, `SettingsTileHeroStyle` → `TextPrimaryBrush` (real ink).
- `ProviderSelectButtonStyle` (SELECT label) → `AccentBrush`.
- `DownloadProgressBarStyle` (fill) → `AccentBrush`.

### 2) The "by-default bluish system theme" — runtime text ink leak

`ThemeManager.PublishLiquidReference` writes `TextPrimaryBrush` /
`TextSecondaryBrush` into the **app scope on every Light/Dark swap**
(the app scope wins over the merged theme dictionaries), and
`VisualThemeEngine.ManagedKeys` did **not** contain those two keys —
so `ClearOverrides` could never remove them. Result: even in Normal,
light secondary text stayed cool slate `#991E2832` and permanently
masked the theme file's warm `#9957534E`.

Fixes:
- `PublishLiquidReference` light secondary → `Color.FromArgb(0x99, 0x57, 0x53, 0x4E)`
  (warm stone, same as the Normal canvas; Dark keeps its designed cool-white).
- `ManagedKeys` += `TextPrimaryBrush`, `TextSecondaryBrush` → Normal's
  `ClearOverrides` now truly restores the theme dictionary ink.
- Custom-accent fallbacks in the engine were cool slate
  (`#475569` / `#64748B`) → warm stone (`#57534E` / `#78716C`).

### 3) Sidebar / nav "touch" was missing (normal + settings rail)

- **Normal drawer invisible:** Normal has no engine case, so the drawer's
  frost is the base theme's `ShellLiquidBrush` — which was ~5–8 % alpha
  in Light (`#14F6F1E6…`) and ~4 % in Dark (`#0C0F2847…`). Module buttons
  washed out over the content card. Both theme dictionaries now ship a
  readable frost (Light warm-cream `#E6F6F1E6 / #D9FFFCF5 / #E0F1EBDE`,
  Dark deep-slate `#D90F2847 / #D41E293B / #D90F2847`). The families
  still override this token at runtime.
- **Settings rail active pill invisible:** `SettingsNavButtonStyle`'s
  active state used `GlassSurfaceUltra` (~5 % alpha) — invisible on the
  frosted rail; `IsPressed` only dimmed the whole button (more
  transparent, not pressed). Now: active = `GlassSurfaceBrush` material
  ramp + `GlassBorderGradientCrystal` edge + accent bar; pressed =
  `TintIndigoBrush`; glyph + label light up `AccentBrush` (Bold) via a
  `Tag`-bound `DataTrigger` on the parent button.
- **Main shell module items:** active `SidebarNavItem` used the same
  `GlassSurfaceElevated` as hover — the open module never read as
  selected. Active module + active sub-item now use the full
  `GlassSurfaceBrush` ramp.

### 4) UHD crisp edges (iOS Liquid), token level only

`GlassRefractionEdge` (the ONLY live edge key — 6 consumers in
`LiquidGlassDesign.xaml`) was already pure white; the mid stop got
lifted for crispness: Light `#10FFFFFF` → `#1CFFFFFF`, Dark
`#08FFFFFF` → `#12FFFFFF`. Engine `CrystalEdge`/`HorizontalSheen` and
the `GlassBorderGradientCrystal` runtime override were inspected and
confirmed warm-white — not blue sources. `LiquidEdgeBrush` /
`LiquidGlassEdgeTop/BottomBrush` / `LiquidGlassRefractionBrush` have
zero live consumers (engine/relic tokens only; left untouched).

### Verification of round 2

- `python3 /tmp/theme_emu.py` → **PASS 219 / FAIL 0**.
- `tools/xaml-guard.py` clean (179 files); `tools/xaml-resource-lint.py`
  clean (7 benign pre-existing L2 corner-radius warnings).
- 23 new static checks (mirroring the 4 new XUnit facts) all pass;
  all touched XAML parses as XML.
- New pinned facts in `Tests/Agent/LiquidGlassThemeSourceTests.cs`:
  runtime warm ink + managed keys, no `GlassBorderSoft`-as-Foreground,
  rail touch states, Normal drawer frost + module material pills.
