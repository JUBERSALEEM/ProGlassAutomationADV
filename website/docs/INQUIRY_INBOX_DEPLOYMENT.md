# Customer inquiry cloud-to-Windows inbox

This Phase 8C/9E bridge is separate from `inquiry-bridge.mjs`. It pulls only accepted website customer inquiries into an isolated protected inbox. Phase 9E adds an immutable active-assignment snapshot and routes each delivery under `assigned/<ESTIMATOR_CODE>/` or `unassigned/`; customer email is never used in a path. It never writes desktop SQLite, the authoritative ProGlass data root, or desktop attachment manifests.

## Security boundary

Use a dedicated Supabase Auth machine account with an enabled `inquiry_bridge` role. Do not reuse a customer, human operator, publisher, or service-role credential. The bridge can plan/download only complete accepted intake records. Acknowledgement occurs only after every byte/hash check and an atomic local directory rename.

## Environment

```text
NEXT_PUBLIC_SUPABASE_URL=<disposable project URL>
NEXT_PUBLIC_SUPABASE_ANON_KEY=<publishable anon key; never service_role>
INQUIRY_INBOX_EMAIL=<dedicated pull-bridge account>
INQUIRY_INBOX_PASSWORD=<secret supplied through protected service environment>
INQUIRY_INBOX_ROOT=C:\ProgramData\ProGlass\CustomerInquiryInbox
INQUIRY_INBOX_MAX_ITEMS=25
```

`INQUIRY_INBOX_ROOT` must be access-controlled for the Windows service identity and must not be the ProGlass data root. Do not place credentials or the inbox in this repository.

## Deployment sequence

1. Review and separately authorize/apply `supabase-inquiry-intake-phase8c-inbox.sql` to disposable Supabase. Deployments that applied its original revision before the inbox event constraint was included must also apply `supabase-inquiry-intake-phase8c-event-repair.sql` once. After Phase 9A/9B is installed, separately authorize/apply `supabase-inquiry-estimator-phase9e-handoff.sql` once to enable manifest v2 and estimator-aware routing.
2. Create a dedicated Auth machine user and assign exactly `inquiry_bridge` through the protected SQL administration channel.
3. Set the environment in a protected PowerShell/service context.
4. Run a genuine remote plan:

   ```powershell
   npm run inquiries:inbox:dry-run
   ```

   It authenticates and calls both planning RPCs, but creates no directories, downloads no bytes, writes no checkpoint, and acknowledges nothing.
5. Inspect references/counts, then run once:

   ```powershell
   npm run inquiries:inbox:apply
   ```
6. Verify each final folder under `assigned/<ESTIMATOR_CODE>/` or `unassigned/` contains `manifest.json`, `READY`, and `documents`. Manifest version 2 contains the stable primary estimator code and complete active primary/collaborator assignment snapshot. Final folders appear only after all files pass expected byte size and SHA-256.
7. Re-run dry-run. Successfully acknowledged items must not be planned again.
8. Verify customer status is `downloaded` and an `inbox_delivered` event exists.
9. Disable the bridge role and prove dry-run fails before revealing a plan.
10. Only after acceptance, schedule with Windows Task Scheduler under a least-privilege service identity and prevent overlapping runs.

## Phase 12: opt-in desktop integrations

Two contracts in `supabase-inquiry-inbox-bridge-phase12.sql`, applied as
migrations `inquiry_inbox_bridge_status_write_back` and
`inquiry_inbox_bridge_chat_pull` on project `afpbdyamedpanwijxghk`. Guarded by
`scripts/inquiry-inbox-bridge.test.mjs` and `tests/inquiry-pipeline.test.tsx`.

Both are inert until the desktop enables its two Inbox settings, "Report status
to website" and "Sync website chat". Neither changes a CHECK constraint, an
existing policy, an inbox RPC, the community room, or anything a customer sees.

