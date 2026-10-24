# Phase C10: a proforma invoice request is visible, and approval is a flag

C9 parked a proforma invoice request in `under_review`, so the desk saw nothing
at all until a coordinator accepted it. That is wrong for a shop floor that needs
to know what is coming, and it hid the customer's attachments behind a click
nobody had made yet.

The request now behaves like every other submission and keeps one extra fact:
a coordinator has not approved the priced document yet.

| | C9 | C10 |
| --- | --- | --- |
| Finalized as | `under_review` for `proforma_invoice` | `submitted` for every type |
| Desk sees it | only after acceptance | immediately, with its files |
| Approval | a gate on delivery | a recorded decision (`accepted_at`) |
| A deliberate hold | `under_review` | unchanged: `under_review` still withholds it |
| The submitted event | `desktop_handoff: held_for_coordinator` | `desktop_handoff: ready`, `approval_required: true\|false` |

## How "awaiting approval" is read

Derived, never stored twice:

```
inquiry_type = 'proforma_invoice' AND status IN ('submitted','under_review','downloaded')
```

That is the same rule in `src/lib/inquiry-submission-status.ts`
(`isAwaitingProformaApproval`), on the queue row, and on the review screen.
Because it is derived, it survives the desk downloading the submission: a
request that was pulled by the shop PC and never accepted is still awaiting
approval, and the badge says so.

Approving is the existing review action — set the status to **Accepted**. The
review screen now says that in words on a proforma request, so nobody has to
guess which of the four statuses means "approved". A request that was already
delivered can still be accepted: only `rejected` and `closed` are locked.

## What the desk needs

The delivery plan already returns `inquiry_type` and `status`, so no new call is
needed: a row with `inquiry_type='proforma_invoice'` and `status='submitted'` is
a request nobody has approved. The desktop prompt asks the WPF session to show
that as a badge on the row; nothing about the download changes.

## Files

* `supabase-inquiry-intake-phaseC10-proforma-requests-visible.sql` — the
  migration, one transaction, idempotent, with the C9 behaviour restored in its
  rollback block.
* `src/lib/inquiry-submission-status.ts` — `isAwaitingProformaApproval`.
* `src/components/inquiries/CustomerSubmissionRegister.tsx` — the queue badge.
* `src/components/inquiries/CustomerSubmissionReview.tsx` — the approval note.
* `scripts/inquiry-auto-handoff.test.mjs` — the source guards.

## Validation

PostgreSQL 17.9, a disposable database, all inquiry migrations replayed, then C9,
then C10:

| Probe | Result |
| --- | --- |
| Under C9, a proforma request | `under_review` |
| Under C10, a new proforma request after finalize | `status=submitted`, `accepted_at IS NULL` |
| The submitted event | `{"inquiry_type":"proforma_invoice","desktop_handoff":"ready","approval_required":true,...}` |
| The bridge plans it | yes, and its storage object is readable |
| A row left in `under_review` by C9 | still not planned |
| A coordinator accepts it | planned, `status='accepted'` |
| Re-apply C10 | clean |

## Applied to production

The live `finalize_customer_inquiry_submission` was changed in place, the same
way C9 was: two string replacements on the function's own current text, guarded
so the migration refuses to run if either line is absent. No other function was
touched. Verified afterwards that the function now contains
`status='submitted',accepted_at=NULL` and
`'approval_required',v_submission.inquiry_type='proforma_invoice'`.

No existing row needed changing: production had no `proforma_invoice`
submission before this migration.
