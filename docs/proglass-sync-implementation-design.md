# ProGlass synchronization — implementation design for approval

**Decision status:** design only. No synchronization code, Supabase schema, or website code was changed for this design. Implementation must wait for your approval.

**Scope:** ProGlass remains the source of truth. Local PI/JO/DN/report workflows must work without internet. The separate website shows only successfully published online data. Document types use separate destination contracts; `public.records` remains the existing PI-shaped contract.

**Verification boundary:** this design uses the source review in [`proglass-sync-architecture-audit.md`](proglass-sync-architecture-audit.md) and the same pinned website/Windowfilehost snapshots. It does not assert what the live Supabase schema, RLS, grants, or website deployment currently are. **LIVE SUPABASE TEST: NOT VERIFIED**

## Recommendation in one paragraph

Keep the existing `RecordsSyncService` as the opt-in background coordinator and extend its PI path rather than creating a second sync platform. Add a small encrypted, durable local outbox and whole-snapshot validation. For Phase 1, stage PI payloads remotely and use one reviewed server-side publish transaction to apply the complete PI snapshot to the existing `public.records` shape. The website continues reading the same published table while records are staged. Add JO and DN later as typed contracts—never by adding them to `public.records`—and use a shared publication generation when the website needs a consistent cross-type view. The backend migration is required for the strict no-half-published-snapshot guarantee; no website repository change is required for the basic PI table shape, but generation-pinned website reads may be needed for stronger page-response consistency.

## 1. Existing components to reuse

| Existing component | Reuse |
|---|---|
| `Services/RecordsSync/RecordsSyncService.cs` | Keep as the background orchestrator, opt-in and dormant by default. Preserve the delayed startup pass, non-blocking local workflows, and bounded retry cadence. Extend it to capture snapshots, persist work, stage, publish, and expose durable status. |
| `Services/RecordsSync/ProGlassRecordsSource.cs` | Keep the read-only PI extraction leg. Add explicit validation/completeness results and source version/fingerprint information. Later, extend with separate read-only JO and DN projections. |
| `Services/RecordsSync/SupabaseRecordsClient.cs` | Keep the HTTPS/PostgREST transport and existing source/owner/version preconditions. Add staged-generation upload and a publish-RPC call. Do not silently fall back to direct row writes in the new atomic mode. |
| `Services/RecordsSync/RecordsSyncModels.cs` | Keep the existing PI payload/settings definitions. Extend this file initially with the compact generation/outbox/status models rather than creating a broad framework. |
| `Services/Security/SecretVault.cs` | Preserve the current DPAPI/vault setup. Never put passwords, API keys, bearer tokens, or other credentials in the outbox. Do not add a service-role secret. |
| `Services/Database/DocumentLifecycle.cs` | Subscribe to its existing `Changed` event as a local change signal. The handler only schedules background queue capture; it must not do network I/O or delay the repository save. The source scan on startup and on the normal timer remains the recovery path if a signal is missed. |
| Existing PI/JO/DN repositories and SQLite databases | Remain the business writers and local source. Use read-only queries/projections and existing `RowVersion` values. Do not make network calls from repository save methods. |
| Website's `public.records` reader | Preserve its existing row shape in Phase 1. The website source reviewed reads this table through its own authenticated/server API path. No website source is moved into this repository. |

Windowfilehost remains a reliability reference only: use the durable encrypted queue, persist-before-send, per-item retry/failure state, reconciliation-before-replay, and cross-process locking patterns; do not port its workbook reader, service, UI, or application.

## 2. Components to modify

### ProGlass Phase 1

1. **`RecordsSyncService`** — replace “extract → direct POST/PATCH → save hashes” with “capture/validate → persist pending generation → stage → publish → acknowledge.” Subscribe/unsubscribe to `DocumentLifecycle.Changed`, debounce bursts, keep a periodic/startup full scan, and surface generation/record counts and last successful publish information.
2. **`ProGlassRecordsSource`** — return a typed snapshot plus validation diagnostics, expected row count, PI IDs, payload hashes, and local `RowVersion` values. It must report skipped/invalid rows; it must not turn a partial read into a valid snapshot.
3. **`SupabaseRecordsClient`** — add idempotent staging and a single publish operation. Preserve the current PI wire payload and ownership/version safeguards. Keep the existing direct path only as a clearly identified legacy implementation during rollout if needed; never auto-fallback from failed atomic publication to direct row writes.
4. **`RecordsSyncModels`** — add generation state (`Queued`, `Staging`, `ReadyToPublish`, `Published`, `Failed`, `Superseded`), item state (`Pending`, `Staged`, `Published`, `Failed`), retry metadata, typed identity/version metadata, and a `RecordsSyncStatusSnapshot` for callers/logging.
5. **`ServiceRegistration.cs`** — register the new queue store if it is injected. The existing startup call in `App.StartupCore.cs` can remain; change it only if shutdown ownership/status exposure requires it.

