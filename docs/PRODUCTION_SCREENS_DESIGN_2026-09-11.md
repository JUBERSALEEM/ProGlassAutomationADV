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
factory-wide piece counts (open · done/total); the current stage is
highlighted and any chip navigates straight to that stage's module — so
an operator can follow a job through the chain without leaving production.

```
┌────────────────────────────────────────────────────────────────────────┐
│ [icon tile]  CUTTING   Stage 1 of 8                                    │
│ Track glass cutting operations…                                        │
│ › Stage 1 of 8 · first stage — cut pieces feed Holes, Polish, Tempering│
│ ┌ PRODUCTION PIPELINE ─────────────── click a stage to follow the work ┐│
│ │ ①Cutting  ②Holes  ③Polish  ④EdgeDel  ⑤Mitring  ⑥Temper  ⑦Pair  ⑧Dbl ││
│ └───────────────────────────────────────────────────────────────────── ┘│
│ ┌ Release from Job Order [ JOB-2026-0031 · Emaar (Released) ▾ ] [Release] [Refresh] ┐
│ └ release message / amber clamp warning banner                       ┘
│ KPI: Arrived 4 rows · Pending 2 · In progress 1 · Completed 1 ·      │
│      Load 214.6 m² / 148 pcs · Ready for dispatch 2 / 7              │
│ Filters: [job ▾] [status ▾] [search ref / customer / spec…]          │
│ ┌──────────────────────────────────────┬──────────────────────────────┐│
│ │ UNIT LIST (master)                   │ UNIT DETAIL (detail)         ││
│ │ JOB PI SPEC REF W1 H1 W2 H2 QTY      │ JOB-2026-0031 · PI-2026-0148 ││
│ │ … SQM1 LM1 SQM2 LM2  IN DONE BAL     │ Emaar · Ravi Kumar · AL NOOR ││
│ │                                      │ [Open Job Order] [Open PI]   ││
│ │                                      │ ─ pane stage items ──────────││
│ │                                      │  Glass 1200×900 Qty24        ││
│ │                                      │  IN 24 · DONE 10 · BAL 14    ││
│ │                                      │  [Done pcs 10][Save][All][Rst]││
│ └──────────────────────────────────────┴──────────────────────────────┘│
└────────────────────────────────────────────────────────────────────────┘
```

### Tier 1 — Unit list (what the floor reads first)

One row per **unit** (Job + Spec + serial row) so the list mirrors the Step 5
grid the estimator built — W1/H1 **and** W2/H2 on the same line, full area
and LM split per pane, **no pricing**. Each row also carries this stage's
**piece flow**: `IN` (pieces arrived from upstream) · `DONE` (pieces the
floor completed here) · `BALANCE` (IN − DONE).

**Scale:** the grid is fully virtualized (rows recycled, columns
virtualized) and the list loads **incrementally — 100 rows at a time**.
Scrolling near the bottom renders the next 100 (footer shows
`Showing X of Y units — scroll for the next 100`), so jobs with very long
unit lists never lag. The selection survives reloads and is paged into
view automatically.

| Column | Source |
|---|---|
| St | unit stage status: ✅ DONE / ◐ IN PROGRESS / ● PENDING / ⏳ WAITING |
| Job No | Job Order id |
| PI No | source ProformaId |
| Customer · Salesman | invoice Client snapshot (`SalesRepresentative`) |
| Spec | `Spec 1 · 6mm Clear FT Glass` |
| Sr · Ref | Step 5 serial + GlassRef — identical to the estimator's grid |
| Build | SGU / DGU / LAM / DGU+LAM |
| W1 · H1 · W2 · H2 | unit envelope — W2/H2 blank for SGU; identical in every stage |
| Qty · SQM1 · TOT SQM1 · LM1 · SQM2 · TOT SQM2 · LM2 | full Step 5 maths split per pane (pricing — Rate/Value — deliberately excluded) |
| IN · DONE · BALANCE | this stage's piece flow (arrived / completed / remaining) |

