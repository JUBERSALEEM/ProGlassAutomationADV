# Why the desktop inquiry inbox shows 0 while the website holds 8 submissions

Read-only diagnosis against production `afpbdyamedpanwijxghk`, 2026-10-24
13:1x UTC. No row, role, policy, bucket rule or bridge setting was changed.
Nothing was accepted, acknowledged or downloaded to produce this report.

## The short answer

The desktop inbox is not a mirror of the website submission list. It is a
delivery queue that only ever hands over submissions an operator has **accepted**.
Eight submissions exist and all eight are `upload_state = 'complete'`, but **not
one of them is in `status = 'accepted'` right now**, and the only two that were
ever accepted already have a delivery row. Both conditions empty the queue.

Verified inside a transaction that was rolled back, signed in as the account the
deliveries themselves record (`up17mix@gmail.com`, the enabled `inquiry_bridge`
role):

| probe | result |
| --- | --- |
| `plan_customer_inquiry_deliveries_v2(25)` | **0 rows, no error** |
| enabled roles for that user | `{inquiry_bridge}` |
| capability `inbox.plan.bridge` | held |

Zero rows with no exception means the role and capability gates passed. Phase 14
is live in production: the deployed
`plan_customer_inquiry_deliveries_v2`, `plan_customer_inquiry_delivery_documents`
and `acknowledge_customer_inquiry_delivery_v2` all admit
`inquiry_bridge OR inquiry_admin`. **This is not an access problem, and widening
access would not change it.**

## The exact gate in the database

```sql
-- plan_customer_inquiry_deliveries_v2
WHERE s.upload_state = 'complete'
  AND s.status       = 'accepted'
  AND x.submission_id IS NULL          -- no delivery row yet
```

```sql
-- plan_customer_inquiry_delivery_documents(p_submission_id)
IF NOT EXISTS (SELECT 1 FROM inquiry_customer_submissions s
               WHERE s.id = p_submission_id
                 AND s.upload_state = 'complete'
                 AND s.status = 'accepted') THEN
  RAISE EXCEPTION 'Accepted submission not found.' USING ERRCODE = 'P0002';
END IF;
```

## Where each of the eight submissions stands

| Reference | status | complete | docs | delivery row | why the inbox ignores it |
| --- | --- | --- | --- | --- | --- |
| WEB-2026-000001 | submitted | yes | 1 | none | never reviewed → not `accepted` |
| WEB-2026-000002 | submitted | yes | 1 | none | never reviewed → not `accepted` |
| WEB-2026-000003 | downloaded | yes | 13 | 2026-10-17 11:02 | already delivered |
| WEB-2026-000004 | submitted | yes | 3 | none | never reviewed → not `accepted` |
| WEB-2026-000005 | submitted | yes | 1 | none | never reviewed → not `accepted` |
| WEB-2026-000006 | under_review | yes | 1 | none | reviewed but not accepted |
| WEB-2026-000007 | downloaded | yes | 3 | 2026-10-24 08:39 | already delivered |
| WEB-2026-000008 | submitted | yes | 1 | none | arrived 24 Oct, never reviewed |

`accepted_at` is null on all six open submissions, and the event log shows the
review RPC has not been run on any of them. Six submissions and eight documents
are sitting one decision away from the queue.

## The second problem: `WEB-2026-000007` is "Failed" with no files

This one is not a website query problem, and pressing *Check now* cannot fix it.

The server already considers `WEB-2026-000007` delivered:

* delivery row written 2026-10-24 08:39:28 UTC, `document_count = 3`,
  `routing_key = assigned/EST-PRIMARY-TEST`, manifest present;
* `bridge_user_id = 0568fecb-…` → `up17mix@gmail.com` (the enabled
  `inquiry_bridge` account, **not** the `techwithyassh@gmail.com` admin login);
* the same call flipped the submission to `downloaded` and stamped
  `downloaded_at = 2026-10-24 08:39:28`;
* the three objects (`7702-r1- zener.pdf`, `7706- NH GLASS.pdf`,
  `7715- oya aluminium.pdf`) are present, `availability = 'available'`, with
  byte sizes and SHA-256 recorded.

Once that acknowledgement lands, no inbox call can offer those files again:

1. `plan_customer_inquiry_deliveries_v2` excludes the submission because the
   delivery row exists (`x.submission_id IS NULL`), and it would exclude it
   anyway because the status is no longer `accepted`.
2. `plan_customer_inquiry_delivery_documents` raises `P0002` for a `downloaded`
   submission, so the file list cannot even be requested.
