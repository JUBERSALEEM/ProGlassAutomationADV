# Neumorphism (theme 4)

Separate package `Theme/ThemeNeumorphism`. Not Bento, Clay, or Fluent.

## Engine

- Dual Gaussian glow at WPF 125° / 305° (CSS 145deg). Card glow sits in `NeumorphGutter`. Control/Chip paint outside the layout box; `GetLayoutClip` returns null.
- Face fill is the opaque 145° canvas pair. Wells invert it. Inset is one 6px dual-tone ring with Gaussian blur, clipped to Face.
- Spread is `NeumorphSpreadThickness` (0 by default) — a hard ring, which is what CSS spread is before blur.
- Motion duration is `NeumorphCustomization.MotionDuration()` (ClientArea + Menu + ComboBox). Applied in code on Concave. `StaticPropertyChanged` retunes the resource.
- StatusBadge stays Border; light glow is a Grid wrap, same cell.
- Near plates and rim wrappers are gone.
- Press/hover: inset well / stronger shadow. Unselected rows stay flat.

Windows pixels remain **0%**. This environment cannot produce a Windows visual proof.
