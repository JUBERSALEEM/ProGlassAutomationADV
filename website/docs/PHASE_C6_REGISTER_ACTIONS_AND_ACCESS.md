# Phase C6: register actions and access truth

What this phase fixes, why the proforma workflow looked missing on the live site,
and what still cannot be built without a decision.

No production schema change was made. Nothing was applied to the database.

---

## 1. What was actually wrong

Two separate things, both real.

**The account.** The register is gated on an enabled `inquiry_auditor` or
`inquiry_admin` role, read from `inquiry_operator_roles`. An account holds exactly
one role (the table's key is `user_id`), so a user cannot be "bridge and viewer".
The account being used to look at the register holds `inquiry_bridge`, which is the
desktop publication identity: `publish_inquiry_snapshot` requires that role and
matches `inquiry_sources.bridge_user_id` to the caller. Giving that account
`inquiry_admin` would stop the desktop publication. The website therefore has to
say which account is which, instead of a dead end that reads like a bug.

**The register.** `public.inquiries` is empty because nothing has been published
from the desktop yet, so there was no inquiry card and no link into the proforma
workspace. The workspace page exists and is deployed, but nothing on screen led to
it.

The denial screen used to print "does not have an enabled inquiry viewer role",
which names a role (`inquiry_viewer`) that does not grant access either, and then
offered a sign-in link and two dead ends.

## 2. What changed

| Change | File |
| --- | --- |
| The role behind a refusal is now readable, with the account, the role held and the role needed | `src/app/api/inquiries/access/route.ts` (new), `readOperatorAccess` in `src/lib/inquiries-api.ts` |
| The refusal screen states the account, the role, the required role, and explains the bridge and estimator roles by name | `src/components/inquiries/InquiryAccessGate.tsx` |
| A refused register is shown on the register, not redirected to the public customer form | `src/components/inquiries/InquiriesDashboard.tsx` |
| "New PI", "Submit customer inquiry" and an inquiry chooser on the register, with a real explanation when there is nothing to choose | `src/components/inquiries/ProformaLauncher.tsx` (new) |
| An unknown inquiry reference ends on "Inquiry not found" with the way back, instead of a bare error | `src/components/inquiries/ProformaWorkspace.tsx` |
| The inquiry page separates "wrong role" from "not found" and says which | `src/components/inquiries/InquiryDetail.tsx` |

The launcher is honest about the model: a website proforma requires
`inquiry_proformas.inquiry_id`, which points at a published inquiry. The chooser
lists those inquiries, and when there are none it says the register fills from the
desktop publication and offers the submission queue. It does not invent an inquiry
number or a customer record to make a button work.

## 3. The flow after this change

1. Sign in as an account holding `inquiry_auditor` or `inquiry_admin`.
2. `/inquiries` now opens with **New PI**, **Submit customer inquiry**, and, once
   there are inquiries, a searchable chooser whose rows each carry **Create PI**,
   **Resume PI** or **Open PI**.
3. The workspace at `/inquiries/<id>/proforma` is the same one; the register simply
   now reaches it.

## 4. What is still not possible, and why

- **A PI for a customer submission that has not been published.** The desktop owns
  inquiry numbering (`INQ-YYYY-NNNN`) and publishes inquiries through the bridge.
  Letting the website create inquiries from submissions would fork that identity;
  it needs a decision, not a button.
- **Any PI at all until the first publication.** With an empty register there is
  nothing to hang a PI on. The page says so.
- **C5 persistence.** `inquiry_proforma_groups` is not in production, so rate
  settings will not persist there; the workspace reports that rather than pretending.

## 5. Tests

`tests/inquiry-proforma-launcher.test.tsx` (new, 6 tests): the two visible actions,
a published inquiry linking to its workspace, a draft labelled Resume, search by
customer, the empty-register explanation appearing without a click, and the
filtered-empty case being described as filters rather than an empty register.

Gates for this phase are recorded with the change in the repository history.
