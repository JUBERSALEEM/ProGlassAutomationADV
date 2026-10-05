# Optimization — Size-Import & Spec-Count Fixes (2026-09-12)

Follow-up to `OPTIMIZATION_STOCKS_STEP4_FIX_2026-09-12.md`. This round
fixes four concrete defects in the JOBS → SIZES → STOCKS → run chain that
caused "sizes not getting imported" and a wrong spec count on the job card.
No engine parameters changed — the packer stays pinned to the owner's
benchmark (Low-E 17 sheets @ 82.77%).

## 1. Build type now honours the operator's Step 5 glass type

`Services/Production/ProductionDistributionEngine.ResolveBuildType`
previously ignored `SpecGroupSnapshot.SelectedGlassType` and relied on
Step 9 targets or the spec-name grammar.

Problem: a spec whose NAME does not parse to two panes (e.g. a free-typed
"6mm Clear DGU" name) collapsed to **SGU**, so its INNER pane — its sizes,
pieces and Step 4 sheet — silently disappeared from the optimization
import and from the production distribution.

Fix — precedence is now:

1. `SelectedGlassType` ("SGU" / "DGU" / "LAM" / "DGU+LAM"), normalised by
   the new `NormalizeGlassTypeToBuildType` helper (unknown values fall
   through, so legacy data degrades exactly as before);
2. Step 9's persisted `SpecTargets.BuildType`;
3. the spec-name grammar.

`Distribute` and the optimization picker both call this same resolver, so
production cutting and the optimization import can never disagree on how
many panes a spec physically has.

## 2. JOBS card shows the PI's real spec count

`OptimizationViewModel.Jobs.cs` counted `opt.Specs` — i.e. only the spec
groups that produced at least one selectable pane. A Job Order with two
specs where one had no cutting rows yet read "1 spec".

Fix: `specCount = groups.Count` (the PI's actual, non-null specification
groups), so the card and summary always report the true number of specs.
A group with no cutting rows still contributes nothing to the pane
breakdown (there is nothing to select), which is correct.

## 3. Each cutting group packs ONLY on its own Step 4 rack

`OptimizationViewModel.Run.cs` `ResolveGroupStocks` lumped dedicated
Step 4 racks (composition-tagged) and manual "any group" racks (empty
composition) into one list. The packer's "smallest stock first" rule could
then route a spec's pieces onto a manual rack even when the spec had its
own Step 4 sheet — and the documented manual-rack fallback branch was
unreachable dead code.

Fix — strict precedence, matching the documented intent:

1. dedicated racks for that glass composition;
2. manual "any group" racks (with the "no Step 4 sheet" note);
3. only then every available rack, so pieces are never silently dropped.

This keeps the run reproducing the reference exactly (low-e → 3210×2250,
clear → 3210×2250, ramly → 2500×1875) even when an operator has added a
manual rack.

## 4. SIZES tab "SIZE ROWS" shows distinct rows, not pieces

`SizesTabView.xaml` bound the "SIZE ROWS" column to `PieceCount`. A group
with one 1050×2127 row at qty 2 showed "2" size rows.

Fix: `SizeGroupVm.SizeRows` carries the distinct W×H count (computed in
`RefreshDerived`), and the column binds to it.

## Validation

- All 252 XAML files parse clean (XML round-trip).
- All edited C# files brace/paren balanced (no .NET toolchain available,
  so validation is static, consistent with prior rounds).
- `ResolveBuildType` callers all pass a non-null group; the new
  normalisation returns `null` for unrecognised values, so the behaviour
  for legacy data is unchanged.

## What was deliberately not changed

- Engine margins/kerf/rotation: unchanged — the benchmark stays pinned.
- STOCKS pool semantics: unchanged — it already replaces the pool with
  only the ticked jobs' Step 4 sheets (`SyncStocksFromSelectedJobs`);
  the doc comment was corrected to match (manual racks are replaced on
  selection change, not preserved).
