# Liquid Glass Prototype — Phased Checklist & Validation Log

> Scope law for this track: **prototype first, site-wide rollout last.**
> Nothing in this document authorises reskinning production pages. The
> prototype lives on one internal, noindex review route
> (`/[locale]/sys/liquid-glass`) and every selector it owns is scoped
> under `.lgx`, so no production surface can inherit a prototype rule by
> accident. Existing routes, content, i18n, SEO and the Clay/Crystal
> engines are untouched by this track until Phase 5 is explicitly
> approved by the user.

## Reference study (what was actually looked at)

1. **ZenSwap — Liquid Glass Concept** (Vitalii Razgulin, via
   liquidglassdesign.com/gallery/liquid-glass-concept; Dribbble shot
   26315138). Studied visually. Grammar extracted:
   - Near-clear glass panes over a **sculptural, folded satin backdrop**
     (monochrome folds), so the eye reads refraction against shape, not
     against flat colour.
   - **Ultra-thin luminous edge**, brightest at the top-left; the edge
     is light, not a drawn stroke.
   - **Layered overlapping sheets** (two wallet panes at different
     depths/opacity tiers).
   - **Pill tab switch** (Tokens / Activity) with a solid white pill
     indicator sliding between segments.
   - **Circular glass icon buttons** (close, exit) floating on the pane.
   - A **selected row raised as an inset glass pill** inside a list.
   - Serene, cool, low-saturation palette; muted text (a contrast risk
     we deliberately do *not* copy — see Differences).
2. **Liquid-Glass-Design-26262238** (HoseinBagheri, via
   liquidglassdesign.com/gallery/liquid-glass-design-26262238;
   Dribbble shot 26262238). Style prompt + shot palette extracted:
   "liquid glass aesthetic, frosted glass elements, **vibrant color
   palette with bold contrasts, playful geometric shapes**, soft
   lighting effects, **layered depth**, glossy accents for
   interactivity"; negatives: low contrast, muted colours, harsh
   shadows, clutter, flat design. Shot palette: near-black base
   `#010101`, magenta `#F933D8`, amber `#D69126`, plum `#561F3B`.

**Combined grammar the prototype must demonstrate:** floating pill
navigation · pointer-reactive glossy buttons with press/release ·
a panel that morphs open/selected/dismissed · reflections and layered
transparency tiers · thin luminous edge highlights · simulated optical
distortion · fluid transitions caused by real user actions · one
coordinated light/dark pairing.

## Technology evaluation (decided before coding)

| Technique | Verdict | Reason |
|---|---|---|
| `motion` (Framer Motion, already a dependency) | **Adopt** | `layoutId` sliding indicator, `AnimatePresence` panel morph, spring press/release, drag — the interaction layer, zero new deps. |
| CSS gradients + `backdrop-filter` | **Adopt (core material)** | The glass itself: tiered fills, specular sweeps, edge light. GPU-cheap, themeable via custom properties. |
| SVG filters (`feTurbulence` + `feDisplacementMap`) | **Adopt, contained** | Real optical-distortion read, but applied to **one decorative lens layer only**, with a user-controlled intensity slider and an off position. Applied to text it would be illegible and expensive — never on content. |
| WebGL (custom refraction shader) | **Rejected for the prototype** | Would need a render loop, canvas compositing over DOM text, and a fallback stack — cost not demonstrated by value. CSS backdrop blur + SVG displacement achieve the showcase read at a fraction of the budget. Revisit only if a future hero showpiece proves the need with measurements. |
| CSS scroll-driven animations | **Already shipped** | §H2 (Crystal) covers the scroll-stirred water site-wide; the prototype focuses on *action-triggered* fluidity instead of duplicating it. |

## Phases

- [x] **Phase 0 — Reference study & tech evaluation.** Both references
      fetched and viewed; grammar and palette extracted above; stack
      decided (motion + CSS + contained SVG; WebGL rejected with
      reasons).
