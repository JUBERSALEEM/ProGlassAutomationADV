# ProGlass-side synchronization architecture audit

**Status:** pre-implementation static audit and proposal; no synchronization code was changed.

**Scope:** local ProGlass business records → ProGlass sync engine → durable offline state/queue → Supabase/backend → the existing, separate website. The website remains a separate repository/branch; no website files are proposed for this repository.

**Verification boundary:** source-code compatibility only. No live Supabase schema, policies, credentials, network connection, production data, or Vercel deployment was inspected. **LIVE SUPABASE TEST: NOT VERIFIED**

## Executive assessment

Keep and extend the existing `RecordsSyncService` and `SupabaseRecordsClient`; do not start a second unrelated sync subsystem. They already provide an optional, background-only PI publisher, a narrow PI projection, a dedicated-auth-user PostgREST path, source/version-fenced updates, and source-scoped soft deactivation.

It is not yet an offline outbox: `recordssync-state.json` stores hashes and last-pass metadata, not pending payloads, durable per-record retry state, or a publishable complete snapshot. It also updates remote rows one at a time, so a failed pass can leave the website with a partially updated set. Before expanding beyond PI, harden snapshot validation and durable pending state, then agree the backend publication contract with the website owner.

`public.records` is a PI/dashboard contract, not a generic PI/JO/DN table. The website has a separate production-job-order ledger and a delivery-movement ledger, but their semantics and write paths are not equivalent to ProGlass local records. Keep PI on `public.records`; do not place JO or DN payloads there by adding guessed fields. JO/DN publication needs an explicitly approved contract and destination. Tax invoices, statements, reports, PDFs, and attachments should not be included in the default sync.

## 1. Existing PI-only Supabase sync

### What the code does

