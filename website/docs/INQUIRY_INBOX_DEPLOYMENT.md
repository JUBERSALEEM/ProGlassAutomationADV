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

## Recovery

`.staging/*.partial` folders are never operator-visible completed deliveries. They are removed on a handled failure, and a later apply removes stale interrupted staging directories before retrying. If the process stops after atomic rename but before acknowledgement, the next apply validates the existing manifest and every delivered file hash before safely acknowledging without downloading duplicate files. A conflicting existing final folder stops the run. Remote acknowledgement is idempotent only for the same manifest hash and document count; conflicts fail closed.

## Rollback

Disable the machine user's `inquiry_bridge` role and stop the scheduled task. Preserve final inbox folders, events, and delivery rows for audit. Do not delete records or Storage objects during incident response.
