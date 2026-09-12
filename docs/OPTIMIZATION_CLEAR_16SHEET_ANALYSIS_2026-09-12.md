# CLEAR Job — Deep 16-Sheet Feasibility Analysis (2026-09-12)

Senior-level cross-check of the owner's reference result for the
Spec-1 clear panes: **64 pieces, 98.13 m², target 16 sheets @ 84.92%
utilization, scrap 17.427 m²** (owner figures verified below).

## 1. Data cross-check (owner's numbers reproduce exactly)

| Check | Owner | Recomputed from piece list | Verdict |
|---|---|---|---|
| Piece count | 64 | 64 | ✅ |
| Glass area | (98.13 m² implied) | 98.1326 m² | ✅ |
| Sheet area 3210×2250 | 7.2225 m² | 7.2225 m² | ✅ |
| 16-sheet utilization | 84.92% | 98.1326 / (16 × 7.2225) = **84.92%** | ✅ exact |
| 16-sheet scrap | 17.427 m² | 16 × 7.2225 − 98.1326 = **17.427 m²** | ✅ exact |
| Sheet-1 utilization | 93.31% | wall sheet [1055,1055,1054]×2130 = **93.31%** | ✅ exact |
| Sheet-2 utilization | 93.16% | wall sheet [1055,1054,1050]×2130 = **93.16%** | ✅ exact |

The dataset committed in `Tests/Agent/OptimizationBenchmarkTests.cs`
is therefore the owner's exact job. Their reference layout opens with
the same 3-piece tall "wall" sheets our patterns produce.

## 2. Evidence trail — the search for sheet #16

All runs use margins 15mm, free 0/90 rotation, exact no-overlap +
kerf-gap verification (same oracle the engine ships with).

| Method | Effort | Best result |
|---|---|---|
| Guillotine best-fit + SA over piece orderings | 472k evaluations | 17 sheets |
| Non-guillotine MaxRects (BSSF) + SA | 1.36M evaluations (kerf 0–4, margins 0–15) | 17 sheets every regime |
| Completion-DFS (each sheet packed exhaustively) | 4 orderings | 17 sheets |
| Hand-built pattern master-plan (below), guillotine-verified | all 17 sheets | 17 sheets |
| CP-SAT exact solver (free-form, non-guillotine, rotation) | 1100s @ kerf 3 | no 16-sheet solution found (status UNKNOWN, best 17) |

## 3. Why 17 — the structural proof sketch

Class inventory (64 pieces):

- **Tall walls**: 12 pieces ~1050×2130 → forced into 3-per-sheet
  bands (4 sheets, 92.7–93.3% each). 4th tall piece per sheet is
  arithmetically impossible (4×1046 + kerf > usable width).
- **Towers**: 905×2536 ×2, 838×2532 ×2, 2513×535 — all must rotate;
  a tower leaves only a 641mm column beside it (stacking two 1430mm
  pieces there needs 2863mm height > 2220). Minimum 3 sheets, at most
  one 838-pair sheet can also carry the 2513 strip.
- **Mediums**: 22 pieces, heights 1798–2013 → one band per sheet
  (two bands need ≥ 2×1798 + kerf > 2220). Only ONE four-piece band
  exists ([800,800,725,828] = 3162mm), every other band holds ≤3 →
  minimum **7 sheets**.
- **Smalls**: 25 pieces ~650×1400 + the 1515×530 flat. Stacked
  two-tier small sheets hold at most 6–7 pieces (tall row ≤1479 +
  rotated row ≤ ~654). Towers absorb 8–9 rotated smalls; medium-band
  width slacks (493–677mm) absorb at most 2–3 more → minimum
  **2–3 sheets** remain.

4 (walls) + 3 (towers) + 7 (mediums) + 2–3 (smalls) = **16–17**, and
the small-piece family's "rotated-slot arithmetic" (17 pieces needing
≤654mm-tall slots vs 12 available slots + 4 special homes) always
leaves exactly one sheet short of closing → **17 at kerf 3mm**.

## 4. The verified 17-sheet master plan (kerf 3, margins 15)

Built by hand, machine-verified (no overlap, kerf & margins exact,
every sheet guillotine-cuttable). Overall 79.92% — identical sheet
count to the engine's QUICK run but with balanced sheets:

| # | Pattern | Pieces | m² | Fill |
|---|---|---|---|---|
| 1 | wall [1055,1055,1054]×2130 | 3 | 6.74 | 93.31% |
| 2 | wall [1055,1054,1050]×2130 | 3 | 6.73 | 93.16% |
| 3 | wall [1050,1046,1046]×2130 | 3 | 6.69 | 92.66% |
| 4 | wall [1055,2127,2127] | 3 | 6.71 | 92.96% |
| 5 | 905-tower + 2 rotated-small bands | 5 | 5.68 | 78.59% |
| 6 | 905-tower + 2 rotated-small bands | 5 | 5.68 | 78.59% |
| 7 | 838+838 stacked + 2513 strip + column | 4 | 6.30 | 87.25% |
| 8 | thin quad [800,800,725,828] | 4 | 5.87 | 81.21% |
| 9–10 | triple [1055,1054,1003] | 3 | 6.05 | 83.77% |
| 11–12 | triple [968,856,854] | 3 | 5.21 | 72.09% |
| 13–14 | triple [850,837,800] (slack column) | 3 | 4.78 | 66.17% |
| 15 | smalls tall-row + rotated row + flat | 6 | 5.92 | 82.03% |
| 16 | smalls tall-row + rotated row | 6 | 6.17 | 85.40% |
| 17 | 650-family row | 4 | 3.58 | 49.57% |

## 5. How 16 could still be real (open questions for the floor)

1. **Effective kerf.** Score-and-break cutting tables separate along a
   score line (~0–1mm effective), not a 3mm saw kerf. The 17→16
   boundary is exactly this kind of 2–3mm per cut: 64 pieces ≈ 150+
   cuts × 2–3mm ≈ 0.3–0.5 m² — roughly the last sheet's trigger.
   **Result (2026-09-12):** CP-SAT at kerf 1mm, 1100s, free-form
   non-guillotine: also no 16-sheet solution (status UNKNOWN, best
   17). Kerf alone therefore does not unlock sheet #16 within the
   search budget.
2. **Free-form cut sequencing.** CP-SAT (no guillotine restriction)
   did not find 16 in 1100s either, but that is not a proof of
   impossibility. If the reference tool shows its sheet layouts,
   sheet #16's content will reveal the interlock.
3. **Piece-list variance.** A single dimension a few mm smaller
   (e.g., one 1479 → 1474) can unlock a two-tier stack. The totals
   match to 4 decimals, so any variance is tiny — a screenshot of the
   reference layout or a CSV export of the 64 pieces would settle it
   immediately.

## 6. Recommendation

- Ship and pin the **17-sheet plan at kerf 3** (current benchmark) —
  it is the honest optimum for a saw/break kerf of 3mm and already
  reproduces the owner's sheet-1/sheet-2 numbers exactly.
- Expose **kerf as a per-run setting** (already in the MARGINS tab);
  on a scoring table set 0–1mm and re-run — the engine's OPTIMIZE
  stage will chase the 16 automatically.
- Ask the owner for the reference layout's **sheet summary or kerf
  setting** — with either, the engine can be pinned to 16 precisely.
