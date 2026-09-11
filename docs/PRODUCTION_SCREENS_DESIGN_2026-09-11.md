# Production Module Screens — List & Detail Design (with worked samples)

**Date:** 2026-09-11 · **Status:** Approved for implementation · Barcode/label printing deferred

Senior analysis of the eight production module screens (Cutting · Holes &
Cutouts · Polish · Edge Deletion · Mitring · Tempering · Pairing ·
Doubling/Lamination): information architecture, field contract, navigation,
and a full worked example showing exactly what the floor sees.

---

## 1. Information architecture

Each stage module is a real, designed page. The **module view**
(`CuttingView.xaml` etc.) owns the stage-identity header — tinted icon
tile, module title, `Stage n of 8` badge, description, and a one-line
chain-context hint (what feeds this stage and where it feeds next).
Below the header sits the shared `ProductionStageView` body: a
**pipeline chain bar**, the release bar, KPIs, filters, and the
two-tier master–detail list/detail (same pattern the app uses for Saved
Job Orders → Job Order detail).

The **pipeline chain bar** shows all eight stages in floor order with
factory-wide counts; the current stage is highlighted and any chip
navigates straight to that stage's module — so an operator can follow a
job through the chain without leaving production.

```
┌────────────────────────────────────────────────────────────────────────┐
│ [icon tile]  CUTTING   Stage 1 of 8                                    │
│ Track glass cutting operations…                                        │
│ › Stage 1 of 8 · first stage — cut panes feed Holes, Polish, Tempering │
│ ┌ PRODUCTION PIPELINE ─────────────── click a stage to follow the work ┐│
│ │ ①Cutting  ②Holes  ③Polish  ④EdgeDel  ⑤Mitring  ⑥Temper  ⑦Pair  ⑧Dbl ││
│ └───────────────────────────────────────────────────────────────────── ┘│
│ ┌ Release from Job Order [ JOB-2026-0031 · Emaar (Released) ▾ ] [Release] [Refresh] ┐
│ └ release message / amber floor-connectivity warning banner          ┘
│ KPI: In queue 96 · Pending 84 · In progress 8 · Completed 4 ·        │
│      Load 214.6 m² / 148 pcs · Ready for dispatch 2 / 7              │
│ Filters: [job ▾] [status ▾] [search ref / customer / spec…]          │
│ ┌──────────────────────────────────────┬──────────────────────────────┐│
│ │ UNIT LIST (master)                   │ UNIT DETAIL (detail)         ││
│ │ St JOB   PI   SPEC   REF  W1 H1 W2 H2│ JOB-2026-0031 · PI-2026-0148 ││
│ │ …                                    │ Emaar · Ravi Kumar · AL NOOR ││
│ │                                      │ [Open Job Order] [Open PI]   ││
│ │                                      │ ─ stage items for this unit ─││
│ │                                      │  Outer 1500×1100 [Start][✓]  ││
│ │                                      │  Inner 1500×1100 [Start][✓]  ││
│ └──────────────────────────────────────┴──────────────────────────────┘│
└────────────────────────────────────────────────────────────────────────┘
```

### Tier 1 — Unit list (what the floor reads first)

One row per **unit** (Job + Spec + serial row) so the list mirrors the Step 5
grid the estimator built — W1/H1 **and** W2/H2 on the same line. Charge-driven
batch items (fan holes, polish LM, mitring LM) appear as their own rows tagged
`(whole spec)`.

| Column | Source |
|---|---|
| St | unit stage status: ✅ COMPLETED / ◐ IN PROGRESS / ● PENDING |
| Job No | Job Order id |
| PI No | source ProformaId |
| Customer · Salesman | invoice Client snapshot (`SalesRepresentative`) |
| Spec | `Spec 1 · 6mm Clear FT Glass` |
| Ref | GlassRef from Step 5 |
| Build | SGU / DGU / LAM / DGU+LAM |
| Sr · Ref | Step 5 serial + GlassRef — identical to the estimator's grid |
| W1 · H1 · W2 · H2 | unit envelope — W2/H2 blank for SGU; identical in every stage |
| Qty · SQM1 · TOT SQM1 · LM1 · SQM2 · TOT SQM2 · LM2 | full Step 5 maths split per pane (pricing — Rate/Value — deliberately excluded) |
| Progress | this module's items done: `2/3` + charge metric for batch rows |

Implemented column order (all 8 modules — long production rows, exactly
Step 5 minus pricing):
`ST · JOB · PI NO · CUSTOMER · SALESMAN · SPEC · SR · REF · BUILD · W1 ·
H1 · W2 · H2 · QTY · SQM1 · TOT SQM1 · LM1 · SQM2 · TOT SQM2 · LM2 ·
STAGE · CHARGE`

### Production-chain flow (tool-chain gating)

Production moves like a tool chain — a unit's data physically advances
from one module to the next:

1. **Every unit enters at Cutting.** The Cutting module shows every
   released pane (SGU glass, DGU outer+inner, laminate layers).
2. **Data moves only when it completes.** A unit appears in stage N only
   when every non-charge item of that unit in stages below N is
   **Completed**. Finishing the last upstream item makes the row appear
   in the next module on the next refresh — no manual pushing.
   - Tempering row visible once the pane's Cutting is done.
   - Pairing once every pane's Cutting + Tempering is done.
   - Doubling/Lamination once Pairing is done.
   - Charge batches (Holes / Polish / Mitring / Edge Deletion) release
     once the spec's cutting is complete.
