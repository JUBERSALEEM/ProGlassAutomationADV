# Phase 15, Phase A: Inquiry Inbox redesign and Proforma Invoice entry point

Source audit of the website repository before any visual or structural change.
Everything below was read on branch `arena/2611f8ff-proglassautomationadv` at
commit `dc9f032`, working tree clean.

## 1. The three responsibilities as they exist today

| Responsibility | Route(s) | Server layer | Authorization |
| --- | --- | --- | --- |
| A. Main Inquiry Inbox (desktop publication) | `/inquiries`, `/inquiries/[id]` | `src/lib/inquiries-api.ts`, `/api/inquiries`, `/api/inquiries/[id]`, `/api/inquiries/[id]/documents/[documentId]` | Bearer token, `auth.getUser`, `inquiry_operator_roles` enabled row, `VIEW_ROLES` |
| B. Website customer submissions | `/inquiries/customer-submissions`, `/inquiries/customer-submissions/[id]`, `/inquiries/submit`, `/inquiries/submissions/[id]` | `src/lib/inquiry-submissions-api.ts`, `/api/inquiry-submissions` | Same operator gate for the register, public intake for submit |
| C. Estimator workspace | `/inquiries/estimator`, `/inquiries/estimator/[id]`, `/inquiries/chat` | `src/lib/inquiry-estimators-api.ts`, `/api/inquiry-estimators/*` | Bearer token, `inquiry_operator_roles` enabled row, `ROLES = inquiry_admin, inquiry_auditor, inquiry_estimator` |

A and B are separate tables and separate reference spaces. The inbox reads
`public.inquiries`, whose `inquiry_number` matches `^INQ-[0-9]{4}-[0-9]{4,}$`.
Website submissions are `public.inquiry_customer_submissions`, referenced as
`WEB-2026-000001` and so on. There is no join between them and no column that
carries one into the other. Any inbox card that shows both numbers would be
joining two unrelated identifiers, so the redesign keeps the submission
register behind its own entry point.

C already lives inside the `/inquiries` route tree, which is why `PageOverlay`
treats every route starting with `inquiries/` as the inquiry workspace and pins
the `.inquiry-route` visual grammar. The redesign must not move estimator
routes, must not move assignment, analytics or admin controls into the inbox,
and must keep the estimator authorization list untouched.

## 2. The inbox data path, exactly as it runs

1. `InquiriesDashboard` reads its initial filters from the URL (`search`,
   `status`, `priority`, `hasDocuments`, `page`), then fetches
   `/api/inquiries?page&pageSize&sortBy&sortOrder` with a Bearer token and
   `cache: "no-store"`, aborting the previous request.
2. `/api/inquiries/route.ts` is `force-dynamic`, `no-store`, and maps failures
   to 401, 403, 404, 503.
3. `authorizedClient` in `src/lib/inquiries-api.ts` requires a token, calls
   `client.auth.getUser(token)`, then reads `inquiry_operator_roles` for that
   user and requires `enabled === true` and a role inside
   `VIEW_ROLES = inquiry_viewer, inquiry_auditor, inquiry_admin`.
4. `fetchInquiries` queries `public.inquiries` with `count: "exact"` and
   `range()`, so filtering and pagination are already server side. It excludes
   `availability = tombstoned`, searches with `.or()` across
   `inquiry_number`, `customer_name`, `project_name`, `proforma_number`,
   `job_order_number`, and inner-joins `inquiry_documents` when
   `hasDocuments` is set.
5. Summary counts (total, active, confirmed, withDocuments) are four separate
   `head: true` counts. They are global, not scoped to the active filters.
   The redesign may only display these four values as returned.
6. `lastSuccessfulSyncAt` comes from `inquiry_sources.last_success_at`, a
   single value for the publication channel.

Client defaults: page size 50, `pageSize` clamped to 10 to 100, default sort
`source_updated_at desc`. Statuses are `Received, Prepared, Sent, Confirmed,
Hold, Cancelled`. Priorities are `Low, Normal, High, Urgent`.

## 3. Fields the inbox can honestly show, and the ones it cannot

`InquiryListRow` carries exactly 17 fields: `id`, `inquiry_number`,
`inquiry_reference`, `customer_name`, `project_name`, `salesman_name`,
`prepared_by_name`, `status`, `priority`, `inquiry_value`, `received_at`,
`source_updated_at`, `proforma_number`, `job_order_number`, `availability`,
`attachment_count`, `published_at`. `customer_location`, `project_description`
and the timestamp columns belong to `InquiryDetail` only.

