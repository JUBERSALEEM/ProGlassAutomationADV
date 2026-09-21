# Level 1 pattern promotion — version 2.0.10

## Request and diagnosis

The second user job previously returned 14 sheets / 68.41% at Levels 1–3 and 13 sheets / 73.67% at Levels 4–5. The winning strategy was Best-Split Beam, width 8. Its availability was gated by effort; QUICK could not choose a candidate it never evaluated. This was a search-policy limitation, not a failure to import or rotate panes.

The owner requested the deeper winning strategy in Level 1 and additional search breadth at Levels 2–5. The existing engine is retained, not replaced with a source-branch engine.

## Executed results

Same settings: 3210 × 2250 mm stock, 15 mm trims on all four edges, 3 mm kerf, 15 mm minimum breakout, Free90, effectively unlimited inventory.

| Level | Second job, before | Second job, now | First job, now |
|---|---|---|---|
| 1 / QUICK | 14 / 68.41% | **13 / 73.67%** | **9 / 75.72%** |
| 2 | 14 / 68.41% | **13 / 73.67%** | **9 / 75.72%** |
| 3 | 14 / 68.41% | **13 / 73.67%** | **9 / 75.72%** |
| 4 | 13 / 73.67% | **13 / 73.67%** | **9 / 75.72%** |
| 5 | 13 / 73.67% | **13 / 73.67%** | **9 / 75.72%** |

All 42 panes in the second job and all 46 in the first job are placed. Comprehensive verification and the separate breakout audit report zero issues at every level. Second-job winner: `PATTERN BEAM (MaxSide, width 8)`. First-job winner remains capacity strips.

The second cutting list remains external scratch input: **it is not added to production code, test fixtures, or checked-in sample data**. The new regression cases use generated dimensions. These results show feasibility, not a proof of global optimality.

## Search ladder

Every higher level retains the validated QUICK floor. The new pattern candidate schedule is cumulative:

| Level | Additional pattern search | Existing strategies |
|---|---|---|
| 1 | Max-side order; beam widths **4 and 8** | Shelf and both capacity-strip axes retained |
| 2 | Area order; beam **12** | Multi-order grid, Best-Split, Sheet-Fill, Wall-Seed, Smart Placement retained |
| 3 | Min-side order; beam **16** | Broader split/order combinations, MaxRects and eligible legacy beam search retained |
| 4 | Height order; beam **24** | Shuffles, both Smart Placement split preferences and deeper existing search retained |
| 5 | Width order; beam **32** | Maximum existing ordering/shuffle breadth retained |

Each beam explores alternative per-sheet guillotine split patterns and prunes equivalent partial search states. Equivalence now includes **per-stock consumption and pane rotation permissions**, rather than only remaining dimensions and total sheet count. Different orderings and wider beams provide additional candidates, not merely different UI labels. They do not guarantee another saved sheet on every job; this job stays at 13.

## Bounded search and large jobs

`GuillotinePacker.PatternSearch.cs` promotes the existing beam algorithm without running a whole-million-pane beam:

- Up to **256 panes**: evaluate the whole job.
- Larger inputs: repair at most **six weak sheets**, with at most **256 total panes** in the search window, optionally including up to 64 unplaced panes.
- These are **search-window limits, not input or placement limits**. All other panes remain in the result.
- Every candidate is bounded to `256 × beam width` state expansions and three consolidation sweeps. Candidate boundaries observe the applicable QUICK/OPTIMIZE time budget; cancellation still throws.
- Finite stock committed outside a repair window is reserved before the local beam opens sheets. Original stock objects are retained, so global inventory validation remains meaningful.
- Candidate assembly copies sheet containers, avoiding mutation of the previously validated floor while renumbering.
- The existing comprehensive verifier and valid-first comparison decide whether to keep the candidate. A failed or inferior attempt cannot replace the floor.
- Partitioned batch searches also receive the bounded pattern improvements. Existing partitioning, advanced engine files, governor and two-million expanded-input ceiling remain.

The old whole-job beam is still available at its existing deeper-search scale gate. QUICK deliberately does **not** promise all of the old Level 5 search at unlimited cost. Whole-job versus local-window scope means some other large jobs can still benefit from higher effort. Existing legacy passes and full-result validation also have their own costs; these bounds do not imply a hard end-to-end wall-clock deadline or a whole-million-job timing guarantee.

## Verification

- **111 selected test cases passed**, zero failed, through the existing Roslyn 5.9 / Linux .NET 10.0.11 source-engine harness and assertion adapter. This is not the native Windows application or real xUnit runner.
- The 14 new pattern cases cover the valid beam-4/8 quality floor over generated jobs, cumulative schedules, 255/256/257/400-pane boundary behavior, finite stock, pane conservation, immutable retained layouts, increasing effort, an expired optional budget and cancellation at the new stage.
- Existing calibration, breakout, rotation, progress, input-conservation and **10k / 25k / 50k partitioned** cases passed again.
- **14/14 portable UI/import/hash checks passed**; original protected hashes remain with explicit owner-authorized exceptions.
- Both customer jobs were executed separately against the actual final engine sources, not a constructed witness.
- `git diff --check` passed.

The portable real-xUnit project includes the new tests:

```sh
dotnet test Tools/EngineRegression/EngineRegression.csproj -c Release
python3 scripts/verify-optimization-port.py
```

Native .NET 8/xUnit execution, Windows GUI acceptance and full million-piece end-to-end benchmarking remain outstanding. The previous report documents stale historical scale tests excluded from the portable target; selected-test success is not a claim that the entire historical Windows suite builds or passes.
