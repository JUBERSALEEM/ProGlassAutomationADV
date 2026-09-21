# Quantity-aware repeated-pattern engine — 2.0.11

## Delivered behavior

Level 1 now compares a **whole-order repeated-pattern candidate**, not just individual-pane shelf/strip packing and a small beam repair. The actual C# engine generates patterns, solves their repeat quantities, completes the integer remainder and expands the original pane identities. There is no Python process, SciPy installation, native solver DLL or stored customer layout in production.

Levels 2–5 retain the improved QUICK floor and increase the pattern-search/rounding/branch budgets alongside existing eligible strategies. Existing advanced algorithms and files remain. At industrial quantities the dispatcher avoids whole-order quadratic shelf/beam work rather than pretending that an unlimited expanded search scales to millions.

## Measured customer results

Neutral 44: all 131 source rows, **29,796 W1/H1 panes**, nearest-whole-mm dimensions, 3660 × 2140 stock, 15 mm trims each side, 3 mm kerf, 15 mm breakout, Free90, effectively unlimited stock.

| Level | Before quantity-aware integration | Integrated engine |
|---|---|---|
| 1 | 9,573 / 62.60% | **6,909 / 86.74%** |
| 2 | 8,769 / 68.34% | **6,909 / 86.74%** |
| 3 | 8,769 / 68.34% | **6,909 / 86.74%** |
| 4 | 8,769 / 68.34% | **6,909 / 86.74%** |
| 5 | 8,769 / 68.34% | **6,909 / 86.74%** |

Every level places every pane with zero comprehensive-verification and separate breakout issues. The new Level 1 saves **2,664 sheets** against the old QUICK and **1,860** against the previous deeper result.

**Important provenance distinction:** the earlier external C++/SciPy/HiGHS experiment found **6,908 / 86.75%**. The integrated managed solver currently finds **6,909 / 86.74%**, one sheet more. It is not presented as reproducing the external integer solver exactly or proving a global optimum. A restricted-pattern LP objective is also not a global packing lower-bound certificate.

Earlier customer cases remain valid at every level: the 46-pane fixture retains ≤9 sheets / ≥75.72%; the separate 42-pane run retains 13 / 73.67%. The newer customer cutting lists remain external scratch inputs, not embedded in production or regression fixtures.

## Algorithm

1. **Aggregate compatible demand.** Distinct width, height, rotation permission, thickness, color and material identity are tracked separately. Original `CutPiece` references remain available for exact expansion.
2. **Price reusable guillotine patterns.** Integer-mm dynamic programming builds strip/crosscut patterns along both axes. It considers common kerf cuts and waste separators derived from the configured breakout plus two kerfs. Width trims and terminal remainders are checked during construction.
3. **Balance whole-order quantities.** A managed restricted-master simplex chooses fractional pattern multiplicities against exact demand and finite-stock rows. Unplaced demand has an explicit penalty greater than placing the whole window on individual sheets.
4. **Make the plan integer.** Feasible flooring, deterministic multi-start completion, bounded exact-cover repair, alternate optimal-basis tie orders and optional deeper branch-and-bound search choose whole repeats. Count and rack availability checks—not floating-point tolerances—authorize every consumed pattern.
5. **Finish small remainders.** Partial versions of patterns avoid stranding the last few repeats. Subsets are checked for newly introduced narrow gaps. Existing strip, Smart Placement and bounded beam candidates also compete on the small remainder while reserving inventory already used by bulk repeats.
6. **Validate before selection.** Existing comprehensive geometry/identity/inventory and breakout checks gate selection. The result is compared with the retained floor. A worse or invalid candidate cannot replace a valid better plan.

Higher effort expands pricing-round, rounding-diversification and integer-search budgets. More search can return the same result; it is not a promise to save another sheet at each level.

## Scale/resource policy

Search is bounded by distinct types and candidate work, not by silently truncating the order:

