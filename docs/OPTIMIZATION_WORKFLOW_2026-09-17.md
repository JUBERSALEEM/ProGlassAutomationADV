# OPTIMIZATION — HOW A RUN ACTUALLY WORKS

_Reference: 2026-09-17. Every claim below is traceable to the file and line named next to it._

---

## 1. The one-sentence version

Pieces are collected, split into **cutting groups by glass composition** (different glass never
shares a sheet), each group is packed by a deterministic **guillotine** engine that evaluates
several candidate plans and keeps the best, every result is re-checked by an independent
**oracle**, and the finished run is published to a process-wide feed that Step 5, Step 6 and
Step 9 read instead of hand-typed numbers.

---

## 2. Three doors into the same engine

| Door | Where | Notes |
|---|---|---|
| **OPTIMIZATION module** | `Views/Optimization/OptimizationView.xaml` | Full workspace: SIZES · STOCKS · MARGINS · SETTINGS · JOBS · 2D VIEW · CUT LIST · SUMMARY · REPORT |
| **Step 5 top panel** | `ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.Optimization.cs` | QUICK / OPTIMIZE straight from the grid the operator is looking at — no confirmed Job Order needed. Late-bound through a delegate; the grid never references the optimization module. |
| **Production** | `Services/Production/Cutting/CuttingWorkflowService.cs:238`, `Services/Production/ProductionOptimizationBridge.cs:210` | Both call the same `GuillotinePacker.PackQuick` / `PackOptimize`. |

All three converge on **one** engine: `Services/Optimization/GuillotinePacker.cs`.

---

## 3. The pipeline, stage by stage

```
   SIZES rows / Step 5 snapshot / Step 5 cut-list import
   (CutListGridIo → CutListImportParser: csv, txt, xlsx, json)
                    │
                    ▼
   ┌─ TryBuildInputs ─────────────── UI thread: validates, refuses to start bad runs
   │
   ├─ BuildCuttingGroups ─────────── GroupBy GlassRef (trimmed, case-insensitive)
   │                                 "(unlabelled glass)" if the ref is blank
   │
   ├─ SIZES dropdown filter ──────── optional: one composition at a time
   │
   ├─ Margins per group ──────────── MarginProfile.DefaultFor(thickness), or the
   │                                 MARGINS tab value, or a thickness-aware override
   │                                 when the group's thickness differs
   │
   ├─ Effort (1..5) + Rotation ───── RotationEnabled ? Free90 : Locked
   │
   ▼   ══ background thread, one group at a time ══
   PackQuick / PackOptimize ────────► OptimizationResult
                    │                 (sheets, unplaced, scale notes)
                    ▼
   GuillotinePacker.Verify ────────── independent oracle → "Verified" or "N issue(s)"
                    │
                    ▼
   ApplyResults ───────────────────── groups ordered MAX → lowest utilization
                    │                 summary first, ScaleNotes appended
                    ├──► PublishLiveFeed → OptimizationResultFeed  (Step 5/6/9)
                    └──► SaveSessionAsync → JOBS tab, reopen straight to REPORT
```

Key code: `ViewModels/Optimization/OptimizationViewModel.Run.cs:231` (`Run`),
`:363` (`RunCoreAsync`), `:287` (`BuildCuttingGroups`), `:501` (`ApplyResults`),
`:625` (`PublishLiveFeed`). Strategy candidates live in `GuillotinePacker.Advanced.cs`
(`RunSheetFill:69`, `RunWallSeed:137`, `RunMaxRects:206`) and
`GuillotinePacker.BestSplit.cs` (`RunBestSplit:119`, `RunBestSplitBeam:214`) and the smart
placement policy in `GuillotinePacker.SmartPlacement.cs` (`PackSmart:94`, `TrySmartPlace:278`,
`AuditBreakout:401`).

### 3.1 What one candidate plan does — `RunPass`

For every piece, in the chosen order:

