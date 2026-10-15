# Liquid Glass — Performance & Architecture Audit

> Companion to `docs/LIQUID_GLASS_PROTOTYPE.md`. Written **before** any
> refinement code, per the master execution order: diagnose first,
> optimize with evidence, refine the material, verify in a real
> browser, roll out only after the prototype proves itself.
> Every number below is labelled **measured** (and how) or **estimate**.
> Nothing is claimed from code inspection alone.

## 1. Current architecture (confirmed by inspection, 14 Oct 2026)

- **Framework**: Next.js 16.3.6 (App Router), React 19, `motion` 14
  (Framer Motion) as the only animation library, Tailwind 3.
- **Routing**: every page under `src/app/[locale]/…` (19 locales);
  layouts nest `I18nProvider` → chrome (`Navbar`, `Footer`,
  `ThemeStudioLazy`, `SupportBubble`) → page. Internal review surfaces
  live under `/[locale]/sys/{clay,crystal,liquid-glass}`, all
  `noindex` and excluded from the sitemap.
- **Theme system**: three independent engines, one file each —
  `clay-engine.css` (default family), `crystal-engine.css`,
  `globals.css` (tokens/shared surfaces) — selected by
  `data-appearance` on `<html>`, with `data-theme` midnight/daylight
  orthogonal to family. The Liquid Glass prototype is a fourth,
  deliberately isolated material: all selectors scoped `.lgx`, CSS
  imported only by its route, component in
  `src/components/glass/LiquidGlassPrototype.tsx`.
- **Existing live-glass systems** (Crystal family): §C5 pointer catch
  light (`lib/glassLife.ts`, one rAF-coalesced pointermove listener),
  §H2 scroll-stirred water (`lib/glassScroll.ts`, one passive scroll
  listener, demand-driven rAF that parks at rest), §C5 view-chill
  (CSS `animation-timeline: view()`), §F6 economy lane
  (`html.glass-econ` halves frost on weak devices). These are
  Crystal-only and do not run in the default Clay family.
- **Server/client boundaries**: pages are server components; client
  islands are the chrome (nav/studio), dashboards, and motion
  components. The prototype is one client component on an internal
  route — production routes import none of it.

## 2. Baseline measurements (production build, warm loopback)

_Measured 14 Oct 2026 against `next start` on this machine. Loopback
timings exclude real network/device variance — they are a floor, not
a field measurement._

| Metric | Value | Method |
|---|---|---|
| Total client JS (all chunks, uncompressed) | **2.4 MB** | `du` over `.next/static/chunks` |
| Largest single JS chunks | 239 / 229 / 181 / 163 KB | same |
| Largest CSS chunk | **251 KB** | same (globals + engines ship as one sheet per route group) |
| Homepage HTML | **771 KB** (TTFB 19 ms loopback) | `curl` |
| — of which inline script text | **~348 KB** (31 blocks; 4 ≈ 73 KB each) | regex census of served HTML |
| Prototype page HTML | 159 KB (TTFB 6 ms) | `curl` |
| Crystal blurred surfaces, worst page | **531** (`/features`; budget 600) | `npm run crystal:census` |
| `backdrop-filter` declarations | crystal-engine 45 · globals 24 · clay 2 · prototype 3 | source census |
| Scroll/pointer loops at idle | 0 (both liquid listeners park) | source + behaviour tests |

### What the baseline says
1. **The heavy glass cost is Crystal-mode surface count, not the
   prototype.** 531 backdrop-filtered surfaces on one page is the
   single largest rendering liability found. It only exists when a
   visitor switches family to Crystal; the default Clay experience
   carries none of it. The engine already self-polices with
   `crystal:census` (budget 600) — it is near the line.
2. **Homepage HTML is obese**: ~348 KB of inline script (RSC flight
   payload + locale overlay data) inflates parse and hydration cost
   on the most-visited route. This is a site-wide finding, unrelated
   to Liquid Glass, and out of this track's mandate to change — it is
   recorded here as the top follow-up investigation.
