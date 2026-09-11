# DGU Overlap Job — Full Production Pipeline Example to Delivery-Note Dispatch

**Date:** 2026-09-11 · Companion to `PRODUCTION_STEP4_MAPPING_ANALYSIS_2026-09-11.md`

One **DGU overlap** Job Order traced through all eight production modules,
the dispatch panel, and two dispatched delivery notes. Every module table
below uses the **full production row shape** exactly as the screens render
it — ref, outer size W1/H1, inner (overlap) size W2/H2, qty, SQM and LM
per pane, plus this stage's IN / DONE / BALANCE.

---

## The job

**PI-2026-0160** · Marina Gate Tower · Nakheel Interiors LLC · salesman
**Ravi Kumar** → converted to **JOB-2026-0052** (Released).

**Step 4** — one spec group, build **DGU**:
`8mm Clear FT Glass + 12mm Argon + 6mm Clear Annealed Glass`
(outer lite tempers; inner lite annealed — no furnace).

**Step 5** — overlap rows (W2/H2 **filled** ⇒ overlap glass, inner lite
cut to its own size):

| Sr | Ref | W1·H1 (outer) | W2·H2 (inner) | Qty | SQM1 | TOT SQM1 | LM1 | SQM2 | TOT SQM2 | LM2 |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | G-501 | 2000×1500 | 1800×1300 | 20 | 3.00 | 60.00 | 7.0 | 2.34 | 46.80 | 6.2 |
| 2 | G-502 | 1500×1000 | 1300×800 | 10 | 1.50 | 15.00 | 5.0 | 1.04 | 10.40 | 4.2 |

**Step 6:** FAN_HOLES ×4 (Spec 1) · POLISH All-Sided (Spec 1) ·
ARGON GAS (Spec 1).

**Shared columns on every row of every module below:**
JOB `JOB-2026-0052` · PI NO `PI-2026-0160` · CUSTOMER `Nakheel Interiors LLC`
· SALESMAN `Ravi Kumar` · SPEC `Spec 1 · 8mm Clear FT + 12A Argon + 6mm Clear
Annealed` · BUILD `DGU`. Every sidebar also shows the badge
**`OVERLAP GLASS — inner 1800×1300`** (G-502: 1300×800) and treatments
`FT · Annealed`.

---

## Phase 0 — Release (any module's release bar)

`Release from Job Order: JOB-2026-0052 · Nakheel (Released)` → **Release**.
The engine distributes per pane occurrence from the Step 4 build — qty
follows the row: 20 units ⇒ outer 20 pieces + inner 20 pieces:

| Stage | Items created |
|---|---|
| Cutting | G-501 Outer 2000×1500 ×20 · G-501 Inner 1800×1300 ×20 · G-502 Outer 1500×1000 ×10 · G-502 Inner 1300×800 ×10 |
| Holes & Cutouts | FAN HOLES · 4 QTY on all four panes |
| Polish | POLISH · ALL SIDED on all four panes (LM) |
| Tempering | **outers only** — `8MM FT CYCLE` (inner is annealed) |
| Pairing | G-501 ×20 units · G-502 ×10 units (`2-PANE UNIT`) |
| Doubling | `DGU ASSEMBLY · SPACER 12MM · ARGON GAS FILL` ×20 / ×10 |

Chain bar: everything 0 except Cutting `60 arrived`.

---

## Phase 1 — Cutting in progress (G-501: outer 12/20, inner 20/20 cut)

### ① Cutting
| St | Sr | Ref | W1 | H1 | W2 | H2 | Qty | SQM1 | TOT SQM1 | LM1 | SQM2 | TOT SQM2 | LM2 | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ◐ | 1 | G-501 | 2000 | 1500 | 1800 | 1300 | 20 | 3.00 | 60.00 | 7.0 | 2.34 | 46.80 | 6.2 | 40 | 32 | 8 |
| ● | 2 | G-502 | 1500 | 1000 | 1300 | 800 | 10 | 1.50 | 15.00 | 5.0 | 1.04 | 10.40 | 4.2 | 20 | 0 | 20 |

**Sidebar G-501** — detail cards `Outer 2000×1500 · Qty 20 (IN 20 · DONE 12)`
and `Inner 1800×1300 · Qty 20 (IN 20 · DONE 20)`; Stage totals — Pieces
`40 · 32 done · 8 left` · Area `106.8 m² · 82.8 m² done` · LM
`264 LM · 208 LM done`.

### ② Holes & Cutouts (cut pieces flow straight in)
| St | Sr | Ref | W1 | H1 | W2 | H2 | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|---|---|
| ● | 1 | G-501 | 2000 | 1500 | 1800 | 1300 | 20 | 32 | 0 | 32 |

