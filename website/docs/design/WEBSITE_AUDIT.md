# WEBSITE_AUDIT — Phase 0 discovery + defect ledger

Commit `d6c5514` · 2026-10-13 · Author: principal audit per master prompt Phase 0.
Reading contract: every row names its evidence. “Verified sound” rows exist so later phases cannot silently regress them. Rows are numbered A-01…; the backlog in §3 re-orders them by severity.

## 1 · What this audit could and could not do

- Could: read every route, layout, shared component and stylesheet; measure the production build; execute all 13 local gates; diff deployed-vs-source only until the network edge.
- Could not: open a browser, screenshot, Lighthouse, or fetch the Vercel URL (sandbox egress). Those are recorded in WEBSITE_BASELINE §6 as program-wide constraints — no visual claim anywhere in this program is made without them.

## 2 · Architecture findings (verified sound — regression-guarded)

| Route/area | Current condition (evidence) | Note |
|---|---|---|
| All 27 page routes | Server-rendered, SSG ×15 locales; zero client `page.tsx` | Exceptionally strong posture for a marketing site; preserve. |
| Re-theming studio | `ThemeStudio` + `useStudio` write inline `<html>` vars; boot script replays before first paint; observer re-applies on mode/family changes; NaN-poison spectrum normalized (P18/P19) | Live, tested (21 click-through DOM tests). |
| Two material engines | `clay-engine.css` (2,600 ln) + `crystal-engine.css` (1,072 ln) over `globals.css` grammar (4,257 ln); ~793 tokens; per-family isolation doctrine with harness locks (175 paint, 25+87 contrast) | The design system already IS a system. Phase 2 must extend, not rebuild. |
| Motion foundation | No JS animation library; CSS motion with shared-IO reveals, CSS-var pointer light, 46 reduced-motion guards | Phase 3 decision record required before adding any lib. |
| Scroll behavior | Native scrolling, no hijacking; sticky/fixed audited via `overflow-x: clip` on body anvil | Preserve; master Phase 4 confirms. |
| Image pipeline | In-repo render → AVIF/WebP ladders (4 sizes) via sharp; heroes ship 28–160K variants | Sound. Verify variant selection at common viewports in Phase 7. |
| Contrasts | 25 clay pairs, 87 crystal pairs, harness-enforced per mode × family | Sound; keep green. |

## 3 · Defect & opportunity ledger

Severity: 🟥 critical · 🟧 high · 🟨 medium · 🟦 low. AC = acceptance criterion.