1. `PlaceOnOpenSheets` — try the sheets already open;
2. else `OpenNewSheet` — take the next size from the stock pool;
3. else the piece lands in `result.Unplaced` (reported, never silently dropped).

Cancellation is tested **per part**, so Cancel takes effect within one part rather than one
whole pass. Then `ConsolidateAll` (donor sweeps — moves pieces between sheets to free one),
`SortSheetsFullestFirst`, and `SheetNo` renumbering.

### 3.2 Progress you can actually read

`Progress<OptimizeProgress>` is constructed on the UI thread, so the engine reports from the
worker and the label updates on the dispatcher. Overall completion is
`(finished groups + this group's pass fraction) / group count` — a 12-group job moves smoothly
instead of jumping once per group. QUICK emits no per-pass progress, so its bar is driven by
group position instead of sitting at zero.

---

## 4. QUICK vs OPTIMIZE — what each level buys

`GuillotinePacker.cs:106` (`PackQuick`) and `:136` (`PackOptimize`).

**QUICK** = one deterministic pass: **Area** ordering, `HorizontalFirst` split.
Area was chosen over MaxSide deliberately — MaxSide cost a whole sheet on the owner's own
benchmark (Low-E 18 @ 78.18% vs Area 17 @ 82.77%).

**OPTIMIZE** = multi-start. Six orderings (`MaxSide, Area, MinSide, Height, Width, Perimeter`)
× three split modes (`HorizontalFirst, VerticalFirst, LongerLeftover`), plus extra strategy
candidates. Every candidate is a complete packing; `Better()` keeps the fewest sheets, then the
highest utilization. Fully deterministic — shuffles are seeded, never random.

| Level | Ordering × split passes | + SheetFill / WallSeed | + BestSplit | + Smart | + MaxRects | + Beam | Total candidates |
|---|---|---|---|---|---|---|---|
| 1 | 1 (mirrors QUICK exactly) | – | – | – | – | – | **1** |
| 2 | 3 | 6 | 1 | 1 | – | – | **11** |
| 3 | 12 | 6 | 1 | 1 | 3 | 1 (width 4) | **24** |
| 4 | 72 (6×3×4 shuffles) | 6 | 1 | 2 | 3 | 2 (4, 8) | **92** |
| 5 | 126 (6×3×7 shuffles) | 6 | 1 | 2 | 6 | 2 (4, 8) | **149** |

*Smart = the capacity-preserving, breakout-aware policy in
`GuillotinePacker.SmartPlacement.cs` (§4.1). Skipped entirely in the Huge scale
band, where termination matters more than the last sheet.*

### 4.1 Smart placement — the policy added 2026-09-17

Every other strategy in that table asks the same question at every placement:
*which free rectangle does this piece go in?* The legacy score answers it locally —
exact-edge fit, then best-short-side-fit, then leftover area, all measured **inside the
rectangle being consumed**. `PackSmart` answers it by what *survives* the placement:

1. least stock area condemned to sub-breakout trim;
2. then the **largest free rectangle that survives**, by area;
3. then that rectangle's **shortest side** — a long thin survivor is worth much less than a
   squarer one of the same area;
4. then the legacy geometry as a tie-break, so equal-capacity choices behave like the engine
   the benchmark was calibrated on;
5. then orientation, rectangle index, position and input order — a total order, so the run
   stays deterministic.

It also stops forcing two things that were forced before:

* **Split direction.** Legacy candidates fix one `SplitMode` for the whole run. Smart evaluates
  *both* directions at every placement, and the caller's `SplitMode` is only a tie-break
  preference. Whether the remainder belongs to the right or below is a local decision, and now
  it is made locally.
* **Minimum breakout.** `MarginProfile.MinBreakoutMm` is now honoured: a remainder narrower
  than it in either dimension becomes trim waste instead of being offered as stock the saw
  cannot cut out.

`GuillotinePacker.AuditBreakout(result, margins)` reports thin offcuts in any finished plan. It
is deliberately **not** part of `Verify` — Verify is the oracle that runs on every group of
every run, and a thin offcut is not an illegal layout, so folding it in would have turned every
legacy plan into "N issue(s)" overnight. The winning strategy is named on the result
(`OptimizationResult.Strategy`) and reported in the run message, so a better sheet count is
explainable rather than lucky.

