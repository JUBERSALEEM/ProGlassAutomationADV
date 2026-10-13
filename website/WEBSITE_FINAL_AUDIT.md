# Website Final Audit — Premium Glass & Motion Programme

Date: 2026-10-13 · Branch: `arena/51889ca3-proglassautomationadv` (uncommitted on `d3469e4`)
Production at audit time: `1de50cd` on `arena/ff328a9c-proglassautomationadv` — **programme not yet deployed**.

## Programme outcome
Phases 0–10 executed in order (audit → plan → implement → checks → document → gate).
Gate decisions: **G-A** Crystal-by-default — *not approved, Clay remains the first-run
default*; **G-B** `motion/react` — approved, shipped; **G-C** per-route hierarchy in
Phase 3 — approved, shipped and completed in Phase 5.

## 100-point rubric (Phase 1 rubric; scores conservative where evidence is partial)
Categories: Visual 20 · Design-system 15 · Motion 15 · Content 15 · Responsive 10 ·
Accessibility 10 · Performance 10 · Functional 5. "U" = unverified sub-evidence.

| Route group | Vis /20 | DS /15 | Mot /15 | Con /15 | Resp /10 | A11y /10 | Perf /10 | Fun /5 | Total |
|---|---|---|---|---|---|---|---|---|---|
| Homepage (sequenced hero) | 16U | 15 | 14 | 15 | 9U | 9 | 8U | 5 | **91U** |
| Showcase (features, optimization, screenshots, pricing, how-it-works, worked-example) | 15U | 15 | 14 | 15 | 9U | 9 | 8U | 5 | **90U** |
| Document (blog, resources, about, contact, collaborate) | 15U | 15 | 11 | 15 | 9U | 9 | 8U | 5 | **87U** |
| Editorial (developer, sponsors) | 15U | 14 | 11 | 15 | 9U | 9 | 8U | 5 | **86U** |
| Legal (privacy, terms, license) | 14U | 14 | 8 | 15 | 9U | 9 | 9U | 5 | **83U** |
| Operator dashboards (deliveries, job-orders, records, reports) — bespoke by design | 14U | 13 | 8 | 14 | 9U | 8U | 8U | 5 | **79U** |

Why capped: **Visual/Responsive carry U** because no browser exists in the build
sandbox — computed harnesses (E1) and rendered-HTML crawls (E2) are green everywhere,
but witness pixels (E3) were never captured. Performance is lab-bytes only (no
Lighthouse/field data). Any score above ~91 without E3 would be fabrication; these
numbers rise only when screenshot evidence lands.

## Final gate sweep (Phase 9, all actually run)
lint ✅ · typecheck ✅ · build ✅ 429/429 · vitest 55/55 · navbar tests ✅ ·
clay-paint 175/175 · crystal-contrast 87/87 (100% coverage) · crystal-motion 12/12 ·
motion-tokens 2/2 · pixel-pass ✅ · pointer-guard ✅ · blur census 522/600 ·
audit-site crawl ✅ 0/0 · budgets ✅ 0/0 · doctor ✅ · RTL/locale spot checks ✅.

## Defects found & fixed during the programme
1. No-JS blank page (Phase 8) — noscript resilience sheet. **Fixed.**
2. Missing safe-area insets on fixed launchers (Phase 4). **Fixed.**
3. Two HTML budget ceilings over by 0.1K after hero variants (Phase 7) — re-justified
   with measured trail. **Closed.**
4. Lint defects D-07 (Phase 1). **Fixed.**

## Rejected with measurements
LazyMotion feature-splitting (Phase 7): +9K emitted JS, +3.6K first-load. Reverted.
Route page-transitions (Phase 3): skipped to protect native navigation semantics.

## Open items for the owner
1. **Deploy decision**: merge `arena/51889ca3-proglassautomationadv` through the normal
   flow; production does not contain Phases 3–9 yet. Work is uncommitted in the
   working tree (repo convention for this session) — commit before merging.
2. **E3 witness pass**: capture 1440×900 / 768×1024 / 390×844, {Clay, Crystal} ×
   {Daylight, Midnight}, en-US/de-DE/ar-AE from the deployed preview; close the
   witness-required rows in `WEBSITE_VISUAL_QA.md` and re-score Visual/Responsive.
3. **i18n completeness**: 8 locales marked complete at ~75% coverage (pre-existing).
4. **CSS headroom**: 38.9K of 39K gz — diet or re-baseline before new global CSS.
5. Gate G-A remains open if Crystal-by-default is ever wanted.
