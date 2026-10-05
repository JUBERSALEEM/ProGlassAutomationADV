# Confirmed PI → production Job Order tracking

## Purpose and boundary

The Job Orders page is now a release-and-progress surface rather than only a
read-only PI list. It continues to admit only active `public.records` rows whose
`status` is exactly `Confirmed` (case-sensitive, with no padding). An operator
opens one PI, reviews its route, then explicitly releases a website production
Job Order.
The original PI rows remain unchanged in Excel Sync, and this website tracker
does not write into the desktop application's local SQLite databases.

Apply `website/supabase-dashboard.sql` first, then
`website/supabase-job-orders.sql` in the Supabase SQL editor. The migration
creates:

- `public.production_job_orders`: one immutable release snapshot per PI and
  source workbook, with a generated `JO-YYYY-NNNNNN` number and lifecycle
  `Released → In Production → Completed`.
- `public.production_job_order_operations`: planned and completed quantity for
  each route stage.
- `public.production_job_order_events`: append-only release/progress audit.
- `release_confirmed_pi_to_production(...)`: authenticated, transactional RPC
  that rechecks active/Confirmed source rows and their IDs at release time,
  snapshots them, validates the stage plan, and is idempotent for a PI/workbook.
- `update_production_job_order_progress(...)`: authenticated RPC that prevents
  negative progress and quantities greater than the stage plan, updates stage
  and job status, and records the change.

The browser never receives a Supabase service-role credential. RLS permits
reads only for authenticated operators; writes happen through the validating
RPCs, not direct table mutation. If only the production-tracking migration is
missing, the confirmed PI queue remains readable and the page displays the
migration requirement while disabling release. If `is_active` or `source_file`
is missing from `public.records`, active/workbook scope cannot be verified and
release stays disabled until the dashboard migration is applied.

## Route and quantity rules

The intended floor order follows the desktop production model:

1. Cutting
2. Holes & Cutouts (only when required)
3. Polish (only when required)
4. Edge Deletion (only when required)
5. Mitring (only when required)
6. Tempering (only when the glass treatment requires it)
7. Pairing (multi-pane builds)
8. Doubling / Lamination (multi-pane builds)

Cutting is mandatory; recognized DGU, LAM and DGU+LAM builds also require
Pairing and Doubling / Lamination, and the release RPC enforces those stages.
Optional process stages remain subject to operator review because the website
feed omits their source charge details.

Quantities distinguish physical panes from finished units. Cutting and
Tempering are counted in panes; Holes & Cutouts in pane pieces; Pairing and
Doubling / Lamination in finished units; Polish, Edge Deletion and Mitring in
linear metres:

| Build family | Cutting | Tempering | Pairing | Doubling / Lamination |
|---|---:|---:|---:|---:|
| SGU | 1 pane per finished unit | 1 pane only when treatment requires it | — | — |
| DGU | 2 panes per finished unit | 2 panes only when either pane requires it | 1 finished unit | 1 finished unit |
| LAM | 2 panes per finished unit | treatment-dependent panes | 1 finished unit | 1 finished unit |
| DGU + LAM | 3 panes per finished unit | treatment-dependent panes | 1 finished unit | 1 finished unit |

Therefore, a 10-unit DGU route starts with 20 cutting panes, then has 10 units
for Pairing and 10 for Doubling. If its panes are tempered, Tempering is also
20 panes. A 10-unit SGU starts with 10 cutting panes, has no assembly stage, and
only includes 10 tempering panes when its treatment requires tempering.

## Important input-data limit

The current website feed in `public.records` exposes PI identity, `type_of_work`,
quantity and area, but not the complete immutable PI specification: individual
pane dimensions, per-pane treatment, or all Step 6 process charges. The page
therefore builds a **summary-based suggested route**, labels the missing detail,
and requires an operator to review/adjust the stage list and quantities before
release. Optional stages can be added and quantities edited. The RPC still
revalidates the Confirmed source lines and snapshots them, but it cannot prove a
missing treatment or charge from fields that were never synced.

Do not treat that preview as a fully automated shop-floor distribution plan.
The authoritative desktop distribution engine uses the full Proforma snapshot,
per-pane treatments, dimensions, and charge lines. For exact web routing, the
next integration should sync that immutable specification snapshot (or an
approved normalized production-spec table) into Supabase, then generate the
route server-side from that source and retain the same operator review step.

## Operational verification

1. Apply the two SQL files in the order above.
2. Confirm `public.records` has active PI rows with `status = 'Confirmed'`, a
   stable `id`, and the correct `source_file`.
3. Confirm the operator's Supabase Auth session is valid and RLS grants that
   authenticated role SELECT on the source and production tables.
4. On `/job-orders`, inspect a confirmed PI, verify its build family and
   pane/unit counts, add missing optional stages, then check the route-review
   confirmation and release it.
5. Record completed quantities stage by stage. A stage cannot exceed its own
   released plan; `Completed` is reached when every planned stage is complete.
6. Verify a duplicate release is idempotent, a PI changed away from Confirmed
   cannot be newly released, and source IDs changing between load and release
   returns a conflict instead of creating a stale job.

## Follow-up hardening

- Replace the summary estimator once the full Proforma snapshot is synced.
- Split aggregate stage totals by specification/component where the floor needs
  pane-level traceability; this first website tracker intentionally reports
  order-level totals.
- Add optimistic concurrency/version checks for simultaneous operators, plus
  hold, pause, rework, breakage, QC and cancellation event types.
- Add upstream-ready quantity gates using component-aware counts. The current
  first release protects planned quantity bounds but is not a replacement for
  the desktop's piece-by-piece production readiness engine.
- Expand fixture coverage for FT/HS/HSO treatments, multi-workbook duplicate PI
  numbers, release retries and concurrent progress updates. The current
  `npm run test:job-orders` suite covers exact active/Confirmed gating, pane and
  finished-unit routing, mixed SGU/DGU counts, unknown builds and fractional
  source quantities.