### Later phases

- **JO:** add a read-only sync projection in the Job Order repository/model only if the existing `JobOrderListRow` does not carry the approved fields, `IsVoid`, and `RowVersion` needed by the contract.
- **DN:** add a read-only projection from real DN headers/items. It must exclude Master DNs and calculate delivered SQM using the same approved ProGlass/reporting arithmetic, not an invented formula.
- **Financial/report documents:** separate projections and contracts only after the Phase 4 field/privacy/retention decision. No snapshot or file upload is part of Phases 1–3.

The normal PI/JO/DN save methods are not to be modified to call Supabase. Event-driven capture is advisory; periodic/startup reconciliation protects the crash gap between a local save and queue capture.

## 3. Genuinely new components

### ProGlass-side

- **`RecordsSyncQueueStore`** — one small durable local outbox/state store. It protects payload-bearing queue data with Windows DPAPI for the current user and replaces files atomically. A path override is injectable for tests. Prefer a machine-local `%LOCALAPPDATA%` location for the mutable queue; do not place a shared mutable outbox beside the SQLite business databases merely because `AppDataRoot.CurrentFolder` may point to a LAN share.
- **A narrow publication transport seam for tests** — inject a fakeable HTTP handler or a small `IRecordsSyncPublisher` interface. This is only to test the existing coordinator; it is not a general connector/plugin framework.

The status snapshot belongs in `RecordsSyncModels.cs`; a new UI framework is not required. The existing app-level service reference and `AppLogService` can expose/report pending, failed, published, and last-success state. If a visible Settings surface is required, decide its location before adding XAML/ViewModel files.

### Supabase/backend

- A private staging area for PI generations and a restricted `publish` RPC. Exact table/function names and SQL file location are deliberately **not** fixed until read-only inspection of the live schema, RLS/grants, unique constraints, and current migration conventions is authorized. The backend implementation is not in the ProGlass repository.

### Not proposed

No generic all-document storage table for the website, no event bus, no service installation, no second scheduler, no distributed lock service, and no generic extensibility framework.

## 4. Local queue/state design

### Local snapshot and durable queue

Each validated source capture creates a generation header containing:

- stable logical `source_id`/scope, document type(s), `generation_id`, capture time;
- expected record count, deterministic snapshot fingerprint, and current published-generation value last observed from the backend;
- queue state, retry count/next attempt, last error/classification, and the last successful published time/ID.

Each queued item contains:

- document type and stable business ID (PI number, later JO ID or DN ID);
- compact type-specific payload and payload schema version;
- local `RowVersion`/content fingerprint captured with the payload;
- operation (`Upsert` or audited soft-deactivation/tombstone), generation ID, enqueue time;
- state, retry count, next attempt, last error, and remote owner/version precondition if one was observed.

The outbox is written **before** network calls. On local change, replace an unsent snapshot item for the same type/key with the newest local version and mark the prior one superseded; preserve separate DN identities as separate movements. Persist each generation so a process restart can resume staging without losing its payload. If local data has advanced since a queued row was captured, re-read and replace it rather than replaying stale data.

For 50 offline edits to 50 distinct records, the pending generation contains all 50 rows. If the process crashes before the change event finishes queue capture, the next startup/periodic authoritative scan reconstructs pending work from the still-local SQLite databases. Local save success never depends on queue I/O or internet.

### Change notification and recovery

`DocumentLifecycle.Changed` is a local “something changed” hint, not a transactionally complete change log. The sync service should debounce it and schedule a background snapshot capture after PI/JO/DN repository operations. It must also do a full validated scan at startup and on the existing periodic timer. The scan is the recovery guarantee; the event is for prompt pending-state visibility.

### Status semantics

