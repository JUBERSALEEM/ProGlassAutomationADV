# step1.c.15 — Theme engine upgrade (shared geometry, future themes)

Date: **2026-10-21**. Senior analysis and the layer that was implemented. Source only. Windows pixels remain **0%**. Not an acceptance certificate. Themes 4–9 are **not** implemented.

## What was going wrong

Bento and Fluent already shared one mosaic (`BentoGrid` + `Card` / `BentoKpi` / `ChartCard`). They looked thin because:

1. **Color** — Fluent used a gray Windows-11 fill (`#F3F3F3` / `#1C1C1C`) with 8px corners. Bento headings were overwritten to SemiBold after apply.
2. **Rounding was not a product token** — `CardCornerRadius` existed, but Fluent had no studio writer. Future modules would each hard-code a radius.
3. **Theme apply was a fork** — Bento configure, Clay configure, or `null` for Fluent. Upcoming themes would copy that fork.

ExcelSticker’s `ThemeEngine` is the wrong upgrade. It is a sticker token catalog. ProGlass already has `ThemeService` + a 102-token contract.

## Engine shape (keep this)

```
ThemeCatalog.Find(name)
  → LoadResources(appearance)     // theme-owned package + extras
  → theme-owned configure         // BentoCustomization / ClayCustomization / future
  → Shared overlay (opt-in)       // geometry now; never mica, never clay plump
  → ResourceValidation            // 102 keys, styles, finite values
  → replace the one application slot
```

`SharedGeometry.Compose` is the single apply path used by `App`, Theme Studio, import, and save restore.

## Shared vs owned (do not mix engines)

| Layer | Shared across themes | Theme-owned |
| --- | --- | --- |
| 102-token contract | Yes | Values differ |
| Card / KPI / ChartCard templates bind `DynamicResource CardCornerRadius` | Yes | Template chrome differs (Border vs `FluentSurface` vs Clay extras) |
| **Card rounding (Crisp 6 / Soft 12 / Rounded 18 / Pill 26)** | **SharedTokens** | Clay plumpness **ThemeOwned** |
| Accent, canvas, density, type | No | Bento engine / Clay engine |
| Mica, acrylic, luminosity | No | Fluent extras only |
| Clay drop / inset / sheen | No | Clay `SetOwned` extras |

Fluent still has **no material customization engine**. The studio control on Fluent is **shared geometry**, not Fluent knobs.

## Why rounding is the overlay to do first

There will be many modules. If each screen sets `CornerRadius="12"`, Theme Studio cannot restyle them. If every `Card`, `BentoTile`, `ChartCard`, `BentoKpi`, `HeroCard`, button and chip binds `CardCornerRadius` / `ControlCornerRadius` / `ChipCornerRadius`, one studio combo restyles the application.

**Rule for every future module:** no local radius. Bind the token.

## Policy for the nine-theme list

Register each theme with `ThemeGeometry.SharedTokens` or `ThemeOwned`. Do not add a theme to `ThemeCatalog` until its package is complete.

| Theme | Geometry | Color / type | Material |
| --- | --- | --- | --- |
| **Bento Grid** | SharedTokens (also mirrored by `BentoShape`) | Bento engine | Flat plates, strong type |
| **Claymorphism** | **ThemeOwned** (`ClayPlumpness`) | Clay engine | Extras only |
| **Fluent / Acrylic** | SharedTokens | Package defaults + shared rounding | Mica/acrylic extras, **no Fluent engine** |
| Neumorphism | Likely ThemeOwned (soft inset radius is the material) | Future engine | Do not fake with Fluent luminosity |
| Liquid Glass | SharedTokens or owned once a glass renderer exists | Future | Do not reuse Clay extras |
| Glassmorphism | SharedTokens for layout; blur/frost owned | Future | Native WPF only |
| Spatial | SharedTokens | Future | Depth as tokens, not CSS |
| Swiss | SharedTokens (Crisp default) | Future | Tight type, not Bento density |
| Holographic | ThemeOwned if iridescence needs its own radii | Future | Do not mix with Clay sheen |

Upcoming themes plug in as: folder + Light/Dark 102 tokens + extras dictionary + `ThemeDefinition` + optional owned configure. They consume `SharedGeometry` when `Geometry == SharedTokens`.

## What this checkpoint changed

- Shared `CardRounding` studio control (Bento + Fluent). Persisted on `SavedCustomization.Geometry`.
- Clay plumpness still owns Clay corners.
- Bento default shape **Rounded**, headings **ExtraBold**, richer plates. Compact density no longer un-rounds cards.
- Fluent leaves gray mica-lite: cyan-tinted surfaces, 18px cards, ExtraBold headings, 168px rows/plots. Still mica extras. Still no Fluent knobs.
- `ThemeCatalog` carries `ThemeGeometry` so themes 4–9 can declare ownership without rewriting Theme Studio.

## Explicit non-goals

- Do not copy ExcelSticker `ThemeEngine` / hex sliders / `ClayBevelRenderer`.
- Do not give Fluent a BentoCustomization clone.
- Do not implement Neumorphism–Holographic until authorized.
- Do not claim Windows visual success from source.
