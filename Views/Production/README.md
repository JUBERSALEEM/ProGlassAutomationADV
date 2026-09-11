# Production Modules — Stage Queues

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
        (predecessor connectivity: completing out of order raises a warning;
         a unit is READY FOR DISPATCH when every stage item is completed)
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

**Screens (redesign 2026-09-11)** — every module is a two-tier
master–detail, full design + worked samples in
`docs/PRODUCTION_SCREENS_DESIGN_2026-09-11.md`:

- **Tier 1 · Unit list** — one row per unit, shaped like the Step 5 grid:
  Job / PI No · Customer · Salesman · Spec · Ref · Build · W1/H1 + W2/H2 ·
  Qty · Unit SQM · Total SQM · LM · stage progress (n/m). Charge work
  (fan holes, polish LM, mitring LM) shows as `(whole spec)` batch rows.
  Filters: free-text search, job, status. KPI chips: queue / pending /
  in progress / completed / load / ready-for-dispatch.
- **Tier 2 · Unit detail** — selecting a row opens the job header
  (Job No, PI No, Customer, Project, Salesman) with **Open Job Order**
  and **Open Proforma Invoice** cross-navigation (checks Step 5 sizes /
  Step 6 charges in the source documents), plus the pane-level stage
  items with Start / Complete / Reopen floor actions.

**Key files**

| File | Role |
|---|---|
| `ProductionStageView.xaml` | Shared master–detail screen hosted by all 8 module views |
| `ViewModels/Production/ProductionStageViewModelBase.cs` | List + detail, filters, KPIs, floor actions, cross-nav hooks |
| `Services/Production/ProductionDistributionEngine.cs` | Snapshot → work items (pure, deterministic) |
| `Services/Production/ProductionReleaseService.cs` | Release picker + orchestration |
| `Services/Production/ProductionRepository.cs` | production.db persistence + stage list/detail projections |
