# Crystal Engine Audit — scroll splash, heading legibility, engine health

**Date:** 15 Oct 2026 · **Scope:** `website/` only · **Status:** FINDINGS ONLY —
no code changed by this audit. Phases in §6 await approval.

**Method.** Read in full: `src/app/crystal-engine.css` (all 1,636 lines),
`src/components/glass/GlassLife.tsx`, `src/lib/glassLife.ts`,
`src/app/[locale]/developer/page.tsx` (the page in the witness screenshots),
plus the heading/label definitions in `globals.css` and the label census
across `src/`. Cascade outcomes proven by selector specificity + source
order. Contrast computed with the WCAG formula from the shipped token
values against the backend composites §Y measured (disc cores, disc+lamp
stack, and those seen through a midnight pane: scrim 0.38 + lift 0.05).
Findings verified at HEAD `e0979a4` (§AA) — they are independent of the
motion-system pass.

---

## 1 · Verdict

The engine's architecture is sound (isolation, fallback lanes, one-edge
law, text-on-top seating all hold). Three real defects remain, and the
first one fully explains the user's screenshot witness:

1. **The §Z scroll-rest is defeated on touch by a CSS cascade tie** — the
   one liquid disc that still animates on phones never actually pauses
   while scrolling. Colour keeps swimming behind every card. (§2·A1)
2. **Small heading labels (`.eyebrow`, `.mono-label`, raw micro-labels)
   sit below the contrast floor exactly where the room is brightest** —
   2.4–4.5:1 at the lamp/disc stacks, and because the liquid drifts, any
   given label rotates in and out of legibility. That is the "~10% of
   headings blurred or not visible" witness. (§3)
3. **Two chrome surfaces still sample the backdrop on touch** (`.nav-sheet`,
   `.glass-bar`), missed by every film census so far. (§2·A2–A3)

## 2 · Scroll splash & bounce

### A1 · CRITICAL — §Z's pause loses the cascade on coarse pointers

The pause (§Z, crystal-engine.css:1409):

```css
[data-appearance="crystal"] .liquid-scene[data-liquid-rest="1"] span {
  animation-play-state: paused;
}
```

Specificity: three class-level selectors + one element = **(0,3,1)**.

The touch lane's disc-1 rule (§S, crystal-engine.css:1584, *later in the
file*):

```css
[data-appearance="crystal"] .liquid-scene span:nth-child(1) {
  animation: liquid-drift-a 20s ease-in-out -9s infinite alternate;
}
```

Also **(0,3,1)** (attribute + class + pseudo-class + element). Equal
specificity → source order decides → the `animation` **shorthand** wins,
and a shorthand resets `animation-play-state` to `running`. On every
touch device, the brand disc (64vmax, the largest and brightest mover)
therefore **keeps drifting during scroll**, behind midnight panes whose
0.05 lift + 0.38 scrim still show ~57% of it, unblurred (content panes
sample no backdrop since §O — the colour is *sharp*). On fine pointers
§Z works (no competing shorthand after line 1409), which is why desktop
scroll rests and phone scroll splashes. `html.glass-econ` (0,3,2) still
outranks both, so weak-device stilling is unaffected.

The §Z regression guards pinned the pause rule's *presence*, not its
cascade victory — the exact blind spot this bug lived in.

### A2 · MEDIUM — `.nav-sheet` still samples on touch

§O's chrome set (crystal-engine.css:794) gives `.nav-sheet` a 14px
backdrop blur. The coarse film lane (§S/§X/§Z) filmed `.nav-glass`, its
`::before`, `.nav-pill`, `.menu-card`, `thead`, inputs, `.studio-sheet`
— never `.nav-sheet`. The mobile nav sheet (Navbar.tsx:774) is a live
sampler whenever the sheet is open over a scrolling page.

### A3 · LOW — `.glass-bar` still samples on touch

16px blur (crystal-engine.css:899), absent from the film lane. Today it
is used only on the `/sys/crystal` showcase page, so the cost is
confined — a census gap, not a field defect.

### A4 · MEDIUM (perceptual) — entrance rise reads as bounce on phones

Cards still enter with a 12px rise + fade (both reveal lanes; §AA capped
their delays, not the geometry). During a fling this is a continuous
stream of rising cards — motion the eye reads as bounce layered on the
scroll itself. Fine pointers can afford it; touch cannot.

### A5 · NOTE — fixed-layer viewport jitter (platform)

`.liquid-scene` and the nav are `position: fixed`. Mobile browsers
resize the visual viewport as URL chrome hides/shows mid-scroll, and
fixed layers can bob a few px with it. Soft 64vmax gradients hide most
of it; it is platform behaviour, listed for completeness, not a code
defect we can remove.

### Cleared by the read (not suspects)

- `GlassLife.tsx` / `glassLife.ts`: two attribute writes per gesture at
  most, passive listener, fine-pointer-gated catch light, no React state,
  no per-scroll work. The §P doctrine holds in code.
- `--glass-surge` / `--glass-drift`: writers were deleted in §P; the
  registered properties rest at 0. Still water, no per-frame restyle.
