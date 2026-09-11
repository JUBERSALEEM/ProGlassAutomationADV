# Glass Cutting Optimization Module — Deep Analysis & Implementation Plan

**Date:** 2026-09-12 · **Status:** Approved for phased implementation
**Scope:** new top-level module `Optimization` — sheet optimization, stock
utilization, smart job merging, 2D sheet preview.

---

## 1. What the module must do (owner requirements)

1. **Optimize pieces onto stock sheets** and report **sheets used** and
   **utilization %** per sheet and per run.
2. **Smart job picker** — import pieces (Glass Ref, W1, H1, W2, H2, Qty…)
   from PI / Job Orders; merge pieces intelligently by extracting the
   **color/glass from the spec**, per pane position — e.g. a DGU job's
   Spec 1 *HD Grey* outer and an SGU job's *HD Grey* single can be
   optimized **together or separately** via smart grouping.
3. **Multi-stock** — run against several stock sizes (3210×2250,
   3660×2440, any others) **together or separately**.
4. **Two-stage engine** — a **Quick** result (fast heuristic) and a
   deeper **Optimize** result with an **effort-levels bar**; the run
   shows progress while optimizing.
5. **Rotation policy** — per run: no rotation / free 90° rotation.
6. **2D canvas preview** — see exactly how each sheet is utilized:
   pieces placed, labels, margins, kerf, offcuts.
7. **Smart placement** — deep packing, **no overlaps ever**, proper
   margins: **LM / RM / TM / BM** (edge trims), **kerf**, and
   **minimum breakout**, all **per thickness** (different mm margins
   for different glass thicknesses).
8. Multi-tab UI: **Jobs · Sizes (pieces) · Stocks · Result · 2D View**.

## 2. What already exists in the app (senior audit)

| Asset | Where | What it gives the module |
|---|---|---|
| License slot | `Services/Licensing/LicenseGate.cs` — `Optimization` mapped behind `Cutting.*` | module key already reserved — no license work |
| Sheet Store | `Models/SheetStore/GlassSheet.cs`, `Services/SheetStore/` | **real stock**: Category + Thickness + **Color** + W×H + Supplier, Stock/Used counts, price/SQM. Stock sizes like 3210×2250 / 3660×2440 live here |
| Per-pane sheet matching | `Models/Specifications/SpecPane.cs` | the Spec Center already pairs a pane (Outer/Inner, color, thickness) with its Sheet Store size — the merge-by-color concept exists at spec level |
| Spec grammar | `ViewModels/ProformaInvoice/Step9ProfitLoss/PnLSpecParser.cs` | parses **thickness + color + treatment** per pane token (`6mm HD Grey FT Glass` → 6 / HD Grey / FT) |
| Cut pieces | `production.db` Cutting stage items | pane-by-pane pieces per released JO (JobRef, GlassRef, Position, W, H, Qty, Treatment, SpecName) — ready-made import source |
| Standard sizes | `Agent/Knowledge/AgentKnowledgeService.cs` | house stock canon: 2440×1830 · 3210×2250 · 3660×2440 |
| Module patterns | Production modules, DN workspace | tab styles (`ModuleTabStyles`), master–detail, chain bars, async VM loading |

**Conclusion:** nothing external is needed. Pieces come from production
(or a PI snapshot on demand), stock comes from Sheet Store, colors come
from the existing spec grammar, the license key exists.

## 3. Module architecture

```
MainWindow sidebar → Optimization (icon tile header like Production)
└── OptimizationView (module tabs)
    ├── 1. JOBS      smart job picker: released JOs → rows → merge groups
    ├── 2. SIZES     cut-piece table (ref, color, thickness, W×H, qty,
    │                rotate flag) — editable quantities, remove/add
    ├── 3. STOCKS    stock pool: Sheet Store balances + custom sizes,
    │                per-run selection, together/separate modes
    ├── 4. MARGINS   margin/kerf profiles per thickness (LM/RM/TM/BM,
    │                kerf, min breakout) — defaults editable
    ├── 5. RESULT    run summary + per-sheet table (stock size, pcs,
    │                used SQM, utilization %, offcut)
    └── 6. 2D VIEW   canvas preview of every sheet
```

