# Production Distribution — Analysis & Implementation Plan

**Date:** 2026-09-11 · **Author:** Senior analysis (Arena session) · **Status:** Approved for implementation

Connects the 8 "coming soon" Production modules (Cutting, Holes & Cutouts,
Polish, Edge Deletion, Mitring, Tempering, Pairing, Doubling/Lamination) to the
specification data that already exists in the Proforma Invoice wizard
(Step 4 build types · Step 5 grid · Step 6 other-charges) and the Job Order
module.

---

## 1. What already exists (analysis findings)

| Asset | Location | Reuse |
|---|---|---|
| Spec-name grammar (single source of truth) | `ViewModels/ProformaInvoice/Step9ProfitLoss/PnLSpecParser.cs` | Parses "6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass" into panes, spacers, interlayers. Handles triples (3+ panes). Pinned by `Tests/Agent/SpecParsingTests.cs`. |
| Build-type resolver | `GlassBuildType` (Sgu/Dgu/Lam/DguLam) in `Services/ProformaInvoice/TargetPricing/TargetPriceModels.cs`; per-spec result persisted in `ProfitLossSnapshot.SpecTargets[].BuildType` | Step 9 already classifies every spec. |
| Pane↔dimension mapping | `SpecGroupColorSide.SideLabel` — "Outer (W1/H1)", "Inner (W2/H2)", "Lam Outer (W2/H2)", "Lam Inner (W2/H2)" | **W1/H1 = first pane · W2/H2 = second pane** (DGU/LAM). DGU+LAM laminated pair shares W2/H2. SGU uses W1/H1 only — W2/H2 are ignored. |
| Pane factor | `SpecGroupMetric.PaneFactor` — 1 = SGU, 2 = DGU/LAM, 3 = DGU+LAM hybrid | Physical panes = Qty × PaneFactor. |
| Treatments | Step 4 dropdowns: `FT`, `Annealed`, `Heat Strengthened`, `Heat Soaked` | Grammar matches the token but does NOT capture it yet — **gap closed in Phase 0**. |
| Work-type charges | Step 6 `PredefinedChargeTemplates`: POLISH (lm, 13 sub-types), POLISH_MATCHED_LAM (2×), FAN_HOLES / HOLES / CUTOUT / COUNTERSUNK_HOLES (qty), EDGE_GRINDING (lm), MITRING (lm), SANDBLASTING (sqm), ARGON_GAS, SILICON_BED, OVERLAP, SHUTTER_BONDING, U_INSERT, DELIVERY | Each persisted charge row carries `IsActive`, `MetricQty`, `PolishSubType`, `CheckedSpecGroupNumbers` (per-spec targeting, Chat 20). |
| Source documents | `SavedProformaInvoice.FullSnapshotJson` → `ProformaInvoiceSnapshot`; `SavedJobOrder.FullSnapshotJson` → `JobOrderSnapshot` embedding the PI snapshot immutably + mutable `ProductionDetails` | **Job Order is the production source of truth** (PI is a quotation; JO is the released factory order). Direct-from-PI release also supported for jobs without a JO yet. |
| Production module shells | 8 VMs inheriting `ComingSoonViewModelBase`; nav + DataTemplates already wired in `MainWindow.xaml` | VMs are promoted in Phase 3 — no navigation surgery needed. |
| DB pattern | Repository-owned lazy init: `EnsureCreated()` + `SchemaMigrationRunner.Upgrade` (see `JobOrderRepository.cs:28`); isolated `.db` per document family | New `production.db` follows the exact same pattern. |

### Routing facts confirmed with the business (2026-09-11)

1. **SGU** rows carry **W1/H1 only**. W2/H2 belong to multi-pane builds.
2. SGU **FT** (full tempered): Cutting → Tempering → ready for dispatch.
   SGU **Annealed**: Cutting only → ready for dispatch.
3. SGU with **fan holes**: hole qty is read from Step 6 (FAN_HOLES / HOLES
   charge qty) and routed to Holes & Cutouts — drilling happens **before**
   tempering (tempered glass cannot be drilled).
4. SGU with **polish**: glass ref, W1/H1 and the **polish total LM** come from
   Step 6 (POLISH charge) and route to Polish — also **before** tempering.
5. Heat Strengthened and Heat Soaked behave like FT for routing (both require a
   furnace pass); Annealed skips the furnace.

---

## 2. Stage routing matrix (the distribution engine)

One **work item** = one stage visit for one spec row (or one charge-driven
batch). `✔` = always generated · `S6` = generated when the Step 6 charge is
active for this spec · `per pane` = generated for each pane whose treatment
requires it.

