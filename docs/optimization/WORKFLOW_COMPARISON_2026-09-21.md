# Before vs source vs integrated port

## Versions compared

| Version | Exact code |
|---|---|
| **Before: our advanced branch** | `024a1148d5db98049e780a88136f616492802fa8` |
| **Source: requested workflow branch** | `arena/01a0c38d-proglassautomationadv`, `b43782d3811225fed2b1dd0bfa19ac98d1d4cecc` |
| **Integrated port** | Changes on `arena/01a0c412-proglassautomationadv`, app version 2.0.5 |

**Verdict:** the source is the better reference for a straightforward operator workflow. The previous branch is the better foundation for the optimization engine and current data contracts. The integrated port is the preferred direction, not yet a Windows-verified repair.

The user's report establishes that the previous optimization workflow was broken in their use. The source was proposed as working; it was not independently run here. None of the ratings below is a measured runtime pass rate.

## Workflow patterns side by side

| Stage | Before: advanced branch | Source branch | Integrated port |
|---|---|---|---|
| Step 4 → Step 5 | Broader selection synchronization, including price/fingerprint changes | Separate price-only synchronization to avoid resetting type/spec | Source price-only synchronization; current Step 4 domain/persistence retained |
| Selecting jobs | Automatic rebuilds; direct saved-row import, distributed panes and synthetic fallback paths | Select jobs, review panes, then explicitly **ADD SELECTED PANES** | Explicit source flow; no synthetic pane fallback |
| Manual cut list | Coupled to automatic job-selection synchronization | Separate manual-entry workspace | Separate source workspace |
| Import identity | More extensive source/material identity handling, but more interacting paths | Simple deduplication by glass/label/dimensions can discard distinct jobs' matching rows | Job/spec/pane included in deduplication; repeated import stays idempotent |
| Sizes | Automatic regeneration from several input paths | Derived from explicitly imported pane rows | Source flow, with quantity-conservation regression tests added |
| Stocks | Automatic inventory balance lookup plus richer material constraints | Step 4 racks; defaults to usage-tracked availability; can borrow other specs' racks | Step 4 racks; rejects unrelated tagged-glass fallback; **automatic balance lookup was not carried over** |
| Inventory caps | Run-wide finite balance handling | Simpler stock handling | Explicit finite STOCKS values validated and shared across groups; exhausted racks removed |
| Search engine | Partitioned search, candidate index, governor, advanced strategies | Ordinary multi-start engine with a high-throughput strategy above 20k | Previous engine files unchanged; partitioned path for unconstrained runs; global advanced path for finite stock |
| Margin/pattern rules | Current thickness buckets, breakout rules and advanced engine contracts | Older rules and contracts | Current engine rules retained; source settings UI adapted to them |
| DGU+LAM | Independent third-pane sheet metadata and controls | Older two-slot assumptions; lacks current third-sheet contract | Current domain/schema and DGU+LAM controls retained; third rack routed separately |
| Live result feed | Current run id/fingerprint contract | Older publisher without the current run-id requirement | Current feed contract retained; source caller adapted |
| Saved sessions | Current identity-envelope JSON and legacy support | Older raw-array expectation | Current envelope retained; rack price/spec/side restored on reopen |
| Step 5 background host | Headless flag set after ordinary VM construction starts loads | Ordinary optimization VM construction | Headless constructor skips job/session loads before they start |
| Stale inputs/results | More existing invalidation and validation machinery | Simpler lifecycle | Input edits invalidate results; concurrent edits cancel; validation gates export/feed |
| Capacity | 2,000,000 expanded-piece admission guard; advanced engine | **20k is a strategy threshold, not a hard input cap** | 2,000,000 expanded-piece guard retained; million-piece performance unmeasured |

### Before

```text
Job selection / pane changes
    → automatic rebuilds
    → saved Step 5 rows OR distributed panes OR synthetic fallback
    → cutting list + manual-grid synchronization
    → sizes / stocks / validation
    → advanced engine
```

More automation and richer safeguards, but more coupled state transitions to reason about when imports or selections go wrong. Complexity alone does not prove the root cause of the user's failure.

### Source and integrated operator flow

```text
Select jobs → review/tick panes → ADD SELECTED PANES
    → inspect sizes → confirm stocks/settings → QUICK or OPTIMIZE
    → summary / 2D / report

Live Step 5 grid → QUICK or OPTIMIZE → result feed → Steps 6 and 9
```

The explicit import action makes it clearer when input changes enter the cutting list. It is a deliberate behavior change: selecting a job alone does not auto-import sizes.

## Ratings out of 10

These are **subjective code-review ratings for design suitability**, not measured correctness or reliability. Rough scale: 3 = substantial concerns, 5 = mixed strengths/gaps, 7 = good foundation with known limitations, 9 = strong design in the reviewed area. A higher score does not replace tests.

| Area | Before | Source | Integrated port, provisional | Reason |
|---|---:|---:|---:|---|
| Operator workflow clarity | 4 | **8** | **8** | Source has a clearer explicit import boundary |
| Step 4–5 synchronization design | 5 | **8** | **8** | Price-only updates are less destructive; current data schema stays |
| Large-job engine architecture | **8** | 6 | **8** | Advanced partitioning/index/governor retained, not benchmarked here |
| Import/data-contract protection | **8** | 5 | **8** | Current identity, session and third-pane contracts retained/adapted |
| Automatic inventory integration | **8** | 4 | 5 | Integrated port enforces explicit caps but restores usage-tracked import defaults |
| Lifecycle and stale-result protection | 7 | 5 | **8** | Constructor-time headless mode, cancellation and feed compatibility adapters |
| Verified end-to-end working status | **Not verified here; user reports broken** | **Not verified here; proposed as working** | **Not verified; Windows acceptance pending** |

**No overall “working properly” score is assigned.** It would conceal the difference between a sound design and a successfully executed production workflow.

## Important boundaries and remaining checks

- Every protected `Services/Optimization` and `Models/Optimization` file matches the previous baseline by SHA-256. No source engine or pattern files were imported.
- The previous engine is not flawless: its partitioner passes each batch the full rack cap. Finite inventory therefore uses the current globally capped optimizer, with a visible explanation, rather than multiplying a scarce rack across batches.
- The source engine handles more than 20k pieces through a different strategy. Claiming it stops at 20k would be incorrect.
- Two million **expanded physical pieces** is not proof of two million distinct dimension rows or acceptable UI performance.
- This is not a literal full-tree import. Current Step 4 domain code, three-pane controls, parsers, production services, repositories and unrelated app screens remain.
- Imported racks default to `99999999` usage-tracked availability. Confirm/set finite STOCKS values when inventory limits must constrain planning. The previous automatic balance-import behavior is a feature difference, not an improvement.
- Six portable checks pass. C# syntax and XAML checks passed in the previous turn. Windows compilation, xUnit execution, real invoice/job import, save/reopen, export and large-job benchmarking remain outstanding.

See [the engineering handoff](WORKFLOW_PORT_2026-09-21.md) for the full Windows acceptance checklist. Recommendation: continue with the integrated port, but do not declare the repair complete or ship a release before that checklist passes.
