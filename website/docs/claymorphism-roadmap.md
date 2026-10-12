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
| Clay Studio (presets, accent, custom plate, tint dial, replay) | complete v1 | 90% (no radius/depth dials yet) |
| Performance on interaction (D25: imperative pill, shared observer) | complete v1 | 90% (see Phase 2 notes) |
| Coverage of *motion grammar* (§11 pack: blobs, press pricing, hover lifts) | complete | 95% (reduced-motion audited, container-query media card pending) |
| Pixel-perfect pass across all 25 routes, both modes | **not done** | 0% |
| Remaining fine washes (`bg-brand-400/8,10`), PreviewShell bubble-input, hero orb clamps | open | — |
| Typography/specimen clay styling in the studio | not started | 20% |

**Overall: ≈ 88–90% of the clay grammar is shipped and gated.** What remains
is polish, two service-quality upgrades, and the site-wide pixel audit.

## 2. The freezing fixes that just shipped (phase P)

| Report | Root cause (found in source) | Fix |
|---|---|---|
| Menus freeze while hovering | `onPointerOver` **bubbles** — every glyph/span boundary fired it; each fired two layout reads + `setIndicatorStyle` → full header re-render per event | pill is **imperative** now (style writes, zero React state in the hover path), target + geometry dedupe, and `.menu-card` items excluded (the pill used to teleport into open dropdowns) |
| Theme switch freezes | `subscribe()` created a **MutationObserver per subscriber** (3 mounted: ThemeStudio, AppearancePreview, lazy keeper) → one click re-ran `applyStudioVars` (~40 style writes + 2 localStorage serialisations) ×3 — and mode+family switches fire twice in the same task | **one shared observer**, rAF-coalesced — at most one apply per frame, for all subscribers |

Gates after both fixes: paint 106/106 · contrast 25/25 · vitest 22/22 · tsc 0 · eslint 0 · build 399/399.

## 3. Phased roadmap (implementation order)

### Phase 1 — Pixel pass (owner: you + me, next) — *gates the rest*
- [ ] Daylight + Midnight screenshots, all 25 routes, in the preview
- [ ] Log every non-clay or mis-painted surface into progress doc §D (route, element, pair)
- [ ] Fix at the engine, never locally; re-run paint/contrast after each batch
- Exit: zero open §D rows; 26-route screenshot set recorded

### Phase 2 — Performance hardening (1 change + audit)
- [ ] `applyStudioVars` delta-writes (compare-then-set to skip unchanged vars) — halves writes on every switch
- [ ] Audit every `onPointerOver/MouseMove` in `src/components` for setState-in-handler (rule: pointer handlers write styles or nothing)
- [ ] Profile the studio open path (dial drag currently applies ~40 vars per input event — throttle to rAF)
- Exit: no React state write from any pointermove/-over handler app-wide (grep guard in CI)

### Phase 3 — Studio v2 (the visible upgrade)
- [ ] Radius dial (bends `--radius-*` scales, keeps wells pressed)
- [ ] Depth dial (bends `--shadow-panel` tiers at kept geometry)
- [ ] Preset gallery: 2 more families (dust/dusk) with the §16 twist chains
- [ ] Share/export the bench (JSON in localStorage → copyable URL param), "reset to house" already lives
- Exit: 5 new vitest cases; paint/contrast still 106/25 green

### Phase 4 — Plate adoption pass (visual richness, strictly inside 70/20/10)
- [ ] Stats grids: connect `.stat-tile` rotation to `--plate-5/6` pairs so teams can pick 4-of-6
- [ ] Feature grids: alternate zone plates per row on developer/about/features pages
- [ ] Chips/dividers: thin plate underlines replacing brand hairlines (§4 semantics intact)
- Exit: every route demonstrably follows a non-house bench without touching contrast locks

### Phase 5 — Motion grammar completion
- [ ] Container-query media card (the one §11 section still pending)
- [ ] Parallax shards → clay-fall (translate+shadow answers scroll), reduced-motion gated
- [ ] Exit: §11 checklist 10/10

### Phase 6 — Chrome & forms
- [ ] Inputs/selects as pressed wells in clay (borderless, inner shade)
- [ ] Scrollbars, focus rings, print stylesheet tuned for clay (print: plates → ink outlines)
- Exit: form flows read as the same material end-to-end

### Phase 7 — Documentation + handover
- [ ] `docs/claymorphism-redesign-progress.md` §A refreshed to as-built grammar
- [ ] Component catalog page (hidden route `/sys/clay`) rendering every plate/surface/class for reviewers
- [ ] Exit: a new contributor can extend the engine without reading history

## 4. Rules that keep the roadmap honest
1. Every visual change lands with an executed gate run (paint, contrast, vitest) in its commit message.
2. Midnight rules are written as **plain selector lists** (D22) or the source-level gate misreads them.
3. Element sources are the truth: selectors live in the engine file; components declare intent only.
4. A phase closes only when its exit line is TRUE, not when the code is written.
