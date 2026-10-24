# Phase 14: the owner's admin account may run the desktop delivery loop

Status: **applied on 2026-10-24** on project `afpbdyamedpanwijxghk` as migration
`inquiry_desktop_operator_access_phase14`. Applied from
`supabase-inquiry-desktop-access-phase14.sql`.

## 1. What the shop PC was reporting

The screenshot showed three different things in one window, and they have three
different causes. Only one of them was on the website side.

| What the desk said | What it actually was |
|---|---|
| "Signed in, this account is not authorized for the website inbox" | The account was `inquiry_admin`. The desktop's first call is `plan_customer_inquiry_deliveries_v2`, and Phase 13 left that gated to `inquiry_bridge` alone, so the database raised `42501` and the desk correctly rendered a 403 |
| "Invalid login credentials" | Supabase Auth rejecting the email and password pair. Seven of seven accounts on the project have a password hash, so this is a credentials problem, never a role problem. The exact wording also appears when the box holds an email code instead of a password |
| "Website check failed: Cannot access a disposed object. Object name: System.Net.HttpClient" | A desktop defect. The inbox service owns one `HttpClient`, and something disposed it while a later check still held the service. Nothing on the website can cause or fix this |

The database is not ambiguous about which one it was. Repeated read-only, with
a simulated claim:

```sql
-- admin, the account in the screenshot
select count(*) from public.plan_customer_inquiry_deliveries_v2(25);
-- ERROR: 42501 Inquiry bridge role required.

-- the machine bridge
select count(*) from public.plan_customer_inquiry_deliveries_v2(25);
-- 1 (WEB-2026-000007)
```

Storage was never the blocker. The existing SELECT policy
"Customers and operators download inquiry intake documents" already lets an
enabled `inquiry_admin` read private objects for every complete submission, and a
simulated admin claim sees the three objects for the accepted submission. The
desk never got that far because planning failed first.

## 2. The decision

The owner works at the shop PC and signs in with their own account. Rather than
hand out the machine account's password, `inquiry_admin` now runs the same three
delivery calls as the bridge:

| Function | Before | After |
|---|---|---|
| `plan_customer_inquiry_deliveries_v2` | `inquiry_bridge` | `inquiry_bridge`, `inquiry_admin` |
| `plan_customer_inquiry_delivery_documents` | `inquiry_bridge` | `inquiry_bridge`, `inquiry_admin` |
| `acknowledge_customer_inquiry_delivery_v2` | `inquiry_bridge` | `inquiry_bridge`, `inquiry_admin` |

Everything else is untouched: the estimator, auditor and viewer boundaries, the
accepted-only rule, the replay guard, the document count check, the assignment
snapshot check, the database-derived routing key, the customer status CHECK, and
the private bucket.

### The trade-off, stated plainly

If an admin and the machine bridge both run the delivery loop, both can plan the
same accepted inquiry. The ledger allows exactly one acknowledgement, so the
second one fails closed with a conflict rather than duplicating a delivery. Run
one of them per shop PC. Two shop PCs would need two accounts anyway.

### Capability wording

`inquiry_admin` gains `inbox.plan.bridge` and `inbox.acknowledge` in
`get_my_inquiry_access_v1`. The first name is historical: it means "may plan
desktop deliveries", which the admin now may. The bridge's own five capabilities
are unchanged, so a desktop can test one capability name for either account.

## 3. Why every check after the gate is unchanged

`acknowledge_customer_inquiry_delivery_v2` is the only function in the set that
writes. It still refuses to trust the caller:

- the manifest hash must be 64 lowercase hex characters,
- the document count must be between 0 and 15,
- the assignment snapshot must be an array and must equal the database's own,
- the routing key is recomputed from the primary estimator, never accepted,
- a replay of an accepted delivery returns quietly,
- a second, different acknowledgement raises `23505`.

Identity still comes from `auth.uid()`. No function takes a user id, a role or a
scope parameter.

