# Production & Optimization Modules — Complete Analysis & Industrial Redesign

**Date:** 2026-09-12 · **Status:** Implemented (static verification only — no dotnet SDK in sandbox)
**Scope:** the eight Production stage modules and the Optimization module.

---

## 1. Executive summary

The Production module shipped as **one shared screen** (`ProductionStageView.xaml`)
hosted by eight near-identical wrappers, so Cutting and Doubling/Lamination
looked like the same page with the same 22-column grid, the same detail panel
and the same empty state. The Optimization module was functional but visually
flat — plain list boxes, text-only empty states and a bare 2D canvas with no
margin/kerf geometry, no filmstrip and no zoom, and its engine ran on the UI
thread.

Both modules are now redesigned to an industrial floor standard:

- **Production** — eight **separate, purpose-built pages**. Each stage owns
  its header, its master-grid columns and its detail sidebar; only the
  genuinely-shared *chrome* (pipeline chain bar, release bar, KPI strip,
  filter bar) is factored into small controls.
- **Optimization** — a six-tab workspace (**Jobs · Sizes · Stocks · Settings ·
  Summary · 2D View**) with a real job picker, merge groups, stock pool,
  cutting settings, an oracle-verified summary, and a proper 2D layout canvas
  (margin zone, kerf gaps, rotation legend, zoom, filmstrip). Runs execute on
  a background thread with a progress bar.

The data contract from **Step 4 (spec build) → Step 5 (sizes) → Step 6
(charges)** was read first and is preserved end-to-end; both modules remain
source-locked to **confirmed Job Orders** (Released / In Production, non-void).

---

## 2. Findings — what was broken

| # | Symptom | Root cause |
|---|---|---|
| 1 | "Same screen" — all 8 stages identical | Every `*View.xaml` was a ~4.8 KB wrapper hosting one shared `ProductionStageView`; only the header text + tint differed. The grid always rendered the full Step-5 column set and the same detail panel for every stage. |
| 2 | Empty / ugly on the floor | The empty states were long plain text; the detail sidebar was a single undifferentiated wall of cards; no stage-specific "what this station does" signal. |
| 3 | Non-professional information density | Holes/Polish/Mitring are LM- or charge-driven but still showed the full SQM/LM pricing-style envelope; Pairing/Doubling showed pane maths they don't use. |
| 4 | Optimization was a flat form | Job picker was a bare `ListBox`, empty states were bare strings, and the 2D canvas drew only piece rectangles — no margin zone, no kerf, no zoom, no filmstrip. |
| 5 | Optimize blocked the UI | `GuillotinePacker.PackOptimize` ran synchronously on the UI thread (levels 1–5), freezing the window on large runs. |
| 6 | Stage accents collided | Only 7 tint brushes existed for 8 stages (Cutting and Doubling shared blue). |

None of these were data problems — `ProductionRepository`, the piece-flow
engine and the packer are sound — they were presentation and information-
architecture problems.

---

## 3. Step 4 / 5 / 6 mapping (the contract both modules honour)

Read from `PRODUCTION_STEP4_MAPPING_ANALYSIS_2026-09-11.md`,
`STEP4_STEP6_SHEET_STORE_LINK_2026-09-10.md`,
`SHEET_DETAIL_STEP6_INTEGRATION_2026-09-06.md` and
`OPTIMIZATION_MODULE_DESIGN_2026-09-12.md`.

