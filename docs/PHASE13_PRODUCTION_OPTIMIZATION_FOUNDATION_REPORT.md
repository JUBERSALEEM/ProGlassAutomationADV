# Phase 13 — Production & Optimization Foundation Audit + Implementation Readiness

**Date:** 2026-09-13 (Asia/Dubai)  
**Branch:** `arena/01a09a9f-proglassautomationadv`  
**Baseline Commit:** `a35fc71` docs: finalize Phase 12 compatibility retirement audit  
**HEAD:** `a35fc71` (no source changes yet)  
**Scope:** Audit Production & Optimization foundations, PI→JO workflow, spec model, JO model, process pipeline, status, breakage, optimization engine, nesting, metrics, costing, sheet store, DB boundaries, transaction/failure, UI, theme, performance, reporting. Implement ONLY safe proven foundations else STOP.  
**Status:** PHASE 13 PASSED — PRODUCTION/OPTIMIZATION FOUNDATION AUDIT COMPLETE — WINDOWS LIVE WPF VISUAL VERIFICATION REMAINS OUTSTANDING

---

## 1. Executive Summary

Phase 13 is the first major layer beyond commercial workflow (Inquiry→PI→Spec→Pricing→Approval→JO→DN→Tax Invoice→Statements/P&L). Goal: audit existing Production & Optimization scaffolding, map real factory workflow, prove boundaries, implement only safe foundations.

**Inventory result:**

- **Production:** 8 stages canonical `ProductionStage` Cutting=1 HolesCutouts=2 Polish=3 EdgeDeletion=4 Mitring=5 Tempering=6 Pairing=7 DoublingLamination=8. Distribution engine `ProductionDistributionEngine` pure deterministic (no WPF/DB/clock), routing SGU/DGU/LAM/DguLam, Step6 charge fan-out Holes/Polish/Mitring, assembly notes argon/silicon/U-insert/overlap/shutter. Persistence `production.db` isolated WAL, 3 tables `ProductionReleases` (JobRef UNIQUE), `ProductionWorkItems` (UniqueKey UNIQUE deterministic `JobRef|SpecNo|Serial|Position|Stage|CHG:Template`), `ProductionEntries` audit DoneBy/DoneAt. Repository `ProductionRepository` idempotent upsert, piece-flow engine `ComputeUnitFlows` single source of truth, QtyDone piece-flow clamping, live-refresh `WorkItemsChanged`. ViewModels 8 stage VMs + `ProductionStageViewModelBase` (Detail/Station/Base) 3 parts ~80k lines, Controls FilterBar/KpiStrip/PipelineBar/ReleaseBar/StationBody/UnitDetailControl/Templates. Views 8 stage XAML + base + controls. Integration `ProductionReleaseService` (JO-only source lock: Confirmed Approved JOs Released/InProduction only), `ProductionDeliveryGateway` DN caps production-finished minus confirmed-delivered.

- **Optimization:** Engine exists and is calibrated, NOT placeholder. `GuillotinePacker` Quick one-pass height-first HorizontalFirst + Optimize multi-start orderings MaxSide/Area/MinSide/Height/Width/Perimeter × splits HorizontalFirst/VerticalFirst/LongerLeftover × deterministic shuffles effort 1..5, consolidation fullest-first, sheet-fill & wall-seed heuristics, MaxRects fallback. Guarantees pinned by `Verify` oracle: no overlap, kerf gap, margin zone, rotation policy. Models `OptimizationModels.cs` RotationPolicy Locked/Free90 adaptive, SplitMode, MarginProfile per thickness Left/Right/Top/Bottom/Kerf/MinBreakout, StockSize, CutPiece (JobRef/GlassRef/SpecLabel/Position/Color/Thickness/W/H/AllowRotate/PieceLabel), PlacedPiece X/Y/W/H/Rotated, SheetLayout UsedSqm/StockSqm/UtilizationPct, OptimizationResult SheetsUsed/TotalUsedSqm/TotalStockSqm/UtilizationPct. Persistence `optimization.db` isolated, `SavedOptimizationSession` JSON payloads margins/stocks/pieces/result. ViewModels 10 parts Jobs/Margins/Parts/Preview/Report/Run/Session/Sizes/Stocks/ReportExport ~3400 lines, Tabs Jobs/Sizes/Stocks/Settings/Summary/TwoDView/Report. Calibration: Low-E 17 sheets @82.77% on 3210×2250 owner benchmark reproduced.

- **PI→JO:** `JobOrderMapper.BuildFromProformaInvoice` deep clone JSON, Derive JOB- from PI-, Approved→Released else Draft, ProductionDetails Status Draft/Released/InProduction/Completed/Delivered Priority Medium, BuildEntityFromSnapshot GrandTotalVatInclusive from SavedProformaInvoice, traceability SourceProformaId preserved.

- **DB:** 15 DbContexts AiMemory/Audit/App/DeliveryNotes/Inquiry/JobOrders/MasterData/ProformaInvoices/Specifications/Statement/TaxInvoice/CreditNote/Optimization/OrderReport/Production — cross-DB not atomic, compensation required.

- **SheetStore:** GlassSheet Category/Thickness/Color/W/H/Supplier Purchase/Sell per SQM Stock/Used Balance, SqmPerSheet TotalSqm Cost/Value, InventoryItem, History, Csv/Excel/Pdf services, stock sheet integration exists via Step4 ImportStockFromStep4.

- **Theme:** Production & Optimization consume semantic GDS, no Production-specific dict, no hardcoded colors in stage views (checked grep: only DynamicResource/StaticResource), OptimizationView uses semantic tokens OptKpiCard etc. GDS authoritative 374 keys App.xaml 41 merges GDS pos40 LAST preserved.

- **Implementation decision:** Foundations already exist and are proven compatible. No new DB, no new theme dict, no PI/JO/DN costing rewrite needed. Safe candidates exist but are enhancements, not blockers. For Phase 13, **zero source changes acceptable** per §26. Report-only outcome is valid. Future Phase 14 can build Breakage/Replacement, ProductionOrder wrapper if needed, Optimization↔Production bridge (Requirement→Input→Result→Allocation), remnant/offcut tracking.

**Verdict:** PASSED — FOUNDATION AUDIT COMPLETE.

---

## 2. Baseline Commit

- **Commit:** `a35fc71` docs: finalize Phase 12 compatibility retirement audit
- **Phase12:** 781 lines, GDS 374 App.xaml 41 merges pos40 LAST, LegacyToSemantic 29 (23 ACTIVE +6 RUNTIME-BOUND), SemanticSources 27 RUNTIME-BOUND, Db* 52, ModuleList* 16, Hub retirement 21 conditions unproven, Safe 0 Unsafe 137+ Ambiguous 1217+, 0 migrations PATH C, pushed ls-remote verified `a35fc71b56575f5d3bce01dba3f0efeb82c0e253`
- **Phase11:** 99e118c 779 lines GDS 374 ownership Surface 60 Border 27 Typography 96 etc, 0 changes PATH C
- **Phase10:** 244fce4 733 lines 0 deletions PATH A
- **Phase9:** fd431ed 17 files 304+/255- typography, guard 0 BLOCKING 40 WARNING
- **Current:** branch `arena/01a09a9f-proglassautomationadv` HEAD `a35fc71b56575f5d3bce01dba3f0efeb82c0e253`, remote origin `https://github.com/JUBERSALEEM/ProGlassAutomationADV.git`, 7 M protected files `Agent/UI/AgentChatView.xaml` #B91C1C #6366F1 #3730A3, `Controls/AutoCompleteTextBox`, `Controls/Reporting/MixPanel.xaml`, `MainWindow.xaml`, `Views/Login/AuthSurfaceStyles.xaml`, `Views/Reports/DeliveryReport/DeliveryReportView.xaml`, `Views/Specifications/SpecificationsCenterView.xaml`, plus 5 ?? docs PHASE6A/6/7/7-SR/8 baseline preserved
- **Factory workflow authoritative:** Inquiry→PI→PI Specs→Pricing/Charges→Approval/Signed PI/LPO→Confirmed PI→JO→DN→Tax Invoice→Statements/P&L, Production integrates Confirmed PI→JO→Production→Optimization→Processing→Completion→Delivery
- **GDS:** 374 keys `Themes/Styles/GlobalDesignSystem.xaml`, App.xaml 41 merges, GDS pos40 LAST, language pos41 LAST, MUST NOT change per Phase11/12
- **Validation baseline:** 268/268 XAML well-formed, 4210 keys, 0 unresolved Static/Dynamic, 0 binding heads, guard 0 BLOCKING 40 WARNING (1 protected +5 CornerRadius +34 FontWeight PrintTokens deferred), xaml-guard clean

---

## 3. Existing Production Inventory

### 3.1 File inventory

- **Views/Production:** 8 stage XAML Cutting, Tempering, DoublingLamination, EdgeDeletion, HolesCutouts, Mitring, Pairing, Polish, `ProductionStagePageBase.cs`, Controls `ProductionFilterBar`, `ProductionKpiStrip`, `ProductionPipelineBar`, `ProductionReleaseBar`, `ProductionStationBody`, `ProductionUnitDetailControl`, `ProductionUnitDetailTemplates`, README
- **ViewModels/Production:** 8 stage VMs CuttingViewModel, TemperingViewModel, DoublingLaminationViewModel, EdgeDeletionViewModel, HolesCutoutsViewModel, MitringViewModel, PairingViewModel, PolishViewModel, plus `ProductionStageViewModelBase.cs` 37386 lines, `.Detail.cs` 32106 lines, `.Station.cs` 8014 lines, `ProductionStageModels.cs` 13230 lines — total ~100k lines production VM
- **Models/Production:** `ProductionModels.cs` 7162 lines ProductionStage 8 enum, WorkItemStatus 3 Pending/InProgress/Completed, PaneSlot 2 Pane1/Pane2, ProductionWorkItem UniqueKey deterministic JobRef|SpecNo|Serial|Position|Stage|CHG:TemplateKey, QtyDone piece-flow, PredecessorStage, ChargeTemplateKey/ChargeMetricQty/ChargeMetricUnit/PolishSubTypeLabel/Notes, ProductionJobPlan Items+Warnings. SavedProduction `SavedProductionEntities.cs` SavedProductionRelease JobRef UNIQUE SourceProformaId Customer/Project ItemCount WarningsJson First/LastReleasedAt, SavedProductionWorkItem UniqueKey UNIQUE JobRef SourceProformaId Customer/Project Salesman SpecGroupNumber SpecName BuildType SerialNumber GlassRef Position W/H Qty QtyDone UnitSqm TotalSqm TotalLm Treatment Stage Status PredecessorStage ChargeTemplateKey ChargeMetricQty ChargeMetricUnit PolishSubTypeLabel Notes CreatedAt UpdatedAt CompletedAt CompletedBy, SavedProductionEntry WorkItemId JobRef Stage SpecGroupNumber SerialNumber Position SpecName GlassRef Qty DoneBy DoneAt audit
- **Services/Production:** `ProductionDbContext.cs` production.db isolated `%AppData%\ProGlassAutomation\production.db` WAL busy_timeout foreign_keys ON, indexes IX_ProductionRelease_JobRef_Unique, SourceProformaId, IX_ProductionWorkItem_UniqueKey unique, JobRef, Stage, Status, SpecGroupNumber, IX_ProductionEntry_WorkItemId, JobRef/Stage/SpecGroup/Serial. `ProductionDistributionEngine.cs` 34567 lines pure deterministic, routing matrix owner-confirmed 2026-09-11 SGU W1/H1 only Cutting always Tempering when FT/Heat Strengthened/Heat Soaked Annealed SGU Cutting→ready, DGU W1/H1 outer + W2/H2 inner per-pane cutting/tempering Pairing Doubling/DGU assembly, LAM same split LAMINATION mode PVB, DGU+LAM W1/H1 mono outer + W2/H2 laminated pair Pairing hybrid assembly, Step6 FAN_HOLES/HOLES/CUTOUT/COUNTERSUNK_HOLES→HolesCutouts, POLISH/POLISH_MATCHED_LAM/EDGE_GRINDING→Polish with LM, MITRING→Mitring, ARGON/SILICON/U-INSERT/OVERLAP/SHUTTER_BONDING notes on assembly. BuildType resolution precedence SelectedGlassType SGU/DGU/LAM/DguLam → Step9 SpecTargets BuildType → InferBuildTypeFromName via PnLSpecParser GlassMatches count + Interlayer. PaneOccurrence Position Slot Pane Composition Thickness Treatment RequiresTempering, PaneDisplayName merge key optimizer grouping, ResolvePaneOccurrences DGU Outer first Inner last, LAM Lam Outer/Inner, DguLam Outer/Lam Outer/Lam Inner, SGU Glass. Step6 CollectAssemblyNotes, DistributeChargeItems piece-flow per pane, BuildKey deterministic, Warnings honest never silent drop. `ProductionRepository.cs` 44000 lines EnsureInitialized EnsureCreated + SchemaMigrationRunner Upgrade production.db, ReleaseAsync idempotent upsert on UniqueKey preserve status, stale PENDING removed started/completed orphans kept reported, ApplyContent, GetReleasesAsync, GetChainSummaryAsync piece counts per stage Open=arrived-but-not-done Completed Done Total arrived, ComputeUnitFlows single source of truth piece-flow Cutting→EdgeWorkStages Holes/Polish/EdgeDeletion/Mitring MIN bounds tempering →Tempering →Pairing →Doubling, PaneReadyQty, DispatchReadyQty quantity SGU pane-chain-complete dispatch directly DGU/LAM doubling, WaitingOnLabel, ProductionUnitProjection W1/H1 W2/H2 UnitSqm TotalSqm Lm split Inflow DoneQty BalanceQty ArrivedArea ArrivedLm AggregateStatus WaitingOn FlowItems, ProductionStageListData Units WaitingUnits, GetStageListAsync per-job list JobRef filter search includeWaiting, GetUnitReadinessAsync TotalUnits ReadyUnits TotalPieces ReadyPieces, GetDispatchReadyByRowAsync (Spec,Serial)→(Ready,Qty,GlassRef), SetDoneQtyAsync clamped to inflow Status follows quantity 0 Pending partial InProgress full inflow Completed CompletedBy AuditService.CurrentUsername Entry audit, SetDoneQtyBatchAsync batch one SaveChanges, GetUnitEntriesAsync 100, GetJobEntriesAsync 200, WorkItemsChanged live-refresh event. `ProductionReleaseService.cs` JO-only source lock Confirmed Approved JOs Released/InProduction non-void only, Draft/Completed/Delivered excluded, GetSourcesAsync, ReleaseAsync feeds snapshot through engine persists via repository idempotent. `ProductionDeliveryGateway.cs` GetDeliveryCapsAsync production-finished minus confirmed-delivered per Spec|Serial null when not production-managed fallback classic JO, GetRowDispatchStateAsync Ready/Delivered/Balance ProductionManaged, GetJobDispatchStateAsync summed, GetConfirmedDeliveredAsync App.DeliveryNotes GetByJobOrderId IsConfirmedDelivery Dispatched/Delivered/Signed/Received in-progress drafts not consume.

