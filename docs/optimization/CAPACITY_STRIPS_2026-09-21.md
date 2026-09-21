# QUICK capacity lookahead — 2.0.9

## Result on the supplied job

Executed the actual engine sources, not the previously constructed independent layout witness. Input: 46 HD Grey panes; stock 3210 × 2250 mm; trims 15 mm on every edge; kerf 3 mm; minimum breakout 15 mm; Free90; unlimited stock.

| Engine level | Before this change | After this change | Unplaced after | Comprehensive / breakout issues after |
|---|---:|---:|---:|---:|
| 1 / QUICK | 16 sheets / 42.59% | **9 sheets / 75.72%** | 0 | 0 / 0 |
| 2 | 12 sheets / 56.79% | **9 sheets / 75.72%** | 0 | 0 / 0 |
| 3 | 12 sheets / 56.79%* | **9 sheets / 75.72%** | 0 | 0 / 0 |
| 4 | 12 sheets / 56.79%* | **9 sheets / 75.72%** | 0 | 0 / 0 |
| 5 | 12 sheets / 56.79%* | **9 sheets / 75.72%** | 0 | 0 / 0 |

*The old Level 3–5 result also failed the separate breakout audit: pane 46 left a 5 mm offcut against pane 32, below the 15 mm minimum. The old comprehensive verifier did not incorporate that audit, so it incorrectly returned no problems.*

Exact input area is **49.220233 m²**; nine stock sheets total **65.0025 m²**. Utilization uses full stock area, not the trimmed usable rectangle. The existing result model sums four-decimal per-sheet areas (49.2204 m² in this layout); both calculations round to 75.72%. Candidate tie-breaking now uses exact decimal areas rather than these display-rounded values.

Nine sheets is a validated feasible result, **not a proof of global optimality**. The regression accepts nine or fewer sheets with all panes placed and valid geometry.

## Root cause: missing capacity lookahead, not a lost engine port

The protected ADV engine files were unchanged by the earlier UI/import port (original hashes remain in `protected-engine-manifest.json`). Their local placement policy already ranked exact edges and short-side remainder ahead of orientation. The comment claiming rotation happened only when unrotated placement was impossible did not describe the actual scoring.

A locally attractive orientation can prevent a second pane from fitting down a sheet. More orderings and shuffles do not necessarily escape that same local scoring preference. This job needed an alternative that reasons about **repeat counts across AND down**, plus residual-strip fillers.

| Boundary | Before UI port | Source UI/workflow port | This change |
|---|---|---|---|
| ADV packing algorithms | Existing local-fit engine | Retained, not replaced with source engine | Existing algorithms retained; generic candidate added |
| Nine-sheet target | Same relevant ADV engine logic | 16-sheet QUICK measured before this patch | 9-sheet QUICK measured |
| Target assessment | Missing capacity-based alternative | **Fail** for this fixture | **Pass** for all five levels |
| Windows UI acceptance | Not measured here | See prior workflow comparison and repair reports | Labels reviewed; Windows GUI not executed here |

The customer explicitly authorized this engine improvement after the earlier instruction to preserve the engine during the UI port. Manifest approvals document that exception without overwriting baseline hashes.

## Generic algorithm added

`GuillotinePacker.CapacityStrips.cs` adds deterministic column and transposed-row candidates:

1. Sort by descending area with existing stable identity tie-breaks.
2. Choose the smallest available stock that can fit the next anchor.
3. Evaluate allowed orientations by repeat capacity across × down; prefer more down on equal capacity.
4. Widen a seed strip only within the **same across/down repetition-capacity family**. This lets nearby sizes share a strip without a job-specific dimensional tolerance. Fixed seed widths alone produced ten sheets in the first implementation; family widening allowed the final residual strip to accept its rotated peers.
5. Fill from a bounded frontier containing both large anchors and small fillers. A doubly linked index pool gives constant-time removal and never revisits all previously consumed entries.
6. Derive separation from the settings: breakout + two kerfs when breakout is enabled, otherwise kerf. Check terminal remainders and flush-to-sheet boundaries; the comprehensive oracle remains the final acceptance gate.
7. Preserve each original `CutPiece` object, rotation permission, stock counter and cancellation token.

The **128-entry frontier is a search-work bound, not a part-count limit**. No labels, materials, customer dimensions, expected quantity, nine-sheet constant or stored layout occurs in production strategy code. Customer dimensions exist only in regression data/evidence.

QUICK compares the original shelf candidate with both strip axes. Levels 2–5 start with the validated QUICK result and keep all existing advanced strategies. Partitioned batches use the same checked selection, with a global QUICK floor. Finite inventory uses a global search rather than independently resetting caps in parallel batches. Level 1 uses the QUICK portfolio even through the partitioned API.

## Safety changes

