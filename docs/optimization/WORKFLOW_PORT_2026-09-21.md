# Optimization workflow port — engineering analysis and handoff

> **2.0.8 follow-up:** [Step 4/5 sync and advanced-controls repair](STEP4_SYNC_AND_ADVANCED_CONTROLS_2026-09-21.md) restores omitted controls and fixes spec notifications/rate ownership. Protected engine files remain unchanged except the explicitly requested thick-glass house-table correction (TM 0, BM 50); the original hashes and approved exception are recorded separately. Windows acceptance is still pending.

> **2.0.7 follow-up:** the reported job/pane/Sizes failure exposed a retained integer-parser bug (successful parse with zero outputs), missing card/gesture wiring and stale group projections. See [the repair and revised assessment](IMPORT_WORKFLOW_REPAIR_2026-09-21.md). Earlier workflow ratings are not evidence of a working import. Current portable checks: **11/11**; Windows build and runtime acceptance remain pending.

## Status

**Implemented in the working tree; not certified as a working Windows release.**
Source/static checks passed. The Windows app has not been compiled or exercised in
this Linux sandbox: no .NET SDK is installed and SDK download attempts failed.
No million-piece benchmark was run. Do not interpret the preserved capacity
settings as a measured performance result.

- Destination: `arena/01a0c412-proglassautomationadv`
- Destination baseline: `024a1148d5db98049e780a88136f616492802fa8`
- Requested source: `arena/01a0c38d-proglassautomationadv`
- Source inspected: `b43782d3811225fed2b1dd0bfa19ac98d1d4cecc`
- App version: **2.0.5** (no rollback to the source branch's version)

## Why this is a selective replacement, not a merge

The complete source/destination comparison spans 455 files, including unrelated
production, invoicing, inventory, security-adjacent persistence, shell and theme
changes. Importing the whole tree would delete the partitioned optimizer and
its supporting contracts as well as unrelated current fixes.

The implemented boundary is **optimization presentation/workflow plus the Step
4–5 UI and rate synchronization**, not the entire application's UI. Current
production/distribution services, repositories, saved invoice schema, engine,
patterns, app shell and unrelated modules remain in place.

### Replacement surface

| Surface | Decision |
|---|---|
| Optimization main view and eight tab views | Imported source layouts/styles; input tabs disabled during a run |
| Jobs, pane picker, sizes, stock selection | Source workflow, adapted for current contracts and provenance |
| Manual cut list | Source implementation; obsolete synthetic/automatic job-to-grid paths removed |
| Run, margins, progress, live Step 5 handoff | Source workflow with advanced-engine and safety adapters |
| Step 4 SGU/DGU/LAM/pricing views | Source views |
| Step 5 grid and summary views | Source views, including run popup and select-all behavior |
| Step 4 → Step 5 rate edits | Source rate-only synchronization, respecting manual overrides |
| Step 4 data view models and DGU+LAM view | Retained current schema, pricing capture/restore, material validation and three-pane sheet controls |
| Step 5 snapshots/spec models/parser | Retained current data integrity and strict quantity parsing |
| Optimization costing/report/session/feed/lifetime | Current contracts retained or adapted; not blindly downgraded |
| Whole shell/theme/other ERP modules | Not imported |

The retained Step 4 domain code is intentional: the older branch lacks the
independent DGU+LAM third-sheet contract. Replacing those files would regress
persisted invoices and the production distribution engine even if the older
screen looked correct. The source's rate-only handoff is installed in the
composer without removing current approval and disposal behavior.

## Engine preservation

Every tracked file under `Services/Optimization` and `Models/Optimization` is
byte-for-byte unchanged from the destination baseline. This includes:

- GuillotinePacker main, Advanced, BestSplit, Smart, SmartPlacement, Partitioned;
- PieceCandidateIndex and OptimizationSearchGovernor;
- margin, search, partition and result models;
- material, composition, document/session identity and persistence contracts.

`protected-engine-manifest.json` records baseline SHA-256 hashes. Run:

```sh
python3 scripts/verify-optimization-port.py
```

No older engine, algorithm, candidate grid, or pattern file was imported.

### Dispatch and capacity

- QUICK uses the existing `PackQuick`.
- Unconstrained OPTIMIZE uses the existing `PackOptimizePartitioned`. Its own
  configuration chooses ordinary search for smaller groups and partitioning
  above the existing 2,000-piece threshold.
- Finite-stock OPTIMIZE uses the current global `PackOptimize`, not the source
  engine. The preserved partitioner passes a full stock cap to each independent
  batch; using it directly with a scarce rack could multiply available stock.
  The adapter therefore takes the globally capped path and tells the operator.
- A run-wide balance ledger deducts finite stock used by previous groups;
  exhausted rows are removed, never converted to the engine's `0 = unlimited`.
- The existing **2,000,000 expanded physical pieces** admission limit is retained.
  This is not a limit of two million distinct dimension rows, nor a guarantee of
  acceptable memory/time on any particular PC. No 20k admission cap was imported.
- Display sampling remains: cut-plan, sheet, and unplaced UI rows are bounded;
  engine input is not truncated to those display counts.

The restored source stock workflow defaults imported racks to usage-tracked
availability (`99999999`), rather than automatically fetching current Sheet
Store balances. Explicit finite values in STOCKS are validated and enforced.
Operators requiring inventory-constrained planning must set/confirm those
values; this port is not a redesign of stock reservation/consumption.

## Integration findings and changes

1. **Engine handoff:** a straight source Run.cs copy only calls the older ordinary
   OPTIMIZE entry point. Reconnected it to the preserved advanced paths above.
2. **Feed rejection:** current `PublishLive` requires a run id to mark a result
   current. The older caller omits it. Kept run ids and fingerprints so Steps 6–9
   can read a finished run instead of silently rejecting it.
3. **Session incompatibility:** current `PiecesJson` uses an identity envelope;
   source expects a raw array. Kept current envelope parsing/serialization and
   legacy handling. Restored rack price, material and spec/side when reopening.
4. **Headless race:** Step 5 previously constructed a normal VM and only then
   marked it headless. Added constructor-time headless selection so no picker or
   session database load can replace live Step 5 inputs later.
5. **Incorrect run-start reporting:** `Run` now returns whether validation/start
   succeeded, and Step 5 returns that outcome rather than always claiming success.
6. **Wrong-glass fallback:** removed source's fallback to other specs' tagged
   racks. A group needs matching stock or an explicit manual untagged rack.
7. **Source deduplication defect:** source deduplicates imported pieces by label
   and dimensions. Distinct jobs/specs/panes can share those values. Import keys
   include job, spec and pane; repeat-clicks are idempotent without dropping
   another job's quantities. The import uses a hash set instead of a nested scan.
8. **Picker refresh/deselection:** selected job ids survive refresh; deselecting a
   job removes its sourced pieces only. No 100-job query truncation remains.
9. **Three-pane materials:** DGU+LAM receives its independent third rack. Store
   fallback uses the actual pane's colour/thickness rather than SETTINGS or the
   first pane. Rack provenance retains multiple spec/side aliases for costing.
10. **Stock parsing:** zero excludes an exhausted rack; negative/invalid input
    refuses the run; only blank/∞ explicitly means unlimited.
11. **Validation and stale state:** comprehensive verification runs off the UI
    thread against actual inputs. Failed groups cannot publish/export as valid.
    Input edits invalidate results/feed; edits during execution request cancel;
    stale queued progress is ignored. Disposal cancels and detaches handlers.
12. **Rate edits resetting glass:** retained source's rate-only push, rather than
    applying the entire selection snapshot for every rate notification.

### Deliberate workflow change

The imported workflow is **select jobs → review/tick panes → ADD SELECTED PANES
→ sizes → stocks/settings → run**. Merely selecting a job no longer synthesizes
or force-loads a manual cutting grid. Calibration data is not injected. Job
snapshots must contain real distributable panes. The dedicated manual cut-list
path remains separate.

## Validation performed

- Protected engine/model hash comparison: passed for every manifest entry.
- Portable port checks: **6 passed** (hash boundary, XML, dispatch/cap, source
  workflow exclusions, feed/session contract and rate-only/headless wiring).
- C# syntax parsing with tree-sitter: no parse errors in touched/new C# files.
  This is **not** C# compilation/type checking.
- `scripts/xaml-resource-lint.py`: clean across 337 XAML files; 116 pre-existing
  typed-resource warnings reported.
- `scripts/xaml_validate.py`: zero fatal issues; one existing duplicate
  `SettingsCardMargin` key in `Views/Settings/SettingsView.xaml`.
- `git diff --check`: clean.

C# regression coverage was migrated from the retired private auto-import method
names to explicit-pane import behavior. New adapter tests cover >20k expansion,
the 2M preflight ceiling, bad/zero inventory, rotation locks, wrong-rack refusal
and feed invalidation. Pure engine/model tests remain; obsolete UI source pins
were replaced, not used as evidence that the new UI works. **The xUnit suite was
not executed here; the remaining source-contract tests need a Windows run too.**

## Required Windows acceptance before shipping

```powershell
dotnet restore ProGlassAutomation.csproj
dotnet build ProGlassAutomation.csproj -c Release
dotnet test Tests/ProGlassAutomation.Tests.csproj -c Release
```

1. Fresh invoice: Step 4 SGU/DGU/LAM/DGU+LAM sheet selection, pricing and navigation
   round-trip. Change a rate after a manual Step 5 type/spec override; verify that
   type/spec and manual base rate remain intact.
2. Import a real XLSX/CSV into Step 5; verify quantity and W1/H1/W2/H2, then QUICK
   and OPTIMIZE. Exercise cancel, invalid dimensions, no rack and exhausted stock.
3. Confirm a PI/JO, refresh jobs, select pane subsets, explicitly import, and
   compare totals with approved Step 5. Import two jobs with identical row refs
   and sizes; repeat the import, deselect/reselect and refresh.
4. Check all tabs, margin editing, per-group routing, 2D navigation, report
   generation, disposal/navigation mid-run, and Step 5 popup hit testing.
5. Verify live Steps 6/9, manual SAVE/reopen, VIEW RESULT and process restart;
   open legacy and identity-envelope sessions. Ensure a different invoice cannot
   receive a multi-job aggregate as its own spec results.
6. Inventory scenarios: finite cap shared across groups, repeated rack aliases,
   exhausted cap, manual any-group racks and missing specific material.
7. On a suitably provisioned Windows machine benchmark 20,001 / 50,001 / 250,000 /
   1,000,001 expanded pieces, then distinct-size stress cases. Record peak working
   set, import/expansion time, cancellation latency, verification time, UI
   responsiveness, input = placed + unplaced and layout correctness. Expansion,
   grouping, result projection and export still include UI-thread work; retaining
   the advanced engine alone does not prove those operations scale acceptably.

Do not deploy this as a verified repair until those checks pass. No release,
installer or PR was made. The user subsequently requested GitHub synchronization
of this session branch. See [the side-by-side comparison](WORKFLOW_COMPARISON_2026-09-21.md)
for the before/source/integrated ratings and explicit feature differences.
