# step1.c.14 — Why Bento and Fluent looked minimal

Date: **2026-10-21**. Source audit. Not Windows visual proof.

## Bento Grid

Bento looked like a compact tile sheet because the **layout engine was a real mosaic** while the **metrics were compact-first**.

| Cause in source | Effect |
| --- | --- |
| Default `BentoDensity.Compact` | Live apply set `BentoRowHeight` to 124 and `ControlMinHeight` to 32. |
| Token `BentoRowHeight` 132 / `ChartPlotHeight` 112 / `PaddingCard` 8 | Uncustomized package painted short plots and tight cards. |
| Home `AnalyticsOverview` is a **5-row** inner mosaic hosted in a **3-row** outer span | FillAvailable squashed KPI, charts, activity and table into three short rows. |
| Studio preview `Compact="True"` | Intentional 2×2 laboratory, not the enterprise Home. |
| `Spacious` density existed only as a leftover `_` branch | Density UI offered Compact and Comfortable only. |

This is not “missing ERP.” Hierarchy already existed (hero 2×2, KPI 2-col, chart 2×2, full-width table). The spans did not match the inner mosaic, and the row token was too small for type + chart + caption.

**Correction in this checkpoint (Bento-owned only):**

- Tokens: `BentoRowHeight` 168, `ChartPlotHeight` 168, `PaddingCard` 12.
- Density: Compact 152 / Comfortable 176 / Spacious 200 rows; plot height follows density; type scale drives title/display/section.
- Home: analytics **row span 5** (matches inner 5 rows), work/details at row 7, layers at row 9 span 2.
- Studio compact preview **unchanged** (`Compact="True"`).

Windows pixels are still 0%. Do not treat this table as a screenshot sign-off.

## Fluent / Acrylic

Fluent looks minimal for a different reason: **it is chrome, not a second Bento engine.**

| Cause in source | Effect |
| --- | --- |
| No Fluent customization type, no knobs | Studio copy: mica/acrylic defaults only. |
| Same 102-token mosaic as Bento | Home/Studio structure is shared; Fluent only replaces surfaces with `FluentSurface` (tint, stroke, luminosity). |
| Fluent `ChartPlotHeight` 112 / `BentoRowHeight` 132 | Fluent was not part of the Bento density correction. |
| `CanCustomizeBento` / `CanCustomizeClay` false | Accent, density, type, spacing do not run. |

Fluent is supposed to read as system acrylic, not as a dense enterprise bento. Giving it BentoCustomization would mix engines. **No Fluent customization engine in this checkpoint.**

## What would still look thin on a real PC

- Compact density remains the **default** so ResetDesign / RuntimeChecks stay on `ControlMinHeight` 32. Comfortable/Spacious are the roomier presets.
- Overflow popups still clip to host boxes until hover.
- Without a Windows screenshot, card optical weight (border vs fill) cannot be certified.

Next theme is still **not authorized**. Clay and Fluent stay separate.
