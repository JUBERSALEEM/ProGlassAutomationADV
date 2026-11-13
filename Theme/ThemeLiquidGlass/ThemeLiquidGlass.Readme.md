# Liquid Glass — fifth theme (Phase 01 / step1)

Native WPF reading of Apple Liquid Glass (iOS 26 / macOS Tahoe Control Center). Wallpaper is a coded Tahoe sky/water scene. Glass is rank-based: milky cards, clearer chips. Shell chrome is glass **only while Liquid Glass is active**. Not Fluent acrylic, not Clay, not CSS, not desktop capture, not a PNG wallpaper.

## Showcase canvas

`LiquidWallpaper.Create` paints a smooth Light sky in the `#62B4E8` family (one small in-bounds sun) or a smooth night (one small moon). Blooms never clip the drawing — clipped ellipses read as glass slabs. `ThemedWindow` uses that brush. Dashboard: four KPI tiles with mini graphs, a three-column trends panel (area, lines, stacked bars, spark), donut + channels on the right, then activity / bars / status / spark. Graphs stay in the layout. Clay plumpness is unchanged.

## Standard

- **Card** frost is the CC pane. **Control** is lighter. **Chip** is almost clear.
- **Hero** uses Light `HeroBackground` (`#0A3658` / `#085872`) as glass so `HeroForeground` still reads. Dark keeps a night veil. Light extras are not a copy of Dark.
- **Shell** stays `SurfacePrimary` on Light (`TextPrimary` `#071422`). Transparent chrome is Dark Liquid only. Other themes keep opaque SurfacePrimary.
- **Edge** is a white hairline plus a 1px inset specular lip.
- **Lift** is an unnamed `DropShadowEffect` on a near-transparent caster — never `x:Name`.
- **Inputs** stay `LiquidWellBrush`. Charts stay CPU.

True Metal refraction of the wallpaper through the pane is **not** in this build.

## Completeness

47 contract roles. Fifth catalog entry. Windows pixels **0%**. L8 visual sign-off is not this sandbox.
