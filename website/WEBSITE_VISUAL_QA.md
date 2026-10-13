# Website Visual QA — Evidence Protocol (living document)

> Established: Phase 1, 2026-10-13 · HEAD `d3469e4` · This file is the **protocol** now and becomes the **final visual QA report** in Phase 10. No screenshot exists in this repository yet, and none is claimed.

## 1. Why this protocol exists

The sandbox that builds this site **has no persistent browser**: Playwright/Chromium binaries do not survive between sessions (recorded identically in `docs/design/CLAYMORPHISM_BASELINE_AUDIT.md`, 2026-10-13). Any workflow that claims "visually verified" from inside this environment without a browser is inventing evidence. This protocol separates what can be proven here from what needs a witness, and every visual statement in every phase report must carry one of these labels:

| Label | Evidence | What it can prove | What it cannot |
|---|---|---|---|
| **E1 · computed** | PostCSS/cascade harnesses: `clay-paint` (175), `crystal-contrast` (87), `clay-contrast` (25), `crystal-motion` (12), `clay-pixel-pass` (27 routes), blur census | Colours, contrast ratios, surface coverage, motion gating, blur counts | Layout beauty, overlap, jank, real rendering |
| **E2 · rendered HTML** | Production server crawl (`audit-site.py`, ±`--budget`), HTTP status/content checks | Links, metadata, headings, weights, 404s, locale/RTL attributes | Pixels |
| **E3 · witness pixels** | Screenshots of the **deployed preview** (or local run on the owner's machine) at the viewports in §2, inspected by a human/agent with vision | Actual layout, hierarchy, overlap, material quality | Field performance |
| **E4 · field data** | CrUX/RUM (none available at Phase 0/1) | Real-user CWV | — (currently **unavailable**) |

**Rule**: a phase gate may pass on E1+E2 for code correctness, but any claim about *how the site looks* requires E3. Where E3 is outstanding, the phase report must say **"witness-required"** — never "verified".

## 2. Capture matrix (when a browser channel is available)

- **Viewports**: Desktop `1440×900` · Tablet `768×1024` · Mobile `390×844` (plus `360×800` spot-check for the header ladder).
- **Themes**: priority matrix = {Clay, Crystal} × {Daylight, Midnight}; spot-check {Studio, Monet, Industrial} on `/` + `/features` only.
- **Motion states**: default + `prefers-reduced-motion: reduce` emulated on `/` and `/optimization`.
- **Locales**: `en-US` full set; `de-DE` (long words) and `ar-AE` (RTL) on `/`, `/features`, `/pricing`.
- **Routes**: all 24 public routes in `WEBSITE_ROUTE_INVENTORY.md` §2 (items 1–24) + 1 blog post; internal `/sys/*` excluded from visual sign-off.
- **File naming**: `qa/<phase>/<route>__<viewport>__<family>-<mode>.png`, committed only if total stays small; otherwise stored with the preview deployment and referenced by URL + revision.

## 3. Per-phase visual gate

1. Build + start the production server (or use the branch preview deployment).
2. Run E1 harnesses + E2 crawl — all must be green before pixels are judged.
3. Capture the §2 matrix (or the phase's scoped subset, stated in the phase plan).
4. Inspect every capture; log defects with route + viewport + family/mode.
5. Fix, rebuild, re-capture only the affected cells; record before/after pairs.
6. Phase report lists: captures taken, defects found, defects fixed, cells still witness-required.

## 4. Status log

| Phase | E1 | E2 | E3 | Notes |
|---|---|---|---|---|
| 0 (baseline) | ✅ 299 checks green | ✅ 0/0 + budgets | ❌ none (no browser) | Baseline is computed/rendered evidence only |
| 1 (hygiene) | ✅ paint 175 · crystal 87 · motion 12 · pointer guard · pixel-pass | ✅ audit 0/0, budgets green | ❌ none — no visual change was made, so no new pixel evidence is owed | Lint fixes are behaviour-neutral (dead code removal ×2, verified by full gate re-run) |
| 3 (motion + hierarchy) | ✅ all Phase 2 harnesses + motion-tokens 2/2 | ✅ audit 0/0 (JS 543.9K/560K justified) | ❌ **witness-required**: hero sequence, ModuleBrowser filter motion, PlateInspector transition, showcase/document heroes | First intentional visual-behaviour change of the program — pixel inspection owed before Phase 5 builds on it |
| 2 (design system) | ✅ paint 175 · crystal 87 · motion 12 · clay 25 + pixel-pass · census 522/600 | ✅ audit 0/0, budgets green (CSS 38.8K/39K) | ❌ **witness-required** for `/`, `/features`, `/optimization` under Crystal | Token adoption at identical computed values + new floating primitive: zero intended visual delta; any pixel difference on witness capture = defect |

## 5. Baseline reference

Phase 0 numbers (`WEBSITE_BASELINE.md`): build 429 pages · JS 493.6K gz · CSS 38.6K gz · worst blur route `/features` 522/600 · per-page table in baseline §4. Every later phase compares against these, not against memory.
