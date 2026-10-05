# Optimization Engine — Remediation Plan

**Date:** 2026-09-20
**Baseline:** `d3fd69ee` (v2.0.3)
**Source audit:** 23 findings, all code-proven with file:line evidence
**Status key:** ⬜ not started · 🔄 in progress · ✅ implemented (awaiting runtime proof)

Nothing in this plan is "FIXED" until it is proven on a real build. Every phase
lists the evidence, the change, and the runtime proof required.

---

## House margin table (owner-specified, 2026-09-20)

| Glass thickness | LM | RM | TM | BM | Min breakout | Kerf |
|---|---|---|---|---|---|---|
| up to 6mm | 15 | 15 | 15 | 15 | 15 | 3 |
| 7–8mm | 30 | 30 | 30 | 30 | 30 | 3 |
| 9mm and above (10mm, 12mm, …) | 50 | 50 | 50 | **0** | **50** (switchable to 40) | 4 |

Bucketing rule for thicknesses the owner did not name: **round up to the next
known bucket**. 7mm takes the 8mm profile; 9mm and 11mm take the 10/12mm
profile; anything above 12mm stays on the thick profile. Rationale — thicker
glass needs more trim, and rounding up is the conservative direction. An
unparseable or zero thickness falls back to the 6mm profile, as it does today.

The 10/12mm profile is deliberately asymmetric (BM = 0): the bottom edge is
cut on the float line, so no trim is taken there.

---

## PHASE 1 — Margin authority ⬜→🔄

**Findings:** 3 (silent margin substitution), 4 (label-scraped thickness)

| # | Evidence | Change |
|---|---|---|
| 1a | `OptimizationModels.cs:64-73` — old table (15/20/25, breakout always 15) | Replace with the owner's table above; add `BucketFor`, `HasBreakoutChoice`, `BreakoutPrimaryMm=50`, `BreakoutAlternateMm=40` |
| 1b | `Run.cs:393-394` — `if (LeftMm != 0 \|\| TopMm != 0)` infers "customized" from 2 of 4 fields | Add explicit `MarginProfile.IsCustomized`, computed in `TryBuildInputs` by comparing against house defaults. Left=0/Top=0 is then honoured, not overwritten |
| 1c | `Jobs.cs:728-734` — regex `^\s*(\d+(?:\.\d+)?)\s*mm` is start-anchored and integer-parsed | Match thickness anywhere in the label; round decimals (`6.5mm` → 6, not 0); accept `6 mm`, `6MM`, `Clear 6mm` |
| 1d | `Margins.cs:31` — `ThicknessText` is a passive string | Thickness change auto-selects that bucket's house defaults **when the current values are untouched**; when the operator has customized, it surfaces a hint instead of overwriting (avoids re-creating the RC-016 destructive-refresh class of bug) |
| 1e | `SettingsTabView.xaml:56-59` — breakout is a free-text box only | Add a 50 / 40 breakout selector, enabled only for the thick bucket |

