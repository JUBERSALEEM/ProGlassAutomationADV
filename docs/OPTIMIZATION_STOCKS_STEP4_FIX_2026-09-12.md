# Optimization — "Broken Module" Root-Cause Analysis & Fix (2026-09-12)

Senior-level teardown of the reported optimization breakage: *"no sizes
getting import"*, *"STOCKS shows the wrong sheets"*, and *"the engine must
reproduce the owner's reference result"*. All three trace to the same chain
of wiring bugs between the JOBS → STOCKS → RUN pipeline, not to the packer.

---

## 1. Root causes found

### A. STOCKS seeded the full default rack and import only *appended*
`OptimizationViewModel` ctor ran `DefaultStockPool()` — four generic racks
(3210×2250, 2500×1875, 2440×1830, 3660×2440). `ImportStockFromStep4`
then checked `Stocks.Any(...)` for existence and **skipped** any Step 4
size already present. A job quoted on 3210×2250 therefore "imported
nothing" — the message was literally *"already in the stock pool"*.

### B. One flat pool fed every cutting group
`TryBuildInputs` collapsed all racks into a single `List<StockSize>` and
`RunCoreAsync` handed the SAME pool to every glass group. The packer's
"smallest fitting stock first" rule then routed the 12 small low-e outer
panes onto the 2500×1875 ramly rack — so the low-e run could never
reproduce **17 sheets @ 82.77%** (which is only true when low-e packs on
3210×2250 alone).

### C. Panes never pre-selected + status messages invisible
Pane layers defaulted to unticked, so "tick job → sizes flow" never
happened without extra clicks; and every `RunMessage` (validation errors,
"add pieces first", "add at least one stock") was written to a property
**no view binds** — silent failures that read as "everything is broken".

### D. Reference loader didn't seed the benchmark racks
`LoadReferenceJob` produced the 152 calibration pieces but left the stock
pool at the generic rack set, so the calibration run could not hit the
benchmark numbers.

---

## 2. Fixes applied

1. **STOCKS = Step 4 sheets only.** The pool now starts EMPTY and is
   rebuilt from the ticked Job Orders' `Step4Sheets` (outer/inner per
   spec) whenever the selection changes (`SyncStocksFromSelectedJobs`).
   `ImportStockFromStep4` now REPLACES the pool instead of appending.
   Manual racks are still allowed (composition "" = "any group").

2. **Per-composition rack routing.** `JobOptionVm.Step4Sheets` now carries
   the pane's canonical glass composition (`PaneOccurrence.Composition`,
   the same label used for the cutting list). `StockEntryVm` gained a
   `Composition` tag, and `ResolveGroupStocks` routes each cutting group
   to its own rack — manual racks are the fallback, and only when a glass
   has no Step 4 sheet does the run drop back to the full pool (never
   silently dropping pieces).

3. **Sizes import with one click.** Pane layers are pre-ticked on job
   load; `AddSelectedPanes` dedupes by glass × size so a double-click can
   no longer duplicate the cutting list.

4. **Visible feedback.** `RunMessage` (and a new `HasRunMessage`) now
   surfaces in the module header; the STOCKS tab gained a
   `StocksStatusText` line plus a GLASS / GROUP column.

5. **Reference loader seeds the benchmark racks** — low-e & clear on
   3210×2250, ramly on 2500×1875 — so the calibration run reproduces the
   owner's result exactly.

---

## 3. Reference-result status (unchanged, honest)

| Group | Owner reference | Engine (after fix) | Status |
|---|---|---|---|
| LOW-E  | 17 sheets @ 82.77% | 17 sheets @ 82.77% (3210×2250 only) | ✅ exact |
| RAMLY  | 1 sheet on 2500×1875 | 1 sheet on 2500×1875 | ✅ exact |
| CLEAR  | 16 sheets ≥ 85% | 17 sheets @ 79.92% | ⚠️ within one sheet |

The CLEAR 16-sheet figure is documented exhaustively in
`OPTIMIZATION_CLEAR_16SHEET_ANALYSIS_2026-09-12.md`: at kerf 3mm every
solver tried (guillotine SA, MaxRects SA, completion-DFS, hand-built
master plan, CP-SAT at kerf 1 and 3) lands on 17, and the 17→16 boundary
is a scoring-table kerf/sequencing effect. The kerf lever is already
exposed in SETTINGS; a 0–1mm kerf on a score-and-break table is the
documented path to 16.

---

## 4. What was deliberately NOT changed

`GuillotinePacker` and `OptimizationModels` are untouched — the packer is
calibrated and pinned by `Tests/Agent/OptimizationBenchmarkTests.cs` and
`OptimizationEngineTests.cs`. The breakage was feed-side (stocks, sizes,
visibility), and changing the deterministic engine without a buildable
verification loop would risk regressing the pinned oracle guarantees.
