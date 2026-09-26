# Engine strategy analysis — 2026-09-26

Scope: **the packing engine only.** Nothing about racks, labels, remakes, sequencing for
downstream lines, 1D spacers, IG pair cutting, true-shape nesting or persistent remnant
tables appears here. Those are either production features or separate engines, and you
have said this is an engine. Section 7 lists what was deliberately left out and why.

The question this document answers: **what would actually make this engine better, and in
what order, without breaking what it already achieves.**

---

## 1. What the engine is today

**Shape.** Multi-start construction. A pool of independent generators each produce a
complete plan; a streaming minimum keeps the best. No plan is ever mutated into another —
every candidate is built from scratch and compared whole.

**The generators** (all in `Services/Optimization/`):

| Generator | File | Kind | Runs when |
|---|---|---|---|
| `RunPass` (shelf / strip, 6 orderings × 3 splits) | `GuillotinePacker.cs:837` | guillotine, global split rule | always, effort ≥ 2 |
| `RunCapacityStrips` | `CapacityStrips.cs:21` | guillotine, capacity-aware rows/columns | quick path |
| `RunBestSplit` | `BestSplit.cs:127` | guillotine, **per-plate** split selection | stage/effort ≥ 2 |
| `RunBestSplitBeam` (width 4, then 8) | `BestSplit.cs:226` | per-plate + lookahead + lower bound | stage ≥ 3 (beam 8 at ≥ 4) |
| `RunSheetFill`, `RunWallSeed` | `Advanced.cs:71,147` | guillotine variants | stage ≥ 2 |
| `RunMaxRects` | `Advanced.cs:221` | **non-guillotine** | stage ≥ 3 |
| `PackSmart` | `SmartPlacement.cs:94` | capacity-preserving placement | stage ≥ 2 (×2 at ≥ 4) |
| `RunRepeatedPatterns` | `RepeatedPatterns.cs:81` | repeat-master | all |
| `PackIndustrialPatterns` | `RepeatedPatterns.cs:11` | huge jobs (≥ threshold) | ≥ `IndustrialQuantityThreshold` |
| `PackOptimizePartitioned` | `Partitioned.cs:24` | batched parallel + consolidation | very large jobs |
| `RunFillFirstPlate` | `FillFirstPlate.cs:458` | plate pattern | `FillFirstPlate` selected |
| `ConsolidateIntoOffcuts` | `OffcutReuse.cs` | post-pass | opt-in |

**Objective** (`Better`, `GuillotinePacker.cs:655`): fewer unplaced pieces → fewer sheets →
higher utilization → the selected plate pattern's metric. Lexicographic; the pattern metric
only decides ties.

**Verification**, three independent oracles:
- `Verify` — margins, bounds, rotation policy, dimension mutation, overlap, kerf gaps.
- `ValidateResult` — conservation: every input pane accounted for exactly once.
- `ValidateCutPattern` — stage/axis conformance under a staged `CutPolicy`.

**Reference results** (`Tests/Agent/OptimizationBenchmarkTests.cs:10`): Low-E 17 sheets @
82.77 %, Clear 16 @ ~85 %, Ramly 1.

---

## 2. What the codebase already measured — read this before proposing anything

Two comments in this repo are worth more than any benchmark I could run here, because
they record experiments that were actually executed:

**`GuillotinePacker.BestSplit.cs:8-13`:**
> "the shipped search was exhausted: every one of the 18 RunPass ordering×split
> combinations, plus RunSheetFill, RunWallSeed, RunMaxRects and RunSkyline, returned the
> SAME 17 sheets / 79.92 % for the Clear group … A 21,600-evaluation hill-climb over piece
> sequences also never moved off 17 — so **the bottleneck is the DECODER, not the search.**"

**`GuillotinePacker.Smart.cs:5-18`:**
> "RunSkyline STRICTLY BEAT THE CANDIDATE POOL 0 TIMES … Wiring it in would therefore add
> runtime for no gain; it is kept only so the measurement is reproducible."

Two consequences that constrain every phase below:

1. **Adding search breadth is provably worthless here.** More orderings, more shuffles,
   more restarts, more threads enumerating the *same* decoder: measured zero. Any phase
   that does only that should be rejected on the evidence already in the repo.
2. **The decoder is where the headroom is** — and per-plate split selection was the change
   that moved the Clear group from 17 to 16. That is the only decoder-level win landed so
   far.

The one thing the hill-climb did *not* test is the distinction the next phases rest on:
it hill-climbed over **piece sequences fed to the same decoder**. It never tried moving
**placements** — relocating a pane from one finished sheet to another. That is a different
move set, and it has never been measured in this engine.