- `Pending`: durable locally, not fully staged.
- `Staging`: some/all generation rows have been acknowledged by the backend, but it is not published.
- `ReadyToPublish`: backend count/fingerprint validation succeeded; publish call not yet acknowledged.
- `Published`/`Synced`: the backend confirms the generation was committed.
- `Failed`: permanent/auth/schema/mapping/conflict error; retain data and surface the reason, do not discard it.
- `Superseded`: a newer complete local snapshot replaced an older unsent generation.

`LastSuccessfulSyncUtc`, generation ID, and synced counts advance **only after** the publish RPC is confirmed (or a retry proves that the same idempotency key was already published). A staged/partial generation is always still pending. Do not label a pass successful just because some rows were written.

## 5. Phase 1 PI destination contract

### Keep the existing public shape

Use the current `RecordPayload` fields and no `record_type` expansion of `public.records`:

- `date` ← PI `IssueDate`, formatted `dd-MM-yyyy`;
- `company` ← indexed `CompanyName` with the current snapshot fallback;
- `salesman` ← current snapshot representative mapping;
- `pi_number` ← `ProformaId` (the current global PI identity);
- `customer_ref` ← `Client.CustomerReferenceCode`;
- `prepared_by` ← current document-preparer fallback;
- `type_of_work` ← the existing `ProjectName`/project-identifier mapping (known semantic compromise; do not silently remap);
- `qty`/`sqm` ← current specification sums, invariant string values and existing rounding;
- `status` and `pi_status` ← the exact local PI `Status` string, not a guessed conversion;
- `zoho_status` ← empty as at present;
- `is_active` and `source_file`/`source_worksheet` retain the existing active/provenance meaning (`ProGlassAutomation` / `PI` defaults).

No `FullSnapshotJson`, customer address/contact, bank/TRN data, line-level dimensions, attachments, PDF bytes, or local paths are published.

### Identity, inclusion, lifecycle

- Keep `pi_number` as the PI table key and keep provenance separate. Existing code can conditionally reassign a matching PI from another source; the staged publish must preserve the current observed-owner/version fence and must abort rather than blindly take over a changed row.
- Keep `IsVoid` distinct from business `Status`. The complete publication transaction soft-deactivates only rows missing from the validated exact ProGlass PI source scope; it never physically deletes history.
- Compatibility baseline: preserve the current extractor's `IsVoid = 0` selection and raw status values for Phase 1 unless you approve a narrower publishable-status rule. This means the present code does not distinguish drafts from customer-confirmed PIs. **Approval gate:** confirm whether the desired external PI set includes Draft/Rejected/Expired/Cancelled non-void rows or is restricted to specific statuses. Do not map `Approved` or `Sent` to website `Confirmed` without an approved business rule.
- PI revisions currently have `RootProformaId`/`RevisionNumber` locally but are not in the website payload. Keep the current per-`ProformaId` behavior for Phase 1; confirm later whether website views should show each revision or a revision chain.

### Important live-schema gate

The website type/SQL source reviewed describes the shape and migration helpers but does not establish the deployed base-table DDL or live uniqueness/RLS. Before creating the stage/publish migration, an authorized read-only inspection must confirm the real `public.records` columns, key/unique rule, triggers, active-state behavior, RPC signature, and grants. No schema is inferred from the Windowfilehost template.

## 6. Proposed JO destination contract (Phase 2)

Do not write a ProGlass JO into the website's `production_job_orders` workflow. That table/RPC represents a website operator's release from a confirmed PI with a reviewed production route and separately generated job number; the checked-in contract revokes direct authenticated table writes. It is not a mirror of `SavedJobOrder`.

Proposed separate read-only website projection, with final table/API name pending approval:

- **Stable identity:** `(source_id, job_order_id)`; do not use `SourceProformaId` as the unique key because the local DB indexes it but does not enforce one JO per PI.
- **Required relationship:** preserve local `SourceProformaId` as the PI parent reference. The site must resolve that reference against its published PI projection and show a missing-link warning rather than fabricate a match.
- **Minimum candidate fields:** local JO number, source PI number, local lifecycle status, `IsVoid`/void audit state, `Priority`, target `DeliveryDate`, `UpdatedAt`/`RowVersion`, plus only the company/project summary fields approved for the website.
- **Exclude by default:** embedded immutable PI snapshot, production details, site delivery contact/phone/address, full specs, notes, and attachments.
- Preserve the local status vocabulary; no automatic mapping to website `Released`/`In Production`/`Completed`. Website display of local JO state is read-only and must not create or advance the site's independent production-release workflow.

