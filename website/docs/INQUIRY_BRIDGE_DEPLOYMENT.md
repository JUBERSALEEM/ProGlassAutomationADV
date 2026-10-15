# Inquiry Bridge Deployment Runbook

The bridge publishes a read-only projection of desktop inquiries and inquiry attachments. It never modifies `inquiries.db`, manifests, or source files. Do not enable `--publish` until `supabase-inquiries.sql` has passed all non-production role/storage tests.

## Prerequisites

- Node.js 22.5+ on the Windows host (the bridge uses the built-in read-only `node:sqlite` API).
- Reliable read access to the authoritative ProGlass data root.
- One dedicated Supabase Auth machine user with `inquiry_bridge` in `inquiry_operator_roles`.
- One `inquiry_sources` row assigning that machine user to one stable `source_dataset_id`.
- The private `inquiry-documents` bucket and policies from `supabase-inquiries.sql`.
- A protected service account environment. Do not put credentials in this repository, manifests, shared data root, command history, or browser settings.

## Required environment

```text
PROGLASS_DATA_ROOT=\\SERVER\ProGlassData
INQUIRY_SOURCE_DATASET_ID=<stable UUID assigned to this data root>
NEXT_PUBLIC_SUPABASE_URL=<project URL>
NEXT_PUBLIC_SUPABASE_ANON_KEY=<publishable/anon key, never service_role>
INQUIRY_BRIDGE_EMAIL=<dedicated machine-user email>
INQUIRY_BRIDGE_PASSWORD=<dedicated machine-user password>
```

Optional:

```text
INQUIRY_BRIDGE_CHECKPOINT=<protected local checkpoint path>
INQUIRY_BRIDGE_MAX_ROWS=25000
```

The checkpoint defaults under `.proglass-web-bridge` in the source root. On a shared root, prefer a protected local checkpoint path writable only by the bridge service identity.

## Safe rollout

1. Back up the target non-production project and inspect the migration.
2. Apply `supabase-inquiries.sql` to the non-production project only.
3. Create role/source rows through the SQL administration channel. Do not add browser-writable role policies.
4. Run dry mode first:

   ```powershell
   npm run inquiries:bridge:dry-run
   ```

   Dry mode reads and validates the complete source but performs no authentication, upload, database mutation, or checkpoint write.

5. Resolve every warning before the first complete publication. Warnings make a publish run `partial` and disable missing/tombstone reconciliation.
6. Run one controlled non-production publication:

   ```powershell
   npm run inquiries:bridge:publish
   ```

7. Verify row counts, hashes, object privacy, events, role access, and that no local paths appear remotely.
8. Re-run unchanged. It must not create duplicate inquiries, documents, versions, or publication events.
9. Rename/change one controlled file and verify immutable version supersession.
10. Remove one controlled file and verify source-missing occurs only after a warning-free complete scan.
11. Revoke the bridge user and prove the next run fails before publication.
12. Only after acceptance, configure Windows Task Scheduler/service under a least-privilege account. Prevent overlapping runs and capture path-free stdout/stderr to protected local logs.

## Failure behavior

- Missing/unreachable root or database: exits before remote contact.
- Demo root: exits before remote contact.
- More than the configured safety limit: exits; no partial publication.
- Corrupt manifest, missing file, invalid file, or unsupported format: emits a warning; prior remote availability is preserved because reconciliation is disabled for that run.
- Upload succeeds but registration fails: immutable key makes retry safe; inspect unreferenced objects during audit.
- Authentication, role, source assignment, stale revision, or RPC error: exits without advancing the checkpoint.
- The bridge never uses a Supabase service-role key.

## Rollback

Disable the scheduled task and set `inquiry_sources.enabled=false`. This immediately prevents the assigned bridge from beginning another run. Existing inquiry data remains private/read-only. Do not delete bucket objects or metadata during incident response; preserve events and hashes for diagnosis.
