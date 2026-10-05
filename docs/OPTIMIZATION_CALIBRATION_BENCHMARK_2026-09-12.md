# Optimization — Calibration Benchmark Report (2026-09-12)

Companion to `OPTIMIZATION_MODULE_DESIGN_2026-09-12.md`. This report
pins the engine against the owner's REAL DGU job data before the UI
phases continue. All numbers come from `GuillotinePacker` (pure C#,
deterministic) and are locked by
`Tests/Agent/OptimizationBenchmarkTests.cs`.

## 1. Reference job (owner-supplied)

**Spec 1 — DGU** `6mm climaguard hp neutral 70 low e ft glass + 12mm
asp + 6mm clear ft glass`
- BIG group: 20 rows (1050×2127…2513×535, qty 2 except 4 singles)
- A group: 14 rows (A1…A12/A13/A14, all qty 2)

**Spec 2 — DGU** `6mm climaguard hp neutral 70 low ft glass + 12mm asp
+ 6mm obscure clear ramly ft glass`
- SMALL group: 12 rows around 725×430 (qty 1)

Groups fed to the engine:
| Group | Pieces | Glass m² | Composition |
|---|---|---|---|
| CLEAR | 68 | 98.13 | clear panes of spec 1 |
| LOW-E | 80 | 101.63 | low-e + clear + ramly-clear panes |
| RAMLY | 12 | 3.50 | obscure ramly outers |

## 2. Floor settings (owner-confirmed, 6mm glass)

- Margins LM/RM/TM/BM = **15mm** each side
- Minimum breakout = **15mm**
- Kerf = **3mm** (owner: "maybe 3 or 4" — engine is editable)
- Rotation = **enabled for all pieces by default**; the engine applies
  it ADAPTIVELY: original orientation first, 90° only when the piece
  needs it (the floor's "0 and 90 mix")
- Primary stock 3210×2250; ramly also sized against 2500×1875

## 3. Results (QUICK stage, height-first + horizontal-first split +
   adaptive rotation + consolidation)

| Group | Reference | Engine | Status |
|---|---|---|---|
| LOW-E | 17 sheets @ 82.77% | **17 sheets @ 82.77%, 0 unplaced** | ✅ exact match |
| RAMLY | 1 sheet on 2500×1875 (~76.7%) | **1 sheet on 2500×1875 @ 74.65%** | ✅ single sheet, smallest stock chosen |
| CLEAR | 16 sheets ≥ 85% | 17 sheets @ 79.92%, 0 unplaced | ⚠️ within one sheet — deep OPTIMIZE levels continue chasing it |

Key calibration findings (all enforced in code, not configuration):

1. **Blanket free-rotation is worse** (18 sheets) — laying the tall
   2130mm pieces horizontally fragments the shelves. Adaptive
   original-first rotation is the correct policy and is now the
   meaning of `RotationPolicy.Free90`.
2. Pieces 905×2536 / 838×2532 only fit rotated on 3210×2250 usable
   3180×2220 — with rotation LOCKED the engine reports them unplaced
   (never crashes).
3. The **smallest fitting stock is consumed first** — the ramly job
   lands on 2500×1875 automatically and the 3210 rack is saved.
4. Consolidation: after packing, the emptiest sheet is evacuated into
   the others (with full rollback when a piece doesn't fit).
5. OPTIMIZE stage = multi-start (orderings × split modes × effort
   level 1–5), each run consolidated; it can equal or beat QUICK,
   never regress (pinned by test).

## 4. What is verified every run

`GuillotinePacker.Verify` oracle — pinned by
`Tests/Agent/OptimizationEngineTests.cs` (8 tests) and
`Tests/Agent/OptimizationBenchmarkTests.cs` (9 tests):

- no piece overlaps (kerf gap enforced between neighbours)
- every piece inside the usable rectangle (stock − LM/RM/TM/BM)
- rotation only when policy + piece allow it
- deterministic output (same input → same layout)

## 5. Module state after this phase

- `Services/Optimization/` — models + calibrated engine + DB context
  (`optimization.db` under `%AppData%\ProGlassAutomation\`).
- `Models/Optimization/` — saved-session entity.
- `ViewModels/Optimization/` + `Views/Optimization/` — real module
  shell (JOBS / SIZES / STOCKS / MARGINS / RESULT / 2D VIEW tabs),
  reference-job loader carrying the calibration data above.
- Next phases: smart JO picker (confirmed JOs only), per-group runs,
  session save/open, Sheet Store stock booking, sheet export.
