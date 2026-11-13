# step1.c.13 — ExcelSticker reuse vs ProGlass customization lifecycle

Date: **2026-10-21**. Checkpoint before themes 4–9. Source assessment only. Windows WPF pixels remain **0%**. This is not an acceptance certificate.

## Compatibility (short)

| Piece | Verdict | Why |
| --- | --- | --- |
| ExcelSticker `ThemeEngine` token catalog, hex sliders, ContrastGuard, `ClayBevelRenderer`, `Clay.xaml` | **Do not copy** | ProGlass already has a 102-token `ThemeService`, separate Bento/Clay/Fluent packages, and Clay extras via `SetOwned`. Their sticker palettes and bevel renderer would overwrite identity. |
| ExcelSticker `ThemeEditSession` (Preview → validate → atomic save → Revert on dispose) | **Adapt** | Same session shape, different state. ProGlass live-applies `BentoCustomizationState` / `ClayCustomizationState` into application resources. |
| ExcelSticker Import JSON / Export JSON / 64 KB cap / atomic `path.guid.tmp` | **Reuse the lifecycle** | Implemented on `CustomizationStore` (`ReadFile` / `WriteFile` / `MaxBytes`) and Theme Studio Import/Export commands. Schema is ProGlass v2, not ExcelSticker `theme.json`. |
| Disconnected Theme Studio that only restyles itself | **Reject** | Settings already call `ThemeService.Apply`, which replaces the application merged dictionary. Import/Save use that path. |
| Fluent customization engine | **Out of scope** | Fluent stays mica/acrylic defaults. Import/Export is file exchange only. |

**Reuse, do not redesign:** validate before apply, live preview on application resources, Save commits atomically, Discard restores the last saved package, invalid/oversized JSON is rejected, failed Save keeps the previous disk file.

**Adapt, do not paste:** namespaces, keys, `LocalAppData/ProGlassAutomation/bento-customization.json`, Bento vs Clay engines, no ThemeEngine class.

## Lifecycle now in this product

1. **Live preview** — Bento/Clay knobs call `ThemeService.Apply` with the theme-owned configure delegate.
2. **Save** — validate, atomic write, then update the in-memory baseline. Failed write leaves the previous file and previous baseline.
3. **Discard** — re-apply the last saved theme + appearance + Bento/Clay state. Does not write.
4. **Import JSON** — `ReadFile` (64 KB, version 2, enum validation). Apply live. Persist only when the user Saves.
5. **Export JSON** — writes the current live package, including unsaved preview.
6. **Restart** — `App` loads the store before the window shows. Corrupt disk returns defaults.

ExcelSticker files (`Version` ≠ 2, unknown keys, >64 KB) are rejected. They cannot drive ProGlass resources.

## Checkpoint

Nine-theme order remains: Bento, Clay, Fluent, Neumorphism, Liquid Glass, Glassmorphism, Spatial, Swiss, Holographic. **Do not proceed past Bento** until this lifecycle and the Bento mosaic are reviewed. Clay keeps its own engine. Fluent has no knobs.
