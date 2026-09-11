# Production Module Examples — one job through all eight screens

**Date:** 2026-09-11 · Companion to `PRODUCTION_SCREENS_DESIGN_2026-09-11.md`

A worked walk-through of what each of the eight production modules shows,
using one realistic job mid-production. Every number follows the piece-flow
engine (`ComputeUnitFlows`): only pieces a stage completes move to the next
stage, and dispatch is a quantity.

---

## The job

**PI-2026-0152** · Skyline Business Tower · Al Futtaim Interior Contracting ·
salesman **Ravi Kumar** → converted to **JOB-2026-0044** (Released).

| Spec | Build | Composition | Step 5 rows |
|---|---|---|---|
| Spec 1 | SGU | `10mm Clear FT Glass` | G-101 1500×1200 ×30 · G-102 900×600 ×20 |
| Spec 2 | DGU | `8mm Clear FT + 12mm Argon + 6mm Clear Annealed` | G-201 1800×1200 ×15 (inner 1790×1190) |
| Spec 3 | LAM | `6mm Clear FT + 1.52 Clear PVB + 6mm Clear FT` | G-301 2400×1500 ×8 (both panes) |

**Step 6 charges:** FAN_HOLES ×6 + CUTOUT ×2 on Spec 1 (two charge types on
one spec — the Holes module holds them together) · POLISH All-Sided on
Specs 1–3 · MITRING on Spec 3 · ARGON on Spec 2.

**Snapshot moment:** G-101 fully cut/edged, 20 of 30 tempered (8 already
delivered on DN-01) · G-201 fully paired, 9 of 15 doubled · G-301 cut and
polished, mitring halfway, furnace waiting.

Pane maths used below:
G-101 1.80 m²/pc, 5.4 LM · G-102 0.54 m²/pc, 3.0 LM ·
G-201 outer 2.16 m²/pc, 6.0 LM / inner 2.13 m²/pc, 5.96 LM ·
G-301 3.60 m²/pc, 7.8 LM.

**Chain bar at this moment (factory pieces):**
`①Cutting 96/96 ✓ · ②Holes 100/100 ✓ · ③Polish 96/96 ✓ · ④EdgeDel — ·
⑤Mitring 3 open · 13/16 · ⑥Temper 28 open · 47/75 · ⑦Pair 15/15 ✓ ·
⑧Double/Lam 6 open · 9/15`

---

## ① Cutting — Stage 1 of 8 (every piece enters here)

`IN = Qty`. Four unit rows; multi-pane builds count panes (G-201 cuts 30
panes for 15 units).

| St | Sr | Ref | Build | W1×H1 | W2×H2 | Qty | SQM1 | LM1 | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|---|---|---|
| ✓ | 1 | G-101 | SGU | 1500×1200 | — | 30 | 1.80 | 5.4 | 30 | 30 | 0 |
| ✓ | 2 | G-102 | SGU | 900×600 | — | 20 | 0.54 | 3.0 | 20 | 20 | 0 |
| ✓ | 1 | G-201 | DGU | 1800×1200 | 1790×1190 | 15 | 2.16 | 6.0 | 30 | 30 | 0 |
| ✓ | 1 | G-301 | LAM | 2400×1500 | 2400×1500 | 8 | 3.60 | 7.8 | 16 | 16 | 0 |

**Sidebar · G-201:** items `Outer 1800×1200` and `Inner 1790×1190`, each
`IN 15 · DONE 15 · BAL 0` with the done-piece editor. Stage totals —
Pieces `30 · 30 done · 0 left` · Area `64.35 m² · 64.35 m² done · 0 left` ·
LM `179.4 LM · done · 0 left`.

---

## ② Holes & Cutouts — Stage 2 (charge work, per pane)

Spec 1 carries **two** charge types — the module keeps fan holes and
cutouts together as separate items per pane; each receives the cut
quantity as its IN. The unit-row total counts every required operation
(G-101: 2 ops × 30 pieces = 60).

| St | Sr | Ref | Build | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|
| ✓ | 1 | G-101 | SGU | 30 | 60 | 60 | 0 |
| ✓ | 2 | G-102 | SGU | 20 | 40 | 40 | 0 |

**Sidebar · G-101** shows both operations on the pane:

| Item | Charge | IN | DONE | BAL |
|---|---|---|---|---|
| Glass 1500×1200 · Qty 30 | FAN HOLES · 6 QTY | 30 | 30 | 0 |
| Glass 1500×1200 · Qty 30 | CUTOUT · 2 QTY | 30 | 30 | 0 |

*(Mid-production example: when only 5 fan holes were done the cards read
`IN 30 · DONE 5 · BAL 25` — and only finished pieces move on.)*

---

## ③ Polish — Stage 3 (LM stage)

All three specs polish; each pane receives the cut quantity.

| St | Sr | Ref | Build | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|
| ✓ | 1 | G-101 | SGU | 30 | 30 | 30 | 0 |
| ✓ | 2 | G-102 | SGU | 20 | 20 | 20 | 0 |
| ✓ | 1 | G-201 | DGU | 15 | 30 | 30 | 0 |
| ✓ | 1 | G-301 | LAM | 8 | 16 | 16 | 0 |

**Sidebar · G-301** (`Lam Outer` + `Lam Inner`, POLISH · ALL SIDED):
Stage totals — Pieces `16 · 16 done · 0 left` · Area `57.6 m² done` ·
**LM `124.8 LM · 124.8 LM done · 0 left`** — the polish module reads the
work in LM, done-LM tracks the polished pieces.

