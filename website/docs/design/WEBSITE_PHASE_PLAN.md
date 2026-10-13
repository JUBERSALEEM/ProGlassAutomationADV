# WEBSITE_PHASE_PLAN — Phase 0 output

Commit `d6c5514` · 2026-10-13 · This is the program-of-record for the master-prompt engagement.
Ground rules carried from the master prompt: evidence before claims, phases verified before the next, one phase = one commit = one push on `arena/ff328a9c-proglassautomationadv`, no visual-verification claims without a browser, no 100/100 without rubric + measurements.

## 0 · Program posture (decided at Phase 0, with reasons)

1. **Extend, don’t rebuild.** The repository already runs a genuine design system: two material engines (claymorphism + crystal glass) over a shared token grammar (~793 tokens), five appearance families × two modes, a live re-theming studio, and a harness architecture (paint 175, contrast 25+87, census, coverage) that executes against the built bundle. Master Phases 2/3/5 are implemented as *upgrades inside this system*, not a reskin.
2. **Motion: CSS-first, documented.** Corroborated by dependency ledger: **no animation library is installed**. The house pattern (CSS keyframes + custom-property choreography + shared observers) already satisfies the master’s motion principles including reduced-motion. **Decision: do not add motion/react or GSAP at baseline.** A library is added only when a phase dumps a choreography requirement CSS cannot express (scroll-scrub timelines, interruptible spring handoffs); the phase must then carry a benefit/cost record per master Phase 4. This complies with “Prefer Motion for React using the installed, compatible package *where appropriate*” — none is installed, and none is (yet) appropriate.
3. **Verification ladder (what counts as proof here):** prod build → tsc/eslint → vitest (DOM click-throughs where interaction matters) → paint/contrast lexicon + census + coverage + pixel-pass on the built bundle → prod-server route crawl → DOM/manifest measurements → `check:live` (where network allows) → browser screenshots (only from a session with a browser; until then every visual claim is marked inspection-limited).
4. **Sales of scope stay honest:** WPF app untouched (rule 9); Supabase/Records only where a website dependency exists (rule 10); no fabricated screenshots/testimonials/metrics (rule 12); localization, SEO, routes, integrations preserved (rule 7).

## Phase 1 — Experience audit + rubric (deliverable: `WEBSITE_DESIGN_AUDIT.md`, prioritized backlog)

- Score all 27 routes on the 8-category rubric (20 visual · 15 consistency · 15 motion · 15 hierarchy · 10 responsive · 10 a11y · 10 perf · 5 reliability = 100).
- **Evidence basis constraint:** *visual design* and *responsive behavior* are scored from DOM/CSS structure + harness numbers and marked “inspection-limited” until a browser pass; never awarded assumed points. Performance scored from artifact measurements. Functional reliability from gates.
- Route probes: hero composition (A-02), dropdown/mobile nav paths (Navbar 822 ln — keyboard, Escape, outside-close enumeration), error/loading/404 states, legal/blog secondary routes consistency, operational routes design-system consumption.
- Output: per-route scores table, per-category evidence lines, merged backlog with A-01… from WEBSITE_AUDIT.
- **AC:** every route scored with evidence or marked unverified; backlog priority order frozen.

## Phase 2 — Design-system consolidation (proposed: extension layer, not new system)

- Add the two missing token families: **time/duration/easing tokens** (A-08) and **entrance/stagger tokens**; document motion-token grammar in `globals.css` beside the existing doctrine comments.
- Lock semantic naming pass on any drift found in Phase 1 (e.g., file-level `sys/*` surfaces, one-off shadows) — no visual change without a harness diff reason.
- **AC:** token census before/after shows new families consumed by new work; paint/contrast/coverage gates green; no family isolation regression (clay-paint 175).

## Phase 3 — Motion system

- Ship `lib/motion-governance`: duration/easing/stagger constants mapping to CSS tokens; hero entrance choreography (A-02) CSS-first; marquee audit (A-07) with offscreen/PRM/hover pauses; interaction feedback consistency pass on Button/Accordion/tabs/dropdowns using existing CSS transitions.
- Add vitest coverage: PRM state = final visual state for hero; no-layout-thrash property allow-list enforced by a CSS audit script (animated properties limited to transform/opacity/filter).
- **AC:** hero intro staged and ≤ ~1.2s to readable; PRM instant; A-07 loop registry exists; all gates green including the new motion-property audit.

