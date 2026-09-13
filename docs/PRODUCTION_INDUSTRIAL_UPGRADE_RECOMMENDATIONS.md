# Production module — senior industrial upgrade recommendations

**Date:** 2026-09-13 · **Audience:** product / floor · **Status:** recommendations only (not implemented)

This is a shop-floor MES review of the eight stage queues (Cutting → Double/Lam)
plus Optimization, against how a UAE glass factory actually runs: Job Order
locked to confirmed PI, piece flow IN / DONE / BALANCE, DGU/LAM pairing, then
dispatch. Formulas, `{Binding}` paths, EF schema and workflow engines stay
locked unless a later phase explicitly opens them.

---

## 1. Where we are (honest)

| Strength | Gap vs a professional floor |
|---|---|
| Source lock (Released JO only) | Eight XAMLs still share one grid: JOB / TOTAL / DONE / BALANCE — Cutting does not *look* like a saw, Tempering does not *look* like a furnace |
| Clamped DONE ≤ IN | Operator identity is an audit string, not a station login / badge |
| Pipeline bar + KPIs | No shift, rack, or machine; no “this saw / this furnace” |
| Right panel on row click | Panel is a document editor, not a **station work order** |
| DONE ≠ DELIVERED | No barcode / QR on job or pane |
| Optimization packs by pane glass | Cutting cannot open “today’s cut plan” from a saved optimization session |

The 2026-09-12 redesign split pages and chrome. The remaining work is
**information architecture per station**, not another theme pass.

---

## 2. Design principle

Treat each module as a **station**, not a filtered list of the same table.

```
JO (Released)
  → Cutting        work order = panes to cut (W×H, glass, qty, rack/sheet)
  → Holes          work order = holes/cutouts remaining (pcs + drawing ref)
  → Polish         work order = edge LM remaining
  → Edge deletion  work order = coated face + deletion width
  → Mitring        work order = corners / LM
  → Tempering      work order = furnace load (pcs, thickness, cycle)
  → Pairing        work order = outer↔inner match (DGU)
  → Double / Lam   work order = unit assembly + seal / PVB
  → Dispatch       DONE pieces only, remaining balance DN
```

If a column is not used at that station, it must not appear. Pricing never
appears. SQM is a load metric, not a sell metric.

---

## 3. Priority roadmap

### P0 — Floor correctness (do first)

1. **Station work-order layout (not one grid)**  
   Cutting: W1×H1 / W2×H2 / glass / qty / sheet or rack.  
   Polish/Mitring: LM remaining, not pcs as the hero.  
   Tempering: load size + thickness + cycle hint.  
   Pairing/DGU: outer vs inner match status.  
   *Today every stage still shows the same job-queue columns.*

2. **Operator + timestamp on every SAVE DONE**  
   Badge or station user, not only “operator” in the log. Visible on the
   panel: *who / when / qty*. Required for ISO and customer claims.

3. **Hold / scrap / rework**  
   A pane can be IN but not DONE: cracked at saw, coating scratch, furnace
   break. Today the only verbs are DONE / complete-all. Without hold/scrap,
   BALANCE lies.

4. **Scan-first**  
   Job number and (later) pane serial as scan, then the panel opens. Mouse
   lists do not survive gloves and dust.

5. **Cutting ↔ Optimization session**  
   From Cutting: “Open cut plan for this JO”. From Optimization: “Send
   sheets to Cutting”. Carried in the 2026-09-12 doc; still the biggest
   professional hole between plan and saw.

### P1 — Industrial density

6. **Andon / exception strip**  
   Waiting upstream, clamped writes, unplaced optimizer pieces, furnace
   overload — one red/amber strip per station, not a footer hint.

7. **Rack / stillage**  
   After cut: which rack, which slot. Pairing and dispatch pick from racks,
   not from a flat job list.

8. **Furnace load builder (Tempering)**  
   Thickness mix, max load pcs/m², cycle minutes. Completing a load is one
   action, not 40 row edits.

9. **Pairing board (DGU)**  
   Two columns: outer remaining | inner remaining | matched units. Mismatch
   (size or glass) blocks DONE.

10. **Print pack**  
    Station ticket (job, customer, W×H, qty, barcode), cut list PDF, furnace
    load card. Same visual language as inventory export pills.

### P2 — Plant operations

11. **Shift handover**  
    Open / in progress / holds at 18:00, frozen snapshot, next shift
    continues. No “who left this half-done”.

12. **Sheet Store booking from confirmed cut plan**  
    `Used += sheets` + history (already designed, not wired).

13. **Persisted optimization sessions**  
    `optimization.db` entities exist; save/open/list UI does not.

14. **OEE-lite KPIs**  
    Arrived vs done vs hold vs scrap per station per day — not only
    TOTAL/DONE/BALANCE on the open queue.

15. **Kiosk mode**  
    40px+ targets (inventory already does this), high contrast, no nested
    pills, optional dark shop-floor theme that is *Industrial Dark*, not
    Crystal.

### P3 — Enterprise (later)

16. Label printers (pane QR).  
17. Machine counters (saw metres, furnace cycles).  
18. SPC on tempering breakage.  
19. Tablet / touch only.  
20. Multi-plant.

---

## 4. UI chrome (keep inventory as the reference)

Do **not** invent a fourth button system. Inventory already has the plant
language:

| Token | Use on the floor |
|---|---|
| `PrimaryPillButton` 40px + `Tag` | SAVE DONE, CREATE DN |
| `SuccessPillButton` 40px | Complete load / confirm pair |
| `OutlinedPillButton` 40px | OPEN JO, OPEN PI, CLOSE |
| `DangerButton` | Scrap |
| Card: `SurfacePrimaryBrush` + `BorderDefaultBrush` + `CardBorderThickness` | Station panel (never `WindowBackgroundBrush`) |
| KPI cell MinHeight 72 | Station load, not marketing tiles |

**Layout:** list left, **station panel right, full height**, closed until row
or scan. Hamburger remains the only app menu (no second rail inside the
module).

---

## 5. What not to do

- Do not restyle Crystal/Liquid onto the floor.  
- Do not show PI pricing, salesman commission, or SQM sell on production.  
- Do not auto-open the detail panel on load.  
- Do not “complete all” without a confirm when qty > 1.  
- Do not mix glass compositions on one cutting sheet (optimizer already
  forbids this — the UI must never imply otherwise).  
- Do not change distribution, clamp maths, or DN balance rules in a visual
  pass.

---

## 6. Suggested next build (one slice)

**Cutting station v2 only** (prove the pattern, then clone):

1. Station columns: glass · W×H · qty IN/DONE/BAL · rack.  
2. Right panel = work order + 40px inventory buttons + opaque card.  
3. Hold/scrap reason.  
4. Button: “Open optimization cut plan” (if a session exists).  
5. Print cut ticket.

Then Tempering load builder, then Pairing board. That sequence is how a
factory actually staffs the line.