- `body::after` wallpaper: `position: fixed` (globals.css:1166),
  three static gradients — painted once, composited.
- `LogSurface`/preview panes (the screenshot windows): no backdrop
  filters of their own.
- `.nav-glass` + `::before`, `.nav-pill`, `.menu-card`, `thead`, form
  wells, `.studio-sheet`: filmed on coarse (§S/§X/§Z) — verified present.
- Reveal observers: one-shot, never reset on scroll-back (§AA verified).

## 3 · Heading & label legibility (Midnight Crystal)

The failing pattern is the small caps label tier — `.eyebrow`
(brand-300, 10px), `.mono-label` (text-3, 10px), and raw utility
micro-labels (`font-mono text-2xs … text-text-faint`, `text-text-faint`
stage/date/source labels; `text-text-faint` alone appears 453× site-wide
including microcopy). Big headings are white and fine; Daylight is fine
(dark ink tokens on light). Computed contrast (WCAG; backends: LAMP =
brightest bare stack rgb(42,117,143) per §Y; PANE-LAMP = that stack
through the midnight pane; PANE-DIM = through the pane over a dim core):

| Label pattern | bare LAMP | PANE-LAMP | PANE-DIM | Floor |
|---|---|---|---|---|
| `.eyebrow` brand-300 #BEA5F0 | **2.4:1 ✗** | **3.9:1 ✗** | 4.9:1 | 4.5 |
| `.mono-label` text-3 #D7D0F1 | **3.5:1 ✗** | 5.6:1 ✓ | 7.1:1 ✓ | 4.5 |
| raw `text-text-faint` text-4 #C2B9E2 | **2.8:1 ✗** | **4.5:1 ✗~** | 5.6:1 ✓ | 4.5 |
| candidate brand-200 #D6C7F6 | 3.3:1 ✗ | **5.3:1 ✓** | 6.7:1 ✓ | 4.5 |

Findings:

- **B1 · HIGH.** The eyebrow — the section-heading pattern on every
  page — fails everywhere except over dim cores. §Y climbed translucent
  accents *to* brand-300 believing it the safe rung; at the bright
  stacks brand-300 itself is under the floor.
- **B2 · MEDIUM.** `text-text-faint` micro-labels sit on the 4.5 line
  in-pane and under it bare. 453 usages; only the label-shaped ones
  (dates, stages, sources, file lists) are headings in effect.
- **B3 · HIGH.** `.mono-label` passes in-pane but fails bare (section
  rules, sticky rails sit directly on the wallpaper).
- **B4 · MEDIUM.** Raw utility labels (`font-mono text-2xs …
  text-text-faint`) carry **no halo at all** — the §Y halo covers only
  the `.mono-label`/`.eyebrow` classes, so identical-looking labels get
  different protection depending on how they were authored.
- **B5 · MEDIUM.** The halo itself contributes the word "blurred": a
  10px/0.6 blurred shadow behind 10px glyphs softens their edges.
  Protection and smudge in one rule; the blur radius is the problem,
  not the concept.
- **B6 · Why "~10%":** the failures are positional, not fixed. The
  discs drift on 20–36s orbits, so a given label is legible over a dim
  core and washed when a bright core or the lamp rotates behind it.
  Any screenshot catches whichever labels are currently in bright
  zones — a rotating tenth of the headings on the page.
- **B7.** Gradient display text (`§F5` h1, `.text-gradient`) is exempt
  from the halo by design and washes at its 72% stop over bright
  stacks — large text, lesser defect, noted.

## 4 · Engine bugs & rot (found by the full read)

- **C1 · Doc rot with a live paint consequence: none — but misleading.**
  The §H header (crystal-engine.css:~645) still documents
  `--glass-surge`/`--glass-drift` as "written on <html> by
  lib/glassScroll.ts". That file was deleted in §P; the channels rest
  at 0. `@keyframes glass-flow` + `@property --glass-flow` likewise
  survive their retired ride. Harmless paint, false documentation.
- **C2.** Dead rule: `[data-appearance="crystal"] .chip { /* was 12px
  backdrop blur */ }` — an empty rule kept as a comment's hostage.
- **C3.** Dead token: `--glass-meniscus` is defined twice (midnight +
  daylight) and consumed nowhere (grep across all three stylesheets).
- **C4.** Duplicated declarations: input backdrop blur is declared in
  §C2 and again in the §F retune (14px/saturate(180%)) — the second
  wins everywhere; the first is inert weight.
- **C5 · LOW.** `h1` gradient ink (§F5) has no forced-colors lane —
  under `forced-colors: active`, background-clip:text with a forced
  Canvas background can render headings invisible in some engines.
  The §F8 lane covers panes, not the gradient headings.
