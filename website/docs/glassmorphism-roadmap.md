# Glassmorphism roadmap — the Crystal family, brought to 100%

Status: **G0–G3 CLOSED 2026-10-12** · G4–G7 open · engines are separate by design (crystal-engine.css / clay-engine.css).
Companion doc: `claymorphism-roadmap.md` (phases 1–7 closed) — this file follows the same
conventions: components declare intent, selectors live in the stylesheet, every claim is gated,
numbers in commit messages are executed, not read.

---

## 0 · What Crystal already is (the honest inventory)

Crystal is the site's fourth appearance family (`data-appearance="crystal"`,
`src/data/themes.ts`), described as *"the application's Crystal Fluid family:
see-through surfaces over a luminous environment."* It is **not** a stub —
a real glass system already ships:

**Token system** (`src/app/globals.css`, block at `:606` + daylight re-tune `:781`):

- **Tiered translucency** — `--surface-alpha: 0.55 / 0.72 / 0.92` (three tiers: prose
  cards, raised panels, live-in-app previews), blur `26px` + saturation `200%`.
  Daylight re-weights to `0.82 / 0.90 / 0.99` with a comment that documents *why*:
  *"the fill alone has to carry the text (≥ 0.8), the blur where it works is
  frosting, never the load-bearing layer."* This is the right glassmorphism
  philosophy and the upgrade must keep it.
- **The light rig** — `--glass-hi / lo / sheen / frost / scrim`, an asymmetric
  bevel (`inset 1px 1px` bright, `inset -1px -1px` dark), corner catch-lights
  confined to 72%/84% ellipses *deliberately smaller than the card* so the
  light never lifts the middle where text sits (the comment cites a measured
  4.12:1 failure at 0.14/0.09 specular and the 4.5:1 floor that replaced it).
  **This family already measures contrast. The upgrade inherits that bar.**
- **The room** — `--wall-lamp` + `--wall-floor` feed the fixed `body::after`
  wallpaper stack (`:1034`), so glass panels genuinely have light behind them
  to transmit. Only the glass families switch these on.
- **Edge system** — `--glass-ring` outside, `--glass-edge-light/dark` inside,
  `--card-edge` at `rgb(255 255 255 / 0.34)`.

**Re-glassed component classes (39 family-scoped rules)** — nav-glass, nav-pill
(+ photon sweep), nav-sheet, surface / surface-raised / stat-tile / plate /
card-accent / glass-primary (blur + rim + bevel), float-card (over artwork),
menu-card (0.90 — "the most solid glass on the site"), band-solid, app-chrome /
app-rail (deliberately near-solid inside product screenshots — correct), plus
per-class `@supports` fallbacks that raise fills to 0.94–0.99 when
`backdrop-filter` is absent. **The fallback layer is complete and thoughtful.**

**Studio integration** — the accent ramp dial works (`FAMILY_ACCENT.crystal =
#1AC5E2`, ramp rebuilt via `rampFor`); the tint dial writes `--tint-mix` as a
multiplier *over* the family's own `--tint-base: 0.22` ("a family that is meant
to be saturated keeps its own depth"). Theme boot script contains crystal in
its allowlist.

---

## 1 · What is missing — the gap list, by severity

### S1 · Legibility [BLOCKER — a live defect class, proven today in clay]

**The light-wash + light-ink pairing exists in Crystal daylight too.**
`bg-ink-950/60` inverts per mode (`--ink-950`: `8 5 22` midnight →
`248 245 238` daylight — family-independent, `globals.css:31,386`), so in
daylight *every* family renders that utility as a light wash. The developer
page's `text-brand-200/90` chips are light ink → light-on-light, the exact
defect fixed for clay in §20 (`clay-engine.css` §20, commit `8e811d4`).
**Clay got the re-ink; crystal, studio, monet and industrial did not.**
The glassmorphism work must generalize the fix — same grammar, family-scoped.

### S2 · Coverage [the main gap — the site is ~60% glass]

Named component classes are re-glassed; **utility surfaces are not**. Census
from the source tree:

- `chip` — 20 files (opaque `bg-ink-800` pills floating on glass)
- `<table>` — 16 files (opaque header/row fills)
- `<pre>/<code>` — 6 files (solid `bg-ink-950` code blocks — the developer page)
- inputs/`.field` — 5 files (canvas-default chrome, no glass wells, no
  family focus lip; clay has §19, crystal has **zero** form rules)
- `bg-ink-…/` utility fills — **94 sites** across pages and features
- accordion/tabs/filters — no crystal states

Under crystal these all stay solid (dark at midnight, light-slate at daylight)
— opaque islands on a translucent page. This is precisely what reads as
"not 100% glassmorphic."

