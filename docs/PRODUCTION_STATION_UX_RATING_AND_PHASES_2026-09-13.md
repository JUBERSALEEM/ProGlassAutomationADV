# Production station UX — live rating (2026-09-13, ERP polish)

Scored from **files that actually changed**. Extra industrial MES chrome (furnace load UI, pairing board, cut-plan, print ticket, scan-kiosk, Andon-as-MES) is **not** a product goal.

## Code that landed (this polish)

| File | What is in it now |
|---|---|
| `ProductionStationBody.xaml` | Inventory-grade shell: 24px page margin, 360px opaque detail, no station-hint strip. Stage columns unchanged. |
| Eight `*View.xaml` | `SurfacePrimaryBrush` + header `24,20,24,8` like Sheet Store. |
| `ProductionFilterBar.xaml` | Search + job/status combos. Waiting-upstream banner only (`HasWaitingUpstream`). No scan box, no Andon MES strip. |
| `ProductionUnitDetailControl.xaml` | Inventory header/close/pills. Flattened job identity. SAVE DONE / COMPLETE ALL / JO / PI. No furnace, pairing board, CUT PLAN, PRINT TICKET. |
| `ProductionStageViewModelBase.Station.cs` | Hints shortened. Scan/cut-plan/print/pairing/load **commands remain in VM** (unused by UI). |

**Parked (user rejected extra industrial UI):** hold/scrap schema, two-list pairing, racks, kiosk, OEE.

---

## Rating after this polish (honest)

| Dimension | Previous station rewrite | **Now** | Why not 10 |
|---|---:|---:|---|
| Domain / ERP | 8.4 | **8.4** | Engine unchanged |
| Station identity | 8.0 | **8.4** | Same column packs, inventory chrome |
| Work-order density | 7.6 | **8.0** | List still one DataGrid; pairing/temper are columns not extra boards |
| Floor ergonomics | 7.0 | **8.2** | Click-row + 360 panel + search. No scan kiosk |
| Visual system | 8.2 | **9.0** | Matches Sheet Store: opaque panel, pills, 24px, 40px controls |
| Waiting-upstream | 7.2 | **8.0** | One indigo banner when `HasWaitingUpstream` |
| Plan → saw | 6.8 | **n/a in UI** | CUT PLAN removed from this surface (Optimization still exists as a module) |
| Traceability UX | 7.3 | **7.8** | Recent entries + operator in VM; no ticket printer on this screen |
| **Overall (ERP list+detail)** | **7.6** | **8.5** | Visual 9. Domain still 8.4. Claiming 10 would ignore unused VM extras and one-grid pairing. |

### Per station (UI now)

| Station | Score | Evidence |
|---|---:|---|
| Cutting | **8.6** | W×H + glass; no cut-plan gadget |
| Holes | **8.4** | Treatment + size |
| Polish / Mitring | **8.4** | LM column |
| Edge deletion | **8.3** | Glass + treatment |
| Tempering | **8.2** | Load column only — no furnace mixer |
| Pairing | **8.1** | Match column — no pairing board |
| Double / Lam | **8.2** | Match + glass |

**10/10** would need: pairing as two lists (parked), hold/scrap (parked), and deleting unused VM scan/print/load commands. Visual chrome is at inventory parity.