- [x] **Phase 1 — Prototype build** on `/[locale]/sys/liquid-glass`:
      floating liquid nav with sliding indicator · pointer-reactive
      press/release buttons · morphing panel (open / select / dismiss)
      · layered panes + edge light + reflections · draggable distortion
      lens (SVG) · material lab sliders (frost / clarity / distortion)
      · coordinated light/dark scene toggle. All scoped `.lgx`.
- [x] **Phase 2 — Visual diff vs references, fix loop.** Compare the
      running prototype against the grammar above (see Differences
      log), fix what is off, re-verify.
- [x] **Phase 3 — Interaction & accessibility tests.** Automated
      interaction suite (nav select, panel morph states, tile select,
      mode toggle), keyboard/aria audit, reduced-motion behaviour.
- [x] **Phase 4 — Build, performance & regression verification.**
      Production build, full existing test suites, route inventory
      unchanged, no new runtime cost on production pages (prototype CSS
      loads only on the prototype route).
- [x] **Phase 5 — Site-wide rollout (APPROVED by user 14 Oct 2026,
      scoped promotion shipped).** The user approved applying the
      prototype grammar site-wide ("apply and upgrade it … full of
      motion framer etc"). Promoted, each behind contract tests:
      (1) **Framer Motion route-entry shell** — new
      `src/app/[locale]/template.tsx` (`.route-shell`, opacity/y
      entrance, reduced-motion renders children bare, noscript
      force-visible rule; guarded by `tests/route-template.test.tsx`);
      (2) **Crystal §I liquid promotion** — full-surface
      `--glass-sheen` reflection layer added under the catch light in
      the Crystal material stack (guarded by crystal-contrast
      "§I·liquid-promotion", 94/94); (3) **spring menu entrances** —
      shared `scale-in` retimed to a spring-out cubic-bezier.
      Deliberately NOT churned: the Navbar's hand-rolled sliding
      indicator (test-locked, works), the global Button (server
      component; CSS press physics already shipped — converting it
      would client-boundary every page), FeaturesShowcase's bespoke
      tab system, and the prototype's distortion lens (decorative,
      cost unjustified site-wide). NOT decided here: switching the
      default appearance family (first-run stays Clay) — a brand
      decision for the user, not a silent default. Verification:
      vitest 78/78, testcrystal 94/94, crystal-motion 17/17,
      clay-paint 249/249, test:clay 100%, tsc/lint/pointer-guard
      clean, production build OK, all key routes 200. Pixel renders
      unavailable this pass (sandbox wiped the Qt render rig and its
      system libraries); entrance/sheen verified by SSR output,
      compiled-CSS contracts and unit tests instead.
- [x] **Phase 6 — Production refinement (master execution pass).**
      Baseline-first performance work, recorded in
      `docs/LIQUID_GLASS_PERFORMANCE_AUDIT.md`: idle orb animation
      removed (no continuous decorative loops), permanent
      `will-change` removed, nested backdrop blurs flattened
      (`.lgx-glass-flat` on in-pane controls), material tokenised
      (`--lgx-blur/-sheen/-inner-glow` + mode overrides) with
      guaranteed full-surface frost behind content, SVG filter skipped
      at Distortion 0, mobile nav given a scrollable lane. Verified:
      interaction suite 7/7, full vitest 76/76, build, renders
      (dark/light/states). Mobile geometry measured in-engine at
      390 px: `scrollWidth 380 ≤ innerWidth 390`, nav scrolls
      internally (308/455) — no page overflow.
- **Rollout gate: OPENED BY USER (14 Oct 2026)** for the scoped
  Phase 5 promotion above (see audit §6). Still open items: a
  real-browser eyeball pass on frost/gloss/press physics, and the
  default-family decision. Print-path renders flatten
  `backdrop-filter` and are not accepted as proof of blur quality.

## Differences log (prototype vs references)

Observed on real Chromium renders of `/en-US/sys/liquid-glass`
(Qt WebEngine print-render harness; see Verification log):