---

## ④ Edge Deletion — Stage 4

**Empty state** on this job — no Step-6 charge feeds it yet. The screen
shows the designed empty state ("No edge-deletion work released — coated
glass edge deletion queue").

---

## ⑤ Mitring — Stage 5 (LM stage, per pane)

Spec 3 only. Mid-work: the Lam Outer mitring is done, Lam Inner halfway.

| St | Sr | Ref | Build | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|
| ◐ | 1 | G-301 | LAM | 8 | 16 | 13 | 3 |

**Sidebar · G-301:**

| Item | Charge | IN | DONE | BAL |
|---|---|---|---|---|
| Lam Outer 2400×1500 · Qty 8 | MITRING | 8 | 8 | 0 |
| Lam Inner 2400×1500 · Qty 8 | MITRING | 8 | 5 | 3 |

Stage totals — LM `124.8 LM · 101.4 LM done · 23.4 LM left`.
Because a piece needs **all** its edge work before tempering, G-301's
furnace feed is bounded by the slowest operation (see Tempering).

---

## ⑥ Tempering — Stage 6 (furnace pieces)

Only furnace-eligible panes — G-201's annealed inner never appears here.
`IN` = pieces that finished cutting **and** every edge operation.

| St | Sr | Ref | Pane | Treatment | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|
| ◐ | 1 | G-101 | Glass | FT CYCLE | 30 | 30 | 20 | 10 |
| ◐ | 2 | G-102 | Glass | FT CYCLE | 20 | 20 | 12 | 8 |
| ✓ | 1 | G-201 | Outer | FT CYCLE | 15 | 15 | 15 | 0 |
| ● | 1 | G-301 | Lam Outer + Lam Inner | FT CYCLE | 8 | 10 | 0 | 10 |

*(G-301 shows IN 10 = 5 panes per side: cut 8, polished 8, but mitring
only 5 done on the inner — min(8, 8, 5) = 5 per pane. When the last 3
mitred pieces land, IN grows to 16.)*

**Sidebar · G-101** — Stage totals · Pieces `30 · 20 done · 10 left` ·
Area `54 m² · 36 m² done` and the **dispatch panel** (SGU pieces leave
the factory straight from the furnace — no pairing, no doubling):

> **DISPATCH** — READY **20** · DELIVERED **8** · TO DELIVER **12**
> `[ Create Delivery Note ]`

**KPIs (Tempering):** Arrived 4 rows · Pending 1 · In progress 2 ·
Completed 1 · Load 133.2 m² / 75 pcs · Ready for dispatch **41 / 73 pcs**.

---

## ⑦ Pairing — Stage 7 (multi-pane units only)

`IN` = pieces whose **every pane** is ready (slowest pane bounds the
unit). G-201: outer tempered 15, inner annealed-ready 15 → pairs 15.

| St | Sr | Ref | Build | Envelope | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|
| ✓ | 1 | G-201 | DGU | 1800×1200 + 1790×1190 | 15 | 15 | 15 | 0 |
| ⏳ | 1 | G-301 | LAM | 2400×1500 ×2 panes | 8 | 0 | 0 | 0 |

> ⏳ G-301 — Waiting on Tempering (its laminate panes still have pieces in
> the furnace queue).

---

## ⑧ Doubling / Lamination — Stage 8 (assembly)

`IN` = the paired quantity. G-201's assembly line has doubled 9 of 15.

| St | Sr | Ref | Build | Assembly note | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|
| ◐ | 1 | G-201 | DGU | DGU ASSEMBLY · SPACER 12MM · ARGON GAS FILL | 15 | 15 | 9 | 6 |
| ⏳ | 1 | G-301 | LAM | LAMINATION · PVB 1.52MM CLEAR | 8 | 0 | 0 | 0 |

**Sidebar · G-201** — dispatch panel: READY **9** · DELIVERED 0 ·
TO DELIVER **9** — each doubled unit becomes deliverable the moment
assembly finishes it.

---

## Dispatch & delivery notes at this snapshot

Ready pieces (production `DispatchReadyQty`):
G-101 **20** (SGU, off the furnace) · G-102 **12** · G-201 **9** (off
doubling) · G-301 0 → **41 / 73 pcs** ready.

History: **DN-2026-0044-01** was dispatched earlier with 8 G-101 pieces
(the only pieces tempered at that time — the production cap made it
impossible to deliver more).

Opening the **next delivery note** for JOB-2026-0044 now:

| Row | Ordered | Prev. delivered | Production cap | Fill-all enters |
|---|---|---|---|---|
| G-101 1500×1200 | 30 | 8 | 20 − 8 = **12** | 12 |
| G-102 900×600 | 20 | 0 | **12** | 12 |
| G-201 1800×1200 | 15 | 0 | **9** | 9 |
| G-301 2400×1500 | 8 | 0 | **0** | 0 |

The dispatched pieces are never delivered twice; the next DN after this
one covers only whatever production finishes next.

---

## Reading the chain

Live work at this moment sits at **Mitring** (G-301, 3 pieces),
**Tempering** (G-101 10 · G-102 8 · G-301 10 to temper) and
**Doubling** (G-201, 6 units) — exactly what the chain bar's open counts
say. The backlog strips count units with no pieces arrived yet
(G-301 waiting at Pairing and Doubling), so nothing disappears while it
waits upstream.