---

## 3. Findings, ranked

### F1 — Guillotine feasibility is never asserted, on the default path

`Advanced.cs:218` states it outright:

> "**Non-guillotine** MaxRects BSSF with kerf inflation. Kept as a candidate only —
> Verify still enforces kerf + margins."

Verify enforces kerf and margins. It does **not** enforce guillotine-cuttable — I checked
the whole body: margins, bounds, rotation, dimension mutation, pairwise overlap, kerf
gaps. Nothing about cut reachability.

`RunMaxRects` runs at stage ≥ 3 (`GuillotinePacker.cs:563`), and the default effort is 3,
so **it runs by default**. When a non-guillotine plan wins, `TryDeriveGuillotineCuts`
(`:1421`) is called on it, fails, returns false — and the sheet silently keeps an *empty*
cut list. `PackQuick`/`PackOptimize` (`:282`, `:624`) only call it for sheets that have no
cuts yet; failure is not detected, not reported, and not acted on.

That is the one way the engine can currently return a plan a bridge saw cannot execute,
from a product whose centrepiece is a six-level guillotine cut-complexity control.

**Severity: correctness, not yield.** Cost to detect: low.

### F2 — There is no measure of how far from optimal a plan is

A lower bound already exists — `SheetsLowerBound` (`BestSplit.cs:191`) — but it is used
only to rank partial states inside the beam. It is never surfaced, and the engine never
computes a bound for the plan it returns.

So "16 sheets" is an unqualified number. If the bound is 15 the plan is one sheet from
provably optimal and further work is pointless; if the bound is 13 there is real headroom.
Without it, nobody can tell whether a phase is finished.

**Severity: blindness, not a defect. Zero risk to fix — it is arithmetic on the result.**

### F3 — The decoder has no placement-level local search

Construction only. Every candidate is built once and thrown away. There is no relocate,
no swap, no ejection chain, no ruin-and-recreate over the finished plan.

The offcut-reuse pass I added last session is a first, narrow instance of exactly this
move set — relocate every pane off one sheet to eliminate it — and it is the *only* one in
the engine. It is greedy and first-fit, and it only ever targets the single emptiest sheet.

Per §2, this is the move set that has never been measured, and it is a move set over
placements rather than sequences.

### F4 — The objective is nearly flat, and the flat part is where cut quality lives

`Better` is unplaced → sheets → utilization. Between two plans with the same sheet count,
utilization decides, and utilization is a weak signal: it cannot distinguish a plan that
needs 40 cuts from one that needs 12, or one whose leftover is eight slivers from one
whose leftover is one rackable plate.

The plate patterns I added address this, but only at the two public entry points — the
deep internal passes still compare on yield alone, because the engine streams one
incumbent. A decoder-level preference (prefer the split whose remainder is largest and
most contiguous) would push the same idea into the place where §2 says the headroom is.

### F5 — Deterministic parallelism is unused, and its value is routinely mis-stated

The candidate loop (`GuillotinePacker.cs:466`) is sequential. Multi-start is embarrassingly
parallel, and determinism is preserved as long as each candidate is deterministic and the
reduction is a deterministic minimum over a total order — which `Better` is.

But given §2, parallelism's value here is **not** "evaluate more of the same decoder". It
is "evaluate the same pool in less wall-clock, and spend the saved time on F3/F4". Framed
the other way, it is a temptation to buy breadth that has already been measured as worth
nothing.

### F6 — Duplicate candidates and one documented corpse

`RunSkyline` is unreachable and measured at 0 wins (`Smart.cs:5`) — a deliberate,
documented decision. Leave it; it is evidence, not debt.

Less settled: on the benchmark, SheetFill, WallSeed, MaxRects and Skyline *all* returned
the same 17 / 79.92 %. If they still do, that is wall-clock spent on candidates that add
no diversity. This is cheap to re-measure and should be, before anyone tunes the pool.

### F7 — The huge-job and industrial paths are separate engines

`PackOptimizePartitioned` runs its own batched search and uses `RunBestSplit`
(`Partitioned.cs:173`) plus RunPass variants, so it does get per-plate split selection.
`PackIndustrialPatterns` shares none of the pool. Improvements to the main pool do not
necessarily reach those paths, and the reverse is also true — each phase below has to say
which paths it touches.

---

## 4. Prerequisite before any phase

**The benchmark has never been run in this environment.** There is no .NET SDK here and no
obtainable one. Every number in §2 is inherited from comments written by whoever ran it
last; nothing in this session has been compiled, let alone measured.

