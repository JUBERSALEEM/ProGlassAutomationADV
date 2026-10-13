# Claymorphism Remaining Phases — live work queue

*Rules: a phase leaves this file only when its implementation AND verification pass (evidence appended to CLAYMORPHISM_PHASE_HISTORY.md). Generated from actual findings, not targets.*

- [ ] **P06 — hard-coded color re-audit → zero unexplained literals (incl. JS-generated styles & inline style objects).** Add a repeatable scanner to scripts/ (source + computed DSL). Acceptance: scanner report empty of "unresolved" class.
- [ ] **P07 — performance & stability investigation (PART 9).** Bundle diff ≤ +2KB css-gz target, census 522/600, audit 0/0, hydration errors = 0 (vercel log inspection impossible locally — limitation recorded), reduced-motion proof via motion suite.
- [ ] **P08 — accessibility pass (PART 10).** Contrast re-run incl. ladder steps; keyboard navigation on nav+studio+menu; reduced-motion. Acceptance: 87/87 + manual keyboard evidence logged.
- [ ] **P09 — independent final audit + scores (PART 11/12).** Re-run everything fresh; FINAL_AUDIT.md with the seven scores + evidence; delete this queue only if empty.
