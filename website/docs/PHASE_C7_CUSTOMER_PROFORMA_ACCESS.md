# Phase C7: customer access to their own proforma invoice

Customers can now open the proforma invoice that was shared with their account,
and nobody else's. Nothing was applied to production and nothing was deployed by
this phase.

Validated on 2026-10-24 against the disposable PostgreSQL 17.9 rebuild, with the
migration applied twice and 24 probes run.

---

## 1. The model

A proforma belongs to one inquiry, an inquiry is published from one customer
submission, and a submission belongs to one customer account. So a document has
exactly one customer, and the website does not need a sharing matrix.

| Table | Meaning |
| --- | --- |
| `public.inquiry_proforma_shares` | one row per proforma: the customer account, the revision that was shared, who shared it and when |

Sharing is an operation, not a flag:

- `share_inquiry_proforma(proforma_id)` resolves the customer **in the database**
  from `inquiries.web_submission_id`, never from a request body, writes the share
  row, and moves `Draft` to `Sent`.
- `revoke_inquiry_proforma_share(proforma_id)` deletes the row. Access ends
  immediately; the status stays `Sent`, because the document really was sent.

## 2. Who can read what

The customer read policy on `inquiry_proformas` requires all three:

1. a share row naming the caller,
2. the share's revision equal to the proforma's current revision, and
3. the status is not `Draft`.

The second condition is the important one: **saving a newer revision withdraws the
customer's sight of it**. The customer never sees figures nobody shared with them,
and the operator panel says "shared revision 2, now on revision 3, share again"
rather than leaving a stale document in someone's account.

What stays internal, even for a shared document:

| Internal record | Customer access |
| --- | --- |
| `inquiry_proforma_saves` (who saved which revision) | none, operator roles only |
| `inquiry_proforma_groups` (base rate and surcharge behind each specification) | none, operator roles only |
| Other customers' documents | none |
| Drafts, unshared documents, never-shared revisions | none |

There is no public route, no directory listing and no way to enumerate documents:
a customer's own token is the only thing that unlocks anything, and it unlocks only
rows the policies already agree to.

## 3. The customer pages

| Route | What it shows |
| --- | --- |
| `/account/proformas` | the documents shared with this account, with number, revision, status, totals and the date it was shared |
| `/account/proformas/<id>` | the shared revision, printed from stored values only, with print/save-as-PDF |
| `/account` | a "Proforma invoices" shortcut beside the existing account links |

The document view reuses `ProformaDocument`, so a customer and an operator read the
same markup from the same stored rows. Nothing is recalculated.

## 4. The operator side

`ProformaSharePanel` sits at the top of the workspace and reports exactly one of
five states, each with the honest next step:

| State | What it says |
| --- | --- |
| Not saved yet | save the proforma before sharing it |
| Inquiry has no customer account | nobody to share with; the link comes from the submission the inquiry was published from |
| Not shared | share revision N; nothing is visible in the customer's account |
| Shared but stale | shared revision N, document is on revision M, share again |
| Shared and current | shared on <date> at revision N, with a withdrawal button |

`can_write` comes from the same role row the database checks, so an auditor sees
the state and the reason the action is off, instead of a button that fails.

## 5. What changed in the status vocabulary

`IMPLEMENTED_TRANSITIONS` is no longer empty. Sharing is a real operation with a
real effect in the customer's account, and it is the moment `Draft` becomes `Sent`.
`Approved` and `Issued` remain unimplemented and are still listed as pending,
because no acceptance and no issuance operation exists.

## 6. Validation, executed

Migration applied twice (second run clean), then 24 probes on the disposable
database:

| Group | Result |
| --- | --- |
| An administrator shares revision 1 | revision returned, document moved `Draft` to `Sent`, share names the submission's owner |
| The customer reads | 1 proforma, its 2 lines, its share row |
| The customer cannot read | save ledger 0, specification rates 0, no write to the ledger (42501) |
| A second customer reads | 0 proformas, 0 lines |
| A Draft is never visible, even when shared | 0 rows |
| A revision nobody shared | 0 rows; the operator sees "1 of 2"; re-sharing revision 2 restores access |
| Withdrawal | access ends at once, status stays `Sent`, ledger empty |
| Refusals | bridge account 42501, cancelled proforma 22023, unlinked inquiry P0002 |

Automated gates for this phase are recorded in the repository history: vitest,
the node suites, ESLint, `tsc --noEmit` and the production build.

## 7. Production status

**Not applied.** `supabase-inquiry-proforma-phaseC7-customer-share.sql` must be
applied after phase15, phase15-write, phaseC1 and phaseC5. Until then the customer
pages answer "no proforma invoice has been shared with this account", which is
true, and the operator panel says the sharing state cannot be read.

## 8. Remaining blockers, separated as requested

### A. Customer access to their own PI

1. **C1 and C7 must be applied** through the migration process. C7 reads
   `inquiries.web_submission_id`, which C1 adds. Without both, no share can exist.
2. **The inquiry must be linked to a submission.** `publish_inquiry_snapshot` has
   no parameter that writes `web_submission_id`, so today the bridge cannot set it
   even when the desktop knows the submission. Sharing therefore refuses with
   "not linked to a customer account". Closing this needs either a parameter on the
   publication RPC (a schema change) or a desktop-side field the bridge forwards.
3. **Notification is not implemented.** Sharing makes the document available in the
   account; it does not email anyone. That behaviour is undecided, so it was not
   invented.
4. **Acceptance is not implemented.** The customer can read a shared document but
   cannot accept, query or decline it; `Approved` remains unreachable.

### B. Desktop to website publication

1. The bridge has never run: `inquiry_sync_runs` is 0 while 8 complete submissions
   wait. This is upstream of every proforma screen.
2. The single `inquiry_sources` row is the Phase 8 test source and is assigned to a
   **disabled** `inquiry_bridge` account, so a run would be refused.
3. Fixing either is a production data change; left untouched by instruction.

### C. Persistent pricing settings

1. **C5 is not applied**, so `inquiry_proforma_groups` does not exist in
   production; the workspace reports that rates were not stored instead of
   pretending.
2. A proforma saved before C5 has no settings rows, so reopening it shows empty
   rate boxes until it is saved again. No backfill was invented.
3. The 4 m² threshold and 20 % uplift still have no agreed configuration source,
   and VAT and other charges (Step 6) remain separate and unimplemented.
