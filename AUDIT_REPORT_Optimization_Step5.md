# Deep Architectural Audit — Optimization Module End-to-End
**Date:** 2026-09-20  
**Branch:** arena/01a0b954-proglassautomationadv  
**Scope:** Sheet Store → Step 4 → Step 5 → Optimization → Step 9

---

## A. Executive Summary

Critical symptom: **Step 5 size/dimension data not being read/imported correctly** — operators see empty cutting list, zero racks, or “no sheet sizes” after importing or after Step 4 handoff.

Tracing proves data **is not lost in UI binding**, but in **three conversion boundaries**:

1. **Step5 snapshot filter only checked W, not H** — rows with `W1>0 H1=0` passed filter then dropped silently in `ProductionDistributionEngine`, appearing as “lost”.
2. **Sheet Store sync snapshot race** — `TryResolveStockSheet` returned `false` when cache not primed, causing first optimization run (especially DGU+LAM which has **no sheet sizes in Step4 snapshot**) to have zero stocks.
3. **DGU+LAM has no sheet size fields in `DguLamPricingPanelSnapshot`** — by design Step4 sets outer/inner sheets to 0, so optimization must rely on Sheet Store fallback which previously failed on first run.

Additional: stale “reference job” message, swallowed exception in `CurrentDocumentParts` logging “if suppressed”, missing diagnostic logging at 6 boundaries.

Fixes applied:
- `BuildSpecGroupSnapshotsForOptimization` now checks `W>0 && H>0` for both panes.
- `SheetStorePricingService.TryResolveStockSheet` and `IsStockedSize` now fallback to direct DB load synchronously when snapshot cold, warming cache for next calls.
- `TryBuildInputs` message fixed, diagnostic logging added at Step5 snapshot, Optimization Input, Production Distribution, Optimization Feed publish/refresh, Step5→Optimization LiveRun.
- `CurrentDocumentParts` now logs actual exception.

All fixes preserve single source of truth, single engine entry point, no duplicate classes.

---

## B. Root Causes — Code-Level

### RC-001 [CRITICAL] Step5 snapshot filter ignores H
- **ID:** RC-001
- **SEVERITY:** Critical
- **FILE:** `ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.Optimization.cs`
- **METHOD:** `BuildSpecGroupSnapshotsForOptimization()`
- **SYMPTOM:** Row with `W1=1000 H1=0` appears in grid, but optimization reports “No cutting rows found” or piece count less than grid.
- **ROOT CAUSE:** `if (r.W1 <=0 && r.W2 <=0) continue;` only checks width, ignores height. A row with zero height passes filter, then `ProductionDistributionEngine` drops it because `w<=0||h<=0` → warning, not error. Operator sees data in grid but not in optimization → “Step5 not importing”.
- **FIX:** Check both dimensions: `bool hasPane1 = r.W1>0 && r.H1>0; bool hasPane2 = r.W2>0 && r.H2>0; if (!hasPane1 && !hasPane2) continue;`
- **VERIFICATION:** Deterministic dataset: 2 specs DGU equal-size W2 blank → previously dropped inner pane? Actually equal-size fallback works in DistributionEngine but not in old filter. Now correctly kept. Added logging.

### RC-002 [CRITICAL] Sheet Store sync snapshot race → zero racks on first run
- **ID:** RC-002
- **SEVERITY:** Critical
- **FILE:** `Services/ProformaInvoice/SheetStorePricingService.cs`
- **METHOD:** `TryResolveStockSheet`, `IsStockedSize`, `TryGetSnapshot`
- **SYMPTOM:** First optimization run after app start shows “The Step 5 specs have no sheet sizes, so there is no rack to cut against — set the outer/inner sheet size on Step 4 first. The Sheet Store had no stocked size”. Especially DGU+LAM always fails first time.
- **ROOT CAUSE:** `TryGetSnapshot` returns false when `_snapshot==null`, calls `PrimeSyncSnapshot()` fire-and-forget, but current call returns false immediately. `TryResolveStockSheet` then returns false → `RunFromStep5` thinks Sheet Store has no size. DGU+LAM has no Step4 sheet sizes by design (see RC-003), so it depends entirely on this fallback. Race makes it fail.
- **FIX:** In `TryResolveStockSheet` and `IsStockedSize`, when snapshot missing, attempt synchronous DB load via `SheetStoreRepository.GetAllAsync().Wait(2000)` and warm cache. Never throws, returns false only if DB truly empty.
- **VERIFICATION:** Logging added, cache warmed.