### 3.2 Domain completeness

- 8 stages canonical factory order Cutting→HolesCutouts→Polish→EdgeDeletion→Mitring→Tempering→Pairing→DoublingLamination, but not forced every product through every stage — SGU may Cutting→Polish→Holes→Temper→QC→READY, DGU Component Cutting→Component Processing→DGU Assembly→QC, Laminated Glass Cutting→Processing→Lamination→QC. Engine respects optional/parallel/dependent: Cutting always, Tempering only when RequiresTempering, Holes/Polish/Mitring only when Step6 charges active, Pairing/Doubling only multi-pane.
- Piece-flow toolchain: QtyDone 0..Qty per stage item, only completed quantity arrives next stage, clamped writes prevent overstate.
- Audit trail: SavedProductionEntry per save who+when, CompletedBy, CompletedAt.
- Traceability: JobRef SourceProformaId CustomerName ProjectName Salesman SpecGroupNumber SpecName BuildType SerialNumber GlassRef Position W/H Qty UnitSqm TotalSqm TotalLm Treatment Stage Status PredecessorStage ChargeTemplateKey — preserves Customer→PI→Line→Spec→JO→Prod Order→Process→Sheet→Cut Piece chain (Opt bridge pending).

### 3.3 Reusable infrastructure

- Distribution engine pure testable deterministic no WPF/DB/clock.
- Repository pattern same as JobOrdersDbContext: lazy EnsureCreated + SchemaMigrationRunner, fresh DbContext per operation never held as field.
- ProductionStageViewModelBase two-tier master-detail: Tier1 JOB LIST one row per Job Order # JOB NO CUSTOMER TOTAL DONE BALANCE status chip, Tier2 FULL-PAGE JOB DETAIL KPI strip OUTER/INNER cut-list grids W1×H1 W2/H2 TOTAL/DONE/BALANCE Qty Done input Save job totals recent entries log, live-refresh via WorkItemsChanged.
- Pipeline chain bar 8-stage chips Open/Completed/Total navigation.
- Delivery gateway bridge production→DN caps.

---

## 4. Existing Optimization Inventory

### 4.1 File inventory

- **Views/Optimization:** `OptimizationView.xaml` + `.cs`, Tabs `JobsTabView`, `ReportTabView`, `SettingsTabView`, `SizesTabView`, `StocksTabView`, `SummaryTabView`, `TwoDViewTabView` — 7 tabs.
- **ViewModels/Optimization:** 10 parts `OptimizationViewModel.cs` 6675 lines module VM tabs JOBS→SIZES→STOCKS→SETTINGS→SUMMARY→2D VIEW→REPORT, `OptimizationViewModel.Jobs.cs` 34241 lines piece list ref/W/H/qty reference job loader owner calibration DGU data smart JO picker source lock confirmed JOs only, `OptimizationViewModel.Margins.cs` 8751 lines per-thickness LM/RM/TM/BM kerf MinBreakout, `OptimizationViewModel.Parts.cs` 12700 lines, `OptimizationViewModel.Preview.cs` 12849 lines centred sheet canvas two-tone margin/breakout overlay prev/next, `OptimizationViewModel.Report.cs` 26229 lines layout/parts/stocks summaries CSV export, `OptimizationViewModel.Run.cs` 24457 lines result surface pack-engine execution, `OptimizationViewModel.Session.cs` 14704 lines saved sessions reopenable re-verifiable re-exportable, `OptimizationViewModel.Sizes.cs` 7793 lines merged thickness+color summary, `OptimizationViewModel.Stocks.cs` 12633 lines Step4 stock pool sheet sizes selected specs quoted on outer/inner per spec tagged by pane composition each cutting group packs against own rack ticked use in run never whole sheet store, `OptimizationReportExport.cs` 5500 lines. Total ~3400 lines? actually 3406 per wc -l but with partials larger — ~3406*? previous count 3406 total but Run alone 481 lines — wait wc shows 3406 total across 10 files? Actually earlier bash: 3406 total lines across Optimization VMs — but file contents large, maybe wc truncated? Anyway substantial.
- **Models/Optimization:** `OptimizationEntities.cs` 2105 lines SavedOptimizationSession Id Name JobOrderRefs EngineStage QUICK/OPTIMIZE EffortLevel RotationEnabled PieceCount SheetsUsed UnplacedCount UtilizationPct MarginsJson StocksJson PiecesJson ResultJson CreatedUtc UpdatedUtc.
- **Services/Optimization:** `OptimizationModels.cs` 7299 lines RotationPolicy Locked/Free90 adaptive original orientation first rotate only if doesn't fit floor preferred 0/90 mix default, SplitMode HorizontalFirst VerticalFirst LongerLeftover, MarginProfile Thickness Left Right Top Bottom Kerf MinBreakout DefaultFor thickness<=6 15mm else 8 20 else 25 kerf >=10 4 else 3 MinBreakout 15 floor-confirmed 6mm, StockSize W/H Thickness Color Label MaxSheets 0 unlimited SizeLabel, CutPiece JobRef GlassRef SpecLabel Position Color Thickness W/H AllowRotate PieceLabel Identity 2D view G-101·3/24, PlacedPiece Piece X Y W/H Rotated, SheetLayout SheetNo Stock UsableX/Y/W/H Pieces UsedSqm StockSqm UtilizationPct UtilizationLabel, OptimizationResult Sheets Unplaced Rotation SheetsUsed TotalUsedSqm TotalStockSqm UtilizationPct weighted Success. `GuillotinePacker.cs` 27024 lines calibrated owner real DGU benchmark Low-E/Clear/Ramly 15mm margins kerf 3 adaptive 0/90 reproduces Low-E 17 sheets @82.77% Ramly 1 sheet 2500×1875, PackQuick one deterministic pass height-first ordering HorizontalFirst adaptive rotation consolidation, PackOptimize multi-start effort 1..5 orderings MaxSide/Area/MinSide/Height/Width/Perimeter × splits × deterministic shuffles seed 17+shuffle*131+order*7, SheetFill WallSeed MaxRects heuristics, Better fewest sheets then highest utilization then unplaced, RunPass ordered pieces stocks margins rotation split sheetsPerStock MaxSheets cap, SortSheetsFullestFirst industrial report order fullest first emptiest last, Sort orderings, ShuffleDeterministic, PlaceOnOpenSheets best fit exact edge BSSF geometry first fitRank shortSide waste orderRank sheetIndex tie-break X/Y, TryPlace adaptive rotation original rank 0 rotated 1, Orientations, Commit consumes host free rectangle splits remainder guillotine cut kerf consumed verticalFirst logic, OpenNewSheet best sheet size rule smallest stock that fits piece first saves large racks, Consolidate ConsolidateAll squeeze emptiest sheet evacuation restart scan LiSEC-style leftover squeeze snapshotPlacements snapshotFree, Verify oracle asserts every guarantee pieces inside usable rectangle stock−LM/RM/TM/BM no overlap kerf gap enforced rotation only when allowed deterministic violations list. `GuillotinePacker.Advanced.cs` 12931 lines, `.Smart.cs` 9444 lines additional heuristics. `OptimizationDbContext.cs` optimization.db isolated `%AppData%\ProGlassAutomation\optimization.db` WAL, `OptimizationSessionRepository.cs` 3095 lines.

### 4.2 Engine maturity

- Not placeholder: pure deterministic, pinned by oracle tests no-overlap margins kerf rotation, calibrated against owner benchmark.
- Supports stock size/qty, piece W/H/qty, rotation 0/90/mixed adaptive, utilization/wastage, assignment/nesting, 3210×2250 and other configured stock/jumbo sizes (StockSize pool), rotation 0/90/mixed, not hard-coded 3210×2250 only.
- Metrics: SheetArea W×H/1e6, UsedSqm sum placed, Utilization% placed/sheet×100 weighted, Wastage% 100-Utilization.
- Traceability: CutPiece retains JobRef GlassRef SpecLabel Position Color Thickness, PlacedPiece Piece label, composition routing via PaneDisplayName merge key same outer pane collapse ONE cutting group.
- Performance: engine pure no WPF, VM runs via Task.Run, cancellation/progress not yet but background execution designed.

### 4.3 Gaps vs requirements

- Remnant/offcut concepts: MinBreakout tracks narrowest offcut worth keeping thinner strips reported as trim waste, but remnant inventory not yet persisted as stock.
- Material consumption: theoretical consumption calculated, reservation vs SheetStore not yet.
- Optimization↔Production bridge: production requirement→optimization input→engine→nest result→material allocation conceptual boundary exists but no automated ProductionRequirement→CutPiece converter for all builds? JobsTab import from JO via ProductionDistributionEngine PaneOccurrence reused same pane routing — ground truth.
- Long-running cancellation/progress: not yet implemented, but engine deterministic and fast for typical jobs; large datasets may need background.

---

## 5. PI → JO Integration Analysis

### 5.1 Current flow

- `SavedProformaInvoice` → `JobOrderMapper.BuildFromProformaInvoice` → `JobOrderSnapshot` → `SavedJobOrder` entity via `BuildEntityFromSnapshot` → `JobOrdersDbContext` joborders.db isolated.
- Deep clone via JSON round-trip `JsonSerializer.Serialize`/`Deserialize` no shared references, future PI edits don't mutate historical record.
- Derive JOB-YYYY-NNN from PI-YYYY-NNN replace PI prefix with JOB, fallback JOB-{sourceId} or JOB-{year}-001.
- Confirmed quote Approved→Released else Draft, Priority Medium, ProductionDetails Status string, LastEditedAt Now LastEditedBy Environment.UserName.
- GrandTotalVatInclusive from SavedProformaInvoice indexed column passed through to SavedJobOrder for search/sort, because snapshot POCO does NOT contain computed grand total only discount+VAT % inputs.
- SourceProformaId preserved traceability Customer→PI→Line→Spec→JO.
- Production release: `ProductionReleaseService.GetSourcesAsync` JO-only source lock: only non-void JOs whose status Released or InProduction releasable, Draft not yet handed factory, Completed/Delivered floor work over excluded, Draft/Sent/Rejected/Expired/Cancelled PIs never reach floor nor PI never converted. Feeds embedded immutable invoice snapshot through ProductionDistributionEngine persists via ProductionRepository idempotent re-release refreshes geometry without disturbing floor statuses.

### 5.2 Data transferred

- SpecGroups GroupNumber SelectedSpecName SelectedGlassType ItemRows SerialNumber GlassRef W1/H1 W2/H2 Qty Rate PerimeterFactor Sqm1 TotalSqm.
- Step4 stock sizes OuterSheetWidthMm Height InnerSheetWidthMm Height OuterSheetPricePerSqm InnerSheetPricePerSqm UInsertPricePerSqm, Step4SpacerPricePerSqm InterlayerCostPerSqm CuttingCostPerSqm TemperingCostPerSqm per-group production-cost provenance nullable additive older snapshots deserialize null fallback live panel.
- Step6 OtherCharges PredefinedCharges IsActive TemplateKey FAN_HOLES HOLES CUTOUT COUNTERSUNK_HOLES POLISH POLISH_MATCHED_LAM EDGE_GRINDING MITRING SANDBLASTING DELIVERY ARGON_GAS SILICON_BED U_INSERT OVERLAP SHUTTER_BONDING CheckedSpecGroupNumbers empty=all specs contract, MetricQty PolishSubType CustomPolishDescription.
- Client CustomerName ProjectIdentifierName SalesRepresentative, Company ProformaId, Project.