## Phase 4 — Fluidity verification

- Confirm: native scroll preserved; no scroll-jacking; pointer effects only via custom properties + capability gating (`matchMedia('(pointer:fine)')` where applicable); sticky/fixed stability (`overflow clip` anvil audited); no per-pointermove React state (guard:pointer extended if needed).
- **AC:** pointer-style state-writes = 0 new violations; keyboard/anchor/back-button paths tested in interaction suite; PRM + touch paths honored.

## Phase 5 — Route-by-route (27 routes, in waves)

- Wave order: (1) `/` homepage (A-02 + hierarchy), (2) marketing core (`/features`, `/how-it-works`, `/optimization`, `/worked-example`, `/pricing`, `/screenshots`), (3) trust+company (`/about`, `/developer`, `/collaborate`, `/sponsors`, `/resources`, `/blog*`, `/contact`, legal ×3), (4) operational presentation (`/job-orders`, `/deliveries`, `/records*`, `/reports*` — presentation parity only, contracts and data paths frozen), (5) system surfaces (`/sys/*` stay engine-truthful; error/loading/404 polish).
- Per-route protocol: current-defect note → shared-component-first change → DOM/CSS verification at both modes × 5 families via harness probes where applicable → i18n spot check → per-route changelog line.
- **AC by wave:** all script gates green; census ≤ 600; pixel-pass green; route list changed/unchanged both recorded; before/after evidence = DOM+token diffs (+ screenshots deferred to browser session, listed as such).

## Phase 6 — Content + i18n remediation (A-01 is the headline)

- Execute the A-01 plan (gaps → memory fill → machine fill → re-grade). Copy pass: value proposition above the fold on `/`, CTA hierarchy (one primary per section), terminology consistency (English source) before translation fill.
- SEO: metadata/JSON-LD/canonical diffed before→after; sitemap/robots updated (A-04 check).
- **AC:** `i18n:verify` clean; no mixed-language pages at shipped locales; metadata diff empty except intended lines.

## Phase 7 — Performance engineering (A-05, A-06 headline)

- Per-route JS manifest table + budget; hero-media variant audit (which kB ship at 390/768/1440); font-preload audit (only genuinely critical); client-island split opportunities (largest chunks 240K/232K named and justified or split).
- CWV strategy: lab proxies here + Speed Insights field data post-deploy; targets: LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1 (p75) measured *where data exists* — claims labeled lab vs field.
- **AC:** manifest table committed; budgets named; no budget exceeded without recorded exception.

## Phase 8 — Accessibility

- Focus-visible sweep + visible-focus token audit; ARIA census refresh; dialog/menu focus paths (Navbar dropdowns, studio drawer) with DOM tests; contrast gates re-run on any Phase-5 visuals; touch-target sweep (44px) on nav/CTAs; reduced-motion full-site pass.
- **AC:** new DOM a11y tests green; contrast suites green; PRM verified at CSS level; findings logged with severity.

## Phase 9 — Regression + QA

- Full battery fresh: build, tsc, eslint, vitest, paint, contrasts ×2, census, coverage, pixel-pass, pointer guard, color audit, job-orders, navbar clip, motion property audit. Route crawl of fresh prod server for broken assets/links (extend existing audit tooling if it exists as `check` scripts — else manifest-based checks). Error paths (404/error/loading) per locale sample.
- **AC:** every gate named with result; failed/skipped/blocked separated explicitly.

## Phase 10 — Final review + handover

- Deliver `WEBSITE_FINAL_AUDIT.md`, `WEBSITE_VISUAL_QA.md` (inspection-limited until browser pass — stated), `WEBSITE_PERFORMANCE_REPORT.md`, `WEBSITE_CHANGELOG.md` (cross-linked to the engine phase history — A-10); backlog re-walk with resolved/dispositioned status; branch/deploy verification via `check:live` where reachable.
- **AC:** 10-section final report complete; rubric re-scored with evidence links; remaining defects named with owners/next action.

## Sequencing and stop conditions

1 → 2 → 3 → 4 → 5(w1→w5) → 6 → 7 → 8 → 9 → 10, each gated by its AC. A phase that cannot prove its AC because of the sandbox limits (browser/network) ships everything provable, records the blocked item in the phase report, and continues — the absence of a screenshot never masquerades as a pass.

**Immediate next action:** Phase 1 (audit only, still no code modification) — the 27-route rubric pass and the frozen backlog.
