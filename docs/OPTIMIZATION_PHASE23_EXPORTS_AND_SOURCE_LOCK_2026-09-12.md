# Optimization — Phase 2/3, Exports & Source-Lock Fixes (2026-09-12)

Companion to `OPTIMIZATION_UI_UX_REDESIGN_2026-09-12.md`. This round
closes the two reported defects (wrong-PI import, sheet-count mismatch),
finishes Phase 2 visual polish and Phase 3 reporting depth, and adds
session persistence. The engine is untouched — the packer remains pinned
by `OptimizationBenchmarkTests` / `OptimizationEngineTests`.

---

## 1. Source lock — a Job Order can never read a voided PI's Step 5

**Reported:** the JOBS picker showed a job tied to the live `PI-2026-001`
but imported Step 5 sizes from its voided twin `PI-2026-002` (same base
number, soft-closed).

**Root cause:** a Job Order's own `IsVoid` flag only tracks voiding the
JO — it says nothing about the Proforma Invoice it was converted from.
A JO raised from a voided PI therefore passed every "non-void JO" check
and kept pulling the dead quote's cutting data.

**Fix (three layers, each independent):**

1. `JobOrderProformaPickerViewModel.LoadAsync` — voided quotes are
   excluded from the picker outright and the count line reports how many
   were hidden ("N voided quote(s) excluded"). A voided quote can no
   longer be selected to raise a job.
2. `SavedJobOrdersListViewModel.CreateJobOrderAsync` — a hard guard after
   `GetByProformaIdAsync` blocks conversion of a voided quote with a
   warning, covering every other entry point.
3. `OptimizationViewModel.Jobs.LoadJobsAsync` — reads the PI void flags
   (`GetListRowsAsync`) and excludes any JO whose `SourceProformaId` is
   voided. Each job card now also shows **"from PI-xxxx"** so the
   operator always sees which Step 5 is being imported.

The PI store read is defensive: if it is unavailable the picker degrades
to the old behaviour and logs a warning rather than breaking.

---

## 2. Sheet count — SUMMARY 17/17/1 now matches REPORT 17/17/1

**Reported:** SUMMARY showed per-group sheets 17, 17, 1 while the REPORT
(STOCKS summary / STOCKS tab) showed 34, 34, 1.

**Root cause:** `RefreshStockUsage` keyed run usage by `(width, height)`
only. When a job's outer and inner panes share one sheet size (two
3210×2250 racks — one low-e, one clear), BOTH racks reported the
combined 34 sheets (17 + 17) instead of their own 17.

**Fix:** usage is now keyed by `(width, height, composition)` so each
Step 4 rack's "used" tally is scoped to its own cutting group. Manual
"any group" racks (no composition tag) still report every group that
packed that size.

---

## 3. Exports — CSV + Excel + PDF, plus a combined "Export all"

`ViewModels/Optimization/OptimizationReportExport.cs` routes every
optimization report through the central `ReportingExportService`:

- **Industrial cut report** — CSV keeps the owner format (utilisation
  line(s) + `GLASS REF / W1 / H1 / W2 / H2 / QTY / TOTAL SQM` table per
  composition). Excel/PDF render the same data as a sectioned table
  (composition = section, utilisation line in its own column).
- **Layout / Parts / Stocks summaries** — all three formats.
- **Export all summaries** — one document grouped by a leading SECTION
  column (LAYOUT SUMMARY / PARTS SUMMARY / STOCKS SUMMARY), in CSV,
  Excel or PDF.

The REPORT tab buttons now read **CSV · Excel · PDF** per section, with
the format travelling as the button's `CommandParameter`; the header
carries the **Export all summaries** action. Excel mappers pass typed
numbers so numeric columns stay numeric.

---

## 4. Phase 2 — visual polish

- **KPI elevation** — `OptKpiCard` now carries the shared CardStyles
  treatment: `ShadowSoft` elevation plus a 1px top accent hairline over
  the glass border (a small `ControlTemplate`; all brushes remain
  DynamicResource).
- **Single `OptCorner` token** — a `CornerRadius` resource in
  `OptimizationStyles.xaml`; `OptPanelCard` / `OptInnerCard` /
  `OptKpiCard` all read it instead of scattered 10/12px literals.
- **2D mini-map thumbnail** — the 2D centre read-out now shows a fixed
  120px-wide, zoom-independent scaled render of the whole current sheet
  (`MiniMapRects` / `MiniMapWidth` / `MiniMapHeight`), so the operator
  sees the full layout at a glance even while zoomed in.

---

## 5. Phase 3 — session persistence

- `Models/Optimization/SavedOptimizationSession` (existing) + new
  `Services/Optimization/OptimizationSessionRepository` (thin CRUD over
  `OptimizationDbContext`, following the InquiryRepository pattern).
- `OptimizationViewModel.Session.cs` — `SaveSessionCommand` snapshots the
  cutting list, Step 4 stock pool and margin settings as JSON payloads;
  `OpenSessionCommand` restores them and re-runs the SAME engine stage
  (deterministic, so the reopened layout is byte-for-byte the saved one)
  then lands on the REPORT tab; `DeleteSessionCommand` removes a session.
- REPORT tab gains a **5 · SAVED SESSIONS** section: Save / Refresh
  actions and an Open / Delete list.

---

## 6. Constraints honoured

- Theme stays DynamicResource (light/dark compliant); no hard-coded
  colours introduced.
- Engine untouched.
- No god files — export dispatch, session persistence and the preview
  mini-map each live in their own partial / helper.
