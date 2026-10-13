# Claymorphism Remaining Phases — live work queue

*Rules: a phase leaves this file only when its implementation AND verification pass (evidence appended to CLAYMORPHISM_PHASE_HISTORY.md). Generated from actual findings, not targets.*

- [ ] **P02 — pale-step recalibration (D1).** Data/overlay steps keep hue + raise saturation while stepping lightness — data/overlay read pastel, not white, on every accent. Acceptance: contrast suite 87/87 incl. all dial extremes; clay-paint 175/175; ladder ordering measured (card<data<overlay stays); screenshot from user deploy shows data pills colored on blue accent.
- [ ] **P03 — elevation tiers + one light variable (D2).** `--clay-light` direction + `--elev-0..3` token maps; every clay surface consumes a tier; hover on interactive cards promotes a tier. Acceptance: no per-class shadow literals left except documented tokens; motion suite 12/12 green.
- [ ] **P04 — midnight inset dimming (D3).** Insets dim ~35% + fill-tier carries depth at midnight. Acceptance: midnight clay screenshot shows matte puff (no plastic glare) + contrast suite green.
- [ ] **P05 — interaction grammar pass (D4/D6).** transform-only press on all clay buttons/cards (PRM-gated); `:focus-visible` 3px token everywhere clay controls exist; chips exception documented IN the token block. Acceptance: motion + audit gates; keyboard walk record on /features.
- [ ] **P06 — hard-coded color re-audit → zero unexplained literals (incl. JS-generated styles & inline style objects).** Add a repeatable scanner to scripts/ (source + computed DSL). Acceptance: scanner report empty of "unresolved" class.
- [ ] **P07 — performance & stability investigation (PART 9).** Bundle diff ≤ +2KB css-gz target, census 522/600, audit 0/0, hydration errors = 0 (vercel log inspection impossible locally — limitation recorded), reduced-motion proof via motion suite.
- [ ] **P08 — accessibility pass (PART 10).** Contrast re-run incl. ladder steps; keyboard navigation on nav+studio+menu; reduced-motion. Acceptance: 87/87 + manual keyboard evidence logged.
- [ ] **P09 — independent final audit + scores (PART 11/12).** Re-run everything fresh; FINAL_AUDIT.md with the seven scores + evidence; delete this queue only if empty.
