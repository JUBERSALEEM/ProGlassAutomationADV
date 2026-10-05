# Deliveries Tracker — senior analysis & design

**Scope.** Add a deliveries tracker to the website that reads the same
Excel-synced dataset as the Reports dashboard, accepts only proformas marked
**Confirmed**, tracks a single PI as **several delivery movements**, presents the
same class of statistics as Reports, and connects the two pages.

**Status.** Delivered. This document is the reasoning behind what was built, the
rules that were chosen, and the things that were deliberately not built.

---

## 1. What exists today

| Layer | Where | What it provides |
| --- | --- | --- |
| Workbook sync | `public.records` (Supabase), written by ProGlass Excel Sync | One row per order line: `pi_number`, `date`, `company`, `customer_ref`, `salesman`, `prepared_by`, `type_of_work`, `qty`, `sqm`, `status`, `pi_status`, `zoho_status`, `is_active`, `source_file` |
| Aggregation | `get_records_dashboard_aggregates` RPC + `src/lib/records-api.ts` fallback | Summary, four breakdowns, filter options, paginated rows |
| Workbook scoping | `src/lib/source-scope.ts` | Which attached Excel file is "current", by sync freshness, with an explicit override |
| Dashboard | `/reports` → `src/components/records/*` | Stats cards, filters, breakdowns, sortable table, pagination, record modal |
| Auth | `src/lib/useOperatorSession.ts` | Supabase session; bearer token forwarded to the API; 401 closes the gate |

What is **missing** is any record of dispatch. The workbook knows a PI was
confirmed and how much it was for. It does not know that 12 of the 40 pieces
went out on Tuesday, that 8 went out on Friday, or that one pane came back
chipped. `status` is a single word that describes the PI's current situation; it
is not a history, and it is not a quantity.

## 2. The question the tracker has to answer

> For every confirmed proforma: how much has actually left the building, how
> much is still owed, in how many loads, and which ones are overdue?

Four properties follow from that sentence, and each one rules out a design:

1. **The unit is the PI, not the row.** `public.records` can hold several rows
   for one PI (several specification groups of one order) and the tracker must
   present one obligation per PI, summed.
2. **A PI has many movements.** Part deliveries are the norm, not the
   exception. A schema that stores `delivered_qty` on the PI row cannot express
   "which loads made up that number", cannot show the balance per note, and
   loses the history the moment it is updated.
3. **The status field cannot be the source of truth.** A PI moves
   `Confirmed → In Production → Ready → Completed` as work proceeds. If the
   tracker only reads rows whose status is currently `Confirmed`, a PI that
   ships completely and is then marked `Completed` disappears from the ledger —
   announcing that the job is fully delivered, forever, by deleting it.
4. **The maths must have one owner.** The product's own position (see
   `posts/why-delivery-balance-is-hard.md`) is that exactly one piece of code
   owns the pending-piece calculation. A tracker that recomputes balance from
   the order on every screen is the double-counting bug with a progress bar.

## 3. The model: obligation + ledger

```
public.records                          public.delivery_entries
(PI obligations, written by Excel Sync) (movements, written by dispatch)
        │                                        │
        │  pi_number                             │  pi_number
        └───────────────┬────────────────────────┘
                        ▼
              one tracker row per PI
        ordered − Σ(counted movements) = balance
```

- **Obligation** — every active `records` row for a PI, summed by
  `pi_number` and `qty`/`sqm`. Descriptive columns (company, salesman, …) come
  from the most recently updated row, so an Excel correction shows through.
- **Movements** — rows in `public.delivery_entries`: one per dispatch, with its
  own note reference, date, quantities, type, status, driver and receiver.
- **Balance** — derived, never stored. `pi_delivery_balance` (SQL) and
  `delivery-ledger.ts` (TypeScript) are the two implementations of one
  specification, and they must change together.

### Why the two tables are separate

`records` is rewritten by a sync that reconciles a workbook; `delivery_entries`
is created by dispatch events. Storing the movements inside the records table
would mean the next sync either overwrites them or has to know about them —
coupling a file import to an operational ledger. Keeping them apart and joining
on `pi_number` is what lets a re-sync repair the obligation without touching the
history.

## 4. The rules, stated once

### 4.1 Eligibility — what appears on the tracker

A PI is tracked when **either**:

- its status is one of the configured confirmed values (`Confirmed` by default,
  widen with `PROGLASS_CONFIRMED_STATUSES="Confirmed,In Production,Ready"`), or
- it already has at least one movement on the ledger.

The second clause is the important one and it is not a loophole: goods that left
the building are a fact, and the page's job is to report the balance of a
delivery, not to police whether a spreadsheet cell still says `Confirmed`. Rows
admitted by the second clause are marked `carried` in the UI and
`tracked_without_confirmed_status` in the API, so the reason they are on screen
is visible rather than mysterious.

*Worked example.* 40 pieces confirmed, all 40 delivered, workbook row later
edited to `Completed`. Without the union rule the PI vanishes and the tracker
reports a clean book. With it, the PI stays, showing 40/40 and a zero balance —
the true answer.

### 4.2 Movement counting — what moves the balance

| Movement | Counts? | Why |
| --- | --- | --- |
| `is_void = true` | no | Soft-close. The row stays for audit; the maths skips it |
| `status` Draft / Dispatching / Preparing | no | Prepared, not dispatched — it has not left the building |
| `status` Dispatched / Delivered / Signed / Received | yes | Left the building |
| `delivery_type` Return | yes, **subtracted** | Goods came back; a return is not a delivery however it was invoiced |
| `delivery_type` Rework / Replacement | yes, **added** | Re-cut panes physically went out |
| anything unrecognized | yes | Unknown data degrades to a normal dispatch, never to a silent zero |

Progress is computed on **quantity** (`delivered_qty / ordered_qty`); area
(`sqm`) is reported alongside because glass is ordered in pieces and invoiced in
m², and either figure can be the one the customer quotes back.

### 4.3 Over-delivery — reported, and rework is not an error

```
excess = delivered_qty − ordered_qty
over_delivered  when  excess > 0  AND  excess > rework_qty
```

A job that ships 42 of 40 pieces is over-delivered. A job that ships 40, then
2 re-cut panes and 1 returned pane has `delivered = 41`, `rework = 2`,
`excess = 1` — **not** flagged: the excess is explained by the replacement, which
is the ordinary consequence of breakage. Only the part of an excess that rework
does not explain is raised, and it is raised as a status (`over_delivered`,
danger tone), never silently clamped, because clamping a number an invoice will
later disagree with is how a dashboard loses its reader.

### 4.4 Lifecycle

| State | Condition | UI |
| --- | --- | --- |
| `awaiting_dispatch` | `delivered ≤ 0` | neutral — confirmed, nothing has moved |
| `partial` | `0 < delivered < ordered` | warning — open balance |
| `complete` | `delivered ≥ ordered`, excess explained | success |
| `over_delivered` | excess not explained by rework | danger |

`complete` and `over_delivered` both count as "delivered" in the summary tiles;
the over count is reported separately so it cannot hide inside a green number.

### 4.5 Staleness

A PI that is `awaiting_dispatch` or `partial` and has had no counted movement
for 14 days is flagged `stale` in the row and counted in the summary. The
threshold is a constant, not a preference; it exists so "nobody has touched this
in two weeks" is a number rather than a feeling.

## 5. Page design — and how it connects to Reports

| Concern | Decision |
| --- | --- |
| Route | `/deliveries`, one page in all fifteen languages, registered in the sitemap |
| Data entry point | `/api/deliveries` — same auth contract as `/api/records`: production requires a bearer token, RLS decides what it may read |
| Workbook scoping | `resolveSourceScope` — **the same function** Reports uses. Extracted from `records-api.ts` so the two pages cannot disagree about which workbook is on screen |
| Session | `useOperatorSession` — **the same hook** both dashboards use. Extracted so one page cannot be protected while the other is open |
| Navigation | `OperationsTabs` — Reports & Records ⇄ Deliveries Tracker, on both headers, with the active tab marked |
| Deep links | Reports → Deliveries carries `company`, `salesman`, `preparedBy`, `typeOfWork`, `piStatus`, `sourceFile` and highlights the filtered slice. A row in Reports opens the tracker at `?pi=…`; a row in the tracker opens Reports at `?search=…`. The PI number is never retyped |
| Terminology | A "lifecycle" (awaiting / partial / complete / over) rather than a second status column; the workbook's own `status` is displayed as `record_status` and labelled as such |
| Statistics | The same six-card band, four breakdown panels, filter bar, sortable paginated table, and detail modal as Reports — same components where they are generic (`RecordsPagination`, `Badge`, `Button`) |
| Bounds | Reads are batched at 1 000 rows up to a 10 000-row ceiling; when the ceiling is hit the API sets `truncated` and the UI says the figures are a floor, not a total |

