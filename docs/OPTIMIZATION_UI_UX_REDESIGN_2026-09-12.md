# Optimization Module — UI/UX Audit, Rating & 10/10 Phase Plan (2026-09-12)

Senior UI/UX audit of the Optimization module against the industrial-ERP
standard (dusk-navy liquid-glass design system). This doc gives the
current rating, the concrete issues found, and the phased plan to reach
10/10 — with the Phase-1 fixes already implemented.

---

## 1. Current rating

| Axis | Score | Note |
|---|---|---|
| Layout / grid discipline | 4/10 | Overlapping toolbar buttons in STOCKS; 2D filmstrip pushed the canvas out of centre |
| Card & surface language | 6/10 | Rounded glass cards exist but inputs used the default square WPF chrome |
| Typography hierarchy | 7/10 | OptHeader/Title/Value scale is sound; some rows over-labelled |
| Colour / theme compliance | 7/10 | DynamicResource-aware, but 2D view mixed margin + breakout semantics in one colour |
| Navigation ergonomics | 5/10 | 2D view forced "show all sheets" filmstrip instead of prev/centre/next |
| Data density & readability | 7/10 | SUMMARY order was alphabetical, not max→lowest utilization |
| Reporting integration | 3/10 | No session report surface (layout/parts/stocks) |
| **Overall** | **5.5/10** | Functional, theme-aware, but not "professionally designed software" yet |

---

## 2. Issues found (mapped to the report)

1. **STOCKS toolbar overlap** — "Add stock" used a left-margin offset inside
   an auto-sized column, so it and "Import Step 4 sheets" overlapped at
   narrow widths. *(fixed — dedicated columns)*
2. **No per-sheet "use in run" control** — the run consumed every imported
   rack; the operator could not exclude a sheet for a spec. *(fixed —
   `IsUsedInRun` checkbox, Step 4 sheets auto-selected, unticked racks
   never fed to the optimizer)*
3. **SUMMARY ordering** — per-group breakdown listed alphabetically; the
   owner wants result summary first, then max → lowest utilization.
   *(fixed — `ApplyResults` sorts by utilization descending)*
4. **2D view filmstrip** — "show all sheets" strip was noisy; required
   prev/centre/next navigation. *(fixed)*
5. **2D view colour semantics** — LM/RM/TM/BM margin zone and minimum
   breakout shared the same accent colour; the floor rule is two-tone.
   *(fixed — margin zone = accent dashed, breakout band = amber dotted)*
6. **2D view canvas alignment** — canvas was left-anchored, not centred.
   *(fixed — centred with proper width/height and overflow scrolling)*
7. **Input chrome** — `OptIntInput` used the default square TextBox; cards
   are 12px-rounded. *(fixed — rounded 8px template, focus ring, caret)*
8. **No session reports** — no way to produce Layout / Parts / Stocks
   summaries. *(fixed — new REPORT tab + CSV export via the shared
   `ReportingExportService`, formula-injection-safe, UTF-8 BOM)*

---

## 3. Phase plan → 10/10

### Phase 1 — structural + reported defects  ✅ (this commit)
- STOCKS: non-overlapping toolbar, USE column, auto-selected Step 4 sheets.
- SUMMARY: max→lowest utilization ordering.
- 2D VIEW: prev/centre/next navigator, centred canvas, two-tone
  margin/breakout overlay, legend.
- Inputs rounded to match cards.
- REPORT tab: layout/parts/stocks summaries + CSV export.

### Phase 2 — visual polish  ✅ (done)
- Elevate KPI cards with the shared `CardStyles` treatment (top accent
  hairline, refined shadow) instead of the module's bespoke `OptKpiCard`.
- Unify card corner radius tokens (10/12px) into a single `OptCorner`
  token; remove per-view literal radii.
- Add a sheet thumbnail mini-map inside the 2D centre read-out.
- Per-sheet utilisation colour coding (green ≥85%, amber 70–84%,
  red <70%). ✅ *(done — SUMMARY sheet rows and the industrial report
  utilisation lines are tone-coded via `UtilizationTone`)*

### Phase 3 — reporting depth  ✅ (done)
- **Industrial cut report (owner format).** ✅ *(done — the REPORT tab now
  leads with the owner's production report: grouped by glass composition,
  `6MM THICKNESS …(SPEC) 3210 * 2250 SHEET UTILIZED QTY 17 (SHEET SIZE)
  - 82.77% UTILIZATION INNER` utilisation lines, then a
  `GLASS REF / W1 / H1 / W2 / H2 / QTY / TOTAL SQM` piece table per spec;
  exported through the shared CSV engine as
  `Optimization_Industrial_Cut_Report`.)*
- CSV + Excel + PDF export for every summary via the shared
  `ReportingExportService`; Excel/PDF render the industrial report as a
  sectioned table.
- "Export all three summaries" combined report action (one document,
  grouped by a leading SECTION column).
- Persist optimisation sessions (saved layouts reopen into the REPORT
  tab) — `SavedOptimizationSession` + `OptimizationSessionRepository` +
  `OptimizationViewModel.Session.cs` with Save / Open / Delete.

---

## 4. Constraints honoured

- No mockups — implemented directly.
- Theme stays DynamicResource (light/dark compliant); no hard-coded
  colours introduced (only existing brushes: Accent, Warning, Info,
  Success, Divider, GlassBorderGradientSoft, Tint*).
- Engine untouched — the packer remains pinned by
  `OptimizationBenchmarkTests` / `OptimizationEngineTests`.
