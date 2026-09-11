# Production Module Screens — List & Detail Design (with worked samples)

**Date:** 2026-09-11 · **Status:** Approved for implementation · Barcode/label printing deferred

Senior analysis of the eight production module screens (Cutting · Holes &
Cutouts · Polish · Edge Deletion · Mitring · Tempering · Pairing ·
Doubling/Lamination): information architecture, field contract, navigation,
and a full worked example showing exactly what the floor sees.

---

## 1. Information architecture

Every module screen is a **two-tier master–detail** built on the shared
`ProductionStageView` (one design, eight skins — same pattern the app uses
for Saved Job Orders → Job Order detail):

```
┌────────────────────────────────────────────────────────────────────────┐
│ [icon tile]  CUTTING          Stage 1 of 8                    [Refresh]│
│ Track glass cutting operations…                                        │
│ ┌ Release from Job Order [ JOB-2026-0031 · Emaar (Released) ▾ ] [Release] ┐
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
| W1 · H1 · W2 · H2 | unit envelope — W2/H2 blank for SGU |
| Qty · Unit SQM · Total SQM · LM | Step 5 maths (perimeter LM Σ) |
| Progress | this module's items done: `2/3` + charge metric for batch rows |

Implemented column order (all 8 modules):
`ST · JOB · PI NO · CUSTOMER · SALESMAN · SPEC · REF · BUILD · W1 · H1 ·
W2 · H2 · QTY · SQM · TOT SQM · LM · STAGE · CHARGE`

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

### Cutting (Stage 1 of 8)
| St | Job | PI | Spec | Ref | W1 | H1 | W2 | H2 | Qty | Tot SQM | LM | Prog |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ● | JOB-2026-0031 | PI-2026-0148 | Spec 1 · 6mm Clear FT | G-101 | 1200 | 900 | — | — | 24 | 25.92 | 100.8 | 0/1 |
| ● | … | … | Spec 1 · 6mm Clear FT | G-102 | 800 | 600 | — | — | 12 | 5.76 | 33.6 | 0/1 |
| ● | … | … | Spec 2 · DGU | G-201 | 1500 | 1100 | 1500 | 1100 | 10 | 33.00 | 104.0 | 0/2 |
| ● | … | … | Spec 3 · LAM | G-301 | 2000 | 1000 | 2000 | 1000 | 4 | 16.00 | 48.0 | 0/2 |

Detail for G-201 → pane items **Outer 1500×1100** and **Inner 1500×1100**
with Start/Complete; header buttons open JOB-2026-0031 / PI-2026-0148.

### Tempering (Stage 6) — only furnace panes
G-101 Glass ×24 (FT) · G-102 Glass ×12 (FT) · G-201 **Outer only** ×10 (inner
is Annealed → no furnace) · G-301 Lam Outer ×4 + Lam Inner ×4.

### Holes & Cutouts (Stage 2) — the Step 6 charge row
`FAN HOLES · 8 QTY · Spec 1 · (whole spec)` — predecessor: Cutting.

### Polish (Stage 3) — Step 6 LM rows
One batch row per spec: `POLISH · ALL SIDED POLISH · 130.2 LM` etc.

### Pairing (Stage 7) + Doubling/Lamination (Stage 8)
G-201 and G-301 units; assembly notes read
`DGU ASSEMBLY · SPACER 12MM · ARGON GAS FILL` and `LAMINATION · PVB 1.52MM CLEAR`.

### Mitring / Edge Deletion
Empty until charged (Mitring) — Edge Deletion is the manual queue for coated
glass (future automation).

## 4. Implementation notes

- Unit projection is computed in the repository from stage items (group by
  Job+Spec+Serial); batch rows are the `Position = "Batch"` charge items.
- `Open Job Order` / `Open PI` use the house navigation callbacks
  (`MainViewModel` → `Navigate("JobOrder"/"ProformaInvoice")` +
  `OpenJobOrderAsync` / `OpenSavedInvoiceAsync`), identical to Sales Report
  and Statement drill-downs.
- Unit status aggregation: all items completed → COMPLETED; any in progress →
  IN PROGRESS; else PENDING. Filters apply to the aggregate.
