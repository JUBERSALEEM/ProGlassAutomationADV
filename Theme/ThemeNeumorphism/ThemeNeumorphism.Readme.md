# Neumorphism — fourth registered theme (Phase 01 / step1)

Native WPF. Engines stay separate: not Clay hue-matched plump, not Fluent acrylic, not Bento hairline cards.

## Standard (2026)

Soft UI extrusion from a **shared mid-tone canvas**:

- page fill **and** surface fill are the same hex (`#E6EBEF` light, `#2A2F38` dark)
- dual outer glow (WPF light 125° / dark 305° = CSS 145deg), blur ≈ 2× distance
- convex **face fill** is a 145° canvas pair; wells invert it
- inset is **one** blurred 6px dual-tone ring clipped to Face
- press is inset only; hover is a stronger shadow, not a fill
- CSS spread is a 0-thickness ring (`NeumorphSpreadThickness`) plus caster grow
- motion follows ClientArea + Menu + ComboBox animation; live via `StaticPropertyChanged`; applied in code, not a frozen Storyboard
- `GetLayoutClip` is null so the surface does not clip its own glow
- StatusBadge stays `Border`; light glow wraps it in a Grid cell
- Progress `PART_Indicator` is a Border that hosts a raised canvas chip
- rims and near plates are not in the tree
- dark mode is a retune, not an invert
- card radius stays SharedTokens; Neu control/chip stay 12
- depth / blur / intensity are Neumorph-owned and do not rewrite the gutter
- focus ring stays visible

## Completeness

47 contract roles. Windows pixels **0%** — this environment cannot produce a Windows visual proof.