## 4. Verification after apply

Read-only, on the live project:

```sql
select p.proname, pg_get_function_identity_arguments(p.oid) as args,
       has_function_privilege('anon', p.oid, 'EXECUTE') as anon_exec,
       has_function_privilege('authenticated', p.oid, 'EXECUTE') as auth_exec
  from pg_proc p where p.pronamespace = 'public'::regnamespace
   and p.proname in ('plan_customer_inquiry_deliveries_v2',
                     'plan_customer_inquiry_delivery_documents',
                     'acknowledge_customer_inquiry_delivery_v2');
```

Then, inside a transaction that is rolled back:

- `techwithyassh@gmail.com` (`inquiry_admin`) plans the one accepted submission,
  `WEB-2026-000007`, and reads its three documents.
- `up17mix@gmail.com` (`inquiry_bridge`) plans the same row.
- `jubersaleem01@gmail.com` (`inquiry_estimator`), `ffyassh@gmail.com` (a bridge
  role with `enabled = false`), an auditor, a viewer and an account with no role
  are all refused with `42501`.
- No session at all is refused at the grant, not just inside the function.

## 5. Tests

Deterministic database assertions against a scratch database:

```
psql "$SCRATCH_URL" -v ON_ERROR_STOP=1 -f supabase-tests/phase13-role-matrix-harness.sql
psql "$SCRATCH_URL" -v ON_ERROR_STOP=1 -f supabase-inquiry-role-matrix-phase13.sql
psql "$SCRATCH_URL" -v ON_ERROR_STOP=1 -f supabase-inquiry-desktop-access-phase14.sql
psql "$SCRATCH_URL" -v ON_ERROR_STOP=1 -f supabase-tests/phase13-role-matrix-assertions.sql
psql "$SCRATCH_URL" -v ON_ERROR_STOP=1 -f supabase-tests/phase14-desktop-access-assertions.sql
```

34 assertions from Phase 13 and 13 from Phase 14, all green. Phase 13 case 19
now guards the estimator refusal, because the admin is deliberately allowed.

Static contracts, part of `npm run test:inquiry-security`:

```
node --experimental-strip-types --test scripts/inquiry-desktop-access.test.mjs
```

## 6. What is still the desktop's job

Three things on the shop PC cannot be fixed from this repository, and they are
listed here so the next person does not look for them in SQL:

1. Persist one `HttpClient` for the life of the inbox service and never dispose
   it while a check is running. A disposed client is what produced "Website
   check failed: Cannot access a disposed object".
2. Send the `apikey` header on storage downloads as well as the bearer token.
   Supabase Storage expects both, exactly as `supabase-js` sends them.
3. Call `get_my_inquiry_access_v1` right after sign-in and show its
   `denial_reason`. That turns an unexplained 403 into "signed in, but the
   account has no enabled inquiry role" before any delivery call is attempted.

## 7. Rollback

Run the block below, then re-run the three files named after it. No table, row,
delivery or event is dropped.

```sql
BEGIN;
CREATE OR REPLACE FUNCTION public.plan_customer_inquiry_deliveries_v2(p_limit integer DEFAULT 25) ...
CREATE OR REPLACE FUNCTION public.plan_customer_inquiry_delivery_documents(p_submission_id uuid) ...
CREATE OR REPLACE FUNCTION public.acknowledge_customer_inquiry_delivery_v2(...) ...
COMMIT;
```

In practice, re-running these in order restores the Phase 13 state exactly:

1. `supabase-inquiry-role-matrix-phase13.sql` (capabilities back to eight admin
   capabilities)
2. `supabase-inquiry-estimator-phase9e-handoff.sql` (plan and acknowledge
   back to bridge only)
3. `supabase-inquiry-intake-phase8c-inbox.sql` (document plan back to bridge
   only)

After a rollback the desk must sign in as `up17mix@gmail.com`, or the 403
returns.
