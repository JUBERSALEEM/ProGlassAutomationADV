# Production Modules — Stage Queues

<!-- pg-nav -->
> **[Repository root](../../README.md)** · [All documents](../../docs/README.md) · [Application workflow](../../docs/WORKFLOW.md) · [Design system](../../docs/DESIGN_SYSTEM.md)
<!-- pg-nav -->

The eight production modules (Cutting · Holes & Cutouts · Polish ·
Edge Deletion · Mitring · Tempering · Pairing · Doubling/Lamination)
are **stage queues** fed by the production distribution engine.

**SOURCE LOCK:** production runs EXCLUSIVELY from **Job Orders** — a Job
Order only ever exists for a confirmed (Approved) PI. Drafts and other PI
statuses never reach the floor; only non-void **Released / In Production**
JOs are releasable.

**SCOPE LOCK:** only the four Step 4 build families — SGU / DGU / LAM /
DGU+LAM. No triple glazing; out-of-family names degrade with a warning.

```
Job Order (Released / In Production, non-void)
        │  ProductionReleaseService (loads the embedded invoice snapshot)
        ▼
ProductionDistributionEngine
        specs from Step 4/5 (build type, panes, treatments, W1/H1/W2/H2, Qty)
        work-type quantities from Step 6 (fan holes, polish LM, mitring LM, …)
        ▼
production.db (work items per stage, idempotent re-release)
        ▼
  Cutting → Holes → Polish → EdgeDel → Mitring → Tempering → Pairing → Double/Lam
        (PIECE FLOW: each stage records QtyDone; only completed pieces move
         to the next stage — write is clamped to what arrived upstream.
         A unit is READY FOR DISPATCH when its full quantity finishes the chain)
```

**Routing rules** — see `docs/PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md`:

- SGU rows carry **W1/H1 only**; FT / Heat-Strengthened / Heat-Soaked panes
  route Cutting → Tempering, Annealed panes Cutting → dispatch-ready.
- DGU / LAM / DGU+LAM rows split into pane occurrences (Outer W1/H1,
  Inner or Lam pair W2/H2), then Pairing → Doubling/Lamination.
- Step 6 charges feed the floor: FAN_HOLES / HOLES / CUTOUT /
  COUNTERSUNK_HOLES → Holes & Cutouts qty; POLISH / POLISH_MATCHED_LAM /
  EDGE_GRINDING → Polish LM; MITRING → Mitring LM; ARGON / SILICON /
  U-INSERT / OVERLAP / SHUTTER_BONDING → notes on the assembly item.

**Screens (redesign 2026-09-12)** — the eight modules are now **separate
pages**, not one shared screen. Each stage owns its own columns and its
own detail sidebar; only the *chrome* is shared (pipeline chain bar,
release bar, KPI strip, filter bar — small controls in `Controls/`).
Full design + analysis in
`docs/PRODUCTION_OPTIMIZATION_REDESIGN_2026-09-12.md`:

- **Module pages** (`CuttingView.xaml` … `DoublingLaminationView.xaml`) —
  each is a self-contained page with its own stage-identity header
  (distinct tint, icon tile, `Stage n of 8` badge, description, chain
  context) and a per-stage focus hero card in the detail sidebar.
  The **master grid columns are stage-specific**:
  - **Cutting** — full Step-5 envelope: ST · JOB · PI · CUSTOMER ·
    SALESMAN · SPEC · SR · REF · BUILD · W1 · H1 · W2 · H2 · QTY ·
    SQM1/TOT SQM1/LM1 · SQM2/TOT SQM2/LM2 · IN · DONE · BALANCE.
  - **Polish / Mitring** — LM-based: … QTY · LM1 · LM2 · IN · DONE · BALANCE.
  - **Holes & Cutouts / Edge Deletion / Tempering / Pairing / Doubling** —
    piece/unit flow: … QTY · IN · DONE · BALANCE (no pricing, no SQM/LM noise).