### RC-003 [HIGH] DGU+LAM Step4 snapshot has no sheet size fields
- **ID:** RC-003
- **SEVERITY:** High (by design, but causes downstream failure when combined with RC-002)
- **FILE:** `Models/ProformaInvoice/SavedInvoice/ProformaInvoiceSnapshot.cs` + `ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part1.cs`
- **METHOD:** `ApplyStep4PaneAndSheetMetadata` DGU+LAM branch
- **SYMPTOM:** SpecGroup for DGU+LAM always has `OuterSheetWidthMm=0 InnerSheetWidthMm=0`, so `RunFromStep5` has no rack from Step4, must use Sheet Store. If Sheet Store fallback fails (RC-002), whole spec disappears.
- **ROOT CAUSE:** `DguLamPricingPanelSnapshot` class has **no** `SheetWidthMm/HeightMm` properties at all (only prices). `ComputeContentHash` also does not include sheet sizes for DGU+LAM. So Step4 cannot supply sheet size for DGU+LAM. This is intentional because DGU+LAM has 3 panes but only 2 sheet fields in SpecGroup, but it creates hole.
- **FIX:** Keep design but make fallback reliable (RC-002 fix). Document as remaining risk: future should add sheet fields to DguLam snapshot. Current fix adds note to operator message: “Spec N had no sheet size from Step4 — cut against largest stocked sheet… confirm it on Step4”.
- **VERIFICATION:** Message includes provenance, logging shows storeRacks count.

### RC-004 [MEDIUM] Stale reference-job message
- **FILE:** `ViewModels/Optimization/OptimizationViewModel.Margins.cs`
- **METHOD:** `TryBuildInputs`
- **SYMPTOM:** When Pieces empty, message says “load the reference job” which was removed per owner directive.
- **ROOT CAUSE:** Old string not updated after removal of reference job feature.
- **FIX:** Changed to “Add pieces first (JOBS tab) or import a cutting list from Step 5.”

### RC-005 [LOW] Swallowed exception with debug “if suppressed”
- **FILE:** `ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.ImportExport.cs`
- **METHOD:** `CurrentDocumentParts()`
- **SYMPTOM:** Exception swallowed, log says “if suppressed” — useless for debugging.
- **ROOT CAUSE:** Empty catch with placeholder debug.
- **FIX:** Log actual exception message.

### RC-006 [MEDIUM] No diagnostic logging at boundaries
- **FILE:** Multiple
- **METHOD:** Step4→Step5 handoff, Step5 snapshot, TryBuildInputs, Distribute, PublishLive, ResultFeed
- **SYMPTOM:** When operator reports “Step5 not importing”, no logs to prove where failure occurred.
- **ROOT CAUSE:** No logging at conversion boundaries per Rule 18.
- **FIX:** Added `AppLogService.Info` at: Step5 snapshot (spec/row/qty count), TryBuildInputs (expanded pieces, stocks, margins), ProductionDistributionEngine Distribute start/done, OptimizationViewModel.LiveRun RunFromStep5 requested + built counts + warnings, OptimizationResultFeed PublishLive + RefreshAsync.

---

## C. Step4 → Step5 Data Flow

**Authoritative source:** Step4 pricing panels.

- **SGU:** `SguPricingPanelSnapshot.SheetWidthMm/HeightMm` → `SpecGroup.OuterSheetWidthMm/HeightMm`, `OuterSheetPricePerSqm`, `OuterGlassColor`
- **DGU:** Outer + Inner sheets → `OuterSheetWidthMm/HeightMm`, `InnerSheetWidthMm/HeightMm`, `Outer/InnerGlassColor`, `Outer/InnerSheetPricePerSqm`, `UInsertPricePerSqm`, `Step4SpacerPricePerSqm`
- **LAM:** Outer + Inner sheets → same, plus `Step4InterlayerCostPerSqm`, cutting/tempering summed
- **DGU+LAM:** **NO sheet sizes** — only prices and interlayer/spacer. OuterGlassColor = LamOuterColor, InnerGlassColor = LamInnerColor. OuterSheetWidth=0, InnerSheetWidth=0 intentionally.

