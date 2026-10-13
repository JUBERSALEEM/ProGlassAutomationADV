# Claymorphism Phase History — completed-phase evidence

*(Appended when a phase is removed from the queue; entries are never deleted.)*


## P02 — pale-step recalibration (D1) · completed 2026-10-13

- Objective: data/overlay ladder steps must read PASTEL, not white, under any accent; keep tonal ordering + all contrast locks.
- Changes: useStudio ladder re-saturated (+0.10/+0.05 s against the card as steps lighten); §22 shipped defaults re-tuned (216 200 255 / 227 215 255 / 237 228 255); §22.1 midnight trio RESCUED from nesting (built selector `[data-appearance=clay] html[...]` could never match — html as its own descendant; engine rescope to top level with §22.1 statement).
- Commands/gates: tsc clean · vitest 35/35 · clay-paint 175/175 (7 failures traced to the nesting dead-code before the rescue, measured via failing bg hex = unrescued daylight token at midnight) · crystal-contrast 87/87 · motion 12/12 · census 522/600 · audit 0/0 (build 73d6117 fresh).
- Visual evidence: deployed screenshot channel pending at next deploy; DSL ladders measured.


## P03 — elevation tiers + one light variable (D2) · completed 2026-10-13

- Objective: shadows as TIER tokens (well/rest/raised/float), one declared light direction, hover = tier promotion.
- Changes: §23 declarations both modes (--clay-light-x/y; --shadow-well incl. daylight concave + midnight concave; --shadow-float depth-dial folded); migrations: glass-primary hero + card-accent + menu-card + noria hover → --shadow-float (insets kept). Buttons/beads remain documented grammar/micro-exceptions; state never by depth alone (existing edge+transform hover kept and minted with float).
- Gates: build fresh (06c8d45 baseline) · clay-paint 175/175 · contrast 87/87 · motion 12/12 · vitest 35/35 · census 522/600 · audit 0/0 (single clean server: stale-process artifact re-verified and cleared).


## P04 — midnight inset dimming (D3) · completed 2026-10-13

- Defect: 44 literal inset-white call-sites (0.4–0.75) burned day-bright highlights on dark pastel = plastic-glare risk (reference principle 7). The --clay-hi token was correctly dim (0.11) — the literals bypassed it.
- Fix: role tokens --clay-hi-2 (0.55 daylight / 0.12 midnight) and --clay-lip (0.75 / 0.16); 42 numeric insets + 2 zero-blur hairlines migrated, geometry preserved; daylight intensities keep shipped recipe (median mapping).
- Gates: clay-paint 175/175 · contrast 87/87 · motion 12/12 · vitest 35/35 · census 522/600 · audit 0/0.


## P05 — interaction grammar (D4/D6) + P04-regression repair · completed 2026-10-13

- Cross-check caught a P04 side effect: the regex migration replaced geometry, producing 42 malformed shadow items (`inset var(--clay-hi-2)rgb(255 255 255 / V)` — invalid CSS, browsers drop the item silently). Repaired all 42 from the 2cb8b32↔d1cd0bf diff ledger (geometry restored, color→role var): whole-file `)rgb(` syntax scan = 0. Disclosure: this class of defect passed ALL gates — the DSLs don't validate box-shadow item grammar; a syntax guard is queued (folded into P06 scanner).
- §25 one-ring grammar: competing 3px-brand vs 2px-clay+halo focus rules unified to `--clay-focus` token (3px/offset 3px, brand-500 daylight / foil midnight) + one `:where(...):focus-visible` rule; press = transform-only (verified in btn-primary/card-interactive :active); chips exception documented at §21 door.
- Gates: clay-paint 175/175 · contrast 87/87 · motion 12/12 · vitest 35/35 · census 522/600 · audit 0/0.


## P06 — hard-coded color re-audit, scanner in place · completed 2026-10-13

- Deliverable: scripts/color-liter-audit.mjs (repeatable; exit 1 on any unresolved item). Two guards: (1) malformed shadow-item syntax (the P05 regression class: color var glued to a bare rgb() or missing geometry); (2) every #rrggbb literal classified into token-declaration / registry-data (plates, money defaults, brushes, swatch sheets, studio picks) / documented-exceptions (print stylesheet paper grade, OG image SVG, brand specular stops) / inline dynamic styles.
- Result on full src scan: 25 token-decl · 67 registry · 19 documented · 4 inline-dynamic · **0 unresolved** (it did catch 119 raw hexes on first pass, of which ~100 were color-quoting comments — comments are not colors; classifier strips them before classification).
- Fresh exposures fixed along the way: none — every presentation color in the codebase already flows through tokens or is documented data.


## P07 — performance & stability investigation · completed 2026-10-13

- Measured (fresh build, branch HEAD): CSS gz 38.0K/39K budget (raw 221K) · JS gz 492.9K/540K · frost surface census 522/600 (/features worst of 23 pages) · motion contract 12/12 · link/content audit 0/0.
- Clay rendering profile by construction (engine §4 doctrine): no backdrop-filter, no translucency tier on clay; shadow stacks ≤ 4 items; luxury color burden concentrated in tokens, not per-element filters.
- Recorded limitation: production hydration/runtime-exception telemetry (Vercel logs) is not observable from this sandbox; local build + SSR smoke (audit crawls built pages) show no failures. Not claimed verified.
- No perf-relevant diffs this phase: PASS = preserved budgets.


## P08 — accessibility pass · completed 2026-10-13

- Focus grammar: §25 --clay-focus (3px/3px offset, brand-500 daylight / foil midnight + halo) on all interactive types incl. summary/[tabindex] (P05).
- Contrast: crystal suite 87/87 pairs incl. all dial extremes; clay-paint 175/175 (luminance floors, midnight/daylight, ladder steps measured in vitest: card<data<overlay ordering asserted).
- Reduced motion: motion suite 12/12 (PRM guards verified scan-wide).
- Semantics census (code evidence): 308 aria attributes across components; ThemeStudio 16 (aria-pressed rows/mode); Navbar 7 (menu expanded/controls); DemoRequestForm labels htmlFor + aria-invalid + role=alert; alt attributes on media (counted).
- Recorded limitation: keyboard-walk + screen-reader pass needs real browser assistive tooling (sandbox lacks browsers) — code-level evidence only; not claimed as manual verification.


## P09 — independent final audit · completed 2026-10-13

- Fresh end-to-end re-run (nothing reused from phase text); route/theme coverage as tabulated in CLAYMORPHISM_FINAL_AUDIT.md; all gates green; queue contained only P09 itself at audit start.
- Action: queue file deleted after last entry (per master-program rule Part 7 §“When to delete the remaining-phase file”). Final scores 90/96/90/85/88/90/84 with criteria + limitation ledger in the final audit document. No 100/100 claimed; 100/100 marked as not-used.