- `VerifyComprehensive` includes geometry, breakout, identity conservation and per-stock cap checks.
- Candidates are verified **before** comparison; valid candidates beat invalid candidates even if the invalid plan uses fewer sheets.
- Invalid-only outcomes remain flagged inspection-only, rather than labelled exportable success.
- Strip plans are not passed through legacy consolidation that could reintroduce narrow offcuts. Existing shelf/advanced consolidation is retained and its output is audited.
- QUICK progress maps the shelf to its first 60%, then the two strip passes; `done` is emitted only after the full portfolio and verification finish.
- UI strategy and verification labels reflect the new behavior.

These checks validate the engine's existing geometric/breakout rules; they are not a new machine-specific cut-sequence certification.

## Executed verification

This Linux workspace lacks the native .NET 8 SDK / Windows WPF runtime. The unchanged C#12 engine and selected test bodies were compiled with Roslyn 5.9 and executed on .NET 10.0.11 through an external scratch harness. Test-body execution used a small assertion/attribute adapter, **not the real xUnit runner**. No translated engine, binary dependency, scratch tooling, or generated large dataset is committed.

- **97 selected test cases passed; zero failed.** Sources: `CapacityStripRegressionTests`, `OptimizationEngineTests`, `OptimizationBenchmarkTests`, `OptimizationSmartPlacementTests`, `OptimizationMarginHouseTableTests`, `OptimizationPartitionedTests`.
- Covers all five levels and direct QUICK, scaling, transposed dimensions, reversed order, 24 deterministic perturbed jobs, locked/per-piece rotation, repeatability, mixed racks, finite inventory above the partition threshold, asymmetric trims, oversized panes, cancellation, progress and the missing-breakout-verifier regression.
- Existing partitioned **10,000 / 25,000 / 50,000** jobs completed with no unplaced panes and no comprehensive-verification issues. Their assertions now check the comprehensive oracle instead of only `Verify`.
- Existing calibration tests passed, including Clear's optimized ≤16-sheet floor, Low-E's ≤17-sheet floor, and **Ramly's actual 2500 × 1875 mm smallest-stock regression** (not merely a run on the larger default rack).
- The preliminary four-fixture-class run revealed two pre-existing assertion problems, reproduced against saved pre-edit engine sources: a kerf test silently relied on old 25 mm house margins, and a Free90 test assumed the placed width could not rotate. They now use explicit kerf-only margins and original-piece identity plus geometry validation, respectively.
- **14/14 portable UI/import/hash contracts passed.** All protected baseline hashes retained; only explicitly approved existing files/addition accepted.
- `git diff --check` passed.

### New candidate scalability probe — not whole-engine timing

Executed `RunCapacityStrips` directly on deterministic mixed dimensions, followed by comprehensive verification. One column pass, Release compilation, on this sandbox:

| Panes | Placed | Unplaced | Issues | Generation | Including verification | Process working set* |
|---:|---:|---:|---:|---:|---:|---:|
| 10,000 | 10,000 | 0 | 0 | 0.28 s | 0.36 s | 252 MiB |
| 100,000 | 100,000 | 0 | 0 | 0.57 s | 0.65 s | 278 MiB |
| 1,000,001 | 1,000,001 | 0 | 0 | 4.54 s | 5.34 s | 666 MiB |

*Sampled working set includes the host/compiler and prior runs; it is not an allocation count or peak-memory guarantee. JIT warm-up affects the small run.*

The generator is O(N log N + K·N) for a fixed stock list and frontier K, plus result ordering. Existing full validators remain quadratic **within each sheet**, and the retained legacy portfolio/search has other costs. **No claim is made that the complete million-piece QUICK/partitioned search finishes in these times.** The existing search governor, partitioning, candidate limits, cancellation paths and two-million expanded-input ceiling remain intact.

## Reproduction and remaining gates

A real, portable .NET 8/xUnit target now links those exact engine and regression sources without WPF dependencies:

```sh
dotnet test Tools/EngineRegression/EngineRegression.csproj -c Release
python3 scripts/verify-optimization-port.py
```

The project target itself was not run here because SDK/package restore is unavailable; its selected source files were executed by the harness described above. Full Windows application build, real xUnit runner execution, UI operation, and machine export acceptance remain unverified.

An attempt to include the older `OptimizationScaleTests` in the scratch compilation exposed pre-existing references to removed `ScaleProfile.LargeThreshold` / `HugeThreshold` APIs. That stale class and other unrelated WPF tests are not included in the portable target; **97 passing selected cases is not a claim that the entire historical suite builds or passes**.

`CAPACITY_STRIPS_EVIDENCE_2026-09-21.json` records before/after metrics, the actual nine-sheet Level 1 placement coordinates, test summary and candidate-only scale measurements. The fixture dimensions are in `Tests/Optimization/CapacityStripRegressionTests.cs`.