```
Step 4  spec build — build family (SGU / DGU / LAM / DGU+LAM),
        glass composition, pane treatments (FT / HS / HST / Annealed)
   │
Step 5  estimator grid — Sr · GlassRef · W1×H1 · W2×H2 · Qty · SQM/LM
   │     (blank W2/H2 on multi-pane ⇒ EQUAL-SIZE unit, inherits W1×H1;
   │      filled W2/H2 ⇒ OVERLAP glass)
   │
Step 6  charges — FAN_HOLES/HOLES/CUTOUT/COUNTERSINK → Holes & Cutouts;
        POLISH/POLISH_MATCHED_LAM/EDGE_GRINDING → Polish (LM);
        MITRING → Mitring (LM); ARGON/SILICON/U-INSERT/OVERLAP → assembly
        notes; wastage charges cross-check Sheet Store consumption
   │
   ▼
ProductionDistributionEngine (pure) → production.db work items (per row × pane × stage)
   │
   ▼
Optimization JOBS picker — reads the same JO's Cutting stage items, so the
   cutting plan is built from exactly what the saw will cut.
```

**Rules that drive both modules:**

- **Source lock** — only confirmed (non-draft, non-void) Job Orders; drafts
  never reach the floor or the optimizer.
- **Build → panes** — SGU = 1 pane; DGU/LAM = 2; DGU+LAM = 3. Quantities
  always follow the row (qty 20 ⇒ 20 pieces per pane).
- **W1/H1/W2/H2 semantics** — equal-size inheritance, overlap glass, stray
  W2/H2 on SGU ignored with a warning.
- **Piece flow** — `IN` (arrived) · `DONE` (floor completed) · `BALANCE`;
  a stage can never report more done than arrived (clamped write).
- **Pricing** — never shown in production or optimization.
- **Glass grouping (optimization)** — merge key = **thickness + colour** per
  pane; different glass never mixes on a sheet.

---

## 4. Production — separate, industrial pages

### 4.1 Architecture

```
MainWindow (DataTemplate per stage VM — unchanged)
   └── CuttingView / HolesCutoutsView / PolishView / EdgeDeletionView /
       MitringView / TemperingView / PairingView / DoublingLaminationView
           (8 SEPARATE pages, each with its own header, columns, detail)
           ├── Controls/ProductionPipelineBar.xaml    (shared chrome)
           ├── Controls/ProductionReleaseBar.xaml     (shared chrome)
           ├── Controls/ProductionKpiStrip.xaml       (shared chrome)
           └── Controls/ProductionFilterBar.xaml      (shared chrome)
   ProductionStagePageBase.cs  (code-behind plumbing only: Load + scroll feed)
   ProductionStageViewModelBase.cs (shared LOGIC: list/detail/KPIs/actions)
```

The old `ProductionStageView.xaml` (the shared body) was **removed**.

### 4.2 Per-stage identity (all distinct)

| Stage | Tint | Focus hero |
|---|---|---|
| ① Cutting | Blue | `CUT TO SIZE` |
| ② Holes & Cutouts | Red | `DRILL & CUTOUT` |
| ③ Polish | Purple | `EDGE POLISH & GRIND` |
| ④ Edge Deletion | Gray | `COATING DELETION` |
| ⑤ Mitring | Indigo | `CORNER MITRE` |
| ⑥ Tempering | Amber | `FURNACE CYCLES` |
| ⑦ Pairing | Green | `MATCH & PAIR` |
| ⑧ Doubling / Lamination | **Teal** (new) | `DOUBLE-SEAL & LAMINATE` |

Each stage VM exposes an `IndustryFocus` one-liner (the station's physical
job) shown in the sidebar hero, plus a stage-specific chain-context line in
the header.

### 4.3 Stage-specific master columns

- **Cutting** — full Step-5 envelope (W1/H1, W2/H2, SQM1/TOT1/LM1,
  SQM2/TOT2/LM2) + IN/DONE/BALANCE.
- **Polish / Mitring** — LM-based: drop SQM columns, keep LM1/LM2.
- **Holes & Cutouts / Edge Deletion / Tempering / Pairing / Doubling** —
  piece or unit flow: drop pricing maths entirely, keep IN/DONE/BALANCE.

The detail sidebar keeps the job header + **Open Job Order / Open PI**,
the Step 4/5 intelligence (size rule, treatments, release date), the
pane-level items with the clamped done-piece editor, stage totals
(pieces/SQM/LM) and the dispatch panel (READY / DELIVERED / TO DELIVER +
Create Delivery Note).

