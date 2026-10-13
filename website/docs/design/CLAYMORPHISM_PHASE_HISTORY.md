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
