# Phase 15: Inquiry inbox redesign and proforma entry point

Implementation record for the work described in
`PHASE15_INBOX_PROFORMA_AUDIT.md` and `PHASE15_PROFORMA_CONTRACT.md`.
Branch `arena/2611f8ff-proglassautomationadv`, applied 2026-10-24.

## 1. What the inbox does now

- `InquiriesDashboard.tsx` is a composer: session, one request, URL state and
  the surrounding states. The markup lives in four files: `InboxHeader`,
  `InboxSummary`, `InboxFilters` and `InboxCards` with `InquiryCard`.
- One card component serves every width. The grid is one column on a phone, two
  from 640 px and three from 1280 px. There is no separate mobile list and no
  table to keep in step.
- Card fields, in order: inquiry number and reference, status and priority,
  customer, project, documents, proforma reference, job order, inquiry value,
  source-missing notice, updated time, then the two actions.
- Header: title, the read-only sentence, the active filter count, refresh,
  website submissions and the signed-in operator. `OperationsTabs` stays.
- Summary: four values, each returned by the API, labelled as the whole
  publication rather than the filtered page.
- Filters: search, status, priority, has documents, reset, active count. Every
  change is a query string change and a new server request. The server still
  filters, orders and counts; nothing is filtered in the browser.
- Proforma action per card: `Create proforma` when there is neither a website
  draft nor a desktop reference, `Resume proforma` with the draft revision when
  a website draft exists, `Open proforma` with the desktop number when the
  desktop published one. The button always goes to the workspace, which decides
  what it can honestly show.
- The card shows no estimator, owner or due date, because `InquiryListRow` has
  none of them. Assignment belongs to the website submission register.
- `InquiryDetail` gained the same proforma entry point and lost its dash
  placeholders in favour of words.

## 2. The proforma workspace

Route: `/inquiries/[id]/proforma`. API: `GET` and `POST
/api/inquiries/[id]/proforma`.

Left column, Step 5: numbered specification groups, each with its own rows and
its own totals. Columns are Glass reference, W1, H1, W2, H2, Qty, SQM, Total
SQM, Approved rate and Value, plus the row number and a remove control. Rows
carry a stable key, so renumbering a group does not move a value into another
row. Groups are independent: adding to group 2 never re-prices group 1.

Right column, Step 4: the build family, its own option lists from
`pricingRules`, the generated glass reference, outer and inner dimensions,
quantity and approved rate, then `Add line to specification N`. Below it, the
specifications the last save recorded, with `Reuse as new` and an explicit
two step `Replace specification N` that states what it discards before it does
it. A builder target select makes the assignment visible.

Absent by construction: build costs, sheet purchase rates, margins, profit and
loss, and optimiser controls. `src/lib/proforma-draft.ts` imports only
`billablePanelSqm`, `isFloored`, `moneyRate` and `physicalPanelSqm`, and a
static contract test refuses the internal symbols in the workspace tree.

Both columns stack below 1024 px, Step 5 first.

## 3. Persistence and security

- Migration `inquiry_proforma_phase15_tables`: `inquiry_proformas`,
  `inquiry_proforma_lines`, `inquiry_proforma_saves`, RLS on, anon revoked,
  `authenticated` granted `SELECT` only, read policies on
  `has_inquiry_role(ARRAY['inquiry_admin','inquiry_auditor'])`, a unique open
  draft per inquiry and a unique `(source_system, source_proforma_number)`.
- Migration `inquiry_proforma_phase15_write`:
  `save_inquiry_proforma_v1`, `SECURITY DEFINER`, `search_path` pinned, execute
  revoked from `PUBLIC` and `anon` and granted to `authenticated`, role checked
  inside, money recomputed inside, revision and idempotency enforced inside.
- The API layer verifies the token with `auth.getUser`, reads the enabled role
  from `inquiry_operator_roles`, requires `inquiry_admin` to write and
  `inquiry_admin` or `inquiry_auditor` to read, and maps `40001` to HTTP 409.
- `VIEW_ROLES` in the inbox data layer now matches the Phase 13 matrix:
  `inquiry_auditor` and `inquiry_admin`. `inquiry_viewer` was removed from the
  API list. No live account held it.
- No new role, no anon grant, no service role in any client, no public bucket,
  no signed URL.

## 4. Desktop integration status

The website does not claim synchronisation. Nothing in the desktop reads these
tables, and no desktop proforma is published into them. `inquiries.
proforma_number` remains a reference. The workspace says this on screen when a
desktop number exists, and `PHASE15_PROFORMA_CONTRACT.md` lists the four missing
desktop dependencies and the rules a future consumer must follow.

## 5. Verification

| Check | Result |
| --- | --- |
| `node --test scripts/*.test.mjs` | 607 tests, 607 pass |
| `npx vitest run` | 21 files, 238 tests, 238 pass |
| `npm run lint` | clean |
| `npm run typecheck` | clean |
| `npm run build` | compiled, type check passed, route table includes `/inquiries/[id]/proforma` |
| `GET /inquiries` on the dev server | 200 |
| `GET /inquiries/<uuid>/proforma` | 200 |
| `GET /api/inquiries/<uuid>/proforma` without a token | 401 |
| `POST` without an Idempotency-Key | 400 |
| Migrations | applied, function is `SECURITY DEFINER` with `search_path=public, pg_temp` |
| Rolled-back admin probe | number allocated, freight recomputed, retry idempotent, stale revision `40001`, zero quantity `22023` |
| Estimator token on the write function | `42501` |
| Anon read | `permission denied for table inquiry_proformas` |

## 6. Known limits

- The live `public.inquiries` table holds no rows, so the production inbox shows
  its empty state until the Windows bridge publishes a scan. The workspace was
  therefore verified by unit and render tests plus a rolled-back database probe,
  not against a published inquiry.
- No browser is available in this environment, so the desktop, tablet and mobile
  inspection was done by rendering the components to markup and asserting the
  three column tiers, not by capturing screenshots. A pixel review of the live
  deployment remains for the owner.