Two facts worth knowing operationally:

* **Level 1 is guaranteed never to be worse than QUICK** — it mirrors it exactly, and
  `Benchmark_OptimizeStage_NeverRegressesQuick` pins that. `Better()` only ever takes the
  minimum, so adding candidates cannot regress a result.
* **`RunBestSplit` is the candidate that takes the Clear group from 17 sheets to the owner's
  reference 16.** It only runs at effort ≥ 2 — so hitting the Clear benchmark needs OPTIMIZE,
  not QUICK.
* Beam search is disabled above the standard size band: it clones the whole open-sheet list per
  candidate, O(sheets² × width), and is the main memory multiplier. It only ever *adds*
  candidates, so skipping it can raise the sheet count but never produces an invalid layout.

---

## 5. Margins — the floor settings

`OptimizationModels.cs:49`. Per thickness, editable in the MARGINS tab:

| | ≤ 6mm | ≤ 8mm | ≥ 10mm |
|---|---|---|---|
| Left / Right / Top / Bottom trim | 15mm | 20mm | 25mm |
| Kerf (blade gap) | 3mm | 3mm | 4mm |
| Min breakout (narrowest offcut worth keeping) | 15mm | 15mm | 15mm |

Margins are edge trims cut off the raw stock **before** placement and kerf is the gap
**between** cuts. When a cutting group's thickness differs from the MARGINS tab, the group is
cut with its own thickness-correct profile and the run carries a `MarginNote` saying so.

**Minimum breakout was, until 2026-09-17, a setting nothing obeyed.** It was editable,
persisted and drawn in the 2D preview, but `Commit` kept every remainder above zero as usable
stock. Smart placement honours it; the legacy candidates still do not, because every benchmark
pin is measured through their behaviour and they are deliberately left bit-identical.
`AuditBreakout` is the diagnostic that tells the truth about any plan.

---

## 6. Scale protection — a big job narrows the search instead of hanging

`ScaleProfile.For(pieceCount, effort)` in `OptimizationModels.cs:176`. Cost grows roughly with
parts × sheets, so 100,000 parts is not 1,000× a group of 100 — it is about a million times.

| Band | Parts | Shuffles | Beam | MaxRects orderings | Consolidation sweeps | Sheet-fill / wall-seed / best-split |
|---|---|---|---|---|---|---|
| **Standard** | ≤ 2,000 | unlimited | on | unlimited | unlimited | all 3 split rules |
| **Large** | ≤ 20,000 | 1 | off | 3 | 2 | all 3 split rules |
| **Huge** | > 20,000 | 0 | off | 1 | 1 | **dropped** (`AllowQuadraticStrategies`) |

The last column was added after a 30,000-part job took the application down. Those three
strategies are *sheet-at-a-time*: every placement re-scans the whole remaining part list, and
`RunBestSplit` does it three times per sheet — one trial per split rule. Their cost is quadratic
in the job, so at 30,000 parts across ~1,000 sheets they amount to billions of placement
attempts. `ScaleProfile.Summary` had already been telling the operator "only the minimum search
was run" while all six still executed; now the code agrees with the message.

Everything the calibration benchmark covers is in the **Standard** band, so those results stay
bit-identical. Ordering and split-rule diversity — which produces most of the quality — is kept
in every band; only *breadth* is traded for termination.

**Every stage is cancellable.** `RunPass`, `RunSheetFill`, `RunWallSeed`, `RunMaxRects`,
`FillOneSheet`, `RunBestSplit`, `RunBestSplitBeam`, `PackSmart` and `ConsolidateAll` all accept a
`CancellationToken` and check it inside their hot loops — per part, per placement, per sheet or
per donor, whichever is the expensive unit. `Advanced.cs` and `BestSplit.cs` previously contained
**zero** cancellation checks, which is why Cancel did nothing during the phase where a large run
spends all of its time.