3. **The prototype itself is not the lag**: 3 backdrop declarations,
   one route-scoped client component, no idle loops. Its known sins
   are enumerated in §3 and fixed in the refinement phase.

## 3. Confirmed issues in the prototype (evidence: source + renders)

| # | Issue | Evidence | Fix class |
|---|---|---|---|
| P1 | Continuous idle animation: backdrop orbs drift infinitely (`lgx-drift`, transform on blurred orbs = repaint every frame, forever) | `liquid-prototype.css:77` | Remove idle motion (master §6.1) |
| P2 | Permanent `will-change: transform` on every `.lgx-btn` | `liquid-prototype.css:166` | Remove (no evidence it helps; compositor memory) |
| P3 | Nested backdrop blurs: pill switch + circular buttons sit *inside* already-blurred panel/nav and blur again | component markup + CSS | `.lgx-glass-flat` variant (fill/edge, no second blur) |
| P4 | Material values hard-coded per rule; no token layer to keep frost consistent | CSS inspection | Tokenise (`--lgx-blur/-tint/-edge/-sheen/-shadow`) |
| P5 | SVG displacement filter runs even at Distortion = 0 | component | Skip `url()` filter at 0 |
| P6 | Floating nav can overflow a 360 px stage (4 links + brand + toggle in one pill) | layout math; to confirm on mobile render | Scrollable/wrapping nav lane + tighter mobile padding |

## 4. Suspected bottlenecks not yet measurable here

- **LCP / INP / CLS / frame-time distribution / missed frames** —
  *unmeasured*. This sandbox has no Chrome, Playwright, GPU, or
  physical display; the Qt WebEngine harness used for renders is
  software-composited and its frame pacing would be fiction if
  reported as device truth. Per the master rule, simulated FPS is
  never reported as measured FPS.
- **Scroll jank in Crystal mode** on real devices (531 blurred
  surfaces repaainting under scroll) — plausible, unquantified.
- **Hydration cost of 348 KB inline payload** on low-end mobile —
  plausible, unquantified.

### Missing tooling (explicit)
Chrome DevTools Performance · Lighthouse · React DevTools Profiler ·
Playwright/WebPageTest · any 90/120/144 Hz display. **Collection
plan**: run Lighthouse + a manual DevTools performance pass on the
live preview from a real browser (user-side), or add a Playwright CI
job later; numbers then get appended here with device/browser/refresh
recorded.

## 5. Refinement phases & acceptance criteria

| Phase | Work | Exit criteria |
|---|---|---|
| R1 | Fix P1–P3, P5 (idle motion, will-change, nested blur, filter-at-0) | Source-verified; interaction suite still green; renders unchanged in identity |
| R2 | Material tokens + guaranteed full-surface frost (P4) | `.lgx-glass` = token-driven frost layer *behind* content on 100% of the card; content never blurred (no filter on content nodes — asserted by inspection + tests) |
| R3 | Mobile nav lane (P6) + responsive renders | 390 px render: no horizontal overflow, controls reachable |
| R4 | Frame-pacing hygiene | No idle rAF/animation in prototype; Crystal loops already park (regression-tested) |
| R5 | Re-verify: build, full suites, renders (dark/light/states/mobile) | All suites green; build output compared to baseline (no production-route growth) |
| R6 | **Rollout gate decision** | See §6 |

### Refinement results (14 Oct 2026, same day)
- **R1 done**: P1 idle orb drift deleted (keyframes removed; orbs are
  static paint now), P2 `will-change` deleted, P3 `.lgx-glass-flat`
  applied to the three nested controls (nav toggle, panel dismiss,
  panel pill switch), P5 filter skipped at Distortion 0.