### 4.4 What did not change

`ProductionRepository`, `ProductionDistributionEngine`,
`ProductionReleaseService`, `ProductionDeliveryGateway`, the release flow,
piece-flow clamping and the navigation wiring in `MainViewModel` are
untouched — the redesign is purely the view layer plus two identity
properties on the VM base (`IndustryFocus`, `WorkUnitLabel`).

---

## 5. Optimization — industrial six-tab workspace

| Tab | Purpose |
|---|---|
| **JOBS** | Confirmed Job-Order picker (source lock) → loads the JO's cutting pieces; ad-hoc manual pieces; reference calibration job |
| **SIZES** | Parts merged by glass composition (thickness + colour) — one card per group |
| **STOCKS** | Stock pool (Sheet Store sizes + custom) with the smallest-fits-first rule and per-size availability |
| **SETTINGS** | Per-run cutting settings: thickness, LM/RM/TM/BM, kerf, min breakout, rotation policy, effort level |
| **SUMMARY** | KPI cards (sheets / utilization / unplaced / oracle) + per-sheet table; every row is clickable → jumps to that sheet's 2D layout |
| **2D VIEW** | Scaled sheet canvas: margin-zone rectangle (dashed), kerf gaps, piece labels, rotation legend, zoom (−/+/100%), prev/next + filmstrip |

### 5.1 Engine & run behaviour

- `GuillotinePacker` (pure, deterministic) — `PackQuick` (one fast pass) and
  `PackOptimize` (multi-start levels 1–5, best-of-N by sheets then
  utilization, consolidation).
- **Async runs** — `Run` validates on the UI thread, then executes the packer
  on a background thread (`Task.Run`); the result is applied back on the
  dispatcher. `IsRunning`/`CanRun`/`RunStageLabel` drive an indeterminate
  progress bar and gate the Run buttons.
- **Oracle** — `GuillotinePacker.Verify` still asserts no-overlap + margin +
  kerf compliance; the SUMMARY tab surfaces it as a badge.

### 5.2 2D canvas upgrades

- **Margin zone** — the usable rectangle inside LM/RM/TM/BM is drawn as a
  dashed overlay; the chrome strip prints the exact margins + kerf.
- **Rotation legend** — original orientation vs rotated 90°.
- **Zoom** — 40%–200%, with a reset-to-100% button.
- **Filmstrip** — one clickable chip per sheet, the active sheet highlighted;
  prev/next navigation retained.
- **Summary → 2D link** — clicking a sheet row opens that sheet on the canvas.

---

## 6. File change map

**Production — removed**
- `Views/Production/ProductionStageView.xaml(.cs)` — the shared body.

**Production — new**
- `Views/Production/ProductionStagePageBase.cs` — page plumbing (load + scroll).
- `Views/Production/Controls/ProductionPipelineBar.xaml(.cs)`
- `Views/Production/Controls/ProductionReleaseBar.xaml(.cs)`
- `Views/Production/Controls/ProductionKpiStrip.xaml(.cs)`
- `Views/Production/Controls/ProductionFilterBar.xaml(.cs)`

**Production — rewritten**
- `Views/Production/{Cutting,HolesCutouts,Polish,EdgeDeletion,Mitring,Tempering,Pairing,DoublingLamination}View.xaml(.cs)` — 8 separate pages.

**Production — extended**
- `ViewModels/Production/ProductionStageViewModelBase.cs` — `IndustryFocus`, `WorkUnitLabel`.
- The eight stage VMs — `IndustryFocus` override each.

**Optimization — rewritten**
- `Views/Optimization/OptimizationView.xaml` — six-tab industrial workspace.
- `ViewModels/Optimization/OptimizationViewModel.cs` — async run + progress,
  2D zoom/filmstrip/margin-zone, per-sheet selection (`SheetRowVm` notifies).