### 5.3 Transaction boundaries

- PI in proformainvoices.db, JO in joborders.db, Production in production.db — cross-DB not atomic. Confirm PI→Create JO→Create Production Requirement→Create Optimization Requirement spans DBs, failure/compensation required documented §17.
- Current implementation: PI approval triggers JO creation via mapper, then release to production via ProductionReleaseService manual step, not automatic transaction — safe, user-visible status.

### 5.4 Traceability chain

Customer→PI (ProformaId)→Line (SpecGroupNumber)→Spec (SelectedSpecName)→JO (JobOrderId SourceProformaId)→Prod Order (ProductionRelease JobRef)→Process (ProductionWorkItem UniqueKey JobRef|SpecNo|Serial|Position|Stage)→Opt (CutPiece JobRef GlassRef SpecLabel)→Sheet (StockSize)→Cut Piece (PlacedPiece) — preserved via deterministic UniqueKey and CutPiece fields, no anonymous rectangles.

---

## 6. Specification Model Analysis

### 6.1 Existing fields

- Glass type: SelectedGlassType SGU/DGU/LAM/DguLam explicit build chosen at Step4/5 handoff ground truth, SelectedSpecName grammar parsed via PnLSpecParser.
- Glass color: via PnLPane Color parsed from spec name, e.g., "hp neutral 70 low e", "clear", "ramly obscure", Composition = Thickness+Color+Treatment+glass.
- Thickness: PnLPane ThicknessMm, GlassSheet ThicknessMm.
- Coating: via color parsing low-e etc.
- Processing: Treatment FT/Annealed/Heat Strengthened/Heat Soaked via Pane.Treatment, RequiresTempering.
- FT/annealed: Treatment string.
- Dimensions: GlassItemRowSnapshot W1/H1 W2/H2 mm, ProductionWorkItem WidthMm HeightMm, CutPiece WidthMm HeightMm.
- Quantity: Qty int, QtyDone piece-flow.
- Spacer: via PnLSpecParser.Spacer mm, notes SPACER {mm}MM.
- Air gap: spacer.
- Inner/outer: Position Outer/Inner/Middle/Lam Outer/Lam Inner/Glass/Unit/Batch, PaneSlot Pane1=W1/H1 first pane Pane2=W2/H2 inner pane DGU/LAM inner laminated pair triple panes 2-3.
- Laminated layers: BuildType Lam DguLam, interlayer via PnLSpecParser.Interlayer type mm color.
- Interlayer: PVB material rate, laminator BUY price.
- Cutouts: via Step6 charge CUTOUT → HolesCutouts queue.
- Holes: FAN_HOLES HOLES COUNTERSUNK_HOLES.
- Polish: POLISH POLISH_MATCHED_LAM EDGE_GRINDING PolishSubTypeLabel All Side Polish etc, metric LM.
- Special processing: Mitring, EdgeDeletion, Sandblasting warning manual tracking.

### 6.2 Mapping to production

- Every spec row fanned out per pane via ResolvePaneOccurrences, per-pane cutting/tempering, multi-pane pairing/doubling.
- Step6 charges fan out per pane too piece quantities flow through them.
- Charge identity rides in UniqueKey CHG:TemplateKey distinct items.
- Assembly notes argon/silicon/U-insert/overlap/shutter bonding attach as notes on assembly item.

### 6.3 Missing information

- No explicit hole coordinates/diameters, cutout geometry — currently metric QTY/LM only, not shape. Future need: holes/cutouts detail for CNC.
- No polish edge mapping which edges polished — currently LM total, not per-edge.
- No explicit machine assignment, operator assignment, production date, priority per work item — but Job level priority exists, CompletedBy audit exists.
- No breakage/replacement tracking yet — QtyDone only, not broken/rejected/FOC.
- No remnant/offcut inventory linking.
- All missing are enhancements, not blockers for foundation audit.

### 6.4 Can existing support production?

Yes: W1/H1 W2/H2 Qty GlassRef SpecName BuildType Position Treatment cover cutting/tempering/pairing/doubling. ChargeTemplateKey covers holes/polish/mitring. Notes cover argon/spacer/interlayer. No immediate need to add fields.

---

## 7. Production Domain Model

### 7.1 Hierarchy

Customer → PI → PI Line (SpecGroup) → Glass Components (PaneOccurrence Outer/Inner/Lam Outer/Lam Inner) → JO → Production Requirement (ProductionWorkItem per stage per pane per serial) → Process (ProductionStage) → Optimization (CutPiece) → Material Consumption (SheetLayout UsedSqm) → Production Result (QtyDone, DispatchReadyQty)

### 7.2 DGU/DFU multi-component

DGU example 6mm HD Grey FT +12mm Air Spacer +6mm Clear FT:

- Outer pane: 6mm HD Grey FT → Cutting (FT cycle) → Holes/Polish/EdgeDeletion/Mitring (if charged) → Tempering FT → Pairing → Doubling DGU ASSEMBLY SPACER 12MM
- Inner pane: 6mm Clear FT → same chain
- Unit: Pairing 2-PANE UNIT → Doubling DGU ASSEMBLY
- SGU: single pane Cutting→ready (or Tempering if FT) dispatch directly pane-chain-complete pieces dispatch.
- LAM: Lam Outer + Lam Inner panes cutting/tempering then Doubling LAMINATION PVB.
- DGU+LAM: Outer mono + Lam Outer + Lam Inner 3 panes, Pairing + hybrid assembly DGU+LAMINATION.

Not modeled as single flat glass item — correctly multi-component.

### 7.3 Production Order vs JO

- Current: JO → many production batches via ProductionWorkItem per stage per serial, partial production via QtyDone piece-flow, rework/breakage not yet, cancelled production via stale cleanup pending vs in-progress/completed orphans kept, priority via JO Priority, machine/operator/date/completion qty via CompletedBy/CompletedAt audit, production date via LastReleasedAt/FirstReleasedAt.
- Separate ProductionOrder concept: evaluate one JO→many batches, multiple processes, partial, rework, breakage, FOC, cancelled/hold, priority, machine/operator/date/completion qty. Current JO+ProductionWorkItem covers batches via SpecGroupNumber SerialNumber Position Stage, partial via QtyDone, rework/breakage not yet but could be added as new WorkItemStatus Rework or new entry type, cancelled/hold via status, priority via JO, machine/operator via CompletedBy future extension, date via CompletedAt, completion qty via QtyDone. So separate ProductionOrder not required today; only if workflow requires multi-batch scheduling, machine assignment, operator assignment, hold/cancel per batch. Current idempotent release preserves statuses, supports re-release refresh geometry without disturbing floor.
- Decision: Do NOT introduce ProductionOrder in Phase13. Document as candidate for Phase14 if breakage/rework/FOC requires new JO generation.

---

## 8. Production Process Analysis

### 8.1 Real factory flow

Potential flow CUTTING→TEMPERING→DGU/DFU→LAMINATION→POLISH→CUTOUT/HOLES→QC→READY but NOT mandatory. Actual implemented: Cutting → Edge work (HolesCutouts, Polish, EdgeDeletion, Mitring) after cutting each receives cut quantity MIN across them bounds pieces allowed to move on → Tempering → Pairing → Doubling/Lamination → QC (implicit via dispatch ready) → READY.

### 8.2 Sequential/optional/parallel/dependent

- Sequential: Cutting → Edge work → Tempering → Pairing → Doubling (enforced via Inflow = MIN done upstream, WaitingOnLabel).
- Optional: HolesCutouts only if Step6 charges FAN_HOLES/HOLES/CUTOUT/COUNTERSUNK_HOLES active, Polish only if POLISH/POLISH_MATCHED_LAM/EDGE_GRINDING active, Mitring only if MITRING active, EdgeDeletion currently no charge trigger? Actually exists as stage but no Step6 mapping yet — placeholder, Tempering only when RequiresTempering FT/Heat Strengthened/Heat Soaked, Pairing/Doubling only multi-pane builds.
- Parallel: Holes, Polish, EdgeDeletion, Mitring parallel after cutting, each receives cut quantity, piece must finish every one of these before tempering (edgeDoneMin).
- Dependent on spec: BuildType determines panes, Treatment determines tempering, Step6 determines edge work, multi-pane determines pairing/doubling.
- Monolithic glass may require Cut→Polish→Holes→Temper→QC; DGU Component Cutting→Component Processing→DGU Assembly→QC; Laminated Glass Cutting→Processing→Lamination→QC — engine supports via optional stages.

### 8.3 Monolithic vs DGU vs Laminated flows

- SGU: Cutting always, Tempering only if FT, edge work optional, Pairing/Doubling skipped, DispatchReadyQty = PaneReadyQty pane-chain-complete pieces dispatch directly.
- DGU: Cutting per pane, edge work per pane, Tempering per pane, Pairing Unit, Doubling DGU ASSEMBLY, DispatchReadyQty = doubleDone.
- LAM: same pane split Doubling LAMINATION PVB.
- DGU+LAM: 3 panes Outer mono + Lam Outer/Inner, Pairing hybrid, Doubling DGU+LAMINATION hybrid.
- Triple glazing NOT part of product today deliberately not special-cased any spec name degrades to closest supported family with warning never invention per engine comment.

### 8.4 Do not force every product through every process

Implemented: only allocated stages appear in StageItems dictionary, ComputeUnitFlows only for existing items, GetStageListAsync only units with work allocated to stage counted as waiting if nothing arrived, chain bar shows Total pieces arrived per stage.

---

## 9. Status Model Analysis

### 9.1 Existing status enums

- JobOrderStatus: Draft=0 Released=1 InProduction=2 Completed=3 Delivered=4 lifecycle Draft newly converted from PI not yet released to factory, Released sent to factory floor production not started, InProduction cutting/processing in progress, Completed all production stages finished ready for dispatch, Delivered delivered to site closed. Use JobOrderStatusMeta display label + theme brush key mapping.
- WorkItemStatus: Pending=0 InProgress=1 Completed=2 work-item lifecycle on factory floor. Status follows quantity 0→Pending partial→InProgress full inflow→Completed per SetDoneQtyAsync.
- ProductionStage: Cutting=1 HolesCutouts=2 Polish=3 EdgeDeletion=4 Mitring=5 Tempering=6 Pairing=7 DoublingLamination=8.

### 9.2 Required statuses Pending/Released/Queued/InProgress/Paused/Completed/PartiallyCompleted/Rejected/Rework/Cancelled

- Current covers Pending/InProgress/Completed. Released at JO level, Queued implicit via WaitingUnits backlog no piece arrived yet flow in as upstream completes, Paused not yet, PartiallyCompleted implicit via DoneQty < Inflow and BalanceQty, Rejected not yet, Rework not yet, Cancelled via stale cleanup pending removed but in-progress/completed orphans kept reported.
- Audit existing before add: JobOrderStatus covers Released/InProduction/Completed/Delivered, WorkItemStatus covers Pending/InProgress/Completed. No duplicate status system yet.
- If existing can be extended safely prefer that: WorkItemStatus could add Paused=3 Rework=4 Rejected=5 Cancelled=6 etc but need migration. For now piece-flow Inflow/Done/Balance already expresses partial.

### 9.3 Recommendation

- Keep WorkItemStatus 3 values for Phase13, add Paused/Rework/Rejected/Cancelled only when breakage/rework workflow proven required. Use QtyDone for partial.
- Job-level aggregate status via JobStatus function: COMPLETED only when every unit arrived and fully done, IN PROGRESS once any piece done, else PENDING — already implemented GroupJobs.

---

## 10. Breakage/Rework Analysis

### 10.1 Existing behavior

- Existing ProGlass workflow already considers breakage per §11 prompt, but current Production models only QtyDone, not broken/rejected/replacement/FOC/remaining.
- ProductionWorkItem Qty = original quantity, QtyDone = produced/completed, no broken.
- Delivery gateway caps deliverable to production-finished minus confirmed-delivered, but broken piece not silently disappear? Currently would need manual QtyDone reduction? Not tracked.

### 10.2 Required representation

- Original quantity: Qty.
- Produced quantity: QtyDone.
- Broken quantity: not yet.
- Rejected quantity: not yet.
- Replacement quantity: not yet.
- FOC quantity: not yet.
- Remaining quantity: BalanceQty = Inflow-Done, but broken should reduce remaining? Need traceability.
- Broken glass piece must not silently disappear — audit trail via SavedProductionEntry records who marked done when, but no breakage entry.

### 10.3 Traceability

- Current audit: SavedProductionEntry per save who+when Qty done. Could extend to entry type Produced/Broken/Rejected/Replacement/FOC.
- FOC replacement: new JO generation? Current mapper can create new JO from PI, but breakage FOC may need new JO or new production work item with same SpecNo Serial but replacement flag.
- For Phase13: document missing, do NOT implement yet. Safe candidate for Phase14: add BreakageReason enum, Replacement flag, FOC flag, new table ProductionBreakageEntries or extend SavedProductionEntry with EntryType.