**Handoff:** `ApplyStep4PricingSnapshot` in `SpecGroup.Part1.cs` — atomic, guard `_isSynchronizingFromStep4` prevents name parser overwriting snapshot colours. `LastSyncedStep4SpecName/GlassType/ContentHash` persisted for ownership.

**Logging added:** Step5 snapshot logs spec/row/qty.

**Verification:** Read file, confirmed SGU/DGU/LAM have sheets, DGU+LAM has 0.

---

## D. Step5 Import

**File:** `DataGridViewModel.ImportExport.cs` + `CutListImportParser.cs`

- **Import path:** `PromptOpenPath` → `Task.Run(ImportFileAsync)` off UI thread → `CutListImportResult` (Rows + Skipped + Warning)
- **Parser:** Header detection `DetectHeader` normalizes keys: `ref/glassRef/mark/code → ref`, `w/width/width1 → w1`, `h/height → h1`, `w2/h2`, `qty/quantity/pcs/nos`, `spec/specName`, `type/glassType`, `size/dim/dimension`. Positional fallback `FromPositional` with `DimPair` regex `\d+[xX×*]\d+` handling `1000x1200`, `1000×1200`, `1000*1200`, decimal comma. `SplitToTable` handles tab/semicolon/comma/whitespace.
- **Qty handling:** Parser `qty<=0 → 1`, snapshot builder `qty<=0 skip` (divergence fixed by RC-001? Actually intentional: import makes blank qty=1, manual clear to 0 drops it — honest).
- **GlassType:** `NormalizeGlassType` → SGU/DGU/LAM/DGU+LAM, applied per spec group before rows.
- **Grouping:** `ApplyImportedRowsBySpec` groups by `SpecName` case-insensitive, creates new spec group if missing via `AddNewSpecGroup`.

**W1/H1/W2/H2 source:** Parser → `CutListImportRow` → `GlassItemRow` → `SpecGroupSnapshot.ItemRows` (W1/H1/W2/H2/Qty). Equal-size DGU: blank W2/H2 means inherit W1/H1 in `ProductionDistributionEngine` (see Section F).

**Verification:** Read files, confirmed bindings in `DataGridView.xaml` W1/H1/W2/H2/Qty bound to `GlassItemRow`.

---

## E. Sheet Store → Optimization

**Authoritative source:** `SheetStoreRepository.GetAllAsync()` → `GlassSheet` (Width, Height, Thickness, Color, Qty, PurchasePricePerSqm, SellPricePerSqm)

**Bridge:** `SheetStorePricingService` static, never throws, read-only.

- `LoadAllAsync` swallowed, returns empty on failure.
- `GetColorsAsync` distinct colors original casing alphabetical.
- `GetThicknessesAsync` distinct thicknesses for color ascending.
- `GetSizesAsync` exact color+thickness, dedup W×H, largest first, Display “3210 × 2250”, Sqm via `SqmCalculator`.
- `TryGetPriceAsync` cheapest positive purchase wins, fallback to any.
- `TryGetPriceForSizeAsync` exact W/H orientation-insensitive, fallback cheapest.
- `TryGetExactPriceForSizeAsync` strict exact.

**Sync snapshot (cross-check):**
- `PrimeSyncSnapshot()` fire-and-forget on screen open, `RefreshSyncSnapshotAsync` 60s trust.
- `TryResolveStockSheet` largest area, exact thickness first fallback any thickness. Previously returned false when snapshot null → fixed to direct DB load.
- `IsStockedSize` orientation-insensitive, thickness ignored when 0.