**Themes**
- `Themes/DarkTheme.xaml`, `Themes/LightTheme.xaml` — added `TintTealColor` /
  `TintTealBrush` (8th stage accent).

**Docs**
- This document; `Views/Production/README.md` updated to the new architecture.

---

## 7. Verification performed

No .NET SDK is available in this environment, so verification is **static
only** (the app builds on the user's next `dotnet build`):

- XAML well-formedness — all 8 stage pages, 4 controls, the Optimization view
  and both theme files parse clean (ElementTree).
- C# brace/paren/bracket balance — every changed `.cs` file balanced under a
  string/comment-aware tokenizer.
- Binding path audit — every `{Binding …}` in the new views resolves against
  the VM base, the list/detail row classes or the chain-chip class.
- Reference sweep — no remaining references to the removed `ProductionStageView`;
  the 8 `DataTemplate` mappings in `MainWindow.xaml` still resolve to the new
  pages (same `x:Class`).
- SDK-style project — new `.xaml`/`.cs` files are auto-included via implicit
  `Page`/`Compile` globs; no `.csproj` edit required.

## 8. Professional / industrial hardening pass (second commit)

Applied on top of the redesign, targeting real shop-floor patterns:

**Optimization**
- **Waste / offcut reporting** — SUMMARY now shows a `WASTE / OFFCUT` KPI
  (m² + % of consumed stock) and a per-sheet `Offcut` column, so the run
  reads as *utilization + waste = the sheet* — the metric a glass factory
  actually signs off on.
- **Unplaced pieces list** — pieces the engine couldn't place are listed
  under an amber banner with their size, instead of just a count.
- **Cut plan** — the SIZES tab now expands the piece list into a numbered
  **CUT PLAN** (one row per physical piece, `Ref · composition · W×H`,
  qty expanded `1/N`), capped at 500 on-screen rows with a note — the
  engine still cuts every piece.
- **Editable quantities** — the JOBS piece list quantity is inline-editable;
  the summary, merge groups and cut plan refresh on every keystroke.
- **Searchable job picker** — filter confirmed Job Orders by id / customer /
  summary (`JobSearchText` → `FilteredJobs`).
- **Composition-key merge fix** — the job picker grouped cutting pieces by
  *row ref* instead of *glass composition*, so merge groups would key on
  `G-101`-style refs rather than thickness+colour. Now grouped by spec name
  (composition) + size; the refs ride along as the label, and identical
  glass/size across refs merges into one cut row.
- **Tidy per-piece labels** — expanded pieces now carry `G-101 3/24`-style
  labels into the 2D view and cut plan (was `Label-3`).

**Production**
- **Live floor progress** — the pipeline chain bar now reads
  `PRODUCTION PIPELINE · 12 open · 45 / 57 done` (factory-wide aggregate)
  next to the stage chips.
- **Complete-all-items** — the unit detail sidebar gained a
  *Complete all items* action: finish every arrived piece of the selected
  unit in one click (each item still clamped to its inflow).
- **8th stage accent** — `TintTealBrush` added so Doubling/Lamination no
  longer shares Cutting's blue.

## 9. Carried recommendations (not implemented, by design)

- **2D PNG/PDF sheet export** — house export engine hook (design phase 5).
- **Sheet Store consumption booking** — confirming a run books `Used +=
  sheets consumed` with a history entry.
- **Persisted optimization sessions** — `optimization.db` context and
  `SavedOptimizationSession` entities already exist; the save/open/list UI is
  the next phase.
- **Cross-nav from Cutting → Optimization session** — open a saved sheet plan
  from the Cutting stage (mirrors the production chain cross-nav).
- **Per-thickness margin profiles** persisted to the MARGINS/Settings tab as
  editable rows (today: single profile per run + house defaults).
- **Runtime smoke test** on the user's machine (launch → Production → each
  stage → Optimization → run Quick/Optimize → 2D view), since the sandbox has
  no WPF runtime.