Run bar: **[ QUICK ]** · **[ OPTIMIZE ]** with an **effort-level bar**
(levels 1–5) and progress; results persist as a saved session
(`optimization.db`, house DB pattern, migration-guarded schema).

## 4. Data model (new `optimization.db`)

| Table | Key fields |
|---|---|
| `OptimizationSessions` | Id, Name, CreatedAt, RotationPolicy, EffortLevel, Mode (MixedStock/PerStock), TotalsJson |
| `OptimizationSessionPieces` | SessionId, JobRef, SpecGroupNumber, SerialNumber, Position, GlassRef, Color, ThicknessMm, W, H, Qty, AllowRotate |
| `OptimizationStocks` | SessionId, Source (SheetStoreId or Custom), WidthMm, HeightMm, ThicknessMm, Color, MaxSheets |
| `OptimizationSheets` | SessionId, SheetNo, StockW, StockH, StockRef, PiecesPlaced, UsedSqm, UtilizationPct |
| `OptimizationPlacements` | SheetId, PieceRef, X, Y, W, H, Rotated, JobRef, GlassRef |
| `OptimizationMargins` | ThicknessMm, LM, RM, TM, BM, Kerf, MinBreakout |

Margins are **per thickness**: e.g. 6mm stock trims 25mm/side with a
3mm kerf, 12mm trims 30mm with 4mm kerf — the profile table drives the
packer, defaults editable in the MARGINS tab.

## 5. Piece import & smart merge (the job picker)

1. **Source:** Released / In Production JOs (same source lock as
   production). Pieces = the JO's **Cutting stage items** (pane level:
   outer and inner are separate pieces — a DGU row contributes both
   panes, exactly what the saw cuts). Fallback: distribute the embedded
   PI snapshot on demand (no release needed for planning runs).
2. **Color & thickness per pane:** `PnLSpecParser.ResolveAllPanes`
   resolves the spec's panes in order; paired with the pane position
   using the same occurrence rule as production (Outer = first pane,
   Inner = last, Lam pair = middles). Sheet Store's `SpecPane` match is
   reused when present.
3. **Merge groups:** key = **Thickness + Color** (+ Category when the
   Sheet Store separates it). The picker shows groups across jobs:
   `6mm HD Grey — 2 jobs, 4 rows, 84 pcs` — the user optimizes a group
   together, or splits back to job level with one click. Outer/inner
   with the same color land in the same group automatically; different
   colors never mix on a sheet.
4. **Qty expansion:** piece rows expand into individual rectangles at
   run time (qty 24 → 24 rectangles), identity kept for labelling
   (`G-101 · 3/24`).

## 6. Optimization engine

### 6.1 Geometry & guarantees
- Integer-mm coordinates; a **placement oracle** in tests asserts
  pairwise non-overlap + margin + kerf compliance for every run.
- **Margins:** LM/RM/TM/BM shrink the usable sheet rectangle before
  packing; **kerf** is added between adjacent cuts; **minimum
  breakout:** any resulting offcut strip narrower than the profile's
  MinBreakout is absorbed into waste (never emitted as a phantom
  usable sliver).
- **Rotation policy:** `Locked` (as entered) or `Free90` — the packer
  tries both orientations where the profile allows.

### 6.2 Stage 1 — QUICK (seconds)
- Sort pieces by descending height, then area (FFD-shelf).
- **Guillotine shelf packing** on the usable rectangle: shelves along
  the long axis, pieces placed left→right, shelf height = tallest
  piece in the shelf; guillotine-cuts only (every split is a full
  straight cut — matches real cutting tables).
- Stock selection: first stock size (by pool order) that fits the
  piece; one pass, deterministic. Target: < 1 s for 500 pieces.

### 6.3 Stage 2 — OPTIMIZE (levels bar 1–5)
The level sets the iteration budget; the UI shows a progress bar while
the engine runs (async, cancellable):
1. **Multi-start orderings:** FFD by height / by area / by perimeter,
   BFD variants, original-job-order tiebreaks.