| Build | Cutting | Holes & Cutouts | Polish | Edge Deletion | Mitring | Tempering | Pairing | Doubling / Lam |
|---|---|---|---|---|---|---|---|---|
| SGU Annealed | ✔ W1/H1 | S6 | S6 | manual queue | S6 | — | — | — |
| SGU FT / HS / Heat Soaked | ✔ W1/H1 | S6 (pre-temper) | S6 (pre-temper) | manual queue | S6 (pre-temper) | ✔ | — | — |
| DGU | ✔ both panes | S6 | S6 | manual queue | S6 | per pane | ✔ | ✔ DGU assembly (spacer; argon/silicone/U-insert flags from S6) |
| LAM | ✔ both panes | S6 | S6 (2× when matched) | manual queue | S6 | per pane | ✔ | ✔ Lamination (PVB/SGP/EVA from name) |
| DGU + LAM | ✔ pane 1 (W1/H1) + lam pair at W2/H2 ×2 | S6 | S6 | manual queue | S6 | per pane | ✔ | ✔ hybrid |
| Triple DGU (3 panes, 2 spacers) | ✔ pane 1 (W1/H1) + panes 2–3 (W2/H2) | S6 | S6 | manual queue | S6 | per pane | ✔ | ✔ (2 cavities) |

**Every work item carries:** JobOrderId, SourceProformaId, Customer, Project,
SpecGroupNumber, SpecName, BuildType, GlassRef, SerialNumber, Position
(Outer/Inner/Middle/Lam-Outer/Lam-Inner), W, H, Qty, SQM, LM, Treatment,
Stage, Status, and — for charge-driven items — the Step 6 metric qty and unit
(holes count / polish LM / …).

**Precedence rule encoded on items:** Holes · Polish · Mitring · Edge-Deletion
must complete **before** Tempering; Cutting before everything; Pairing before
Doubling/Lamination. v1 shows the precedence and warns on out-of-order
completion; hard locks are a later phase.

**Step 6 attribution:** charges persist `CheckedSpecGroupNumbers`
(empty = all specs). The engine emits one work item per
`(charge × targeted spec group)`; when a charge targets several specs the item
carries the shared qty with an explicit note (proportional split by piece
count = Phase 4 polish).

---

## 3. Implementation phases

### Phase 0 — Grammar: capture the treatment token
`PnLSpecParser.GlassPattern` gets a `(?<treat>…)` named group (ADDITIVE — the
matched language is unchanged, all 20+ pinned tests stay green). `PnLPane`
gains `Treatment`. New pins in `SpecParsingTests`.

### Phase 1 — Domain models + distribution engine (pure, no WPF)
* `Models/Production/` — `ProductionStage`, `WorkItemStatus`,
  `ProductionWorkItem`, `ProductionJobPlan`, `PaneSlot`.
* `Services/Production/ProductionDistributionService` — snapshot → plan.
  Deterministic ordering (spec #, serial, position, stage). Never throws on a
  bad name (falls back to SGU single pane, flags a warning — mirrors
  `SpecPaneResolver` honesty rule).
* `Tests/Agent/ProductionDistributionTests` — SGU FT vs Annealed, fan-hole
  qty from Step 6, polish LM from Step 6, DGU two-pane split, LAM matched 2×,
  DGU+LAM three panes, triple DGU, idempotent keys.

### Phase 2 — Persistence (`production.db`)
* `ProductionDbContext` (isolated file, same resilience config),
  entities `ProductionJob` + `ProductionWorkItem`,
  unique key on `(JobRef, SpecGroupNumber, SerialNumber, Position, Stage)` →
  **idempotent re-release** (status survives re-distribution).
* Repository with the house lazy-init pattern; migration list seeded in
  `AppSchemaMigrations` style.

### Phase 3 — Module UIs (replace the 8 coming-soon shells)
* Shared `ProductionStageViewModelBase`: job picker → stage queue grid
  (grouped by job), filters (status/spec/ref), Start/Complete actions,
  summary chips (pieces · SQM · LM · hole qty).
* Each of the 8 VMs = base + its stage + stage-specific columns.
* Job Order detail: **Release to Production** button (runs the engine,
  idempotent); Production modules also offer "import directly from a PI".

### Phase 4 — Floor polish (next iteration)
* Per-stage barcode/label printing, out-of-order hard locks, proportional
  charge splitting, Edge Deletion auto-suggestion for coated glasses,
  Sandblasting queue, dispatch-ready dashboard per job.

---

## 4. Explicit assumptions (flagged for owner confirmation)

1. Triple-glazed DGU rows: pane 1 = W1/H1, panes 2–3 cut at W2/H2 (the grid
   has two dimension pairs; the wastage model already maps Lam Outer/Lam Inner
   to the same W2/H2 pair).
2. Heat Strengthened / Heat Soaked route through the Tempering furnace like FT.
3. Production distributes from the **Job Order** (released order). Direct PI
   release is a convenience path, not the primary flow.
4. DELIVERY / per-trip charges and Wastages are commercial lines — they do not
   create floor work items.