Backend and website work will be required to add a typed read surface and a website page/API integration. ProGlass Phase 2 should add only a read projection and adapter, not mutate the local JO workflow.

## 7. Proposed DN destination contract (Phase 3)

Treat a DN as a delivery movement/document with lifecycle—not as another row in `public.records`.

- **Stable identity:** `(source_id, delivery_note_id)`; the local `DeliveryNoteId` is unique. A note number alone is not globally safe if multiple ProGlass datasets share one Supabase project.
- **Required links:** preserve `ProformaId` and `JobOrderId` independently. The website's checked-in `delivery_entries` shape has a PI reference but no explicit JO reference or ProGlass line catalog, so it cannot by itself preserve all requested links.
- **Candidate summary fields:** delivery-note number, PI/JO IDs, sequence, delivery date/time, aggregate delivered quantity and SQM, native `DeliveryType` and `Status`, `IsVoid` plus audit timestamp/reason, local update version, and source identity. Do not include driver/vehicle/receiver/contact details or notes without a separate approval.
- Preserve local `Original`, `Dummy`, `Rework`, and `Return` meanings. The website ledger also recognizes `Replacement`; do not equate that with `Rework` without approval. Draft/Dispatching/Cancelled and void states must not count as completed movement unless the agreed web contract says so.
- Exclude `IsMasterDn` rows entirely. A Master DN is an internal balance tracker, not a real delivery. Do not upload photos, signatures, scans, or PDFs in this phase.
- Quantity is available in DN headers/items; delivered SQM must be derived from the normalized DN items with the existing ProGlass `SqmCalculator`/rounding rule and tested against reporting. Do not trust `TotalBalanceQty` as delivered quantity or derive SQM from an undocumented website formula.

The existing `public.delivery_entries` may be a candidate read-model input, but using it for ProGlass writes requires approval of its global unique note-reference behavior, JO link, source scoping, RLS/write path, and the exactly-once semantics for void/return/rework. The default design is a distinct typed ProGlass DN projection that the website can explicitly merge/read after approval—not an automatic mutation of the website ledger.

## 8. Last-successful-snapshot publication

### Phase 1 recommended protocol: stage, validate, commit to the existing PI table

1. Capture a complete PI snapshot locally, validate it, calculate expected count/fingerprint, and durably persist generation B and its rows before any network request.
2. Create an idempotent backend generation ID tied to the logical PI source scope and the last published generation/version observed. Upload allowlisted PI payloads to a **private staging area**, in bounded chunks. Staging writes are not read by the website and do not modify `public.records`.
3. After all staged rows are acknowledged, call one restricted `publish_proglass_pi_generation`-style RPC (name illustrative). The server verifies generation ownership, exact expected row count, fingerprint, unique PI IDs, and the remote-owner/version preconditions. A mismatch aborts publication; it does not publish a subset.
4. In one database transaction, the RPC applies the complete staged set to the existing `public.records` contract and soft-deactivates only records missing from this exact ProGlass source scope. It records the published generation/acknowledgement in the same transaction. Postgres transaction rollback means an error after row 40 leaves the public table at snapshot A; only a committed transaction changes the visible ProGlass set to B.
5. Retry of the same generation ID is idempotent. If the publish response is lost after commit, the client checks the generation manifest and records success rather than replaying a second insert/takeover.
6. The local queue marks B `Published` and advances `LastSuccessfulSyncUtc` only after this confirmation. Old staging data can be cleaned up later under a retention rule; published business rows are not physically deleted.

This is the minimum compatible Phase 1 recommendation because the existing website already reads `public.records`; its row schema need not change. It **does require a Supabase/backend migration and restricted staging/publish RPC**. The live schema and current permissions must be inspected first. If the live table cannot be safely updated in one transaction or other writers prevent safe fencing, fall back to a generation-pointer/view design—not direct per-row publication.

The website's Records API makes separate aggregation and paginated-row requests. An atomic database commit prevents a partially committed PI set, but a request already in flight at the exact commit boundary could combine a pre-commit summary with a post-commit page. If the acceptance criterion also requires one browser response to be pinned entirely to A or B, the website must capture a published-generation token once and use it for all dashboard reads (or use one server RPC returning the response). That is a small **website change**, not something ProGlass can guarantee alone.