2. **Split heuristics:** for each free rectangle, split along the axis
   leaving the larger remainder (min-waste) vs shorter-leftover — both
   tried per start.
3. **Rotation trials** (when policy allows) at placement time.
4. **Consolidation pass:** after packing, attempt to move pieces off
   the least-utilized sheet onto the others; drop emptied sheets.
5. **Best-of-N selection** by (utilization desc, sheets asc,
   same-size-piece adjacency bonus) — deterministic seed, reproducible.
Levels: L1 = quick + 2 starts · L2 = +all orderings · L3 = +split
variants · L4 = +rotation sweep · L5 = +consolidation retries.

### 6.4 Multi-stock
- **Together (mixed pool):** every sheet is cut from whichever pool
  stock wastes least for the remaining pieces (stock chosen per sheet).
- **Separately:** one run per stock size; results shown side by side.
- Sheet Store balances cap the available sheet count per stock
  (optional: allow "planning beyond stock" with a flag).

### 6.5 Output metrics
Per sheet: pieces, used SQM, **utilization %**, offcut SQM, offcut
bands (usable offcut vs trim). Per run: **sheets used**, total used /
waste SQM, weighted utilization, per-stock breakdown.

## 7. 2D canvas preview

- One sheet per card, scaled to fit (WPF `Canvas`; pieces as
  rectangles with 1px kerf gaps).
- Piece fill color = source job (tint palette), label = GlassRef;
  tooltip = ref · size · job · spec · position · rotated.
- Sheet chrome: margin zones hatched, kerf lines, utilization badge,
  sheet stock label (`3210×2250 · 6mm HD Grey`).
- Sheet navigator (prev/next + film strip), zoom-to-fit; PNG export in
  a later phase (house export engine).

## 8. Smart placement rules baked into the engine

- Same-glass adjacency: orderings group identical W×H pieces so the
  saw repeats cuts (fewer measurements, cleaner breakout).
- Full-width shelves preferred when a piece spans ≥ 80% usable width.
- Left-bottom bias keeps the working corner tidy (offcuts collect on
  the far side of the table).
- Rotation chooses the orientation with the smaller shelf-height cost.
- Never split a piece; never place across margin/kerf; the test oracle
  enforces this on every engine change.

## 9. Integration with the rest of the app

- **Cutting module link:** a saved session can be opened from the
  Cutting stage (cross-nav like the production chain) so the floor
  sees the sheet plan behind the cut pieces.
- **Sheet Store consumption:** confirming a run optionally books the
  sheets (`Used += sheets consumed`) with a history entry — inventory
  stays truthful.
- **Production:** pieces already carry JobRef/Spec/Serial, so a run
  can be traced back to the JO and its delivery-note chain.

## 10. Implementation phases

| Phase | Deliverable |
|---|---|
| **0** | Scaffold: models + `optimization.db` context/migrations + module shell (header, tabs, sidebar entry, license gate) + design doc wiring |
| **1** | Smart job picker: JO → cutting pieces → color/thickness groups (cross-job merge), Sizes tab |
| **2** | Margins/kerf profiles per thickness + **Quick engine** (guillotine shelves, rotation policy, multi-stock) + RESULT tab |
| **3** | **Optimize engine** levels 1–5 with progress bar + consolidation + mixed/per-stock modes |
| **4** | **2D canvas preview** + session save/open/list |
| **5** | Integration: Sheet Store stock booking, Cutting-module cross-nav, PNG/PDF sheet export |

## 11. Rules & guardrails

- No competitor naming anywhere in UI, code, or docs.
- Deterministic runs (seeded), invariant-culture formatting, all money
  math via house rounding rules.
- Engine stays pure (`Services/Optimization/OptimizationEngine`) —
  testable without WPF/EF, pinned by oracle tests (no-overlap,
  margins, kerf, min-breakout, utilization on known piece sets).
- Source lock mirrors production: pieces trace to confirmed Job Orders
  (planning from PI snapshots is allowed but always JO-labelled).
