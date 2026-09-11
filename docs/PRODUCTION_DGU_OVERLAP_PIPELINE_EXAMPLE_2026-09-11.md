# DGU Overlap Job — Full Production Pipeline Example to Delivery-Note Dispatch

**Date:** 2026-09-11 · Companion to `PRODUCTION_STEP4_MAPPING_ANALYSIS_2026-09-11.md`

One **DGU overlap** Job Order traced through all eight production modules,
the dispatch panel, and two dispatched delivery notes — every quantity at
every phase follows the piece-flow engine.

---

## The job

**PI-2026-0160** · Marina Gate Tower · Nakheel Interiors LLC · salesman
**Ravi Kumar** → converted to **JOB-2026-0052** (Released).

**Step 4** — one spec group, build **DGU**:
`8mm Clear FT Glass + 12mm Argon + 6mm Clear Annealed Glass`
(outer lite tempers; inner lite annealed — no furnace).

**Step 5** — overlap rows (W2/H2 **filled** ⇒ overlap glass, inner cut to
its own size):

| Sr | Ref | W1×H1 (outer) | W2×H2 (inner) | Qty |
|---|---|---|---|---|
| 1 | G-501 | 2000×1500 | 1800×1300 | 20 |
| 2 | G-502 | 1500×1000 | 1300×800 | 10 |

**Step 6:** FAN_HOLES ×4 (Spec 1) · POLISH All-Sided (Spec 1) ·
ARGON GAS (Spec 1).

Per-pane maths: G-501 outer 3.00 m² / 7.0 LM · inner 2.34 m² / 6.2 LM.
G-502 outer 1.50 m² / 5.0 LM · inner 1.04 m² / 4.2 LM.

Every unit row carries the badge
**`OVERLAP GLASS — inner 1800×1300`** (or 1300×800) in the sidebar of
every module, and both envelopes W1/H1 + W2/H2 render on every screen.

---

## Phase 0 — Release (any module's release bar)

`Release from Job Order: JOB-2026-0052 · Nakheel (Released)` → **Release**.
The engine distributes, per pane occurrence from the Step 4 build
(qty follows the row — 20 units ⇒ outer 20 + inner 20 pieces):

| Stage | Items created |
|---|---|
| Cutting | G-501 Outer 2000×1500 ×20 · G-501 Inner 1800×1300 ×20 · G-502 Outer ×10 · G-502 Inner ×10 |
| Holes & Cutouts | FAN HOLES · 4 QTY on all four panes |
| Polish | POLISH · ALL SIDED on all four panes (LM) |
| Tempering | **outers only** — `8MM FT CYCLE` (inner is annealed) |
| Pairing | G-501 ×20 units · G-502 ×10 units (`2-PANE UNIT`) |
| Doubling | `DGU ASSEMBLY · SPACER 12MM · ARGON GAS FILL` ×20 / ×10 |

Chain bar: everything 0 except Cutting `60 arrived`.

---

## Phase 1 — Cutting in progress (G-501 outer 12 of 20 cut)

Floor state: G-501 **outer 12/20 cut, inner 20/20 cut** · G-502 untouched.

### ① Cutting
| Sr | Ref | Envelope | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|
| ◐ | 1 | G-501 | 2000×1500 / 1800×1300 | 20 | 40 | 32 | 8 |
| ● | 2 | G-502 | 1500×1000 / 1300×800 | 10 | 20 | 0 | 20 |

**Sidebar G-501** — items `Outer 2000×1500 (IN 20 · DONE 12)` and
`Inner 1800×1300 (IN 20 · DONE 20)`; badge `OVERLAP GLASS — inner
1800×1300`; Treatments `FT · Annealed`; Stage totals — Pieces
`40 · 32 done · 8 left` · Area `106.8 m² · 82.8 m² done` · LM
`264 LM · 208 LM done`.

### ② Holes & Cutouts (pieces flow as they are cut)
| Sr | Ref | IN | DONE | BAL |
|---|---|---|---|---|
| ● | 1 | G-501 | 32 | 0 | 32 |

*(G-502 waits upstream — counted in the waiting strip. The sidebar shows
two cards per unit: `Outer … FAN HOLES · 4 QTY · IN 12` and
`Inner … FAN HOLES · 4 QTY · IN 20` — the drill crew starts on the inner
pieces already cut.)*

### ③ Polish
Same shape: G-501 `IN 32 · DONE 0` — the edging line polishes cut pieces
as they arrive (LM totals in the sidebar).

### ⑥ Tempering / ⑦ Pairing / ⑧ Doubling
**Waiting** — no piece has finished cutting + edge work yet
(`Waiting on Cutting / Holes & Cutouts / Polish`).

---

## Phase 2 — G-501 walks the chain to doubling

Floor state: G-501 **fully cut (40), fan-holed (40 ops), polished (40),
outer lites tempered 20/20, paired 20/20, doubled 14 of 20**.
G-502: outer 6/10 + inner 10/10 cut, edges in progress.