### Multi-type publication after Phase 2

When PI, JO, and DN are all enabled, group their validated snapshots under one publication generation. Stage each typed contract separately, then commit all typed destination writes/soft-state changes in one backend transaction, or publish a common generation pointer and expose generation-filtered typed views. Website readers should pin the generation for a multi-type page/session if cross-request consistency is required. This common-generation step is not needed to force the types into one table; the published destinations remain type-specific.

If the final goal means “all ProGlass types in one consistent website view,” this entails backend and website reader work. If it means “each document type independently shows its latest completed sync,” per-type atomic generations could suffice; confirm that product rule before Phase 2.

## 9. PROGLASS-ONLY CHANGES / WEBSITE/BACKEND CHANGES REQUIRED

### PROGLASS-ONLY CHANGES (Phase 1 client work)

- Durable local outbox, snapshot validation/completeness checks, lifecycle-event capture, startup recovery scan, bounded persisted retry, idempotent run IDs, remote-precondition handling, status snapshot/logging, and staged-generation transport calls.
- Preserve current PI payload fields, local business databases, `SecretVault`, and opt-in/background behavior.
- No website files, public table migrations, credentials, or local DB schema changes are made in this repository for Phase 1.
- **These changes alone cannot publish atomically.** Until the backend staging/publish RPC exists and passes contract tests, the new atomic mode must retain work as pending and must not silently fall back to the current direct POST/PATCH path.

### WEBSITE/BACKEND CHANGES REQUIRED

1. **Phase 1:** a Supabase migration for private PI generation/staging metadata and rows; an idempotent publish RPC that validates a complete run and transactionally updates `public.records` plus the exact ProGlass active scope; least-privilege RLS/grants; and a cleanup/retention rule for un-published staging rows. This is a backend change even if the website's record shape remains unchanged.
2. **Phase 1 optional/recommended:** a small Records API change to pin one published generation across summary and page requests if the UI-level consistency test requires it.
3. **Phase 2:** typed JO destination/view and website read/API/page integration; do not route to the existing website production-release RPC.
4. **Phase 3:** approved DN destination/read model and website integration. Reuse the current delivery ledger only if its identity, link, security, and movement semantics are explicitly amended/approved.
5. **Phase 4:** separate financial/report schemas, website permissions/routes, and (only if approved) object storage for files. No automatic document upload.

The website repository is separate and must not be modified automatically. A backend/website change list and separate approval/release are required before such work starts.

## 10. Security model

- Keep the existing local SecretVault/DPAPI approach for API key and dedicated sync-user password. Do not ask for credentials in chat, add them to code, config JSON, tests, logs, or the outbox.
- The atomic write identity is a dedicated authenticated Supabase user under RLS. It receives only the minimum ability to stage rows for the configured ProGlass source and execute the publish RPC. No client-side `service_role`, no RLS bypass, no broad direct UPDATE/DELETE on business tables.
- The publish function must validate source scope and generation ownership, use a fixed safe search path if it is privileged, reject cross-source keys unless an explicit approved reassignment applies, and return only counts/IDs/status needed for acknowledgement.
- Staging rows are not website-readable. The website's normal authenticated read policies remain separate. Inspect actual current RLS/grants before designing exact policies; checked-in SQL templates are not live policy evidence.
- Encrypt payload-bearing local queue data with DPAPI for the current Windows user; log identifiers/run IDs and error codes, not customer payload or secret-bearing request content.
- Keep field allowlists per document type. No whole snapshots, file paths, attachment bytes, or financial fields in the Phase 1 PI contract.

## 11. Cross-process and multiple-PC behavior

- One `RecordsSyncService` singleton and its `SemaphoreSlim` serialize one process only. Add one exclusive local lock around queue load/update/stage orchestration so two ProGlass processes on the same PC cannot corrupt or duplicate queue work.
- Keep mutable queue/state machine-local. `AppDataRoot.CurrentFolder` may be a shared company data root; a queue file and lock in that folder would create cross-PC DPAPI/locking problems.
- For multiple PCs reading the same shared SQLite source, use one stable logical source identity (not machine name/path) and a server-side compare-and-swap on the previously published generation. Two publishers may stage independently; only one can publish from the same base generation. The loser re-reads the source and either recognizes the identical published fingerprint as synced or stages a newer generation. Never let a stale publisher overwrite a newer one.
- If multiple separate company/data-root installations can target one Supabase project, each needs a distinct provisioned source/tenant identity. The current `ProGlassAutomation` PI source label is not proof that separate companies are uniquely namespaced. This must be confirmed before enabling multi-tenant publishing.
- The server generation fence is the cross-PC coordinator; do not build a custom distributed lock service in ProGlass.

