# Production — Complete Step 4 / Step 5 Mapping Analysis & Module Upgrade

**Date:** 2026-09-11 · **Status:** Implemented
**Companion docs:** `PRODUCTION_SCREENS_DESIGN_2026-09-11.md`,
`PRODUCTION_MODULE_EXAMPLES_2026-09-11.md`,
`PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md`

Senior analysis of how Step 4 (spec build) and Step 5 (grid rows) feed the
eight production modules, the glass-industry size semantics confirmed by the
owner, and the industry-level detail each module now carries.

---

## 1. Data path: Step 4 → Step 5 → production

```
Step 4 (spec definition)
   build family (SGU / DGU / LAM / DGU+LAM)  ← ProfitLoss.SpecTargets.BuildType
   glass composition string ("8mm Clear FT + 12A Argon + 6mm Annealed…")
        │
Step 5 (estimator grid rows)
   Sr · GlassRef · W1 · H1 · W2 · H2 · Qty · SQM/LM maths (no pricing)
        │
ProductionDistributionEngine (pure, deterministic)
   ResolveBuildType  → build family (Step 4 wins; grammar-inferred fallback)
   ResolveAllPanes   → panes + treatments (FT / HS / HST / Annealed)
   ResolvePaneOccurrences → physical cuts per build (position ↔ size pair)
        │
production.db work items — one item per row × pane × stage
```

**Step 4 decides how many panes a unit has and which treatments they
carry; Step 5 decides sizes and quantities.** Production reads both — a
module is only as good as the spec it was released from, which is why the
source stays locked to confirmed Job Orders.

## 2. Pane build table (Step 4 → physical cuts)

| Build (Step 4) | Panes cut per unit | Positions | Sizes | Assembly stages |
|---|---|---|---|---|
| SGU | 1 | Glass | W1×H1 | none — dispatch off the pane chain |
| DGU | 2 | Outer + Inner | Outer W1×H1 · Inner W2×H2 (or inherited) | Pairing → Doubling (spacer/seal/gas) |
| LAM | 2 | Lam Outer + Lam Inner | W1×H1 · W2×H2 (or inherited) | Pairing → Lamination (interlayer) |
| DGU+LAM | 3 | Outer + Lam pair | Outer W1×H1 · pair W2×H2 (or inherited) | Pairing → Doubling + Lamination |

Interlayers (PVB/SGP) and spacers are **not cut** — they ride as assembly
notes (`LAMINATION · PVB 1.52MM CLEAR`, `DGU ASSEMBLY · SPACER 12MM ·
ARGON GAS FILL`).

**Quantities always follow the row:** qty 20 ⇒ outer 20 pieces **and**
inner 20 pieces (each pane occurrence carries the row qty). Pairing and
doubling carry 20 units.

## 3. W1/H1/W2/H2 size semantics (owner-confirmed rules)

| Row shape | Meaning | Engine behaviour |
|---|---|---|
| W2/H2 blank (0/0) on DGU/LAM/DGU+LAM | **Equal-size unit** — both lites cut the same | second pane **inherits W1×H1** (was previously skipped — fixed) |
| W2/H2 filled on a multi-pane row | **Overlap glass** — stepped unit, second lite deliberately different | cut exactly W2×H2 |
| W2/H2 filled on an SGU row | stray data | ignored with a warning (SGU is one pane) |
| W1/H1 missing | unusable row | pane skipped with a warning |

The inherited/equal sizes flow through everything: cutting, holes, polish,
mitring, tempering, the unit envelope in every list, SQM/LM maths, and the
delivery-note dimensions.

The sidebar now states the rule per unit:
`OVERLAP GLASS — inner 1250×950` or
`EQUAL-SIZE UNIT — both lites 1200×900`.

## 4. Piece-flow chain (implemented engine, single source of truth)

