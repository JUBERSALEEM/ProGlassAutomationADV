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
| `/inquiries` | everyone, signed in or out | `InquiryWelcome`: a public welcome screen with both ways to submit, sign-in and sign-up |
| `/inquiries/register` | `inquiry_auditor`, `inquiry_admin` | `InquiryLanding` + the register API; a customer is sent back to `/inquiries` |
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
| `src/components/inquiries/InquiryWelcome.tsx` | New. The public page: what an inquiry is, both submission paths, my inquiries, shared proformas, sign in, create an account, how it moves, and a staff door to the register. |
| `src/components/inquiries/InquiryLanding.tsx` | The register's router, now mounted at `/inquiries/register`. Branches: register, estimator notice, bridge notice, refusal, or the public page. A failed access read **fails closed** on a retry card, never on the register. |
| `src/lib/inquiry-access-roles.ts` | New. The one place that names `inquiry_auditor`/`inquiry_admin` and classifies an account as register, estimator, bridge, restricted or customer. |
| `src/app/[locale]/inquiries/page.tsx` | Renders `InquiryWelcome` — a public page, not a register. |
| `src/app/[locale]/inquiries/register/page.tsx` | New. The staff register at its own address. |
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
plain customer (both of those now land on the public page); admin; auditor;
estimator; bridge; a role that is switched off; and an access read that fails. In every case it asserts that the operator
register is mounted only for a register role, and that a customer page contains
no internal link.

`scripts/inquiry-intake-security.test.mjs` keeps the split at the source level,
including that the refusal screen no longer points at `/inquiries/customer-submissions`.

## Inquiries are not the reports chain

The Proforma Invoice → Job Order → Delivery Note strip used to sit inside the
inquiry register, which made an inquiry page look like a reports page. It is gone
from the register and from the dashboard; the chain step that points at the
inquiry now points at `/inquiries/register`. `/inquiries` itself carries no
operations chrome at all.

## Unchanged

No migration, policy, grant, role or bridge setting was touched. The delivery
RPCs, the review RPC and the storage rules are exactly as they were. Nothing was
accepted, acknowledged or downloaded.
