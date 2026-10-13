# Website Audit — Phase 0 + Phase 1 (design & experience)

> Executed: 2026-10-13 · HEAD `d3469e4` · Scope: `website/` only (WPF desktop app out of scope) · Companion files: `WEBSITE_ROUTE_INVENTORY.md`, `WEBSITE_BASELINE.md`, `WEBSITE_PHASE_PLAN.md`
> **Honesty rules applied**: no route/finding invented; no visual claim without evidence; untestable categories marked **unverified** and score 0 for that category's unverifiable portion rather than assumed points.

## 0. What this site already is (so the upgrade builds on it, not over it)

This is **not** a naive site awaiting a first design pass. Source inspection shows a mature, heavily-gated system:

- A token-driven design system (`globals.css` custom properties → Tailwind `ink/line/brand/text/state`), 2 modes × 5 appearance families, mirroring the WPF product's own `Settings → Appearance`.
- Two material engines already shipped and gated: **Clay** (default; `clay-engine.css`, 175 paint probes, 100% pixel-pass over 27 routes) and **Crystal glass** (`crystal-engine.css`, 87 contrast pairs, 12 motion contracts, 522-surface blur budget). Prior programs (`docs/glassmorphism-roadmap.md` G0–G7, `docs/claymorphism-roadmap.md` phases 1–7) are closed.
- A disciplined motion *grammar* without any animation library: CSS keyframes + `Reveal` (one shared observer), `Spotlight`, `Tilt`, `HeroField`, `GlassLife` — all reduced-motion/hover/pointer gated, all transform/opacity-only by a documented ban list.
- Truth-in-content discipline: `src/lib/site.ts` forbids invented statistics; sample data is labelled as sample data; no fabricated testimonials exist. **This must be preserved.**

The master prompt asks for a *premium glassmorphism + motion + performance* upgrade. Against this baseline, the honest audit conclusion is: **the glass material exists (Crystal) but is not the default experience, the motion system is restrained to the point of invisibility on most routes, and the performance budgets have almost no headroom left.** The defects below are therefore mostly *experience* defects (default presentation, hierarchy, motion choreography, storytelling), not "site is broken" defects.

## 1. Scoring rubric (100 points, applied per route in §3)

| Category | Max | What earns points (evidence required) |
|---|---|---|
| Visual design & art direction | 20 | Distinctive material/depth, intentional lighting, non-generic composition (source classes + harness) |
| Design-system consistency | 15 | Shared tokens/primitives actually used; no route-local visual language (class census) |
| Motion quality & interaction | 15 | Sequenced entrances, scroll reveals, hover/press feedback, product-state transitions (code + motion contracts) |
| Content hierarchy & readability | 15 | Headline→lead→CTA order, scannable sections, truthful concrete copy (rendered HTML read) |
| Responsive behaviour | 10 | Breakpoint structure in source + panel-width model (`measure-panel.py`); **pixel matrix unverified without browser** |
| Accessibility | 10 | Contrast pairs (measured), focus grammar, semantics, reduced-motion, touch targets (harnesses + source) |
| Performance | 10 | Weight/blur budgets, client-boundary discipline, image/font strategy (build + audit numbers) |
| Functional reliability | 5 | Build/tests/crawl green, links/404s correct (executed gates) |

Unverified sub-evidence deducts explicitly; category scores below state the deduction.

## 2. Defect backlog (prioritised; IDs referenced by the phase plan)

Severity: **C**ritical / **H**igh / **M**edium / **L**ow.