```
Cutting (IN = Qty)
   └→ cut pieces fan out to:
        Holes & Cutouts ─┐
        Polish           ├─ ALL must finish a piece before it may temper
        Edge Deletion    │   (stage done = min across the pane's items)
        Mitring         ─┘
Tempering (IN = min(cutDone, every edge-stage done))
   ├─ SGU: tempered pieces go straight to READY FOR DISPATCH
   └─ multi-pane: pane-ready pieces (min across panes) →
        Pairing (IN = slowest pane) → Doubling/Lamination →
        READY FOR DISPATCH (per finished unit)
```

- Floor writes are clamped to the arrived quantity (`SetDoneQtyAsync`).
- Dispatch readiness is a **quantity**; delivery notes are capped to
  finished-but-undelivered pieces (`ProductionDeliveryGateway`).

## 5. Module-by-module industry detail (what each screen carries)

| Module | Unit of work | Industry detail shown |
|---|---|---|
| ① Cutting | panes (pcs + m²) | every pane cut to W1×H1 / W2×H2; overlap vs equal-size rule; treatment per pane; SQM + LM totals |
| ② Holes & Cutouts | pieces per charge op | fan holes, holes, countersinks, cutouts **together on one unit** — one card per charge type per pane, charged qty rides along; must finish before tempering |
| ③ Polish | LM | per-pane edge polishing; done-LM tracks finished pieces; sub-type label (All-Sided, matched-lam, edge grinding) |
| ④ Edge Deletion | LM / pcs | coated-glass deletion queue (reserved; Step-6-fed) |
| ⑤ Mitring | LM | per-pane angled edge work; min-rule bounds the furnace feed |
| ⑥ Tempering | furnace pieces | FT / HS / HST cycles only; IN bounded by cutting + all edge work; SGU pieces dispatch-ready off the furnace |
| ⑦ Pairing | units | multi-pane only; bounded by the slowest pane; waiting-on labels for held units |
| ⑧ Doubling / Lamination | units | assembly notes (spacer size, gas fill, interlayer); finished units → dispatch-ready pieces |

Every module shares: pipeline chain bar (factory piece counts), release bar
(confirmed JOs only), KPI strip (arrived rows · pending · in progress ·
completed · load in the stage's own metric · ready-for-dispatch pieces),
master list = full Step-5 shape minus pricing with IN/DONE/BALANCE, and the
detail sidebar:

- job header + **Open Job Order / Open PI** cross-navigation
- **Step 4/5 spec intelligence:** overlap/equal-size rule badge, pane
  treatments, release date
- pane-level stage items with IN/DONE/BALANCE + done-piece editor
  (Save clamped · All · Reset)
- **Stage totals** — total · done · balance in pieces, SQM and LM
- **Dispatch panel** — READY / DELIVERED / TO DELIVER + Create Delivery Note

## 6. Fixes & upgrades shipped with this analysis

1. **Equal-size inheritance** — blank W2/H2 on multi-pane builds inherits
   W1×H1 for cutting/tempering *and* charge stages (holes/polish/mitring);
   no more silently dropped panes.
2. **Overlap semantics** documented and pinned by tests.
3. Sidebar **Step 4/5 intelligence block** (size rule, treatments,
   release date).
4. Stage-specific KPI metrics (`units to pair`, `units to assemble`,
   `pcs to drill/cut`, LM stages in LM).
5. Industry-level module descriptions on all eight screens.
6. Tests: equal-size DGU/LAM, overlap DGU, charge fan-out on inherited
   sizes, pane quantities = row qty.

## 7. Boundaries kept (owner locks)

- **Scope:** SGU / DGU / LAM / DGU+LAM only — out-of-family names degrade
  to the supported two-pane split with a warning, never invention.
- **Source:** confirmed Job Orders only (Released / In Production,
  non-void). No drafts, no direct-PI path.
- **Pricing:** never shown in production (Rate/Value excluded everywhere).
- **Barcode/label printing:** deferred.