1. **FIXED — headings rendered black on the dark scene.** The site's
   global heading colour beat CSS inheritance into the stage.
   `.lgx :is(h1,h2,h3,h4)` now speaks the scene ink in both modes;
   re-render confirms white-on-dark / navy-on-light.
2. **FIXED — the two demo sheets did not overlap.** `.lgx-glass`'s
   `position: relative` (the gloss anchor) beat Tailwind's `absolute`
   at equal specificity by source order. Sheets now carry `.lgx-sheet`
   (`position: absolute`) and overlap like the ZenSwap panes.
3. **FIXED (toned) — backdrop more saturated than ZenSwap's calm
   folds.** Reference 2 explicitly wants vibrant geometry, so the
   hybrid stays; the dominant violet orb was reduced (340px→320px,
   0.85→0.68 alpha) so panes over it stay legible.
4. **ACCEPTED — stronger text contrast than both references.** The
   references' muted grey-on-glass copy is a documented negative in
   reference 2 itself ("low contrast") and would fail this site's
   contrast contracts. Prototype ink is deliberately darker/bolder.
5. **ACCEPTED — gradient folds instead of a photographic backdrop.**
   Zero image weight, theme-reactive, and the clarity dial (55% →
   toward 100%) approaches ZenSwap's near-clear read. A photo backdrop
   can be trialled in Phase 5 if the user prefers it.
6. **NOTED — lens displacement is subtle in static renders.** The
   SVG-displaced swatch only diverges from the flat swatch when the
   lens moves or the Distortion dial rises; default raised 18 → 22.
   Judged live, not from stills.

## Verification log

### Phase 1–2 — build + visual diff (14 Oct 2026)
- Route live: `GET /en-US/sys/liquid-glass` → 200, `robots: noindex,
  nofollow`; also generated for every locale like the other catalogs.
- Visual evidence: real Chromium renders (Qt WebEngine offscreen →
  `printToPdf` → rasterised) inspected for four states: dark/open,
  light/open, dark/panel-dismissed, dark/Job-orders-selected. All four
  match the reference grammar (floating pill nav with slid indicator,
  panel pill-switch, raised selected row, layered sheets, lens over
  swatches, sliders). Differences 1–3 above were found on these
  renders and fixed; the fixed states were re-rendered and re-checked.
- Known evidence limit: Chromium's print path flattens
  `backdrop-filter`, so frost *blur quality*, pointer gloss and press
  physics cannot be proven from these stills — they are proven by the
  interaction suite (state/ARIA) and must be eyeballed on the live
  preview. No screenshot tooling ships in this sandbox (no Chromium/
  Playwright); the Qt harness was assembled ad hoc for this phase and
  its CDP mode proved unstable under sandbox stub libraries, so the
  print-render path was used. **Not claimed: pixel-perfect parity.**

### Phase 3 — interaction & accessibility (14 Oct 2026)
- `tests/liquid-glass-prototype.test.tsx` — 7/7: nav select re-fills
  panel + `aria-current`; dismiss → pill → reopen; Escape folds;
  tile select (`aria-pressed` + announcement); row select raises pill;
  light/dark scene toggle flips `data-mode`; lab sliders present and
  writable. (Runs under `MotionConfig reducedMotion="always"`: jsdom
  has no WAAPI, so Motion exits can never complete there — DOM/ARIA
  truth is asserted, fluid paint is verified live.)
- Accessibility by construction: real `<button>`s throughout,
  `aria-expanded`/`aria-controls` on the morph, `role=tablist/tab`
  with `aria-selected`, `aria-live` announcements, focus-visible
  outline, `prefers-reduced-motion` collapses Motion + CSS drift,
  `forced-colors` lane drops blur/shadows for CanvasText borders.
- `guard:pointer`: 164 files clean (gloss writes style vars only).

### Phase 4 — build & regression (14 Oct 2026)
- `npm run build` — succeeds; route table adds only
  `/[locale]/sys/liquid-glass` (noindex). No production page imports
  the prototype CSS/component, so production bundles gain nothing.