| ID | Route(s) | Current condition (observed implementation) | Defect / opportunity | Sev | Proposed change (concrete) | Dependencies | Acceptance criteria | Verification |
|---|---|---|---|---|---|---|---|---|
| D-01 | All (first impression) | First-run default family is **Clay** (`DEFAULT_APPEARANCE = "clay"` + hygiene-v2 forced migration in boot script); server renders `data-appearance="crystal"` then boot script flips to clay before paint | The premium *glass* identity the product is named for is opt-in only; a first-time visitor never sees the flagship glassmorphism without opening the Appearance Studio. Directly conflicts with the master prompt's glassmorphism goal | **H** | Decide the flagship default deliberately (recommendation: Crystal as first-run default, Clay remains one click away) — owner decision gate, then flip `DEFAULT_APPEARANCE` + boot allowlist/hygiene in one change | Owner decision (Gate G-A); boot-script hygiene version bump | Fresh profile (no storage) lands on Crystal in both modes; stored choices still win; no FOUC | Vitest boot/hygiene cases + `crystal-contrast` 87/87 + manual fresh-profile check note |
| D-02 | Global motion | CSS-only reveals (`opacity`+18px translate, one shared observer); hero entrance is uniform `Reveal` fades; no sequenced headline choreography, no shared-element/layout transitions, no page-transition continuity | Motion reads as "everything fades up" — competent but generic; lacks the deliberate sequencing the prompt requires (hero stagger, product-state transitions) | **H** | Phase 3 motion system: keep CSS+hooks architecture (no new library without documented need); add tokenised duration/easing/spring-equivalent curves, hero sequence, section stagger discipline, product-demo state transitions | None (existing hooks) | Motion tokens documented; hero + 3 demo transitions implemented; `crystal-motion` contracts still pass; reduced-motion = content immediately visible | Motion contract tests + source inspection + deployed witness |
| D-03 | Global performance | 7/10 budgeted pages within ~1K gz / ~70 DOM nodes of ceiling; CSS budget has 0.4K headroom; `/`+`/features` at 470/600 blur surfaces | Almost no headroom: any added glass/motion markup regresses a gate. Performance work is a *precondition* for visual work, not an afterthought | **H** | Phase 7 runs *interleaved*: budget-neutral rule for Phases 2–5; first-load JS diet candidates (see D-11) before adding anything | Build tooling (already present) | No budget gate fails at any phase boundary; ceilings only move with written justification in same commit | `audit-site.py --budget` + blur census at each phase gate |
| D-04 | Homepage hero | Hero is text + `LiveSurface` demo + 4 floating glass shards + aurora; `next/dynamic` split was tried and reverted (documented: SSR chunk preloads anyway); headline/CTA entrance = 3 uniform `Reveal`s | First viewport is strong but the *sequencing* and pointer-light story is thinner than interior pages' material depth; LCP element = 3840px hero backdrop via `next/image` (correct strategy, weight to confirm per-viewport) | M | Phase 3A: sequenced hero entrance (headline→lead→CTAs→demo), restrained pointer light via existing `HeroField` vars; verify hero image sizes per breakpoint | Existing `HeroField` | Hero readable at first paint (no content hidden behind animation); reduced-motion identical content | Source + motion contracts; deployed witness screenshots (Phase 1 protocol) |
| D-05 | Interior pages | Every interior page opens with the same `PageHero` (eyebrow chip → `display-1` → lede → meta cards) + `PageBanner` line-art | Consistent but monotonous: routes risk "same page, different words"; prompt requires consistency *without* every page looking identical | M | Phase 5: per-route hierarchy differentiation inside the shared system (banner art, meta emphasis, section rhythm variants) — no new visual language | Design tokens (Phase 2) | Each major route has a distinct, purposeful opening while using only shared primitives | Route-by-route checklist + witness screenshots |
| D-06 | Crystal coverage | Crystal re-glasses named classes; utility carriers (`chip`, tables, `pre`, inputs) were the known gap (roadmap S2) — G-phases since closed much of it; census now counts 522 blur surfaces on `/features` | Residual risk: blur count near budget on the two flagship pages; coarse-pointer/mobile compositing cost **unverified** (no device lab) | M | Phase 4/7: coarse-pointer frost reduction already exists (G6 contract); verify + document mobile strategy; do not add blurred surfaces beyond budget | — | Census ≤600 with margin after redesign; coarse-pointer rule present and tested | Blur census + motion contracts |
| D-07 | Lint | `npm run lint` → 2 errors (unused `SHADOW_OK` in `color-liter-audit.mjs`; unused `t` in `sys/clay/page.tsx`) | Red lint at baseline undermines every later "checks pass" claim | L | Fix both (underscore-prefix or remove) as Phase 1 hygiene, before redesign work | None | `npm run lint` exits 0 | Lint re-run |
| D-08 | `check:live` | Sandbox egress to Vercel blocked (`fetch failed`/000) | **Not a site defect** — environment limitation; deployment freshness had to be judged via fetch tool (revision matches HEAD ✅) | L | None (record; re-run `check:live` from an unrestricted network at handover) | Network | Phase 10 re-runs `check:live` or records why it cannot | Command output in final audit |
| D-09 | SEO/content | Sitemap/robots/canonicals verified correct; titles ≤60 chars, descriptions ≤159 (audit-site); banned-claims list enforced | Healthy — risk is *regression* during redesign (metadata, hreflang, structured data must not be touched casually) | L | Phase 6 guardrail: metadata/SEO regression check in every phase gate | — | `audit-site.py` 0/0 at each gate; sitemap unchanged in shape | Audit output per phase |
| D-10 | Docs accuracy | `site.ts` comment claims both Vercel origins "answer no stamp" — stale (primary origin answers current `d3469e4` stamp) | Documentation drift; misleads future freshness checks | L | Correct the comment when next touching `site.ts` (docs-only) | None | Comment matches observed `api/health` behaviour | Inspection |
| D-11 | JS weight | First-load JS 297–308K gz/page (19 chunks) on a marketing site; heaviest chunks 110–239K raw (Turbopack output, un-attributed without `ANALYZE=true` build) | No single offender provable yet; attribution needed before diet claims. Candidates to *investigate* (not assumed): `LiveSurface`/`SpecificationSurface` (1,412/1,732-line client surfaces), ThemeStudio chunk discipline, dashboard shells on `/reports`·`/records` | M | Phase 7: `ANALYZE=true` build, attribute top chunks, then targeted splits/lazy boundaries; re-measure | `@next/bundle-analyzer` (installed) | Attributed report + measurable first-load reduction or documented floor | Analyzer output + audit `--budget` |
| D-12 | Visual QA capability | No browser in sandbox → no pixel screenshots at baseline; prior programs relied on computed-style harnesses + user-deployed screenshots | The prompt's screenshot/before-after requirements **cannot be met from this environment alone**; pretending otherwise would violate the honesty rules | **H** (process) | Phase 1 protocol: (a) computed-style harnesses as the in-sandbox gate, (b) deployed-preview witness screenshots as the pixel gate, (c) every visual claim labelled with which evidence backs it | Preview deployment | Protocol written and used at every phase gate; no unlabelled visual claim | Phase reports |