- **R2 done**: `.lgx-glass` is token-driven; the frost stack (sheen +
  tint gradient + `blur(var(--lgx-blur)) saturate(165%)`) covers the
  full card surface, seated on the surface element behind content.
  No filter ever touches a content node (structure unchanged, tests
  assert content/ARIA state).
- **R3 done with a caveat**: the 390 px print render *looks* clipped,
  but in-engine measurement proves the screen layout is correct
  (`scrollWidth 380 ≤ 390`; nav internal scroll 308/455). Print
  pagination at phone width is not a screen — the discrepancy itself
  is recorded so nobody "fixes" a phantom overflow later.
- **R5 done**: production build passes; prototype HTML 158.9 KB
  (baseline 158.9 KB — unchanged), vitest 76/76, `testcrystal` 93/93,
  tsc/lint clean, pointer guard clean. Desktop dark render after
  refinement: identity preserved, sheen layer visible on card tops.
- **Still unmeasured (unchanged)**: LCP/INP/CLS/frame times on real
  hardware — no GPU browser exists in this sandbox (§4). The gate
  stays blocked on that evidence, by design.

## 6. Rollout gate (master Phase 10) — status

**Decision: OPENED BY USER (14 Oct 2026)** for a scoped promotion.
The user reviewed the refined prototype and directed: "apply and
upgrade it, proceed to phase 5 and others, make it professional,
full of motion framer etc." Shipped in that pass (see the prototype
checklist, Phase 5): Framer Motion route-entry shell on all locale
routes, Crystal §I full-surface sheen promotion, spring menu
entrances — each behind contract tests, full regression green.

Still open, honestly stated:

1. No real-browser frame-pacing or LCP/INP evidence exists yet
   (tooling absent in this environment — §4). The promoted pieces
   add no new backdrop filters and no idle animation, so the §2.1
   Crystal surface-count liability is unchanged, not multiplied.
2. Backdrop-filter fidelity (frost diffusion, gloss, press physics)
   has not been eyeballed in a GPU browser; print-path renders
   flatten `backdrop-filter` and are not accepted as proof.
3. Switching the DEFAULT appearance family from Clay to
   Crystal/Liquid remains a separate brand decision for the user —
   not taken silently in this pass.

Unblocked and verified: prototype responsiveness by construction,
full-card frost implementation, interaction suite, build +
regression (vitest 78/78, testcrystal 94/94, crystal-motion 17/17,
clay-paint 249/249, clay coverage 100%, tsc/lint/guards clean).
## 7 · Motion-performance pass (§AA — scroll lag, card reveals, one system)

A full re-audit of the motion layer found the architecture sound and the
parameters guilty. Single animation library (`motion` 14.2.0), two reveal
lanes with one grammar, token-synced CSS/JS scale, one-shot observers,
no scroll-position state anywhere. What was broken was values.

### 7.1 · Confirmed root causes

| # | Cause | Evidence |
|---|---|---|
| 1 | Uncapped stagger cascades: ~15 grids passed `delay={index * 50–70}` with no ceiling; a 12-card grid's tail began ~770ms after entering view, while only ~5 call sites capped inconsistently at 5–6 items | delay census across `src/` |
| 2 | 1600ms invisible-content failsafes in BOTH reveal lanes — on a busy main thread a card could sit at opacity 0 well past its entrance | `useReveal.ts`, `MotionReveal.tsx` |
| 3 | Trigger disagreement between the lanes billed as one grammar: CSS lane 12% / −10% foot margin, Motion lane 12% / no margin | `useReveal.ts` vs `MotionReveal.tsx` |
| 4 | Page-transition drift: hardcoded 0.32s and `ease [0.22, 0.61, 0.21, 1]`, a curve that exists nowhere in the token scale | `template.tsx` |
| 5 | No named presets (section/card/hover/nav/dropdown/dialog/page); surfaces hand-rolled values | `lib/motion.ts` |

