# Reports Hub — Full Module Audit (2026-09-10)

Every report module audited: filter-coverage matrix (rows vs summary vs
view), KPI live-update chain, load-lifecycle races, KPI scoping.

## Fixed

1. **PI "QUANTITY" KPI card read a permanent 0** — `PiReportSummary
   .TotalQty/.TotalSqm` were never assigned anywhere (only per-row page
   enrichment set them on `PiReportRow`). The card now shows real pcs/m²:
   bounded snapshot scan over the filtered set (newest-first, capped at
   1000 like the GlassSpec engine), best-effort with a logged warning.
   The summary reloads on every filter/search change (debounced), so the
   whole strip — TOTAL PI / CONFIRMED / CONVERTED / QUANTITY — is live
   against the filtered scope.
2. **Stale-load spinner race in all 12 report VMs** — a superseded
   load's `finally { IsLoading = false; }` cleared the spinner while the
   newer load was still in flight. `ReportBaseViewModel` now keeps a
   monotonic `_loadGen`; each VM captures the generation after
   `CancelPrevious()` and only the newest load may end the loading state.
   (Same class of race fixed earlier in the Executive VM.)
3. **GlassSpec KPI aggregation dropped the Status filter** —
   `GetSpecAggregationsAsync` cloned the filter without `Status`, so a
   status-filtered grid would have shown unfiltered KPI totals. Now the
   clone carries Status.
4. **JO report: Project filter added (service + UI)** — the JO filter
   chain existed as two identical inline copies; extracted to
   `ApplyJoFilters` (summary + rows share it), `ProjectName` is a mapped
   column and now filters DB-side, and the JO view gained the Project
   combo (options were already loaded by the base).
5. **Tax Invoice report: Company filter added to the UI** — the service
   (`ApplyTiFilters`) already honored Company; the view now exposes it.
6. **Glass Spec report: Company filter added to the UI** — service
   already honored it; view exposes it now.

## Audited and verified consistent (no change needed)

- **PI**: summary and rows share `ApplyPiFilters` (since the funnel fix);
  all view-exposed filters apply to both KPIs and grid.
- **Delivery**: most complete module — 7 combos + status + dates; summary
  and rows share `BuildDeliveryRowQuery` predicates.
- **Tax Invoice / Statement**: summary and rows share one filter/builder
  (`ApplyTiFilters` / `BuildStatementLineQuery`); opening-balance logic
  uses the same company/customer scope.
- **Profitability**: summary derives FROM the rows (cannot disagree);
  salesman intentionally not row-filterable (documented in-code).
- **VAT**: summary and rows both derive from `GetAllVatSubmissionRowsAsync`.
- **GlassSpec**: KPIs aggregate the full filtered set (PageSize 5000
  scan), grid pages independently — by design; the 1000-PI ceiling is
  surfaced via `IsCapped`.
- **OrderReport / StatementGenerator / StatementList / Workflow**:
  generator-style modules with their own load machinery — out of scope
  for the base-VM pattern.

## Known limits (documented, deliberate)

- PI quantity scan caps at 1000 newest quotations in scope (same ceiling
  as the GlassSpec engine); typical volumes are far below.
- JO date semantics remain "undelivered = still in scope" (open-ended).
- Salesman on PI/JO/TI/GlassSpec remains service-reserved (delivery-side
  resolution); no UI filter by design until product asks for it.