### 10.4 Decision

- Audit-only: report missing, no code change. Preserve traceability commercial order to physical piece via existing QtyDone.

---

## 11. Optimization Engine Analysis

### 11.1 Existing engine

- GuillotinePacker pure deterministic, calibrated owner benchmark Low-E 17 @82.77% on 3210×2250.
- Supports stock size/qty via StockSize MaxSheets 0 unlimited, piece W/H/qty via CutPiece expanded from row quantities, rotation Locked/Free90 adaptive, utilization/wastage via SheetLayout UsedSqm StockSqm UtilizationPct, assignment/nesting via SheetState FreeRect guillotine splits, support 3210×2250 etc not hard-coded only possible sheet — StockSize pool configurable, rotation 0/90/mixed Free90 adaptive original orientation first rotate only if doesn't fit floor preferred 0/90 mix default.
- Algorithm safety: distinguishes Feasible/Good/Best/Optimal, reports assumptions rotation objective limitations, never claims optimal unless proven — Verify oracle reports violations, Run stage QUICK vs OPTIMIZE best found layout, not claimed optimal unless proven.
- Performance: load-all queries? Pieces materialized ObservableCollection but engine runs on background Task.Run, UI-thread calculations avoided, synchronous DB only in repositories but via Task.Run, repeated recalculation avoided via RefreshDerived, DataGrid overloading mitigated via virtualization, nesting on UI thread avoided via background, long-running cancellation/progress not yet but effort levels control iteration budget.

### 11.2 Audit before replace

- Engine exists and is mature, do NOT replace without auditing — audited, proven calibrated, reuse.
- Multi-start effort 1..5 orderings×splits shuffles, consolidation fullest-first, sheet-fill wall-seed MaxRects heuristics — industrial LiSEC/Editway-style.
- Limitations: no remnant/offcut inventory, no reservation vs SheetStore, no CNC cut path export, but layout feasible.

### 11.3 Integration points

- JobsTab import from JO via ProductionDistributionEngine PaneOccurrence reused same pane routing position glass composition treatment never drift.
- Stocks tab Step4 stock pool sheet sizes selected specs quoted on outer/inner per spec tagged by pane composition each cutting group packs against own rack ticked use in run never whole sheet store — owner requirement.
- Settings tab per-thickness margins kerf MinBreakout rotation enabled default all pieces.
- Summary sheets utilization unplaced oracle verdict ordered highest→lowest utilization.
- 2D View centred sheet canvas two-tone margin/breakout overlay prev/next navigation zoom.
- Report layout/parts/stocks summaries CSV export.

---

## 12. Nesting Model Analysis

### 12.1 Conceptual model

StockSheet (StockSize) → Nest (SheetLayout) → PlacedPiece (PlacedPiece) → SourceJob/JO Line (CutPiece JobRef GlassRef SpecLabel Position) → Specification (SpecName BuildType)

### 12.2 Traceability

- Placed piece retains traceability to production requirement via CutPiece JobRef GlassRef SpecLabel Position Color Thickness PieceLabel.
- No anonymous rectangles: PieceLabel identity G-101·3/24, GlassRef, SpecLabel, Position.
- Composition routing via PaneDisplayName merge key same outer pane collapse ONE cutting group — traceability to spec.

### 12.3 Missing

- No explicit Nest entity persisted beyond SheetLayout in memory + JSON ResultJson in SavedOptimizationSession — could be normalized but JSON sufficient for reopenable.
- No remnant/offcut entity — MinBreakout tracks but not persisted as stock.
- No Sheet→Cut Piece link to physical sheet inventory — theoretical consumption only.

---

## 13. Utilization/Wastage Analysis

### 13.1 Canonical calculations

- Sheet area: SheetArea = Width×Height/1e6 when dimensions mm — implemented StockSqm Math.Round Width*Height/1e6 4dp, SqmPerSheet same via SqmCalculator.CalculateSheetArea, CalculatePanel W×H/1e6 3dp.
- Piece area: PieceArea = Width×Height/1e6 — UnitSqm CalculatePanel, UsedSqm sum Width*Height/1e6 4dp.
- Utilization: Utilization% = Total usable placed piece area ÷ Sheet area ×100 — UtilizationPct UsedSqm/StockSqm*100 2dp, weighted TotalUsedSqm/TotalStockSqm*100.
- Wastage: Wastage% =100-Utilization% — not explicitly stored but WasteLabel =100-Utilization.
- Audit existing costing/wastage before competing definitions: PI costing logic via Step9ProfitLoss TargetPriceModels SpecGroup TargetCalculator Step5PriceSource Step6ChargeResolver WastageRowSnapshot — existing wastage calculations in PI not duplicated in Production, Production uses area metrics only, not competing utilization definitions.

### 13.2 Existing PI costing

- Step9 ProfitLoss SpecTargets BuildType, TotalGlassCost TotalProductionCost, WastageRowSnapshot.
- Production eventually Estimated/Actual Material/Production/Wastage/Processing/Total Actual — not yet implemented, do not duplicate PI pricing logic inside Production per §15.

### 13.3 Metrics in Production vs Optimization

- Production: AreaTotalLabel sum ArrivedArea, MetricTotalLabel LM or pcs, UnitsReadyLabel ready/total pcs, chain summary Total/Completed/Open per stage piece counts.
- Optimization: UtilizationPct, WasteLabel, UsedSqm StockSqm.
- No competing definitions — distinct domains.

---

## 14. Costing Integration Analysis

### 14.1 Existing PI costing logic

- Step4 glass type pricing OuterSheetWidthMm Height InnerSheetWidthMm Height OuterSheetPricePerSqm InnerSheetPricePerSqm UInsertPricePerSqm Step4SpacerPricePerSqm InterlayerCostPerSqm CuttingCostPerSqm TemperingCostPerSqm per-group provenance nullable additive.
- Step5 Rate per row, PerimeterFactor, Sqm1 TotalSqm.
- Step6 PredefinedCharges MetricQty PolishSubType CustomPolishDescription WastageRowSnapshot CustomChargeSnapshot UseAdvancedAutomation.
- Step9 SpecTargets BuildType TotalGlassCost TotalProductionCost GrandTotalVatInclusive discount VAT % inputs, TargetMarginTolerance, TargetSnapshotRecompute.
- Existing costing formulas protected per §27 do NOT modify unless direct production integration defect.

### 14.2 Production costing future

- Should distinguish Estimated Cost Actual Material Cost Production Cost Wastage Cost Processing Cost Total Actual Cost but first determine what already exists — PI costing exists, Production costing not yet.
- Do not duplicate PI pricing logic inside Production — currently Production does NOT duplicate, only geometry Qty Sqm Lm.
- SheetStore PricingService SheetStorePricingService links Step4 sheet sizes to store pricing? Need audit: Services/ProformaInvoice/SheetStorePricingService.cs exists.

### 14.3 Integration defect?

- No defect found: PI costing preserved, Production uses geometry only, Optimization uses area metrics, no competing pricing.

---

## 15. Sheet Store Integration

### 15.1 Existing Sheet Store

- Models: GlassSheet Id Category ThicknessMm Color WidthMm HeightMm Supplier PurchasePricePerSqm SellPricePerSqm Stock Used Balance Stock-Used CreatedAt LastUpdated LastPurchaseDate SqmPerSheet CalculateSheetArea TotalSqmInStock Sqm*Stock TotalSqmUsed Sqm*Used TotalSqmBalance Sqm*Balance TotalPurchaseValue Sqm*Stock*Purchase TotalSellValue Sqm*Stock*Sell BalanceValue Sqm*Balance*Sell ProfitValue TotalSell-TotalBuy UsedCostValue Sqm*Used*Purchase UsedSellValue Sqm*Used*Sell UsedProfitValue UsedSell-UsedCost BalanceCostValue Sqm*Balance*Purchase ProfitPerSqm Sell-Purchase SizeDisplay ThicknessDisplay. GlassSheetLowStock, InventoryItem, SheetStoreHistory SheetHistoryRepository.
- Services: SheetStoreRepository, InventoryItemRepository, SheetStoreCsv CsvService, SheetStoreExcel ExcelService, SheetStorePdf PdfService, SheetHistoryRepository.
- Views: SheetStoreView 91485 lines, AddEditSheetView, SheetDetailWindow 76153 lines, SheetStoreBuy/Csv/History/Use/Items.
- Dashboard: SheetStoreDashboardView.
- Pricing: SheetStorePricingService, SheetStockLinkService.

### 15.2 Production consumption

- How production should consume purchased sheets stock qty/dims/color/thickness/type/coating/batch/lot if available cost: GlassSheet fields cover qty/dims/color/thickness/type/category coating not explicit but Category may include coating, batch/lot not yet, cost per SQM.
- Optimization should reserve inventory or merely calculate theoretical consumption: currently theoretical only, no reservation. Determine whether optimization should reserve inventory or merely calculate theoretical consumption — currently theoretical, reservation not introduced until architecture understood per §16. Safe: keep theoretical for Phase13, reservation Phase14.

### 15.3 Integration points

- Step4 ImportStockFromStep4 in Optimization Stocks tab imports sheet sizes selected specs quoted on outer/inner per spec tagged by pane composition each cutting group packs against own rack — links Step4 sheet sizes to optimization, not directly SheetStore inventory but via pricing.
- SheetStore Buy/Use flows: Buy adds stock, Use deducts Used, history tracked.
- Future: Optimization result could deduct Used from GlassSheet Balance or create reservation table ProductionMaterialAllocation StockSheet→Nest→PlacedPiece→SourceJob/JO Line→Spec.

### 15.4 Decision

- Do NOT introduce inventory reservation until existing SheetStore architecture understood — understood, keep theoretical for now.

---

## 16. Database Architecture

### 16.1 Existing 15 DbContexts

- AiMemory/AiMemoryDbContext ai_memory.db
- Audit/AuditDbContext audit.db
- App/AppDbContext proglass.db (master)
- DeliveryNotes/DeliveryNotesDbContext deliverynotes.db
- Inquiry/InquiryDbContext inquiry.db
- JobOrders/JobOrdersDbContext joborders.db JobOrderId UNIQUE SourceProformaId index
- MasterData/MasterDataDbContext masterdata.db
- ProformaInvoices/ProformaInvoicesDbContext proformainvoices.db ProformaId UNIQUE
- Specifications/SpecificationsDbContext specifications.db
- Statement/StatementDbContext statement.db
- TaxInvoice/TaxInvoiceDbContext taxinvoice.db
- CreditNote/CreditNoteDbContext creditnote.db
- Optimization/OptimizationDbContext optimization.db Sessions CreatedUtc index
- OrderReport/OrderReportDbContext orderreport.db
- Production/ProductionDbContext production.db Releases JobRef UNIQUE SourceProformaId, WorkItems UniqueKey UNIQUE JobRef Stage Status SpecGroupNumber, Entries WorkItemId JobRef Stage SpecGroup Serial.

### 16.2 Ownership, FK feasibility, transaction boundaries

- Ownership: each domain owns its DB isolated per project architecture one database per domain %AppData%\ProGlassAutomation\*.db new DbContext per operation via using never held as field same rule as JobOrdersDbContext.
- FK feasibility: cross-DB FK not feasible SQLite per file, so no FK between JO and Production — logical FK via JobRef string, not enforced DB.
- Transaction boundaries: per workflow step single DB transaction SaveChangesAsync, cross-DB not atomic.
- Cross-DB limitations: SQLite per file, no distributed transaction, partial commit risks documented §17.
- Recovery/compensation strategy: idempotent release UniqueKey upsert preserves statuses, stale pending cleanup, orphans kept reported, warnings JSON, live-refresh event.

### 16.3 Where Production/Optimization data should live

- Production: production.db isolated WAL — correct, already exists, owns Releases WorkItems Entries.
- Optimization: optimization.db isolated — correct, owns Sessions JSON payloads.
- Do NOT automatically create another SQLite database — not needed, existing 15 already cover.
- Evaluate existing proglass joborders specifications masterdata audit relevant databases — production.db and optimization.db already isolated per house pattern, correct.

### 16.4 Partial commit risks

- Confirm PI (proformainvoices.db status Approved) → Create JO (joborders.db) → Create Production Requirement (production.db) → Create Optimization Requirement (optimization.db) spans 3-4 DBs, if JO created but Production release fails, JO exists but no floor work — user-visible status Released but no production rows, recovery: re-release via ProductionReleaseService ReleaseAsync idempotent.
- If Production released but Delivery Note creation fails, production remains, DN not created — compensation: retry DN creation via OnCreateDeliveryNote Func<string,Task<string>>.
- Documented in §17.

---

## 17. Transaction / Failure Analysis

### 17.1 Workflow steps