- **Pipeline chain bar** (`Controls/ProductionPipelineBar.xaml`) — all
  eight stages in floor order with factory-wide piece counts (open ·
  done/total); the current stage is highlighted and any chip opens that
  stage's module, so work can be followed stage by stage.
- **Release bar** (`Controls/ProductionReleaseBar.xaml`) — confirmed Job
  Order picker, release action, release message + clamped-write warning.
- **KPI strip** (`Controls/ProductionKpiStrip.xaml`) — arrived / pending /
  in progress / completed / load (m² + stage metric) / ready-for-dispatch;
  labels are stage-aware (LM vs pcs vs units).
- **Filter bar** (`Controls/ProductionFilterBar.xaml`) — search / job /
  status + the waiting-upstream backlog strip.
- **Piece flow (toolchain)** — every piece enters at Cutting; only the
  pieces a stage completes (QtyDone) arrive in the next stage. Floor
  writes are clamped to the arrived quantity
  (`ComputeUnitFlows` + `SetDoneQtyAsync`). Units with no pieces arrived
  yet are counted in a "waiting upstream" strip so the backlog stays
  visible; the chain bar's open count reflects arrived (actionable)
  pieces only. The grid is **virtualized and paginated** — 100 rows at a
  time, next 100 on scroll (footer shows progress).
- **Piece flow (toolchain)** — every piece enters at Cutting; only the
  pieces a stage completes (QtyDone) arrive in the next stage. Floor
  writes are clamped to the arrived quantity
  (`ComputeUnitFlows` + `SetDoneQtyAsync`). Units with no pieces arrived
  yet are counted in a "waiting upstream" strip so the backlog stays
  visible; the chain bar's open count reflects arrived (actionable)
  pieces only.
- **Tier 2 · Unit detail sidebar** — selecting a row opens the job header
  (Job No, PI No, Customer, Project, Salesman) with **Open Job Order**
  and **Open Proforma Invoice** cross-navigation (checks Step 5 sizes /
  Step 6 charges in the source documents), plus the pane-level stage
  items each showing IN / DONE / BALANCE and a done-piece editor
  (Save clamped to IN, All, Reset). The sidebar also shows the unit's
  **stage totals** (total · done · balance in pieces, SQM and LM —
  every module) and a **dispatch panel**: READY / DELIVERED / TO
  DELIVER pieces with **Create Delivery Note**.
- **Production → Delivery Notes** — finished pieces flow into delivery
  notes automatically (`ProductionDeliveryGateway`): a DN row may only
  deliver production-finished pieces minus confirmed deliveries, and
  each next DN is created from remaining balance only, so dispatched
  pieces are never delivered twice. SGU pieces dispatch straight off
  the pane chain (no pairing/doubling); multi-pane units dispatch what
  finished doubling.

**Key files**

| File | Role |
|---|---|
| `CuttingView.xaml` … `DoublingLaminationView.xaml` | Eight SEPARATE stage pages (own columns + detail) |
| `ProductionStagePageBase.cs` | Code-behind plumbing: initial load + 100-row scroll feed |
| `Controls/ProductionPipelineBar.xaml` | Shared pipeline chain bar (chrome) |
| `Controls/ProductionReleaseBar.xaml` | Shared release bar (chrome) |
| `Controls/ProductionKpiStrip.xaml` | Shared KPI strip (chrome) |
| `Controls/ProductionFilterBar.xaml` | Shared filter bar + waiting-upstream strip (chrome) |
| `ViewModels/Production/ProductionStageViewModelBase.cs` | List + detail, filters, KPIs, floor actions, cross-nav hooks, stage identity |
| `Services/Production/ProductionDistributionEngine.cs` | Snapshot → work items (pure, deterministic) |
| `Services/Production/ProductionReleaseService.cs` | Release picker + orchestration |
| `Services/Production/ProductionRepository.cs` | production.db persistence + stage list/detail projections |

<!-- pg-nav -->
---

*This README documents `Views/Production/`. For the end-to-end business chain see [`docs/WORKFLOW.md`](../../docs/WORKFLOW.md); for every other document see [`docs/README.md`](../../docs/README.md).*
<!-- pg-nav -->