*No Critical defects found*: build, tests (55/55 + 299 harness checks), crawl, contrast, and deployment freshness are all green at baseline.

## 3. Per-route scores (evidence-based; deductions stated)

Global evidence applied to all routes: build ✅, vitest 55/55, audit-site 0/0, contrast harnesses green, reduced-motion contracts 12/12. **Pixel-level responsive/visual verification is unavailable (D-12)** — responsive and part of visual scores are therefore capped and marked ◐ (partially verified).

| Route | Visual /20 | Consistency /15 | Motion /15 | Hierarchy /15 | Responsive /10 ◐ | A11y /10 | Perf /10 | Reliability /5 | **Total /100** |
|---|---|---|---|---|---|---|---|---|---|
| `/` (home) | 15 | 13 | 10 | 13 | 6◐ | 8 | 7 | 5 | **77** |
| `/features` | 13 | 13 | 9 | 12 | 6◐ | 8 | 6 (67-node headroom) | 5 | **72** |
| `/optimization` | 14 | 13 | 11 (live demo states) | 13 | 6◐ | 8 | 8 | 5 | **78** |
| `/how-it-works` | 12 | 13 | 9 | 12 | 6◐ | 8 | 7 (0.1K headroom) | 5 | **72** |
| `/worked-example` | 13 | 13 | 9 | 13 | 6◐ | 8 | 8 | 5 | **75** |
| `/screenshots` | 13 | 12 | 9 | 12 | 6◐ | 8 | 8 | 5 | **73** |
| `/pricing` | 12 | 13 | 8 | 12 | 6◐ | 8 | 8 | 5 | **72** |
| `/developer` | 12 | 12 | 8 | 12 | 6◐ | 8 | 7 | 5 | **70** |
| `/about` `/collaborate` `/sponsors` | 11 | 12 | 8 | 11 | 6◐ | 8 | 8 | 5 | **69** |
| `/contact` | 12 | 13 | 8 | 12 | 6◐ | 8 | 8 | 5 | **72** |
| `/resources` `/blog` `/blog/*` | 11 | 12 | 8 | 11 | 6◐ | 8 | 8 | 5 | **69** |
| `/legal/*` (×3) | 10 | 12 | 6 (correctly near-static) | 11 | 6◐ | 8 | 9 | 5 | **67** |
| `/reports` `/records` `/job-orders` `/deliveries` (+2 logins) | 11 | 11 | 8 | 10 | 5◐ (data tables, unverified at mobile widths) | 7 (auth states partially inspectable) | 7 | 4 (session-dependent) | **63** |
| `/sys/clay` `/sys/crystal` (internal) | — | — | — | — | — | — | — | — | **Excluded** (review tools, `noindex` by design) |

Score reading: consistency/accessibility/reliability are genuinely strong (measured). The recoverable points sit in **visual distinction (≈5/route), motion choreography (≈5/route), and hierarchy differentiation** — exactly Phases 2–6. No route scores in the 90s and none should until pixel evidence exists.

## 4. Phase 1 conclusions feeding the plan

1. **Do not reskin.** The design system is the asset; the upgrade is: make Crystal the flagship default (D-01, owner-gated), differentiate route hierarchies (D-05), and choreograph motion (D-02) — all inside existing tokens/primitives.
2. **No new animation library is justified yet.** The CSS+hooks system covers reveals/springs-equivalents/transitions; `motion/react` or GSAP may only enter via the documented need test in Phase 3.
3. **Performance is a constraint, not a phase at the end** (D-03/D-11): budget-neutral changes, analyzer attribution first.
4. **Fix lint first** (D-07) so every later gate claim is clean.
5. **Adopt the visual-evidence protocol** (D-12) before any visual change.
