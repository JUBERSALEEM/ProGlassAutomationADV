# step1.c.12 — Claymorphism theme package (Phase 01 / step1)

Date: **2026-10-20**. Clay-only engine. Bento files not modified.

## Reference used

The reports URL (`/reports?status=Confirmed`) is **sign-in gated**. Public chrome on that host currently reads **System mode, Crystal palette**, not Clay. Upgrade follows the clay.css / setproduct recipe and the earlier pastel references: one outset drop + two insets (top-left sheen, bottom-right shade), 32 DIP radius, pastel plates. No CSS copied into WPF.

## Engine pattern now

`ClaySurface` (ThemeClaymorphism) stacks three native Borders. Buttons, combo, concave inputs, KPIs use it. Screen `Card`/`ChartCard` remain Border + hue drop (Border has no template). Press squashes into concave.

## Completeness (source, not Windows pixels)

| Slice | % | Note |
| --- | --- | --- |
| Isolated package / catalog / Light+Dark / 102×47 | 100 | Clay folder only |
| 3-layer clay on templated controls | 85 | ClaySurface convex + concave |
| Cards/plates on screen Borders | 55 | Outer hue shadow + light edge; no inner shade (Border cannot template) |
| Pastel / coral / navy | 80 | WCAG forces darker coral than marketing salmon |
| Pill press / progress | 70 | Translate + concave; no spring physics |
| Hue-matched inner/outer shadows | 65 | Extras brushes; not per-tile hue |
| 3D icons / shop-kanban chrome | 0 | Out of scope |
| Windows restore/build/pixels | 0 | Not run |
| **Overall Clay look (source)** | **~72%** | **Windows 0%** |

Remaining to 100% source: inner shade on Border cards, per-surface hue shadows, pill chips at capsule radius, Windows proof.
