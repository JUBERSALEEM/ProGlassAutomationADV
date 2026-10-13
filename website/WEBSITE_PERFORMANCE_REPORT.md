# Website Performance Report — 2026-10-13

## Evidence class (read first)
Every number below is a **local-build lab measurement** (gzip bytes, DOM node counts,
chunk counts) taken with the repo's own harness (`scripts/audit-site.py`) against a
production build served locally. **No Lighthouse run and no field data (CrUX/RUM)
exists in this environment** — LCP, INP and CLS are therefore *unverified*, and this
report does not claim them. Targets (LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1) remain
targets, to be confirmed with a real browser lab after deployment.

## Measured budgets (final build, all passing — 0 failures / 0 warnings)
| Metric | Measured | Budget | Note |
|---|---|---|---|
| Emitted JS (all chunks, gz) | 544.0K | 560K | Ceiling raised 540→560 in Phase 3 with measured trail (motion library, Gate G-B) |
| Homepage first-load JS (gz) | 352.3K | 360K | 20 chunks |
| Emitted CSS (gz) | 38.9K | 39K | **Watch item: 0.1K headroom** |
| Homepage HTML (gz) | 104.1K | 107K | 3,841 DOM nodes vs 4,400 ceiling |
| Worst blur census (/features) | 522 surfaces | 600 | Crystal GPU ledger |
| Per-route HTML/DOM | all within ceilings | — | 2 ceilings re-justified +1K in Phase 7 (measured, annotated) |

## What changed performance-wise
- Phase 3 added `motion@14.2.0`: +50.3K gz emitted JS (shared chunk, loaded once),
  +48.2K homepage first-load. Accepted by budget re-justification, not hidden.
- Phase 4: safe-area insets (CSS classes only, negligible bytes).
- Phase 5: hero variants added ~0.1K gz HTML on two routes (ceilings adjusted).
- Phase 7 LazyMotion experiment: splitting motion features made things *worse*
  (552.9K emitted / 355.9K first-load) and was reverted. Final numbers are
  byte-identical to Phase 3.
- Phase 8: noscript resilience sheet ships inside HTML head only; no JS/CSS cost.

## Standing structure (verified in audit)
- Server Components by default; client islands small (motion components are the
  newest, bounded to hero/module-grid/sheet-inspector).
- `next/image` pipeline, static generation 429/429 routes, per-route asset budgets
  enforced by `audit-site.py --budget` in the gate suite.
- Motion restricted to transform/opacity (zero layout-property keyframes, Phase 4 census);
  native scrolling preserved; pointer effects gated to hover-capable fine pointers.

## Recommendations (not yet done, honestly labelled)
1. CSS diet or re-baseline before any new global CSS — 0.1K headroom will not
   survive another engine section.
2. Run a real Lighthouse/WebPageTest pass on the next deployment and record
   LCP/INP/CLS here before claiming them.
3. If the motion surface grows, re-evaluate LazyMotion — at today's three call
   sites it measured negative, but that arithmetic changes with more consumers.