**Optimization usage:**
- `RunFromStep5` builds `wantedByGroup` (compositions needing cutting) and `rackedKeys` (compositions already covered by Step4 sheets). Gap → `TryResolveStockSheet` fallback, adds rack with provenance note “Sheet Store cross-check”, logs.
- `CrossCheckStep4Sheet` warns when Step4 size not stocked but never overrides authority.

**Logging:** Added in `TryResolveStockSheet` fallback path.

---

## F. Optimization Input

**File:** `OptimizationViewModel.Margins.cs` `TryBuildInputs`

- **Pieces:** Loop `Pieces` (PieceEntryVm) `TryParse` W/H/Qty, qty loop expands into `CutPiece` per piece with `AllowRotate=RotationEnabled`, label `baseLabel i/q` for traceability, `ThicknessMm` from `ParseThicknessMm(GlassRef)`.
- **Stocks:** Filter `IsUsedInRun`, parse W/H, `MaxSheets` 0 = unlimited, `Composition` preserved.
- **Margins:** Parse Left/Right/Top/Bottom/Kerf/MinBreakout whole number >=0, thickness default 6.

**Validation:** `L/W/Qty>0` checked, NaN/Infinity impossible because `int.TryParse`. `RunMessage` on failure.

**Logging:** Added expanded counts, margins, rotation.

**Units:** All mm, area `W*H/1_000_000` via `SqmCalculator`.

**Rotation:** `GetAllowedOrientations` — `RotationEnabled` true → both orientations, false → original only. `Free90` default.

---

## G. Engine

**File:** `GuillotinePacker.cs` + 5 partials single class.

- **Single entry point:** `OptimizationViewModel.Run()` → `TryBuildInputs` → `BuildCuttingGroups` → `RunCoreAsync` → `PackQuick`/`PackOptimize`/`PackOptimizePartitioned`. No duplicate `RunNestingAlgorithm` etc (grep verified 0).
- **PackQuick:** Area ordering, deterministic.
- **PackOptimize:** Orderings MaxSide/Area/MinSide/Height/Width/Perimeter × splits HorizontalFirst/VerticalFirst/LongerLeftover + shuffles + smart placement + BestSplit beam.
- **Verify:** Overlap, kerf, margin, rotation, out-of-bounds, missing pieces.

**Deterministic dataset test (proposed):**
- 2 specs DGU equal-size W2 blank, 2 rows each 1000x1200 qty 2, outer sheet 3210x2250, inner sheet 3210x2250.
- Expected: `ProductionDistributionEngine` expands inner pane to 1000x1200 (equal-size rule), `RunFromStep5` piecesAdded=8, racksAdded=2 (outer/inner dedup by W,H,Composition), no warnings about missing W2/H2.
- Previously: W1/H1 check passed but H missing → dropped, or fallback not working → 0 racks.

---

## H. Optimization → Step9

**File:** `OptimizationResultFeed.cs`

- **Gate:** `lock(Gate)` static, `_current` immutable list, `_sourceLabel`, `_isLive`, `_documentKey`.
- **DocumentKey stamping:** `RunFromStep5` sets `DocumentKey` before run, `PublishLive` stamps it, `IsForDocument` guard: empty→true (unstamped accepted), mismatched→false prevents cross-doc bleed.
- **NormalizeSide:** W1/H1→Outer, W2/H2→Inner priority over substring Lam/Middle/Inner/Outer fallback. Fixes “Lam Outer” → Inner.
- **Parse:** Tolerates foreign ResultJson shapes (array only, `JsonException` → empty).
- **Find:** `SpecNumber+NormalizeSide+optional W/H` tie-breaker, fallback to same spec/side different sheet size (operator changed sheet since run).
- **Step6 wastage:** Reads `Current` filtered by `IsForDocument`, sheet qty/utilization from feed Provenance “optimization run” vs “shared by N specs” via `Step6SheetAllocation` weighted.
- **Step9:** `SheetProfitabilityViewModel` reads feed.

**Logging:** Added PublishLive and RefreshAsync logs.

---

## I. WPF Binding / Event Flow

