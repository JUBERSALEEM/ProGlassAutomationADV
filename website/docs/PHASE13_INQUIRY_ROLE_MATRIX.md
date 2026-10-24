# Phase 13: Inquiry role matrix, self-access, and inbox scope

Status: **proposed, not applied.** The migration in
`supabase-inquiry-role-matrix-phase13.sql` has not run on any project, no role
assignment has changed, and nothing has been deployed. Everything below marked
"verified" was read-only inspection plus simulated-claim calls inside
transactions that were rolled back.

Project inspected: `afpbdyamedpanwijxghk` (`https://afpbdyamedpanwijxghk.supabase.co`,
"Proglass Automation", ap-northeast-1). It is the only project on the connected
account, so if the shop PC points anywhere else, none of this describes it.

## 1. The pipeline as it actually stands

### Submission

`POST /api/inquiry-submissions/drafts` calls `create_customer_inquiry_draft`,
which inserts one row with a generated `id`, a sequence-backed
`submission_reference` (`WEB-YYYY-NNNNNN`), `upload_state = 'draft'`,
`expected_document_count`, and a `draft_created` event. The reference is stable
from the first write, so a retry cannot mint a second number.

Each file goes to Storage under `{auth.uid()}/{submission_id}/{document_id}`,
then `register_customer_inquiry_document` checks that the object exists, that the
key matches that exact path, that the byte size and hash are in range, that the
count is under `expected_document_count`, and that the filename is not a
duplicate. Only then does it write metadata and a `document_registered` event.

`finalize_customer_inquiry_submission` is the only place a submission becomes
`complete`. It refuses unless the registered document count equals
`expected_document_count`, sets `finalized_at` and `submitted_at`, and writes
`submitted`.

Consequence: a failed upload cannot leave a complete submission. A draft stays
`draft` with a short document count and is invisible to every operator policy and
to every inbox RPC, all of which require `upload_state = 'complete'`. Verified
statically and against the harness fixtures (draft excluded from the inbox).

### Review

`review_customer_inquiry_submission` requires `inquiry_admin`, accepts only
`under_review`, `accepted`, `rejected` and `closed`, refuses to reopen a closed
state, and writes a customer-visible `status_changed` event. Intake has no draft
insert from the desktop and no write grant on any intake table, so the desktop
can never create a submission.

### Desktop handoff

`plan_customer_inquiry_deliveries_v2` returns complete, accepted submissions
that have no delivery row, with document count, primary estimator code,
assignment snapshot and routing key. `plan_customer_inquiry_delivery_documents`
returns the available documents with bucket, key, hash and size.
`acknowledge_customer_inquiry_delivery_v2` checks the manifest hash shape, the
document count against a live recount, the assignment snapshot against a fresh
one, and the routing key against the snapshot, then writes the ledger row, moves
the submission to `downloaded`, and writes a customer-visible `inbox_delivered`
event.

All three gate on `has_inquiry_role(ARRAY['inquiry_bridge'])` inside the body.

### What I verified on the live project

Read-only, then inside transactions that were rolled back:

| Check | Result |
|---|---|
| Bridge plan as the enabled bridge | one row, `WEB-2026-000007`, accepted, 3 documents, `assigned/EST-PRIMARY-TEST` |
| Same call through `SET LOCAL ROLE authenticated` | identical, so RLS and the execute grants do not block the bridge |
| Document plan | three files with hashes and sizes |
| Acknowledgement with a wrong snapshot | `40001 Estimator assignment changed.` |
| Acknowledgement, correct snapshot | status `downloaded`, delivery row with route, primary, bridge user, one `inbox_delivered` event |
| Second identical acknowledgement | failed, see the defect below |
| Estimator calling the bridge plan | `42501 Inquiry bridge role required.` |
| No claim, or an admin, or a disabled bridge | `42501` in every case |

Live role rows at the time of inspection: two `inquiry_admin` (both enabled), two
`inquiry_bridge` (`up17mix@gmail.com` enabled, last sign-in today;
`ffyassh@gmail.com` disabled), two `inquiry_estimator` (both enabled, one to each
of the two assignments). No `inquiry_viewer` and no `inquiry_auditor` row exists.

### Defect found: acknowledgement is not idempotent

The delivered contract says a replay of the same manifest and count is accepted
quietly. It is not. The function recounts documents with
`s.status = 'accepted'` **before** it looks at the ledger, and the first
acknowledgement has already moved the row to `downloaded`, so the recount returns
zero and the replay raises `40001 Document count changed.` instead of returning.

Reproduced both ways: against the live project (first call succeeded, identical
second call raised `40001`) and in the scratch harness, where the old body prints
`Document count changed. (40001)` on retry and the new body returns quietly. The
conflict branches can never be reached at all, so a genuine manifest mismatch is
reported as a count change.

The fix in this migration moves the ledger check first. Signature, stored
columns and the bridge gate are unchanged.