### Statistics definitions

| Card | Formula |
| --- | --- |
| Confirmed PIs | `COUNT(DISTINCT pi_number)` over the filtered obligation set |
| Ordered | `SUM(ordered_qty)` / `SUM(ordered_sqm)` |
| Delivered | `SUM(delivered_qty)` — counted movements only, returns subtracted |
| Balance | `MAX(0, ordered − delivered)` per PI, summed |
| Progress | `SUM(delivered_sqm) / SUM(ordered_sqm)` |
| Movements | counted entries; average per PI alongside |
| Awaiting / Partial / Delivered / Stale | counts by lifecycle, and by 14-day rule |

Breakdown panels group by **company**, **salesman**, **lifecycle** and **type of
work**, and the bars are scaled by **outstanding m²**, not row count: the useful
question is who is owed glass, not who has the most rows. Each panel is
clickable and feeds the filters.

## 6. What was built

| File | Role |
| --- | --- |
| `src/types/deliveries.ts` | Obligation, movement, row, lifecycle, filters, sort, summary, breakdowns |
| `src/lib/delivery-ledger.ts` | **The one owner of the maths** — movement classification, lifecycle, obligation grouping, row assembly, filtering, sorting, summaries, breakdowns |
| `src/lib/deliveries-api.ts` | Supabase reads (batched, schema-tolerant), ledger discovery, assembly, preview dataset |
| `src/lib/source-scope.ts` | Workbook resolution, shared with Reports |
| `src/lib/useOperatorSession.ts` | Session + token + sign-out, shared with Reports |
| `src/lib/delivery-links.ts` | Deep-link builders between the two dashboards |
| `src/app/api/deliveries/route.ts` | Range/permission checks, filters, sort whitelist, auth |
| `src/app/[locale]/deliveries/page.tsx` | Route, metadata, structured data, skeleton |
| `src/components/deliveries/*` | Header, summary cards, filters, breakdowns, table with expandable ledger, PI modal, status atoms |
| `src/components/operations/OperationsTabs.tsx` | The tab that connects the two pages |
| `supabase-deliveries.sql` | Ledger table, balance view, indexes, opt-in strict guard, scaling RPC |

## 7. Edge cases and how each is handled

| Case | Handling |
| --- | --- |
| PI with several `records` rows | Summed into one obligation; `record_row_count` shown in the modal |
| Movement on a PI with no confirmed row | Tracked and marked `carried`; if no records row exists at all, ordered = 0 and the row says so |
| Movement on a PI absent from `records` entirely | Row is built from the ledger alone; `tracked_without_confirmed_status` + a note in the ledger panel |
| Duplicate note reference | Partial unique index on `delivery_note_ref` where present — a replayed sync fails loudly instead of double-counting |
| Voided movement | Displayed, greyed, excluded from every figure |
| Draft / Dispatching movement | Same treatment: visible, counted as zero |
| Return | Subtracted and labelled with a minus sign and danger tone |
| Rework / Replacement | Added; exempts a matching excess from the over-delivery flag |
| Over-delivery | `over_delivered` status + danger tone + a separate summary count; never clamped |
| Deleted Excel row (`is_active = false`) | Excluded from the obligation, as on Reports |
| Workbook switched | Both obligations and movements are scoped by the same resolver; changing the workbook resets filters and page, as on Reports |
| Ledger table absent | Tracker still works: every confirmed PI shows as awaiting dispatch, with a warning naming `supabase-deliveries.sql` |
| Ledger present but differently named/shaped | Candidate tables (`delivery_entries`, `deliveries`, `delivery_notes`, `delivery_movements`) and per-field alias matching (snake/camel case) |
| Column missing on `records` | `42703` triggers a core-column retry instead of a failed page, matching Reports' resilience |
| Very large dataset | Batched reads; `truncated: true` reported in API, banner and header, rather than a quiet partial total |
| Dates arriving in any Excel format | Parsed through the shared date layer; `movementEpoch` handles DD-MM-YYYY before the JS `Date` parser misreads it as MM-DD |
| Session revoked mid-view | API returns 401 → `requireAuth()` shows the sign-in panel instead of retrying forever |