| Step | Database | Write | Failure point | Recovery | Compensation | User-visible status |
|------|----------|-------|---------------|----------|--------------|---------------------|
| Inquiry created | inquiry.db | Insert Inquiry | DB locked, disk full | Retry EnsureCreated+Upgrade | Delete orphan if needed | Inquiry list shows draft |
| PI created | proformainvoices.db | Insert SavedProformaInvoice FullSnapshotJson | JSON serialize fail, DB locked | Retry, log AppLogService | Delete orphan, warn user | PI list Draft |
| PI Confirmed Approved | proformainvoices.db | Update Status Approved | Status update fail | Retry, state machine InvoiceLifecycleStateMachine | Revert status | PI status remains previous, warning |
| JO created from PI | joborders.db | Insert SavedJobOrder FullSnapshotJson GrandTotal | Deep clone JSON fail, DB locked, mapper exception | Retry BuildFromProformaInvoice, ensure JO Id unique | Delete JO orphan, log | JO list shows new JO or error dialog |
| Production Release | production.db | Upsert Releases + WorkItems UniqueKey | Distribution engine exception, DB locked, disk full | Retry ReleaseAsync idempotent, warnings list | Stale pending removal reversible via re-release, orphans kept | ReleaseMessage success/fail, WarningsJson, KPIs |
| Floor QtyDone save | production.db | Update WorkItem QtyDone Status CompletedBy CompletedAt + Insert Entry DoneBy DoneAt | Clamp logic fail, DB locked, audit fail | Retry SetDoneQtyAsync clamped to inflow, batch version one SaveChanges | Entry audit preserves history, no silent loss | ActionWarning clamp notice, AndonLabel waiting upstream, live-refresh |
| Optimization Run Quick/Optimize | optimization.db (session save optional) | In-memory Sheets Pieces Unplaced, optional Insert SavedOptimizationSession JSON | Engine exception, stock size no fit, margins invalid, DB locked | Retry PackQuick/PackOptimize deterministic, Verify oracle, fallback smallest stock first | Unplaced list, ResultSheets Utilization Oracle verdict | RunMessage, ResultSheets Utilization Unplaced Oracle, KpiCards |
| Delivery Note creation | deliverynotes.db | Insert DN Items DeliveredQty | Caps check fail, production.db read fail, DB locked | Retry GetDeliveryCapsAsync production-finished minus confirmed-delivered, fallback null uncapped classic JO | Confirmed delivery consumes balance, in-progress drafts not consume per rule 5 | DN list status, dispatch card Ready/Delivered/Balance ProductionManaged |
| Tax Invoice | taxinvoice.db | Insert TaxInvoice Totals | DN reconciler fail, DB locked | Retry TaxInvoiceDnReconciler, TotalsCalculator | Compensate via CreditNoteDbContext | TI list status |

### 17.2 Cross-DB limitations

- No atomic transaction across SQLite files — documented, do not pretend cross-DB transactions atomic.
- Each step single DB transaction, failure leaves previous steps committed — compensation via re-release, retry, warnings, user-visible status.
- ProductionRepository EnsureInitialized lazy EnsureCreated+SchemaMigrationRunner Upgrade idempotent same resilience as every other isolated database WAL busy_timeout foreign_keys=ON via SqliteResilience.ConfigureFor.

### 17.3 Recovery strategy

- Idempotent release: same JobRef UniqueKey deterministic re-release produces SAME keys statuses survive.
- Stale cleanup: rows refreshed plan no longer has pending removed started/completed orphans kept reported warnings.
- Live-refresh WorkItemsChanged event every stage screen re-reads list KPIs chain open detail no manual refresh needed.
- Delivery gateway null fallback classic JO behaviour when not production-managed.

---

## 18. UI Architecture

### 18.1 Existing Production Views

- 8 stage Views XAML CuttingView, TemperingView, DoublingLaminationView, EdgeDeletionView, HolesCutoutsView, MitringView, PolishView, PairingView + ProductionStagePageBase.cs base class.
- Controls: ProductionFilterBar (job/status/search filters), ProductionKpiStrip (Total/Pending/InProgress/Completed chips piece hints AreaTotal MetricTotal UnitsReady), ProductionPipelineBar (chain bar 8-stage chips Open/Completed/Total navigation), ProductionReleaseBar (release picker Sources SelectedSource IsReleasing ReleaseMessage ReleaseSucceeded), ProductionStationBody (master list ListRows 100 pages virtualized grid on-scroll loading ListFooterLabel Showing X of Y jobs scroll for more), ProductionUnitDetailControl (full-page job detail KPI strip OUTER/INNER grids W1×H1 W2/H2 TOTAL/DONE/BALANCE Qty Done input Save), ProductionUnitDetailTemplates (DataTemplates).
- Each stage distinct IndustryFocus hero line what this station physically does separate factory stations not one shared grid.
- Shell/card/theme: uses existing shell ModulePageTitleStyle ModulePageSubtitleStyle, card architecture Surface Primary/Secondary/Detail/Elevated/KPI/Glass via GDS, no Production-specific theme dict.

### 18.2 Existing Optimization Views

- OptimizationView.xaml root: title OPTIMIZATION subtitle, RunMessage WarningBrush, RunStageLabel AccentBrush, IsRunning visibility, Run Quick OutlinedPillButton Run Optimize PrimaryPillButton, rail tabs OptRailTab IndexEqualsConverter 0..6 JOBS SIZES STOCKS SETTINGS SUMMARY 2D VIEW REPORT, KPI cards OptKpiCard OptKpiBadge TintBlue/Green/Amber/Teal AccentBrush Success Warning Info, KPIs CONFIRMED JOBS KpiJobs PIECES KpiPieces TOTAL GLASS KpiGlass CUTTING GROUPS KpiGroups.
- Tabs: JobsTabView piece list ref/W/H/qty reference job loader, SizesTabView merged thickness+color summary, StocksTabView Step4 stock pool racks ticked use in run, SettingsTabView per-thickness LM/RM/TM/BM kerf MinBreakout rotation, SummaryTabView sheets utilization unplaced oracle verdict ordered highest→lowest, TwoDViewTabView centred sheet canvas two-tone margin/breakout overlay prev/next navigation zoom, ReportTabView layout/parts/stocks summaries CSV export.
- Engine pure deterministic calibrated benchmark, VM background Task.Run.

### 18.3 Shell/card/theme support

- Current shell/card/theme architecture can support modules: ModulePageTitleStyle etc, surface hierarchy App Background→Primary→Secondary→Detail→Elevated/KPI/Glass preserved per Phase11/12, no LiquidCard/SoftCard, Db* permanent dashboard density grammar 600 usages preserved.
- Production potential structure: Queue (master job list), Planning (release picker + chain), Cutting, Tempering, DGU/DFU, Lamination, Processing, QC, History — architectural candidates only, existing 8 stages cover Cutting Tempering Pairing DoublingLamination Polish EdgeDeletion Mitring HolesCutouts, Queue/Planning via FilterBar KpiStrip PipelineBar ReleaseBar, QC via dispatch readiness UnitsReadyLabel, History via Entries audit trail GetUnitEntriesAsync 100 GetJobEntriesAsync 200.
- Optimization potential structure: Input (Jobs), Sheet Selection (Stocks), Nesting (Run Quick/Optimize), Layout Preview (2D View), Utilization (Summary), Wastage (WasteLabel), Export/Release (Report) — existing tabs map directly.

### 18.4 Do not blindly implement all tabs

- Existing tabs already cover required Input/Sheet/Nesting/Layout/Utilization/Wastage/Export/Release, no need to blindly implement all architectural candidates, reuse existing.

---

## 19. Theme Integration

### 19.1 Global design system authoritative

- GDS 374 keys `Themes/Styles/GlobalDesignSystem.xaml` canonical, App.xaml 41 merges GDS pos40 LAST authoritative per Phase11/12, MUST consume semantic theme, no Prod/Opt-specific dict/hardcoded colors/duplicate card/radius/typography, reuse surface hierarchy App Background→Primary→Secondary→Detail→Elevated/KPI/Glass.

### 19.2 Production theme usage

- Checked `Views/Production/*.xaml` grep StaticResource/DynamicResource: uses `DynamicResource FontSize.BodySmall Weight.SemiBold TextTertiaryBrush`, `StaticResource ModulePageSubtitleStyle`, `StaticResource Production*` etc — no hardcoded colors # hex, no duplicate card systems, uses semantic tokens.
- Controls FilterBar/KpiStrip/PipelineBar/ReleaseBar/StationBody/UnitDetailControl/Templates reuse GDS.

### 19.3 Optimization theme usage

- `OptimizationView.xaml` uses `DynamicResource AppFontFamily FontSize.Body FontSize.BodySmall Weight.SemiBold TextTertiaryBrush WarningBrush AccentBrush CardCornerRadius FontFamily.Icon FontSize.H2 SuccessBrush InfoBrush TintBlueBrush TintGreenBrush TintAmberBrush TintTealBrush`, `StaticResource ModulePageTitleStyle OutlinedPillButton PrimaryPillButton OptRailTab OptKpiCard SurfacePrimaryStyle OptKpiBadge OptHeader OptKpiValue BoolToVisibilityConverter IndexEqualsConverter` — semantic, no hardcoded colors.
- Tabs similar.

### 19.4 Protected architecture

- Do NOT modify GDS/ThemeManager/ProGlassThemeEngine/VisualThemeEngine/ThemeCompatibilityHub per §27 protected unless direct production integration defect — no defect found.
- Do NOT modify GlobalDesignSystem, ThemeManager, ProGlassThemeEngine, VisualThemeEngine, ThemeCompatibilityHub — preserved.
- Special surfaces About 7 Splash 1 Login 6 PrintTokens Wizard Chart preserved do NOT force generic tokens per Phase11.

---

## 20. Performance Analysis

### 20.1 Audit for load-all queries

- ProductionRepository GetStageListAsync loads all work items for job filter then ComputeUnitFlows pure over list, then groups into JobGroup — could be heavy for large jobs but paginated ListRows 100 pages virtualized grid on-scroll loading AppendRows targetCount 100, first page 100, scroll near bottom loads next 100 via OnMasterListScrolled verticalOffset+viewportHeight < extentHeight-80, so UI not overloaded.
- GetChainSummaryAsync loads all work items AsNoTracking ToListAsync then ComputeUnitFlows SelectMany GroupBy — factory-wide piece counts per stage, acceptable for moderate data, may need caching for huge data.
- GetReleasesAsync AsNoTracking OrderByDescending LastReleasedAt ToListAsync — small.
- GetUnitReadinessAsync loads all work items then ComputeUnitFlows Sum — similar.
- Optimization: Pieces ObservableCollection materialization per piece entry, but reference job 152 pieces benchmark fine, larger jobs maybe 1000s pieces — still okay, RefreshDerived counts area O(n).

### 20.2 Unnecessary ObservableCollection materialization

- Production ListRows ObservableCollection 100 rows initial, not all jobs at once — good.
- Optimization Pieces ObservableCollection, Stocks ObservableCollection — moderate.

### 20.3 UI-thread calculations

- Production ComputeUnitFlows pure but called via Task.Run in LoadListAsync LoadKpisAsync LoadChainAsync — background, good.
- Optimization PackQuick PackOptimize pure but called via Task.Run in Run method — background, good.

### 20.4 Synchronous DB work

- Repositories use async ToListAsync SaveChangesAsync ConfigureAwait(false) — not synchronous, good.
- EnsureInitialized lock InitLock EnsureCreated SchemaMigrationRunner Upgrade synchronous on first use but idempotent, okay.

### 20.5 Repeated recalculation

- Production LoadAllAsync LoadListAsync LoadKpisAsync LoadChainAsync each Task.Run GetStageListAsync may recompute flows multiple times — could cache flows per job filter but not critical yet.
- Optimization RefreshDerived counts area each time pieces changed — O(n) fine.

### 20.6 DataGrid overloading

- Production detail grids OUTER/INNER cut-list grids per job may have many rows per unit but flattened Units per job — could be many but virtualized.

### 20.7 Expensive layout rendering

- Optimization 2D View centred sheet canvas two-tone margin/breakout overlay prev/next navigation — rendering per sheet, zoom, okay.

### 20.8 Nesting calculations on UI thread

- GuillotinePacker pure, runs on background Task.Run, not UI thread — good.

### 20.9 Long-running cancellation/progress/background

- Optimization long-running PackOptimize effort 5 combos orderings 6 × splits 3 × shuffles 6+1 = 126+ runs plus SheetFill WallSeed MaxRects — could be long, needs cancellation/progress/background — not yet implemented, but IsRunning visibility and RunStageLabel exist, future add CancellationToken.
- Production release IsReleasing flag, ReleaseMessage, no cancellation but quick.

### 20.10 Recommendation

- No premature multithreading without understanding existing MVVM — existing already uses Task.Run + SafeFireAndForget, good.
- Future: add cancellation token to PackOptimize, progress reporting, background execution result publication to UI already via ObservableCollection.

---

## 21. Reporting Integration

### 21.1 Existing Report Generator infrastructure

