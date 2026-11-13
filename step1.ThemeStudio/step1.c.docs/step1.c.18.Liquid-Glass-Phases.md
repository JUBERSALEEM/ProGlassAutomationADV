# step1.c.18 — Next theme: Liquid Glass (phases only)

Date: **2026-10-21**. Source audit + web research. **No theme code in this checkpoint.** Windows pixels remain **0%**. Do not treat this file as visual proof or as authorization to register a fifth catalog entry.

Stay **Phase 01 / step1**. Native WPF. Engines stay separate. Do not mix Liquid Glass with Fluent acrylic, Clay extras, Neumorph extrusion, or the later Glassmorphism theme.

---

## 1. Are the four themes complete?

**Registered in `ThemeCatalog` (this order):**

| # | Catalog name | Folder | Geometry | Owned material | Studio knobs |
| --- | --- | --- | --- | --- | --- |
| 1 | Bento Grid / Modular | `ThemeBento/` | SharedTokens | Flat `Border` plates | Bento engine |
| 2 | Claymorphism | `ThemeClaymorphism/` | ThemeOwned | `ClaySurface` 3-shadow | Clay engine |
| 3 | Fluent / Acrylic | `ThemeFluent/` | SharedTokens | `FluentSurface` mica/acrylic | **None** (rounding only) |
| 4 | Neumorphism | `ThemeNeumorphism/` | SharedTokens | `NeumorphSurface` dual outer | Depth / blur / intensity |

Each of the four has Light + Dark 102-token packages, the same component set (`Foundation` … `Analytics`), and a Readme. That is **source completeness against the contract**, not a Windows sign-off.

**Still not done for any theme**

- No Windows visual certification from this environment.
- Home/Studio mosaic height is a layout problem, not a missing fifth theme.
- Fluent must **not** gain a lighting/customization engine.
- Catalog comment: *register only complete, authorized themes*. Themes 5–9 are **not** installed.

So: **yes, start the next theme as a planned engine, not by cloning Fluent extras.** The next name in the nine-theme list is **Liquid Glass**. Do not skip to Glassmorphism, Spatial, Swiss, or Holographic.

---

## 2. What Liquid Glass actually is (research)