**Runtime proof:** set thickness 6 → margins read 15/15/15/15 + breakout 15.
Set 8 → 30/30/30/30 + 30. Set 10 → 50/50/50/**0** + 50, and the 40 option
becomes available. Set Left=0, Top=0, Right=15, Bottom=15 by hand, run, and
confirm the result is cut with **your** zeros (no silent substitution).

---

## PHASE 2 — Input parsing: no permissive defaults ⬜

**Findings:** 1 (blank Available = infinite stock), 6 (comma → 10× price), 19 (one bad row aborts the run)

| # | Evidence | Change |
|---|---|---|
| 2a | `Margins.cs:206-208` + `GuillotinePacker.cs:1000`, `Advanced.cs:38`, `Smart.cs:112` — `MaxSheets == 0` means unlimited | Distinguish **blank** (→ unlimited, shown as `∞` on the STOCKS row) from **unparseable non-empty text** (→ refuse the run with a named row). Parse with `NumberStyles.Integer \| AllowThousands` so `1,000` reads as 1000. Negative input is refused, not clamped to unlimited |
| 2b | `TextParsing.cs:25-29` — `CleanNum` strips every comma | Cut-list price parsing must not use comma-stripping. Parse with the operator's current culture as well as invariant; refuse and say so rather than saving `0m` or `455` |
| 2c | `Margins.cs:147-158` — first invalid row returns false | Skip invalid rows, keep a list, and report **all** of them plus the skipped quantity. The run proceeds on the valid rows; the accounting states what was left out so nothing disappears silently |

**Runtime proof:** leave Available blank → rack shows `∞` and is used. Type
`1,000` → 1000 sheets. Type `abc` → the run refuses and names the rack. Type
`-5` → refused, not unlimited. One bad piece row among 50 → run proceeds,
status names the bad row and the skipped qty.

---

## PHASE 3 — Document identity: one namespace ⬜

**Finding:** 2 (`JOB-…` vs `PI-…` keys never match)

| # | Evidence | Change |
|---|---|---|
| 3a | `Run.cs:293-297` stamps `DocumentKey = selectedJoIds[0]`; `Feed.cs:88-96` documents that module runs should be unstamped; `IsForDocument` (`Feed.cs:269`) is a plain string compare; `DeriveJobOrderId` (`JobOrderMapper.cs:119-128`) is a pure `PI-`→`JOB-` prefix swap | Introduce one canonical document-identity normalizer used by the feed guard, the session key and the stamp, so a run keyed by `JOB-2026-004` is recognised as belonging to `PI-2026-004`. Keep the JO id in the result *label* (the operator still needs to see which job ran) but stop using it as the *matching* key |
| 3b | Session rows written under both key spellings fragment persistence | Write the reserved latest-run row under the canonical key only; read accepts either spelling so existing rows are not orphaned |

**Runtime proof:** tick ONE released job in the module, run OPTIMIZE, open the
source quote's Step 6 → the wastage/sheet figures arrive. Then Step 5 → VIEW
RESULT finds the same run.

---

## PHASE 4 — Verification: make the oracles real ⬜

**Findings:** 7 (QUICK unvalidated but labelled "Verified"), 8 (`VerifyComprehensive` has zero callers), 9 (failure never blocks), 10 (`Verify` null-deref)

| # | Evidence | Change |
|---|---|---|
| 4a | `PackQuick` (`GuillotinePacker.cs:174-203`) returns `RunPass` with no `ValidateResult` | Validate conservation on the QUICK path too, with the same fallback-and-note pattern `PackOptimize` already uses (`:415-435`) |
| 4b | `VerifyComprehensive` (`:1247`) — zero callers repo-wide | Wire it as the post-run oracle for the group result; it is the only check covering geometry **and** quantity/material conservation |
| 4c | `Run.cs:566` — `ApplyResults` runs unconditionally regardless of `ProblemCount` | A group that fails the oracle is applied to screen (so the operator can inspect it) but is **excluded from export and from the Step 6 feed**, with a red banner naming the violation. A plan that fails geometry must never become a cut file |
| 4d | `Verify` (`:1207-1211`) dereferences `p.Piece` unguarded while `ValidateResult` (`:906`) guards it | Guard it. A null back-reference must report a problem, not throw away the whole run |
| 4e | `Run.cs:559` — `OracleLabel = "Verified"` comes from geometry-only `Verify` | Label must say what was actually checked. "Verified" only when conservation passed too |

**Runtime proof:** a QUICK run on a job with an unplaceable piece shows a
conservation warning, not "Verified". Export is refused while the banner is up.

---

## PHASE 5 — Stock routing honesty ⬜

**Findings:** 5 (cross-composition racks), 18 (dead `SortStocksPrimaryFirst` intent)

| # | Evidence | Change |
|---|---|---|
| 5a | `Run.cs:441-445` — a group with no matching rack is packed on **every other spec's rack** | Refuse by default and name the glass that has no rack. Keep a single explicit opt-in ("pack on any rack — I know the glass differs") so the operator can still get a ballpark, clearly marked on the result |
| 5b | `Run.cs:448-449` sorts descending; `GuillotinePacker.cs:999-1001` re-sorts ascending, overriding it | Remove the contradictory pre-sort or make it express something the packer honours |

**Runtime proof:** a group whose glass has no rack stops with a named reason
instead of producing a sheet count for the wrong material.

---

## PHASE 6 — Memory and object lifetime ⬜

**Findings:** 11 (no `IDisposable`), 12 (headless host roots the invoice tree), 13 (post-run retention), 14 (unbounded DB growth)

| # | Evidence | Change |
|---|---|---|
| 6a | `OptimizationViewModel` has no `Dispose` across 14 partials; owns `_runClock` (`Progress.cs:149,165`), `_runCts`, `_bridgeCts` | Implement `IDisposable`: stop and detach the timer, dispose both CTS, detach job/layer `PropertyChanged` handlers |
| 6b | `ProformaInvoiceViewModel.Dispose` (`:588-626`) never touches `_optimizationHost`, which holds `host.PropertyChanged += OnOptimizationHostPropertyChanged` (`:507`) — a strong root to all nine step VMs | Unsubscribe and dispose the host in `Dispose`; skip the host's job/session load entirely when `IsHeadlessHost` (it never reads them) |
| 6c | `_groupRuns` / `_flatSheets` (`Run.cs:187-188`) retain every `PlacedPiece.Piece` back-reference until the next run | Release the input back-references once the result has been applied and reported, keeping only what the 2D view and the report read |
| 6d | One reserved row per `DocumentKey` forever, each with full JSON (`OptimizationEntities.cs:75-78`); `ListAsync` shows only the newest 100 (`OptimizationSessionRepository.cs:52-58`) | Add retention pruning for reserved latest-run rows older than a configurable window, run on startup, with the count logged |

**Runtime proof:** open a quote, run from Step 5, close the quote — the host and
its timer are gone (no growth across repeated open/run/close cycles). DB row
count stabilises.

---

## PHASE 7 — Save pipeline integrity ⬜

**Findings:** 15 (non-atomic upsert), 16 (Debug-level silence), 17 (destructive Send), 18 (zero price saved)

| # | Evidence | Change |
|---|---|---|
| 7a | `OptimizationSessionRepository.cs:88-104` — read-then-write, no transaction | Wrap in a transaction with a retry on PK/locking failure, so two concurrent runs cannot lose one |
| 7b | `Session.cs:243-247` — every failure logged at `Debug` | Escalate a *repeated* failure to `Warning` and surface a one-time status line, so a permanently failing save becomes visible. Keep a single transient failure quiet (the run must not be reported as failed) |
| 7c | `EnsureCreatedAsync` on every call; it never migrates | Detect schema drift and log it loudly rather than failing silently forever |
| 7d | `CutList.cs:301` — `Pieces.Clear()` with no confirmation or undo | Confirm when the cutting list is non-empty and was populated from a job selection; offer merge instead of replace |
| 7e | `CutList.cs:543-546` — unparseable price saved as `0m` | Refuse to save a zero/unparseable price into the catalogue; say why |

**Runtime proof:** kill the app mid-run repeatedly — no orphan rows, no lost
saves. SEND TO ENGINE on a job-imported list asks first.

---

## PHASE 8 — Scale, responsiveness and constraints ⬜

**Findings:** 20 (no qty ceiling, no time budget), 21 (Smart.cs uncancel­lable), 22 (governor ignores memory), 23 (no per-piece rotation lock)

| # | Evidence | Change |
|---|---|---|
| 8a | `Margins.cs:159-176` — one `CutPiece` per unit, no cap | Pre-flight the expanded total; above a threshold warn and require confirmation, and stop allocating labels per unit |
| 8b | `OptimizationSearchGovernor.cs:82-145` — breadth only; Tier 1 leaves `MaxConsolidationSweeps`, `OpenSheetWindow`, `MaxDonorAttempts` at `int.MaxValue` | Add a wall-clock budget per group with best-so-far return, so a long run yields a usable plan instead of running until cancelled |
| 8c | `GuillotinePacker.Smart.cs` — 0 cancellation checks (vs 35 elsewhere) | Add checks in its outer loops so CANCEL takes effect |
| 8d | Governor reads only `Environment.ProcessorCount` (`:84`) despite claiming hardware-aware scaling | Read available memory and drop a tier when the expanded workload will not fit |
| 8e | `Margins.cs:172` — `AllowRotate = RotationEnabled` globally | Per-piece rotation lock, so patterned/directional glass can be constrained while the rest rotates |

**Runtime proof:** a 1M-part paste warns before allocating. A long run returns
best-so-far at the budget. CANCEL responds during a Smart-strategy pass.

---

## Systemic pattern behind Phases 1–2

Findings 1, 3, 4 and 6 are one bug wearing four faces: **the engine infers a
physical or monetary parameter from free text, and every parse failure resolves
to a permissive default** — `0` → unlimited stock, `0` → 6mm margins,
unparseable → `0m` price. Individually each looks defensible; together they mean
bad input produces confident, wrong output instead of an error.

The rule adopted for this remediation: **a parse failure is an error, never a
default.** Only an explicitly blank field may take a documented default, and
that default must be visible on screen (`∞`, "house 6mm", "not priced").

---

## Order of execution

Phases 1 → 2 → 4 → 3 → 5 → 7 → 6 → 8.

Phase 1 first because the owner specified it and because margin/thickness
authority is an input to every plan. Phase 2 next because permissive parsing
silently corrupts everything downstream. Phase 4 before Phase 3 because a
verified plan is the precondition for trusting any bridge. Phase 6 late because
lifetime work is the riskiest to land and benefits from the earlier phases being
stable.

---

# Implementation status — v2.0.4

Everything below is **implemented and syntactically verified, NOT yet runtime
verified**. There is no dotnet SDK in this environment, so the proof has to come
from your build. The Python trace harness still reports 31 PASS / 0 FAIL.

## Landed

| # | Finding | What changed |
|---|---|---|
| 1 | Blank Available = infinite stock | `Margins.cs` — blank/`∞` → `StockSize.UnlimitedSheets` (explicit); `1,000` now parses; negative refused; **`0` means zero** and the rack is left out with a named note. New `TryParseWholeMm` / `TryParseSheetCount` / `IsUnlimitedToken` / `RackLabel` |
| 2 | `JOB-` vs `PI-` keys never match | New `Services/Optimization/OptimizationDocumentKey.cs`. `IsForDocument` compares canonically; the repository canonicalizes the storage key, so one row per document instead of two |
| 3 | Silent margin substitution | `MarginProfile.IsCustomized` set in `TryBuildInputs` by comparing against the house table; `ResolveGroupMargins` honours a deliberate zero and now names the substituted values in its note |
| 4 | Label-scraped thickness | `ParseThicknessMm` matches anywhere in the label and rounds decimals (`6.5mm` → 7, `Clear 12mm` → 12). Compiled regex |
| 5 | Cross-composition racks | `ResolveGroupStocks` refuses by default and names the reason; the group reports `NOT CUT — no rack for this glass` with every piece accounted for as unplaced. Opt-in `AllowCrossCompositionStock` + SETTINGS checkbox, labelled "ballpark only — NOT cuttable" |
| 6 | Comma → 10× price | New `TryParseMoney` parses by SHAPE (four unambiguous forms) and refuses anything ambiguous. `SaveCutListSpecAsync` will no longer store `0m` or `455` for `45,5` |
| 7 | QUICK unvalidated but "Verified" | `PackQuick` now runs `ValidateResult` and records the verdict |
| 9 | Failure never blocks | `ValidationFailed` on `GroupRun`; failed groups are withheld from the Step 6 feed (`Feed.cs`) with the withheld compositions named in the label and logged at Warning |
| 10 | `Verify` null-deref | `Label(PlacedPiece)` made null-safe — fixes every call site at once — plus an explicit guard on the dimension-integrity check |
| 11 | No `IDisposable` | New `OptimizationViewModel.Disposal.cs`: stops/detaches/drops the timer, cancels and disposes both CTS, detaches every job and layer handler, releases retained plan data |
| 12 | Host roots the invoice tree | `ProformaInvoiceViewModel.Dispose` unsubscribes and disposes `_optimizationHost`. `LoadJobsInternalAsync` returns immediately for a headless host, so no second copy of the job graph is built — which also makes the "a Step 5 host's DocumentKey is never overwritten" contract true by construction instead of by timing |
| 15 | Non-atomic upsert | Transaction + bounded retry (3 attempts, backoff) on `DbUpdateException` |
| 16 | Debug-level silence | First failure stays at Debug; a **streak** escalates to Warning and sets `SessionStatus` once. A success clears the streak |
| 18 | Zero price saved | Covered by 6 — the save is refused, not silently zeroed |
| 19 | One bad row aborts the run | Invalid rows are skipped, listed (up to 8 inline, all in the log) and **stated to be outside the plan**. Input warnings survive the `RunMessage` clear via a dedicated channel |
| 20 | No qty ceiling | Hard ceiling 2,000,000 expanded pieces (2× the governor's documented envelope) — refused with the largest offending row named; warning from 250,000 |
| 24 | **New** — warning written to a discarded plan | `PackOptimize`'s "replaced with validated quick pass" note was added to `final.ScaleNotes` and then `final = quick` threw that object away, so the operator never learned their optimized result had failed validation. Note now travels with the returned plan |

## House margin table — implemented as specified

`MarginProfile.DefaultFor` + `BucketFor` + `HasBreakoutChoice`, pinned by
`Tests/Agent/OptimizationMarginHouseTableTests.cs` (13 tests):

| Thickness | LM | RM | TM | BM | Breakout | Kerf |
|---|---|---|---|---|---|---|
| ≤ 6mm | 15 | 15 | 15 | 15 | 15 | 3 |
| 7–8mm | 30 | 30 | 30 | 30 | 30 | 3 |
| ≥ 9mm (10mm, 12mm, …) | 50 | 50 | 50 | **0** | **50**, switchable to **40** | 4 |

Auto-select on the thickness trigger is implemented with one deliberate
restraint, and it is a decision worth confirming: **it fires only while the
fields are untouched.** If you have edited a margin, changing thickness does NOT
overwrite your edit — the hint line names the new house numbers and offers LOAD
HOUSE DEFAULTS. Overwriting an operator's work on a routine event is the exact
RC-016 bug class. Say the word if you want it unconditional.

The 50/40 switch appears only on thick glass; asking for 40 on 6mm or 8mm is
refused rather than silently applied, because there the breakout equals the
margin.

## Not done — carried forward

| # | Finding | Why it is still open |
|---|---|---|
| 8 | `VerifyComprehensive` unwired | Conservation coverage now comes from `ValidateResult` on **every** path including QUICK, so the gap that mattered is closed. `VerifyComprehensive` additionally checks min-breakout and material conservation; the right home for it is the **export gate**, not every run (it is a third O(pieces²) pass). Scheduled, not done |
| 13 | Post-run retention | Released on dispose and on every replace. Cannot be released while the result is on screen — the 2D preview and the report read `PlacedPiece.Piece`. The remaining lever is lazy label formatting (finding 20's per-unit string), which is a bigger refactor |
| 14 | Unbounded DB growth | Needs a retention decision from you: how long should a quote's last run survive? Pruning somebody's run is destructive, so it should not be chosen for you |
| 17 | Send-to-engine wipes the list | Needs a dialog + a merge option. Deliberately not slipped in beside engine work |
| 5b | Contradictory stock sort | `SortStocksPrimaryFirst` (descending) is still overridden by `OpenNewSheet`'s `byPreference` (ascending). Harmless but dead intent — remove or honour |
| 7c | `EnsureCreatedAsync` never migrates | Needs a real migration story for `optimization.db`, not a patch |
| 7d | Schema drift detection | Same |
| 8b | No wall-clock budget | Effort still controls breadth only. Adding a deadline with best-so-far return touches every strategy entry point — the largest single change in this plan |
| 8d | Governor ignores memory | Pairs with 8b |
| 8e | No per-piece rotation lock | Needs model + UI + packer support for a per-piece flag |
| 21 | `Smart.cs` uncancellable | **Moot.** The file header documents it as UNREACHABLE — nothing calls `ImproveByRepack`, its only entry point — and it was measured to beat the shipped candidate pool 0 times in 200 runs. Adding cancellation checks to dead code would be theatre. Its own recommendation stands: delete once BestSplit has soaked in production |

## Runtime proof required on 2.0.4

1. SETTINGS: thickness 6 → 15/15/15/15, breakout 15. Thickness 8 → 30/30/30/30, breakout 30. Thickness 10 → 50/50/50/**0**, breakout 50, and the 50/40 switch appears. Type 12 → same. Type 9 → same.
2. SETTINGS: set Left 0, Top 0, Right 15, Bottom 15 → run → the result uses **your zeros** (the note must NOT say house margins were substituted).
3. SETTINGS: edit a margin, then change thickness → your value **survives** and the hint names the house numbers.
4. STOCKS: leave Available blank → rack is used, no limit. Type `1,000` → treated as 1000. Type `abc` → the run refuses and names the rack. Type `0` → the rack is left out and you are told.
5. Cut list: save a spec priced `45,5` → refused or read as 45.5, **never 455**.
6. JOBS: tick ONE released job → OPTIMIZE → open that quote's Step 6 → the figures **arrive** (this is finding 2, and it was completely broken before).
7. Step 5 → QUICK → if any piece was lost the group must NOT read "Verified".
8. A group whose glass has no rack → `NOT CUT — no rack for this glass`, every piece listed as unplaced.
9. Import 500 rows with one bad row → the run completes and names the excluded row plus its quantity.
10. Version reads **2.0.4** in About.

---

# PHASE 8 — the deferred findings, now closed

Everything the table above listed as deferred is implemented. Each entry records
what was changed and, where the recommendation was deliberately *not* followed
literally, why.

| # | Finding | Closed by |
|---|---------|-----------|
| 5b | Contradictory stock sort | `Run.cs`: `SortStocksPrimaryFirst` renamed `SortStocksLargestFirstForTieBreak`. The descending sort was never the sheet-opening order — `OpenNewSheet` re-sorts ascending by area — so it only ever decided ties. The name now says that instead of implying a policy the engine does not have |
| 7c / 7d | `EnsureCreatedAsync` never migrates; no drift detection | `OptimizationSessionRepository.DetectSchemaDriftAsync()` reads `PRAGMA table_info(Sessions)` and returns the columns missing against `ExpectedColumns[]`. Creation still never migrates — that was the finding, and silently inventing a migration for somebody's quote history is not a patch to slip in. Drift is now *detected and reported* (Warning + SessionStatus) so an operator sees it instead of getting an empty SAVED list |
| 14 | Unbounded DB growth | `PruneStaleLatestRunsAsync(olderThanDays = 180)`. Deletes only the *reserved latest-run* rows older than the cutoff; **named sessions are never touched**, so a saved quote's history cannot be pruned. Retention is a constant (`DefaultLatestRunRetentionDays`) rather than a setting, because 180 days was chosen as a conservative default, not as your decision — see the open question below |
| 17 | Send-to-engine wipes the list | `CutList.cs`: the `Pieces.Clear()` at the head of `SendCutListToEngine` is gone. **No dialog was added** — this module has no dialog service at all, it communicates through status lines, and that is also what lets the headless Step 5 host run without UI; injecting `IDialogService` into a DI-resolved view model is precisely the change the PI wizard records as having broken construction once already (ERR-633EEB). Instead the action stops being destructive: SEND now **merges** on the same key the job auto-import uses (normalized composition + size + position, summing quantity), and CLEAR CUT LIST remains the explicit way to start from nothing. The status line names how many rows were already there and how many matched |
| 8b | No wall-clock budget | `SearchPlan.TimeBudget` + `BudgetFor(tier, effort)` (5–40 min by tier and effort). `PackOptimize` runs a `budgetWatch` and a `BudgetExhausted()` check; the combination loop breaks on it and the effort ≥ 2 / ≥ 3 refinement blocks are skipped, returning best-so-far with a `TIME BUDGET` ScaleNote on the plan. **Limitation, documented in the code:** the budget is enforced at candidate boundaries, so one `ConsolidateAll` sweep over a very large group can still overshoot. Tier 1 leaves `MaxConsolidationSweeps = int.MaxValue` because two pinned benchmarks depend on it (`Benchmark_LowE_MatchesReference`, `Benchmark_OptimizeStage_NeverRegressesQuick`) |
| 8d | Governor ignores memory | `GetAvailableMemoryBytes()` via `GC.GetGCMemoryInfo()`, `EstimatedBytesPerPart = 1024`, and `ApplyMemoryPressure(...)`: when the estimated working set exceeds 50 % of available memory the plan drops a tier and `WithReducedBreadth` narrows every breadth parameter. `SearchPlan` is immutable, so this is a `CopyWith(...)` chain, not mutation |
| 8e | No per-piece rotation lock | The packer already honoured `CutPiece.AllowRotate` — `Orientations()`, the smart-placement candidate filter and the placement verifier all test it. The flag simply had no input path: `TryBuildInputs` wrote `AllowRotate = RotationEnabled` for every row. `PieceEntryVm.RotationLocked` now feeds it, a ROT LOCK column was added to the cutting-list rows in `JobsTabView.xaml` (header + per-row checkbox), and it round-trips through the session snapshot. Sessions saved before 2.0.4 deserialize it as `false`, i.e. today's behaviour |
| 4b / 4c | Export had no gate | `Report.cs`: `TryPassExportGate(what)`. It refuses (a) any group already flagged `ValidationFailed`, (b) any group whose `placed + unplaced` does not equal `GroupRun.PieceCount`, and (c) any group failing `GuillotinePacker.VerifyComprehensive` — which had **zero callers in the entire repository** until now and is the only check covering minimum breakout and material conservation. The plan stays on screen when the gate refuses; hiding it would repeat the original RC-009 complaint. The gate is wired into the three **plan-derived** exports (Layout Summary, Industrial Cut Report, Export All) and deliberately **not** into Parts Summary or Stocks Summary, which are input listings — an operator whose export was blocked still needs to export their inputs to compare |

Two honesty notes on verification:

1. **Conservation is checked against `GroupRun.PieceCount`, not a rebuilt list.** `VerifyComprehensive` wants an `IReadOnlyList<CutPiece>`; by export time only the result survives, so the gate reconstructs one from placed + unplaced pieces for the geometry and material checks. A list rebuilt from a result always agrees with that result, so using it for the *count* check would pass by construction. The count is therefore compared against the piece count captured **before** the engine ran.
2. **The Python packer harness is not in this workspace.** The 31-assertion harness used for the earlier phases lived outside the repository and is gone from the sandbox; it could not be re-run against this phase. What *was* re-verified for every file in this changeset: brace/paren balance with comments and strings stripped, and `tools/xaml_validator.py` across all 337 views (`ERROR=0`). Nothing here is claimed as runtime-proven — the 10-item runtime checklist above still stands, on the 2.0.4 build.

**Open question for the owner:** the 180-day latest-run retention in finding 14 is a default I chose, not a requirement you stated. If quotes stay live longer than six months in your workflow, raise `DefaultLatestRunRetentionDays` before this ships.

---

# PHASE 9 — apply the Sheet Store balance

Owner instruction: *"apply balance also."* In this codebase **balance** has one
meaning — the Sheet Store quantity available on a rack, carried into the engine as
`StockSize.MaxSheets` and surfaced on STOCKS as the *Available* column. It was
parsed, validated and reported correctly after Phase 2, but it was **not actually
applied** in three places.

| # | Finding | Evidence | Fix |
|---|---------|----------|-----|
| **B1** | Duplicate stock rows lost their balance | `DistinctBySize` was `GroupBy(size).Select(g => g.First())`. Two deliveries of the same 3210 × 2250 with Available 10 and Available 15 collapsed into one rack carrying whichever row was first — the other 15 sheets vanished, and the plan then reported pieces unplaced against glass standing on the floor | Balances at the same size are now **summed**. If ANY row at a size is unlimited the merged rack is unlimited, because summing a real number into the unlimited sentinel would invent a cap nobody typed |
| **B2** | The balance was per-group, never per-run | `RunCoreAsync` passed the same `stockPool` into `ResolveGroupStocks` for **every** cutting group, and nothing was ever deducted. A rack with Available 10 and no composition tag could be committed 10 sheets to 6mm clear *and* 10 to 8mm clear — twenty sheets cut from ten — with the sheet counts, utilization and cost for that phantom glass published through to Step 6 and Step 9 | A run-wide `balanceLedger` is built from the pool before the loop, each group receives `ApplyRunBalance(pool, ledger)` — what is LEFT, not what it started with — and the sheets in `result.Sheets` are deducted afterwards, keyed off `SheetLayout.Stock` so a group that fell back to another size charges the rack it really used. Surfaced as a `BALANCE:` note through the existing append-never-replace path |
| **B3** | Racks carried no thickness | `Margins.cs` was the **only** place in the module that built a `StockSize`, and it set width, height and balance — never `ThicknessMm`. So every rack in every run carried 0 | Racks now carry `ParseThicknessMm(composition)` |

### The trap in B2, and why the ledger drops racks instead of zeroing them

`MaxSheets == 0` is `StockSize.UnlimitedSheets` — it reads as **infinite glass** in
all three packer strategies (`GuillotinePacker.OpenNewSheet`, `Advanced`, `Smart`).
So an exhausted rack written back as 0 would hand the engine an unlimited supply of
the one rack that has run out. `ApplyRunBalance` therefore **removes** exhausted
racks. `StockSelectionService` already follows this discipline
(`if (effective <= 0 && sheet.Balance > 0) continue;`), so the ledger matches the
Production module's established pattern rather than inventing one.

### Why B3 matters more than bookkeeping

`GuillotinePacker.VerifyComprehensive` contains a piece-vs-stock thickness check
guarded by `sheet.Stock.ThicknessMm > 0`. With the field permanently 0 that check
**could never fire from the Optimization module** — 8mm pieces laid onto a 6mm rack
passed the oracle Phase 8 had just made the export gate. Setting the thickness wakes
it. Pieces already derive their thickness from their own glass ref through the same
parser, and a dedicated rack's composition *is* that glass ref, so the two sides
agree by construction; a manual rack has no composition, the parser returns 0, and
the check stays dormant exactly as today.

**Behaviour change to be aware of:** with the cross-composition opt-in enabled
(SETTINGS ▸ *Allow cross-composition stock*), a run that lays pieces onto the wrong
thickness will now be **refused by the export gate** instead of exporting. That is
the intended consequence — finding 5a already labelled such a plan as not real — but
it is a change an operator will notice.

### Ledger key, and the one case it cannot distinguish

The key is size + thickness + colour, deliberately **not** composition: composition
decides which `ResolveGroupStocks` branch a row is resolved through, and
`SheetLayout.Stock` does not carry the tag back out of the engine, so a
composition-keyed ledger could never be matched at deduction time. Thickness (B3) is
what keeps two different glasses at the same dimensions from sharing an entry.

Verified by case analysis that the size-based key cannot over-restrict: a group can
only be handed less than its own rack's balance if another group genuinely consumed
that physical glass, which requires the cross-composition path — where the
consumption is real.

### Checked and NOT changed

`ProductionOptimizationBridge.GetStockAvailabilityAsync` also does
`MaxSheets = gs.Balance` with no zero guard, which looks like the same bug — but its
query already filters `.Where(g => g.Balance > 0)`, so a zero balance can never
reach it. No patch. It does not subtract reserved/allocated the way
`StockSelectionService` does; that is a separate design question about whether the
Cutting Work Center should see committed stock, and it is recorded here rather than
answered silently.

No pinned test references `DistinctBySize`, and the tests that set `MaxSheets` build
`StockSize` directly and call the packer — unaffected by all three changes.

---

# PHASE 9 (safe) — hardening the balance work

Owner instruction: *"apply balance safely."* Phase 9 as first written applied the
balance correctly but not safely. Auditing it against the real data the module
produces found four ways it could hurt. All four are now closed.

### H1 — integer overflow could have marked a full rack as exhausted *(would have been a regression)*

`StockEntryVm._availableText` defaults to **`"99999999"`** — *"effectively
unlimited — usage is tracked, not capped"*. That is an ordinary `int`, not the `0`
sentinel, so B1's new `g.Sum(s => s.MaxSheets)` summed it like any other balance.
At roughly **22 rows sharing a size** the total passes `int.MaxValue` and wraps
**negative** — and the packer's gate is

```csharp
.Where(i => sheetsPerStock[i] < stocks[i].MaxSheets || stocks[i].MaxSheets == 0)
```

A negative cap fails that test on the very first sheet, so the rack would read as
exhausted and **every piece would come back unplaced**. Summation now accumulates in
`long` and clamps to `int.MaxValue`, and the run ledger's values are `long` for the
same reason.

### H2 — the default balance would have fired a BALANCE warning on every ordinary run

Because `99999999` is finite, default racks entered the run ledger, were deducted
sheet by sheet, and made `poolReduced` true — so a run where **nobody had ever typed
a balance** would have announced that groups "ran on a balance already partly
consumed by earlier groups". Pure noise, and exactly the kind of message that trains
an operator to ignore the real one.

`StockSize.EffectivelyUnlimitedSheets` is now a named constant, tested alongside the
`0` sentinel everywhere the question "is this rack limited?" is asked — in
`DistinctBySize`, in the ledger build, and in the VM default that previously
hard-coded the literal. Three hard-coded copies of the number are now one
definition.

### H3 — B1 summed balances across *different glass*

`AddStep5Rack` de-duplicates on `(W, H, composition)`, so the pool can legitimately
hold two rows at the same dimensions that are **different glass** — a 6mm and an 8mm
3210 × 2250. `DistinctBySize` grouped on size alone, so B1's summing merged them and
reported 20 sheets of a glass that only has 10.

The grouping key is now size **+ thickness + colour** — the same physical identity
`BalanceKey` uses for the ledger, so merging and accounting can no longer disagree
about what a rack is. This only became possible because of B3; before it, every rack
carried `ThicknessMm = 0` and the two glasses were indistinguishable.

### H4 — a group starved by the ledger reported the wrong reason

The refusal path hard-coded `OracleLabel = "NOT CUT — no rack for this glass"`. With
a run-wide ledger that is now often false: the rack exists, it is **empty**. The two
send an operator to different places — Step 4 to set a sheet size, or the Sheet Store
to top up a balance — and the first sends them hunting for a rack that is standing
right there.

The refusal re-resolves against the **untouched** pool. If racks exist there but not
in the reduced pool, the balance is what ran out, and the label says
`NOT CUT — rack balance exhausted earlier in this run`, with a ScaleNote telling the
operator to top up the balance or split the job across two runs.

### H5 — which group loses scarce glass is decided by list order, and the note did not say who

The ledger serves groups in cutting-list order, so the first group gets first call on
a short rack. That is deterministic but arbitrary, and the Phase 9 note reported only
a count. It now **names the affected groups** (first four, then `+N more`) and states
the rule outright: *move a group up the list if it should get first call on a short
rack.* An operator reading a bare count could not tell whether the group they cared
about was the one starved.

### Verified while hardening, and left alone

* **Both stages enforce the balance.** `SheetLayout` is constructed in exactly three
  places — `GuillotinePacker.OpenNewSheet`, `Advanced`, and `Smart` — and each has
  its own `MaxSheets` gate. QUICK reaches `OpenNewSheet` through `RunPass`; OPTIMIZE
  reaches it directly. `Smart` is the unreachable file from finding 21. There is no
  live sheet-creation path that ignores the balance.
* **`ProductionOptimizationBridge`** does `MaxSheets = gs.Balance` with no zero
  guard, which reads like the same bug, but its query filters
  `.Where(g => g.Balance > 0)` so a zero balance cannot reach it. No patch.
* **Precedence when a size mixes sentinels:** a real `0` (unlimited) row beats a
  `99999999` row, which beats a finite cap. Unlimited wins rather than being summed,
  because adding to "no limit" is meaningless — the same rule B1 already applied to
  the `0` sentinel, now extended consistently.

---

# PHASE 10 — the balance was never reaching the engine at all

Owner instruction: *"apply balance and do deep analysis and cross check more
deeply."* Phases 9 applied and hardened the balance **inside the run**. The deep
cross-check found that, for the Step 5 feed, there was no balance to apply: the
number never left the Sheet Store.

## The cross-check: every consumer of `GlassSheet` inventory

| Consumer | Filters `Balance > 0`? | Passes the balance to the engine? |
|---|---|---|
| `ProductionOptimizationBridge.GetStockAvailabilityAsync` | ✓ `.Where(g => g.Balance > 0)` | ✓ `MaxSheets = gs.Balance` |
| `StockSelectionService.GetCompatibleSheetsAsync` | ✓ `.Where(g => g.Balance > 0)` | ✓ `MaxSheets = effective` (less reserved + allocated) |
| `SheetStorePricingService.TryResolveStockSheet` | ✗ **none** | ✗ **returns only `WidthMm`/`HeightMm`** |

`GlassSheet.Balance` is `Stock - Used` — real inventory arithmetic, so 0 genuinely
means none left. The Optimization module was the **only** consumer of that inventory
which ignored it. It resolved a rack *size* from the store, then `AddStep5Rack` built
the row without an `Available` value, so `StockEntryVm`'s default of `99999999`
applied and the engine planned against **infinite glass** — including a rack the
store reported at `Balance = 0`, because nothing filtered on it and the largest sheet
of a colour was picked regardless. Sheet counts, utilization and cost for that glass
flowed on to Step 6 and Step 9.

## What changed

* **`TryGetStockBalance(colour, thicknessMm, w, h, out balance)`** — sync,
  cache-first with a bounded database fallback, matching either orientation, summing
  every inventory row at that size in `long` and clamping (the H1 discipline).
  Returns **false** when the store cannot answer.
* **`TryResolveStockSheet(..., out balance)`** — a balance-aware 5-argument overload
  that filters `Balance > 0`, so an out-of-stock 3210 × 2250 loses to a smaller rack
  that actually has glass. When every rack of that colour is at zero the answer is
  false and the caller's existing honest refusal runs — whose message, *"the Sheet
  Store had no stocked size for these glass colours either"*, is now accurate rather
  than optimistic.
* **`AddStep5Rack(..., int? storeBalance = null)`** — writes `AvailableText` when
  inventory gave a number, leaves the default when it did not.
* Both Step 4 racks (outer and inner) now look up their balance by the spec's own
  `OuterGlassColor` / `InnerGlassColor`; the cross-check fallback uses the
  balance-aware overload. A note reports how many racks now carry a real count, so
  applying inventory is never silent.

### Why the "cannot say" answer is `null` and never `0`

`0` in `StockSize.MaxSheets` is the `UnlimitedSheets` sentinel — **infinite glass**.
Answering "the store could not tell me" with `0` would hand the engine unlimited
stock for a rack inventory could not price. `null` leaves the pre-Phase-10 default in
place: the run plans exactly as it did before, and the operator is not shown a number
nobody gave. A *genuine* `0` is written as `0`, because `TryBuildInputs` already
leaves a zero-balance rack out of the run and says so.

## Two tests were already red before this phase

`Step5ProgressAndViewResultTests.The_sync_store_lookup_never_blocks_and_never_throws`
asserted `DoesNotContain(".Result")` across `TryResolveStockSheet`, `IsStockedSize`
and `TryGetSnapshot`. Verified by simulating the test's own `Slice` helper against the
shipped file: **both lookups contain `.Result`**, so that assertion has been failing
since the `SENIOR FIX 2026-09-20` that added a cold-cache database fallback to them —
the fix that stopped a first optimization run coming back with zero racks for DGU+LAM.

The assertion was not deleted, it was corrected to the invariant the code actually
keeps: **never block without a timeout.** Unbounded `.Wait()`, `.Wait();` and
`GetAwaiter().GetResult()` are still forbidden everywhere; and wherever a blocking
read does occur, the wait preceding it must carry an explicit bound
(`Assert.Matches(@"\.Wait\(\s*(\d+|timeoutMs)\s*\)")`). The two new Phase 10 lookups
were added to the same sweep. Re-simulated: **0 failures across all five markers.**

Both new methods are declared *after* the originals on purpose — the test slices the
file from the first `IndexOf` of each declaration text, so an overload placed above
the original would silently retarget that assertion at different code.

## Deliberate gap: the JOBS-tab Step 4 import still has no balance

`SyncStocksFromSelectedJobs` builds racks from `Step4Sheets`, whose tuple is
`(w, h, composition, source, pricePerSqm, specNumber, side)` — **no colour**. Inventory
is keyed by colour, so a lookup on size + thickness alone would merge a Clear and a
Low-E rack at the same dimensions: precisely the double-count `RefreshStockUsage`'s
header comment was written to prevent. It would hand a run a balance belonging to
glass it is not cutting, and **a wrong balance is worse than no balance** — it refuses
groups that do have stock. So that path keeps the visible `99999999` default and the
operator can type the real count. Recorded here as a known boundary, not an oversight.

## Two accounting systems over one run — cross-referenced, not unified

`RefreshStockUsage` tallies sheets **used** per rack, keyed `(W, H, composition)`, and
drives the STOCKS "used" column. Phase 9's ledger tracks sheets **remaining**, keyed
`(W, H, thickness, colour)` via `BalanceKey`, and constrains the engine. The keys
differ for a reason that is now documented at both sites:

* the tally *can* key on composition because it walks `GroupRun` objects, which carry
  the group's composition beside their sheets;
* the ledger *cannot*, because it deducts from `SheetLayout.Stock`, and `StockSize`
  carries no composition tag back out of the engine — so it uses thickness, populated
  since finding B3.

Case-checked that they cannot disagree on totals: every group is capped at its own
rack's balance by the packer, so after any prefix of groups the ledger remainder is
never below the sum of the untouched racks' caps. The ledger therefore only ever
constrains a rack that genuinely serves more than one group — the manual "any glass"
racks and the cross-composition opt-in, which is the bug it exists to fix. Note that
`StockSize.Color` is never populated by the view model, so the colour component of the
key is currently inert there; it does differentiate racks built by the Production
bridge.

## All four rack-construction sites, accounted for

| Site | Available set from | Status |
|---|---|---|
| `LiveRun.cs` `AddStep5Rack` | Sheet Store lookup | **Phase 10** |
| `Session.cs` restore | the saved DTO value | correct as-is — restores what the operator saw, does not re-lookup |
| `Stocks.cs` manual add | the operator's typing | correct as-is |
| `Stocks.cs` `SyncStocksFromSelectedJobs` | nothing (default) | documented deliberate gap |

Full chain now traceable: `GlassSheet.Balance` → `TryGetStockBalance` →
`AvailableText` → `TryBuildInputs` → `StockSize.MaxSheets` → run ledger →
`OpenNewSheet`'s `MaxSheets` gate, which both QUICK and OPTIMIZE reach.

---

# PHASE 11 — close the safe half of the remaining gap, and pin all of it

Owner instruction: *"do a proper senior developer recommendations implementation
for these, add if required."* Two things were required.

## 1. The JOBS-tab gap was closable after all — for the cases that are certain

Phase 10 left `SyncStocksFromSelectedJobs` without a balance, because `Step4Sheets`
carries no colour and a size-only lookup would merge a Clear and a Low-E rack at the
same dimensions. That reasoning holds — but it argued against *guessing*, not against
*asking a narrower question*.

`TryGetUnambiguousStockBalance(thicknessMm, w, h, out balance)` matches on size and
thickness, then collects the **distinct colours inventory holds there**. Exactly one
colour means the rack cannot be anything else, and the balance is safe to apply.
Zero or two-plus colours returns false and the rack keeps its visible default.

This is safe by construction rather than by argument: it never attributes a balance to
a rack it cannot identify. `GlassSheet.Color` defaults to `"Clear"`, so single-glass
sizes — the common case — resolve, and genuinely mixed sizes decline.

The result is reported both ways. `StocksStatusText` now says how many Step 4 racks
carry a real balance **and how many could not be matched**, because silently applying
a balance to some rows and not others is how an operator ends up trusting a number on
one row without knowing the next one is a default.

## 2. Three phases of behavioural change had no tests at all

Phases 9, 10 and 11 changed what glass a run believes it has — which flows into sheet
counts, utilization, and the cost published to Step 6 and Step 9 — and not one line of
it was covered. `Tests/Agent/OptimizationBalanceApplicationTests.cs` adds **18 tests**.

They follow the convention already established across `Tests/Agent`: behavioural
assertions against the public engine and service API, plus source-text pins for the
private view-model logic that cannot be called from a test (`RunCoreAsync` needs a
Dispatcher). The pins target the exact expression carrying each invariant, so a
refactor that drops it fails in CI rather than on the shop floor.

| Invariant pinned | Why its loss would be silent and expensive |
|---|---|
| `UnlimitedSheets != EffectivelyUnlimitedSheets` | conflating them either caps infinite glass or grants it |
| `WithMaxSheets` copies **every** property | dropping `ThicknessMm` would put the B3 oracle back to sleep |
| The packer gate reads `MaxSheets == 0` as unlimited, therefore `ApplyRunBalance` drops and never zeroes | an exhausted rack written as 0 becomes infinite glass |
| `DistinctBySize` sums in `long` and clamps | int overflow at ~22 default rows reads a full rack as exhausted |
| The `99999999` default never enters the ledger | a BALANCE warning on every run nobody set a balance for |
| Rows merge on `BalanceKey`, not size alone | two glasses at one size become one rack of double the stock |
| Racks carry `ThicknessMm` | the dormant piece-vs-stock oracle goes back to sleep |
| Groups get `effectivePool`, not `stockPool` | the original per-group double-commit returns |
| The refusal distinguishes `ranOutOfBalance` | an operator hunts for a rack that is standing there, empty |
| The note names the constrained groups | a count cannot say whether *your* group was starved |
| All three inventory consumers filter `Balance > 0` | one dropping the filter diverges from the others silently |
| "Cannot say" is `null`, never `0` | 0 is the infinite-glass sentinel |
| The unambiguous lookup declines on 2+ colours | a wrong balance refuses groups that do have stock |
| No blocking lookup without an explicit timeout | the UI thread freezes on a cold cache |
| The export gate still runs `VerifyComprehensive` and a pre-run count check | a plan that lost pieces reaches a cut file |
| `AllowRotate` and `RotationLocked` reach the engine and survive save/load | a locked pane gets rotated anyway |

**Every assertion in the file was verified by simulating the test's own `Read` and
`Slice` helpers against the shipped sources: 0 failures.** There is no dotnet SDK in
this workspace, so the file has not been compiled or run — it is written to the
repository's conventions and its string assertions are machine-checked, but it is not
proven green.

## Also corrected, not deleted

`Step5ProgressAndViewResultTests.The_sync_store_lookup_never_blocks_and_never_throws`
was already red before Phase 10 (see that section). The new test file carries the same
corrected invariant — never block *without a timeout* — and extends the sweep to the
Phase 11 lookup.