*(G-502 has no pieces arrived yet → counted in the waiting strip, not
listed. Sidebar cards: `Outer 2000×1500 · FAN HOLES · 4 QTY · IN 12` and
`Inner 1800×1300 · FAN HOLES · 4 QTY · IN 20` — drilling starts on the
pieces already cut.)*

### ③ Polish (LM stage, same arrivals)
| St | Sr | Ref | W1 | H1 | W2 | H2 | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|---|---|
| ● | 1 | G-501 | 2000 | 1500 | 1800 | 1300 | 20 | 32 | 0 | 32 |

### ④ Edge Deletion — empty state (no Step-6 charge feeds it)
### ⑤ Mitring — no units allocated (no mitring charge on this job)

### ⑥ Tempering / ⑦ Pairing / ⑧ Doubling
**Waiting** — no piece has finished cutting + edge work yet
(`Waiting on Cutting / Holes & Cutouts / Polish`).

---

## Phase 2 — G-501 walks the chain to doubling

Floor state: G-501 fully cut (40), fan-holed (40 ops), polished (40),
outer lites tempered 20/20, paired 20/20, doubled 14 of 20 · G-502:
outer 6/10 + inner 10/10 cut, edges in progress.

### ① Cutting
| St | Sr | Ref | W1 | H1 | W2 | H2 | Qty | SQM1 | TOT SQM1 | LM1 | SQM2 | TOT SQM2 | LM2 | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ✓ | 1 | G-501 | 2000 | 1500 | 1800 | 1300 | 20 | 3.00 | 60.00 | 7.0 | 2.34 | 46.80 | 6.2 | 40 | 40 | 0 |
| ◐ | 2 | G-502 | 1500 | 1000 | 1300 | 800 | 10 | 1.50 | 15.00 | 5.0 | 1.04 | 10.40 | 4.2 | 20 | 16 | 4 |

### ② Holes & Cutouts
| St | Sr | Ref | W1 | H1 | W2 | H2 | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|---|---|
| ✓ | 1 | G-501 | 2000 | 1500 | 1800 | 1300 | 20 | 40 | 40 | 0 |
| ● | 2 | G-502 | 1500 | 1000 | 1300 | 800 | 10 | 16 | 0 | 16 |

### ③ Polish (sidebar stage totals read the work in LM)
| St | Sr | Ref | W1 | H1 | W2 | H2 | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|---|---|
| ✓ | 1 | G-501 | 2000 | 1500 | 1800 | 1300 | 20 | 40 | 40 | 0 |
| ● | 2 | G-502 | 1500 | 1000 | 1300 | 800 | 10 | 16 | 0 | 16 |

*(G-501 sidebar: LM `264 LM · 264 LM done · 0 left`.)*

### ④ Edge Deletion — still empty · ⑤ Mitring — not allocated

### ⑥ Tempering (outers only — the annealed inner never enters the furnace)
| St | Sr | Ref | Pane | Treatment | W×H | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|---|
| ✓ | 1 | G-501 | Outer | 8MM FT CYCLE | 2000×1500 | 20 | 20 | 20 | 0 |
| ⏳ | 2 | G-502 | Outer | 8MM FT CYCLE | 1500×1000 | 10 | 0 | 0 | 0 |

*(G-502 waits on Holes & Cutouts / Polish. G-501's IN was bounded by
min(cut 20, holed 20, polished 20) = 20.)*

### ⑦ Pairing (IN = slowest pane ready)
| St | Sr | Ref | Envelope (outer / inner) | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|
| ✓ | 1 | G-501 | 2000×1500 / 1800×1300 | 20 | 20 | 20 | 0 |
| ⏳ | 2 | G-502 | 1500×1000 / 1300×800 | 10 | 0 | 0 | 0 |

### ⑧ Doubling / Lamination
| St | Sr | Ref | Assembly note | Envelope (outer / inner) | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|
| ◐ | 1 | G-501 | DGU ASSEMBLY · SPACER 12MM · ARGON GAS FILL | 2000×1500 / 1800×1300 | 20 | 20 | 14 | 6 |

**Sidebar G-501 at Doubling** — Pieces `20 · 14 done · 6 left` (assembly
stages track units; glass volume lives on the pane stages) ·
**Dispatch panel: READY 14 · DELIVERED 0 · TO DELIVER 14** ·
`[ Create Delivery Note ]`

**KPIs (Doubling):** Arrived 1 row · In progress 1 · Load 20 units to
assemble · Ready for dispatch **14 / 30** units.