Audited and exonerated: `ModuleBrowser`'s AnimatePresence + layout
springs (filter changes only, non-scroll), the accordion's
`grid-template-rows` transition (no JS measuring), `useCountUp`
(one-shot), the Navbar's CSS transitions (de facto nav preset, settled
code — untouched), all `will-change` short-lived, no scroll listeners
that write state (§Z's passive rest-gate only), no smooth-scroll system,
no per-scroll React state.

### 7.2 · Fixes (all inside the existing choke points)

- **Stagger law** (`lib/motion.ts`): `stagger { stepMs: 45, capMs: 200 }`
  + `staggerDelay(index)`. Both lanes clamp whatever a call site passes,
  so the ~40 uncapped call sites inherited the fix without being
  rewritten; a future uncapped call site cannot regress it.
- **Failsafes**: `revealFailSafeMs = 1000` shared by both lanes.
- **One trigger geometry**: both lanes now 12% visible with a −4% foot
  margin (CSS lane −10% → −4%; Motion lane gains the margin).
- **Presets**: `presets { section, card, hover, nav, dropdown, dialog,
  page }` in `lib/motion.ts`; `template.tsx` now programs against
  `presets.page` (280ms, standard ease). CSS-side surfaces already use
  the mirrored `--dur-*`/`--ease-*` tokens (token sync test).
- **Guards**: §AA locks in `tests/motion-reveal.test.tsx`; the §Q
  failsafe guard was renegotiated from the literal 1600ms to the token.

Native scrolling untouched; no hijacking, no artificial smoothing, no
new dependencies, no design-system changes. `prefers-reduced-motion`
paths in both lanes and the template are unchanged, and reveal content
is server-rendered (57 SSR `.reveal` instances on the homepage) with a
`<noscript>` force-visible sheet — content never depends on animation
completion.

### 7.3 · Before / after

| Measure | Before | After |
|---|---|---|
| Worst reveal delay, grid item 8–16 | 560–1,050ms (uncapped `index × 70`) | ≤ 200ms (clamp) |
| Hidden-content failsafe | 1,600ms | 1,000ms |
| Reveal trigger (both lanes) | −10% / none (disagreed) | 12% / −4% (aligned) |
| Total client JS chunks (uncompressed) | 2,377,862 B | 2,292,112 B (three largest chunks byte-identical; delta is split variance, no dependency added) |
| Homepage HTML | 790,784 B | 790,784 B (unchanged — preserved) |
| Frame times / FPS | unmeasurable in sandbox (§4) | still unmeasurable — device check is the arbiter |

### 7.4 · What was actually tested

- Suites: vitest 97/97, crystal contrast 111/111, crystal motion 25/25,
  Clay 33/33 @ 100%, Navbar 2/2, pointer-state guard (164 files),
  motion-token sync 2/2, ESLint clean, typecheck clean, production
  build ok.
- Served (production preview): `/`, `/features`, `/records`,
  `/how-it-works`, `/optimization` all 200 with full SSR HTML; new
  trigger geometry present in the shipped JS; noscript force-visible
  rules present in SSR.
- Code-verified states: slow/fast scroll (one-shot observers, capped
  delays), scroll-back (reveals never reset — `once` everywhere),
  viewport-boundary starts (aligned 12% / −4%), route navigation
  (`presets.page`), dropdown/dialog timings (token-based, unchanged),
  light/dark + Crystal/Liquid (§Z behaviour untouched by this pass),
  reduced motion (plain-render paths in both lanes).
- NOT verified, same limit as §4: actual frame times on a GPU device,
  fast phone flick-scroll cadence, mid-end Android compositor cost.
  Scroll smoothness on the user's device remains the judge; nothing
  here claims otherwise.

### 7.5 · Remaining known problems

- The ~791KB homepage HTML (mostly inline i18n/RSC payload) is still
  the largest single delivery cost; untouched by this pass.
- The animation library is inside the shared first-load chunks; motion
  is nearly all CSS-driven already, but a first-load byte diet needs
  its own pass with real-network measurement.