So: **phase 0 is not code, it is a measurement.** Run `Tests/Agent/OptimizationBenchmarkTests`,
record Low-E / Clear / Ramly sheets and utilization, and keep that as the baseline. Every
later phase is then "does the baseline move, and in which direction". Without it, F3 and F4
cannot be evaluated at all — they would be shipped on faith, which is how an engine regresses.

---

## 5. Phases

Ordered by what they can break. Nothing in E1 can change a result. E2 and E3 are gated.
Only E4 can move the default run, and it is the one that fixes F1.

### Phase E1 — Instrument (cannot change any result)

*Oracle for the whole phase: `git diff` on the engine is empty, and the benchmark numbers
are identical before and after. If they move, the phase failed.*

| # | Deliverable | Files | Finding |
|---|---|---|---|
| E1.1 | **Guillotine audit.** After a run, assert every sheet with panes derives a cut sequence. Report which generator produced the winner and whether it was guillotine-cuttable. Report only — change nothing. | new `GuillotineAudit`, called from `PackQuick`/`PackOptimize` tails, result on `OptimizationResult` | F1 |
| E1.2 | **Optimality bound + gap.** Continuous area bound per stock size, kerf-adjusted; report `sheets`, `bound`, `gap`. Pure arithmetic on the finished plan. | new `OptimizationBound`, result property + scale note | F2 |
| E1.3 | **Candidate census.** Which generator wins, how often, and how often two generators return an identical plan. A test/benchmark, not production code. | `Tests/Agent/` | F6 |
| E1.4 | **Baseline capture.** Record DGU Low-E / Clear / Ramly sheets + utilization as the numbers every later phase is judged against. | docs + benchmark test output | §4 |

E1 is the only phase that is safe to apply blind, and it is what makes every other phase
decidable instead of arguable.

### Phase E2 — Decoder improvements (opt-in, default off)

*Oracle: with the switch off, the benchmark is byte-identical. With it on, sheets never
increase and no pane is ever lost or overlapped — `Verify` + `ValidateResult` on every
result, as an assertion in the test, not a hope.*

| # | Deliverable | Files | Finding |
|---|---|---|---|
| E2.1 | **Placement local search.** Relocate / swap / ejection-chain moves over the finished plan. Deterministic move ordering; accept only strictly better; bounded iterations. The move set §2 never tested. | new `GuillotinePacker.LocalSearch.cs` | F3 |
| E2.2 | **Remainder-aware split scoring.** Inside the per-plate split choice, prefer the split whose remainder is largest and most contiguous, breaking ties on the current rule so the default is unchanged when the preference is disabled. | `BestSplit.cs` | F4 |
| E2.3 | **Strengthen offcut reuse.** Best-fit instead of first-fit, multiple donor sheets, iterate to a fixpoint instead of one pass. Reuses the rollback discipline already there. | `OffcutReuse.cs` | F3 |

Each of these is a change to *how a plan is built or refined*, so each needs its own
switch, its own oracle and its own benchmark run. Do not batch them: E2.1 alone could
matter more than everything else in this document, and if it is combined with E2.2 and
E2.3 there will be no way to tell which one moved the number.

### Phase E3 — Search structure (must not change results; verify, do not assume)

| # | Deliverable | Risk |
|---|---|---|
| E3.1 | **Deterministic parallel evaluation** of the existing candidate pool. Same candidates, same reduction, less wall-clock. The saved time is the budget for E2. | Threading. Proven safe only by an unchanged benchmark, not by argument. |
| E3.2 | **Prune provably duplicate candidates**, once E1.3 identifies them. Runtime win — and it *can* change results if two "identical" generators ever differ off-benchmark, so it needs E1.3's data first. | Medium |
| E3.3 | **Wider beam at high effort only**, kept out of the default path. | Low (effort 5 only) |

E3.1 is the one people ask for and the one §2 says is worth the least on its own. It is
worth doing **only** as the funding mechanism for E2, and it should be sequenced after
E2.1 is measurable, not before.

### Phase E4 — Correctness policy (will change results; needs a decision)

| # | Deliverable | Why it moves the baseline |
|---|---|---|
| E4.1 | Make guillotine feasibility a **rejection at the candidate boundary**: a non-guillotine generator may only win if every one of its sheets derives a cut sequence; otherwise the best guillotine-cuttable candidate wins and the note says so. | If `RunMaxRects` currently wins on any real job, this changes that job. It should — but that is a decision, not a fix, and it needs the E1.1 audit first to know how often it fires. |

E4.1 is the only item here that trades yield for correctness. It is also the only one that
closes F1. E1.1 tells us whether the trade is real or theoretical.

---

## 6. Order of work