There is no estimator, no assigned owner, no due date, no total documents byte
size and no PI state on the list row.

Assignment exists only for website submissions, in
`inquiry_estimator_assignments`, keyed by `submission_id` and surfaced through
`/api/inquiry-estimators/queue` as `submission_reference`. Using it to label a
desktop inquiry card would be a fabricated relationship. The card therefore
shows no estimator, and the report records this as a missing data contract
rather than inventing a field.

## 4. The Proforma Invoice gap

There is no PI table in this repository.

- `public.inquiries.proforma_number` is `text NOT NULL DEFAULT ''`. It is a
  reference copied from the desktop publication, not a PI record, and it
  carries no status, no lines, no value and no revision.
- `public.records.pi_number` belongs to the `/reports` demo workbook canvas,
  pinned by `supabase-deliveries.sql`. `/api/job-orders` and
  `/api/deliveries` read `public.records`; `production_job_orders` and
  `production_job_order_operations` are the production reads.
- No table anywhere in `website/supabase-*.sql` stores inquiry line items. Not
  a single one of `Glass Ref, W1, H1, W2, H2, Qty, SQM, Total SQM, Rate,
  Value` exists as a stored column on the website side.

Consequences for the brief's PI workspace:

- Step 5 cannot be seeded from an inquiry. The only specification rows in the
  repository are the seeded demonstration quotation in
  `src/data/pricingRules.ts` and `src/data/workedExample.ts`, rendered by
  `SpecificationSurface.tsx`.
- "Resume an existing draft" has nothing to resume today.
- The arithmetic is available and is customer safe: `billablePanelSqm` (floor
  0.5 m²), `physicalPanelSqm`, `isFloored`, `surchargeApplies` (>= 4 m²),
  `rowRate`, `moneyRate`, `STANDARD_SURCHARGE_PERCENT` (20). The same file also
  holds `buildRateAdders`, `outerSheetRates` and `step4RatePerSqm`, which are
  internal costs and selling costs. A PI working surface must import only the
  safe half, and `ProfitLossSurface.tsx` must not be reachable from it.

## 5. Role and access inconsistencies found

- `VIEW_ROLES` still includes `inquiry_viewer`, while the Phase 13 role matrix
  grants inbox reads to `inquiry_admin` and `inquiry_auditor` only. No live
  account holds `inquiry_viewer`. This is a live inconsistency between the API
  list and the database matrix.
- The estimator layer uses its own list, which is correct for its own routes.

## 6. Visual grammar already in place

- Tokens: `--ink-950/900/850/800/700`, `--line`, `--line-soft`,
  `--line-strong`, `--brand-50..700`, `--text-1..4`,
  `--state-success/warning/danger/info`, with a light reinterpretation block
  around line 455 of `src/app/globals.css`.
- Utilities in use: `shell`, `eyebrow`, `dense-card-grid`, `shadow-panel`,
  `card-pad`, `rounded-panel`, `text-3xs`, `text-2xs`, `rounded-2xl`,
  `border-line-strong/60`, `bg-ink-900/80`.
- `.inquiry-route` (globals.css, around line 1399) adds a faint brand radial
  and forces editable inputs to `ink-950` at 0.72 with `line-strong` borders.
  New PI inputs inherit it.
- `Badge` tones are `neutral, accent, success, warning, danger`, plus
  `IndexChip` and `IconChip`. `EmptyState`, `Button`, `Field`, `Feedback`,
  `LocaleLink` are available.
- `OperationsTabs` is the document chain strip; its Inquiry tab points at
  `/inquiries` and its Proforma tab at `/reports?status=Confirmed`.

The inbox currently carries no i18n keys: every sentence is a literal English
string, so the route overlay subset is unaffected by this work.

## 7. Files that must change