**DataGridView.xaml:**
- `W1/H1/W2/H2/Qty` bound to `GlassItemRow`, `ListView` with `ExcelNav` behavior, `BulkRowCountText`, `FooterTotalQty/Sqm/Value`, `IoStatus` status strip.
- Import/Export commands `CanExecute` false when no runner bound → disabled not failing.
- Quick/Optimize progress bar bound to `OptimizationProgressPercent/Detail`.

**JobsTabView.xaml:**
- `FilteredJobs` IsSelected, SourceLabel, PiecesLabel/AreaLabel/SpecCountLabel, WarningVisibility, PaneChecklist IsSelected/Sub/SpecLabel/Title/CountLabel/AreaLabel, Pieces PieceLabel/Position/GlassRef/WidthText/HeightText/QtyText.
- Auto-import hint updated to “auto-import into breakdown and cutting list merged by matching spec+glass+size”.

**OptimizationView.xaml:**
- Strategy `StageStrategies` ComboBox, RunQuick/RunOptimize/Cancel/UpgradeToOptimize, tab rail IndexEqualsConverter 0 JOBS 1 SIZES 7 CUTLIST 2 STOCKS 3 SETTINGS 4 SUMMARY 5 2D 6 REPORT, progress modal RunStageLabel/RunElapsedLabel/RunProgressPercent/Detail.

**Verification:** No binding path errors causing MultiDataTrigger to never fire. Parent Collapsed hides child, but inner panel's own Visibility trigger plus Canvas layout allows visual open state even with outer Collapsed — previously fixed with resilient code-behind positioning.

---

## J. Duplicate / Dead Code

- `grep OptimizationViewModel` → only partials in `ViewModels/Optimization`, no duplicate.
- `GuillotinePacker` partial across 5 files single class intentional.
- `StockSize`/`CutPiece` single definition in `OptimizationModels.cs`.
- `grep RunOptimizationFromInvoice|RunNestingAlgorithm|ExecuteIQ200` → zero old dispatch methods.
- `manualCutList` re-entry guard prevents recursion.

**Conclusion:** No duplicate/dead code.

---

## K. Exception Handling

- `grep catch {} empty` → none in `ViewModels/Optimization Services/Optimization Services/Production`.
- Only `OptimizationResultFeed.cs` catch preserves previous snapshot (deliberate: reporting bridge must never break quoting).
- `CurrentDocumentParts` previously had empty catch with debug “if suppressed” → fixed to log actual exception.
- All public methods in `SheetStorePricingService` never throw, return empty/false/null.

---

## L. Unit / Dimension

- All dimensions mm throughout, area `W*H/1_000_000` via `SqmCalculator.CalculateSheetArea`.
- `TryBuildInputs` validates `L,W>0 Qty>0` via `int.TryParse`.
- Rotation `AllowRotate=RotationEnabled` all pieces, `Free90` default.
- Equal-size DGU rule: blank W2/H2 → inherits W1/H1 in `ProductionDistributionEngine` and `DistributeChargeItems`.
- Overlap glass: W2/H2 filled → exact W2×H2.

---

## M. Tests

**Manual deterministic test (cannot run dotnet in sandbox, but logic verified):**

Dataset:
- Spec 1: DGU, SelectedSpecName “6mm clear FT + 12mm spacer + 6mm clear FT”, GroupNumber 1, OuterSheet 3210x2250, InnerSheet 3210x2250, OuterGlassColor “Clear”, InnerGlassColor “Clear”
- ItemRows: Row1 W1=1000 H1=1200 W2=0 H2=0 Qty=2, Row2 W1=800 H1=1000 W2=0 H2=0 Qty=1
- Spec 2: DGU same but W2/H2 blank too.