**No reduction is ever silent.** `scale.Summary` is added to `result.ScaleNotes` and shown next
to the sheet count, in plain language, telling the operator exactly what was skipped and why.
`ApplyResults` **appends** these notes rather than replacing the summary — overwriting would hide
the result precisely on the large jobs where the note matters most.

---

## 7. After the run — where the numbers go

### 7.1 The feed (`Services/Optimization/OptimizationResultFeed.cs`)

A read-only bridge from a finished run to the quoting steps. It exists because Step 6 asks the
operator to type a SHEET QTY and an optimization %, and Step 9 drives its Sheet Details tab from
exactly those two numbers — while the optimization module already computes both, per rack, from
the real cut. Hand-copying them is where a quote silently drifts from the plan.

* **Joined by `(SpecNumber, Side)`**, both taken from the same Step 5 SpecGroup. This is
  structural. Matching on *colour* would be fragile: Step 6 deliberately allows a pane colour to
  stay blank, so a colour join would drop rows without saying so.
* **Shared-rack caveat:** the engine groups by *composition*, not by spec. When two specs use the
  same glass at the same sheet size they are cut from one pool, and afterwards there is no record
  of which sheet belonged to which spec. **Utilization is still exact** (a property of the rack);
  **sheet quantity is the shared total**. The feed reports it once, flags it with
  `IsSharedAcrossSpecs`, and leaves the split to `Step6SheetAllocation`'s existing weighted
  allocation. Inventing a per-spec split in the feed would double the buy.
* **Layering:** services must not reach into ViewModels, so the feed owns its own read model and
  parses persisted `ResultJson` defensively — three writers exist with different shapes, and a
  foreign shape yields an empty feed rather than a crash.
* **State:** `Current` is a cache over a persisted store, refreshed explicitly, always carrying a
  provenance label and a timestamp.

### 7.2 Consumers

| Consumer | What it reads |
|---|---|
| Step 5 grid | QTY USED + OPT % per row, in the CUT LIST workspace |
| Step 6 `WastagesSectionViewModel:148` | sheet qty + utilization instead of hand-typed values |
| Step 9 `SheetProfitabilityViewModel:732` | Sheet Details tab, driven by those same two numbers |
| Production cutting | the cut plan itself |

### 7.3 Persistence

`OptimizationSessionRepository` — `UpsertLatestRunAsync(snapshot, DocumentKey)` keeps one latest
run per document, `InsertAsync` saves a named session, `GetLatestRunAsync` / `ListAsync` /
`DeleteAsync` back the JOBS tab. `_openToReportAfterRun` reopens the module straight on REPORT
after a run — and is cleared on **every** early exit, so a run that never starts cannot leave the
flag dangling.

---

## 8. Failure modes — and what the operator sees

| Case | Message |
|---|---|
| Cancelled | "Optimization cancelled. **No results were applied.**" — honoured inside every strategy, not just between them |
| Out of memory | "Stopped: this machine ran out of memory packing a group this large. Split the job into smaller cutting groups or close other applications, then run it again." |
| Other exception | "Optimization failed: {message}" |
| Selected group has no valid pieces | "The selected cutting group has no valid pieces." |
| Search narrowed by size | `ScaleProfile.Summary`, appended to the result |
| Oracle found overlaps/overruns | "N issue(s)" in the group's Oracle column |

`finally` always clears `IsRunning`, stops the run clock, disposes the CTS and refreshes
`CanCancel` / `CanUpgradeToOptimize`.

---

## 9. The benchmark the engine is calibrated against

`Tests/Agent/OptimizationBenchmarkTests.cs` — the owner's **real DGU job data**
(Spec 1: climaguard low-e + clear panes; Spec 2: obscure clear ramly panes), floor settings
6mm → 15mm trims, 15mm breakout, 3mm kerf, rotation enabled.

