# Why the admin could not see every website submission

## Symptom

An authenticated `inquiry_admin` reported that the operator website showed only
part of the complete website intake. At the time of the report the queue held
seven complete submissions: four `submitted`, one `under_review` and two
`downloaded`. The four pending ones were the ones the operator was looking for.

## What was checked first

1. **The project.** The Supabase connector lists exactly one project,
   `afpbdyamedpanwijxghk` (Proglass Automation, ap-northeast-1), and the
   production Vercel variable `NEXT_PUBLIC_SUPABASE_URL` resolves to
   `https://afpbdyamedpanwijxghk.supabase.co`. The website cannot be reading a
   second project.
2. **The rows.** All seven rows had `upload_state = 'complete'`, none had a
   null reference, company, project, location, `submitted_at` or
   `status_updated_at`, and the documents were attached and available.
3. **The policy.** The read policies on `inquiry_customer_submissions`,
   `inquiry_customer_documents` and the `inquiry-intake-documents` objects all
   carry the same operator branch:
   `upload_state = 'complete'` AND `has_inquiry_role(['inquiry_auditor','inquiry_admin'])`.
4. **The identity.** Inside a transaction rolled back at the end, with
   `set local role authenticated` and the admin's own JWT claims:

   | probe | result |
   | --- | --- |
   | complete submissions visible | 8 |
   | of those, `submitted` | 5 |
   | of those, `under_review` | 1 |
   | of those, `downloaded` | 2 |
   | documents visible | 24 |
   | storage objects visible | 24 |
   | enabled roles for this user | 1 |

   (An eighth submission, `WEB-2026-000008`, arrived while this investigation
   was running, which is why the probe reads eight. Nothing was inserted,
   accepted, acknowledged or downloaded to produce these numbers.)

**Conclusion: the database was never the reason.** Row Level Security returns
every complete submission to this identity, the list query applies no status
filter, and the detail read and the document download are both authorized for
an operator.

## Root cause, in the operator UI

The failure was in the operator surface, in four places:

1. **The inbox was the wrong register.** `/inquiries` lists `inquiries`, the
   desktop publication, which is empty until the Windows bridge publishes an
   accepted and downloaded submission. That is the page an operator lands on,
   and it showed nothing about the seven rows. The only route to them was one
   small "Website submissions" link with no count on it.
2. **The register narrowed in the browser.** It fetched up to two hundred rows
   and filtered them client side, so a filter could only ever hide rows and the
   page could not say how many rows existed.
3. **The status filter was missing a status.** Its option list was typed out by
   hand as `submitted, under_review, accepted, rejected, closed`. `downloaded`
   was not in it, so the two downloaded rows could not be isolated, and the
   vocabulary existed in two places that had drifted.
4. **The review screen misreported the current status.** A `downloaded`
   submission opened on a select with no matching option, showing no selection
   at all, and a save would have sent that empty value. A `submitted`
   submission opened on "Under review", which is a real review transition, but
   nothing said so.

None of this is an authorization problem, and none of it is fixed by widening
access.

## The fix

| file | change |
| --- | --- |
| `src/types/inquiry-submissions.ts` | one runtime vocabulary for all six statuses (`CUSTOMER_INQUIRY_STATUSES`), the four the review RPC may write (`CUSTOMER_INQUIRY_REVIEW_STATUSES`), and the paged result type |
| `src/lib/inquiry-submission-status.ts` | labels, badge tones, one line of meaning per status, and the rule for which status a review screen may open on |
| `src/lib/inquiry-submissions-api.ts` | `listOperatorCustomerInquiries` now takes status, search, page and page size, applies them in PostgREST under the caller's token, returns exact `total`, `pageCount`, per status counts and `allTotal`; six `head` counts supply the tabs |
| `src/app/api/inquiry-submissions/operator/route.ts` | forwards `status`, `q`, `page`, `pageSize` |
| `src/components/inquiries/SubmissionStatusTabs.tsx` | the filter, rebuilt as six counted tabs plus All |
| `src/components/inquiries/SubmissionPager.tsx` | "showing x to y of n" from the server count, Previous and Next only when there is a page to move to |
| `src/components/inquiries/CustomerSubmissionRegister.tsx` | server driven search, filter, paging and counts; a status badge, submitted date, document count and desktop handoff line on each row; empty states that distinguish "no rows at all" from "no rows for this filter"; no write of any kind |
| `src/app/[locale]/inquiries/customer-submissions/page.tsx` | reads `status`, `q` and `page` so a filter can be linked to |
| `src/components/inquiries/WebsiteSubmissionsPanel.tsx` | a counted strip in the inbox: complete total, how many are waiting, one link per status, and a way into the queue |
| `src/components/inquiries/CustomerSubmissionReview.tsx` | current status as a badge with the time it changed; `downloaded` opens with no status chosen and a warning that Accepted re-queues it for the desktop; `submitted` says that saving moves it to Under review; a rejected or closed review is locked and says it cannot be reopened; documents show size and upload time with an explicit Download |
| `src/components/inquiries/InboxCards.tsx` | an empty desktop register now says it is empty and explains what fills it |
| `src/components/inquiries/InquiriesDashboard.tsx` | renders the website queue strip above the desktop register |

## What was deliberately not done

* No RLS policy, grant or role was changed. No migration was needed: every
  table, policy, RPC and bucket this surface uses already existed and already
  authorized the admin.
* `review_customer_inquiry_submission`,
  `authorize_customer_inquiry_document_download` and the delivery RPCs,
  including `plan_customer_inquiry_deliveries_v2`, are untouched.
* No acknowledgement row was inserted, no status was changed, and no document
  was downloaded to make rows appear.
* No document is read through a public URL. The download still goes through
  `authorize_customer_inquiry_document_download` with the operator's own
  session, and the existing storage policy for the `inquiry-intake-documents`
  bucket.
* The register cannot accept, reject, download or acknowledge anything. Review
  is a deliberate choice on the detail screen, and a submitted or downloaded
  row is never written back as it is.

## Tests

| command | result |
| --- | --- |
| `node --test scripts/*.test.mjs` | 615 pass, 0 fail |
| `npx vitest run` | 27 files, 282 pass |
| `npm run lint` | clean |
| `npm run typecheck` | clean |
| `npm run build` | clean |

New tests:

* `tests/customer-submissions-query.test.tsx` pins the six status vocabulary,
  the review selection rule and the query parser (status, sanitised search,
  bounded page and page size, inclusive range).
* `tests/customer-submissions-api.test.tsx` drives `listOperatorCustomerInquiries`
  against a strict PostgREST stand-in and proves the status filter, the search
  expression, the range, the exact counts and the row shape.
* `tests/customer-submissions-register.test.tsx` renders the register against a
  fixture of seven rows and proves all seven appear with their statuses, that a
  tab asks the server, that paging requests page two, that a linked filter opens
  filtered, and that an empty filter explains itself.
* `tests/customer-submission-review.test.tsx` proves a downloaded submission
  opens with no status chosen, saves exactly the chosen status only when the
  operator presses save, and that a rejected review is locked.
* `tests/website-submissions-panel.test.tsx` proves the inbox strip counts the
  queue and links every status.
* `scripts/inquiry-admin-visibility.test.mjs` keeps the whole repair in place at
  the source level, including that no screen retypes the status list and nothing
  uses service credentials or weakens row level security.