- `RecordsSyncService` is registered as an optional singleton. `RecordsSyncSettings.Enabled` defaults to false. At startup, `StartIfConfigured()` only reads local configuration/secrets and arms a timer; the first attempt is two minutes later. Normal app startup and local document workflows do not wait for the network. The default interval is 15 minutes (clamped to 5–1,440 minutes). Failures are logged and retried with an in-memory exponential delay capped at 60 minutes. See [`RecordsSyncService.cs`](../Services/RecordsSync/RecordsSyncService.cs#L60) and [`ServiceRegistration.cs`](../Services/DependencyInjection/ServiceRegistration.cs#L105).
- The extractor opens the Proforma Invoice SQLite database read-only, reads non-voided rows, deserializes `FullSnapshotJson`, and maps a compact payload. It does **not** send the whole snapshot. It derives quantity and area from PI specification rows. `TypeOfWork` is currently mapped from `ProjectName`/`ProjectIdentifierName` because the inspected PI model has no dedicated native work-type field. [`ProGlassRecordsSource.cs`](../Services/RecordsSync/ProGlassRecordsSource.cs#L33)
- The payload sent to `public.records` has string business fields: `date`, `company`, `salesman`, `pi_number`, `customer_ref`, `prepared_by`, `type_of_work`, `status`, `qty`, `sqm`, `pi_status`, and `zoho_status`; it also sends `is_active`, `source_file`, and `source_worksheet`. `qty` and `sqm` are formatted strings. `status` and `pi_status` both receive the ProGlass PI status; `zoho_status` is empty. [`RecordsSyncModels.cs`](../Services/RecordsSync/RecordsSyncModels.cs#L51)
- The transport reads a remote snapshot by PI number. Missing records are POSTed; existing records are PATCHed only when the observed PI number, source provenance, and `updated_at` still match. It validates returned keys, handles insert conflicts, then calls `reconcile_records_active_state` with the complete extracted PI-number list. Empty lists are blocked locally as well as by the existing RPC. This is a fenced read/POST/PATCH/reconcile sequence, not a single transaction spanning the whole pass. [`SupabaseRecordsClient.cs`](../Services/RecordsSync/SupabaseRecordsClient.cs#L75)
- If an authenticated email and its vault password are configured, the client obtains a bearer token for that user; otherwise it has a compatibility path that uses the stored API key directly as bearer. The source comments specify no service-role key and no RLS bypass. Secret values are stored through `SecretVault`; URL and non-secret settings are separate. [`SecretVault.cs`](../Services/Security/SecretVault.cs#L1), [`RecordsSyncService.cs`](../Services/RecordsSync/RecordsSyncService.cs#L34)

### Limitations that affect the requested design

1. `RecordsSyncState` is a change-detection ledger: PI number → SHA-256 payload hash, plus last-success time and count. It is not a durable outbox: it has no queued payload, per-record pending/failed state, persistent retry schedule, or attempt/error history. The source is re-extracted on a later pass. [`RecordsSyncModels.cs`](../Services/RecordsSync/RecordsSyncModels.cs#L90)
2. Retry timing is held in memory, so it resets on process restart. State is written with `File.WriteAllText`; read errors fall back to an empty hash map and write errors are logged rather than surfaced as a durable pending-queue fault. Losing this state causes re-sends, not loss of the authoritative local PI, but operators cannot reliably inspect pending records.
3. The extractor can produce an incomplete or weakly mapped snapshot without failing the whole pass. A blank PI number returns no payload; malformed snapshot JSON falls back to indexed columns and zero/blank snapshot-derived fields. Other row exceptions are caught and skipped. If at least one other PI is extracted, reconciliation can then deactivate a skipped PI in that source scope because the active set is built from successfully mapped rows. A full-snapshot validity/completeness gate is needed before any active-state reconciliation.
4. A network fault after one or more successful POST/PATCH calls can leave those remote rows updated even though the pass as a whole fails. The next pass retries from the local database; it is not an atomic last-successful snapshot. `LastSuccessUtc` is pass metadata, not proof that every row was acknowledged.
5. The single `SemaphoreSlim` prevents overlapping work in one service instance only. `AppDataRoot.CurrentFolder` can be a configured shared company data root; it is not necessarily machine-local. Multiple app processes/workstations therefore need an explicit single-publisher policy or cross-process coordination before sharing mutable sync state.
6. There is a path-comment mismatch worth correcting in implementation: `RecordsSyncModels.cs` describes `%LOCALAPPDATA%`, but `RecordsSyncService` builds settings/state paths from `AppDataRoot.CurrentFolder`; the production fallback uses `Environment.SpecialFolder.ApplicationData`, and the current folder may instead be demo/test/shared. Keep credentials in the existing vault and deliberately choose a machine-local or coordinated queue location.

## 2. PI, JO, and DN repositories and identity

The three modules use separate SQLite databases and repository write paths; each entity contains indexed summary columns plus a full JSON snapshot. Local business records must remain the source of truth.

| Type | Local identity and relationship | Duplicate/lifecycle behavior relevant to sync |
|---|---|---|
| PI | `ProformaId`, unique in `ProformaInvoicesDbContext`. `RootProformaId` and `RevisionNumber` describe revision chains. | Insert reallocates a colliding generated PI number; updates carry an optional optimistic `RowVersion`. `IsVoid` is a soft-close flag, separate from `Status`. A PI's revision-chain identity is not currently sent in the website payload. [`SavedProformaInvoice.cs`](../Models/ProformaInvoice/SavedInvoice/SavedProformaInvoice.cs#L27), [`ProformaInvoicesDbContext.cs`](../Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoicesDbContext.cs#L53), [`ProformaInvoiceRepository.cs`](../Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs#L186)
| JO | `JobOrderId`, unique. `SourceProformaId` is indexed but not unique. The repository documentation describes the usual 1-PI-to-1-JO workflow, while the query returns a list; the database does not enforce one JO per PI. | `RowVersion` increments on update; `VoidAsync` retains the document with audit fields, while `DeleteAsync` physically removes it and triggers PI lifecycle repair. [`SavedJobOrder.cs`](../Models/JobOrder/SavedJobOrder/SavedJobOrder.cs#L27), [`JobOrdersDbContext.cs`](../Services/JobOrder/Database/JobOrdersDbContext.cs#L40), [`JobOrderRepository.cs`](../Services/JobOrder/Repository/JobOrderRepository.cs#L85)
| DN | `DeliveryNoteId`, unique; linked to JO and PI; multiple real delivery notes can belong to one JO. Items are normalized under the DN reference. `IsMasterDn` identifies an internal master tracker; delivery type and sequence are separate fields. | `GetAllAsync()` excludes Master DNs but is not, by itself, an external-publication policy. `IsVoid` is audited separately from status; `VoidAsync` retains the row, whereas `DeleteAsync` removes it. A website delivery ledger must preserve void/return/rework semantics and must never treat a Master DN as a dispatch. [`SavedDeliveryNote.cs`](../Models/DeliveryNote/SavedDeliveryNote/SavedDeliveryNote.cs#L37), [`DeliveryNotesDbContext.cs`](../Services/DeliveryNote/Database/DeliveryNotesDbContext.cs#L64), [`DeliveryNoteRepository.Part1.cs`](../Services/DeliveryNote/Repository/DeliveryNoteRepository.Part1.cs#L448), [`DeliveryNoteRepository.Part2.cs`](../Services/DeliveryNote/Repository/DeliveryNoteRepository.Part2.cs#L23)

The repository interfaces expose the same persisted identifiers and list projections. The existing Document Register architecture document explicitly treats its PI/JO/DN view as a read model over those repositories, joined by persisted business identifiers—not as another business-record store. Sync should follow the same boundary: read a compact projection, never write into the register or bypass normal lifecycle/repository logic. [`document-register-architecture.md`](document-register-architecture.md#L1)

## 3. Document Register architecture

The existing Document Register review describes PI, JO, and DN tabs populated from repository list projections and linked by `ProformaId`, `SourceProformaId`, `JobOrderId`, and `ProformaId`. It deliberately avoids a mirrored document database and direct edits to `FullSnapshotJson` entities. That is compatible with an offline-first sync source: reuse or add narrowly scoped read projections on the existing repositories; do not make the register itself a sync store or writer.

The register is not evidence that the website already has a matching three-document backend contract. Its implementation and this sync integration are separate concerns.

## 4. Reports, documents, and attachment storage

- PI, JO, and DN full wizard/document state is held in `FullSnapshotJson`; PI attachments and DN images/PDFs are stored on disk with local manifest/path records. A file path is machine/share-specific, not a downloadable website document.
- Tax Invoice attachments are stored as `byte[]` blobs in SQLite and are also represented in the invoice snapshot; the invoice model includes tax registration, payment terms, financial totals, and bank fields such as account number and IBAN. Do not publish its full entity/snapshot or attachments by default. [`SavedTaxInvoice.cs`](../Models/TaxInvoice/SavedTaxInvoice/SavedTaxInvoice.cs#L1), [`TaxInvoiceAttachment.cs`](../Models/TaxInvoice/Attachment/TaxInvoiceAttachment.cs#L1)
- Statements and saved Order Reports live in their own SQLite databases. Their records contain full report snapshots and filter JSON, and persist a local `PdfPath`. Statement snapshots include invoice rows, balances/aging, TRN/contact fields, and local logo path/base64. These are derived outputs, not canonical PI/JO/DN source records. [`SavedStatement.cs`](../Models/Statement/SavedStatement.cs#L1), [`StatementSnapshot.cs`](../Models/Statement/StatementSnapshot.cs#L29), [`SavedOrderReport.cs`](../Models/OrderReport/SavedOrderReport.cs#L20)
- No inspected ProGlass report/document path uploads files to Supabase Storage. The existing sync payload does not include snapshots, local file paths, PDF bytes, attachment bytes, or dimensions/specification rows.

Therefore the safe default is metadata-only, explicit allowlists. Remote file storage, access control, retention, and signed-document visibility would require a separate approved design; none is implied by this sync audit.

## 5. `public.records` contract

The website's `RecordRow` is a flat PI/dashboard shape: `date`, `salesman`, `company`, `pi_number`, `type_of_work`, `customer_ref`, `qty`, `sqm`, `prepared_by`, `status`, `pi_status`, `zoho_status`, and optional `is_active`, `source_file`, `source_worksheet`, timestamps. The ProGlass payload matches the business fields but does not send server `id`/timestamps. Website queries filter inactive rows, source-scope results, paginate, and use `get_records_dashboard_aggregates`; the ordinary dashboard limit is 50/100/250 rows. These consumers are in `website/src/types/records.ts` and `website/src/lib/records-api.ts` at separate website commit `35c5384a093997ae4d1efb4ed95310d7de6fa473`.

The code-level identity assumption is global `pi_number`. `source_file` and `source_worksheet` are provenance/ownership scope, not a composite identity; ProGlass defaults these labels to `ProGlassAutomation` and `PI`. The checked-in website dashboard SQL adds `is_active`, source labels, timestamps, indexes, and `reconcile_records_active_state`; that RPC soft-deactivates missing PI numbers only within the exact source-file/worksheet scope. The SQL does not define the original `public.records` table. The Windowfilehost `supabase/schema.sql` explicitly labels itself a non-executed template, not deployed-schema evidence. Consequently the live primary/unique constraints, column types, RLS policies, and RPC signature remain unverified.

The current PI mapping is specific and should not be silently generalized: issue date → `date`; salesman from PI snapshot representative; company from indexed PI company with snapshot fallback; ProformaId → `pi_number`; client reference → `customer_ref`; document preparer → `prepared_by`; project name → `type_of_work`; snapshot item totals → string `qty` and `sqm`; local PI status → both `status` and `pi_status`; empty Zoho status. This identifies an existing semantic compromise (`ProjectName` used as “type of work”) and a status-compatibility question; changing either requires an agreed mapping.

## 6. Windowfilehost / ProGlass Excel Sync reliability reference

The reference inspected was Windowfilehost branch `arena/01a10053-windowfilehost`, commit `137835425c74150b764d132395641be740119535`. It is a reliability/design reference, not a request to port its Excel application.

Patterns relevant to ProGlass:

- A durable queue stores payloads, stable keys, operation type, source-run ID, expected remote owner/version, retry count, next attempt, last error, and permanent-failure state. It separately stores the last validated source snapshot and pending active-state reconciliation/history.
- The queue is DPAPI-protected for the current Windows user. Its store validates queue structure, writes a temporary file, then replaces the queue file. This is a durable payload queue, unlike ProGlass's current hash-only ledger. `Services/PendingSyncQueueStore.cs`; `Models/PendingSyncItem.cs`; `Models/PendingSyncQueue.cs`.
- The service persists work before network calls, reconciles queued intent against a freshly read source/remote snapshot before replay, fences conditional updates with the observed owner/version, treats ambiguous inserts as requiring reconciliation, and separates soft removal/reconciliation from successful record writes. Empty/invalid source snapshots are blocked. `Services/SyncService.cs`.
- A separate exclusive file lock coordinates the WPF process and Windows service. A bounded retry worker and network-availability notification resume queued work; permanent failures remain visible rather than being discarded. `Services/CrossProcessSyncLock.cs`; sync tests cover queue persistence/migration.

Adopt these reliability invariants selectively. Do not bring across the workbook reader, monitoring, UI, service installation, or Windowfilehost application architecture.

## 7. Separate website's Supabase consumption model

The website source reviewed is separate branch `arena/01a10ad1-proglassautomationadv`, static commit `35c5384a093997ae4d1efb4ed95310d7de6fa473`.

- The Records/Reports dashboard reads `public.records` through its server/API layer and Supabase client; in production the records route requires an authorization bearer token. The main data contract and aggregation/filter logic remain PI-shaped.
- The website has `public.production_job_orders`, `production_job_order_operations`, and an events table. Its own `release_confirmed_pi_to_production` RPC requires an active PI with exact status `Confirmed`, an operator-reviewed route/stage plan, and creates a separate website-side job-order number/snapshot. The SQL template enables RLS and revokes direct authenticated table writes; the website uses RPCs for release/progress. This is not a direct mirror of `SavedJobOrder` and is not a safe direct-write target for the desktop sync without a separately designed RPC/ownership rule. `website/supabase-job-orders.sql`; `website/src/lib/job-order-source-gate.ts`; `website/src/app/api/job-orders/release/route.ts`.
- The website Deliveries tracker treats `public.records` as the order obligation and `public.delivery_entries` as a movement ledger. The SQL template has one movement row with PI number, optional unique delivery-note reference, sequence/date/time, delivered quantity/SQM, delivery type/status, void metadata, and `source_file`. It has no `source_worksheet` column or ProGlass line-item/detail table. Website balance logic has explicit treatment for drafts, voids, returns, and rework. The API inspected reads data; no ProGlass write route was found. Suggested RLS statements in the checked-in delivery SQL are comments, so actual write permissions/security are not established by that file. `website/supabase-deliveries.sql`; `website/src/lib/deliveries-api.ts`; `website/src/lib/delivery-ledger.ts`.

There is a concrete status mismatch to resolve: the current canonical ProGlass PI statuses are `Draft`, `Sent`, `Approved`, `Rejected`, `Expired`, and `Cancelled` ([`ProformaInvoiceStatus.cs`](../Models/ProformaInvoice/Common/ProformaInvoiceStatus.cs#L14)); the website's `job-order-source-gate.ts` requires exact `Confirmed` for production release and its delivery tracker defaults to `Confirmed` (although a site environment setting can alter the delivery list). Do not map `Approved` or `Sent` to `Confirmed` by assumption. Agree which ProGlass state is publishable and which state satisfies a customer-confirmed order. The live website environment setting and live database policies were not checked.

These findings describe code-level compatibility only; they do not establish that the current deployed site, Supabase project, or Vercel deployment has the same schema/configuration.

## 8. Identity and duplicate rules across local and remote systems

- Locally, PI, JO, DN, Tax Invoice, Statement, and Order Report each have their own document-number namespace and unique index. A stable sync key must include a record type/source namespace for any new multi-type contract; a bare number may collide across types or installations.
- For current `public.records`, preserve its PI-only `pi_number` identity. Existing code intentionally reads globally by PI number and can conditionally reassign provenance when the same PI exists remotely under another source but locally exists in ProGlass. That behavior assumes PI numbers are globally unique and that ProGlass is authorized to become that PI's source owner. Confirm this cross-source ownership rule before additional publishers are introduced.
- PI revisions have `RootProformaId`/`RevisionNumber`, but neither is sent to the current website. Decide whether the website should see every revision as a distinct PI, only the current revision, or an explicit chain; do not infer it.
- The local JO→PI relationship is not a uniqueness constraint. The website production ledger's `(source_pi_number, source_file)` unique rule and independently generated job-order number are a different identity model.
- The website delivery template uses `delivery_note_ref` as a partial unique key when supplied. Any retryable ProGlass delivery import must use a stable DN reference or another reviewed idempotency key; sequence or row order alone is unsafe.
- Tenant/company identity and whether more than one ProGlass data set may publish into the same Supabase project are not established by inspected source. Resolve this before defining new remote keys or takeover behavior.

## 9. Offline queue and publication-state requirements

The existing design already keeps sync off the local save path, which should remain invariant: ProGlass must create, edit, view, report, and save documents with no internet. The website may remain stale while ProGlass is offline; that is preferable to blocking or losing local work.

To satisfy the requested durable pending behavior, the extended engine should retain per-record pending payload/state (or a recoverable validated generation), last acknowledged hash/version, operation, source identity, retry schedule, last error, and remote precondition. Persist before sending; acknowledge only after verified remote results; after restart, rebuild/validate against the local source and never blindly replay stale payloads. Protect payload-bearing local state (DPAPI and atomic replacement are established reference patterns). Keep secrets in `SecretVault`; never place credentials in queue payloads or hardcode them.

Reconciliation/deactivation must only run after a **complete, validated** source snapshot. A missing database, malformed snapshot, blank identifier, duplicate business key, mapping error, or suspiciously empty extraction must block that reconciliation and leave existing remote state untouched. Voids/deletions should be represented as audited soft-deactivation/tombstones where the destination supports them; never use a partial result as evidence that a local record disappeared.

For the literal requirement that the website stays at the **last fully successful snapshot** until a new sync succeeds, the current direct row writes are insufficient. Use a staging/generation model (or one reviewed server-side transaction) and atomically publish a generation only after every row and reconciliation step validates. The separate website reader must continue querying only the published generation. This requires a backend contract/migration and likely a small change in the separate website repository; it cannot be guaranteed by ProGlass-only queue code while the site reads mutable `public.records` directly. If “success” instead means per-record acknowledgement, document that weaker eventual-consistency behavior explicitly before implementation.

## 10. Safe record types for synchronization

| Type | Recommendation | Safe boundary / reason |
|---|---|---|
| PI | **Existing supported candidate; harden first.** | Keep the current flat allowlist and PI-only target. Decide publishable statuses, fix incomplete-snapshot handling, and resolve the website `Confirmed` mismatch. Current extractor includes all non-voided PIs, not just approved/customer-confirmed PIs. Never send full PI snapshots, financial line details, local paths, or attachments. |
| JO | **Potential later phase, not mapped yet.** | Publish only an approved summary if a website-owned contract is agreed. Do not copy the embedded PI snapshot, production details, site contact/phone/address, or write directly into `production_job_orders`; it is a different website workflow. |
| DN | **Potential later phase as movement events, not a generic document row.** | Could be considered for the website delivery ledger only after agreeing quantity/SQM aggregation, status/type mapping, idempotency key, void/return/rework handling, and RLS/write path. Exclude Master DNs and do not upload driver/vehicle/receiver fields, notes, images, signatures, or PDFs by default. |
| Tax Invoice | **Not safe for default sync.** | Fiscal/payment record with TRN, bank account/IBAN, customer/company contact data, balances, and potentially embedded attachments. Requires a separate approved audience, minimal schema, access policy, and retention decision. |
| Account Statement | **Do not sync by default.** | Derived customer-facing financial document: invoice rows, paid/outstanding/overdue balances, identifiers, contact/TRN/logo fields, and local PDF path. |
| Order Report / other saved reports | **Do not sync by default.** | Derived technical/commercial bundles with document references, quantity and production data, filters, totals, and local PDF paths. Use an explicit export product if needed, not the generic record sync. |
| PDFs, images, signatures, attachments, local file paths | **Out of scope.** | No remote file-storage contract or access/retention design was found; paths are not portable and content can contain sensitive business/customer information. |
| Master DN | **Never publish as a real dispatch.** | It is an internal per-JO reference tracker (`IsMasterDn`), not a customer delivery movement. |

## 11. Proposed extension of the existing PI foundation

1. **Keep the existing coordinator and transport.** Retain `RecordsSyncService` as the optional background orchestrator and `SupabaseRecordsClient` as the PostgREST transport. Preserve the no-network local workflow, current configuration/vault approach, source/version fences, and no-service-role posture.
2. **Separate source extraction from destination contracts.** Evolve the PI-only extractor into a typed, read-only ProGlass source that returns validated PI/JO/DN projections from their existing repositories/databases. Use lightweight list/sync projections; only parse `FullSnapshotJson` where the approved payload truly requires derived values. Keep a distinct PI adapter targeting `public.records`. Do not send JO/DN through the PI payload.
3. **Add durable state/outbox before adding more types.** Persist compact pending payloads, typed stable keys, source/local `RowVersion` or fingerprint, schema version, operation, retry state, and observed remote precondition. Use encrypted atomic local persistence and a cross-process lock or an explicit single-publisher policy. Where local saves cannot share a transaction with a central outbox, make authoritative snapshot reconciliation a recovery path so a crash between the business save and enqueue cannot lose the change. Coalesce replaceable snapshot rows; do not coalesce distinct delivery movements.
4. **Make extraction and reconciliation fail closed.** Validate the whole source snapshot (non-empty where required, unique stable keys, parseable required fields, expected counts, no skipped rows) before any remote deactivation. Preserve local business data and pending status for all HTTP/network failures. Keep remote writes idempotent and conditional; retain conflicts for reconciliation rather than blind overwrite.
5. **Meet the last-successful-view requirement explicitly.** Prefer staging each validated source generation and switching a server-side published-generation pointer only after all uploads are acknowledged. Keep the website reading its last published generation until that switch. This is a cross-repository/backend dependency; do not silently claim whole-snapshot atomicity from the current per-row API.
6. **Roll out by risk.** Phase 1: harden PI mapping, status gate, snapshot completeness, durable queue, and publish semantics. Phase 2: JO summary only after a website contract and ownership decision. Phase 3: DN movement sync only after event/quantity/status/security mapping is agreed. Leave Tax Invoices, Statements, Reports, and binary documents excluded unless separately authorized.
7. **Test reliability without live credentials.** Add deterministic tests for restart/replay, duplicate keys, source ownership change, stale remote `updated_at`, partial batch failure, malformed/empty source snapshot, void/tombstone, and “pending remains pending offline.” Use a mock PostgREST transport for code tests. A real Supabase test requires operator-supplied configuration through the existing vault and a verified isolated target; no credentials should be requested or embedded in code.

## Uncertainties and checks intentionally not claimed

- The live Supabase schema, unique constraints, RLS policies/grants, RPC signatures, and actual delivery/job-order tables were not read. The Windowfilehost schema is explicitly a software-side template only.
- The production ProGlass dataset, local status distribution, current settings/vault entries, and whether multiple machines share the same ProGlass data root were not inspected.
- The production website's `PROGLASS_CONFIRMED_STATUSES`, deployed version, and live RLS behavior are unknown. No Vercel/deployment check was attempted; this audit is not deployment verification.
- No build or test suite was run; findings are from static source inspection.

## Source references

### ProGlass worktree

- `Services/RecordsSync/RecordsSyncService.cs`, `ProGlassRecordsSource.cs`, `SupabaseRecordsClient.cs`, `RecordsSyncModels.cs`
- `Services/Security/SecretVault.cs`, `Services/Database/AppDataRoot.cs`, `App.StartupCore.cs`, `Services/DependencyInjection/ServiceRegistration.cs`
- `Services/ProformaInvoice/ProformaInvoiceRepository/`, `Models/ProformaInvoice/SavedInvoice/`
- `Services/JobOrder/Repository/`, `Services/JobOrder/Database/`, `Models/JobOrder/SavedJobOrder/`
- `Services/DeliveryNote/Repository/`, `Services/DeliveryNote/Database/`, `Models/DeliveryNote/SavedDeliveryNote/`
- `Models/TaxInvoice/SavedTaxInvoice/`, `Models/TaxInvoice/Attachment/`, `Services/Statement/`, `Models/Statement/`, `Services/OrderReport/`, `Models/OrderReport/`
- `docs/document-register-architecture.md`

### Separate website snapshot

- Branch `arena/01a10ad1-proglassautomationadv`, commit `35c5384a093997ae4d1efb4ed95310d7de6fa473`
- `website/src/types/records.ts`; `website/src/lib/records-api.ts`; `website/src/lib/source-scope.ts`; `website/src/app/api/records/route.ts`
- `website/supabase-dashboard.sql`; `website/supabase-job-orders.sql`; `website/src/lib/job-order-source-gate.ts`; `website/src/app/api/job-orders/`
- `website/supabase-deliveries.sql`; `website/src/lib/deliveries-api.ts`; `website/src/lib/delivery-ledger.ts`; `website/src/app/api/deliveries/route.ts`

### Windowfilehost reliability reference

- Branch `arena/01a10053-windowfilehost`, commit `137835425c74150b764d132395641be740119535`
- `Services/PendingSyncQueueStore.cs`; `Models/PendingSyncItem.cs`; `Models/PendingSyncQueue.cs`; `Services/CrossProcessSyncLock.cs`; `Services/SyncService.cs`; `tests/ProGlassExcelSync.Tests/PendingSyncQueueStoreTests.cs`
- `supabase/schema.sql` is explicitly a template, not deployed-schema evidence.