| Group | Stock | Reference (owner's tool) |
|---|---|---|
| Low-E | 3210 × 2250 | **17 sheets @ 82.77%** |
| Clear | 3210 × 2250 | **16 sheets @ ~85%** (needs OPTIMIZE — see §4) |
| Ramly | 2500 × 1875 | **1 sheet** |

Pinned by tests, not by memory: `Benchmark_LowE_MatchesReference`,
`Benchmark_OptimizeStage_NeverRegressesQuick`, plus determinism (same input → same plan).
`OptimizationSmartPlacementTests` pins the new policy: the extracted split maths checked
rectangle-by-rectangle against the legacy rule, oracle-clean output, determinism, stock caps,
locked rotation, oversized pieces, and the monotone guarantee that adding a candidate never
costs a sheet. Related suites: `OptimizationScaleTests`, `OptimizationFeedTests`, `OptimizationCutListTabTests`,
`OptimizationCutListWorkspaceTests`, `Step5OptimizationRunTests`,
`WastageOptimizationFormulaTests`.

---

## 10. Operator walkthrough

1. **SIZES** — enter the parts, or bring them in from Step 5 (its cut-list import handles csv,
   txt, xlsx and json through `CutListGridIo` → `CutListImportParser`). Or press QUICK/OPTIMIZE
   on the Step 5 grid and skip straight to step 5 below.
2. **STOCKS** — confirm the sheet sizes in the pool; the engine takes them in order and opens a
   new sheet only when no open sheet can take the piece.
3. **MARGINS** — check trims / kerf / breakout for the thicknesses in the job.
4. **SETTINGS** — effort level (1–5), rotation on/off.
5. **QUICK** for an instant plan, **OPTIMIZE** for the best one. Watch the overlay: it names the
   group, the pass number and the ordering/split in use, and Cancel lands within one part.
6. **2D VIEW** — the sheets, drawn. **CUT LIST** — per-piece read-back with QTY USED and OPT %;
   it pastes like every other grid here (`IExcelPasteable` + `DataGridNavigationBehavior`).
7. **SUMMARY** — sheets, utilization, waste m², unplaced, and the oracle verdict per group,
   ordered MAX → lowest utilization.
8. **REPORT** — the industrial export. **JOBS** — saved sessions, reopen any of them.
9. Go back to the quote: Step 6 and Step 9 now read sheet qty and utilization from the run
   instead of waiting for someone to type them.

---

## 11. File map

```
Services/Optimization/
  GuillotinePacker.cs              PackQuick, PackOptimize, RunPass, Verify (oracle)
  GuillotinePacker.Advanced.cs     RunSheetFill, RunWallSeed, RunMaxRects, beam
  GuillotinePacker.BestSplit.cs    per-sheet split selection — Clear 17 → 16
  GuillotinePacker.SmartPlacement.cs
                                   PackSmart + AuditBreakout: capacity-preserving
                                   placement, per-placement split choice, breakout honoured
  OptimizationModels.cs            CutPiece, StockSize, SheetLayout, MarginProfile,
                                   RotationPolicy, SplitMode, ScaleProfile, OptimizeProgress,
                                   OptimizationResult
  OptimizationResultFeed.cs        read-only bridge to Steps 5 / 6 / 9
  OptimizationSessionRepository.cs saved runs, latest-run-per-document

ViewModels/Optimization/
  ...Run.cs        Run / RunCoreAsync / BuildCuttingGroups / ApplyResults
  ...LiveRun.cs    run clock, upgrade-to-OPTIMIZE, stamping
  ...Feed.cs       PublishLiveFeed
  ...Session.cs    save / load / list / delete sessions
  ...Parts.cs      SIZES grid rows
  ...Stocks.cs     stock pool
  ...CutList.cs    cut list read-back
  ...Report.cs     export
  ...Costing.cs    cost view
  ...Jobs.cs       jobs tab

Views/Optimization/Tabs/
  SizesTabView · StocksTabView · SettingsTabView · JobsTabView ·
  TwoDViewTabView · CutListTabView · SummaryTabView · ReportTabView
```
