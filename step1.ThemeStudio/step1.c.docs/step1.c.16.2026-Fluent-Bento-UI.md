# step1.c.16 — Why the Fluent screenshot looked outdated, and the 2026 upgrade

Date: **2026-10-21**. Source upgrade from the Theme Studio screenshot (Fluent Light). Windows pixels still not a certification.

## What the screenshot showed

| Symptom | Cause in source | 2026 pattern |
| --- | --- | --- |
| Baby-blue / mint / lavender KPI slabs | `BentoKpi` filled `SurfaceInformation` / `SurfacePositive` / `SurfaceFeature` | Layered white/mica card + 4px accent **rail**, not a painted tile |
| Acrylic invisible | Tint `#140A58A8` (~8%) on already-blue fills | ~24% accent tint + luminosity edge on **white** cards |
| ExtraBold titles | `FontWeightHeading` ExtraBold | Fluent 2 / WinUI: **SemiBold** display, Regular body |
| Solid blue module bar vs white sidebar | `ShellModuleBanner` = `AccentPrimary` + `TextOnAccent` | App chrome = `SurfacePrimary` + `TextPrimary`, accent only on selected nav |
| Candy chart plates | Feature/Positive plates used full tint fills | Same acrylic card as ChartCard; colour lives in the series |
| Washed canvas | Fluent bg `#E7F1F8` | Mica gray `#F3F5F7` / dark `#1C1C1C` |

This is 2016–2018 “coloured dashboard widget”, not 2025–26 WinUI 3 / Linear / Fluent 2.

## What changed (Bento + Fluent only)

- **Chrome:** module banner is a surface card, not an accent slab.
- **Type:** Segoe UI Variable fallback Segoe UI; headings SemiBold; KPI value uses `FontSizeDisplay`.
- **KPI:** `SurfacePrimary` + semantic rail (`AccentPrimary` / success / warning).
- **Plates:** alias of ChartCard (no pastel fill).
- **Fluent extras:** stronger tint/luminosity/stroke so acrylic reads on white.
- **Spacing:** card padding 16.

Clay is untouched. Fluent still has **no material engine**. Card rounding remains the shared studio token.

## Not claimed

No Windows visual sign-off. Rebuild on the PC to judge acrylic and type.
