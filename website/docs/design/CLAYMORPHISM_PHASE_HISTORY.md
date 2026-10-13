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


## P15 — midnight kills the lamps (user witness: "extra glowing section") · completed 2026-10-13

- Witness: 13:42 midnight /developer (house palette) — the Engineering Metrics stat tiles carried a white xenon halo on the dark page.
- Trace: the noria recipe's rim light is a daylight instrument that never got a midnight branch — `-7px -7px 18px rgb(255 255 255 / 0.85)` ambient plus a 0.3 colored bleed on every `.stat-tile:nth-child(4n±)`, and the same lamp at 0.7 on the 36-unit index/icon tiles. §24 had capped bright INSETS ("plastic glare on dark pastel"); the amBIENT lamp outside the tile was untouched until now. Faces unchanged — §9.4's lamps are the pastel FACES by design, the halo was the defect.
- Change: midnight-scoped shadow stacks for the four noria tiles (white ambient removed, bleed halved to 0.16/0.18) and for `rounded-[0.6rem]` carriers (ambient out, violet inset → P13's `--band-glow-h` token — byte-identical at house, accent-aware under a pick).
- Gates: build+tsc · vitest 38/38 · clay-paint 175/175 · contrast 87/87 + 25/25 · motion 12/12 · color-audit 0 · census 100%.


## P16 — the meniscus pass (user: "crystal… not excellent like these iOS shots") · completed 2026-10-13

- Witness: five iOS-26/27 Liquid Glass references (weather card, two music widgets, Jack White tile, Focus capsule) against our deployed crystal. Read against the audit in §C7: the engine already ships the pattern's blur (24px × frost dial), 180% vibrancy, rich pools, the wide drop, ring grammar — what the references have and we lacked is the RIM: our edge was a uniform 1px ring; theirs is a directional arc (key light strikes the top-left curve, the under-edge dies, a cool relight answers from below) plus the thin wet specular line just under the top edge. That meniscus is the whole "liquid" tell.
- Honesty on "exactly": true optical refraction (background lensing/distortion) has no portable web substrate — SVG feDisplacementMap filters elements, never the backdrop; backdrop-filter: url() is Chrome-experimental only. The pass ships every portable signature; the lensing stays Apple's.
- Change (crystal-scoped only — the family isolation doctrine holds):
  • §C9 meniscus rim: a masked 1px conic arc (per-mode token `--glass-meniscus` — white strike → dead middle → cyan relight at midnight, rose by day) painted as ::before over the pane's own radius on the drop-shadow registry (.surface/.surface-raised/.menu-card/.float-card/.glass-primary/.card-accent/.panel-feature/.stat-tile); @supports-guarded so mask-less engines lose the arc, never legibility.
  • §C1 spec streak: a 14%-deep wet line (`--glass-spec-a` 0.14 midnight / 0.26 daylight) prepended to the lift's own background stack — under content, composed over the scrim, never instead of it. Text floors untouched by construction.
- Gates: build+tsc · crystal-contrast 87/87 · motion 12/12 · blur census 522/600 (no new blurred surfaces) · clay-paint 175/175 (isolation green) · clay contrast 25/25 · vitest 38/38 · coverage 100% · color-audit 0.
- Tranche-2 candidates (if the next witness asks): hover glint shift on interactive panes; per-context pane tints; a photo-backed showcase band (the references' real multiplier — glass sings over photography; our walls are abstract pools).


## P17 — the phone-scale pass: mid-wall colour + the launcher returns (user witness: "not even close" · "why did you remove the theme and colour plate") · completed 2026-10-13

- Witness: 14:12 deployed crystal screenshots against the same iOS references — on 360-390px swaths the engine still read as flat frosted slate (user: not one of five telltales landed), AND the ThemeStudio launcher (menu-card, `fixed bottom-5 right-5`) had vanished from the viewport entirely on crystal pages — the family could no longer be switched or re-themed.
- Root causes (two, both crystal-side, verified by trace):
  1. P16's §C9 anvil rule declared `position: relative` on the WHOLE drop-shadow registry. In CSS, two declarations of equal specificity and property resolve by source order — the engine loads after Tailwind — so `relative` OUTRANKED Tailwind's `.fixed`, un-anchoring the launcher off-viewport. The meniscus pass disabled the very control used to visit midnight, pick accents, and switch families (the user's "why did you remove the theme and colour plate").
  2. Frost at 180% over a bare page IS slate: the wallpaper gave glass nothing to bend except at the very crown and floor (the pools' geometry), so the entire mid-vault — where a phone-length viewport spends its scroll — was colourless. Colour behind the pane is the references' real substrate.
- Changes:
  • §C9 guard: the anvil now reads `:is(registry):not(.fixed):not(.absolute):not(.sticky):not(.relative)` — relative is declared only on panes that carry no position utility; anchored panes (the launcher, dropdowns, sticky bars) are already positioned anvils for the ::before and keep their exact P15 anchoring.
  • Wallpaper 4b (globals.css, shared grammar): two mid-vault pools added to `body::after` as `--wall-mid-1`/`--wall-mid-2` layers, OFF by default (`var(--wall-mid-1, transparent)`) — clay and every other family composite pixel-identically; the variable layer count on `background-repeat` extended accordingly. Anchored at 12%/88% gutters so colour lives behind panes and margins, not behind running text; depth 760×520/720×500 keeps them behind the vault, not on it.
  • Crystal buys the seam (this is the family's own doctrine — colour for the bend, §C·A): midnight cyan 0.40 / rose 0.32; daylight aqua 0.34 / rose 0.30.
  • Phone-scale intensity: `--glass-sat` 180→195%; midnight meniscus brightened (strike 0.50→0.68, tele 0.16→0.22) with the references' caustic corner fringe (cyan 0.40 at 88%, rose 0.34 at 100%); daylight strike 0.85→0.92, relights rose 0.40 / cyan 0.34; `--glass-spec-a` 0.14→0.18 midnight, 0.26→0.30 daylight. All arc work stays on the masked 1px ::before, so no text floor is exercised by construction.
- Discipline check on "exact": still no feDisplacementMap lensing (no portable substrate — P16 record); the phone-scale gap is closed through colour-behind-pane fidelity + rim intensity, the two portable differences left.
- Gates: build fresh (de14053) · crystal-contrast 87/87 · blur census 522/600 (no new blurred surfaces — paint on the existing wallpaper element only) · vitest 38/38 (clay wallpaper-tint-contract green: mid pools default transparent) · guard:pointer 158 files clean · clay coverage 100% (engine lift active) · clay-paint 175/175 (family isolation: globals layer is inert off-crystal).
- Residual honesty: perceived as richer but abstract — the photo-backed showcase band from the P16 candidate list remains the largest single realism multiplier if a later witness still asks for "exact".


## P18 — the studio, forensically: click-through suite, the NaN-poison class, and everything the plates touch now moves (user witness: "theme & colour card plates theme studio not working … frostness also not working properly … complete senior analysis") · completed 2026-10-13

- Method (the senior part the complaint asked for): before touching anything, a REAL click-through suite was written (`tests/studio-interaction.test.tsx`, jsdom + testing-library, 17 tests driving the actual panel with actual pointer events against the actual stores). Verdict on the baseline: mode plates (Light/Dark/System), palette-family plates, the 16 accent plates (ramp + Material-You clay adoption), frost/body/glint writes + reset, the clay plate rows, and Reset-all were ALL CORRECT — the suite proves the applied DOM state after every click, not the painted pixel. "Not working" was therefore reduced to what the suite alone could not see, and three genuine failure classes were found beyond it:
- **Defect 1 — the NaN-poison class (the "frostness not working" root).** `applyStudioVars` trusted its input: any non-finite numeric dial became the literal string `NaN` inside a custom property, e.g. `--glass-frost-mult: NaN` → `blur(calc(24px * NaN))` → the browser drops the ENTIRE backdrop-filter declaration → every frosted pane on the family goes unfrosted silently. NaN vividness/brightness poisoned the whole `--brand-*` ramp (every accent plate paints nothing). JSON storage compounds it: `NaN` serialises to `null`, which passed the old partial read-guards and clamped to `0.25` — frost stuck permanently "nearly clear" across reloads, and the boot script replayed the poisoned blob on every visit. Three entry holes closed: `decodeStudio` (share links) ran zero validation; `readStored` guarded a cherry-picked subset; hygiene-v2 cleared paint blobs but left the state blob. Fix: ONE `normalizeStudio()` spectrum — every dial finite-checked and clamped to its own range, hexes type- and pattern-checked, enums by strict membership — run on every read path AND self-healing (if normalisation changed anything, the healed state is written back over the poisoned storage on the spot); the write path re-clamps each numeric token through `finite()` before it can touch the DOM; the boot script skips any `NaN|Infinity` value on replay. Contract tests assert: poisoned storage ↦ field-by-field defaults, poisoned share link ↦ same, and an all-NaN bench writes ZERO NaN/Infinity into the DOM while still painting the ramp.
- **Defect 2 — crystal wallpaper never adopted the accent (the "colour plates not working" root, P17's own shadow).*— The more prominent P17 made the walls, the more obvious it became that they never rotated: washes, mid pools and aurora balls stayed the shipped cyan/violet/rose under any pick, so on the crystal family a colour plate visibly changed almost nothing. Fixed in the family's own grammar: the relight stops of `--glass-meniscus` and the three aurora balls became fallback-tokenized slots (`--glass-relight-1/2`, `--aurora-ball-1/2/3` — fallbacks byte-exact the shipped paint, engine owns the alpha grammar), and useStudio's crystal branch now rotates washes + mid pools + balls + relight from the accent hub per mode. Clean bench composites identically; a pick re-hues the whole wallpaper like the pools already did. Family isolation proven both ways: clay picks write no crystal wall tokens; crystal picks write no clay paper tokens.
- **Defect 3 — the UX lie: benches that look live while inert.** The crystal bench dials and clay bench rows rendered fully interactive under families they do nothing in — the exact shape of "I dragged frost and nothing changed." Now, when the bench's family isn't active, the bench's ownership sentence becomes a bordered notice chip (reusing the same translated strings, zero new dict keys) and the controls render muted + pointer-events:none, so off-family benches explain instead of impersonating. Suite test: gate wrapper present under clay, gone under crystal, dialog interaction resumes.
- Senior recommendations implemented alongside: copy facts fixed (frost hint said "26–28px with 200% … 0.40" — shipped truth is 24px @ 195%, dial min 0.25; re-keyed and filled in the five prepared locales, old key retired); storage self-heal means a poisoned bench repairs itself on next read without a "Reset everything" hunt; the family-switch-after-pick regression (the historic "plates work intermittently" bug) is now a locked contract test.
- i18n housekeeping exposed by the method: `keys.ts` had not been regenerated since P-batches (106 site strings unregistered); regenerated keys/coverage-table/route-overlays. `i18n:verify` reports 8 locales "marked complete" at ~75% — PRE-EXISTING drift (verified identical verdict with the original stale key list; itemised for the i18n programme, out of P18 scope). No runtime rendering change: untranslated keys render the English sentence exactly as before.
- Gates: tsc clean · eslint clean (touched files + suite) · build ✓ · vitest 55/55 (38 + 17 new interaction/hardening contracts) · crystal-contrast 87/87 · blur census 522/600 · clay-contrast 25/25 + coverage 100% · pointer guard 158 files clean · color-liter-audit 0 unresolved.


## P19 — the studio audit: every dial either moves paint or stands down (user witness: "theme & colour card plates, theme studio not working … frostness also not working") · completed 2026-10-13

- Method (senior analysis, not guessing): every studio WRITE path was ledged against every READ path in the corpus — 60 written custom properties × consumer count across the three engine files + Tailwind registry, plus the five written attributes × CSS selectors, plus the click-through suite (21 jsdom DOM tests driving the real panel against the real stores, no mocks). Three defects fell out of the ledger; the rest of the bench proved wired end-to-end, including frost (28 consumers of --glass-frost-mult, blur calc'd from the dial on every pane class).
- Defect 1 — the dead principal light (the real "plates do nothing"): every family block declares --pool-1 as its main ambient pool (13 declarations; clay's midnight whisper 0.13 §9.1; crystal's 0.62 mass), and the studio rotates it on every pick at three write sites. But the `body::after` wallpaper stack — which consumes --pool-2 and --pool-3 — had lost the pool-1 layer in the wash-era rewrite: the strongest declared light was painted by nothing. On a pick, pools 2/3 rotated and the wall's biggest mass didn't — the page ambient read as "barely following the accent". Fix: the crown pool is restored as layer 2b of the wallpaper (1080×620 at the ceiling between the gutter washes; `var(--pool-1, transparent)` so alpha grammar stays owned by the family token blocks; background-repeat count synced). Harness-visible: clay-paint recomposites the page through the new stack, 175/175 still green with the light on.
- Defect 2 — the family gates were veils, not gates (the real "frost not working"): the glass bench and the clay plate rows were wrapped in `pointer-events-none` when off-family — which stops the mouse and never the keyboard. Tabbing the drawer let the frost dial still be dragged with arrow keys, writing --glass-frost-mult live while no consumer read it on the active family: a dial that moves and visibly does nothing, precisely the reported failure. Fix: `disabled` is now the gate — Slider takes a `disabled` prop, all three glass dials + both plate rows + bench reset/link buttons carry it, off-canvas of veil classes.
- Defect 3 — the launcher's accent dot lied: with no pick it fell back to a house-violet literal (#7E57CE) on every family — a fixed purple dot asserting an accent the page did not wear. Fix: the dot is the live accent — picked plate, else the active family's own swatch.
- Housekeeping from the same pass: two baseline eslint errors (unused `SHADOW_OK` regex in the P06 audit script, unused translator binding on the internal clay catalog), one mojibake em-dash in useStudio comments.
- Senior recommendations implemented (not just fixes): the write↔consumer ledger is now a repeatable audit artefact (re-run: extract `vars["--x"] =` from useStudio, count `var(--x` across the corpus — any DEAD row is a defect by definition); new tests lock the regressions — wallpaper consumes var(--pool-1), gated dials/buttons are actually disabled off-family and live on-family, the accent dot is the family swatch with no pick (59/59 total, +4).
- Gates: build fresh · tsc clean · eslint clean (2 baseline errors cleared) · clay-paint 175/175 · clay-contrast 25/25 · crystal-contrast 87/87 · blur census 522/600 (crawl of the fresh prod server) · pointer guard 158 files · colour audit 0 unresolved · vitest 59/59.
- Honesty on scope: what this phase can certify is wiring, gating and paint — every control now provably lands on a token a consumer reads, or is verifiably disabled. Live-device drag smoothness and deploy freshness are observed by the next witness round, not claimed here.
