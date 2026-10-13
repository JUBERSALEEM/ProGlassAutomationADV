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


## P10 — Paper Mode (program extension, user-directed) · completed 2026-10-13

- Origin: the user's reference-compare (ClayDash cream / ClayHub fog) — ambient neutrality is what makes plates ignite; our hue-carrying bands+pools ("blue inside blue") dulled the pop.
- Change: clay-family-only ambient recalibration in the adoption block — band at s0.12 (hue whisper L~0.955/light, 0.18/midnight), wallpaper pools desaturated (s0.35, gossamer alphas); cards/channels/tints keep full saturation. Crystal & glass wallpaper keep their own grammar (family-gated: the floors still breathe the accent there).
- New contract test (#36): clay ambient purity < card purity×2 (regression guard).
- Gates: vitest 36/36 · clay-paint 175/175 · contrast 87/87 · motion 12/12 · color-audit 0 · census 522/600 · audit 0/0.


## P11 — chip micro-pillows (§26, user-directed) · completed 2026-10-13

- Re-voiced .chip from a washed well to a RAISED micro-pillow: solid pastel face (0.55 daylight), lip crest lit top, small grounding drop (2px 3px 6px @0.22, + thin bottom shade) — the ClayHub delta-chip grammar. Same pill geometry for utility beads via §26: rounded-full bg-ink/brand/state + rounded-md bg-state tiles get lip+drop while keeping semantic faces and staying outside the §21 card ladder.
- Harness-side catch + fix (the loop works): midnight chip face at 0.55 lifted pastel past the 4.5:1 floor for its light text → midnight override keeps the wash face (0.3), pillow geometry unchanged.
- Gates: clay-paint 175/175 · contrast 87/87 · motion 12/12 · vitest 36/36 · color-audit 0 · census 522/600 · audit 0/0.


## P12 — default richness (user witness: "still white cards") · completed 2026-10-13

-Witness evidence: 12:19–12:20 deployed screenshots with NO accent picked — noria plates read saturated but every generic card white. Compared against the 10:42 pick-active build (cards visibly pastel). Mechanics: the §22 DEFAULT ladder (216/227/237, s-tapered cousin of the plates) was the wash the user named.
- Change: shipped defaults rise to plate-family richness — card-face 206 196 255 (the house plate body itself), data 224 216 255, overlay 238 230 255; midnight trio deepens (62 40 94 / 76 50 112 / 52 32 76). Pick-active adoption already wrote full-plate saturation — defaults now match that poverty-line elimination.
- Gates: vitest 36/36 · clay-paint 175/175 · contrast 87/87 · motion 12/12 · color-audit 0 · census 522/600 · audit 0/0.


## P13 — one hue family (user witness: "where from is this violet?") · completed 2026-10-13

- Witness evidence: 12:44 + 12:46 deployed midnight screenshots — orange accent, then green, and the banded sections (.band-rich "raised" tone — overthinking / "what that background produced" / FAQ on /developer, plus every Section tone=raised|sunken site-wide) stayed saturated violet both times while page, pools, plates and cards all rotated. The band was accent-deaf.
- Root cause (trace, not one-line patch): the zone-band family never joined the adoption grammar. `.band-rich` midnight face was the literal `rgb(60 42 94)`; `.band-deep` and `.glass-primary` (§12.3) `rgb(50 34 76)`; daylight `.band-rich`/`.glass-primary` read the full-saturation `--clay-lavender` channel (rot(0,0.68,0.72) under a pick — a loud accent slab, against P10's paper decree). P10 wrote the calibrated `--band-face` whisper, but no band class consumed a band token — only `.band-solid`/`.feature-plates` do. Same species in the mould: `rgb(92 60 220 / …)` glow literals inside every band-scoped box-shadow.
- Change (engine): the faces/glows are tokenised with house-stable defaults — new `--band-rich-h` (lit band), `--band-deep-h` (sunken/dream band), `--band-glow-h` (mould glow), declared in the §13 daylight bench (resolving to var(--clay-lavender)/var(--panel-face)/92 60 220 — byte-identical house paint) and re-declared in the §-midnight block (60 42 94 / 50 34 76), mirroring the --band-face theme-scope pattern. Midnight band-button insets swap 140 108 255 → var(--clay-drop-h) (default-identical, already accent-rotated). Adoption block (clay family only) now writes all three: sunken deep → lit band one step under the card face at midnight (rot(-24): s0.26/L0.19 → s0.3/L0.235), paper-lit whispers by day (L0.9/L0.93), warm glow rot(0,0.8, L0.5 dark / 0.42 light).
- Harness-side catch (the loop works, twice): first paint run failed 5/175 — the midnight defaults had been anchored into the universal token block and clobbered daylight (daylight probes resolved #32224C/#3C2A5E). Moved to the correct theme scopes; full green restored. The violet literal is now impossible without the harness naming the probe.
- Change (page, the "broken main card layer"): /developer's gaps list wore `.card-accent` ×4 — the pastel accent grammar (the deliberate 10% of the 70/20/10 budget) used as the section's MAIN content cards: four peach slabs floating on the dark page, hierarchy inverted at midnight. The list now wears `.card-live` (the same live card the five story cards wear); the single closing "You talk" card keeps card-accent as the one intentional pastel accent. Error/collaborate/sponsors/legal/home accent cards are single-use and stay.
- New contract test (#37): midnight clay + orange pick ⇒ the band tokens are written, the lit band's red channel leads its blue (violet impossible), the ladder deep < lit < card-face holds, and white band ink keeps ≥7:1 on the lit band.
- Gates: vitest 37/37 · clay-paint 175/175 · contrast 87/87 · motion 12/12 · color-audit 0 · census 100% · audit 0/0 · crystal-contrast green.


## P14 — the verb joins the bench (user witness: "hard coded button… make these dynamic") · completed 2026-10-13

- Witness evidence: 13:17 deployed midnight screenshots of /developer, same page under two picked accents. Rotation confirmed working (page, bands, dossier, tiles all re-hued), but four elements read wrong: the primary pill stayed house-coral in BOTH shots; the meta tiles and the whole dossier painted as saturated neon bricks (picked-midnight ladder at s0.62–0.72, L0.30–0.37); the stat-tile noria showed fuchsia-on-salmon and violet-on-green mixes (plate offsets spanning the full hue circle); the neutral badges stayed fixed navy under every accent.
- Root cause set (four literals/stale recipes, all adoption-side — stylesheet defaults untouched, so the house look and every harness floor keep guarding the shipped paint):
  1. `--clay-coral`/`--clay-coral-h` were constants (230 110 96 / 230 81 68) — the only button family whose hue no token could move.
  2. D1's midnight ladder values predate OLED-picked accents: full plate chroma at L≤0.37 glows as brick.
  3. plateHues offsets [0,55,110,180,245,310] = full-circle rainbow under any hub.
  4. `--clay-foil` (neutral badges/utility panes) never twisted with the ink channels.
- Changes (useStudio adoption block only):
  • The verb takes the bench hue with its own contract preserved: white bold labels ≥3.2:1 (house coral documents 3.1:1) — lightness is SOLVED per hue (greens/yellows sit perceptually lighter; one recipe would wash their labels). coral-h steps with it.
  • Midnight ladder recalibrated: chroma capped (s≤0.45/0.5/0.4), the three steps separate in lightness instead (overlay 0.26 < card 0.29 < data 0.34). Daylight recipe unchanged (D1 richness stays).
  • Noria compressed to an analogous arc ±92° around the picked hue — six distinct pastel cousins, never confetti.
  • `--clay-foil` twists with the hub at the ink channels' own contract (hue-only, luminance untouched), keeping its rgb()-wrapped form.
- New contract test (#38, worst-hue aware): for orange/green/blue picks at midnight — coral ≠ house literal AND white ≥3.2:1; all three ladder faces hold white ≥4.5:1 and their luminance order; every plate sits within 96° of the bench hue; the foil left its navy literal.
- Gates: vitest 38/38 · clay-paint 175/175 · contrast 87/87 (crystal) + 25/25 (clay) · motion 12/12 · color-audit 0 · census 100% · build+tsc+eslint clean.