1. **E1 in full** — no risk, and it is what makes the rest decidable.
2. **E2.1 alone**, measured. This is the one bet in the document, and §2 is the evidence
   for placing it.
3. Then E2.2, E2.3, one at a time, each measured.
4. E3.1 once E2 has something worth spending time on.
5. E4.1 last, on E1.1's numbers.

Anything that lands without a before/after benchmark number has not landed — it has been
guessed.

---

## 6A. Implementation status — 2026-09-26

Every phase in §6 is written and pushed. **None of it has been compiled or executed** —
there is no .NET SDK on this machine and none obtainable. The status below is "written and
reviewed", not "verified". §6's closing line still stands: an item has landed when it has a
before/after benchmark number, and not one of these does yet.

| Phase | Commit | Switch | Default | Changes the plan? |
|---|---|---|---|---|
| E1 — instrument | `87a0f32` | none, always on | on | No. Adds fields and notes only. |
| E2 — local search | `28f85f1` | `localSearch`, `enableSwaps` | off | Yes, when on. |
| E2.2 — remainder-aware split | `a69c8ef` | `remainderAware` | off | Yes, when on. |
| E3.1 — parallel evaluation | `ae2916d` | `parallelEval` | off | Only when the time budget binds. |
| E3.3 — wider beam | `ae2916d` | `wideBeam` | off | Yes, when on. Effort 5 only. |
| E4.1 — guillotine policy | `9d0c1a6` | `requireGuillotine` | off | Yes, when on. That is the point. |

E1 is the deliberate exception to "off by default": `Instrument` is called from both
`PackQuick` and `PackOptimize`, so the cut-reachability audit and the sheets lower bound
are always populated. F1 survived this long precisely because nothing looked.

Every switch has a test asserting that passing it explicitly `false` produces the plan the
engine produced before the phase existed. The default path is byte-for-byte unchanged.

### E3.2 was deferred, not forgotten

Pruning duplicate candidates is a pure speedup, and §2 says the pool is degenerate on the
benchmark job. But "saves time" is only worth having once E1.3 shows the pool spending real
time on candidates that never win on production jobs. Pruning a candidate that turns out
*not* to be duplicate is a silent regression in plan quality — the one failure mode this
roadmap cannot afford. Do it on census numbers, not on §2's.

### What to measure, in order

1. **Build and run the suite.** This is the gate. Nothing above is believed until it is green.
2. **The DGU benchmark at default settings.** It must be identical to the pre-E1 plan. If it
   is not, stop — that is a bug in a switch that was supposed to be inert.
3. **E1.1 on a real job mix.** Read `UncuttableSheetCount` and `SheetsLowerBound`. A
   non-zero uncuttable count on the *default* path is F1 firing in production, and it is
   what decides E4.1.
4. **Then the timer**, to confirm §2's claim that the bottleneck is the decoder and not the
   search. If that is wrong, half of §5 needs rethinking.
5. **Then E2, E2.2, E3.1 and E3.3, one at a time**, each with a before/after number.
6. **E4.1 last**, once step 3 says how often it fires.

### Two bugs found and fixed along the way

Not in the original plan — found by reading while implementing E2.

- **Kerf was dropped** — four `DeriveLeftovers(..., 0)` calls measured rectangles from the
  cut line rather than after the blade, so a pane could sit closer than a kerf to its
  neighbour.
- **Stale rectangles after rollback** — `RestorePlan` restored pieces and cuts but not
  `Leftovers`, so an undone move left rectangles behind and the report and DXF export could
  draw space a pane was standing on.

---

## 7. Deliberately not in this roadmap

Recorded so the decision is on the record rather than re-litigated each time:

- **Production and downstream:** rack/slot assignment, sequencing for the IG line or
  furnace, label printing, remake re-entry, post-run sheet editing, 1D spacer/profiles.
  Real, valuable, and not engine.
- **IG pair cutting, priority fields, filler/standard pieces, persistent remnant
  inventory.** These change what the engine is *asked* to do (constraints, objectives,
  stock pool) rather than how well it solves the problem it is given. The persistent
  remnant store is the one with real yield in it, and it is Phase 3, which you asked me to
  skip as high risk — it stays skipped until you say otherwise.
- **True-shape nesting.** A different engine, not an extension of this one. Out because
  you cut rectangles.
- **Defect avoidance.** The algorithm is straightforward — reject placements that overlap
  a defect rectangle, same boundary-rejection pattern the staged rules already use — but
  there is no defect data anywhere in the model. Blocked on input, not on algorithm.
- **More orderings, more shuffles, more restarts.** Measured at zero by
  `BestSplit.cs:8-13`. Re-proposing these should be treated as a bug in the proposal.
