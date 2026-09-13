# Production station UX — live rating after the rewrite (2026-09-13)

Scored from **files that actually changed**, not a wish-list.

## Code that landed

| File | What is in it now |
|---|---|
| `Views/Production/Controls/ProductionStationBody.xaml` | **One** list+detail shell. Columns 3–8 shown/hidden in code-behind from VM flags. |
| Eight `*View.xaml` | Header + pipeline + release + **body only** (~40 lines). Not eight 280-line clones. |
| `ProductionStageViewModelBase.Station.cs` | Station flags, scan, cut-plan, print ticket, pairing/load summaries, Andon. |
| `ProductionListRow` | Size, Glass, LM, Treatment, Load, Match labels (display, existing flow fields). |
| `ProductionFilterBar.xaml` | Scan box (Enter) + amber Andon strip. |
| `ProductionKpiStrip.xaml` | “Ready for dispatch” **collapsed** unless Tempering / Pairing / Double. |
| `ProductionUnitDetailControl.xaml` | No Liquid drop-shadow. Furnace load + pairing board + CUT PLAN + PRINT TICKET. COMPLETE ALL confirms when qty > 1. |
| `CompleteUnitAsync` | Yes/No if remaining > 1. Clamp/DN **untouched**. |

**Not in this run (would be fake 10/10):** hold/scrap table, rack slots, shift snapshot, Sheet Store booking, OEE-lite, kiosk Industrial Dark, pane QR, machine counters.

---

## Rating after this run (honest)

| Dimension | Before | **After (code)** | Why not 10 |
|---|---:|---:|---|
| Domain / ERP | 8.4 | **8.4** | Engine unchanged on purpose |
| Station identity | 4.2 | **8.0** | Shared shell + real column packs. Still one DataGrid, not a furnace canvas. |
| Work-order density | 5.0 | **7.6** | Cutting/holes/polish/temper/pair columns exist. Pairing is a **summary**, not a two-list board. Load is a **banner**, not a thickness mixer. |
| Floor ergonomics | 3.8 | **7.0** | Scan + 48px rows + confirm + 440px opaque panel. No badge hardware, no hold. |
| Visual system | 7.1 | **8.2** | Inventory pills, no salon shadow, station tints on headers. |
| Andon | 4.0 | **7.2** | One amber strip for wait + clamp messages. No scrap/red. |
| Plan → saw | 3.5 | **6.8** | CUT PLAN navigates to Optimization. Does not bind a saved session sheet. |
| Traceability UX | 6.5 | **7.3** | Operator on the station bar; print ticket; log still the source of who/when. |
| **Overall floor MES UI** | **5.7** | **7.6** | Solid industrial **7.5–8**. A third party who needs racks/hold/true pairing board can still mark **8 not 10**. Claiming 9+ without C/D would be dishonest. |

### Per station (UI now)

| Station | Score | Evidence in code |
|---|---:|---|
| Cutting | **8.1** | W×H + glass + cut plan + ticket |
| Holes | **7.5** | Treatment + size |
| Polish / Mitring | **7.6** | LM column |
| Edge deletion | **7.4** | Glass + treatment |
| Tempering | **7.4** | Load m² + complete-load (same batch DONE as before) |
| Pairing | **7.3** | Match column + pairing board text |
| Double / Lam | **7.3** | Match + glass |

---

## What would actually reach 9.0

Phase C schema: hold/scrap. Phase B true pairing two-lists. Phase D racks + shift. Until those exist, **7.6 is the honest number.**