### S3 · The bench [feature gap]

Clay has a studio bench (6 presets + accent + custom plate, persisted,
shareable, saturation-guarded). Crystal has **no material dials at all**:
blur radius, fill alpha/tint, and specular strength are frozen constants.
Glassmorphism is a material family; its studio surface should expose the
material (with the same guards: write-only-when-moved, never outrank
defaults, never fight other families).

### S4 · Verification [process gap]

Clay is pinned by 125 paint probes + 25 contrast pairs + pixel-pass + coverage.
Crystal has **zero harness coverage** — its rules are gated only by the generic
contrast pairs and by eye. The measured comments in the block are excellent
hand-written audits, but nothing stops the next edit from silently dropping a
fill below the floor. **No glass work ships before the probes exist (phase G0).**

### S3b · Motion [opportunity]

The only moving light is the 46s nav-pill photon. Glassmorphism's signature is
light that answers the room: pointer-tracked specular on cards, view-linked
frost. All behind the existing reduced-motion and pointer-guard rails.

### S5 · Performance budget [risk to keep managed]

Every tier-1/2 surface composites a blur layer. Fine today (measured choices:
26–28px not 34+, tier 3 excluded "so every live preview of the application
stays sharp and uncomposited"). G1 adds ≈30–80 more blurred surfaces
(chips/tables/code) — mobile scroll weight must be re-measured, with a
coarse-pointer de-blur strategy that keeps *fills* (the load-bearing layer)
intact.

### S6 · Decision [user input needed at the end]

Site default is currently `clay` (hygiene v2 boot migration,
`themes.ts DEFAULT_APPEARANCE`). Whether Crystal becomes the default after
G0–G6 is a deliberate last decision, not an accident of shipping.

---

## 2 · Standing invariants (the "don't ruin" list)

1. **Text tiers are untouchable.** `--text-1..4` are mode-scoped only
   (`globals.css:77-80, 418-421`), with **zero** declarations under any
   `data-appearance` — verified by grep today. Glass phases may not write
   `--text-*` under `[data-appearance="crystal"]` scope. The only text work
   allowed is the S1 re-ink: light-ink *utility classes* on light fills
   resolve through the tiers — which fixes text, never restyles it.
2. **Contrast floors are measured by the harness, not asserted in prose.**
   4.5:1 body / 3:1 large, both modes, over the *brightest point of the
   wallpaper* (the family's own documented bar).
3. **Family isolation.** Every rule crystal-scoped; isolation probes prove
   the four other families silent after each phase (the clay §15–20 probes
   are the template).
4. **The daylight doctrine stays.** Fills carry text (≥0.8 by day); blur is
   frosting, never structure. Specular stays out of the text field.
5. **Fallbacks ship with every new glass surface** (the existing `@supports`
   pattern: fills rise, scrim rises, design intent survives).
6. **Studio writes only what was moved** (the tint-dial precedent) and reads
   storage through the existing hygiene/version keys.
7. **No change to `DEFAULT_APPEARANCE`, boot script, or storage keys** outside
   phase G6; no UI change without gate evidence in the commit message.

---

## 3 · The phases

### G0 — Gates first ✅ CLOSED `8040c23` *(red before green; 28 dossier rows + 33 contract pairs)*

Extend the verification stack to be family-parametric before any pixel moves:

- `clay-paint.test.mjs` → probe runner parameterized by family; add the
  **crystal dossier**: chip/foil lifts, nav chrome, tiers, bands, plates.
- **S1 red probe**: `bg-ink-950/60 + text-brand-200/90` chip in *daylight
  crystal* must currently fail (it does — the defect exists) → turns green
  in G1. Same probe for studio/monet/industrial daylight where the pairing
  is authored (documents the defect class globally, fixes it everywhere).
- Crystal contrast pairs into `clay-contrast.test.mjs`, incl. text-4 (#C6C0E2,
  the faintest tier) at the lightest fill over the brightest wallpaper point,
  re-validating the documented 4.5:1 scrim math automatically.
- Exit: new probes run; S1 red rows identified; everything else green.

### G1 — The glass lift ✅ CLOSED *(same commit as G2/G3)*

Family-scoped utility lift, mirroring clay §9's grammar:

- `[data-appearance="crystal"] [class*="bg-ink-"], [class*="bg-white"],
  [class*="bg-line"], [class*="bg-glass"]` → tiered glass fills (frost +
  hi/lo/sheen + scrim), blur per tier, rim ring; daylight re-weights fills
  per the doctrine. `@supports` fallback per class group.
- **The re-ink (S1)**: brand-carried and light-ink text on lifted fills →
  tier system, generalized so studio/monet/industrial benefit equally where
  authored (cross-check obligation from today's clay fix).
- Probes: the S1 red rows turn green; paint count rises with the new surface
  rows; isolation green.

### G2 — Chrome & forms in glass ✅ CLOSED *(§C2 wells, focus lip, caret, placeholder, accent)*

- Inputs, selects, textareas, checkboxes, radios, range: frosted wells
  (inset, slightly more solid than tier 1 — wells receive text), family
  focus lip (ring from `--glass-edge-light`, not the OS ring),
  `accent-color` from the accent ramp, family scrollbars; placeholder tier.
- Tabs, accordion, filter pills: glass states incl. active/pressed.
- Probes: field wells + focus lip + placeholder contrast, both modes;
  family isolation.

### G3 — Chips, tables, code ✅ CLOSED *(§C3 named surfaces + seams/hover)*

- `.chip` (20 files) → glass pill: translucent fill, rim, tier ink.
- Tables (16 files) → frosted header band + glass seams; row hover lifts.
- `<pre>/<code>` (6 files, the developer page's wall of chips) → glass well
  with etched edge; mono ink unchanged.
- Probes per surface class, both modes.

### G4 — The Crystal bench (studio parity, S3) ⬜ OPEN

- Three dials, crystal-scoped, write-only-when-moved, persisted under the
  studio keys with the existing guards:
  - **Frost** — blur radius 0–32px (writes `--surface-blur`)
  - **Body** — fill strength 0–3× over `--surface-alpha` base (the
    tint-dial pattern; daylight minimum respected: the dial cannot take a
    daylight fill under the 0.8 doctrine floor)
  - **Glint** — specular strength `--surface-specular` 0–2×, capped so the
    corner ellipses never cross the text field (the documented 4.12:1
    boundary is the ceiling, encoded as a clamp, not a plea)
- ThemeStudio section + preview card; vitest for dial math + persistence +
  neon-saturation guard parity for crystal accents (ramp path already
  shares `rampFor`; verify + pin).
- Probes: bench-active vs house renders; isolation.

### G5 — Living glass (motion, S3b) ⬜ OPEN

- Pointer-tracked specular: cards take a cursor-following catch-light
  (the nav-photon grammar, card-local), via one shared rAF throttled
  handler — pointer-guard compliant (no unguarded state writes; CSS var
  writes only).
- View-linked frost: sections gently cool as they leave the viewport
  (`animation-timeline: view()` behind `@supports`, the §11.13 precedent).
- All off under `prefers-reduced-motion`; all off under coarse pointers if
  measured heavy (see G5 budget check).
- Probes: reduced-motion block scan (the minified-tolerant regex precedent).

### G6 — Performance & fallback certification (S5) ⬜ OPEN

- Per-route blurred-surface census script (counts `backdrop-filter` live
  surfaces per route DOM), budget recorded in `audit-site.py` with the
  measured-trail comment convention.
- Coarse-pointer strategy if over budget: blur radius step-down / tier-2
  blur skip on `(pointer: coarse)` — fills and scrims untouched (they carry
  legibility), only the frosting thins.
- Fallback sweep: `@supports not` path painted and probed (near-opaque,
  scrim-raised) on every new surface from G1–G3.

### G7 — Catalog, docs & the default decision (S6) ⬜ OPEN

- `/sys/crystal` — the single-screen review surface (mirror of `/sys/clay`:
  every re-glassed class lane, both modes, noindex + sitemap-free).
- Decision log in `claymorphism-redesign-progress.md` (D30+) with executed
  numbers per phase; this roadmap's phases marked CLOSED as they land.
- **Default family decision**, made here and only here, with evidence:
  switch `DEFAULT_APPEARANCE`/boot hygiene key to crystal (or deliberately
  keep clay), documented as its own commit with rollback = one revert.

---

## 4 · Where it stands now

**Closed:** G0, G1, G2, G3 — the crystal engine is a separate file
(`src/app/crystal-engine.css`, §C1–§C3); the four daylight light-chip pins
sprung and were PROMOTED to floor probes (paint 175/175, contract 41/41).
The shared daylight legibility grammar lives in globals.css (requested:
engines stay separate; clay's §20 untouched).

**Balance to 100%:** G4 · G5 · G6 · G7.

Next action when resumed:

Phase G0 (~small): family-parameterize the probe runner, add the crystal
dossier incl. the S1 daylight chip rows (expect them red), extend contrast
pairs, run the full existing suite to prove zero clay regressions. Ship as
one commit with executed counts — the green board the rest of the roadmap
builds on.
