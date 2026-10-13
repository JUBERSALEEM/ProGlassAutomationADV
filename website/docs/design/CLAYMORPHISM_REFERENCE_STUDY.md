# Claymorphism Reference Study — ProGlass Automation ADV

Executed: 2026-10-13 · P01 of the master program.

## Sources actually read (with retrieval proof)

| Source | Retrieved | Notes |
|---|---|---|
| godrichstory.com/en/posts/claymorphism-landing/ | ✅ via platform fetch | 9 clay sections; demo SCSS quoted below |
| MDN — CSS box-shadow | ✅ via platform fetch | inset stacking, pairing/interpolation rules |
| codefronts.com/design-styles/css-claymorphism/ | ✅ via search excerpt | light-direction variable, elevation tiers, dark-mode recalibration |
| superdesign.dev/styles/claymorphism | ✅ via search excerpt | the canonical 3-shadow stack + press/focus rules |
| edesignify.com — claymorphism 3D trend | ✅ via search excerpt | 2 inner + 1 outer shadow; high radius; soft-vivid pastels |
| stellae.design — claymorphism 3D UI | ✅ via search excerpt | pastel palette (lavender/mint/peach/baby blue), matte finish |
| Session memory (2026-10-12) — Liquid Glass corroboration band | ✅ previously fetched | fill .05–.15, blur 12–18px, specular lip .85 — for the crystal family only |

## Principles extracted (the agreed recipe)

1. **Clay = three shadows, one declaration** — outer drop (tinted to the object's own hue, NEVER neutral grey), a dark inset on the side away from light, a light inset on the lit side. Drop any one and the illusion collapses: outer-only = floating card; insets-only = pressed well. (codefronts, superdesign, edesignify)
2. **One light source for the ENTIRE page** — every highlight/inset/drop agrees on direction (top-left conventions); the highlight is LIGHTER than the shade is dark — a symmetric pair reads as plastic. (codefronts, godrichstory)
3. **Elevation as tiers, not per-component choreography** — 4 tiers (well / resting / raised / floating) driven by one `--elev`-style number per element; hover promotes a tier. (codefronts)
4. **Geometry sells the mould** — corner radius ≈ 25% of the element's height (cards ~28–36px, buttons ~24px); generous padding (2rem on cards); zero sharp corners. (superdesign, godrichstory)
5. **Pastel but VIVID surfaces, never pure white** — matte fills; deep desaturated purple/ink text ≥ 4.5:1 on them; tint the drop toward the element's hue. (stellae, superdesign)
6. **Interaction grammar** — press feedback via `transform: translateY(2px) scale(0.98)`; never animate box-shadow (painting cost); state is NEVER signaled by depth alone (needs color/icon/text too); `:focus-visible` = a real 3px outline, never a shadow swap (WCAG 2.2 SC 1.4.11 needs a 3:1 edge a shadow can't supply). (superdesign, codefronts)
7. **Dark mode is NOT an inversion** — insets dim and tighten (bright 0.65 highlight = plastic glare on dark); depth comes partly from FILL LIGHTNESS TIERING because shadows can't carry 4 tiers on a dark page. (codefronts)
8. **Non-clickable containers take outer shadow only, no insets; no gradients on surfaces; no glassmorphism blur on clay** (crystal family owns blur; clay family's own grammar stays blur-free). (superdesign + project DECREE: crystal ≠ clay, never merge)
9. **Wallpaper ambient ≠ surfaces** — background blobs/pools drift behind the card; they don't paint the card. (godrichstory) — matches our hue-stepped pools (adoption block).

## Rejected patterns (recorded so they stay rejected)

- Global `* { background: var(--plate-1) }` sweeps — destroys chips/badges/icons (master prompt §4: no indiscriminate selectors).
- White text on pastel — fails 4.5:1; the engine ships plum ink (`--clay-ink`).
- Pure-black/neutral-greys in shadows — makes clay look dirty instead of warm.
- Shadow-only state signaling; blur added to clay surfaces to "suit" crystal.

## Proposed direction for this repo (binding for the phase queue)

- Keep the tonal ladder (`--band-face < --card-face < --data-face < --overlay-face`, panel as chroma hero) in ONE hue derived from the accent/family (Material-You adoption) — already the spine.
- Raise ladder SATURATION at the pale steps so data cards read colored, not "white with a whisper" (P02 target).
- Formalize the clay light variable & elevation tiers in the engine (P04).
- Enforce dark-mode inset dimming + fill-tiering (P04).
- Chips/badges/`rounded-md` elements: documented exception — they keep ink fills, never pose as cards (§21 door comment stands).
- Focus rings: assert 3px token + color shift on every interactive clay element; press = transform only (P05).