## 12. Failure, retry, and restart behavior

| Situation | Required behavior |
|---|---|
| No internet before capture | Local saves continue. Snapshot is validated and queued locally; website remains on last published generation. |
| Internet fails after 40/100 staged rows | Generation stays pending/staging. No publish RPC; `public.records` remains at A. Retry missing/all rows idempotently. |
| Process closes/restarts | Load encrypted outbox; verify local versions; resume the same run if still current, otherwise supersede it with a fresh complete snapshot. Local SQLite remains authoritative. |
| Publish commits but HTTP response is lost | Query by idempotency/generation ID. Treat as published only after backend confirmation; never repeat inserts blindly. |
| Invalid/empty/partial/duplicate local snapshot | Do not stage/publish/deactivate. Retain last successful web generation; mark failed with row diagnostics. An empty source is not a delete-all instruction. |
| Remote owner/version changed | Abort the entire generation before public updates, retain pending work, reconcile/re-capture; no blind takeover. |
| Auth/schema/RLS/permanent field error | Preserve queue; mark `Failed` with actionable safe diagnostic; back off or wait for configuration/schema correction. No retry loop that discards the pending payload. |
| Queue file unreadable/corrupt | Preserve the original file; stop network writes; report “queue unavailable.” Recover by rebuilding only from validated local source after an explicit recovery path. |
| Local database/share unavailable | Do not infer deletion and do not publish/deactivate. Retry local extraction later; main UI follows existing shared-data-root behavior. |

Retry schedule: persist the existing bounded exponential policy (starting at approximately 1 minute, doubling to the current 60-minute cap), retain the normal configured interval, and resume promptly on network restoration if the current platform hook is reliable. Permanent configuration/schema errors stay visibly failed until corrected; they are not removed from the queue. Exact jitter/attempt caps can be finalized in tests without changing the user-facing A/B guarantee.

## 13. Exact test plan

### ProGlass unit/source tests

- PI mapping contract tests for every current field, date culture, quantity/SQM rounding, source labels, raw status preservation, empty Zoho status, and absence of unapproved/sensitive fields.
- Source validation tests: valid complete snapshot; `IsVoid` exclusion; blank/duplicate PI ID; malformed or missing snapshot; row exception; missing DB; empty DB; count mismatch. Assert every invalid/incomplete case blocks publication and deactivation.
- Queue store tests: DPAPI round-trip under current user; atomic replacement; restart/load; retry metadata; 50 distinct offline edits remain 50 pending; newer same-key edit supersedes stale payload; unreadable/corrupt file is retained and sends nothing; no credentials serialized.
- Generation coordinator tests: persist-before-send; ack-after-publish; restart recovery; last-success timestamp only changes after publish; a newer local `RowVersion` prevents replay of an older payload.
- `DocumentLifecycle.Changed` tests: callback is non-blocking/local-only, burst debounced, unsubscribed on dispose; startup/periodic scan recovers if the signal was missed.

### Mocked transport/backend-contract tests

- Staging replay is idempotent by generation ID and PI key; concurrent insert and owner/version conflict never cause blind overwrite.
- Simulate 100 staged rows with network drop after 40. Assert publish was never called and the public snapshot remains A. Restart, retry, stage the remainder, publish once, and assert B is now the entire visible set.
- Simulate publish commit followed by lost response; retry resolves the existing published generation without duplicate or second takeover.
- Assert server count/fingerprint mismatch, duplicate keys, stale source scope, empty generation, and stale base generation reject publication transactionally.
- Verify reconciliation only affects the exact ProGlass source scope and never physically deletes rows.
- Run two local service instances and two simulated publishers. Same-PC lock prevents concurrent queue mutation; backend compare-and-swap allows one publisher and forces the other to refresh/no-op/retry.

### Local workflow/integration tests