Implemented column order (all 8 modules — long production rows, exactly
Step 5 minus pricing, plus the stage flow):
`ST · JOB · PI NO · CUSTOMER · SALESMAN · SPEC · SR · REF · BUILD · W1 ·
H1 · W2 · H2 · QTY · SQM1 · TOT SQM1 · LM1 · SQM2 · TOT SQM2 · LM2 ·
IN · DONE · BALANCE`

### Piece flow — the production toolchain

Production flows **piece by piece**; it is never an all-or-nothing gate.
Every stage item records how many pieces the floor has **completed**
(`QtyDone`, 0..Qty). Only the completed quantity arrives in the next stage:

1. **Every piece enters at Cutting.** `IN = Qty`; the floor records how
   many pieces are cut (`DONE`), the remainder is `BALANCE`.
2. **Cut pieces walk to pre-tempering work.** Holes / Polish / Edge
   Deletion / Mitring are per-pane stages too; each receives the **cut
   quantity** as its `IN`. A piece must finish *every* one of these that
   applies before it may be tempered.
3. **Tempering receives the pieces that finished cutting + all edge work.**
   `IN = min(cutDone, holesDone, polishDone, …)` per pane.
4. **Pairing** receives the pieces whose panes are all ready (the slowest
   pane bounds the unit); **Doubling/Lamination** receives the paired
   quantity. SGU has no assembly — a fully-cut SGU pane is dispatch-ready.
5. **Floor writes are clamped.** A stage can never report more done than
   it received — the toolchain cannot be overstated. Trying to saves the
   arrived count and says so.

Flow is computed in one place — `ProductionRepository.ComputeUnitFlows` —
and drives the lists, the chain bar, the KPIs and the clamped writes.

### Tier 2 — Unit detail sidebar (select a row)

Job header (Job No, PI No, Customer, Project, Salesman), **Open Job Order**
and **Open Proforma Invoice** buttons (navigate to the real JO detail / PI
wizard — Steps 5/6 included — via the house `MainViewModel` callback
pattern), and the pane-level stage items. Each item shows its own
`IN · DONE · BALANCE` and a **done-piece editor**: type the number of
pieces completed, `Save` (clamped to IN), `All` (complete everything that
arrived), `Reset` (back to zero).

Below the items the sidebar shows, for the selected unit in the current
stage:

- **STAGE TOTALS** — total · done · balance in **pieces**, **area (m²)**
  and **LM** (every module; polish/mitring naturally read in LM, holes in
  pieces — all three are always available).
- **DISPATCH** — `READY` (pieces finished in production) ·
  `DELIVERED` (confirmed delivery notes) · `TO DELIVER` (the balance),
  with a **Create Delivery Note** button: it creates/reopens the
  Dispatching DN for the Job Order — remaining balance only, capped to
  production-finished pieces — and navigates to it.

## 2. Field contract & connectivity

- **Salesman** is carried from the invoice snapshot through the engine into
  every work item (additive `Salesman` column, migration-guarded).
- **QtyDone** is the floor's piece counter per stage item (additive column,
  migration 1002). Status is derived from it against the arrived quantity.
- A stage can only complete up to the pieces it received — writes are
  clamped and the clamp is surfaced in the warning banner.
- **Ready for dispatch** is a QUANTITY, not a flag: SGU pieces dispatch
  straight off the pane chain (single glass needs no pairing/doubling);
  DGU/LAM/DGU+LAM dispatch what finished doubling. The KPI reads
  `X / Y pcs`.
- **Delivery notes are fed by production**: `ProductionDeliveryGateway`
  caps each DN row's deliverable qty to production-finished pieces minus
  confirmed-delivered pieces (`DnItemRowViewModel` clamp). Jobs without a
  production release keep the classic Job-Order balance behaviour. Each
  next DN is created from remaining balance only (existing
  `DeliveryNoteCreateService` rule), so a second note covers exactly the
  pieces not yet dispatched.
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