Chain bar: `①Cutting 24 open · 36/60 · ②Holes 16 open · 40/56 ·
③Polish 16 open · 40/56 · ⑥Temper ✓ 20/20 · ⑦Pair ✓ 20/20 ·
⑧Dbl 6 open · 14/20`.

---

## Phase 3 — First delivery note (DN-2026-0052-01)

Production sidebar → **Create Delivery Note** for JOB-2026-0052 — created
from remaining balance, capped to production-finished pieces:

| DN row | Ref | W1·H1 / W2·H2 | Ordered | Prev. delivered | Production cap | Entered |
|---|---|---|---|---|---|---|
| G-501 | G-501 | 2000×1500 / 1800×1300 | 20 | 0 | **14** | 14 |
| G-502 | G-502 | 1500×1000 / 1300×800 | 10 | 0 | **0** | 0 |

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

### ① Cutting (final)
| St | Sr | Ref | W1 | H1 | W2 | H2 | Qty | SQM1 | TOT SQM1 | LM1 | SQM2 | TOT SQM2 | LM2 | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ✓ | 1 | G-501 | 2000 | 1500 | 1800 | 1300 | 20 | 3.00 | 60.00 | 7.0 | 2.34 | 46.80 | 6.2 | 40 | 40 | 0 |
| ✓ | 2 | G-502 | 1500 | 1000 | 1300 | 800 | 10 | 1.50 | 15.00 | 5.0 | 1.04 | 10.40 | 4.2 | 20 | 20 | 0 |

### ⑥ Tempering (final)
| St | Sr | Ref | Pane | Treatment | W×H | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|---|
| ✓ | 1 | G-501 | Outer | 8MM FT CYCLE | 2000×1500 | 20 | 20 | 20 | 0 |
| ✓ | 2 | G-502 | Outer | 8MM FT CYCLE | 1500×1000 | 10 | 10 | 10 | 0 |

### ⑧ Doubling / Lamination (final)
| St | Sr | Ref | Assembly note | Envelope (outer / inner) | Qty | IN | DONE | BAL |
|---|---|---|---|---|---|---|---|---|
| ✓ | 1 | G-501 | DGU ASSEMBLY · SPACER 12MM · ARGON GAS FILL | 2000×1500 / 1800×1300 | 20 | 20 | 20 | 0 |
| ✓ | 2 | G-502 | DGU ASSEMBLY · SPACER 12MM · ARGON GAS FILL | 1500×1000 / 1300×800 | 10 | 10 | 10 | 0 |

(② Holes · ③ Polish mirror the Cutting final: G-501 40/40 · G-502 20/20 ·
⑦ Pairing: G-501 20/20 · G-502 10/10.)

**Dispatch panels:** G-501 `READY 20 · DELIVERED 14 · TO DELIVER 6` ·
G-502 `READY 10 · DELIVERED 0 · TO DELIVER 10`.
**KPI Ready for dispatch: 30 / 30 units · 2 unit(s) complete.**

Chain bar: every stage cleared —
`①✓ 60/60 · ②✓ 60/60 · ③✓ 60/60 · ④— · ⑤— · ⑥✓ 30/30 · ⑦✓ 30/30 · ⑧✓ 30/30`.

---

## Phase 5 — Second delivery note (balance only)

**Create Delivery Note** again — the rule holds: *the next DO covers only
the pieces not yet dispatched*:

| DN-2026-0052-02 row | Ref | W1·H1 / W2·H2 | Ordered | Prev. delivered | Production cap | Entered |
|---|---|---|---|---|---|---|
| G-501 | G-501 | 2000×1500 / 1800×1300 | 20 | 14 | 20 − 14 = **6** | 6 |
| G-502 | G-502 | 1500×1000 / 1300×800 | 10 | 0 | **10** | 10 |

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

- Both lites carried their **own** ref-and-size envelope through all eight
  screens — G-501 outer **2000×1500** to the furnace, inner **1800×1300**
  straight from cutting to pairing (annealed); SQM2/LM2 columns tracked
  the inner pane's 2.34 m² / 6.2 LM on every module.
- Overlap detection is automatic from Step 5 (W2/H2 filled) and stated on
  every sidebar (`OVERLAP GLASS — inner …`); a blank W2/H2 row would have
  shown `EQUAL-SIZE UNIT — both lites …` and cut the inner at W1×H1.
- Quantities always read from the Step 4 build: 20 units meant 20 outers
  + 20 inners + 20 pairings + 20 assemblies — tracked piece by piece.
- Dispatch was a quantity all the way: 14 units → DN-01, then 6 + 10 →
  DN-02 — never a re-delivery, never more than production finished.
