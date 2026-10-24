# Phase C8: the inquiries page has two audiences

A customer signed in at `/inquiries` and read a screen about `inquiry_auditor`
roles, an instruction to ask an administrator for a role they do not need, and a
button into the website submission queue — an internal page that then refused
them too. The operator register, the submission queue and the estimator
workspace appeared to share one page with the public customer workflow. They
never shared data, but they shared a URL, so they looked public.

## What is private, and what enforces it

| Surface | Who may open it | Enforced by |
| --- | --- | --- |
| `/inquiries` | everyone | `InquiryLanding`: a customer sees the customer page, a staff role sees the register |
| `/inquiries/submit`, `/inquiries/chat`, `/inquiries/submissions/[id]` | the signed-in customer | `customer_user_id = auth.uid()` in the table policies and in every API route |
| `/inquiries/customer-submissions` (+ detail) | `inquiry_auditor`, `inquiry_admin` | `authorizedClient` on the route, `review_customer_inquiry_submission` for writes |
| `/inquiries/estimator` (+ detail) | `inquiry_estimator` (scoped), `inquiry_admin` | route + `inquiry_estimator_assignments` policies |
| `/inquiries/[id]`, `/inquiries/[id]/proforma` | `inquiry_auditor`, `inquiry_admin` | route + proforma policies (C1/C5/C7) |

The live policies confirm the split. `inquiry_customer_submissions` returns a
row to:

* its owner — `customer_user_id = auth.uid()`;
* an auditor or admin, only when the submission is `complete`;
* the bridge, only when it is `complete` **and** `accepted`.

There is no policy that lets one customer read another customer's submission,
and none that lets a customer read the operator projections. The classification
in `src/lib/inquiry-access-roles.ts` is a router, not a permission: it decides
which page to *show*, and every read and write still re-checks the role on the
server.

## What changed

| File | Change |
| --- | --- |
| `src/components/inquiries/InquiryLanding.tsx` | New. Reads `/api/inquiries/access` once and branches: register, estimator notice, bridge notice, refusal, or the customer page. A failed access read **fails closed** on a retry card, never on the register. |
| `src/components/inquiries/CustomerInquiriesHome.tsx` | New. Three customer surfaces (submit, my inquiries and messages, proformas shared with me), a "what is staff only" note, and — signed out — a sign-in path. No internal link, no role vocabulary. |
| `src/lib/inquiry-access-roles.ts` | New. The one place that names `inquiry_auditor`/`inquiry_admin` and classifies an account as register, estimator, bridge, restricted or customer. |
| `src/app/[locale]/inquiries/page.tsx` | Renders `InquiryLanding`; metadata is now "Inquiries" instead of "Inquiry document register / restricted internal". |
| `src/components/inquiries/InquiryAccessGate.tsx` | A customer sees "This page is for ProGlass staff" with their own links; the internal queue button is gone; the role-granting SQL block is hidden from accounts that hold no role; an estimator is offered the estimator workspace. |

## Tests

| Command | Result |
| --- | --- |
| `npx vitest run` | 34 files, 360 pass |
| `node --experimental-strip-types --test scripts/*.test.mjs` | 628 pass |
| `npx tsc --noEmit` | clean |
| `npm run lint` | clean |
| `npm run build` | clean |

`tests/inquiry-landing.test.tsx` proves the eight cases that matter: signed out;
plain customer; admin; auditor; estimator; bridge; a role that is switched off;
and an access read that fails. In every case it asserts that the operator
register is mounted only for a register role, and that a customer page contains
no internal link.

`scripts/inquiry-intake-security.test.mjs` keeps the split at the source level,
including that the refusal screen no longer points at `/inquiries/customer-submissions`.

## Unchanged

No migration, policy, grant, role or bridge setting was touched. The delivery
RPCs, the review RPC and the storage rules are exactly as they were. Nothing was
accepted, acknowledged or downloaded.
