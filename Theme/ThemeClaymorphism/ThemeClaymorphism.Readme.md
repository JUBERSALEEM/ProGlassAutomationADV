# Claymorphism — second registered theme (Phase 01 / step1)

Native WPF. Engines stay separate: not Bento plates, not Neumorph gray volume, not Fluent acrylic.

## Standard (2026)

Clay is a **floating inflated object**, not an extrusion:

- analytic quarter-round bevel (diffuse + specular) sampled onto DPI-aware nine-slice patches
- text is never inside a shadow Effect — `ClayChrome` is a sibling of the presenter
- one hue-matched outer drop plus a depth-0 ambient lift
- press inverts the bevel (inset well); hover lifts the drop
- Control / Chip use a compact bevel so buttons pop without a second card gutter

Ported from ExcelSticker Theme Studio (Coral sculpted + six reference styles).

## Presets

Rose Plush, Berry Puffy, Sage Soft, Night Berry, **Coral sculpted**, Clayhub, Puffy studio, Daily progress, Clayboard, Clayshop, Clayplay.

## Engine

`ClaySurface` hosts `ClayChrome`. `ClayLightingModel` / `ClayBevelRenderer` own the curve. Rank Chip / Control / Card. CheckBox track stays a 40×24 `Border` pill (not `ClaySurface`) so On is not a plump bead. `ClayCustomization` stays Clay-owned.

## Completeness

47 contract roles. Windows pixels **0%**.