Apple announced **Liquid Glass** at WWDC 2025 (9 Jun 2025) as the material for iOS 26 / macOS Tahoe 26 and later. It is a **dynamic glass material**, not a blur skin. [1](https://www.apple.com/newsroom/2025/06/apple-introduces-a-delightful-and-elegant-new-software-design/) [2](https://developer.apple.com/videos/play/wwdc2025/219/)

| Property | Glassmorphism (theme 6) | Fluent Acrylic (theme 3) | Liquid Glass (theme 5) |
| --- | --- | --- | --- |
| Backdrop blur | Yes, static frost | Tint + luminosity + noise | Yes, plus **refraction / lensing** |
| Specular | Optional hairline | Reveal hover | **Moving highlight**, thicker glass when the control grows |
| Colour | Manual rgba | Wallpaper/mica sample | Adapts light/dark and nearby content |
| Motion | Rare | None required | Morphing capsules, scroll-edge dissolve |
| How you get it | Write frost | `FluentSurface` | Apple: OS control. WPF: **approximation** |

WWDC 2026 / iOS 27 retuned the material: less default transparency, **darkened edge**, brighter specular, user slider from clear → frosted. [3](https://www.macrumors.com/2026/06/10/how-liquid-glass-is-changing-in-ios-27/) Accessibility: Reduced Transparency → frostier/opaque; Increased Contrast → solid + border; Reduced Motion → no elastic specular. [2](https://developer.apple.com/videos/play/wwdc2025/219/)

**CSS cannot refract.** `backdrop-filter` has ten functions; none displace pixels. “Liquid Glass in pure CSS” is glassmorphism with a brighter rim. [4](https://theplusaddons.com/blog/liquid-glass-ui/) This product is **native WPF**, so we do not import that CSS.

**WPF public approximations** (May 2026 demo): desktop GDI capture → crop → `ShaderEffect` distortion. [5](https://xamltemplates.net/wpf-liquid-glass-effect-windows) That path is **rejected for ProGlass cards**: recursive self-capture while dragging, per-tile screenshot cost (scroll already lagged), Win32 GDI outside the theme contract, and it still is not Apple’s Metal sampler.

AppKit’s real API (`NSGlassEffectView`) samples an area **larger than the control** and **cannot sample other glass** unless grouped. [6](https://developer.apple.com/videos/play/wwdc2025/310/) WPF has no equivalent sampler. We will not fake one with a live `BlurEffect` on every mosaic tile.

---

## 3. Engine shape (do not invent a second ThemeService)

Keep the existing apply path from `step1.c.15`:

```
ThemeCatalog.Find("Liquid Glass")
  → LoadResources(appearance)     // ThemeLiquidGlass Light/Dark + extras
  → optional LiquidGlass.Configure  // frost / tint / specular only
  → SharedGeometry.Compose        // rounding + font overlay + color overlay
  → ResourceValidation            // same 102 keys
  → replace the one application slot
```

**Do not** copy ExcelSticker, CSS, Clay extras, Fluent luminosity, or Neumorph dual-shadow into this folder.

| Layer | Shared | Liquid-owned |
| --- | --- | --- |
| 102-token contract | Yes | Values (translucent fills, rims) |
| `CardCornerRadius` | SharedTokens | — |
| Font / color overlay | Always | — |
| Material | — | `LiquidGlassSurface` |
| Frost / specular / tint | — | Extras + optional small studio |
| Mica / acrylic | Fluent only | Forbidden here |
| Clay plump / 3-shadow | Clay only | Forbidden here |
| True refraction shader | — | **Not in v1** (see Phase L4) |

`ThemeGeometry.SharedTokens` for v1 (rounding stays the shared combo). Do not add a Fluent-style lighting engine. A **small** owned state (`Frost`, `Specular`, `Tint`) is allowed later, analogous to Neumorph depth — not a second BentoCustomization.

---

## 4. Honest WPF material (v1)

`LiquidGlassSurface : ContentControl`, same rank idea as Clay (Card / Control / Chip):

1. **Face** — low-alpha tinted fill (`rgba` via `SolidColorBrush` / gradient). Light: cool white ~10–18% on a **saturated canvas**. Dark: 8–14% light over a deep canvas. Canvas stays **opaque** so charts and text do not fall through the window.
2. **Specular edge** — inset top highlight (bright, 1–2 px). This is the highest-value tell versus frost-only glass. [4](https://theplusaddons.com/blog/liquid-glass-ui/)
3. **Darkened rim** — iOS 27 separation; 1 px inner shade, not Clay hue glow.
4. **Lift** — unnamed outer `DropShadowEffect` on the card (never `x:Name` a `DropShadowEffect` — MC4111).
5. **Capsule chrome** — buttons / nav / banner cluster use stadium radii from tokens; content cards use `CardCornerRadius`.
6. **Hierarchy** — glass is the **control layer above content** (sidebar, module banner, flyouts, primary buttons). KPI/chart **faces** may be glass; plot area stays opaque/transparent as today (do not Blur Skia — GPU/cache already went black).
7. **Fallback** — when a future “reduce transparency” token is true, fill becomes opaque `SurfacePrimary` and effects collapse. Contrast 4.5:1 still required.

**One glass recipe per viewport region.** Do not stack six blurred cards. Nested glass samples nested glass poorly even on Apple hardware.

**Not v1:** per-frame desktop capture, PixelShader refraction, device-tilt specular, Win2D, extra NuGet, CSS `backdrop-filter`, animating blur radius on scroll.

---

## 5. Phases (apply in order; stop if a gate fails)

### L0 — Gate (no files)

- Four themes stay registered; do not unregister them to “make room.”
- User confirms: **start Liquid Glass**, still step1, no PR until asked.
- Do not open `ThemeGlassmorphism/` in the same change set.

### L1 — Isolation spec (this file)

- Done when this document is reviewed. No catalog entry yet.
- Folder spelling when authorized: `Theme/ThemeLiquidGlass/` (project rules). Files: `ThemeLiquidGlass.Light.xaml`, `.Dark.xaml`, `.Readme.md`, `step1.e.7.LiquidResources.cs`, `step1.e.9.LiquidGlassSurface.cs`, extras, `step1.e.Components/` copy of the nine pages.

### L2 — Token package (no catalog)

- Light + Dark dictionaries implementing **all** `ThemeContract.Required` keys.
- Canvas **saturated and opaque**. Surfaces **translucent tokens**, not Fluent gray mica and not Neumorph same-hex extrusion.
- Extras: specular brush, rim brush, frost opacity, lift depth. Never merge Clay/Fluent extras.
- `ResourceValidation` must pass in isolation tests **before** `ThemeCatalog` grows.

### L3 — `LiquidGlassSurface`

- Native WPF `ContentControl` (pattern: `FluentSurface` / `ClaySurface`).
- Layers listed in §4. `GetLayoutClip` policy: do not clip the lift shadow; do not unclip BentoGrid (`ClipToBounds` stays true on the mosaic host).
- No `x:Name` on `DropShadowEffect`.
- No `BitmapCache` / Skia GPU on this surface.

### L4 — Component templates

- Replace `Card`, `HeroCard`, plates, buttons, chips, flyouts with `LiquidGlassSurface` where the control is chrome.
- Inputs/text stay opaque enough to hit 4.5:1.
- Charts: `ChartCard` may use the glass chrome; `BentoChart` stays CPU + transparent plot (donut hole).
- Bind radii to `CardCornerRadius` / `ControlCornerRadius` / `ChipCornerRadius` only.

### L5 — Load + Compose (still unregistered)

- `LiquidResources.Load` mirrors Fluent: package + extras.
- `SharedGeometry.Compose` grows **one** optional configure delegate — do not fork `ThemeService.Apply`.
- Font overlay and color overlay still run after material.

### L6 — Studio copy only

- Theme picker shows the name **after** L7.
- Until then, CustomizationPanel copy: “Liquid Glass uses shared rounding. Frost is owned. Not Fluent acrylic.”
- **No** Fluent extras sliders. **No** Clay plumpness. Optional later: Frost / Specular / Tint enums, persisted like Neumorph, `ThemeGeometry.SharedTokens` still.

### L7 — Register

- Add `ThemeDefinition` only when L2–L5 compile and contract tests pass.
- `ThemeService.LiquidGlass` constant. `CanCustomizeLiquid` only if L6 knobs exist; otherwise false.
- `verify_foundation.py` / runtime checks gain the fifth name the same way Clay was added — not as “missing.”

### L8 — Windows visual (not this sandbox)

- Light + Dark, Reduced-transparency path, 100/150/200% DPI, flyout over mosaic, donut not black.
- Until then, Readme must say **pixels 0%**.

### L9 — Stop

- Do **not** start Glassmorphism in the same PR/session. Theme 6 is frost-only and will look “the same” if mixed.

---

## 6. File plan (when L0 is authorized)

```
Theme/ThemeLiquidGlass/
  ThemeLiquidGlass.Light.xaml
  ThemeLiquidGlass.Dark.xaml
  ThemeLiquidGlass.Light.Extras.xaml
  ThemeLiquidGlass.Dark.Extras.xaml
  ThemeLiquidGlass.Readme.md
  step1.e.7.LiquidResources.cs
  step1.e.9.LiquidGlassSurface.cs
  step1.e.Components/   # 1,2,3,4,5,6,9,10 — same set as the four live themes
```

`ProGlassAutomation.csproj` glob `Theme/ThemeLiquidGlass/**/*.cs` and `*.xaml` only at L7.

Empty folder now = forbidden (project rules: no placeholder themes).

---

## 7. Risks (why we did not code today)

| Risk | Mitigation |
| --- | --- |
| Shipping blur-only and calling it Liquid Glass | Specular + rim + translucent face required in L3 review |
| Mixing with Fluent acrylic | Separate control; no `FluentSurface` in this folder |
| Mixing with theme 6 Glassmorphism | L9; frost-only is a later engine |
| Scroll lag / black charts | No BlurEffect on LiveCharts; no BitmapCache; no GPU |
| Desktop capture shader | Rejected for product cards |
| Catalog half-entry | Register only at L7 |

---

## 8. Decision needed

Reply with **L0 authorized** to implement L2–L3 next (tokens + surface, still unregistered), or hold. Do not ask for Glassmorphism, Spatial, Swiss, or Holographic until Liquid Glass is registered and this file’s L8 note is still honest.

L0–L7 applied as separate commits. L8 Windows visual remains **0%**. L9: do not start Glassmorphism.