### The 403 from the desktop check

Sign-in succeeded and the plan call was refused, so the session existed in GoTrue
but not in the request that reached Postgres. The server side is demonstrably
fine: the same RPC, called with the bridge's own `sub` claim as the
`authenticated` role, returns the expected row.

`auth.uid()` is null in that function only when the request carried no user JWT
(an anon key, or a client built without the session), or when it was sent to a
different project. The role row is enabled and was not the cause.

Triage, in order, from the desktop:

1. Call `get_my_inquiry_access_v1` with the same client that calls the plan RPC.
   `authenticated = false` means the session is not attached to the request.
   The reason string says exactly that.
2. If it returns an identity that is not the bridge account, the client is
   configured with another account or another project.
3. If it returns the right identity with no roles, the role row is wrong or
   disabled.
4. Only then look at the RPC.

The RPC also exists so the desktop can show the signed-in user's own roles rather
than a generic failure.

## 2. The role matrix

The matrix is the one agreed for this phase. It is implemented in the migration
as server-side checks and storage policies; nothing is decided from a client
parameter, a claim other than `sub`, or a user-supplied role.

| Role | Submissions | Documents | Acknowledge | Pipeline write-back | Chat |
|---|---|---|---|---|---|
| `inquiry_admin` | all complete | only visible submissions | no (bridge only) | any submission | permitted threads |
| `inquiry_auditor` | all complete | only visible submissions | no | no | permitted threads |
| `inquiry_estimator` | only active assignments | only those submissions | no | only assigned submissions | only assigned threads |
| `inquiry_bridge` | accepted, undelivered (unchanged) | only those submissions | yes (unchanged) | unchanged | unchanged |
| `inquiry_viewer` | none | none | no | no | none |

Capabilities, as returned by the self-access RPC:

| Role | Capabilities |
|---|---|
| `inquiry_admin` | `inbox.read.all`, `inbox.download`, `inbox.review`, `pipeline.write`, `chat.read`, `chat.write`, `audit.read`, `estimator.manage` |
| `inquiry_auditor` | `inbox.read.all`, `inbox.download`, `chat.read`, `audit.read` |
| `inquiry_estimator` | `inbox.read.assigned`, `inbox.download`, `pipeline.write`, `chat.read`, `chat.write` |
| `inquiry_bridge` | `inbox.plan.bridge`, `inbox.download`, `inbox.acknowledge`, `pipeline.write`, `chat.read` |
| `inquiry_viewer` | none |

Unassigned work is **not** included for estimators. Estimates are derived from
`inquiry_estimator_assignments` joined to an active `inquiry_estimators` row,
never from the request.

### What the migration changes

New:

- `inquiry_capabilities_for_roles(text[])` maps roles to capabilities, so the
  website, the desktop and the documentation cannot drift.
- `get_my_inquiry_access_v1()` returns the caller's own id, email, display name,
  enabled roles, capabilities and a denial reason. Identity comes from
  `auth.uid()`; there is no parameter and no other account is readable.
- `inquiry_can_read_submission(uuid)` is one read predicate for server use.
- `list_customer_inquiry_inbox_v1(p_limit)` is the operator inbox, scoped by the
  database. An estimator gets assignments only, an admin or auditor gets
  everything, anyone else is refused. It is read-only and never touches the
  delivery ledger.
- `inquiry_operator_role_audit` plus an `AFTER INSERT OR UPDATE OR DELETE` trigger
  on `inquiry_operator_roles`, recording account, role, enabled, action, actor
  and time, readable only by admins and auditors.

Changed, with approval needed before apply:

1. **`inquiry_viewer` loses the intake surfaces.** Three select policies are
   rebuilt without the viewer branch and the download authorization function no
   longer accepts it. No live account holds this role, so nothing breaks today.
   The website operator gate was narrowed to match, in source.
2. **Pipeline write-back widens** from bridge-only to bridge, admin, and an
   actively assigned estimator. The event records
   `source = 'website_operator'` for a person and `'desktop_inbox'` for the
   bridge, and `pipeline_updated_by` keeps the actor.
3. **The chat pull widens** to bridge, admin, auditor and an assigned estimator,
   with the same columns.
4. **Acknowledgement replay** is fixed as described above.

Unchanged: the submission status CHECK constraint, every customer-facing read and
policy, the bridge plan RPCs, the global community room, and the `pipeline_*`
columns as the only carrier of desktop workflow state.

The migration is idempotent: it applied twice in a row in the scratch harness
without error.

## 3. Applying it

Order, once approved:

1. Review the file and this document.
2. Apply `supabase-inquiry-role-matrix-phase13.sql` through the project's
   migration path, not the SQL editor, so history matches the repository.
3. Verify (read-only):