### 🟥 A-01 · Eight languages silently half-translated
- **Route** /[locale]/* (all 27) · **Condition**: `i18n:verify` = 4,502 keys; 8 locales at ~75.3–76.0% coverage marked “complete”; 6 more at ~10% “in progress”.
- **Defect/opportunity**: a visitor in ar/bn/de/es/fr/hi/id/pt reads a mixed-language page (untranslated keys fall back to English) — the master-prompt’s “eliminate unintended mixed-language experiences” item is objectively failing today.
- **Severity**: critical (global, content-level).
- **Proposed change**: (a) run `i18n:gaps` to enumerate missing keys per locale; (b) fill via `i18n:memory` (terminology memory from the application’s own XAML) then `i18n:fill` for residue; (c) re-grade “complete” honestly or downgrade badge; (d) lock with `i18n:verify` green as a merge gate.
- **Dependencies**: none external — scripts exist in-repo (`i18n-*`).
- **AC**: `i18n:verify` exits clean; per-locale coverage ≥ agreed threshold with no “marked complete” below it.
- **Verification**: script output table; spot-check 2 locales’ dictionaries.

### 🟧 A-02 · Single-column first impression: hero entrance has no choreography
- **Route** /[locale] (homepage).
- **Condition**: `ProductHero.tsx` composes headline/subhead/CTAs/figure with generic `Reveal` (one shared-IO fade/lift, delays) + `hero-shine-once`; `HeroField` client field. There is no sequenced arrival (headline → support → CTA stagger), no pointer-responsive lighting on the hero field beyond per-card `Spotlight` (desktop-only, degrades silently).
- **Defect/opportunity**: master Phase 3-A asks for a deliberately sequenced first viewport. Current = uniform fade family; the “premium first impression” is carried by material quality, not by direction.
- **Severity**: high (the only viewport every visitor sees).
- **Proposed change**: CSS-first entrance sequence on the hero (orchestrated `--d*` delays + dedicated keyframes, one-shot), pointer-light on the hero stage via custom properties (no per-move React state — the house `useSpotlight` pattern), parallax layers already present (`hero-backdrop-parallax`) kept under PRM caps. No library added (see Phase 3 decision record).
- **Dependencies**: none.
- **AC**: entrance order measurably staged (delay tokens increasing across elements); total intro ≤ ~1.2s before content fully readable; `prefers-reduced-motion` renders final state immediately (guard count unchanged or higher; `pointer` guard 0; frame budget: no layout-affecting properties animated).
- **Verification**: DOM assertion on classes/tokens (vitest), CSS keyframe audit, paint gates green; visual confirm deferred to browser session (recorded).

### 🟧 A-03 · Deployed-vs-source verification is unprovable from here
- **Route** deployment boundary.
- **Condition**: `check:live` exists and runs; at audit time the host is unreachable from the sandbox (egress). The deployed build visible to users may lag HEAD.
- **Defect/opportunity**: witness rounds in this repository’s own history were repeatedly “stale deploy” artifacts. A premium program that cannot see its production pixels will ship regressions politely.
- **Severity**: high (process, not code).
- **Proposed change**: make `check:live` the documented post-push step in every phase report; where network allows (user side / CI), run it and record the beacon; do not claim deploy verification otherwise.
- **Dependencies**: none (script present).
- **AC**: every phase after Phase 0 carries a `check:live` line: reachable+in-sync, or “blocked: sandbox egress”.
- **Verification**: script output pasted per phase.

### 🟨 A-04 · Two internal review surfaces live in the public route table
- **Route** /[locale]/sys/clay, /[locale]/sys/crystal.
- **Condition**: server routes, excluded from sitemap, `noindex`, hardcoded English (recorded in P19).
- **Defect/opportunity**: acceptable as engineering instruments; risk is accidental linkage/SEO bleed, plus they bypass localization by design.
- **Severity**: medium-low.
- **Proposed change**: confirm robots disallow covers `/sys/`, keep noindex; no redesign work on these routes in Phase 5 (they must track the engine truthfully instead).
- **Dependencies**: none.
- **AC**: `robots.txt` inspected and lists `/sys/`; routes stay internal.
- **Verification**: `cat src/app/robots.ts`; build table still shows noindex metadata.

### 🟨 A-05 · Per-route First-Load-JS composition is unmeasured
- **Route** all.
- **Condition**: Next 16 build omitted the classic size table in this terminal; only aggregate numbers exist (0.5 MB gz total chunks).
- **Defect/opportunity**: Phase 7 needs the per-route split to find duplicated client islands (e.g., hero field + studio chunks on content pages).
- **Severity**: medium.
- **Proposed change**: parse `.next/app-build-manifest.json` per route; tabulate in Phase 7 baseline; budget: no content route may exceed (homepage+studio) tier without a named exception.
- **Dependencies**: `@next/bundle-analyzer` already in devDeps (unused by scripts today).
- **AC**: `PERF` table with per-route kB exists; regressions vs. budget named.
- **Verification**: manifest parse output committed to the report.

### 🟨 A-06 · Blur surface budget at 87% of cap on the worst route
- **Route** /features worst = 522/600 surfaces (live crawl of prod build).
- **Condition**: every backdrop-filter is a GPU readback; the budget holds today with 13% headroom.
- **Severity**: medium (trend risk, not a failure).
- **Proposed change**: Phase 5 visual upgrades must not add blurred carriers on /features; census gate stays at 600; if a design needs lift, spend body-alpha tokens (§C4) not new blur.
- **Dependencies**: none.
- **AC**: census ≤ 600 on every crawled route at phase end.
- **Verification**: `crystal-blur-census --budget` output.

### 🟨 A-07 · Marquee is a measured-motion exception to audit
- **Route** any consumer of `Marquee.tsx`.
- **Condition**: `marquee-run` keyframes with continuous motion; PRM guard presence unverified in this pass.
- **Defect/opportunity**: continuous loops are the cheapest way to look busy and the fastest way to look cheap; audit which routes show it and whether it pauses offscreen/under PRM/hover.
- **Severity**: medium-low.
- **Proposed change**: inventory consumers; ensure PRM stop + offscreen pause (IO) + hover-pause; else remove from those consumers.
- **Dependencies**: none.
- **AC**: every animated loop on the site is named, justified, and gated.
- **Verification**: grep census + CSS guard check.

### 🟦 A-08 · Motion vocabulary exists but is undocumented as tokens
- **Route** design system.
- **Condition**: durations/easings live inline across components (e.g., `transition-transform`, ad-hoc ms values); no `--dur-*`/`--ease-*` token registry was found in the token census pass (793 tokens are color/geometry/surface, not time).
- **Severity**: low-high (consistency drift risk as Phase 3 adds choreography).
- **Proposed change**: introduce a time token family (`--dur-1..3`, `--ease-out`, `--spring-mild` as cubic-bezier tokens) consumed by new work; do not retrofit every existing transition in one pass (risk).
- **Dependencies**: none.
- **AC**: tokens exist, new hero choreography consumes them, motion contract tests still 12/12 (or equivalent suite) green.
- **Verification**: token census + gate.

### 🟦 A-09 · Raw `<img>` usage inconsistency
- **Route** hero + a few components (4 raw `<img>` uses vs `next/image` in 5 files).
- **Condition**: hero glass-3D tiles are raw `<img>` src with fixed variants — deliberate where custom `loading`/sizes behavior was needed, but the split is undocumented.
- **Severity**: low.
- **Proposed change**: record the rule (“next/image default; raw img only where rationale in comment”) — most rows already comply via asset ladders.
- **Dependencies**: none.
- **AC**: each raw img has a one-line rationale comment.
- **Verification**: grep + review.

### 🟦 A-10 · Scoreboard debt: crystal record-keeping spans two docs ecosystems
- **Route** docs.
- **Condition**: `docs/design` tracks a rigorous phase history (P01–P19) for the material engines; the new master program creates a second document family (WEBSITE_*). Keep them as ONE program-of-record (this file + history link), or history diverges.
- **Severity**: low (process).
- **Proposed change**: master program appends its phase records INTO `CLAYMORPHISM_PHASE_HISTORY.md`-style single file (this program’s `WEBSITE_CHANGELOG.md` cross-links it).
- **Dependencies**: none.
- **AC**: no parallel truth: one changelog of record by program end.
- **Verification**: file links at final audit.

## 4 · Explicitly not defects (checked, so nobody “fixes” them later)

- The `--clay-1..6` ledger artifacts are a probe-expansion false positive; the real channels (`--clay-lavender…pink`) are consumed (P19 audit).
- `body` `position: relative` anvil for crystal rims is guarded `:not(.fixed…)` since P17/P18; the launcher anchoring is tested.
- The studio’s inline `<html>` variables outranking stylesheets is the intended mechanism, guarded by family isolation tests.
- Supabase/Records internals: present, operational, out of scope (rule 10).

## 5 · What was NOT inspected (honest ledger)

- Rendered pixels of any route (no browser).
- The deployed site (no network path from sandbox).
- Supabase dashboard, Vercel project settings, OG image render output at runtime.
- Screen-reader behavior (code-level aria census only: 300+ attributes counted in earlier passes; Phase 8 re-verifies selectors).