- Full suites: vitest 76/76 (6 files, incl. the new suite),
  `testcrystal` 93/93, `test:clay` 100% coverage, `clay-paint` +
  `crystal-motion` 266/266, `tsc --noEmit` clean, `eslint` clean.
- Routes spot-checked after final build: `/en-US`, `/sys/liquid-glass`,
  `/sys/crystal`, `/sys/clay` all 200.

### Addendum — §L Living Liquid + §J text-on-top (user witness, same day)

The user rejected the shipped Crystal as "still the old frosted pattern"
and named four defects; all four traced to code, all fixed at engine
level (commit follows this note):

1. **No living liquid.** The room behind the panes was a static
   wallpaper; frost blurred it into soup and nothing moved without
   scrolling. §L adds the liquid: three accent-hued blobs (brand/cyan/
   rose — a picked plate re-hues them) drifting on 42/55/66s orbits
   behind the wallpaper (`.liquid-scene`, layout-rendered, `display:none`
   outside Crystal), and the panes went clearer (frost 26/28 → 18/20px,
   utility lift 28 → 16px) so the moving colour reads through the
   glass. Reduced motion stills it.
2. **Text under the effect.** The pane `::before` water layer is
   positioned; positioned boxes paint above non-positioned text, so
   washes up to 0.40 alpha sat ON the words. §J: panes `isolation:
   isolate`, water `z-index: -1` — above the glass fill, below every
   letter.
3. **Black reads dark blue (+ every plate shifted).** Three causes:
   the Black preset was literally #111827 (slate blue); the midnight
   scrim was deep TEAL (6 26 38); the midnight wall was a violet bath.
   Black is now #0C0C0F, the scrim neutral `rgb(4 6 14 / 0.38)` (also
   stronger for text over the brighter liquid room), and Crystal's
   midnight wall is neutral-deep — pools/aurora still carry colour.
4. **Invisible text.** Same §J fix, plus the stronger neutral scrim;
   the crystal-contrast harness re-verified (97/97 incl. new §J/§L/
   palette guards).

Honest limit, unchanged: no GPU browser exists in this sandbox, so
the liquid's motion is verified by construction + compiled-CSS and
contract evidence, not by an eyeball render. The user's live-preview
pass is the visual verdict.

### Addendum 2 — §M stability, clear centre, professional wallpaper

Second witness, sharper: "UI coming going like crashing", fog still in
card centres, wallpaper "ugly pattern". Root causes, all structural:

1. **The crash-feel was a frame-budget collapse, and §L caused it.**
   The liquid blobs carried `filter: blur(48px)` on 64vmax layers
   behind hundreds of backdrop-filtered panes. Against a static wall
   the compositor caches pane blurs; a perpetually moving backdrop
   invalidates every pane's blur every frame. §M: blobs defiltered
   (gradients are pre-feathered), will-change removed, drift parks
   while scrolling ([data-liquid-rest] from glassScroll's own loop),
   economy lane stills it, phones drop the third blob — and every
   blur radius dropped (registry 26/28 → 12/16px, utility 28 → 12px),
   which is also the clarity fix below. Blur cost falls roughly with
   the square of the radius.
   *Process note, on the record: two of the previous pass's blur edits
   silently failed to land (engine bases stayed 26/28px, utility stayed
   28px) while their neighbours landed — grep-verification after every
   edit is now the working rule, and this pass re-verified each value
   in source AND compiled CSS.*
2. **Centre fog evicted.** §H painted a face-wide top pool and a
   centre band (120% 55% at 50%) in every section card. Both gone;
   all water light is corner-confined, perimeter inset glow kept —
   the centre of a pane is clear glass over the liquid, glass
   character at the edges, as directed. Refract band narrowed/dimmed.
3. **Wallpaper: pattern → room.** Crystal replaces the shared
   13-layer wallpaper (etched grid + grain + sweep + pools) with
   lamp + floor + vignette in the family's own tokens, grid retired.
   Colour and motion come from the liquid; the wall is light.

Guards: crystal-contrast 99/99 (§M tests added), crystal-motion
17/17, clay-paint 249/249, clay 100%, vitest 78/78, pointer guard
clean. Same honest limit: no GPU browser in this sandbox — frame
behaviour is engineered and contract-verified, not measured. The
live preview remains the verdict.

### Addendum 3 — §N the lag witness: austerity, idle liquid, reveal failsafe

Third witness: still lagging, cards disappearing, full-card fog. The
cross-check (motion systems read as one budget, not per-feature):

1. **Disappearing cards were the reveal system, not the glass.**
   `.reveal` cards start at opacity 0 and wait on one shared
   IntersectionObserver with NO failsafe; on a starved main thread the
   callback trails the scroll and cards sit invisible. useReveal now
   force-shows any in/near-viewport card still hidden after 1.6s.
2. **Lag was a stack of always-on repaint systems**: moving backdrop
   under ~500 backdrop-filters (re-blur per frame), per-card view()
   custom-property rides, scroll surge writes, pointer catch reads.
   §N: (a) small utility carriers — the census majority — drop
   backdrop-filter for an honest tint (at chip size blur ≡ tint);
   panes keep real glass; (b) the liquid now flows ONLY while the
   page is idle (GlassLife watchdog parks it on any scroll/pointer
   activity, resumes after 1.6s idle) — motion never competes with
   interaction for frames; (c) blur 12→10/9px, saturation 205→175%.
3. **Full-card fog**: daylight renegotiated (user-signed, harness
   re-proven): fill 0.74→0.62 (floor 0.68→0.55), lift 0.18→0.12,
   scrim 0.18→0.30 — the scrim, not the milk, carries the text; every
   daylight reading pair still clears its floor in the harness.
   Midnight registry blur → 9px; specular softened 0.32→0.26 (the
   pointer catch was a fog circle following the mouse).

Guards: crystal-contrast 99/99, crystal-motion 20/20 (§N tests),
clay-paint 249/249, clay 100%, vitest 78/78. Still no GPU browser in
this sandbox: frame behaviour is engineered + source-audited, and
the live preview remains the visual verdict.

### Unresolved / awaiting user
- Eyeball pass on the live preview for gloss, press feel, frost blur
  and lens drag — the parts stills cannot prove.
- ~~Phase 5 (site-wide rollout) is **not started** and stays gated on
  explicit user approval of this prototype.~~ **Superseded 14 Oct
  2026:** the user approved the rollout; Phase 5 shipped scoped
  (route motion, Crystal §I sheen, spring menus — see checklist
  above, commit `32c5dbc`). Still awaiting the user: the
  default-appearance-family decision (Clay remains the default;
  switching to Crystal/Liquid is a brand call, not taken silently).

### §O — the flash root cause and the clear-pane doctrine (*shipped*)

**Witness.** After `32a7166`, Crystal still flashed on the live preview:
colour cards blinked visible↔invisible, whole cards went blank, the top
panel flashed — "the theme completely breaking." The user demanded a
deep-read root-cause analysis instead of another blind patch.

**Deep read (code, not vibes).**
1. **Not a writer fight.** `useTheme.ts` has exactly one DOM writer
   (`writeDom`, boot/guard/studio all converge on it); `useStudio.ts`'s
   MutationObserver only re-applies `data-glass-vars` through rAF and
   writes no attributes. Nothing oscillates `data-appearance`.
2. **The real mechanism is compositing load.** Hundreds of
   `backdrop-filter` surfaces sample a *moving* backdrop; Chromium
   thrashes/drops those layers under sustained repaint pressure, and a
   dropped backing tile renders as a blank or flashing pane — exactly
   the witnessed symptom, Crystal-only, worse while scrolling/interacting.
3. **A hidden saboteur defeated every radius cut since §F.** A second
   `@supports` escalation block deep in the engine (~L1153 pre-§O)
   applied `blur(28px) saturate brightness` to the *entire* carrier list
   later in the cascade at equal specificity, silently overriding the
   §M/§N "blur 9/10px" readings. Greps kept finding the small number and
   reporting victory while the browser ran 28px over ~500 panes.

**The doctrine (§O).** *Content panes do not sample the backdrop at all.*
A pane is a crystal-clear bottle: translucent fill + scrim + corner
light, with the moving liquid *seen through* it un-blurred — sharper
than frost, and structurally unable to blank when the compositor drops
a backdrop tile, because there is no backdrop tile. Backdrop blur
survives only on **bounded chrome** whose count does not scale with
page content: `.nav-pill`, `.nav-sheet`, `.menu-card`, `.studio-sheet`
(14px), `.glass-bar` (16px), `.nav-glass::before` (24px), table `thead`
(10px), form fields (14px carved wells). Chips (`.chip`, `.eyebrow-chip`)
are tint-only. The 28px escalation block is deleted. The @supports-not
(no-backdrop) lane is untouched — panes were already opaque there, and
now the mainline matches its legibility.

**The other always-on repaint engine went too.** The `view()` rides —
`--glass-chill` frosting every `.section` edge and `--glass-flow`
crossing every card — animated two registered custom properties on
every card/section against scroll, forcing style+paint of every
visible pane per scroll frame. Both are retired (tokens stay
registered, resting at 0; the JS surge/drift slosh, which self-parks,
remains the water's motion). `G5·4`/`G7·4` were renegotiated in the
open to pin the rides' *absence*, and a new `§O·1` census test fails
the suite if any non-chrome selector ever declares `blur()` again.

**Honest limit.** Structural proof that panes no longer *can* blank
from a dropped backdrop tile; the perceived flash-stability on a real
GPU remains a live-preview judgement, as always in this audit.

### §P — stability doctrine: scroll writes nothing (*shipped*)

**Witness.** After §O, cards still flashed/blanked *the moment the page
scrolled*, in both modes, Crystal only; the on-page theme/colour cards
read as empty pages. Clay — same layout, same reveals — scrolled clean.

**The half §O missed.** §O removed backdrop *sampling* but left the
scroll-coupled *writer*: `lib/glassScroll.ts` ran a rAF loop for every
scroll (plus a ~1.5s decay tail), writing `--glass-surge` /
`--glass-drift` onto `<html>` per frame. Both are inherited registered
properties consumed by every pane's §H water layer (six gradients + two
inset glows), so every scroll frame restyled and repainted every
visible pane — starving raster and delaying the reveal observer, which
is why cards presented blank and then popped. Two root-attribute
systems compounded it: the stir's and the §N watchdog's
`[data-liquid-rest]` flips at scroll boundaries restyled the whole
document twice per gesture. In the hero, `HeroField` wrote `--sy` per
scroll frame into the heaviest subtree on the page (the banner
parallax), and `.hero-aurora-strong` re-ran a 42px blur per frame
forever.

**Doctrine.** *Scroll writes nothing; no interaction restyles the
document.* Motion is transform/opacity on bounded compositor layers
(the liquid drift, shard floats, the one-card pointer catch light), or
it does not move. Concretely: `glassScroll.ts` deleted (channels rest
at their registered 0 — still water); the §N idle watchdog deleted
(the drift is transform-only on three fixed spans and needs no parking
once panes do not sample the backdrop); the `[data-liquid-rest]` gate
retired from the engine; `HeroField`'s scroll listener removed (its
pointer parallax remains, now rAF-coalesced); the aurora's per-frame
filter removed (its gradients were already soft).

