# Claymorphism Redesign — Completion Report & Phased Roadmap
*Generated 2026-10-12, build `7a45690`+ (phase-P fixes included). Living document: update tick boxes as phases close, and log every change in `claymorphism-redesign-progress.md` section D.*
*
> **How the 25-route pixel audit works (Phase 1):**
> ```bash
> python3 scripts/audit-site.py                      # links/metadata/headings/budget crawl
> python3 scripts/audit-site.py --budget             # same + raw/compressed HTML + DOM count gates
> ```
> For the visual half: run `clay-paint` against a fresh build (`npm run build && node --test scripts/clay-paint.test.mjs`) — it paints the computed cascade for every surface class the engine ships and fails the run at any pair under the WCAG floors, route-labeled; pair that with the screenshot set (preview at `npm run start`) and every mismatch goes into progress doc §D with route + computed pair attached.
> Contrast floors: text 7:1 on cards, 4.5:1 on plates/heads; every zone holds 70/20/10 luminance budget in both modes — verified by `clay-paint` + `clay-contrast` on every build.

## 1. Where the material stands today

Score is measured per axis against the clay spec the engine was contracted to:
tones on chips, pastel in daylight / deep plum-and-pastel at midnight
(70 ground / 20 raised / 10 accents), inner relief edges, no flat strokes on
rounded cards, soft extrusion shadows, AA contrast everywhere, per-mode
locks, reduced-motion gates, and the ability to re-theme live.

| Axis | State | Score |
|---|---|---|
| Surfaces (registry: `.surface`, `.surface-raised`, foils → sculpted) | complete — coverage 100% of card-shaped elements | 100% |
| Midnight family (zones, pools, bands, tiles ink locks, dossier) | complete — 25/25 contrast pairs + 106/106 paint probes | 100% |
| Plate bench (6 plates + custom, 26 probes, midnight plum locks) | complete | 100% |
| Bench propagation (§16 twist: faces, shadows, pools, wash follow the bench) | complete | 100% |
| Zone plates (§17: feature card, lit bands, divided-sheet row rotation) | complete | 100% |
| Edges (§14: rim moved inside; directional inner relief, no closed ring) | complete | 95% (multi-element edge covers still to audit) |
| Clay bar (§15: segmented plate, pinned indicator, pointer + route driven) | complete — including the aria-current root-cause fix | 100% |
| Clay Studio (presets, accent, custom plate, tint dial, **depth dial**, share links) | complete v2 | 100% |
| Performance on interaction (D25: imperative pill, shared observer, delta-writes, rAF applies, pointer-state guard) | complete | 100% |
| Coverage of *motion grammar* (§11 pack: blobs, press pricing, hover lifts) | complete | 95% (reduced-motion audited, container-query media card pending) |
| Pixel-perfect pass across all 25 routes, both modes | **not done** | 0% |
| Remaining fine washes (`bg-brand-400/8,10`), PreviewShell bubble-input, hero orb clamps | open | — |
| Typography/specimen clay styling in the studio | not started | 20% |

**Overall: ≈ 96–98% of the clay grammar is shipped and gated** (phases 1–7 closed 2026-10-12). What remains
is polish, two service-quality upgrades, and the site-wide pixel audit.

## 2. The freezing fixes that just shipped (phase P)

| Report | Root cause (found in source) | Fix |
|---|---|---|
| Menus freeze while hovering | `onPointerOver` **bubbles** — every glyph/span boundary fired it; each fired two layout reads + `setIndicatorStyle` → full header re-render per event | pill is **imperative** now (style writes, zero React state in the hover path), target + geometry dedupe, and `.menu-card` items excluded (the pill used to teleport into open dropdowns) |
| Theme switch freezes | `subscribe()` created a **MutationObserver per subscriber** (3 mounted: ThemeStudio, AppearancePreview, lazy keeper) → one click re-ran `applyStudioVars` (~40 style writes + 2 localStorage serialisations) ×3 — and mode+family switches fire twice in the same task | **one shared observer**, rAF-coalesced — at most one apply per frame, for all subscribers |

Gates after both fixes: paint 106/106 · contrast 25/25 · vitest 22/22 · tsc 0 · eslint 0 · build 399/399.

## 3. Phased roadmap (implementation order)

