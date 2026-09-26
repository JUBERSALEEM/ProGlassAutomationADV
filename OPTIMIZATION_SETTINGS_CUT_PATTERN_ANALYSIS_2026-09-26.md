# Optimization Settings — Strategy, Plate Pattern & Cutting-Complexity Upgrade
### Deep analysis and implementation plan · 2026-09-26 · ProGlass Automation 2.0.11

> **Question put to this analysis:** we have 5 strategies and 2 plate patterns, but a lot
> seems to be missing. What cutting-complexity levels should exist (simple first-X,
> simple first-Y, simple, 3-stage, complex with Z/W trim cuts), where should they live,
> and how many / which strategies and plate patterns do we need to compete with
> industrial ERP optimizers?

**Short answer up front:** you are right that things are missing, but not the things the
count suggests. Adding pattern *names* will not move yield by itself — today's five
"strategies" are **one algorithm at five time budgets**, and the engine has **no cut-pattern
constraint and no cut sequence at all**. The biggest real wins are (1) capturing the cut
sequence, (2) remnant/offcut reuse, and (3) making cut complexity a genuine constraint the
packer respects. Staged patterns *trade yield for table time* — they are a floor-efficiency
feature, not a yield feature. Details, evidence and a phased plan follow.

---

## 1. What we actually have today (evidence-based inventory)

### 1.1 The five "stages" are one algorithm at five budgets

`PackOptimize` (Services/Optimization/GuillotinePacker.cs:250) does **not** dispatch to five
different algorithms. It builds a multi-start portfolio from the same placement kernel and
varies only the search breadth:

| Effort | What changes | Candidate count |
|---|---|---|
| 1 | `PackQuick` only — area shelf, capacity strips, bounded pattern beam, horizontal-first splits | 1 |
| 2 | 3 orderings × 1 split + 1 smart-placement pass | ~4 |
| 3 | 4 orderings × 3 splits + smart pass | ~13 |
| 4 | 6 orderings × 3 splits × 3 deterministic shuffles + 2 smart passes | ~74 |
| 5 | 6 orderings × 3 splits × 6 shuffles + smart passes, MaxRects on every ordering | ~147 |

Orderings: `MaxSide, Area, MinSide, Height, Width, Perimeter`. Splits: `HorizontalFirst,
VerticalFirst, LongerLeftover` (GuillotinePacker.cs:295-310). Time budgets from
`OptimizationSearchGovernor`: 5 / 8 / 15 / 25 minutes for effort 1-2 / 3 / 4 / 5.

`StageStrategies` and `EffortLevels` are **two views of the same integer** — see
`SyncStageStrategyFromEffort` and `SyncEffortFromStageStrategy`
(ViewModels/Optimization/OptimizationViewModel.Margins.cs:279-330). There is no second axis
in the system. **That is the structural finding of this analysis**: *search effort and
pattern family are conflated*, so we cannot offer "3-stage cut, deep search" — only
"deep search".

### 1.2 What genuinely exists (and is good)

| Capability | Where | Status |
|---|---|---|
| Free-form guillotine placement, kerf-correct, deterministic | `GuillotinePacker.cs` | Production |
| Multi-start + deterministic shuffles + deadline governor | `PackOptimize`, `OptimizationSearchGovernor` | Production |
| Per-sheet split selection beam | `GuillotinePacker.BestSplit.cs` | Production |
| Survival-scored placement (least area condemned) | `GuillotinePacker.SmartPlacement.cs` | Production |
| MaxRects free-rectangle pass | `GuillotinePacker.Advanced.cs` | Production |
| Repeat/Master pattern industrial path (>100k parts) | `RepeatedPatterns.cs`, `RepeatMaster.cs`, `IndustrialQuantityThreshold = 100000` | Production, internal only |
| Two independent oracles (geometry/kerf + conservation) | `Verify`, `ValidateResult`, `ConservationVerified` | Production |
| House margin profiles per thickness (incl. asymmetric thick bucket) | `ThicknessBucket`, `MarginProfile` | Production |
| Kerf, min breakout 50/40, rotation Free90/Locked | `MarginProfile`, `RotationPolicy` | Production |
| Fill-first-plate post-pass, never worsens the winner | `FillFirstPlate.cs` | Production |
| Session save/restore, CSV/Excel/PDF export | `OptimizationSessionRepository`, report tab | Production |
| Calibration benchmark | DGU: Low-E 17 sheets @ 82.77 %, Clear 16 @ 84.92 %, Ramly 1 on 2500×1875 | Locked by tests |