**Framer Motion audit (asked for explicitly).** `MotionReveal` is
one-shot (`viewport.once`) and mount-triggered above the fold;
`ModuleBrowser` layout animations are interaction-triggered (tab/filter
changes), not scroll; the route shell animates on navigation only. No
Framer surface re-runs on scroll — the scroll painters were the
hand-rolled writers above, and they are gone. The two remaining scroll
listeners app-wide (`Navbar`'s 12px scrolled flag, which flips at most
twice per gesture, and `ProductSlider`'s element-local handler) are
family-independent and were exonerated by the Clay control case.

**Locked.** G7·1–G7·3 and §N·2 renegotiated in the open to pin the
deletions (stir file absent, no scroll listeners writing styles, no
root dataset flips), §M·stability pins the rest gate's absence.
Structural proof as always; perceived smoothness remains the
live-preview judgement.

### §Q — the top-panel rebuild (*shipped*)

**Witness.** Two phone screenshots, seven seconds apart: at rest the
hero's app preview renders (its stat tiles crushed into 90px cylinders
with word-per-line text); after a short scroll the same frame shows
its header and tab strip over a vast empty field containing one stray
glass sphere — the "two cards"/empty-page complaint, located at last
in the top panel itself.

**What the code said.** (1) The frame's rail (`hidden sm:flex`, 176px)
and the dashboard's `sm:grid-cols-4` tiles were composed for desktop
widths and simply crushed inside a phone-width frame. (2) The inline
frame's height chain — AppFrame `min-h` body → LiveSurface `h-full` →
DashboardEngine `h-full min-h-0` → a `flex-1 overflow-auto` rows well —
depended on percentage-height fallbacks through an auto-height parent;
the pattern is sound in the fixed-height lightbox it was drawn for and
fragile everywhere else. (3) The hero's decor sphere/rings were placed
for the wide composition but rendered at every width, so on a phone
they float alone in empty hero background (the lone circle in the
screenshot). (4) The frame's entrance scaled the heaviest subtree on
the page. (5) `MotionReveal` — unlike the CSS reveal lane — had no
failsafe: a delayed observer callback strands SSR content at opacity 0.