3. **Backlog stays visible.** Held rows are counted and surfaced by a
   "waiting upstream" strip + KPIs; the pipeline chain bar's `open`
   count reflects released (actionable) work only, so the chain shows
   where the live work really sits.

Gating is computed in one place — `ProductionRepository.ComputeReleasedKeys`
— and drives the lists, the chain bar and the KPIs.

### Tier 2 — Unit detail (select a row)

Job header (Job No, PI No, Customer, Project, Salesman), **Open Job Order**
and **Open Proforma Invoice** buttons (navigate to the real JO detail / PI
wizard — Steps 5/6 included — via the house `MainViewModel` callback
pattern), and the pane-level stage items for this module with
**Start / Complete / Reopen** actions and predecessor warnings.

## 2. Field contract & connectivity

- **Salesman** is carried from the invoice snapshot through the engine into
  every work item (additive `Salesman` column, migration-guarded).
- Completing before the predecessor stage raises the amber banner (soft
  lock); **Ready for dispatch** KPI = units whose every stage item is done.
- Release source remains **confirmed Job Orders only** (Released /
  In Production, non-void).

## 3. Worked sample — what each module actually shows

**The invoice:** PI-2026-0148 · AL NOOR TOWER · Emaar Interiors LLC ·
Salesman **Ravi Kumar** → converted to **JOB-2026-0031** (Released).

| Spec | Build | Step 5 rows |
|---|---|---|
| Spec 1 — `6mm Clear FT Glass` | SGU | G-101 1200×900 ×24 · G-102 800×600 ×12 |
| Spec 2 — `6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass` | DGU | G-201 1500×1100 ×10 (inner 1500×1100) |
| Spec 3 — `8mm Clear FT Glass + 1.52mm Clear PVB + 8mm Clear FT Glass` | LAM | G-301 2000×1000 ×4 (both panes 2000×1000) |

**Step 6:** FAN_HOLES ×8 on Spec 1 · POLISH All-Side on all specs ·
ARGON_GAS on Spec 2.

### Cutting (Stage 1 of 8) — the entry point, full Step-5 rows
| St | Sr | Ref | Build | W1 | H1 | W2 | H2 | Qty | SQM1 | TOT SQM1 | LM1 | SQM2 | TOT SQM2 | LM2 | Prog |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ● | 1 | G-101 | SGU | 1200 | 900 | — | — | 24 | 1.08 | 25.92 | 100.8 | — | — | — | 0/1 |
| ● | 2 | G-102 | SGU | 800 | 600 | — | — | 12 | 0.48 | 5.76 | 33.6 | — | — | — | 0/1 |
| ● | 1 | G-201 | DGU | 1500 | 1100 | 1500 | 1100 | 10 | 1.65 | 16.50 | 52.0 | 1.65 | 16.50 | 52.0 | 0/2 |
| ● | 1 | G-301 | LAM | 2000 | 1000 | 2000 | 1000 | 4 | 2.00 | 8.00 | 24.0 | 2.00 | 8.00 | 24.0 | 0/2 |

(all rows JOB-2026-0031 · PI-2026-0148 · AL NOOR TOWER · salesman Ravi Kumar)

Detail for G-201 → pane items **Outer 1500×1100** and **Inner 1500×1100**
with Start/Complete; header buttons open JOB-2026-0031 / PI-2026-0148.

### The flow — how this job walks the chain
1. **Day 1**: only **Cutting** lists the four rows above. Tempering /
   Pairing / Doubling are empty ("2 rows waiting upstream" strip).
2. Operator completes G-101's cutting → next refresh, G-101 appears in
   **Tempering** (FT pane). G-201's outer+inner get cut → G-201 appears
   in **Pairing** only after BOTH panes are cut (and the FT outer is
   tempered), then in **Doubling** after pairing.
3. Spec 1's cutting completion releases the **Holes & Cutouts** batch
   row (`FAN HOLES · 8 QTY · (whole spec)`); the **Polish** LM batches
   release the same way.
4. When a unit's last stage completes it counts toward **Ready for
   dispatch**.

### Tempering (Stage 6) — furnace panes, after cutting
G-101 Glass ×24 (FT) · G-102 Glass ×12 (FT) · G-201 **Outer only** ×10 (inner
is Annealed → no furnace) · G-301 Lam Outer ×4 + Lam Inner ×4 — each row
carries the same Step-5 envelope as cutting.

### Pairing (Stage 7) + Doubling/Lamination (Stage 8)
G-201 and G-301 units, full envelope preserved; assembly notes read
`DGU ASSEMBLY · SPACER 12MM · ARGON GAS FILL` and `LAMINATION · PVB 1.52MM CLEAR`.

### Mitring / Edge Deletion
Mitring: batch rows once the MITRING charge exists and cutting is done.
Edge Deletion: queue for coated-glass edge deletion (future automation).

## 4. Implementation notes

- Unit projection is computed in the repository from stage items (group by
  Job+Spec+Serial); batch rows are the `Position = "Batch"` charge items.
- `Open Job Order` / `Open PI` use the house navigation callbacks
  (`MainViewModel` → `Navigate("JobOrder"/"ProformaInvoice")` +
  `OpenJobOrderAsync` / `OpenSavedInvoiceAsync`), identical to Sales Report
  and Statement drill-downs.
- Unit status aggregation: all items completed → COMPLETED; any in progress →
  IN PROGRESS; else PENDING. Filters apply to the aggregate.