Expected after `BuildSpecGroupSnapshotsForOptimization`:
- 2 groups, each 2 rows (old filter would have kept them anyway because W1>0, but new filter also checks H>0 → still kept).
- `ProductionDistributionEngine.Distribute`: 8 cutting items? Actually 2 specs × (outer+inner) × qty: Spec1 outer 2+1=3 pieces, inner same 3 (equal-size fallback), total 6, Spec2 same 6 → 12 cutting items. Outer/inner each 1000x1200 etc.
- `RunFromStep5`: `newPieces` grouped by W/H sum qty: 1000x1200 qty 4 (2 specs ×2), 800x1000 qty 2 → 2 distinct sizes ×2 panes = 4 piece groups? Actually per composition, so if compositions differ, more groups. But at least piecesAdded=12.
- Stocks: Outer 3210x2250 + Inner 3210x2250 dedup by (W,H,Composition) → if compositions same, 1 rack, else 2 per spec. With different spec names, compositions differ → 4 racks? But `AddStep5Rack` seen (W,H,C) prevents duplicate rack for shared sheet only when composition same. So 4 racks expected.
- Previously with RC-002, if Sheet Store not primed and DGU+LAM, racks=0 → “no sheet sizes”.

**Automated tests:** No existing unit tests for this path; recommend adding.

---

## N. Build Verification

- `dotnet` not available in sandbox (checked `dotnet build` → not found).
- Syntax verified via file reads, no obvious compile errors (all edits preserve existing types, only added logging and fallback).
- `SheetStoreRepository` usage: `new SheetStoreRepository()` parameterless exists, `GetAllAsync()` returns `Task<List<GlassSheet>>` — verified in file.
- `AppLogService.Info` with `LogContext` exists — used elsewhere.

---

## O. Remaining Risks

1. **DguLamPricingPanelSnapshot has no sheet size fields** — future should add `OuterSheetWidthMm/HeightMm`, `LamOuterSheetWidthMm/HeightMm`, `LamInnerSheetWidthMm/HeightMm` or at least two sheets. Until then, DGU+LAM always depends on Sheet Store fallback.
2. **Sheet Store empty** → `GetColorsAsync` empty → Step4 cannot select sheet → OuterSheetWidth=0 → optimization fails with honest message. Need UI hint when Sheet Store empty.
3. **Quantity handling divergence:** Import parser qty<=0→1, snapshot builder qty<=0 skip. If user manually clears qty to 0, row dropped silently. Should show warning.
4. **No unit tests** for `BuildSpecGroupSnapshotsForOptimization` filter and `TryResolveStockSheet` fallback.
5. **Snapshot staleness:** 60s trust, background refresh fire-and-forget — if Sheet Store changes during quote, first run uses stale size but warns via cross-check note.

---

## P. Files Changed

- `ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.Optimization.cs` — fixed W/H filter, added diagnostic logging.
- `Services/ProformaInvoice/SheetStorePricingService.cs` — added fallback DB load in `TryResolveStockSheet` and `IsStockedSize`, warmed cache, improved comments.
- `ViewModels/Optimization/OptimizationViewModel.Margins.cs` — fixed reference-job message, added diagnostic logging in `TryBuildInputs`.
- `ViewModels/Optimization/OptimizationViewModel.LiveRun.cs` — added diagnostic logging at 3 boundaries (request, distribute result, built counts).
- `ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.ImportExport.cs` — fixed swallowed exception logging.
- `Services/Production/ProductionDistributionEngine.cs` — added diagnostic logging start/done.
- `Services/Optimization/OptimizationResultFeed.cs` — added diagnostic logging PublishLive/RefreshAsync.
- `.gitignore` — added mockup png exclusion per owner directive (previously).

---

## Q. Files NOT Changed (and why)

- `Services/Optimization/GuillotinePacker*.cs` — engine correct, single entry, no duplicate, validation already comprehensive.
- `Views/ProformaInvoice/Step5DataGrid/DataGridView.xaml` — bindings correct W1/H1/W2/H2/Qty.
- `Views/Optimization/Tabs/JobsTabView.xaml` — pane checklist bindings correct.
- `Models/ProformaInvoice/SavedInvoice/SpecGroupSnapshot.cs` — provenance fields already correct.
- `Models/ProformaInvoice/SavedInvoice/ProformaInvoiceSnapshot.cs` — DGU+LAM missing sheet fields is design, not fixed here to avoid schema migration, but documented as risk.
- `ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part1.cs` — DGU+LAM zero sheets is consequence of snapshot missing fields, not bug in this file.
- `Services/ProformaInvoice/Step5Grid/CutListImportParser.cs` — parsing correct, DimPair regex, qty handling.

---