- **C6 · Verified OK (suspicion cleared).** `.glass-rows`' border uses
  the globals `--card-edge` — that *is* the one permitted edge (the
  pane's own border), not a second line. No action.

## 5 · Missing optimizations & what is healthy

**Missing:**
- **D1.** The coarse film census never became a *complete* census —
  it grew by witness (bar → pill → menu → thead → inputs → sheet)
  instead of by enumeration. A2/A3 are its residue. The engine has a
  census script (`crystal:census`); its static count (531 surfaces,
  /features) does not model the coarse lane at all.
- **D2 · Minor.** Every lifted pane paints a 6-layer background stack;
  the catch layer's default centre (−400%) still rasterizes a
  full-pane radial gradient per pane at first paint. Static and
  tile-cached, but it is paint bought for a pointer that may never
  arrive (and never does on touch).
- **D3 · Optional, deferred.** Below-fold `content-visibility` exists
  only for the gallery (`.defer-render`). Long pages (developer:
  ~830 lines of sections) raster everything up front. Any extension
  must stay off above-the-fold content (the hero rule) — separate
  decision, not bundled into a fix pass.

**Healthy (verified, do not touch):** family isolation
(`[data-appearance="crystal"]` on every rule); `@supports not
(backdrop-filter)` lanes raise fills correctly in both modes;
reduced-motion / reduced-transparency / forced-colors lanes exist;
§J seats text above the water layer; the one-edge law holds (no rings,
no masked frames); corner-safe insets intact (`.glass-row` padding
formula); the §F6 economy lane; static wallpaper (`body::after`
fixed, 3 gradients); the GlassLife JS budget.

## 6 · Phases as proposed

1. **Splash cure (A1)** — restate the §Z pause inside the coarse lane
   after the disc-1 shorthand; cascade-aware guard (order, not
   presence).
2. **Heading legibility (B1–B5)** — `.eyebrow` → solid brand-200 in
   Midnight Crystal; halo tightened (crisp core); raw faint label
   sites on the witness pages climb to the muted tier; contrast suite
   gains in-pane pairs.
3. **Touch scroll budget (A2, A3)** — `.nav-sheet` + `.glass-bar`
   join the coarse film lane; census re-run and recorded.
4. **Calm entrances on touch (A4)** — coarse reveals opacity-only in
   both lanes; fine pointers keep the rise.
5. **Engine hygiene (C1–C4)** — dead code/docs removed. *Not
   approved this pass; deferred.*
6. **Verify & ship** — suites, build, served-output checks, docs.

## 7 · Resolution (§AB — Phases 1–4 applied; Phase 5 deferred by the user)

Applied 15 Oct 2026 per the user's selection (Phases 1–3 + 4):

- **Phase 1 (A1):** the pause is restated inside the coarse lane,
  after the disc-1 shorthand. Guard: crystal-motion **G7·6** asserts
  the restatement's source ORDER (the cascade victory), its `paused`
  value, and both Phase-3 films. G6·1's property allowance was
  renegotiated in the open to admit `animation-play-state` for
  exactly this. At rest the attribute is absent — the brand disc
  still drifts on touch exactly as §S decided; it now truly parks
  while scrolling.
- **Phase 2 (B1–B5):** `.eyebrow` → solid brand-200 in Midnight
  Crystal (engine-scoped; Daylight and other families untouched, no
  `--text-*` token written); halo tightened from a 10px bloom to a
  3px core at the same 0.60 alpha (the §Y·halo guard renegotiated to
  the tightened string); six raw `text-text-faint` label sites on
  /developer and /how-it-works climbed to the muted tier (prose
  microcopy deliberately left). Contrast suite gains three in-pane
  §AB pairs (brand-200 / text-3 / text-4 × brightest disc through
  the pane stack) — all pass ≥ 4.5:1; the contract's pair count was
  renegotiated 93 → 96 in its completeness lock.
- **Phase 3 (A2, A3):** `.nav-sheet` (ink-900/0.97) and `.glass-bar`
  (ink-900/0.94) filmed on coarse. Census re-run: 531 static surfaces
  on the worst page, unchanged as expected (the census counts
  surfaces; the film changes touch behaviour). No un-filmed coarse
  samplers remain above 0px blur.
- **Phase 4 (A4):** coarse reveals are opacity-only in both lanes —
  a `(pointer: coarse)` rule drops `.reveal`/`.reveal-scale`
  geometry in globals; `MotionReveal` swaps to an opacity-only
  hidden variant on the same signal (matchMedia-guarded so engines
  and test DOMs without it keep the standard lane). Guarded by
  vitest §AB.

**Verification.** vitest 98/98 · crystal contrast 114/114 (96 pairs)
· crystal motion 26/26 · clay 33/33 @ 100% · navbar 2/2 · pointer
guard (164 files) · token sync 2/2 · typecheck and lint clean ·
production build ok. Served: `/`, `/developer`, `/how-it-works`,
`/features` all 200; shipped CSS carries both pause rules (×2), the
brand-200 eyebrow, and the nav-sheet film.

**Not verified (stated plainly):** frame times, FPS and the felt
scroll cadence on a real GPU phone — this sandbox has no GPU
browser, the same limit the audit opened with. A1's removal is
proven by cascade arithmetic and pinned by an order-asserting test;
whether the scroll now *feels* clean is the user's device verdict,
and the rotating-wash headings should now hold their contrast at
every point of the liquid's orbit.