### 1.3 What is missing — with proof

| Gap | Evidence | Impact |
|---|---|---|
| **No cut-pattern / staged constraint.** Every layout is unlimited-depth guillotine. | `SplitRemainder` (GuillotinePacker.cs:964) recurses freely; `FreeRect` has no depth or axis field. | Cannot offer "3-stage" or "first-X" at all — floor cannot be protected from complex cuts. |
| **No cut sequence.** `PlacedPiece` = `{Piece, X, Y, WidthMm, HeightMm, Rotated}`; `SheetLayout` = pieces + usable rect only. | OptimizationModels.cs:316-355 | No breakout order for the floor, no Z/W counting, no CNC file. |
| **CNC export is decorative.** The "Bottero .OPT / Lisec .DAT / Intermac .ISO / Generic DXF" pills are four static `TextBlock`s with no `Command`. | Views/Optimization/Tabs/ReportTabView.xaml:247-259 | Advertised capability does not exist. |
| **Offcuts are measured but never become stock.** Free rects narrower than `MinBreakoutMm` are condemned to trim; the OFFCUT KPI only reports them. | GuillotinePacker.cs:964, SmartPlacement.cs:397 | No remnant reuse — the single biggest yield lever in the industry. |
| **No defect avoidance.** | No defect concept anywhere in `Services/Optimization` | Cannot re-arrange around a bad sheet. |
| **Rectangular only.** No shaped/L parts, no jump cuts, no notches. | `CutPiece` is W×H | Shaped glass out of scope. |
| **Objective is physical, not economic.** Score = unplaced → sheets → utilization. | `PlatePattern.Yield` doc comment | No cost-weighted choice between a jumbo and a cheap remnant. |
| **No remake / re-optimization loop.** | — | Cannot fold a broken sheet back into the pool. |

---

## 2. Cutting complexity — the new module

### 2.1 Why Settings, not the top card (you were right)

The top card holds **run intent** (what do I want from this run). Cut complexity is **house
policy** — it depends on the cutting line, the laminate/coating, and the crew, and changes
rarely. The Settings tab already owns exactly this class of input: kerf, breakout, rotation,
margins live in the "CUT RULES & SEARCH EFFORT" card (SettingsTabView.xaml:98-155).
Cut complexity belongs in that same card, right under **MINIMUM BREAKOUT**.

### 2.2 The taxonomy — your list maps 1:1 onto industry vocabulary

Your five levels are real, named concepts in glass software:

- **GlasSave** (a UK production glass optimizer) literally advertises "will use **X cuts, Y
  cuts, Z cuts, and even W cuts** to improve yield", lets you "turn off W and Z cuts for
  cutting laminate glass", and can "cut **trim cuts** on the sides of the sheets"
  [2](https://clearthinkingsoftware.co.uk/glassave-glass-optimiser/).
- **CutGLib** ships "five levels of the cutting complexity: **2 (XY), 3 (two-stage XY),
  4 (XYZ), 5 (XYZW) and 6 (Standard)**", plus *first cut orientation* and *cut-off size
  control* [3](https://www.optimalon.com/cutting_optimization_library.htm).
- **LiSEC** sells a cycle-time upgrade specifically for tables with "a high proportion of
  **Z cuts**", by performing Z cuts together with X cuts so the operator can break them
  manually right after the Y cuts break automatically
  [2](https://www.glassonweb.com/news/lisec-laminated-glass-cutting-increase-performance-with-retrofits).
- Academically the staged families are named **2SHP / 2SGP / 3SHP / 3SGP** (two-/three-stage,
  homogeneous/general), and the literature is explicit that staged patterns exist to *trade*
  material utilization against cutting complexity — 2SHP has "the lowest material
  utilization and simplest cutting complexity", 3SGP the opposite
  [3](https://hindawi.com/journals/amse/2021/3732720).

So your five entries become:

| # | Your term | Professional name | Equivalent | Cut stages | Effect |
|---|---|---|---|---|---|
| 0 | *(today's behaviour)* | **Free guillotine** | CutGLib 6 / Standard | ∞ | Best yield, hardest to cut. **Keep as default.** |
| 1 | Simple first X cut | **2-stage, first cut X** (`SimpleFirstX`) | CutGLib 2 (XY), X-pattern | 2, first axis forced X | Strips cut first along X, then cross-cut. Predictable, fast. |
| 2 | Simple first Y cut | **2-stage, first cut Y** (`SimpleFirstY`) | CutGLib 2 (XY), Y-pattern | 2, first axis forced Y | Same, rotated — matters when the table loads long-edge-first. |
| 3 | Simple | **2-stage general (2SGP)** | CutGLib 3 (two-stage XY) | 2, axis auto per sheet | Packer chooses the better first direction per plate. |
| 4 | 3 stage | **3-stage general (3SGP)** | CutGLib 4 (XYZ) | 3 | Best yield of the staged family; more handling. |
| 5 | Complex, allow trims | **Complex with trim cuts (Z/W)** | CutGLib 5 (XYZW) + GlasSave trim cuts | ∞ + non-piece trim cuts | Highest yield; lets the engine cut a condemned area into a **reusable remnant**. |

**Honest trade-off you should decide with open eyes:** levels 1-4 will usually *cost* you a
little yield and *save* table time and breakage risk. They are not "better optimization" —
they are a production constraint. The yield upside in level 5 comes from the **trim cuts**
releasing usable remnants, not from the extra stages.

### 2.3 Data model

```csharp
public enum CutComplexity
{
    FreeGuillotine = 0,  // today's behaviour — default, zero regression
    SimpleFirstX   = 1,  // 2-stage, first cut forced X
    SimpleFirstY   = 2,  // 2-stage, first cut forced Y
    Simple         = 3,  // 2SGP — 2-stage, direction chosen per sheet
    ThreeStage     = 4,  // 3SGP — 3-stage
    ComplexTrims   = 5   // free guillotine + Z/W trim cuts allowed
}

public sealed record CutPolicy(
    CutComplexity Complexity,
    int  MaxStages,        // derived: 2 / 2 / 2 / 3 / int.MaxValue
    bool ForceFirstCutX,   // level 1
    bool ForceFirstCutY,   // level 2
    bool AllowTrimCuts,    // level 5 only — Z/W
    int  MinTrimWidthMm);  // narrowest trim worth cutting (reuse MinBreakoutMm by default)
```

Deliberately **not** added to `MarginProfile`: that type is per-thickness and is serialized
into saved sessions (`OptimizationSessionRepository`), so extending it forces a session
migration. A separate `CutPolicy`, versioned on its own, keeps old sessions openable and the
default `FreeGuillotine + MinTrimWidthMm = MinBreakoutMm` reproduces today's numbers exactly.

**Per-material override (needed before level 5 ships):** GlasSave switches Z/W cuts off for
laminate. Mirror that: `ComplexTrims` must be refused (or auto-downgraded) for laminated
stock, with the reason shown in the hint line.

### 2.4 How it actually constrains the packer

This is the engineering meat. Today placement is: *place a piece in a free rect → split the
remainder → recurse*, with an unbounded guillotine tree. To enforce staged patterns:

1. **Track depth and axis on the free rectangle.** `internal struct FreeRect` gains
   `Depth` and `LastAxis` (it is `internal`, shared only with `SmartPlacement.cs` and the
   test assembly — a contained change).
2. **Reject splits that would exceed `MaxStages`.** At commit time in `SplitRemainder`, if
   `rect.Depth + 1 > policy.MaxStages`, the candidate placement is refused and the next
   candidate is tried — the same rejection path `SmartPlacement` already uses for
   sub-breakout rects.
3. **Force the first axis** for levels 1-2: at depth 0 only the matching split is generated.
4. **Level 5 trim cuts:** instead of condemning the leftover when both remainders are below
   `MinBreakoutMm`, `CapacityStrips`-style logic already notes that "a separated offcut needs
   two cuts, not just an empty gap" (GuillotinePacker.CapacityStrips.cs:38). Extend that to
   *emit* a trim cut and register the resulting rectangle as a **remnant candidate**.

Because every rule is a rejection at a candidate boundary (never mid-placement), the existing
`Better()` contract still holds: a deeper constraint can only remove candidates, and the
`FreeGuillotine` default removes none.

### 2.5 Prerequisite: cut-sequence capture (do this first)

Z and W cuts, breakout order, and CNC files are all the same missing object: **the ordered
list of cuts per sheet**. Add it once and everything downstream unlocks:

```csharp
public sealed class CutStep
{
    public int    Seq;         // execution order on the table
    public bool   IsVertical;  // axis
    public int    AtMm;        // position along the other axis
    public int    FromX, FromY, FromW, FromH;   // rect being cut
    public bool   IsTrim;      // Z/W — releases a remnant, produces no order piece
}
// SheetLayout gains: public List<CutStep> Cuts { get; } = new();
```

Every cut is already computed — `SplitRemainder` decides both remainders for every placement.
Recording it costs one list append at the four mutation sites that already maintain the scale
metrics (constructor, `Commit`, consolidation rollback, MaxRects commit).

### 2.6 Where it goes in the UI

Settings tab → "CUT RULES & SEARCH EFFORT" inner card (SettingsTabView.xaml:95-157), under
**MINIMUM BREAKOUT**:

```
CUTTING COMPLEXITY            [ Free guillotine (default)        ▾ ]
  hint: "Every cut may be followed by another in either direction — best yield,
         most table handling."
MIN TRIM WIDTH (MM)           [ 50 ]   (enabled only for Complex with trim cuts)
  hint: "Narrowest strip worth cutting as a Z/W trim to release a usable remnant."
PER-MATERIAL: laminated stock automatically refuses trim cuts.
```

Nothing is added to the top card — that stays exactly as approved yesterday.

---

## 3. How many strategies and plate patterns, and which ones

### 3.1 Restructure: make pattern family orthogonal to effort

Two axes, not one:

- **Family** — *what kind of layout is allowed* (free guillotine, 2SGP, 3SGP, repeat-master,
  remnant-first…). This is what "strategy" should mean.
- **Effort** — *how long we search within that family*. Already exists, keep as-is.

The 350px strategy combo now fits labels like "3-stage (3SGP) — deep search" without
clipping.

### 3.2 Strategy families — what is achievable, ranked by value ÷ effort

| # | Family | Basis in our code / industry | New work | Verdict |
|---|---|---|---|---|
| 1 | **Auto** (existing) | Picks family + effort from the job profile | — | Keep |
| 2 | **Quick** (Stage 1) | `PackQuick` | — | Keep |
| 3 | **Balanced** (Stage 3) | multi-start, default | — | Keep |
| 4 | **Deep** (Stage 5) | full breadth + MaxRects | — | Keep |
| 5 | **Repeat / master pattern** | `RepeatMaster`, `RepeatInteger`, `RepeatedPatterns` already exist but are only reachable on the >100k-part industrial path. GlasSave lists "repeat cutting, mirror cutting and direct pairing" as first-class methods [2](https://clearthinkingsoftware.co.uk/glassave-glass-optimiser/) | Expose as a selectable family | **Do — cheap, already written** |
| 6 | **Remnant-first** | Needs a remnant inventory; this is LiSEC Dynopt's headline: merging optimizations and absorbing remakes, "hardly any remnant plates left", ~3 % waste reduction at Gethke Glas [1](https://www.lisec.com/solutions/software/detail/optimizations/dynopt) [4](https://www.glass-international.com/news/german-manufacturer-uses-lisec-software-tool) | Remnant stock table + reuse pass | **Do — biggest real yield win** |
| 7 | **Staged 2-cut / 3-cut** | Cut pattern constraint (§2) | Depth-limited packing | Do with §2 |
| 8 | **Cost-optimal** | `OptimizationViewModel.Costing.cs` already exists; weighted objective | Value-weighted scoring | Later |
| 9 | **Defect-aware** | LiSEC oad re-arranges the plate so the defect lands in waste, a trim, or the smallest piece [3](https://www.lisec.com/solutions/software/detail/optimizations/oad) | Defect rects + re-arrange pass | Later |
| 10 | **Shaped / mirror / pair** | Needs non-rect geometry | Large | Out of scope this year |

**Recommended list for the top-card combo (6 entries, operator-sized):**
`Auto · Quick · Balanced · Deep · Repeat / master · Remnant-first`
Staged 2-cut and 3-cut belong on the **Settings** side (they are cut complexity), not in the
run-strategy list — otherwise the operator sees the same concept twice.

### 3.3 Plate patterns — 2 → 6

| # | Pattern | Rule | Why |
|---|---|---|---|
| 1 | **Yield** (existing, default) | Fewest unplaced → fewest sheets → utilization | Historical winner |
| 2 | **Fill first plate** (existing) | Same limits, then the fuller leading sheet | Gets one plate done early |
| 3 | **Fewest cuts** | Tie-break on total cut length / Z-cut count | LiSEC reports 5-10 % cutting-time saving from cut-count work [1](https://www.lisec.com/solutions/software/detail/optimizations/dynopt) |
| 4 | **Minimise stock changes** | Group by plate size to reduce rack changes | Floor throughput |
| 5 | **Remnant-first** | Consume offcut stock before opening a jumbo | Pairs with strategy #6 |
| 6 | **Balanced spread** | Even utilization across plates instead of one very full plate | Breakage / handling risk |

All six are *scoring* changes on the result we already compute — no new packing algorithm.
That makes them cheap and, importantly, safe: each is a tie-break among plans that are
already valid.

---

## 4. Competitive gap analysis (flat-glass optimizers)

| Capability | Industry | Us | Priority |
|---|---|---|---|
| Cut-complexity levels (XY / 2-stage / XYZ / XYZW / free) | CutGLib ships 5 levels [3](https://www.optimalon.com/cutting_optimization_library.htm) | **None** | P0 |
| X / Y / Z / W cuts, trims | GlasSave; LiSEC Z-cut cycle-time upgrade [2](https://clearthinkingsoftware.co.uk/glassave-glass-optimiser/) | **None** | P0 |
| First cut orientation | CutGLib, Cut 2D X [4](https://optimalprograms.com/cut_2d_x.htm) | **None** (split mode is global per run — `BestSplit.cs:15` documents this as a known defect) | P1 |
| Ordered CNC cut list | CutGLib emits "guillotine cut list… the cuts order is crucial" [3](https://www.optimalon.com/cutting_optimization_library.htm) | **None** (pills are decorative) | P0 |
| Remnant / offcut reuse | LiSEC Dynopt, Hegla RPS remnant storage | **None** | P0 |
| Defect avoidance | LiSEC oad | **None** | P2 |
| Remake re-integration | LiSEC Remake Pool + Dynopt | **None** | P2 |
| Edge deletion (Low-E soft coat) | GlasSave "understands… edge deletion of soft coat glass" | Margins only, no coating model | P2 |
| Min breakout per glass type | GlasSave, Cut 2D X "adjustable breakage width" | Yes, per thickness bucket | Have |
| Repeat / mirror / pair cutting | GlasSave | Repeat engine exists, not exposed | P1 |
| Multi-stage, first-cut direction | Cutting Optimization Pro, Cut 2D X | **None** | P1 |
| Shaped parts | CutGLASS, LiSEC | None | P3 |
| 98 % utilization claims | LiSEC ExpertTools marketing | 82-85 % on the DGU benchmark | — |

Note on that last row: 98 % figures are marketing for *ideal* mixes. Our 82.77 % / 84.92 %
on the owner's real DGU job is a credible production number, and the gap to close is
remnants + trims, not a magic algorithm.

---

## 5. Roadmap

| Phase | Deliverable | Files touched | Risk | Guard |
|---|---|---|---|---|
| **0** | Cut-sequence capture: `CutStep`, `SheetLayout.Cuts` | `OptimizationModels.cs`, `GuillotinePacker.cs` (4 mutation sites) | Low — append-only | Existing oracle tests must stay green |
| **1** | `CutComplexity` + `CutPolicy`, depth/axis on `FreeRect`, staged rejection, Settings UI, per-material laminate rule | `OptimizationModels.cs`, `GuillotinePacker.cs`, `SmartPlacement.cs`, `SettingsTabView.xaml`, `OptimizationViewModel.Margins.cs`, session repo | Medium — touches the hot placement path | **New oracle**: every sheet's cut tree depth ≤ `MaxStages`; first axis honoured. Default `FreeGuillotine` must reproduce the DGU benchmark bit-for-bit |
| **2** | Trim cuts (Z/W) + remnant candidate registration | `GuillotinePacker.cs`, `CapacityStrips.cs`, new `RemnantCandidate` | Medium | Trim rects ≥ `MinTrimWidthMm`; conservation oracle unaffected (trims are not pieces) |
| **3** | Remnant inventory + **Remnant-first** strategy/pattern | New model + table, `OptimizationViewModel.Stocks.cs`, Run pipeline | High — changes stock selection | Conservation + no-regression benchmarks |
| **4** | Real CNC exporters: Lisec .DAT, Bottero .OPT, Intermac .ISO, DXF (make the pills live) | New `Services/Reporting/CncExport/*`, report tab | Medium | Golden-file tests per format, cut order asserted |
| **5** | Strategy family × effort orthogonalisation; 6 plate patterns | `OptimizationViewModel.Margins.cs` + `Run.cs`, top-card combo | Medium — **breaks the current stage↔effort sync** | Migration: existing sessions restore to family = Balanced, effort = stored level |
| **6** | Defect avoidance, cost objective, remake re-entry | Various | — | Later |

Phase 0 is a prerequisite for 1, 2 and 4 — three of the highest-value items are blocked on
one small change, which is why it goes first.

### Regression discipline

- The DGU calibration (Low-E 17 @ 82.77 %, Clear 16 @ 84.92 %, Ramly 1) must be **byte-identical**
  under the `FreeGuillotine` default. Any change there means the default path was altered.
- Every new constraint needs its own oracle alongside `Verify` and `ValidateResult` — the
  codebase already sets that standard, and it is why the engine is trustworthy today.
- Determinism must hold: depth tracking must not change tie-breaking for the default path.

---

## 6. Direct answers

**"How many strategies can we implement?"** Eight families are reachable with the engine we
have; six belong in the operator's list (Auto, Quick, Balanced, Deep, Repeat/master,
Remnant-first). The staged variants are cut complexity, not run strategy. Beyond that
(defect-aware, cost-optimal, shaped) is genuinely new science and should be scheduled, not
promised.

**"How many plate patterns?"** Six. All six are scoring rules over plans we already generate,
so they are low-risk and quick.

**"Do the complexity patterns go in the top card?"** No — Settings, under MINIMUM BREAKOUT,
as you said. The top card stays exactly as approved.

**The correction worth repeating:** more pattern *names* will not raise yield. Staged
patterns trade yield for table simplicity; the yield levers are remnant reuse (≈3 % at a real
LiSEC customer), trim/Z-W cuts releasing usable offcuts, and exposing the repeat-master
engine we already own on small jobs. Sequence the work that way.

---

## 7. Sources

- Guillotine cutting definitions and staged-pattern theory: [1](https://en.wikipedia.org/wiki/Guillotine_cutting)
- GlasSave — X/Y/Z/W cuts, laminate restriction, trim cuts, edge deletion, repeat/mirror/pair: [2](https://clearthinkingsoftware.co.uk/glassave-glass-optimiser/)
- CutGLib — five cutting-complexity levels (2 XY, 3 two-stage XY, 4 XYZ, 5 XYZW, 6 Standard), first cut orientation, cut-off size control, ordered CNC cut list: [3](https://www.optimalon.com/cutting_optimization_library.htm)
- Cut 2D X — multi-stage optimization with a chosen first cut direction, adjustable breakage width: [4](https://optimalprograms.com/cut_2d_x.htm)
- LiSEC Dynopt — merging optimizations, remake integration, 1-2 % waste / 5-10 % time: [1](https://www.lisec.com/solutions/software/detail/optimizations/dynopt)
- LiSEC Z-cut cycle-time handling: [2](https://www.glassonweb.com/news/lisec-laminated-glass-cutting-increase-performance-with-retrofits)
- LiSEC oad — defect avoidance by re-arranging to waste/trim/smallest piece: [3](https://www.lisec.com/solutions/software/detail/optimizations/oad)
- Gethke Glas case study — Dynopt, remake pool, Re-Opt, ~3 % waste reduction: [4](https://www.glass-international.com/news/german-manufacturer-uses-lisec-software-tool)
- Wu (2021) — 2SHP / 2SGP / 3SHP / 3SGP staged families and the utilization-vs-complexity trade-off: [3](https://hindawi.com/journals/amse/2021/3732720)
- Cutting Optimization Pro — multi-stage guillotine, edge band and trim management: [5](https://cutting-optimization-pro.soft112.com/)

## 8. Implementation status

### Phase 0 — cut-sequence capture (DONE)

- `CutAxis`, `CutStep` and `SheetLayout.Cuts` in `Services/Optimization/OptimizationModels.cs`.
- Recorded inside `SplitRemainder` — the same code that decides the free
  rectangles — so the cut list and the free-rect ledger cannot disagree.
  A placement emits at most two cuts: the first spans the host rectangle,
  the second runs inside the strip the first one created.
- Cut lists roll back with the pieces they belong to, at both rollback
  sites: consolidation (`SnapshotBeforeWrite`) and the fill-first-plate
  trial loop (`CaptureSheet` / `RestoreSheet`). `ClonePlan` copies them.
- Three paths stamp pieces straight onto a layout instead of going through
  `Commit` (the repeat-pattern stamper, the MaxRects candidates, the skyline
  placer). Their sheets are reconstructed from the finished layout by
  `TryDeriveGuillotineCuts`, which finds a straight line that splits the
  pieces without crossing one and recurses.
- **Finding that fell out of this:** that reconstruction is also a
  guillotine-validity test, and it can FAIL. MaxRects is a free-rectangle
  decomposition, so it can produce layouts no guillotine table can cut. A
  sheet like that is deliberately left with an EMPTY cut list rather than
  given invented cuts — but it means the engine has been able to emit
  uncuttable plans on a glass table. Worth a follow-up: either drop the
  MaxRects candidate, or add guillotine validity to the oracle and refuse
  such plans.
- No consumer yet: this phase only makes the data exist (unblocks
  phases 1, 2 and 4).

### Phase 1 — cutting complexity (DONE)

- `CutComplexity` (FreeGuillotine / SimpleFirstX / SimpleFirstY / Simple /
  ThreeStage / ComplexTrims) and the immutable `CutPolicy` it resolves to.
  Deliberately NOT on `MarginProfile` — that type is per-thickness and is
  serialized into saved sessions.
- `FreeRect.Depth` records which cut stage produced a rectangle; a staged
  policy refuses to keep a rectangle that has no stage left to cut it with.
  That leftover becomes waste — the documented yield cost of a staged
  pattern, and the reason `FreeGuillotine` stays the default.
- Forced first direction is applied inside `SplitRemainder`, so the ledger
  and the cut list agree on which way the sheet was cut first. A piece that
  spans the sheet's full height is treated as cut out of a single strip, so
  its separating cut is a stage-2 cut and never contradicts the pattern.
- `ValidateCutPattern` is a third oracle, beside `Verify` (geometry/kerf)
  and `ValidateResult` (conservation): it checks the CUTS, not the layout.
- Per-material rule: Z/W trims are refused on laminated stock
  (`CutPolicy.ResolveFor` + `GuillotinePacker.GroupIsLaminated`).
- Settings-tab control under MINIMUM BREAKOUT, with a hint that states the
  yield cost as well as the benefit.

### Known limits, stated rather than hidden

- Under a staged policy the alternative engines (capacity strips, best-split
  beam, smart placement, fill-first-plate, repeated patterns) are SKIPPED,
  because they build their own free rectangles and cannot honour a stage
  budget. Skipping candidates costs yield, never correctness.
- Jobs of 100,000 pieces or more take the industrial path, which cannot be
  stage-bounded; such a run returns a free-guillotine plan and says so in
  its scale notes instead of pretending.
- The complexity choice is not yet persisted with a saved session.
- `CutComplexity.ComplexTrims` currently permits trims but does not yet
  GENERATE them (no remnant extraction). Phase 2.