```sql
select p.proname, pg_get_function_identity_arguments(p.oid) as args,
       has_function_privilege('anon', p.oid, 'EXECUTE') as anon_exec,
       has_function_privilege('authenticated', p.oid, 'EXECUTE') as auth_exec
  from pg_proc p join pg_namespace n on n.oid = p.pronamespace
 where n.nspname = 'public'
   and p.proname in ('get_my_inquiry_access_v1','list_customer_inquiry_inbox_v1',
                     'inquiry_can_read_submission','inquiry_capabilities_for_roles');
```

4. Call `get_my_inquiry_access_v1` as an admin and as an estimator and confirm the
   roles and capabilities are what the matrix says.
5. Confirm the bridge still plans: `select * from plan_customer_inquiry_deliveries_v2(5);`
   as the bridge account.

### Rollback

Run the block at the bottom of the migration in a separate transaction, then
re-run `supabase-inquiry-intake-phase8a-staged-uploads.sql`,
`supabase-inquiry-estimator-phase9b-access.sql` and
`supabase-inquiry-inbox-bridge-phase12.sql` to restore the previous policy and
function bodies. No table is dropped by the rollback except the new audit table,
so no submission, document, delivery or event is lost.

## 4. Tests

Static contracts, part of `npm run test:inquiry-security`:

```
node --experimental-strip-types --test scripts/inquiry-role-matrix.test.mjs
```

They check the grants and revokes for every new function, that identity comes only
from `auth.uid()`, that the inbox has no scope parameter, that the viewer branches
are gone while the owner branch survives in each policy, that the delivery ledger
is checked before the queue, that the role audit exists and is closed, that the
customer status vocabulary is untouched, and that the operator pages show the
handoff state.

Deterministic database assertions, against a scratch database only:

```
psql "$SCRATCH_URL" -v ON_ERROR_STOP=1 -f supabase-tests/phase13-role-matrix-harness.sql
psql "$SCRATCH_URL" -v ON_ERROR_STOP=1 -f supabase-inquiry-role-matrix-phase13.sql
psql "$SCRATCH_URL" -v ON_ERROR_STOP=1 -f supabase-tests/phase13-role-matrix-assertions.sql
```

34 cases, each run as `authenticated` with a simulated claim, covering: no
session, bridge, admin, assigned estimator, unrelated estimator, auditor, viewer,
a disabled role, an account with no role, admin inbox scope, estimator inbox
scope, empty estimator inbox, table-level RLS for each role, customer isolation
including a draft, download authorization for allowed and refused callers,
acknowledgement success, replay and conflict, bridge-only acknowledgement,
pipeline write-back for an assigned estimator, refusal for an unassigned one,
attribution and event source, chat scope, the role audit and its readers, anon
privileges, the untouched status check and the untouched bridge plan.

## 5. Desktop authentication

Decision: **email and password for desktop-capable accounts.** The website keeps
email OTP; the two do not mix, and the desktop password field must not accept an
OTP.

- Password setup: Supabase dashboard, Authentication, Users, the account, "Send
  recovery" or set a password directly. The account must then sign in with
  `signInWithPassword`.
- An account that only ever used OTP has no password and will fail
  `signInWithPassword` with 400, which is a credentials problem, not a role
  problem.
- No service_role key and no password in website code, logs, analytics or
  unprotected configuration. Errors and telemetry must redact JWTs.
  `get_my_inquiry_access_v1` deliberately returns no token material.

HTTP semantics the desktop should implement:

| Status | Meaning | UI |
|---|---|---|
| 401 | No session, expired session, bad credentials | "Session expired" or "Sign-in failed" with a retry |
| 403 | Authenticated, not authorized | "Your account cannot use the inbox" plus the roles and reason from the self-access RPC |

A successful sign-in followed by a 403 is never "sign-in failed".

## 6. Setup for a new operator

1. Create the account in Supabase Auth (dashboard).
2. Grant exactly one role:

```sql
insert into public.inquiry_operator_roles(user_id, role, enabled)
values ('<auth-user-uuid>', 'inquiry_admin', true)
on conflict (user_id) do update set role = excluded.role, enabled = true, updated_at = now();
```

3. An estimator also needs a directory row: `register_inquiry_estimator` as an
   admin, which creates the estimator code and enables the role together.
4. Assign work with `set_customer_inquiry_estimators`.
5. Confirm with `get_my_inquiry_access_v1` as that user.

Every one of those changes now lands in `inquiry_operator_role_audit` once the
migration is applied.

## 7. Privacy notes

- `pipeline_*` columns are internal workflow state. The two customer-facing reads
  strip them before responding (`withoutPipelineFields`), and no customer screen
  reads them.
- Estimators see only assigned submissions, documents and threads. There is no
  endpoint, parameter or policy that widens that.
- The storage bucket stays private; no policy in this migration touches
  `storage.buckets`.
- The self-access RPC returns the caller's own email only. It is not a directory,
  and the desktop does not need `auth.users` or the Auth Admin API.