- Up to 128 types per pricing window; larger repeated families are processed in further windows with inventory carried forward.
- Up to 32 stock alternatives priced per window; untouched inventory/panes remain available to completion.
- At most 8,192 generated master columns, 256 panes per generated pattern, and 16,384 integer positions per pricing axis. Unsupported pricing dimensions use existing completion candidates rather than losing input.
- Bounded simplex pivots, integer nodes, repair sizes and per-run pattern-search time budgets (30–70 seconds by effort). Budgets govern optional search; completing/validating the incumbent can take additional time.
- All-singleton/high-diversity inputs bypass the expensive quantity master when it has little repetition to exploit. Small-job shelf/strip/beam candidates remain available. This avoids wasting tens of seconds on a small unique-size job.
- At **100,000+ panes**, public QUICK, OPTIMIZE and partitioned entry points use a global count-based master, both scalable strip floors and bounded remainder/weak-sheet searches. They do not launch the old whole-order quadratic passes.
- Cancellation is observed in aggregation, pricing, solver iterations, repair and expansion.
- Finite stock is reserved across windows/remainder repairs. Exhausted stock is removed; it is never converted to the zero-means-unlimited sentinel.
- Layout schema/provenance is unchanged. Expanded outputs and UI/export still require O(N) storage; this is not a constant-memory promise for arbitrary quantities.

The existing **two-million expanded-pane UI safety ceiling remains in place**. The engine is tested at that ceiling. Raising it requires a separate UI/export/memory acceptance decision; this change does not claim unlimited hardware capacity.

## Full-engine scale execution

Generated workload (not customer data): width `600 + (i % 7) * 50`, height `400 + (i % 5) * 60`, per-pane rotation disabled every eleventh pane; 3660 × 2140 stock and the same 6 mm allowances. These are complete public entry-point runs, not isolated candidate timings.

| Quantity | Levels run | Result | Unplaced / comprehensive issues |
|---:|---|---|---|
| 10 | 1–5 | 1 sheet / 47.76% | 0 / 0 at every level |
| 10,000 | 1–5 | 529 sheets / 94.12% | 0 / 0 at every level |
| 1,000,001 | 1–5 | 52,098 sheets / 95.58% | 0 / 0 at every level |
| 2,000,000 | 1 | 104,190 sheets / 95.58% | 0 / 0 |

The final million-pane Level 1 run took approximately 20 seconds including the harness's verification; the two-million run took approximately 36 seconds. Some benchmark processes shared sandbox resources. These are illustrative observations, not service-level guarantees. The two-million process's sampled working set was approximately 1,710 MiB, including the compiler/host; it is not a peak-memory guarantee or a WPF UI measurement.

Existing validators still compare pairs within each sheet. Exceptionally dense tiny-pane layouts and the UI's display/export allocations require separate performance assessment. No finite heuristic guarantees the absolute best result for every possible geometry, quantity, stock supply and machine rule.

## Tests and remaining acceptance gates

- **126 selected regression cases passed, zero failed.** Existing calibration, 46-pane all-level, rotation, breakout, conservation, cancellation, progress and 10k/25k/50k partitioned cases passed.
- New tests cover exact repeat quantities, finite/multiple racks, locked pieces, incompatible colors, multiple type windows, oversized panes, small-pane subset gaps, a known fractional LP optimum and a finite-stock LP optimum, the 100,000/100,001 dispatch boundary, and cancellation.
- **14/14 portable workflow/hash checks passed.** Original protected hashes are retained with explicit owner-approved additions/changes.
- `git diff --check` passed.

Execution used the actual C#12 sources, Roslyn 5.9 and Linux .NET 10.0.11 with the external assertion adapter previously described. **This is not a claim that the native .NET 8/WPF application or real xUnit runner was executed.** Windows build, GUI behavior at large quantities, machine-specific cut/export acceptance and memory behavior on the shop PC remain gates.

Reproduce the linked real-xUnit source selection with an installed .NET 8 SDK/package access:

```sh
dotnet test Tools/EngineRegression/EngineRegression.csproj -c Release
python3 scripts/verify-optimization-port.py
```

`REPEATED_PATTERN_EVIDENCE_2026-09-21.json` records final measured metrics and explicitly separates the prior external 6,908-sheet experiment from the integrated result. No large dataset, runtime, scratch solver, binary or customer cutting list is committed.