## 8. Alignment with the desktop application's delivery vocabulary

The ledger deliberately reuses the product's existing words so the same data
means the same thing in both places: a delivery note reference (`DN-…`), a
sequence within the PI, statuses `Dispatching → Dispatched → Signed →
Received`, and types `Original | Rework | Return | Dummy`. The desktop model
also carries a per-item `PreviouslyDeliveredQty` / `DeliveredQty` /
`BalanceQty` triplet and a "Master DN" reference tracker per job; this ledger is
the web-side equivalent of that reference tracker, with the difference that it
derives the balance instead of storing it — the stored-balance-can-go-stale
problem simply does not exist when the number is a query.

A future sync from the desktop delivery notes into `public.delivery_entries`
would need a `pi_number` mapping (the desktop keys on job order and proforma)
and a rule for the Master/Dummy rows: **exclude them from the ledger** — they
are trackers, not dispatches, and importing them would double every quantity.

## 9. Rollout

1. **Today — read-only.** The tracker works against `public.records` alone:
   every confirmed PI appears as awaiting dispatch. Nothing to install.
2. **Ledger.** Run `website/supabase-deliveries.sql`. Movements become real;
   part deliveries, balances, returns and over-deliveries appear immediately.
3. **Capture at the source.** Write movements where dispatch actually happens —
   the desktop delivery-note flow — and sync them into `delivery_entries`.
   Manual entry on the web is deliberately not built: the website is a reader of
   operational truth, and an edit form would create a second one.
4. **Scale.** Past the 10 000-row read ceiling, point the API at
   `get_deliveries_dashboard_aggregates` (shipped, not yet called) so the
   aggregation happens in PostgreSQL.

## 10. Data-quality checks worth running once a month

```sql
-- 1. Movements on PIs whose ordered quantity is zero (broken PI numbers).
SELECT pi_number, SUM(delivered_qty)
FROM public.delivery_entries WHERE is_void = false
GROUP BY pi_number HAVING SUM(delivered_qty) <> 0 AND pi_number NOT IN (
  SELECT pi_number FROM public.records WHERE is_active IS NOT FALSE
);

-- 2. PIs moving long after their order date (stale balance).
SELECT pi_number, ordered_qty, delivered_qty, balance_qty, last_delivery_date
FROM public.pi_delivery_balance
WHERE delivery_status IN ('awaiting_dispatch','partial')
  AND (last_delivery_date IS NULL OR last_delivery_date < CURRENT_DATE - 14)
ORDER BY balance_sqm DESC;

-- 3. Over-deliveries the import could not see.
SELECT pi_number, ordered_qty, delivered_qty, rework_qty
FROM public.pi_delivery_balance WHERE delivery_status = 'over_delivered';
```

## 11. Decisions taken, and what they cost

| Decision | Cost accepted |
| --- | --- |
| Balance derived, never stored | Every read does the arithmetic; correct at the price of a little CPU |
| Ledger as a separate table | A join on `pi_number`; the two writers never contend |
| Eligibility = confirmed ∪ has-movements | A PI can appear with `carried`; the alternative loses history |
| Returns subtracted rather than ignored | A returned pane keeps counting as a movement (for the count) but not as delivery (for the balance) |
| Over-delivery reported, not refused (in SQL) | An anomaly can exist in the table; it is impossible to miss on the page |
| No writes from the website | No capture on the web; dispatch remains the desktop's job |
| Bounded reads with a reported ceiling | Past 10 000 rows the figures are a floor until phase 4 |
| `delivery_entries` tolerant of foreign column names | Slightly more mapping code; the page works against a ledger an existing integration already maintains |

## 12. Open questions for the operator

1. **Which statuses mean "confirmed"?** The default is exactly `Confirmed`
   (`PROGLASS_CONFIRMED_STATUSES` widens it). If a PI is a commitment from `In
   Production` onwards, say so and the entry condition moves without a code
   change.
2. **Who writes movements first — web import or desktop sync?** The tracker is
   ready for either; the ledger's `source_file` column and note-reference
   uniqueness exist for that choice.
3. **Is 14 days the right staleness threshold** for this business's vehicle and
   site cadence?
4. **Should returns reduce the delivered count or raise a separate credit
   figure?** They currently subtract, and are reported separately as well.