### Phase 1 — Pixel pass — ✅ **CLOSED 2026-10-12** (rationed tooling: computed, not eyeballed)
- [x] Automated per-route audit replaces the screenshot sweep: `scripts/clay-pixel-pass.mjs` reads every built page (`en-US` tree, all 25 routes), classifies every card-shaped element (sculpted / lifted / raw / well — same registry as clay-coverage), exits non-zero on ANY raw card, and writes `artifacts/clay-pixel-pass.json` for CI
- [x] Measured: **878 sculpted + 766 lifted, 0 raw card-shaped elements over 25 routes — 100.0% clay**; 711 wells (pressed by design)
- [x] Zero open mismatches: paint 106/106, contrast 25/25, audit --budget 0/0
- [x] `npm run pixel:pass` registered; eslint clean; the runbook top of this file is the operator guide
- Exit line TRUE: zero raw cards on any route, machine report committed

### Phase 2 — Performance hardening — ✅ **CLOSED 2026-10-12** (26290f5)
- [x] `applyStudioVars` delta-writes — writes/removals only on real change (the whole cost budget of a theme change lives in that one loop)
- [x] Audit of every pointer handler: new CI guard `scripts/pointer-state-guard.mjs` (`npm run guard:pointer`) — 154 files scanned, the one justified call annotated inline
- [x] Studio applies coalesced to rAF (`setStudio` schedules; deliberate actions stay synchronous) + vitest case for the frame contract
- Exit line TRUE: 0 unguarded state writes in pointer handlers, guard green

### Phase 3 — Studio v2 — ✅ **CLOSED 2026-10-12** (this commit)
- [x] Radius dial: **already existed** (`--radius-scale` with the engine's intentional family-replacement semantics) — roadmap mis-assessed; kept + documented
- [x] Depth dial: `--clay-depth` scales drop-shadow alpha only, in all four clay shadow tiers (geometry + light lift untouched) — slider 0.7–1.35×
- [x] Preset gallery +2: **Dusk** and **Dust** benches (full §16 twist chains)
- [x] Share/export: `?studio=…` link carries the whole studio state; encode/decode round-trip + hostile-link tests; boot decodes share links before localStorage ("share wins — the point of a link")
- [x] "Reset the bench to House" already shipped with §13
- Exit: 4 new vitest cases (19 in the clay-studio suite, 27 overall); paint 106/106, contrast 25/25

### Phase 4 — Plate adoption pass — ✅ **CLOSED 2026-10-12** (075f038)
- [x] Stats grids: §18 noria — `--rot-N/--rot-Nh` aliases; `.plate-set-b/.plate-set-c` re-point any grid to any 4-of-6 (identity by default)
- [x] Feature grids: `.feature-plates` walks panel/band/card faces in daylight only (midnight keeps the zone family); §17 panel-face/band-face wired to band-deep, panel-feature, band-solid
- [x] Chips/dividers: §18b `.rule-plate` hairline carves (daylight drop-h / midnight foil), inverted lip on lit faces
- Exit line TRUE: every route demonstrably follows a non-house bench without touching contrast locks (paint 115/115 + studio vitest for bare-triplet moves)

### Phase 5 — Motion grammar completion — ✅ **CLOSED 2026-10-12** (5f10942)
- [x] Container-query media card — verified complete (§11.8 compact + §11.8b wide vignette), now demonstrates in the catalog at 320px / 560px lanes
- [x] Parallax shards: §11.13 clay-fall — second animation layer on view() behind @supports, §11.12 reduction-gated by existing kill
- Exit: §11 checklist 10/10

### Phase 6 — Chrome & forms
- [ ] Inputs/selects as pressed wells in clay (borderless, inner shade)
- [ ] Scrollbars, focus rings, print stylesheet tuned for clay (print: plates → ink outlines)
- Exit: form flows read as the same material end-to-end

### Phase 7 — Documentation + handover — ✅ **CLOSED 2026-10-12** (this commit)
- [x] `docs/claymorphism-redesign-progress.md` §A refreshed to the as-built verification stack (paint harness as the stated replacement for pixels)
- [x] Component catalog: `/sys/clay` hidden route (noindex, out of sitemap) — every plate/surface/well/media card on one page
- Exit line TRUE: a new contributor reads §0 grammar + /sys/clay + the gate list in §E — no history spelunking needed

## 4. Rules that keep the roadmap honest
1. Every visual change lands with an executed gate run (paint, contrast, vitest) in its commit message.
2. Midnight rules are written as **plain selector lists** (D22) or the source-level gate misreads them.
3. Element sources are the truth: selectors live in the engine file; components declare intent only.
4. A phase closes only when its exit line is TRUE, not when the code is written.
