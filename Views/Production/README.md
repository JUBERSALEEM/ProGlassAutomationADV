# Production Modules — Stage Queues

The eight production modules (Cutting · Holes & Cutouts · Polish ·
Edge Deletion · Mitring · Tempering · Pairing · Doubling/Lamination)
are **stage queues** fed by the production distribution engine.

```
Job Order (released)  ─┐
                       ├─ ProductionReleaseService ─▶ ProductionDistributionEngine
Approved Proforma     ─┘         (loads the embedded invoice snapshot)
                                           │
        specs from Step 4/5 (build type, panes, treatments, W1/H1/W2/H2, Qty)
        work-type quantities from Step 6 (fan holes, polish LM, mitring LM, …)
                                           │
                                           ▼
                              production.db (work items per stage)
                                           │
                ┌──────┬──────┬──────┬──────┬──────┬──────┬──────┐
             Cutting Holes  Polish EdgeDel Mitre Temper Pair  Double/Lam
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

**Key files**

| File | Role |
|---|---|
| `ProductionStageView.xaml` | Shared queue screen hosted by all 8 module views |
| `ViewModels/Production/ProductionStageViewModelBase.cs` | Queue, filters, KPIs, floor actions |
| `Services/Production/ProductionDistributionEngine.cs` | Snapshot → work items (pure, deterministic) |
| `Services/Production/ProductionReleaseService.cs` | Release picker + orchestration |
| `Services/Production/ProductionRepository.cs` | production.db persistence (idempotent upsert) |
