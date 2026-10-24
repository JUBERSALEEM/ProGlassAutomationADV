# Phase C9: a submitted inquiry reaches the desk, and a proforma request does not

The desk used to wait for an operator to accept every submission. In production
that step had never been taken: eight complete submissions, none `accepted`, an
empty inbox. The owner's instruction is now explicit — an ordinary inquiry must
reach the shop PC as soon as the customer sends it, with no acceptance step, and
only a **proforma invoice request** should wait for a coordinator.

## The rule, before and after

| | Before | After |
| --- | --- | --- |
| Offered to the desk | `complete` **and** `accepted` **and** not yet delivered | `complete` **and** (`submitted` or `accepted`) **and** not yet delivered |
| A normal inquiry | waits for a human | delivered on the next check |
| A `proforma_invoice` request | waits for a human | finalized in `under_review`; delivered only after a coordinator accepts it |
| An operator hold | only `accepted` was delivered, so `under_review` was already a hold | unchanged: `under_review` is still a hold |
| The files | readable only while `accepted` | readable while `submitted` or `accepted` |

The acceptance step did not disappear — it moved. It is no longer a prerequisite
for every inquiry; it is the deliberate hold an operator uses when they want one,
and it is the mandatory gate for a priced document.

## What the migration touches

`website/supabase-inquiry-intake-phaseC9-auto-handoff.sql`, one transaction:

1. **The type vocabulary.** `proforma_invoice` joins the five existing
   `inquiry_type` values, in the submission constraint, in the project default
   constraint, and in the two functions that validate them
   (`create_customer_inquiry_draft_v2`, `save_customer_inquiry_project`).
2. **Finalize.** `finalize_customer_inquiry_submission` still marks the row
   `complete`, but it sets `under_review` for a `proforma_invoice` request and
   `submitted` for everything else, and it records
   `desktop_handoff: held_for_coordinator|ready` in the customer-visible event.
3. **The delivery gate.** `plan_customer_inquiry_deliveries_v2`,
   `plan_customer_inquiry_delivery_documents` and
   `acknowledge_customer_inquiry_delivery_v2` accept `submitted` or `accepted`.
   The acknowledgement contract itself is untouched: one delivery per
   submission, matching document count, matching assignment snapshot, a routing
   key derived from the database, a 64-hex manifest hash, a silent replay of an
   identical acknowledgement and `23505` for a conflicting one.
4. **The bridge's read rules.** Three policies said `accepted`:
   `inquiry_customer_submissions`, `inquiry_customer_documents` and
   `storage.objects`. All three now say `submitted` or `accepted`.

Point 4 was not cosmetic. The storage policy is evaluated as the caller, so it
inherits the row level security of the two tables it joins. Widening only the
storage policy changed nothing: the bridge saw no submission rows, no document
rows, and an empty object listing. Validation caught it before production did.

Nothing else moves: no role is granted, `inquiry_sources` and its bridge account
are untouched, no customer gains a permission, no row is rewritten, and there is
no backfill.

## Validation

PostgreSQL 17.9, a disposable database, every inquiry migration from
`supabase-inquiries.sql` through `supabase-inquiry-desktop-access-phase14.sql`
replayed first, then this file.

| Probe | Result |
| --- | --- |
| Before C9: a complete `submitted` quotation | **0 rows** planned |
| Before C9: inserting `inquiry_type='proforma_invoice'` | rejected, `23514` |
| Apply C9 to a database holding rows | no row, status or event changed (hash before = hash after) |
| Same data after C9 | **1 row** planned, no acceptance |
| Apply C9 twice | clean, identical |
| Document plan for the deliverable submission | 1 document |
| Storage object visible to the bridge, before the acknowledgement | **1** |
| Storage object visible after the acknowledgement (`downloaded`) | **0** |
| Acknowledgement | status `downloaded`, one delivery row, `downloaded_at` set |
| Identical acknowledgement replay | silent, still one delivery row |
| Conflicting acknowledgement | `23505` |
| Plan after delivery | 0 rows |
| `proforma_invoice` request, before acceptance | `under_review`, not planned, document plan refuses `P0002` |
| Same request after a coordinator accepts it | planned, `accepted` |
| A submission parked back in `under_review` | not planned |

The harness lives outside the repository (`/tmp/c9/run.sh`); the source-level
guards that must keep holding are in `scripts/inquiry-auto-handoff.test.mjs`.

## Applying it

```bash
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f supabase-inquiry-intake-phaseC9-auto-handoff.sql
```

It is idempotent and wraps everything in one transaction. The rollback block at
the bottom of the same file restores the accepted-only gate, the old storage and
table policies, and the five-value type list. Re-typing any existing
`proforma_invoice` row is the only data step a rollback would need, and it is
written out there.

**Not applied to production yet.** Applying it changes what the shop PC
downloads on its next check: the six open submissions would arrive immediately.
The desktop in `ProglassAutomation3.0` already stages, verifies and only then
acknowledges, so that is the right order of operations — but it should be the
owner's decision, on a build that is known to be deployed.

## What the owner will see

* A customer submits a quotation, and it appears on the desk's next check —
  no acceptance, no waiting.
* A customer submits a proforma invoice request, and it appears in the website
  queue as **Under review**. Until a coordinator accepts it there, the desk does
  not see it at all.
* The website's review queue keeps working exactly as before: it can accept,
  reject, close, or park a submission in `under_review` to hold it.