- With the transport disabled/offline, create/edit/void/delete local PI through the real repository test DB; assert the repository result succeeds and no HTTP call occurs in the save path.
- Repeat for JO/DN when their phases are approved; assert Master DN never becomes a published movement and void/return/rework semantics remain intact.
- For every failed sync case, assert local source rows and snapshots are unchanged and the website-facing test store still contains the last published generation.
- Keep all these tests mocked/local. A real Supabase/RLS transaction test is a separate post-approval test against an explicitly isolated target after read-only schema review; it is not part of this design and is not claimed as run.

The existing test project is `Tests/ProGlassAutomation.Tests.csproj` (`net8.0-windows`, xUnit). Proposed tests can live under `Tests/RecordsSync/`; no new test framework is required. No build/test execution was done for this design.

## 14. Files expected to change

### ProGlass repository — Phase 1, after approval

**Modify**

- `Services/RecordsSync/RecordsSyncService.cs` — queue-first orchestration, event/debounce/full-scan recovery, status and persisted retry.
- `Services/RecordsSync/ProGlassRecordsSource.cs` — complete PI projection/validation and source version/fingerprint.
- `Services/RecordsSync/SupabaseRecordsClient.cs` — stage + publish RPC transport and idempotent acknowledgement; no silent direct-write fallback.
- `Services/RecordsSync/RecordsSyncModels.cs` — outbox/generation/status contracts while retaining existing `RecordPayload` compatibility.
- `Services/DependencyInjection/ServiceRegistration.cs` — queue store registration.

**Add**

- `Services/RecordsSync/RecordsSyncQueueStore.cs` — encrypted, atomic local pending/state store.
- `Tests/RecordsSync/` — source, queue/restart, mocked transport, publication, and failure tests.

`App.StartupCore.cs` already starts the optional service; it is not expected to change unless shutdown ownership must be corrected. `DocumentLifecycle.cs` already exposes `Changed`; subscribing in the service should avoid editing repository save workflows. No local SQLite schema migration is expected for Phase 1.

### Separate backend/website repository — not modified automatically

- A new Supabase migration for private PI generation staging, publish manifest/RPC, idempotency, scoped inactivation, RLS/grants, and staging cleanup. The exact SQL path/name is TBD after inspecting the separate repository's migration conventions and the live schema.
- Optional Records API generation pinning if one website response must stay on one generation across separate summary/page queries.
- Phase 2 JO and Phase 3 DN typed backend readers/pages only after their field and lifecycle contracts are approved.
- Phase 4 financial/report schemas/storage and website permissions/routes only after that separate review.

No code in the website branch is proposed for ProGlass to edit. Its repository remains separate.

## 15. Files explicitly protected from modification

- The entire separate website repository/tree, including its `website/` source and Supabase SQL. No website code is copied here or edited automatically.
- Existing untracked/reference artifacts: `final-*.png`, `style8-*.png`, `sidebar-top-light-*.png`, `splash-options/`, and `Views/Reports/StatementGenerator/Styles/WizardStyles.xaml.cs`.
- ProGlass local SQLite business schemas and normal PI/JO/DN save/validation/lifecycle paths in Phase 1; no network call is added to them. Later phases may add read-only projections only.
- `Services/Security/SecretVault.cs` and all credentials/configured secret values. No credentials are requested, printed, moved to source control, or hardcoded.
- Tax Invoice/Statement/Report full snapshots and all binary documents until Phase 4 has its own approved data, access, retention, and storage design.

## Approval gates before coding

Please approve or change this design, especially these explicit decisions:

1. **PI eligibility:** preserve the existing all-nonvoid PI selection (including Draft/Rejected/Expired/Cancelled values where present), or require a defined publishable-status allowlist? No status translation is proposed.
2. **Publication consistency:** approve Phase 1’s backend stage + one transaction into `public.records`; confirm whether the website must also pin one generation across separate summary/page API requests.
3. **Publisher identity:** confirm whether one logical ProGlass dataset uses the current `ProGlassAutomation` scope, or whether separate tenant/source IDs must be provisioned before any write.
4. **Backend scope:** authorize a separate read-only schema/policy review and, after approval, a migration/RPC in the website/Supabase repository. No live credentials are required for this design review.
5. **JO/DN direction:** approve separate typed read projections and destination contracts; do not integrate JO with the existing website production-release workflow by default.

Until you approve this design, no sync code, backend SQL, or website code will be changed.
