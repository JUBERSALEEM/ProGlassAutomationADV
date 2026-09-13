# Frosted module cards

## The bug (production + optimization)

KPI / panel cards used the **same opaque white** as the page (`SurfacePrimaryBrush` or `WindowBackgroundBrush` at opacity 1). Text sat on an invisible rectangle. Wallpaper never showed because the **root Grid** was also opaque.

## The rule

| Layer | Token / style | Blur? |
|---|---|---|
| Wallpaper behind content | `ModuleWallpaperBrush` on MainWindow | **Yes** — `ModuleSkinBlurEffect` |
| Page / tab / sidebar host | `Transparent` | No |
| Main module card | `SurfacePrimaryStyle` / `ModuleListShellStyle` | No (text stays sharp) |
| KPI / filter / nested | `SurfaceSecondaryStyle` | No |
| Chip / fact | `SurfaceDetailStyle` | No |

WPF `BlurEffect` on a `Border` **blurs the Border’s children**. So blur lives **only** on the wallpaper layer.

## Settings → Appearance → “Frosted module cards”

| Control | Persist | Apply |
|---|---|---|
| Intensity 1–100 | `Appearance.CardBlurIntensity` (default **28**) | `BlurEffect.Radius = intensity × 0.36` on wallpaper |
| Colour plate | `Appearance.CardFrostTint` (`#RRGGBB` or empty) | Mix into `Surface.*.BackgroundBrush` |
| Auto | empty tint | Theme fill only |
| Glass / Transparent material | opacity sliders | Existing `SurfacePrimaryOpacity` (default **0.82**) |
| Solid material | — | Blur off, opacities 1 |

Reduced motion → blur off.

## Shared styles to consume (do not fork)

- `SurfacePrimaryStyle`, `SurfaceSecondaryStyle`, `SurfaceDetailStyle`
- `ModuleListShellStyle` (based on Primary)
- `ProGlassCard` / `ProGlassKpiCard` in `ProGlassMaterials.xaml`
- Optimization: `OptKpiCard` / `OptPanelCard` **BasedOn** those surfaces (no private `WindowBackgroundBrush`)
- Shell drawer: `ShellLiquidBrush`; sub-menu card uses `Surface.Primary.BackgroundBrush`

## Anti-patterns

- Root `Grid Background="{DynamicResource SurfacePrimaryBrush}"`
- `OptKpiCard` background = `WindowBackgroundBrush`
- Nested white `Border` with no border when parent is already white
- Per-view `BlurEffect` copies