**The rebuild.** Rail yields below `md`; summary tiles hold two
readable columns until `lg`; the inline dashboard path drops the
`h-full`/`flex-1` chain for natural flow with a visibly bounded rows
well (`max-h-[22rem] overflow-auto`, the pattern the optimizer surface
already used); decor sphere/rings join the materials cloud's existing
rule (`max-lg:hidden` — decor for the wide composition only); the
frame reveal rises instead of scaling; `MotionReveal` gains the CSS
lane's 1.6s near-viewport failsafe. The background animation section
itself (backdrop artwork, aurora banks, liquid drift) is unchanged in
kind — it is still transform-only — it simply can no longer be
*exposed* as empty space by stray ornaments.

### §R — the coarse-pointer stability lane (*shipped*)

**Witness.** Two more phone screenshots: at rest the hero preview
paints fully; after a short scroll, whole regions present as raw
background — the hero artwork showing through tiles the rasteriser had
not reached. The theme studio, opened, presented as an empty frosted
slab over the hero.

**Verdict.** Tile-raster starvation (checkerboarding) on the device:
Crystal's tiles were still paint-expensive after §O/§P/§Q — a fixed
liquid in continuous transform animation behind the page, six-gradient
water stacks with blurred inset glows on every section card, aurora
banks breathing over the first screen. Clay's flat stacks sail through
the same fling; the control case convicts the paint weight, not the
layout. Separately, the studio panel stacked backdrop-filter,
`overflow-y-auto` and a transform entrance on one box — the most
fragile paint combination on the page.

**The lane.** `@media (pointer: coarse)` at the engine's tail: the
liquid holds still (its pools still read as colour through the clear
panes), card water reduces to the §C1 two-gradient corner sheen with
no inset glows, aurora banks rest. Tiles paint once and cache; the
rasteriser keeps up because there is little left to rasterise. Fine
pointers keep the full living liquid — this is a device budget, not a
redesign. The studio's frosted shell and its scroller are now
different boxes, and the scroller fades (opacity) instead of scaling.
G6·1 renegotiated in the open to allow exactly the lane's three
properties beside the two dials.

### §S — the touch budget (*shipped*)