| File | Change |
| --- | --- |
| `src/components/inquiries/InquiriesDashboard.tsx` | reduce to composition: session, data fetch, URL state |
| `src/components/inquiries/InboxHeader.tsx` | new, compact header with active filter count and operator identity |
| `src/components/inquiries/InboxSummary.tsx` | new, four API-driven indicators |
| `src/components/inquiries/InboxFilters.tsx` | new, search and selects, URL backed |
| `src/components/inquiries/InquiryCard.tsx` | new, one responsive card used at every breakpoint |
| `src/components/inquiries/InboxCards.tsx` | new, grid, skeleton and empty states |
| `src/lib/inquiry-inbox-view.ts` | new, pure helpers: active filter count, query string, card field order |
| `src/components/inquiries/ProformaWorkspace.tsx` | new, two column PI surface |
| `src/lib/proforma-draft.ts` | new, pure draft arithmetic over the customer-safe rules |
| `src/app/[locale]/inquiries/[id]/proforma/page.tsx` | new, server page beside the inquiry detail |
| PI data layer and API (pending decision, section 9) | new |
| `src/components/inquiries/InquiryDetail.tsx` | add the PI entry point for the detail route |
| `tests/inquiry-inbox-view.test.ts` and `tests/proforma-draft.test.ts` | new unit tests |
| `scripts/inquiry-inbox-contract.test.mjs` | new static contract on the API and route surface |

## 8. Behaviour that must not regress

1. `VIEW_ROLES` stays enforced inside `src/lib/inquiries-api.ts`, never in React.
2. `authToken` is still the only identity; no client supplied role is trusted.
3. The download route keeps SHA-256 and byte size verification, `attachment`,
   `nosniff`, sandbox CSP and `private, no-store`.
4. 401 calls `requireAuth()`, `FORBIDDEN` sends the visitor to the access gate
   or the submission page exactly as today.
5. URL backed filters survive reload and back navigation, and page resets to 1
   on any filter change.
6. Skeleton on first load, empty state with reset, error state with retry and
   the `NOT_CONFIGURED` explanation remain.
7. Customer submission routes, statuses, and the estimator routes, analytics,
   assignment and admin actions are untouched.
8. No internal rate, cost, margin or P&L value reaches any inbox or PI payload.
9. No new live role, no anon access, no `service_role` in client code, storage
   bucket stays private.
10. Existing static contracts still pass, including
    `scripts/inquiry-api-security.test.mjs`, which pins `VIEW_ROLES.includes`.

## 9. Open decision: where a PI is stored

The brief asks for a versioned contract with idempotency, revision handling and
source system identity, and for the entry point to resume a draft or say
plainly that none exists. The repository can support either shape, and the
choice determines whether Phase 15 includes a migration.

- Option A, website-side PI store. Two tables, `inquiry_proformas` and
  `inquiry_proforma_lines`, plus RLS, a `SECURITY DEFINER` write function with
  a unique idempotency key, a `revision` counter and `source_system`
  (`website` or `desktop`), with a unique `(source_system,
  source_proforma_number)`. The workspace becomes durable, resume is real, and
  the desktop handoff is documented as not yet consuming the contract.
- Option B, no persistence. The workspace computes correctly in the session and
  says that saving is unavailable. Honest, but the brief's "resume" and
  "saved-specification list" cannot be delivered.
- Option C, route the entry point at the existing `/reports` ledger. No new
  schema, but that ledger is the demonstration workbook, not an operator PI, so
  the workspace would be fake and is excluded.

Decided on 2026-10-24: Option A. A website-side store with a versioned
contract, applied as `inquiry_proforma_phase15_tables` and
`inquiry_proforma_phase15_write`, and documented for a later desktop consumer
in `PHASE15_PROFORMA_CONTRACT.md`.

The same decision round aligned `VIEW_ROLES` in `src/lib/inquiries-api.ts` with
the Phase 13 matrix: `inquiry_auditor` and `inquiry_admin` only. No live account
held `inquiry_viewer`, so this removes an access path the database already
denied.

## 10. Phases as planned

- Phase B, inbox: decompose the dashboard into the files in section 7, one card
  layout from 320 px upward, active filter count, URL state, server side
  filtering preserved, no invented fields, no em dash placeholders.
- Phase C, PI contract and data layer: migration only if Option A is chosen,
  authorization inside the function and inside the API route, idempotency and
  revision on write, and a documented desktop dependency.
- Phase D, PI workspace: Step 5 groups on the left with stable row identities
  and independent groups, Step 4 builder and saved specifications on the right,
  explicit assign and reuse, destructive replace warning, stacked below the
  large breakpoint, no cost, margin, P&L or optimisation control.
- Phase E, verification: unit tests for the pure helpers, a static contract for
  the API surface, `node --test scripts/*.test.mjs`, `npx vitest run`,
  `npm run lint`, `npm run typecheck`, `npm run build`, and a desktop, tablet
  and mobile inspection before the nine item report.
