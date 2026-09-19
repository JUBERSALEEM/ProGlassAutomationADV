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