**Witness.** The owner, on the device, after §R: "still not fixed
properly" — with a process correction this project takes seriously:
stop patching symptoms; read the real code; find the proper issues.

**The audit.** The whole paint-critical path read end to end, not
grepped: `crystal-engine.css` (1510 lines), the paint regions of
`globals.css` (wallpaper, surface registry, nav, reveal utilities,
hero tail), `[locale]/layout.tsx` + `template.tsx`, `Navbar.tsx`,
`ThemeStudio.tsx`, `Reveal.tsx`/`useReveal.ts`,
`MotionReveal.tsx`, `LiveSurface.tsx`, `AppFrame.tsx`, `GlassLife.tsx`
+ `lib/glassLife.ts`, `lib/useStudio.ts` (observer region),
`SupportBubble.tsx`, `Container.tsx`.

**Exonerated — recorded so nobody re-investigates them.** No CSS rule
hides Crystal content (no `opacity: 0` on content anywhere; panes are
explicitly unblurred). `.reveal` releases `will-change` at
`.is-visible` (documented in place). The wallpaper carries no
`will-change`. `glassLife` writes nothing on a coarse pointer
(fine-pointer gate verified). The studio, sheets and support panel
render their content unconditionally — no JS can empty them.
`useStudio`'s observer watches theme attributes only. The route shell
settles in 320ms. §P's doctrine — scroll writes nothing — held
everywhere the audit looked.

**What was actually still spending the frame at scroll.**
1. **The sticky bar sampled the page.** `[data-appearance=crystal]
   .nav-glass` ran `backdrop-filter: blur(var(--surface-blur))
   saturate(...)` — 24px × the studio Frost dial — over everything
   sliding beneath the fixed bar, re-sampled every scroll frame; the
   pill carried a second 22px sampling where it renders. Backdrop
   re-sampling mid-fling is the most expensive scroll-time feature on
   Android Chrome, it is Crystal-only, and §O's "bounded chrome"
   exemption never priced its scroll cost. This — not the transform
   drift — is the remaining scroll-coupled painter.
2. **§R's full stop of the liquid bought little**: the drift is
   transform-only (cached tiles, compositor-translated; it never
   repaints), so stilling it freed almost no raster budget while
   costing the product its defining motion.
3. **The studio faded its content inside the scroller** (§R
   structure): the shell could present before its content painted.
4. Hero `will-change` hints stood on layers whose transforms only run
   under `[data-live]` (fine pointers) — phones promoted layers for
   motion that can never run there.

**The budget (all in the `@media (pointer: coarse)` lane + one JSX
class move).** The bar and pill take solid film at the codebase's own
no-backdrop fallback fills (`ink-950/0.96`, `ink-900/0.92`) — bar
text gets more legible, not less; fine pointers keep the glass bar.
The brand disc (`span 1`, its shipped 20s transform drift) drifts
again on touch; discs 2–3 rest as still pools; `html.glass-econ`
still stills everything on weak hardware (its selector outranks the
lane). §R's thinned card water is kept. The studio's entrance fade
moved from the scroller to the shell, so panel and content paint as
one composited surface from frame 0; the scroller never animates.
Hero hints now live under `[data-live]` with the transforms they
describe. Fine-pointer behaviour is unchanged everywhere.

**Locked.** `crystal-motion` §S·1/§S·2 (bar film; drifting disc,
transform-only), `tests/motion-reveal.test.tsx` §R+§S block (lane
shape, studio entrance, hero hints — §R's two guards renegotiated in
the open above their replacements). All prior suites green.

**Honest limits.** No GPU browser exists in the sandbox, so nothing
here is device-proven; the proof is structural (the scroll path now
contains no per-frame painter at all on touch: static card tiles, one
compositor-translated gradient disc, a solid bar) plus the live
preview. If the witness phone still blanks after §S, the next
evidence to gather is the device itself: model/GPU, Chrome version,
and a screen recording — the code's scroll path has nothing left that
repaints by construction.