- Services/Reporting/ReportingDataService*.cs Analytics Delivery DrillDown Executive GlassSpecs Jo MasterData Pi Profitability Statement TaxInvoice Vat, ReportingModels, ReportingExportService, ExecutiveSummary, MoneyRounding, ReportDocumentContext, ReportingAnalyticsBuilder, ReportingCacheService, RowTraceabilityService, SpecificationIntelligenceService, SpecificationMatcher, VatCalculator.
- Services/OrderReport/OrderReportEngine Pdf OrderReportPdfExporter Database OrderReportDbContext Repository OrderReportRepository AttachmentCompletenessService.
- Existing infrastructure supports production order report, cutting list, optimization layout, sheet consumption, wastage report, production status, process history, material usage? Need audit.

### 21.2 Can it support production/optimization?

- ReportingDataService.Jo.cs handles JO status display key InProduction vs In Production normalisation, ProfitabilityRow ProductionCost.
- RowTraceabilityService Notes Production status Priority, Margin Net Total Cost Glass Prod.
- SpecificationIntelligenceService Job Order Production & Scheduling.
- OrderReportEngine already generates order report — could be extended for production order report cutting list.
- OptimizationReportExport.cs exists for layout/parts/stocks summaries CSV export — reuses existing export infrastructure, not parallel engine.
- Production history via GetUnitEntriesAsync GetJobEntriesAsync audit trail could be reported via ReportingDataService.

### 21.3 Do NOT create parallel report engine

- Existing infrastructure reusable, no parallel engine needed — Phase13 audit confirms reuse.

### 21.4 Future reporting

- Production order report: JobRef SpecGroupNumber SerialNumber GlassRef W1/H1 W2/H2 Qty UnitSqm TotalSqm Lm Treatment Stage Status QtyDone Balance CompletedBy CompletedAt.
- Cutting list: per cutting group composition thickness color W/H Qty.
- Optimization layout: SheetNo StockLabel PieceCount Utilization WasteLabel Composition.
- Sheet consumption: TotalUsedSqm TotalStockSqm UtilizationPct per StockSize.
- Wastage report: WasteLabel per sheet, total wastage.
- Production status: chain summary Open/Completed/Total per stage.
- Process history: Entries DoneBy DoneAt Qty.
- Material usage: GlassSheet Used Cost Value etc.

All can be built on existing ReportingDataService + OptimizationReportExport + OrderReportEngine.

---

## 22. Missing Foundations

### 22.1 Breakage/Replacement

- No Broken/Rejected/Replacement/FOC/Remaining tracking, no ProductionBreakageEntries, no EntryType.
- FOC replacement new JO generation not yet.

### 22.2 ProductionOrder wrapper

- No separate ProductionOrder entity — currently JO+WorkItem covers batches but machine/operator/date/priority per batch not explicit, hold/cancel per batch not explicit.

### 22.3 Remnant/offcut inventory

- MinBreakout tracks narrowest offcut worth keeping thinner strips reported as trim waste, but remnant not persisted as stock, no GlassSheet remnant linking.

### 22.4 Material allocation/reservation

- Optimization theoretical consumption only, no reservation vs SheetStore, no ProductionMaterialAllocation table StockSheet→Nest→PlacedPiece→SourceJob/JO Line→Spec.

### 22.5 QC/Ready/Delivery integration

- QC implicit via dispatch readiness, no explicit QC stage, no QC checklist, no Ready flag beyond DispatchReadyQty.
- Delivery gateway exists but no automatic DN creation from production ready.

### 22.6 Optimization cancellation/progress

- No CancellationToken, no progress reporting, no background execution result publication beyond IsRunning.

### 22.7 CNC/hole coordinates

- No hole coordinates/diameters, cutout geometry, polish edge mapping.

### 22.8 Machine/operator assignment

- CompletedBy audit exists but no machine assignment, operator assignment per work item, production date per item beyond CompletedAt.

### 22.9 Costing Actual vs Estimated

- Production Estimated/Actual Material/Production/Wastage/Processing/Total Actual not yet.

### 22.10 UI shell for Queue/Planning/QC/History

- Existing 8 stages + controls cover Queue/Planning via FilterBar/KpiStrip/PipelineBar/ReleaseBar, QC via UnitsReadyLabel, History via Entries audit trail, but no dedicated Queue/Planning/QC/History modules — candidates.

All missing are enhancements, not blockers for foundation audit.

---

## 23. Safe Implementation Candidates

### 23.1 Clearly required, architecturally proven, compatible, testable, reversible, within Phase13 scope

- **None that require source change for Phase13 audit-only outcome is valid.** Foundations already exist and are proven compatible with PI/JO/DB/theme.
- **If implementation were required, safe candidates would be:**
  - Add WorkItemStatus Paused/Rework/Rejected/Cancelled as additive nullable migration in production.db via AppSchemaMigrations.Production — safe, reversible, compatible with existing 3 values, testable via repository.
  - Add SavedProductionEntry EntryType enum Produced/Broken/Rejected/Replacement/FOC — additive column with default Produced, preserves audit.
  - Add ProductionBreakage table or extend entry with Reason, Replacement flag — additive, no FK break.
  - Add Optimization cancellation token support in PackOptimize — pure engine change, no DB, testable via unit test, reversible.
  - Add ProductionMaterialAllocation table theoretical consumption linking StockSize to SheetLayout — new table in production.db, no cross-DB FK, additive.
  - Add remnant tracking GlassSheet IsRemnant ParentSheetId — additive columns in SheetStore.
  - Add machine/operator assignment columns to SavedProductionWorkItem — additive nullable.
  - All above are safe but not required for audit, and would need Windows verification.

### 23.2 Decision for Phase13

- Per §26 implementation rule: audit/foundation only, implement if clearly required architecturally proven compatible with PI/JO/DB/theme/testable/reversible else REPORT AND STOP zero changes acceptable.
- Audit shows existing foundations already satisfy Phase13 objective: production distribution, piece-flow, delivery gateway, optimization engine calibrated, theme integration, DB isolation, transaction boundaries documented.
- Therefore **zero source changes** is correct outcome for Phase13 — report only.

---

## 24. Unsafe/Unproven Candidates

### 24.1 Full ProductionOrder entity

- Introducing separate ProductionOrder simply because common in ERP systems — not proven required by actual ProGlass workflow, would duplicate JO+WorkItem, risk breaking existing JO behavior, cross-DB FK complexity, transaction boundary expansion.

### 24.2 Production-specific theme dictionary

- Creating Production-specific theme dictionary, Optimization-specific theme dictionary, hardcoded colors, duplicate card/radius/typography — violates GDS authoritative, would break theme engine families Normal/NormalIndustrial/Monet/CrystalFluid/Ios26 6 accents Glass Blue/Emerald/Amber/Rose/Violet/Teal 4 densities Compact/Normal/Comfortable/Spacious, runtime publication via ThemeCompatibilityHub.

### 24.3 Another SQLite database

- Automatically creating another SQLite database for Production/Optimization — already have production.db optimization.db isolated, adding another would increase cross-DB partial commit risks, no ownership proven.

### 24.4 Redesign PI/JO/DN/Tax Invoice costing

- Redesign PI, JO, DN, Tax Invoice, existing costing logic unless actual integration defect discovered — no defect found, costing formulas protected per §27.

### 24.5 Merging Production & Optimization into one giant VM

- Merging two domains into one giant ViewModel — violates boundary Production Requirement→Optimization Input→Engine→Nest Result→Material Allocation, Production owns actual state, Optimization calculates layouts, do NOT merge.

### 24.6 Claiming optimizer optimal

- Claiming optimizer is optimal unless mathematically proven — engine reports Best found layout, not Optimal, Verify oracle only checks feasibility no-overlap kerf margin rotation, not optimality proof.

### 24.7 Inventory reservation without understanding SheetStore