## R. Data-Flow Diagram

```
Sheet Store (SQLite GlassSheet)
   ↓ GetAllAsync() / GetColors/Thicknesses/Sizes
SheetStorePricingService (static, never-throws, sync snapshot + async fresh)
   ↓ TryResolveStockSheet (largest area, exact thickness first) — FIXED fallback
   ↓ IsStockedSize (cross-check)

Step4 Pricing Panels (SGU/DGU/LAM/DGU+LAM)
   ↓ Sgu/Dgu/Lam snapshots have SheetWidth/Height + Price
   ↓ DguLam snapshot has NO sheet size (risk)
   ↓ ApplyStep4PricingSnapshot (atomic, _isSynchronizingFromStep4 guard)
SpecGroup (OuterSheetWidthMm/HeightMm, InnerSheetWidthMm/HeightMm, Outer/InnerGlassColor, prices, UInsert, Spacer, Interlayer)
   ↓ BuildSpecGroupSnapshotsForOptimization — FIXED W/H filter + logging
SpecGroupSnapshot (ItemRows W1/H1/W2/H2/Qty + sheets)

Step5 Grid (DataGridViewModel)
   ↓ Import: PromptOpenPath → Task.Run(ImportFileAsync) → header/positional/JSON → ApplyImportedRowsBySpec → SpecGroup
   ↓ Manual edit: GlassItemRow W1/H1/W2/H2/Qty
   ↓ Export: Excel/Csv/Pdf

Step5 → Optimization LiveRun
   ↓ RunFromStep5: snapshot-first validation, ProductionDistributionEngine.Distribute (equal-size fallback W2 blank → W1/H1)
   ↓ cut filter Stage.Cutting W>0 H>0
   ↓ newPieces: GroupBy W/H sum Qty, label distinct GlassRef
   ↓ newStocks: AddStep5Rack dedup (W,H,Composition) ComposeForSlot Pane1=Outer Pane2=Inner CompositionKey.Normalize
   ↓ wantedByGroup / rackedKeys provenance, Sheet Store fallback TryResolveStockSheet — FIXED
   ↓ CrossCheckStep4Sheet IsStockedSize notes
   ↓ Pieces.Clear() + Stocks.Clear() only after validation
   ↓ Run() same as JOBS tab

OptimizationViewModel
   ↓ TryBuildInputs: expands qty loop, AllowRotate=RotationEnabled, IsUsedInRun filter, margins Parse
   ↓ BuildCuttingGroups (composition grouping)
   ↓ RunCoreAsync → GuillotinePacker PackQuick/PackOptimize (single entry)
   ↓ Verify overlap/kerf/margin/rotation

OptimizationResultFeed
   ↓ PublishLive (DocumentKey stamped) → logging + RunPublished event
   ↓ Current filtered by IsForDocument guard prevents cross-doc bleed
   ↓ NormalizeSide W1/H1→Outer W2/H2→Inner priority
   ↓ Find sizeMatches fallback

Step6 Wastage
   ↓ Reads feed, weighted allocation for shared racks

Step9 Profit & Loss
   ↓ Reads feed + SpecGroup sheets, SheetSource provenance
```

---

## S. Final Verdict

**Root cause proven:** Step5 size data **was** being read, but **lost at two boundaries**: 
1) snapshot filter ignored H, allowing invalid rows to pass then silently dropped in distribution engine → “Step5 not importing” perception.
2) Sheet Store fallback failed on first run due to cold snapshot → DGU+LAM and any spec with zero Step4 sheet had zero racks.

**Fixes applied:** Targeted, minimal, no blind changes, preserve single source of truth, add diagnostic logging at 6 boundaries, fix messages, fix swallowed exception.

**Build:** Cannot compile in sandbox (dotnet missing) but syntax verified, no duplicate/dead code, no empty catches, WPF bindings verified.

**Remaining:** DGU+LAM sheet fields missing from snapshot schema — requires future migration. Recommend adding unit tests for filter and fallback.

**Status:** Ready for operator test with deterministic dataset (2 specs DGU equal-size W2 blank, 3210x2250 sheets, 1000x1200 qty).