| Contract | Function | Grants |
|---|---|---|
| Pipeline status write-back | `update_customer_inquiry_status_v1(uuid, text, text)` | definer, `authenticated` only |
| Read-only chat pull | `list_customer_inquiry_communications_v1(uuid)` | definer, stable, `authenticated` only |

The write-back adds four nullable columns to `inquiry_customer_submissions`
(`pipeline_status`, `pipeline_note`, `pipeline_updated_at`,
`pipeline_updated_by`). It accepts `prepared`, `sent`, `confirmed`, `hold` and
`cancelled`, and nothing else. Each accepted write records one `status_changed`
event with `customer_visible = false`, so the shop's workflow state is audited
without appearing in the customer timeline. The customer-facing `status` column
is never touched by this path.

The chat pull returns the thread for one submission, with the author's display
name resolved through `user_profiles` and a fallback of "Website user". There is
no companion write function, by design: the desktop renders the conversation and
cannot post into it, so no machine identity can speak to a customer.

Both functions gate on `has_inquiry_role(ARRAY['inquiry_bridge'])` inside the
body and raise `42501` otherwise, so the gate holds even for a signed-in caller
who is not the bridge. `EXECUTE` is revoked from `PUBLIC` and `anon`.

Verified on the project: both functions are `SECURITY DEFINER`, `anon` execute is
false and `authenticated` execute is true for both; a simulated bridge claim
pulled the one existing room message for `WEB-2026-000007` and wrote
`prepared` with note "TEST desktop write-back", which was rolled back; an
unsigned call, a disabled bridge account and an inquiry admin all failed with
`42501`.

### Operator display

The internal Customer submissions list and its review screen now report the
workflow state, so a report can answer "where did the shop get to" without
opening the desktop.

- List row: `Desktop: Prepared`, with the full status, note and update time in
  the row's tooltip. Nothing renders for an inquiry the desktop has never
  reported on, so the list is unchanged for every existing row.
- Review screen: a `Desktop pipeline` line with the status, the note and the
  update time, or `Not reported`.

Both read through `src/lib/inquiry-pipeline.ts`, which holds the five statuses,
their labels and the shop-time formatter. The formatter pins `Asia/Dubai`, so the
time reads the same on the shop PC and on a laptop abroad.

The operator queries already select every column, so no query changed. The two
customer-facing reads do strip the four columns before responding
(`withoutPipelineFields`), so internal workflow state cannot reach a customer
browser. That is a tightening of the payload, not a change to any customer
screen.

Display only: no new route, no new filter, no change to the status control, the
review RPC or the operator permissions.

## Phase 13: role matrix and self-access (applied)

`supabase-inquiry-role-matrix-phase13.sql` adds `get_my_inquiry_access_v1`, a
database-scoped operator inbox (`list_customer_inquiry_inbox_v1`), a role audit,
and three approved behaviour changes: the viewer role leaves the intake
surfaces, pipeline write-back and the chat pull widen to admins and assigned
estimators, and the acknowledgement replay is fixed. It is live on project
`afpbdyamedpanwijxghk` as four ordered migrations, the last of which only
restores in-body comments. Read `PHASE13_INQUIRY_ROLE_MATRIX.md` for the matrix,
the post-apply evidence, and the triage for a bridge call that is refused after
a successful sign-in.


## Recovery

`.staging/*.partial` folders are never operator-visible completed deliveries. They are removed on a handled failure, and a later apply removes stale interrupted staging directories before retrying. If the process stops after atomic rename but before acknowledgement, the next apply validates the existing manifest and every delivered file hash before safely acknowledging without downloading duplicate files. A conflicting existing final folder stops the run. Remote acknowledgement is idempotent only for the same manifest hash and document count; conflicts fail closed.

## Rollback

Disable the machine user's `inquiry_bridge` role and stop the scheduled task. Preserve final inbox folders, events, and delivery rows for audit. Do not delete records or Storage objects during incident response.