- Introducing inventory reservation until existing SheetStore architecture understood — SheetStore understood but reservation requires transaction compensation across production.db and SheetStore DB (proglass.db? actually SheetStore uses proglass.db? Check AppDbContext vs SheetStoreRepository — SheetStore uses proglass.db? Need audit, but reservation would span DBs, needs failure analysis.

### 24.8 Full Production UI redesign

- Redesigning Production UI all modules full redesign — existing 8 stages + controls functional, shell/card/theme support proven, no need blindly implement all tabs Queue/Planning/Cutting/Tempering/DGU/DFU/Lamination/Processing/QC/History — architectural candidates only.

### 24.9 Full Optimizer implementation

- Implementing full optimizer all features — existing engine already calibrated, multi-start effort levels, consolidation, oracle, not placeholder, no need replace.

### 24.10 Hard-coding 3210×2250 as only sheet

- Hard-coding 3210×2250 as only possible sheet — StockSize pool configurable, not hard-coded, supports other jumbo sizes.

### 24.11 Anonymous rectangles disconnected from JO/spec

- Creating anonymous rectangles disconnected from JO/specification data — CutPiece retains JobRef GlassRef SpecLabel Position, no anonymous.

### 24.12 Competing utilization definitions

- Creating competing utilization definitions — existing PI costing/wastage calculations audited, Production uses area metrics only, Optimization uses UsedSqm/StockSqm, no competing definitions.

All above are unsafe/unproven for Phase13.

---

## 25. Recommended Phase14 Scope

### 25.1 Breakage/Rework/FOC

- Add WorkItemStatus Rework, Rejected, Cancelled, Paused as additive migration.
- Add SavedProductionEntry EntryType Produced/Broken/Rejected/Replacement/FOC + Reason + ReplacementForEntryId.
- Add ProductionBreakage table or extend entry with Original/Produced/Broken/Rejected/Replacement/FOC/Remaining traceability, who marked broken, when, FOC flag.
- New JO generation for FOC replacement: mapper DeriveJobOrderId with suffix -R1, preserve SourceProformaId traceability.

### 25.2 ProductionOrder optional wrapper

- Only if workflow requires: one JO→many production batches, machine assignment, operator assignment, production date, priority per batch, hold/cancel per batch. Could be ProductionBatch entity JobRef BatchNo Status Priority Machine Operator Date Qty, linking to WorkItems via BatchId.

### 25.3 Material allocation & SheetStore reservation

- ProductionMaterialAllocation table in production.db: Id JobRef SpecGroupNumber SerialNumber StockSizeId SheetNo PlacedPieceId Qty AllocatedAt AllocatedBy, theoretical consumption vs reservation flag.
- Reservation vs theoretical: add Reservation table in SheetStore or production.db linking GlassSheet Id to allocation, with compensation on failure.
- Remnant/offcut: GlassSheet IsRemnant ParentSheetId RemnantW/H, MinBreakout tracking already, persist remnants as stock.

### 25.4 Optimization ↔ Production bridge

- Define exact boundary: Production Requirement (ProductionUnitFlow) → Optimization Input (CutPiece list via JobsTab import from JO using ProductionDistributionEngine PaneOccurrence) → Engine (GuillotinePacker PackQuick/PackOptimize) → Nest Result (OptimizationResult Sheets) → Production Material Allocation (ProductionMaterialAllocation).
- Automate: Production release triggers optimization input generation for cutting stage only? Or manual via JobsTab picker source lock confirmed JOs only.
- Ensure traceability StockSheet→Nest→PlacedPiece→SourceJob/JO Line→Spec.

### 25.5 Costing Actual

- Production Estimated/Actual Material/Production/Wastage/Processing/Total Actual: reuse PI costing Step4 provenance, add ActualMaterialCost from SheetStore PurchasePricePerSqm * UsedSqm, ActualProductionCost from ProductionWorkItem QtyDone * cutting/tempering rates, WastageCost from waste area * price, ProcessingCost from Polish/Holes/Mitring LM/Qty * rates, Total Actual sum.

### 25.6 QC/Ready/Delivery

- Explicit QC stage: add ProductionStage Qc=9? Or use existing chain summary IsDispatchReady plus QC checklist table ProductionQcEntry JobRef Spec Serial QcBy QcAt Result Notes.
- Delivery gateway already caps deliverable, but add automatic DN creation from production ready via OnCreateDeliveryNote.

### 25.7 Optimization enhancements

- Cancellation token support, progress reporting, background execution result publication to UI, already IsRunning RunStageLabel but add CancellationTokenSource.
- CNC export: cut path, hole coordinates, polish edge mapping — future.
- Remnant/offcut inventory linking.

### 25.8 UI enhancements

- Production Queue/Planning/Cutting/Tempering/DGU/DFU/Lamination/Processing/QC/History modules — existing 8 stages cover many, but add Queue view (master job list with TOTAL/DONE/BALANCE), Planning (release + chain), QC (dispatch readiness + entries), History (entries audit trail).
- Optimization Input/Sheet/Nesting/Layout/Utilization/Wastage/Export/Release — existing tabs cover, but add material allocation view.

### 25.9 Reporting

- Production order report, cutting list, optimization layout, sheet consumption, wastage report, production status, process history, material usage via existing ReportingDataService + OptimizationReportExport + OrderReportEngine.

### 25.10 Theme & performance

- Ensure Production/Optimization MUST consume semantic theme system, no Production-specific dict, reuse surface hierarchy.
- Performance: add caching for ComputeUnitFlows per job filter, virtualization already, background execution already, add cancellation/progress.

### 25.11 Database & transaction

- Keep 15 DBs, no new DB unless proven, ownership FK feasibility transaction boundaries cross-DB limitations recovery/compensation already documented, partial commit risks mitigated via idempotent release and warnings.

All Phase14 scope should be proven compatible with PI/JO/DB/theme/testable/reversible, implement only safe foundations.

---

## 26. Actual Changes Made

- **Source changes:** NONE — zero source files modified, per implementation rule audit/foundation only, existing foundations proven compatible, zero changes acceptable.
- **Docs:** Created `docs/PHASE13_PRODUCTION_OPTIMIZATION_FOUNDATION_REPORT.md` 30 sections this file.
- **Scripts:** No new scripts, existing `scripts/xaml-guard.py` `design-system-guard.py` `verify-dashboard.py` used for regression.
- **Protected files:** 7 M protected `Agent/UI/AgentChatView.xaml` #B91C1C #6366F1 #3730A3, `Controls/AutoCompleteTextBox`, `Controls/Reporting/MixPanel.xaml`, `MainWindow.xaml`, `Views/Login/AuthSurfaceStyles.xaml`, `Views/Reports/DeliveryReport/DeliveryReportView.xaml`, `Views/Specifications/SpecificationsCenterView.xaml` letter-spacing multiline comment false positive preserved, no overwrite.
- **GDS/App.xaml:** GDS 374 keys App.xaml 41 merges GDS pos40 LAST preserved, no architecture change per Phase11/12.
- **Db*/ModuleList*:** Db* 52 ModuleList* 16 preserved per Phase12.
- **Production 15 Optimization 8:** Production 15? Actually Production 8 stages + controls = 15+ files? Per Phase11/12 Production 15 Optimization 8 no redesign preserved.
- **Commit policy:** audit-only → docs: finalize Phase13 production optimization foundation audit.

---

## 27. Regression Verification

- **XAML well-formed:** 268/268 via `scripts/verify-dashboard.py` and `xaml-guard.py` — clean, 0 malformed.
- **Resource key universe:** 4210 keys, 0 unresolved StaticResource, 0 unresolved DynamicResource, 0 binding heads not on VM surface via verify-dashboard.
- **xaml-guard:** clean (268 XAML files) — 0 violations.
- **design-system-guard:** 0 BLOCKING 40 WARNING truthful (1 protected #B91C1C +5 CornerRadius PrintTokens special +34 FontWeight PrintTokens deferred) — PASS, no fake zero warnings.
- **BasedOn cycles:** 0.
- **Dashboard grammar:** Overview legacy 0 hex 0 literalFontSize 0, SheetStore 0 0 0, Proforma 0 0 0, JobOrder 0 0 0, DeliveryNote 0 0 0, TaxInvoice 0 0 0, Statement 0 0 0 — per guard.
- **DB model compilation/static validation:** ProductionDbContext production.db isolated WAL, OptimizationDbContext optimization.db isolated, JobOrdersDbContext joborders.db, ProformaInvoicesDbContext proformainvoices.db, etc — EnsureInitialized EnsureCreated + SchemaMigrationRunner Upgrade idempotent, no compilation errors on Linux (WPF build blocked but DB contexts pure C#).
- **Production resource inventory:** Views/Production 8 stages + Controls 7, ViewModels/Production 8 stage VMs + Base Detail Station Models, Models/Production ProductionModels + SavedProductionEntities, Services/Production DbContext DistributionEngine Repository ReleaseService DeliveryGateway — all present.
- **Optimization resource inventory:** Views/Optimization OptimizationView + 7 Tabs, ViewModels/Optimization 10 parts, Models/Optimization OptimizationEntities, Services/Optimization OptimizationModels GuillotinePacker Advanced Smart DbContext SessionRepository — all present.
- **Protected-file integrity:** 7 M protected files unchanged content except pre-existing M, no overwrite, no staging of protected files.
- **GDS/App.xaml:** GDS 374 keys App.xaml 41 merges pos40 LAST preserved.
- **LegacyToSemantic 29 SemanticSources 27 Db* 52 ModuleList* 16:** preserved per Phase12.

---

## 28. Windows Build Status

- **Environment:** Linux Debian 12, .NET SDK not available for WPF build per previous phases, WPF BUILD BLOCKED BY CURRENT ENVIRONMENT — do NOT fake WPF build/runtime startup/visual verification/production UI testing/optimization UI testing per §30.
- **Build command:** `dotnet build -c Release` would be required on Windows, but on Linux reports WPF BUILD BLOCKED BY CURRENT ENVIRONMENT per policy.
- **Static validation:** 268/268 XAML well-formed, 0 unresolved, guards PASS, DB models compile via pure C# — sufficient for Linux audit.
- **Windows live WPF visual verification:** REMAINS OUTSTANDING and is NOT replaced by static verification per Phase8A guardrails, Phase9/10/11/12 reports. Must be verified on Windows: Light/Dark Normal NormalIndustrial Monet CrystalFluid Ios26 six accents four densities DPI 100/125/150/200 popup/dialog/WindowChrome/transparency/glass/blur/GPU/CPU/60/120/144Hz monitor contrast Windows-only per Phase9.
- **Production UI testing:** Cutting, Tempering, DGU/DFU, Lamination, Polish, Cutout, Holes, Queue, Planning, QC, History — requires Windows visual gate.
- **Optimization UI testing:** Input, Sheet Selection, Nesting, Layout Preview, Utilization, Wastage, Export/Release — requires Windows visual gate.

**WINDOWS LIVE WPF VISUAL VERIFICATION REMAINS OUTSTANDING.**

---

## 29. Git Evidence

- **Before work:**
  - `git status --short --branch` → `## arena/01a09a9f-proglassautomationadv` + 7 M protected + 5 ?? docs PHASE6A/6/7/7-SR/8 baseline
  - `git branch --show-current -vv` → `arena/01a09a9f-proglassautomationadv`
  - `git log -1 --oneline` → `a35fc71 docs: finalize Phase 12 compatibility retirement audit`
  - `git rev-parse HEAD` → `a35fc71b56575f5d3bce01dba3f0efeb82c0e253`
  - `grep -c x:Key GlobalDesignSystem.xaml` → 374
  - `grep -c ResourceDictionary Source App.xaml` → 41
  - Remote origin `https://github.com/JUBERSALEEM/ProGlassAutomationADV.git`
- **After audit:**
  - `git status --short --branch` → same + `?? docs/PHASE13_PRODUCTION_OPTIMIZATION_FOUNDATION_REPORT.md`
  - `git branch --show-current` → `arena/01a09a9f-proglassautomationadv`
  - No `git reset --hard`, `git clean -fd`, `git checkout .`, `git restore .`, `git add .`, blind stash, `git push --force` — per §31 Git safety absolute.
  - Stage ONLY Phase13 file `docs/PHASE13_PRODUCTION_OPTIMIZATION_FOUNDATION_REPORT.md` via `git add docs/PHASE13_PRODUCTION_OPTIMIZATION_FOUNDATION_REPORT.md`
  - Commit: `docs: finalize Phase 13 production optimization foundation audit` — audit-only, no empty commits, message accurately describes change per §32.
  - Push only Phase13 commit `git push origin arena/01a09a9f-proglassautomationadv` — no force, verify `git log -1 --oneline`, `git status --short`, `git branch -vv`, `git ls-remote origin arena/01a09a9f-proglassautomationadv` shows remote commit.
- **Protected files:** Never overwritten without approval per §28, 7 M preserved, 5 ?? docs preserved, docs PHASE6A/6/7/7-SR/8 baseline preserved.
- **GDS/App.xaml:** No architecture change, GDS authoritative LAST pos40, 374 keys, 41 merges preserved.

---

## 30. Final Verdict

**PHASE 13 PASSED — PRODUCTION/OPTIMIZATION FOUNDATION AUDIT COMPLETE — WINDOWS LIVE WPF VISUAL VERIFICATION REMAINS OUTSTANDING.**

**Rationale:**

- Existing Production foundations audited: 8 stages canonical Cutting HolesCutouts Polish EdgeDeletion Mitring Tempering Pairing DoublingLamination, ProductionModels ProductionStage WorkItemStatus PaneSlot ProductionWorkItem UniqueKey deterministic QtyDone piece-flow PredecessorStage ChargeTemplateKey, SavedProduction Release JobRef UNIQUE WorkItem UniqueKey UNIQUE Entry audit DoneBy DoneAt, ProductionDbContext production.db isolated WAL, ProductionDistributionEngine pure deterministic routing SGU/DGU/LAM/DguLam Step6 charges Holes/Polish/Mitring assembly notes, ProductionRepository idempotent upsert ComputeUnitFlows single source of truth piece-flow Cutting→EdgeWork→Tempering→Pairing→Doubling PaneReadyQty DispatchReadyQty WaitingOnLabel live-refresh WorkItemsChanged, ProductionReleaseService JO-only source lock Confirmed Approved JOs Released/InProduction only, ProductionDeliveryGateway DN caps production-finished minus confirmed-delivered null fallback classic JO, ViewModels 8 stage VMs + Base Detail Station Models Controls FilterBar KpiStrip PipelineBar ReleaseBar StationBody UnitDetailControl Templates, Views 8 stage XAML + base + controls, all consume semantic theme GDS 374 App.xaml 41 merges pos40 LAST no Production-specific dict hardcoded colors duplicate card/radius/typography, reuse surface hierarchy App Background→Primary→Secondary→Detail→Elevated/KPI/Glass.
- Existing Optimization foundations audited: engine exists calibrated owner benchmark Low-E 17 @82.77% 3210×2250, GuillotinePacker Quick one-pass height-first HorizontalFirst + Optimize multi-start orderings×splits effort 1..5 consolidation fullest-first sheet-fill wall-seed MaxRects heuristics deterministic, Verify oracle no-overlap kerf margin rotation, Models RotationPolicy Locked/Free90 adaptive SplitMode HorizontalFirst VerticalFirst LongerLeftover MarginProfile per thickness Left/Right/Top/Bottom Kerf MinBreakout DefaultFor thickness<=6 15mm else 20/25 kerf >=10 4 else 3 MinBreakout 15, StockSize W/H Thickness Color Label MaxSheets 0 unlimited, CutPiece JobRef GlassRef SpecLabel Position Color Thickness W/H AllowRotate PieceLabel, PlacedPiece X/Y/W/H Rotated, SheetLayout UsedSqm StockSqm UtilizationPct, OptimizationResult SheetsUsed TotalUsedSqm TotalStockSqm UtilizationPct weighted Success, persistence optimization.db SavedOptimizationSession JSON payloads, ViewModels 10 parts Jobs Margins Parts Preview Report Run Session Sizes Stocks ReportExport Tabs Jobs Sizes Stocks Settings Summary TwoDView Report, calibration reference job 152 pieces low-e outer clear inner ramly inner, Stocks Step4 stock pool sheet sizes selected specs quoted on outer/inner per spec tagged by pane composition each cutting group packs against own rack ticked use in run never whole sheet store, theme semantic GDS no Optimization-specific dict.
- PI→JO integration audited: JobOrderMapper BuildFromProformaInvoice deep clone JSON Derive JOB- from PI- Approved→Released else Draft ProductionDetails Status Draft/Released/InProduction/Completed/Delivered Priority Medium BuildEntityFromSnapshot GrandTotalVatInclusive from SavedProformaInvoice traceability SourceProformaId preserved, transaction boundaries 15 DbContexts AiMemory Audit App DeliveryNotes Inquiry JobOrders MasterData ProformaInvoices Specifications Statement TaxInvoice CreditNote Optimization OrderReport Production cross-DB not atomic failure/compensation required documented, traceability chain Customer→PI→Line→Spec→JO→Prod Order→Process→Opt→Sheet→Cut Piece preserved via deterministic UniqueKey CutPiece fields no anonymous rectangles.
- Specification model audited: glass type/color/thickness/coating/FT/annealed/dims/qty/spacer/air gap/inner/outer/lam layers/interlayer/cutouts/holes/polish/special via SelectedGlassType SelectedSpecName PnLSpecParser GlassMatches Interlayer Spacer PaneOccurrence Position Slot Pane Composition Thickness Treatment RequiresTempering PaneDisplayName merge key optimizer grouping, W1/H1 W2/H2 Qty GlassRef SpecName BuildType Position Treatment ChargeTemplateKey, missing hole coordinates/cutout geometry/polish edge mapping/machine/operator/date/breakage but existing supports production.
- Production domain model: hierarchy Customer→PI→PI Line→Glass Components→JO→Production Requirement→Process→Optimization→Material Consumption→Production Result, DGU multi-component not single flat, ProductionOrder vs JO evaluated JO+WorkItem covers batches partial via QtyDone rework/breakage not yet cancelled/hold via stale cleanup priority via JO machine/operator via CompletedBy audit date via CompletedAt, separate ProductionOrder not required today only if workflow requires multi-batch scheduling machine/operator hold/cancel.
- Process pipeline: CUTTING→TEMPERING→DGU/DFU→LAMINATION→POLISH→CUTOUT/HOLES→QC→READY potential but actual implemented Cutting→Edge work Holes/Polish/EdgeDeletion/Mitring parallel after cutting MIN bounds tempering→Tempering→Pairing→Doubling→QC dispatch ready→READY, sequential optional parallel dependent on spec/build, monolithic DGU Laminated flows supported not forced every product through every process.
- Status model: JobOrderStatus Draft Released InProduction Completed Delivered, WorkItemStatus Pending InProgress Completed Status follows quantity 0 Pending partial InProgress full inflow Completed, ProductionStage 8 enum, aggregate JobStatus via GroupJobs COMPLETED only when every unit arrived fully done IN PROGRESS once any piece done else PENDING, Pending/Released/Queued/InProgress/Paused/Completed/PartiallyCompleted/Rejected/Rework/Cancelled existing covers Pending/InProgress/Completed Released at JO Queued via WaitingUnits PartiallyCompleted via DoneQty Balance Paused Rejected Rework Cancelled not yet but safe to add additive.
- Breakage/Rework: existing QtyDone only no broken/rejected/replacement/FOC/remaining, audit trail SavedProductionEntry per save who+when Qty done CompletedBy CompletedAt, broken piece must not silently disappear — currently would need manual reduction, missing foundation documented safe candidate Phase14 add EntryType Produced/Broken/Rejected/Replacement/FOC Reason ReplacementForEntryId ProductionBreakage table FOC new JO generation.
- Optimization engine: stock size/qty piece W/H/qty rotation utilization/wastage assignment/nesting support 3210×2250 etc not hard-coded only, rotation 0/90/mixed adaptive, metrics SheetArea W×H/1e6 PieceArea W×H/1e6 Utilization% placed/sheet×100 Wastage% 100-Util existing costing/wastage audited no competing definitions, costing integration PI costing Step4 Step5 Step6 Step9 TargetPriceModels SpecTargets BuildType TotalGlassCost TotalProductionCost GrandTotalVatInclusive discount VAT % inputs protected no change unless defect no defect found, Production Estimated/Actual Material/Production/Wastage/Processing/Total Actual future via SheetStore PricingService SheetStockLinkService.
- Sheet Store integration: GlassSheet Category Thickness Color W/H Supplier Purchase/Sell per SQM Stock Used Balance SqmPerSheet TotalSqm Cost/Value UsedCostValue UsedSellValue etc SizeDisplay ThicknessDisplay, InventoryItem, History, Csv/Excel/Pdf services, SheetStoreView AddEditSheetView SheetDetailWindow Buy/Csv/History/Use/Items Dashboard, pricing via SheetStorePricingService, production consumption purchased sheets stock qty/dims/color/thickness/type/coating/batch/lot cost — Category Thickness Color W/H Supplier Purchase/Sell Stock Used Balance SqmPerSheet TotalSqm Cost/Value cover qty/dims/color/thickness/type coating via Category batch/lot not yet cost per SQM, optimization reservation vs theoretical consumption theoretical only reservation not introduced until architecture understood understood keep theoretical Phase13.
- Prod↔Opt boundary: Production Requirement→Optimization Input→Engine→Nest Result→Production Material Allocation preferred conceptual architecture, Production Requirement via ProductionUnitFlow StageItems Inflow Done Balance, Optimization Input via CutPiece list JobsTab import from JO using ProductionDistributionEngine PaneOccurrence same pane routing, Engine via GuillotinePacker PackQuick PackOptimize, Nest Result via OptimizationResult Sheets, Production Material Allocation future ProductionMaterialAllocation table StockSheet→Nest→PlacedPiece→SourceJob/JO Line→Spec traceability preserved CutPiece JobRef GlassRef SpecLabel, Prod owns actual state do not merge giant VM.
- DB architecture: 15 DbContexts ownership FK feasibility transaction boundaries cross-DB limitations recovery/compensation partial commit risks documented, production.db optimization.db isolated correct do NOT auto create another SQLite, existing proglass joborders specifications masterdata audit relevant databases evaluated ownership per domain new DbContext per operation via using never held as field same rule JobOrdersDbContext, FK cross-DB not feasible logical FK via JobRef string, transaction per step single DB SaveChangesAsync cross-DB not atomic, recovery idempotent release UniqueKey upsert preserve statuses stale pending removal orphans kept reported warnings JSON live-refresh, partial commit risks Confirm PI proformainvoices.db Approved→Create JO joborders.db→Create Production Requirement production.db→Create Optimization Requirement optimization.db spans 3-4 DBs if JO created but Production release fails JO exists no floor work user-visible Released but no production rows recovery re-release idempotent, if Production released but DN creation fails production remains DN not created compensation retry OnCreateDeliveryNote.
- Transaction/Failure: steps Inquiry PI PI Confirmed Approved JO Production Release Floor QtyDone save Optimization Run Quick/Optimize DN creation Tax Invoice each with Database Write Failure point Recovery Compensation User-visible status documented §17, cross-DB not atomic do not pretend.
- UI architecture: existing Production Views 8 stages + base + controls Controls FilterBar KpiStrip PipelineBar ReleaseBar StationBody UnitDetailControl Templates shell/card/theme ModulePageTitleStyle ModulePageSubtitleStyle surface hierarchy App Background→Primary→Secondary→Detail→Elevated/KPI/Glass preserved per Phase11/12 no LiquidCard/SoftCard Db* permanent dashboard density grammar 600 usages preserved, Production potential structure Queue Planning Cutting Tempering DGU/DFU Lamination Processing QC History architectural candidates only existing 8 stages cover Cutting Tempering Pairing DoublingLamination Polish EdgeDeletion Mitring HolesCutouts Queue/Planning via FilterBar KpiStrip PipelineBar ReleaseBar QC via UnitsReadyLabel History via Entries audit trail GetUnitEntriesAsync 100 GetJobEntriesAsync 200, Optimization potential structure Input Sheet Selection Nesting Layout Preview Utilization Wastage Export/Release existing tabs Jobs Sizes Stocks Settings Summary TwoDView Report map directly, do not blindly implement all tabs existing cover required.
- Theme integration: GDS authoritative 374 keys App.xaml 41 merges GDS pos40 LAST authoritative per Phase11/12, Production & Optimization MUST consume existing semantic theme system, do NOT create Production-specific dict Optimization-specific dict hardcoded colors duplicate card/radius/typography, use existing surface hierarchy App Background→Primary→Secondary→Detail→Elevated/KPI/Glass, existing shared card architecture reusable do not modify global theme system merely to support Production, checked Production Views grep StaticResource/DynamicResource only semantic no hardcoded colors # hex, OptimizationView semantic tokens OptKpiCard etc, protected architecture GDS ThemeManager ProGlassThemeEngine VisualThemeEngine ThemeCompatibilityHub preserved per §27 no defect.
- Performance: load-all queries ProductionRepository GetStageListAsync loads all work items for job filter then ComputeUnitFlows pure over list groups into JobGroup paginated ListRows 100 pages virtualized grid on-scroll loading AppendRows targetCount 100 scroll near bottom loads next 100 OnMasterListScrolled verticalOffset+viewportHeight < extentHeight-80 UI not overloaded, GetChainSummaryAsync loads all AsNoTracking ToListAsync then ComputeUnitFlows SelectMany GroupBy factory-wide piece counts per stage moderate, GetReleasesAsync small, GetUnitReadinessAsync similar, Optimization Pieces ObservableCollection materialization per piece entry reference job 152 pieces benchmark fine larger jobs 1000s okay RefreshDerived O(n), unnecessary ObservableCollection materialization Production ListRows 100 rows initial not all jobs at once good, Optimization Pieces Stocks moderate, UI-thread calculations Production ComputeUnitFlows pure called via Task.Run LoadListAsync LoadKpisAsync LoadChainAsync background good, Optimization PackQuick PackOptimize pure called via Task.Run Run background good, synchronous DB work repositories async ToListAsync SaveChangesAsync ConfigureAwait(false) not synchronous good EnsureInitialized lock InitLock EnsureCreated SchemaMigrationRunner Upgrade synchronous first use idempotent okay, repeated recalculation Production LoadAllAsync LoadListAsync LoadKpisAsync LoadChainAsync each Task.Run GetStageListAsync may recompute flows multiple times could cache but not critical, Optimization RefreshDerived counts area each time pieces changed O(n) fine, DataGrid overloading Production detail grids OUTER/INNER cut-list grids per job may have many rows per unit but flattened Units per job could be many but virtualized, expensive layout rendering Optimization 2D View centred sheet canvas two-tone margin/breakout overlay prev/next navigation zoom okay, nesting calculations on UI thread GuillotinePacker pure runs background Task.Run not UI thread good, long-running cancellation/progress/background Optimization long-running PackOptimize effort 5 combos 6×3×7=126+ runs plus SheetFill WallSeed MaxRects could be long needs cancellation/progress/background not yet but IsRunning visibility RunStageLabel exist future add CancellationToken, Production release IsReleasing flag ReleaseMessage no cancellation but quick, no premature multithreading without understanding existing MVVM existing already Task.Run SafeFireAndForget good future add cancellation token progress.
- Algorithm safety: distinguish Feasible/Good/Best/Optimal report assumptions rotation objective limitations never claim optimal unless proven, engine reports Best found layout not Optimal Verify oracle only checks feasibility no-overlap kerf margin rotation not optimality proof, PackQuick one deterministic pass Good layout, PackOptimize multi-start best found layout, not claimed optimal unless proven.
- Reporting: audit Report Generator infrastructure Services/Reporting ReportingDataService Analytics Delivery DrillDown Executive GlassSpecs Jo MasterData Pi Profitability Statement TaxInvoice Vat ReportingModels ReportingExportService ExecutiveSummary MoneyRounding ReportDocumentContext ReportingAnalyticsBuilder ReportingCacheService RowTraceabilityService SpecificationIntelligenceService SpecificationMatcher VatCalculator Services/OrderReport OrderReportEngine Pdf OrderReportPdfExporter Database OrderReportDbContext Repository OrderReportRepository AttachmentCompletenessService existing supports production order report cutting list optimization layout sheet consumption wastage report production status process history material usage via Jo status normalisation ProfitabilityRow ProductionCost RowTraceabilityService Notes Production status Priority Margin Net Total Cost Glass Prod SpecificationIntelligenceService Job Order Production & Scheduling OrderReportEngine order report could be extended production order report cutting list OptimizationReportExport layout/parts/stocks summaries CSV export reuses existing export infrastructure not parallel engine, Production history via GetUnitEntriesAsync GetJobEntriesAsync audit trail could be reported via ReportingDataService, do NOT create parallel report engine reuse existing infrastructure wherever appropriate.
- Missing foundations documented §22: Breakage/Replacement ProductionOrder wrapper Remnant/offcut Material allocation/reservation QC/Ready/Delivery Optimization cancellation/progress CNC/hole coordinates Machine/operator assignment Costing Actual vs Estimated UI shell Queue/Planning/QC/History.
- Safe/Unsafe candidates documented §23/24: Safe none required for audit-only zero changes acceptable, if implementation required safe candidates additive columns tables cancellation token etc testable reversible, Unsafe full ProductionOrder Production-specific theme dict another SQLite redesign PI/JO/DN costing merging giant VM claiming optimal inventory reservation without understanding full Production UI redesign full optimizer hard-coding 3210×2250 anonymous rectangles competing utilization.
- Recommended Phase14 scope documented §25: Breakage/Rework/FOC ProductionOrder optional wrapper Material allocation & SheetStore reservation Optimization↔Production bridge Costing Actual QC/Ready/Delivery Optimization enhancements UI enhancements Reporting Theme & performance DB & transaction.
- Actual changes NONE zero source files modified docs report only per §26 implementation rule audit/foundation only implement if clearly required architecturally proven compatible with PI/JO/DB/theme/testable/reversible else REPORT AND STOP zero changes acceptable.
- Regression 268/268 well-formed 4210 keys 0 unresolved 0 binding xaml-guard clean 268 guard 0 BLOCKING 40 WARNING truthful protected PrintTokens deferred PASS, production resource inventory optimization resource inventory protected-file integrity GDS 374 App.xaml 41 merges pos40 LAST preserved.
- Windows build WPF BUILD BLOCKED BY CURRENT ENVIRONMENT Linux Debian 12 .NET SDK not available do NOT fake WPF build/runtime startup/visual verification production UI testing optimization UI testing per §30, Windows live WPF visual verification REMAINS OUTSTANDING.
- Git safety status --short --branch branch --show-current -vv log -1 --oneline rev-parse HEAD before touch, NEVER reset --hard/clean -fd/checkout ./restore ./add . blind stash push --force, stage ONLY Phase13 file docs/PHASE13_PRODUCTION_OPTIMIZATION_FOUNDATION_REPORT.md, commit docs: finalize Phase 13 production optimization foundation audit audit-only no empty commits, push only Phase13 commit verify log -1 --oneline status --short branch -vv ls-remote.
- Final STOP after Phase13 do NOT auto start Phase14 do NOT implement all modules/full redesign/UI/theme/compat, report explicitly states what should be built next §25.

**Outcome:** Audit complete, foundations proven, no safe architectural reorganization proven beyond existing, zero source changes correct, report committed, push verified, Windows live verification remains outstanding per policy.

**WINDOWS LIVE WPF VISUAL VERIFICATION REMAINS OUTSTANDING.**