### Cutting (Stage 1 of 8) — entry point, full Step-5 rows + piece flow
| St | Sr | Ref | Build | W1 | H1 | W2 | H2 | Qty | SQM1 | TOT SQM1 | LM1 | SQM2 | TOT SQM2 | LM2 | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ◐ | 1 | G-101 | SGU | 1200 | 900 | — | — | 24 | 1.08 | 25.92 | 100.8 | — | — | — | 24 | 10 | 14 |
| ◐ | 2 | G-102 | SGU | 800 | 600 | — | — | 12 | 0.48 | 5.76 | 33.6 | — | — | — | 12 | 6 | 6 |
| ◐ | 1 | G-201 | DGU | 1500 | 1100 | 1500 | 1100 | 10 | 1.65 | 16.50 | 52.0 | 1.65 | 16.50 | 52.0 | 20 | 20 | 0 |
| ● | 1 | G-301 | LAM | 2000 | 1000 | 2000 | 1000 | 4 | 2.00 | 8.00 | 24.0 | 2.00 | 8.00 | 24.0 | 8 | 0 | 8 |

(all rows JOB-2026-0031 · PI-2026-0148 · AL NOOR TOWER · salesman Ravi Kumar;
DGU/LAM IN counts both panes)

Detail for G-101 → pane item **Glass 1200×900, Qty 24** with
`IN 24 · DONE 10 · BALANCE 14` and the done-piece editor. Header buttons
open JOB-2026-0031 / PI-2026-0148.

### How the pieces walk the chain (the toolchain in numbers)
1. **Cutting**: the operator cuts **10 of 24** G-101 pieces. Cutting now
   shows `IN 24 · DONE 10 · BAL 14`. Those 10 cut pieces become the `IN`
   for G-101's downstream stages.
2. **Holes & Cutouts**: G-101 (Spec 1 has FAN_HOLES) shows `IN 10`. The
   floor drills **5** → `DONE 5 · BAL 5`.
3. **Polish**: G-101 shows `IN 10` (the cut quantity) and is polished in
   step with the pieces.