| Module | G-501 row | G-502 row |
|---|---|---|
| ① Cutting | IN 40 · DONE 40 · BAL 0 ✓ | IN 20 · DONE 16 · BAL 4 |
| ② Holes | IN 40 · DONE 40 ✓ | IN 16 · DONE 0 |
| ③ Polish | IN 40 · DONE 40 ✓ (264 LM done) | IN 16 · DONE 0 |
| ④ Edge Deletion | empty state — no charge feeds it | — |
| ⑤ Mitring | not allocated (no charge) | — |
| ⑥ Tempering | outer IN 20 · DONE 20 ✓ (inner never appears — annealed) | waiting |
| ⑦ Pairing | IN 20 · DONE 20 ✓ | waiting |
| ⑧ Doubling | IN 20 · DONE **14** · BAL 6 | waiting |

**Sidebar G-501 at Doubling** — Pieces `20 · 14 done · 6 left` (assembly
stages track units; glass volume stays with the pane stages) ·
**Dispatch panel: READY 14 · DELIVERED 0 · TO DELIVER 14** ·
`[ Create Delivery Note ]`

**KPIs (Doubling):** Arrived 1 row · In progress 1 · Load 20 units to
assemble · Ready for dispatch **14 / 30** units.

Chain bar: `①Cutting 24 open · 36/60 · ②Holes 24 open · 40/64* ·
③Polish same · ⑥Temper 10 open · 20/30 · ⑦Pair ✓ 20/20 · ⑧Dbl 6 open · 14/20`
(*G-502's 16 arrived pieces count in holes/polish totals.)

---

## Phase 3 — First delivery note (DN-2026-0052-01)

Production sidebar → **Create Delivery Note** for JOB-2026-0052:
`DeliveryNoteCreateService` creates the next DN from remaining balance,
capped to production-finished pieces:

| DN row | Ordered | Prev. delivered | Production cap | Entered |
|---|---|---|---|---|
| G-501 2000×1500 / 1800×1300 | 20 | 0 | **14** | 14 |
| G-502 1500×1000 / 1300×800 | 10 | 0 | **0** | 0 |

Trying to enter 20 on G-501 clamps to 14 — *"Production ready: 14 pc(s) —
the rest follows as production completes."*

Dispatch the DN: driver **Ahmed K.** · vehicle **DXB-4412** · receiver
**Site-B Store** → status **Dispatched** (confirmed). The master DN
records 14 delivered; the sidebar flips to
**READY 14 · DELIVERED 14 · TO DELIVER 0**.

---

## Phase 4 — The balance finishes

Floor state: G-501 doubling completes the last **6 units** (20/20).
G-502 walks its chain: cut 20/20 → holed 20 → polished 20 → outer
tempered 10/10 → paired 10/10 → doubled **10/10**.

| Module | G-501 | G-502 |
|---|---|---|
| ①–③ (cut/holes/polish) | ✓ 40/40 each | ✓ 20/20 each |
| ⑥ Tempering | ✓ outer 20/20 | ✓ outer 10/10 |
| ⑦ Pairing | ✓ 20/20 | ✓ 10/10 |
| ⑧ Doubling | ✓ 20/20 | ✓ 10/10 |

**Dispatch panels:** G-501 `READY 20 · DELIVERED 14 · TO DELIVER 6` ·
G-502 `READY 10 · DELIVERED 0 · TO DELIVER 10`.
**KPI Ready for dispatch: 30 / 30 units · 2 unit(s) complete.**

Chain bar: every stage cleared —
`①✓ 60/60 · ②✓ 60/60 · ③✓ 60/60 · ④— · ⑤— · ⑥✓ 30/30 · ⑦✓ 30/30 · ⑧✓ 30/30`.

---

## Phase 5 — Second delivery note (balance only)

**Create Delivery Note** again — the rule holds: *the next DO covers only
the pieces not yet dispatched*:

| DN-2026-0052-02 row | Ordered | Prev. delivered | Production cap | Entered |
|---|---|---|---|---|
| G-501 | 20 | 14 | 20 − 14 = **6** | 6 |
| G-502 | 10 | 0 | **10** | 10 |

Dispatch → **Dispatched**. Master balances hit zero. Pressing
**Create Delivery Note** once more returns:

> All confirmed deliveries for this job order are complete.
> Job Order : JOB-2026-0052 · Ordered : 30 units · Delivered : 30 · Balance : 0 —
> open God View to inspect dispatched delivery notes.

The production screens stay live as history: every module shows ✓ with
IN = DONE and BAL 0, and the dispatch panels read
`DELIVERED = READY` for both rows.

---

## What the overlap semantics bought this job

- Both lites carried their **own** envelope through all eight screens —
  outer 2000×1500 to the furnace, inner 1800×1300 straight from cutting
  to pairing (annealed).
- Overlap detection is automatic from Step 5 (W2/H2 filled) and stated on
  every sidebar (`OVERLAP GLASS — inner …`); a blank W2/H2 row would have
  shown `EQUAL-SIZE UNIT — both lites …` and cut the inner at W1×H1.
- Quantities always read from the Step 4 build: 20 units meant 20 outers
  + 20 inners + 20 pairings + 20 assemblies — tracked piece by piece.
- Dispatch was a quantity all the way: 14 units → DN-01, then 6 + 10 →
  DN-02 — never a re-delivery, never more than production finished.
