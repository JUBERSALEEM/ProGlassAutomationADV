# Fluent / Acrylic — third registered theme (Phase 01 / step1)

Native WPF. Engines stay separate: Bento stays a flat `Border`, Clay stays `ClaySurface`. No Win2D, no extra NuGet, no CSS `backdrop-filter`. No Fluent customization studio.

## Standard (Fluent 2)

- **Mica-like content layer**: opaque cards on the window canvas (`ShowTint=False`)
- **In-app acrylic**: tint + luminosity + stroke on transient chrome (menus, tooltips, plates)
- **Reveal**: hover brightens the top edge and stroke
- **Smoke**: `OverlayBrush` for modal dim
- Flyouts carry an unnamed drop shadow (never a named `DropShadowEffect` trigger target)

## Engine

`FluentSurface` paints those layers. Shared studio card rounding only. Hue extras stay in `ThemeFluent.*.Extras.xaml`.

## Completeness

47 contract roles. Windows pixels **0%**.