3. `inquiry_customer_deliveries` has RLS enabled and **no policies at all**, so
   the desktop cannot read the delivery row to tell "already delivered" from
   "nothing was ever accepted". It can only see an empty plan.
4. The storage policy `Inquiry bridge reads accepted intake objects` requires
   `status = 'accepted'`, so the bridge credential can no longer read those three
   objects either. An `inquiry_admin`/`inquiry_auditor` session still can,
   because `Customers and operators download inquiry intake documents` only
   requires `upload_state = 'complete'`.

So the local ledger row for `WEB-2026-000007` is stuck: the acknowledgement was
written, the register import did not survive, and there is no server-side path
back. Two things follow:

* the three PDFs must be recovered from the website operator register
  (`/inquiries/customer-submissions` → `WEB-2026-000007` → each document's
  Download, which runs `authorize_customer_inquiry_document_download` under the
  admin's own session), or the desktop's saved copy if it still exists under
  `C:\ProgramData\ProGlass\CustomerInquiryInbox`;
* do **not** delete the delivery row to force a retry. That erases the audit
  trail of a real delivery and would let a second machine acknowledge the same
  submission with a different manifest.

## What actually unblocks the other six

Nothing in the database, the schema or the bridge has to change. An
`inquiry_admin` accepts each submission on the website:

`/inquiries/customer-submissions` → open the row → **Accepted** → Save

That calls `review_customer_inquiry_submission(p_status := 'accepted')`, which
sets `accepted_at`, writes a customer-visible `status_changed` event, and makes
the submission immediately visible to `plan_customer_inquiry_deliveries_v2`.
Expected result on the next *Check now*: 6 queued submissions, 8 documents.
Routing keys will be `unassigned` for `000001`, `000002`, `000004`, `000005` and
`000008` (no active estimator assignment) and `assigned/…` for `000006`, which
has one.

`WEB-2026-000006` is already `under_review`, so saving Accepted from the review
screen moves it on; `000001`, `000002`, `000004`, `000005` and `000008` move from
`submitted` to `under_review` first if the screen is saved without changing the
selection, which is the desktop contract's normal two-step review.

## What must not be done

* Do not add `submitted` to the plan predicate to make rows appear. The
  acceptance step is the operator's decision and it is customer-visible
  (`status_changed`, `inbox_delivered`); bypassing it would deliver unreviewed
  customer files to the shop floor and tell the customer nothing.
* Do not grant `inquiry_admin` to the bridge account or the reverse. Both already
  hold `inbox.plan.bridge`.
* Do not touch `inquiry_sources`, its `enabled` flag or its
  `bridge_user_id` (`622def69…`, disabled) — nothing in the delivery loop reads
  it.
* Do not rewrite submission statuses with SQL. Use the review RPC so the event
  log stays truthful.

## Desktop-side follow-ups (repository `JUBERSALEEM/ProglassAutomation3.0`)

These cannot be fixed from this repository and were not attempted:

1. **Acknowledge only after the files are verified on disk.** The reference
   implementation in `website/scripts/inquiry-inbox-bridge.mjs` downloads to a
   staging folder, checks byte size and SHA-256, writes `manifest.json` and
   `READY`, renames the folder into place, and only then calls
   `acknowledge_customer_inquiry_delivery_v2`. The shop PC wrote the
   acknowledgement and ended up with no files, so its ordering differs.
2. **Do not roll back the downloaded files when the register import fails.**
   Keep the staged folder and retry the import, otherwise the delivery is
   unrecoverable (see above).
3. **Report "already delivered", not "Failed".** A submission that has vanished
   from the plan while a local ledger entry exists is the normal post-ack state.
   Distinguishing it properly needs a server signal the desktop can read —
   `inquiry_customer_deliveries` has no policies — so that is a future website
   change (a `plan_…_v3` that also reports recently delivered references), not
   something to patch in SQL today.

## Reproduce this diagnosis (read-only)

```sql
-- the queue predicate, per submission
select s.submission_reference, s.status, s.upload_state,
       (select count(*) from public.inquiry_customer_documents d
         where d.submission_id = s.id and d.availability = 'available') as docs,
       exists (select 1 from public.inquiry_customer_deliveries x
                where x.submission_id = s.id) as delivered
  from public.inquiry_customer_submissions s
 order by s.submission_reference;

-- what the desk's own credential is offered (transaction, rolled back)
begin;
  set local role authenticated;
  set local request.jwt.claims =
    '{"sub":"0568fecb-0f8e-4f1d-9d77-845a5be0cbbc","role":"authenticated"}';
  select count(*) from public.plan_customer_inquiry_deliveries_v2(25); -- 0
rollback;
```
