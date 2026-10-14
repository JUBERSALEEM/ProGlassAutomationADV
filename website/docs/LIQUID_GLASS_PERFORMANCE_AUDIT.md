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

**Decision: BLOCKED.** Blocking items, honestly stated:

1. No real-browser frame-pacing or LCP/INP evidence exists yet
   (tooling absent in this environment — §4).
2. Backdrop-filter fidelity (frost diffusion, gloss, press physics)
   has not been eyeballed in a GPU browser; print-path renders flatten
   `backdrop-filter` and are not accepted as proof.
3. Crystal surface-count liability (§2.1) must be understood before
   any rollout multiplies blurred surfaces further.

Unblocked and verified: prototype responsiveness by construction,
full-card frost implementation, interaction suite, build + regression.
Rollout begins only after the user validates the live preview in a
real browser and the gate items above are closed or explicitly
accepted in writing in this document.