4. **Tempering**: G-101's FT pane shows `IN = min(cutDone, holesDone,
   polishDone)`. Once all 10 cut pieces are holed and polished, `IN 10`
   — the floor tempers 5, then 5 more: `DONE 10`.
5. **Dispatch**: the SGU pane chain is done → those 10 pieces count toward
   **Ready for dispatch**. The same arithmetic runs for every unit; DGU/LAM
   additionally Pair and Double once both panes are ready.

### Tempering (Stage 6) — furnace panes
G-101 Glass ×24 (FT) · G-102 Glass ×12 (FT) · G-201 **Outer only** ×10 (inner
is Annealed → no furnace) · G-301 Lam Outer ×4 + Lam Inner ×4 — each row
carries the same Step-5 envelope as cutting, with its own IN/DONE/BALANCE.

### Pairing (Stage 7) + Doubling/Lamination (Stage 8)
G-201 and G-301 units, full envelope preserved; assembly notes read
`DGU ASSEMBLY · SPACER 12MM · ARGON GAS FILL` and `LAMINATION · PVB 1.52MM CLEAR`.
IN is bounded by the slowest pane (pairing) / the paired count (doubling).

### Mitring / Edge Deletion
Mitring: per-pane rows once the MITRING charge applies and pieces are cut.
Edge Deletion: queue for coated-glass edge deletion (future automation).

## 4. Implementation notes

- Unit flow is computed in `ProductionRepository.ComputeUnitFlows` from all
  items grouped by Job+Spec+Serial; per-pane pieces flow Cutting → edge work
  → Tempering, unit-level Pairing → Doubling.
- Charge-driven stages (Holes / Polish / Mitring) fan out **per pane** (not
  whole-spec batches), so cut pieces physically walk through them.
- Floor writes go through `SetDoneQtyAsync(itemId, qty)`, which clamps `qty`
  to the arrived quantity and derives Pending / InProgress / Completed.
- `Open Job Order` / `Open PI` use the house navigation callbacks
  (`MainViewModel` → `Navigate("JobOrder"/"ProformaInvoice")` +
  `OpenJobOrderAsync` / `OpenSavedInvoiceAsync`), identical to Sales Report
  and Statement drill-downs.
- Row status: no arrived pieces → WAITING UPSTREAM; done 0 → PENDING;
  partial → IN PROGRESS; done ≥ IN → COMPLETED. Filters apply to this.

## 5. Appendix — one job, all eight modules (screen by screen)

**Job:** JOB-2026-0031 · PI-2026-0148 · AL NOOR TOWER · Emaar Interiors ·
salesman **Ravi Kumar** (Released).

| Spec | Build | Composition | Rows |
|---|---|---|---|
| Spec 1 | SGU | 6mm Clear FT Glass | G-101 1200×900 ×24 · G-102 800×600 ×12 |
| Spec 2 | DGU | 6mm Clear FT + 12A Argon + 6mm Clear Annealed | G-201 1500×1100 ×10 |
| Spec 3 | LAM | 8mm Clear FT + 1.52 Clear PVB + 8mm Clear FT | G-301 2000×1000 ×4 |

**Step 6 charges:** FAN_HOLES ×8 (Spec 1) · POLISH All-Sided (Specs 1–3) ·
MITRING (Spec 3) · ARGON (Spec 2).

**Snapshot moment:** G-101 cut 10/24 and those 10 holed 5/10; G-102 cut 6/12;
G-201 fully cut (20 panes) with all edge work done and the FT outer tempered
5/10; G-301 not started.

### ① Cutting — Stage 1 of 8 (entry point)
Every piece starts here; `IN = Qty`.

| St | Sr | Ref | Build | W1×H1 | W2×H2 | Qty | SQM1 | TOT1 | LM1 | SQM2 | TOT2 | LM2 | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ◐ | 1 | G-101 | SGU | 1200×900 | — | 24 | 1.08 | 25.92 | 100.8 | — | — | — | 24 | 10 | 14 |
| ◐ | 2 | G-102 | SGU | 800×600 | — | 12 | 0.48 | 5.76 | 33.6 | — | — | — | 12 | 6 | 6 |
| ✓ | 1 | G-201 | DGU | 1500×1100 | 1500×1100 | 10 | 1.65 | 16.50 | 52.0 | 1.65 | 16.50 | 52.0 | 20 | 20 | 0 |
| ● | 1 | G-301 | LAM | 2000×1000 | 2000×1000 | 4 | 2.00 | 8.00 | 24.0 | 2.00 | 8.00 | 24.0 | 8 | 0 | 8 |

### ② Holes & Cutouts — Stage 2 (per-pane rows; IN = cut pieces)
Spec 1 carries FAN_HOLES, so G-101/G-102 walk through here.

| St | Sr | Ref | Pane | Charge | W×H | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|
| ◐ | 1 | G-101 | Glass | FAN HOLES · 8 QTY | 1200×900 | 10 | 5 | 5 |
| ● | 2 | G-102 | Glass | FAN HOLES · 8 QTY | 800×600 | 6 | 0 | 6 |

### ③ Polish — Stage 3 (IN = cut pieces)
| St | Sr | Ref | Pane | Charge | W×H | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|
| ◐ | 1 | G-101 | Glass | POLISH · ALL SIDED | 1200×900 | 10 | 10 | 0 |
| ● | 2 | G-102 | Glass | POLISH · ALL SIDED | 800×600 | 6 | 0 | 6 |
| ● | 1 | G-201 | Outer + Inner | POLISH · ALL SIDED | 1500×1100 | 20 | 20 | 0 |
| ● | 1 | G-301 | Lam Outer + Lam Inner | POLISH · ALL SIDED | 2000×1000 | 0 | 0 | 0 |

### ④ Edge Deletion — Stage 4
**Empty.** No Step-6 charge feeds it yet — reserved queue for coated-glass
edge deletion (future automation). The screen shows the empty state.

### ⑤ Mitring — Stage 5 (IN = cut pieces)
| St | Sr | Ref | Pane | Charge | W×H | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|
| ● | 1 | G-301 | Lam Outer + Lam Inner | MITRING | 2000×1000 | 0 | 0 | 0 |

*(G-301 not yet cut, so nothing has arrived — the row appears with IN 0 as a
waiting row or not at all until cutting progresses.)*

### ⑥ Tempering — Stage 6 (IN = pieces that finished cutting + all edge work)
Only furnace-eligible panes — G-201's annealed inner never appears here.

| St | Sr | Ref | Pane | Treatment | W×H | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|
| ◐ | 1 | G-101 | Glass | FT CYCLE | 1200×900 | 5 | 5 | 0 |
| ● | 1 | G-201 | Outer | FT CYCLE | 1500×1100 | 10 | 5 | 5 |
| ● | 1 | G-301 | Lam Outer + Lam Inner | FT CYCLE | 2000×1000 | 0 | 0 | 0 |

*(G-101's tempering IN is 5 — only the pieces that are cut **and** holed **and**
polished. When the other 5 holed pieces finish polish, IN grows to 10.)*

**Dispatch (SGU):** G-101 needs no pairing and no doubling — each piece the
furnace finishes is READY FOR DISPATCH immediately. At this snapshot 5 G-101
pieces are tempered → the sidebar dispatch panel reads
`READY 5 · DELIVERED 0 · TO DELIVER 5`, and **Create Delivery Note** opens a
DN that can deliver exactly those 5. The remaining 19 pieces flow through the
same chain and feed the NEXT delivery note — never the pieces already out
the door.

### ⑦ Pairing — Stage 7 (IN = pieces whose panes are all ready)
| St | Sr | Ref | Build | Envelope | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|
| ◐ | 1 | G-201 | DGU | 1500×1100 ×2 panes | 5 | 0 | 5 |
| ⏳ | 1 | G-301 | LAM | 2000×1000 ×2 panes | 0 | 0 | 0 |

*(G-201 pairs at the slowest pane: outer tempered 5, inner annealed-ready 10
→ IN 5. G-301 waits on its laminate tempering.)*

### ⑧ Doubling / Lamination — Stage 8 (IN = paired pieces)
| St | Sr | Ref | Build | Assembly note | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|
| ⏳ | 1 | G-201 | DGU | DGU ASSEMBLY · SPACER 12MM · ARGON GAS FILL | 0 | 0 | 0 |
| ⏳ | 1 | G-301 | LAM | LAMINATION · PVB 1.52MM CLEAR | 0 | 0 | 0 |

*(Assembly IN grows only as pairing completes. When G-201 pairs 5 and doubles
them, those units move toward **Ready for dispatch**.)*

**Reading the chain:** at this snapshot the live work sits at Cutting
(G-101 14 to cut), Holes (G-101 5 to drill), Tempering (G-101 done, G-201
5 to temper) and Pairing (G-201 5 to pair). The chain bar's `open` counts
show exactly that — pieces, not rows.

## 6. Appendix — production → delivery notes (automatic dispatch flow)

Finished pieces flow into delivery notes automatically; production is the
gate for what a DN may deliver:

1. **Production completes pieces.** G-101 tempers 5 → `DispatchReadyQty = 5`
   (SGU); a DGU's `DispatchReadyQty` grows as doubling completes pieces.
2. **Create Delivery Note** (production sidebar) calls the house
   `DeliveryNoteCreateService.CreateOrOpenDispatchingAsync` — it reuses an
   open Dispatching DN when one exists, else creates the next DN **from
   remaining balance only** (classic rule: never re-delivers dispatched
   pieces).
3. **The DN editor is production-capped.** Each row's deliverable qty is
   clamped to `productionReady − confirmedDelivered`
   (`ProductionDeliveryGateway.GetDeliveryCapsAsync` →
   `DnItemRowViewModel` clamp). Trying to deliver 24 G-101 saves 5.
4. **Dispatch the DN.** Confirmed statuses (Dispatched / Delivered / Signed /
   Received) consume the production balance; the sidebar flips to
   `READY 5 · DELIVERED 5 · TO DELIVER 0`.
5. **Next pieces, next note.** When the next 10 G-101 pieces temper, the
   dispatch panel shows `TO DELIVER 10` and the next DN covers exactly
   those 10 — never the pieces already dispatched.

Jobs without a production release are untouched: the gateway returns
`null` and DNs keep the classic Job-Order balance behaviour.
