# Inquiry and Order Document Workflow Programme

**Scope:** `website/` only
**Branch inspected:** `arena/85c84b99-proglassautomationadv`
**Discovery baseline:** commit `324b4f0fc96ebc1a53704734e313f032923909fd`
**Report date:** 2026-10-15
**Status:** Phase 8A customer website intake is implemented and locally verified but its additive migration is unapplied; Phase 8B internal review and Phase 8C pull bridge remain incomplete

This document is both the Phase 1 findings report and the persistent acceptance checklist for the seven-phase programme. It must be updated with evidence and unresolved issues as work proceeds. A phase may be marked complete only after every required acceptance item has passed.

## Executive finding

At the Phase 1 baseline, the website was an authenticated operational **read/report and production-tracking companion** to data synchronized from the desktop/local workflow; it was not an inquiry system. Its operational source is Supabase `public.records`, populated from Excel, with PI number and source workbook used to group confirmed records. A separate website-owned Supabase ledger can release those confirmed PI rows into production job orders and track production stages. Deliveries and exports are also PI-oriented.

The desktop application already contains the authoritative inquiry domain and local document workflow: `INQ-YYYY-NNNN` identifiers, inquiry statuses, customer/project and BOQ data, inquiry-to-PI linkage, downstream lifecycle snapshots, audit/permission checks, and inquiry attachments stored under the configured application data root. None of that inquiry data is exposed to the website by the current Excel/Supabase synchronization contract.

Therefore, the website must **not infer inquiries from PI rows, treat the public demo form as an inquiry database, or independently create a competing inquiry numbering/status system**. Before implementation, Phase 2 must choose and document an explicit bridge/synchronization contract from the desktop/local authoritative workflow to a private website projection. A browser cannot directly enumerate a Windows folder or SMB share.

---

# Phase 1 — Discovery findings

## 1. Repository and runtime state

- Next.js 16 App Router, React 19, TypeScript, Tailwind, and `@supabase/supabase-js` 2.117.2.
- Website routes are localized beneath `src/app/[locale]`; API routes are unprefixed beneath `src/app/api`.
- The production build completed successfully and generated 444 static pages.
- The production server was restored and verified with HTTP 200 responses from `/api/health` and `/en-US`.
- Local HEAD is `324b4f0`, although the branch contains many pre-existing modified files plus untracked `scripts/card-compaction-census.mjs`. Those files predate this programme and were not reset, cleaned, staged, or altered by discovery.
- This report is the only file created for this programme in Phase 1.

## 2. Existing website route and feature surface

### Public and marketing surface

Localized public pages include home, about, features, how-it-works, optimization, pricing, resources, screenshots, worked example, blog, developer, contact, sponsors, collaboration, and legal pages. These explain the desktop product but are not evidence of operational website functionality.

The contact page contains `DemoRequestForm`, posting to `POST /api/demo-request`. The endpoint validates fields, checks same-origin browser requests, uses a honeypot and in-memory rate limit, then sends the message through a configured mail transport. Its own contract explicitly says that it creates no database record and stores no personal data. It is a sales/demo lead form, **not** a business inquiry register.

### Authenticated operational surface

- `/[locale]/records`: paginated/filterable PI-oriented rows synchronized into `public.records`.
- `/[locale]/job-orders`: active `Confirmed` PI rows grouped by PI number and workbook; supports reviewed production release and progress tracking when the production schema exists.
- `/[locale]/deliveries`: delivery balances and movement ledger joined to PI obligations.
- `/[locale]/reports`: operational reporting and export entry points.
- Shared login pages use Supabase passwordless email OTP.
- APIs exist for records, job orders, production release/progress, deliveries, exports, auth sign-out, health, and demo requests.

At discovery there was no inquiry list route, inquiry detail route, inquiry API, attachment API, document download route, storage signing route, or upload route. Phases 3–5 subsequently added the scoped read-only routes/APIs described later; browser upload remains intentionally absent.

## 3. Current website data architecture

### `public.records`

The base table predates the included SQL and is expected to exist already. `supabase-dashboard.sql` adds active/source/timestamp fields and aggregation/reconciliation functions. The TypeScript model is spreadsheet/PI-oriented:

- date, company, PI number, customer reference
- salesman and prepared-by names
- type of work, quantity, square metres
- record, PI, and Zoho status strings
- source workbook and worksheet
- active flag and timestamps

It has no inquiry primary key, inquiry number, customer entity relation, project relation, BOQ relation, document relation, or lifecycle event relation.

Development-only canonical mock records are used when Supabase is absent. Production refuses to silently use those records. Any inquiry Phase 5 integration must likewise use real data and must not repurpose preview records.

### Website production job-order ledger

`supabase-job-orders.sql` creates:

- `production_job_orders`
- `production_job_order_operations`
- `production_job_order_events`

Release is gated on active, confirmed source PI rows and stores an immutable source snapshot. `(source_pi_number, source_file)` is unique and makes release idempotent. Production writes occur through authenticated `SECURITY DEFINER` RPCs with server-side validation and actor IDs. This is a strong pattern to reuse for controlled inquiry/order actions, but it is a **website production release ledger**, not the desktop Job Order database and not an inquiry/order document repository.

### Delivery ledger

`delivery_entries` is joined to PI obligations from `public.records`. It has delivery metadata but no inquiry relationship or general document attachment model.

### Exports

Operations exports serialize existing records, job-order, and delivery report data. They do not export inquiry entities or files.

## 4. Authentication, authorization, and security findings

### Reusable strengths

- Browser OTP authentication creates a Supabase session.
- `useOperatorSession` forwards the access token to operational APIs.
- Production APIs reject requests without a bearer token.
- `supabaseServerWithToken` runs Supabase reads/RPCs in the authenticated caller context and does not expose a service role.
- `supabase.ts` explicitly rejects a service-role credential supplied as the public anon key.
- Production job-order tables have RLS enabled, anonymous access revoked, authenticated read policies, and RPC-only writes.
- Operational API responses use no-store/private caching.

### Blocking security gaps before private documents

The route-level bearer-token check is not sufficient if Supabase table grants or RLS permit the anon role to read directly. The SQL currently leaves RLS for `public.records` and `delivery_entries` commented out, and grants several read/aggregate functions or views to `anon`. Some reconciliation/activation functions are also executable by `anon` and `authenticated` under invoker privileges. Effective risk depends on deployed table privileges/policies, which cannot be proven from this repository alone.

Before inquiry documents are deployed, the project needs a verified authorization model at **both** API and database/storage levels:

- authenticated operators only by default;
- explicit roles/claims for read, create, edit, upload, delete, and lifecycle actions;
- customer/tenant isolation if external customer access is intended;
- private storage bucket, no public object URLs;
- signed or authenticated downloads with short expiry;
- file extension, MIME, magic-byte, size, and path validation;
- collision-safe object keys and immutable integrity metadata (size/hash);
- append-only document/action audit events;
- no absolute Windows paths or local credentials in browser/API responses;
- production verification of deployed RLS, grants, bucket policies, and OTP enrollment controls.

The OTP screen does not itself maintain an application allowlist. Whether arbitrary email sign-up is prevented is a Supabase Auth deployment setting and must be verified rather than assumed.

## 5. Desktop/local workflow discovered (read-only assessment)

The desktop code was inspected only to understand the authoritative workflow; it was not modified.

### Inquiry identity and record

- Stable business number format: `INQ-{year}-{four-digit sequence}`, for example `INQ-2026-0007`.
- SQLite store: `{AppDataRoot.CurrentFolder}/inquiries.db`.
- Unique inquiry number index.
- Core fields: customer/project identity and names, salesman, preparer, priority, value, notes, received/prepared/sent/confirmed dates, update actor/timestamps.
- BOQ line items include build type/specification, colours, dimensions, quantity, expected rate, and notes.
- Optional links to Proforma row/number and Job Order number.

### Inquiry lifecycle

Desktop inquiry status vocabulary is:

`Received → Prepared → Sent → Confirmed`, with controlled `Hold` and `Cancelled` paths.

Permitted transitions are enforced in the service layer rather than only in UI. The broader commercial lifecycle is explicitly:

`Inquiry → Proforma Invoice → Job Order → Delivery Note → Tax Invoice → Statement`

Each downstream entity keeps its own status vocabulary; the desktop computes a coarse lifecycle presentation rather than creating another mutable global status.

### Inquiry-to-order conversion

A PI may be created only from an eligible Prepared or Confirmed inquiry, only when no PI is already linked, and only after BOQ safety validation. Customer/project data and BOQ are carried into the PI workflow. This establishes the inquiry as the upstream business entity and PI as a downstream document, rather than as interchangeable identifiers.

### Local inquiry documents

Inquiry attachments are stored beneath:

`{AppDataRoot.CurrentFolder}/Inquiries/{InquiryId}/Attachments/`

The folder contains copied, GUID-prefixed files and a `manifest.json` metadata source of truth. A legacy SQLite attachment table is only a best-effort mirror. Current accepted extensions are PDF, common images, DWG/DXF, XLS/XLSX, and CSV, subject to a shared maximum-size limit. Desktop actions are permission checked and audited.

This local path must never be sent to the public website. The manifest is local application metadata, not a remotely accessible contract. A future bridge can read it locally under an authorized account and publish sanitized metadata/files to private remote storage, but that bridge does not exist today.

## 6. Capability/gap matrix

| Capability | Exists today | Reusable basis | Gap for requested workflow |
|---|---:|---|---|
| Operator authentication | Yes | OTP session + JWT APIs | Verify enrollment/role restrictions |
| Search/filter/pagination | Yes | Records/job-order/delivery dashboards | No inquiry query/data contract |
| Inquiry identifiers | Desktop only | `INQ-YYYY-NNNN` | Not synchronized to website |
| Inquiry lifecycle | Desktop only | Controlled transitions and lifecycle projection | No website model/API/UI |
| Customer/project data | Partial PI text fields on website | Existing list/detail conventions | No relational inquiry/customer/project source |
| Inquiry-to-PI relation | Desktop only | Stable `InquiryId` on PI | Missing from website records feed |
| Order relation | Partial website PI/workbook release key | Production snapshot pattern | No authoritative desktop JO relationship feed |
| Inquiry attachments | Desktop local only | Manifest + validation pattern | No website metadata/storage/API |
| Order documents | Fragmented desktop attachment systems | Existing desktop conventions | No unified website projection |
| Private file storage | No | Supabase already in stack | Bucket/policies/schema not designed |
| Audit trail | Production events; desktop audit | Append-only event pattern | No inquiry/document audit table |
| Local-folder bridge | No | Desktop/local process is the only viable location | Must be designed, implemented, deployed, tested |
| Empty/loading/error states | Yes in dashboards | Existing components | New workflow still required |
| Responsive/i18n/theme | Yes | Existing tokens and localized routing | New UI must follow them |

## 7. Recommended architecture direction for Phase 2

This is a recommendation, not an implemented design.

1. **Keep the desktop/local workflow authoritative for inquiry creation, numbering, and commercial conversion.** Reuse `INQ-YYYY-NNNN`; do not issue independent website numbers.
2. **Create a website-facing projection in the existing Supabase project**, not a second SaaS/database. Normalize inquiry metadata, lifecycle links, document metadata, and immutable sync identity/version fields.
3. **Store remote document copies in a private Supabase Storage bucket** and store only sanitized object keys plus metadata in database rows. Never store or expose absolute local paths.
4. **Use an authorized local bridge/outbox**, running where the Windows data root/share is available, to publish inquiry snapshots and selected files. The website must not attempt direct SMB/browser access.
5. **Prefer one-way local → cloud publication first.** Website upload or bidirectional sync introduces conflict resolution, malware scanning, deletion semantics, and reconciliation complexity and should not be assumed without explicit approval.
6. **Model relationships by stable source identifiers**: inquiry number plus a durable source installation ID; desktop PI identifier/number; desktop JO identifier/number. Do not derive inquiry identity from company, customer reference, PI number, or filename.
7. **Separate inquiry documents from downstream order documents in metadata and UI**, while presenting one lifecycle detail view. A document should have an owning entity kind/id, category, original filename, object key, size, MIME, hash, source version, uploader/sync actor, and timestamps.
8. **Use RLS and RPC validation modeled after production job-order tracking**, with explicit roles and append-only audit events. Do not copy the looser `records`/deliveries SQL posture into a confidential document feature.
9. **Make sync idempotent and non-destructive** with a source version/fingerprint and unique source identity. Remote deletion should be a tombstone/reconciliation decision, not an accidental consequence of one missing scan.

## 8. Phase 2 approved scope decisions

The user approved these choices on 2026-10-15:

1. **Direction:** read-only website. Desktop/local data remains authoritative; the website cannot create, edit, transition, upload, replace, or delete inquiries or files.
2. **Document scope:** inquiry attachments only. PI, Job Order, Delivery Note, Tax Invoice, Statement, production, and delivery documents are explicitly out of the first release.
3. **Audience:** role-based internal operators only. No customer portal or external sharing.
4. **Foundation:** the existing Supabase project with a private bucket and a least-privilege Windows bridge that can reach the authoritative `AppDataRoot`/shared folder.

These decisions supersede the broader alternatives in the Phase 1 questions. Expanding any of them requires a new design/security review.

## 9. Phase 2 workflow contract

### 9.1 System boundaries and ownership

| Concern | Authoritative owner | Website behavior |
|---|---|---|
| Inquiry ID/number | Desktop `inquiries.db` | Preserve and display; never allocate |
| Inquiry metadata/status | Desktop inquiry services | Read-only projection |
| Inquiry attachment manifest/file | Desktop inquiry attachment folder | Private published copy |
| PI/JO relationship labels | Desktop inquiry row | Optional read-only references, not document sources |
| Web identity/roles | Supabase Auth + approved operator profile | Enforce on API, DB, and Storage |
| Publication state | Windows bridge checkpoint + Supabase sync fields | Show last successful sync/staleness |
| Browser access | Website API | Search/view/download only |

The browser never receives a local path, bridge credential, bucket credential, or unrestricted object URL. The Next.js application does not connect to SQLite or SMB. The bridge does not serve inbound HTTP and does not accept commands from browsers.

### 9.2 Stable identity and relationships

- `source_dataset_id`: random UUID generated once for the authoritative data root and persisted in bridge-owned state beside that root. It identifies the shared dataset, not a workstation, Windows username, path, or license.
- `source_inquiry_id`: desktop integer `InquiryRecord.Id`, scoped by `source_dataset_id`.
- `inquiry_number`: desktop `INQ-YYYY-NNNN`, retained as the human identifier and unique within one source dataset. It is not used alone as a cross-installation primary key.
- Remote inquiry identity: unique `(source_dataset_id, source_inquiry_id)`; additionally require unique `(source_dataset_id, inquiry_number)` to detect source corruption/collisions.
- `source_attachment_id`: manifest attachment `Id`, scoped by inquiry. Because legacy data can be imperfect, a file version also carries SHA-256, byte size, and source modified/upload time.
- Remote attachment identity: unique `(inquiry_id, source_attachment_id, content_sha256)`. Re-publishing unchanged content is an idempotent no-op; changed bytes create a new immutable version rather than overwriting history.
- `ProformaInvoiceId`, `ProformaNumber`, and `JobOrderNumber` may be copied as display/linkage metadata. They do not authorize access and do not cause downstream files to be scanned.

Names, filenames, paths, customer references, and PI numbers are never relationship keys. Absolute source paths are bridge-local only.

### 9.3 Published inquiry projection

The first release publishes the minimum useful read model:

- source identity and inquiry number;
- reference, customer name/location, project name/description;
- salesman/preparer display names;
- desktop status and priority (verbatim normalized vocabulary);
- inquiry value, received/prepared/sent/confirmed/update timestamps;
- PI/JO reference labels where present;
- attachment count and current-version metadata;
- source version/fingerprint, last observed time, last successful publication time, and availability/tombstone state.

BOQ line items, communications, audit database contents, and arbitrary local files are excluded from v1. Notes are excluded by default because free text may contain unnecessary sensitive data; adding them requires an explicit field-level privacy decision.

### 9.4 Lifecycle mapping

The website displays the desktop inquiry status verbatim: `Received`, `Prepared`, `Sent`, `Confirmed`, `Hold`, or `Cancelled`. It may derive a presentation-only state:

- active: Received, Prepared, Sent;
- completed: Confirmed;
- paused: Hold;
- closed: Cancelled.

This derived state is never persisted as a second business status. PI and JO references appear as downstream linkage facts only. The full six-stage desktop lifecycle is out of v1 document scope and must not be simulated from `public.records`.

### 9.5 Document organization

One private bucket is proposed, with opaque keys generated from server-safe IDs:

`inquiry-documents/{source_dataset_id}/{remote_inquiry_uuid}/{remote_document_uuid}/{version_uuid}`

The object key contains no customer name, inquiry number, original filename, Windows path, or email. Database metadata retains the sanitized original filename for display. Each document has:

- owning inquiry UUID;
- category fixed to `inquiry_attachment` in v1;
- source attachment ID and immutable version UUID;
- original filename and lowercase extension;
- detected MIME and client-reported/source extension;
- byte size and SHA-256;
- source upload/modified time and bridge publication time;
- availability (`available`, `superseded`, `source_missing`, `quarantined`);
- object key (never a public URL);
- bridge run ID and source fingerprint.

The UI has an **Inquiry documents** section. It must not show an empty “Order documents” section in v1; instead, PI/JO references are metadata and the scope limitation is documented. This avoids implying that unimplemented order files are synchronized.

### 9.6 Role and permission model

Proposed internal roles:

- `inquiry_viewer`: list/search/view inquiry metadata and request downloads.
- `inquiry_auditor`: viewer rights plus read access to publication/audit history.
- `inquiry_admin`: auditor rights plus operator-role administration and controlled document quarantine/restore operations; still cannot edit desktop inquiry data or upload a replacement in v1.
- bridge principal: machine integration identity restricted to publication RPCs and bucket writes for this workflow; it is not an interactive operator and cannot use general website APIs.

No role receives direct bucket listing. Browser download flow is: authenticated request → API verifies operator and inquiry/document access → API records access event → short-lived signed download response or authenticated stream. Role claims must come from protected server-verifiable profile/app metadata, not browser-editable user metadata.

### 9.7 Bridge deployment and scan rules

- Run exactly one active bridge instance for a `source_dataset_id`, preferably as a Windows service/task on the host that has reliable access to the authoritative shared data root.
- Service account gets read-only access to `inquiries.db`, manifests, and inquiry attachment files; write access only to its own local checkpoint/log directory. It does not modify desktop data or manifests.
- Never publish demo-mode data. Refuse startup if the configured root is ambiguous, fallback-local while a shared root is expected, or the source identity does not match the checkpoint.
- Read SQLite through a read-only connection/consistent snapshot with busy timeout; do not copy a partially written database. Parse manifests defensively.
- Resolve attachment paths locally using the same absolute-then-folder-fallback concept, but publish no path value.
- Ignore symlinks/reparse points and reject resolved files escaping the authorized `Inquiries/{id}/Attachments` tree.
- Perform extension, size, MIME/signature, regular-file, and hash validation before upload. Unsupported or missing files produce safe per-file errors without failing unrelated inquiries.
- Bridge credentials belong in Windows protected secret storage/environment supplied by deployment, never in the repository, data root, manifest, logs, browser bundle, or chat.

### 9.8 Checkpoint, idempotency, and ordering

Each run receives a UUID and follows this order:

1. Acquire a dataset-scoped lease so two bridges cannot publish concurrently.
2. Validate source identity/root and open a consistent read snapshot.
3. Read candidate inquiry rows and manifests; create canonical metadata and SHA-256 fingerprints.
4. Validate and hash each current attachment before any remote metadata marks it available.
5. Upload bytes to a new immutable object key if that hash/version does not already exist.
6. Call one constrained publication RPC to upsert inquiry metadata, attach the uploaded version, append events, and advance the source version atomically.
7. Mark source-missing/superseded items only after a complete successful scan, never after a partial scan, unreadable share, corrupt manifest, or timeout.
8. Save the local high-water/checkpoint only after remote commit.
9. Release the lease and emit a path-free summary.

Retries use the same source fingerprint/content hash and therefore do not duplicate inquiries, document versions, or audit events. Metadata with an older source revision cannot overwrite a newer publication. Clock time alone is not used for conflict resolution.

### 9.9 Deletion, supersession, and retention

V1 is non-destructive:

- Cancelled and historical inquiries remain visible subject to filters; cancellation is a status, not deletion.
- Changed attachment bytes create a new current version and mark the old version `superseded`.
- An attachment absent from one **complete** later source scan becomes `source_missing`; it is hidden from normal document lists but remains in audit/version history.
- An inquiry explicitly deleted locally becomes tombstoned only after a complete reconciliation scan. Metadata and audit events remain; it disappears from the default active list.
- Bridge/API code performs no hard deletion of metadata or objects in v1.
- A production retention duration and irreversible purge procedure remain a governance/deployment policy to approve before enabling any future purge. Until then retention is indefinite, with storage growth monitored.

This deliberately favors recoverability and audit integrity over storage reclamation.

### 9.10 Data flow

```text
Authoritative Windows data root
  inquiries.db + Inquiries/{id}/Attachments/manifest.json + files
       |
       | read-only, consistent scan
       v
Least-privilege Windows bridge
  validate -> hash -> upload immutable bytes -> publish constrained RPC
       |
       | outbound TLS only
       v
Supabase
  private inquiry projection + document metadata/events + private bucket
       ^
       | authenticated JWT, RLS, API authorization
       |
Next.js API <-> authenticated internal operator browser
  list/search/detail              authorized short-lived download
```

### 9.11 Primary sequence: publication

```text
Bridge          Local source          Private storage       Publication RPC
  | lease + validate |                       |                    |
  |----------------->|                       |                    |
  | snapshot rows + manifest/files           |                    |
  |<-----------------|                       |                    |
  | validate/hash    |                       |                    |
  | upload immutable object ---------------->|                    |
  |<---------------- object success ---------|                    |
  | upsert metadata/version/event -------------------------------->|
  |<---------------- committed source version ---------------------|
  | save checkpoint |                       |                    |
```

If object upload succeeds but the RPC fails, retry reuses/detects the immutable object; an orphan-reconciliation job or admin report can identify unreferenced objects. Metadata must never claim `available` before object success.

### 9.12 Primary sequence: download

```text
Browser              Next.js API             Supabase DB/Storage
  | GET document/:id      |                         |
  |---------------------->| verify JWT + role/RLS  |
  |                       |------------------------>|
  |                       | metadata/access result  |
  |                       |<------------------------|
  |                       | append access event     |
  |                       | create short-lived URL  |
  |<----- safe response --|                         |
  | download before expiry ------------------------>|
```

Errors distinguish unauthenticated (401), unauthorized/not exposed (403 or non-enumerating 404), missing/superseded source (409/410 as designed in Phase 3), validation quarantine (423), and temporary storage failure (503), without leaking object keys or source paths.

### 9.13 Failure and recovery matrix

| Failure | Required behavior |
|---|---|
| Shared root unavailable/fallback-local | Stop run; retain prior remote state; alert operator; no tombstones |
| SQLite busy/inconsistent read | Retry boundedly; no checkpoint/tombstones on failure |
| Missing/corrupt manifest | Record inquiry warning; preserve prior document state |
| Missing attachment file | Per-file warning; only mark missing after complete confirmed scan |
| Unsupported/oversized/signature mismatch | Quarantine/reject metadata; never expose bytes |
| Network/storage timeout | Retry with backoff and idempotency; no duplicate version |
| RPC conflict/stale revision | Fetch current revision, do not overwrite; alert for reconciliation |
| Bridge crash after object upload | Retry safely; report/reconcile orphan object |
| Two bridge instances | Dataset lease permits one publisher; loser exits without changes |
| Revoked operator | JWT/profile/RLS check blocks next request; signed URLs expire quickly |
| Browser guesses IDs/object keys | Non-public bucket and RLS/API check deny access |
| Production Supabase migration absent | Website shows unavailable/configuration error, never mock inquiry data |

## 10. Phase 2 unresolved deployment controls

These do not change the approved workflow but must be closed before production acceptance:

1. Confirm the actual authoritative shared/local root and host on which the single bridge will run.
2. Choose the operator role-assignment authority and initial approved users.
3. Approve exact file-size limit and MIME/signature allowlist in Phase 3; desktop extension acceptance alone is insufficient for cloud publication.
4. Approve indefinite v1 retention or define a legal/business retention period before any purge feature exists.
5. Audit deployed Supabase Auth enrollment controls, `public.records`/delivery grants, RLS, and project settings. Repository SQL does not prove production posture.
6. Confirm whether inquiry value and customer location are necessary for all viewer roles; minimize fields if not.

---

# Persistent phase checklist

Legend: `[x]` accepted, `[ ]` pending, `[!]` blocked or failed.

## Phase 1 — Discovery

- [x] Confirm active branch and baseline commit.
- [x] Inspect working tree and preserve unrelated modifications.
- [x] Inventory website pages, APIs, components, libraries, types, SQL, tests, and docs relevant to operational workflows.
- [x] Trace contact/demo submission and prove it is not persisted as an inquiry.
- [x] Trace records, confirmed-PI grouping, production release/progress, delivery, and export architecture.
- [x] Inspect authentication/session/token forwarding and Supabase client boundaries.
- [x] Search for inquiry, attachment, document, storage, upload, quotation, order, customer, and local-path functionality.
- [x] Inspect the desktop inquiry/lifecycle/attachment contracts read-only to identify the existing authoritative workflow.
- [x] Record current capabilities, gaps, security concerns, and decisions required.
- [x] Create this persistent findings/checklist document.

**Acceptance evidence**

- `git branch --show-current` → `arena/85c84b99-proglassautomationadv`.
- `git rev-parse --short HEAD` → `324b4f0`.
- `npm ci --no-audit --no-fund` → 600 packages installed successfully.
- `npm run build` → successful Next.js production build; 444 static pages generated.
- Production server checks: `/api/health` → HTTP 200; `/en-US` → HTTP 200.
- Route inventory confirms no inquiry page/API and no upload/download/storage route.
- Repository search confirms no website inquiry/document schema or Supabase Storage integration.
- Desktop read-only inspection confirms `InquiryRecord`, `InquiryLifecycle`, `InquiryWorkflowService`, `InquiryRepository`, `InquiryAttachmentService`, and inquiry-to-PI integration.

**Outstanding issues after Phase 1**

- Product/operational decisions listed in section 8 remain unanswered.
- Production Supabase policies/grants and Auth enrollment settings have not been remotely audited.
- No real inquiry data is currently available to the website.
- No local bridge, cloud document store, or synchronization contract exists.

**Phase 1 result:** ACCEPTED for discovery/reporting. No workflow implementation was performed.

## Phase 2 — Workflow design

- [x] Confirm system of record and one-way/bidirectional scope.
- [x] Approve identifier and relationship contract without duplicate numbering.
- [x] Define inquiry and downstream document ownership/categories.
- [x] Define lifecycle display mapping without a duplicate mutable status system.
- [x] Define local bridge deployment, identity, checkpoint, retry, conflict, tombstone, and offline behavior.
- [x] Define retention, supersession, and deletion semantics.
- [x] Produce reviewed sequence/data-flow diagrams and failure cases.
- [x] Record stakeholder decisions and unresolved risks.

**Acceptance evidence**

- User selected read-only local-to-website publication.
- User limited v1 document scope to inquiry attachments.
- User selected role-based internal access with no external customer portal.
- User approved the existing Supabase project plus a least-privilege Windows bridge as the design foundation.
- Sections 9.1–9.13 define ownership, stable source identity, relationships, minimal projection, lifecycle mapping, opaque private object organization, roles, bridge rules, transactional ordering, retries, tombstones, retention, data/sequence flows, and failure recovery.
- Design preserves desktop `INQ-YYYY-NNNN`, SQLite inquiry status, and local manifests as authoritative; the website has no mutation path.
- V1 hard deletion is prohibited. Source-missing/deleted data is hidden/tombstoned only after a complete scan and retained for audit/recovery.

**Outstanding deployment controls:** section 10. These are Phase 3/deployment inputs and do not alter the accepted workflow boundary.

**Phase 2 result:** ACCEPTED for architecture/workflow design. No schema, bucket, bridge, API, or UI implementation was performed.

## Phase 3 — Data and security

- [x] Prepare additive, reviewable schema migration; do not apply to production without approval.
- [x] Define private storage bucket and object-key convention.
- [!] Implement and test RLS/table grants/storage policies for every role.
- [x] Implement server-side authorization and role checks in the migration/RPC boundary.
- [x] Implement file extension, MIME, signature, size, filename, and integrity validation.
- [x] Implement idempotency, uniqueness, foreign keys, and source-version checks.
- [x] Implement append-only audit events and safe error handling foundations.
- [x] Prove new code contains no Windows paths, service-role keys, or public object URLs.

**Implemented artifacts**

- `supabase-inquiries.sql`: additive draft schema for protected operator roles, source assignments, sync runs, inquiry projections, document/version metadata, and append-only events. It enables RLS on every inquiry table, revokes anonymous/direct mutation access, creates role-specific read policies, provides bridge-only monotonic publication RPCs, and defines a private immutable 10 MB Storage bucket policy. It is not automatically executed.
- `src/lib/inquiry-documents.ts`: server/bridge-side publication policy with safe filename handling, a narrower cloud allowlist, MIME/signature checks, mandatory deep parsing signals for XLSX/CSV, 10 MB limit, lowercase SHA-256 validation, and opaque UUID object-key construction.
- `scripts/inquiry-document-security.test.mjs`: behavioral validation tests for accepted/rejected documents, spoofing, traversal/device names, deep parsing, hashes, and opaque keys.
- `scripts/inquiry-schema-security.test.mjs`: static migration security gates for RLS, grants, bridge/source checks, bucket privacy/immutability, integrity constraints, and service-role/public-bucket absence.
- `package.json`: adds `npm run test:inquiry-security`.

**Acceptance evidence (2026-10-15)**

- `npm run test:inquiry-security` → 16/16 tests passed after correcting one initially incorrect test fixture hash.
- `npm run typecheck` → passed.
- Targeted ESLint for the new TypeScript/test files → passed.
- `npm run build` → passed; 444 static pages generated.
- `git diff --check` for Phase 3 artifacts → passed.
- Environment probe → Supabase URL/anon key not configured; Supabase CLI/`psql` unavailable. No remote schema was changed.

**Blocking verification still required**

- Apply the draft migration only to a disposable/non-production Supabase project after SQL review.
- Exercise real database/storage policy tests for anonymous, viewer, auditor, admin, assigned bridge, wrong bridge, disabled bridge, stale revision, and direct-object attempts.
- Inspect deployed Auth enrollment settings and existing project grants/RLS.
- Confirm initial operator UUIDs, bridge auth UUID, source dataset UUID, and field-minimization choices.
- Phase 5 must add and test complete-scan missing/tombstone reconciliation; Phase 3 intentionally does not mark source data missing without that proven complete-set protocol.

**Phase 3 result:** PARTIALLY ACCEPTED. The additive data/security implementation and local gates are complete, but Phase 3 cannot be marked fully accepted until real non-production Supabase RLS/Storage verification passes. Nothing was applied to production.

## Phase 4 — UI

- [x] Add searchable/filterable/paginated inquiry/order list using existing dashboard conventions.
- [x] Add inquiry detail route/view with lifecycle and metadata.
- [x] Clearly separate inquiry documents from downstream order documents.
- [x] Add loading, empty, partial, permission-denied, missing-file, and retry states.
- [x] Preserve responsive mobile/table/desktop behavior.
- [x] Meet keyboard, focus, labels, contrast, and reduced-motion requirements applicable to these non-dialog views.
- [x] Use existing light/dark/theme tokens, navigation, and i18n routing conventions.
- [x] Avoid redundant dashboard cards/navigation and unrelated redesign.

**Implemented artifacts**

- `src/app/[locale]/inquiries/page.tsx`: localized, metadata-aware inquiry register route.
- `src/app/[locale]/inquiries/[id]/page.tsx`: localized inquiry detail route using opaque remote UUIDs.
- `src/components/inquiries/InquiriesDashboard.tsx`: authenticated read-only register with URL-backed search, status/priority/document filters, concise summary, pagination, desktop table, mobile cards, loading/empty/error/forbidden/configuration states, refresh, and sign-out.
- `src/components/inquiries/InquiryDetail.tsx`: metadata/lifecycle view, clearly scoped Inquiry documents section, unavailable/quarantined states, integrity/size metadata, protected download action, and explicit notice that downstream order files are outside v1.
- `src/components/inquiries/InquiryAccessGate.tsx`: role-aware sign-in explanation and safe return path.
- `src/components/operations/OperationsTabs.tsx`: adds Inquiry as the real upstream step in the existing operational document chain rather than creating separate navigation.
- `src/types/inquiries.ts`: typed API/UI contracts with no mock records.

**Acceptance evidence (2026-10-15)**

- `npm run typecheck` → passed.
- Targeted ESLint over all new inquiry UI/routes/types → passed after removing synchronous state updates from effects.
- `git diff --check` over the Phase 4 UI → passed.
- `npm run build` → passed; route count increased from 444 to 459 generated static pages, including all 15 localized `/[locale]/inquiries` routes and dynamic `/[locale]/inquiries/[id]`.
- Restarted production preview on port 3000 (`website-2eb67b87`).
- HTTP smoke checks: `/en-US/inquiries` → 200; opaque UUID detail route → 200; `/api/health` → 200.
- The UI imports no preview inquiry dataset. Until Phase 5 APIs exist, it reports an honest service-unavailable/configuration error rather than displaying mock content.

**Phase 4 result:** ACCEPTED for UI implementation. Live records/download behavior remains intentionally pending Phase 5 integration and Phase 3 non-production policy verification.

## Phase 5 — Integration

- [!] Connect UI to authenticated real inquiry data.
- [!] Connect private download/access flow to real storage.
- [x] Confirm website upload is out of v1 scope; no upload UI/API was added.
- [x] Implement and exercise the read-only local bridge against a controlled SQLite/manifest/file fixture.
- [!] Verify remote idempotent retries, updates, supersession, missing files, and partial failures.
- [x] Confirm no mock data substitutes for integration.

**Implemented artifacts**

- `src/lib/inquiries-api.ts`: validates JWTs with Supabase Auth, requires enabled viewer/auditor/admin database roles, performs RLS-scoped list/detail queries, sanitizes search input, maps only approved projection fields, resolves audited document access, downloads through private Storage with the operator JWT, and verifies byte size plus SHA-256 before returning bytes.
- `src/app/api/inquiries/route.ts`: authenticated list/search/filter/pagination endpoint with no-store/no-index responses and explicit 401/403/503 behavior.
- `src/app/api/inquiries/[id]/route.ts`: authenticated opaque-UUID detail endpoint.
- `src/app/api/inquiries/[id]/documents/[documentId]/route.ts`: authenticated same-inquiry document endpoint with safe RFC 5987 filename handling, no-sniff/sandbox/no-store headers, and no object-key disclosure.
- `scripts/inquiry-bridge.mjs`: dry-run-by-default Windows publisher using read-only `node:sqlite`, complete-scan safety limits, local-tree/reparse checks, manifest parsing, cloud file validation, deterministic object/version UUIDs, immutable uploads, constrained RPC publication, warning-gated reconciliation, and protected checkpointing. `--publish` is explicit.
- `supabase-inquiries.sql`: adds document-access authorization/audit RPC plus warning-free complete-set reconciliation for inquiry tombstones and source-missing documents. Partial/warning runs preserve prior availability.
- `docs/INQUIRY_BRIDGE_DEPLOYMENT.md`: least-privilege setup, dry-run, non-production rollout, failure behavior, verification, scheduling, and rollback guidance.
- `package.json`: adds dry-run and explicit publish bridge commands.

**Evidence (2026-10-15)**

- Controlled bridge fixture: one real temporary SQLite inquiry row, one on-disk manifest, and one PDF were read through the production bridge code; dry run reported `inquiries: 1`, `documents: 1`, `warnings: 0`. The fixture was outside Git and was not used by the website UI.
- `npm run test:inquiry-security` → 16/16 passed.
- `npm run typecheck` → passed.
- Targeted ESLint for inquiry APIs, data layer, and bridge → passed.
- `git diff --check` for Phase 5 artifacts → passed.
- `npm run build` → passed; generated API routes include `/api/inquiries`, `/api/inquiries/[id]`, and `/api/inquiries/[id]/documents/[documentId]`.
- Production preview rebuilt and restarted as `website-36897ac8`; unauthenticated list, detail, and document API requests returned 401 in smoke checks; `/en-US/inquiries` returned 200.

**Blocking live verification**

The UI and APIs are connected to the real Supabase schema/storage contract and contain no mock fallback, but no Supabase environment/project is configured in this workspace and the Phase 3 migration has not been applied. Therefore no real authenticated inquiry row, private object, publication RPC, RLS role matrix, or end-to-end download could be exercised. Remote retries, supersession, reconciliation, and revocation remain unverified.

**Phase 5 result:** PARTIALLY ACCEPTED. Integration code and bridge dry-run are implemented, but Phase 5 cannot be marked complete until the migration is verified in non-production, controlled real data is published, and an authorized operator completes list → detail → audited integrity-checked download end to end.

## Phase 6 — Testing

- [x] Unit/static-test identifiers, mappings, validation, authorization, object keys, and warning-gated sync reconciliation.
- [!] Integration-test list/detail/search/filter/pagination against a real non-production Supabase schema.
- [!] Test authorized and unauthorized document access, including direct object/API attempts; unauthenticated API paths are verified, live roles/storage remain blocked.
- [!] Test real inquiry-to-PI/JO references and lifecycle projection; UI/type mappings are implemented but no published live row exists here.
- [x] Test empty, oversized, unsupported, MIME/signature-spoofed, unsafe-name, path-escape, malformed-hash, missing, and container/deep-validation cases locally.
- [!] Test remote network interruption, idempotent retry, concurrency, stale source versions, supersession, and reconciliation against non-production Supabase.
- [x] Run lint, typecheck, production build, existing tests, and new tests.
- [!] Complete browser-based responsive widths, keyboard/focus, screen-reader, light/dark, and representative-locale audit with authenticated real data.

**New automated tests**

- `scripts/inquiry-api-security.test.mjs`: UUID boundary, Auth user verification, enabled database-role checks, same-inquiry document ownership, audited RPC authorization, byte/hash integrity checks, no public/signed URL path, and hardened download headers.
- `scripts/inquiry-bridge.test.mjs`: creates disposable real SQLite databases/manifests/files and verifies successful dry scan, unsupported-file warning, external-path rejection, stable source UUID requirement, and demo-root rejection.
- Expanded `scripts/inquiry-schema-security.test.mjs`: warning-gated complete-set reconciliation and audited/role-gated download authorization.

**Evidence (2026-10-15)**

- `npm run test:inquiry-security` → 26/26 passed.
- `npm run test:job-orders` → 8/8 passed.
- `npx vitest run` → 8 test files, 98/98 tests passed. Reduced-motion informational warnings only.
- `npm run test:clay` → 33/33 tests passed; Clay coverage reported 100%.
- `npm run testcrystal` → 114/114 tests passed.
- `npm run lint` → passed across `src` and `scripts`.
- `npm run typecheck` → passed.
- `npm run build` → passed; 459 static pages and all three inquiry APIs generated.
- `git diff --check -- website` → passed.
- Production preview rebuilt/restarted as `website-bee84a4f`.
- HTTP smoke matrix: `/en-US/inquiries` 200; `/api/health` 200; unauthenticated list/detail/download 401; fake bearer while Supabase is absent 503 `NOT_CONFIGURED` rather than fallback/mock data.

**Failures found and corrected during Phase 6**

1. The API security test initially imported a path-aliased server module under Node strip-only mode; it was changed to inspect the compiled source contract without bypassing application module resolution.
2. The first bridge external-path assertion incorrectly expected filename fallback to a differently named local file. The fixture expectation was corrected; the bridge consistently rejected the outside path.
3. The `InquiryApiFailure` TypeScript parameter property was converted to standard property assignment, improving compatibility with Node's strip-only test runner.

**Blocking live test matrix**

No Supabase project/environment is configured and the migration remains unapplied. Viewer/auditor/admin/assigned-bridge/wrong-bridge/disabled-bridge RLS, private object access, live search/pagination, audited download, idempotent publication, immutable supersession, warning-free tombstone reconciliation, revocation, and real authenticated responsive UI cannot be truthfully marked passed.

**Phase 6 result:** PARTIALLY ACCEPTED. All repository-local quality, regression, bridge-fixture, security-static, build, and unauthenticated HTTP gates pass. Phase 6 remains open until the documented non-production Supabase and authenticated browser matrix is executed.

## Phase 7 — Final audit

- [x] Review every inquiry-workflow file and prove scope is limited to `website/`.
- [x] Confirm unrelated pre-existing card-compaction work remains intact and unstaged.
- [x] Re-run complete local quality/build/test/security route matrix.
- [!] Audit deployed database/storage/Auth configuration against repository intent.
- [x] Document deployment steps, bridge operations, rollback, failure behavior, monitoring needs, and limitations.
- [x] Report changed files/reasons, implemented and verified features, exact test results, security considerations, remaining work, and next phase.

### Final changed-file audit

**Inquiry UI and routes**

- `src/app/[locale]/inquiries/page.tsx` — localized inquiry register route and metadata.
- `src/app/[locale]/inquiries/[id]/page.tsx` — localized opaque-ID detail route.
- `src/components/inquiries/InquiriesDashboard.tsx` — authenticated responsive list, filters, URL state, pagination, and honest states.
- `src/components/inquiries/InquiryDetail.tsx` — read-only metadata/lifecycle and private inquiry-document presentation/download flow.
- `src/components/inquiries/InquiryAccessGate.tsx` — restricted-access sign-in state.
- `src/components/operations/OperationsTabs.tsx` — adds the real Inquiry step to the existing document chain.
- `src/types/inquiries.ts` — UI/API contracts.

**Real-data API and security boundary**

- `src/app/api/inquiries/route.ts` — authenticated list endpoint.
- `src/app/api/inquiries/[id]/route.ts` — authenticated detail endpoint.
- `src/app/api/inquiries/[id]/documents/[documentId]/route.ts` — hardened private download endpoint.
- `src/lib/inquiries-api.ts` — Auth user/role verification, RLS queries, ownership checks, audited access, Storage fetch, and integrity verification.
- `src/lib/inquiry-documents.ts` — cloud file policy, signatures, hashes, and opaque keys.
- `supabase-inquiries.sql` — unapplied additive schema/RLS/Storage/RPC draft.

**Bridge, tests, and operations**

- `scripts/inquiry-bridge.mjs` — dry-run-first read-only Windows publisher.
- `scripts/inquiry-bridge.test.mjs` — disposable SQLite/manifest/file bridge tests.
- `scripts/inquiry-api-security.test.mjs` — API security contract tests.
- `scripts/inquiry-document-security.test.mjs` — document validation tests.
- `scripts/inquiry-schema-security.test.mjs` — schema/policy static gates.
- `docs/INQUIRY_BRIDGE_DEPLOYMENT.md` — deployment and rollback runbook.
- `docs/INQUIRY_ORDER_DOCUMENT_WORKFLOW.md` — discovery, decisions, checklist, evidence, and final audit.
- `package.json` — three inquiry commands only: security tests, bridge dry-run, and explicit bridge publish. The separate pre-existing `test:density` work was preserved and is not inquiry work.

### Final local verification evidence

- Inquiry security/API/schema/bridge tests: 26/26 passed.
- Existing Job Order production tests: 8/8 passed.
- Vitest: 8 files, 98/98 passed.
- Clay: 33/33 passed and 100% coverage report.
- Crystal: 114/114 passed.
- Full ESLint: passed.
- TypeScript: passed.
- Production Next.js build: passed, 459 pages.
- `git diff --check -- website`: passed.
- Scope check: no modified/untracked path outside `website/`.
- Secret/path scan: no credential value, private key, service-role use, local absolute Windows path, public object URL, or signed URL implementation found. Documentation contains placeholders and explicit prohibitions only.
- No inquiry mock/preview generator or fallback is referenced by the UI/API.
- Current production preview: `website-bee84a4f` on port 3000.
- Response audit: inquiry page 200 with site CSP/security headers; unauthenticated inquiry API 401 with `WWW-Authenticate`, `no-store`, and `noindex`.

### Final security/deployment assessment

The repository implementation is intentionally fail-closed: no bearer token returns 401; missing Supabase configuration returns a truthful 503; database-owned roles and RLS are required; the bucket is private; direct browser upload is absent; document object keys are opaque; bytes are size/hash checked before delivery; and warning runs cannot reconcile away prior data.

It is **not production-ready** until `supabase-inquiries.sql` is reviewed and applied to an approved non-production project, then tested with actual Auth users, RLS, Storage, RPCs, bridge identity/source assignment, and controlled inquiry documents. The SQL has static checks and build coverage but has not been parsed/executed by PostgreSQL in this workspace. Production migration remains expressly unapproved.

### Remaining work / release gates

1. Provision an approved disposable/non-production Supabase target.
2. Review and execute the migration there; correct any PostgreSQL/PostgREST/Storage-policy issues found by actual execution.
3. Create controlled viewer, auditor, admin, assigned bridge, wrong bridge, disabled bridge, and revoked identities.
4. Execute the full role/object matrix and retain results.
5. Publish controlled real inquiry data twice; prove idempotency, then changed-file supersession, warning preservation, missing-file reconciliation, and tombstoning.
6. Complete authenticated list/search/filter/pagination/detail/audited-download browser tests at responsive widths and representative locales/themes.
7. Decide initial production users, source dataset/host, minimized field set, retention duration, monitoring/alerting, backup, and incident owner.
8. Obtain explicit production migration/deployment approval. Do not run bridge publish against production before that approval.

**Phase 7 result:** PARTIALLY ACCEPTED. The repository-level final audit is complete and all available local gates pass. The programme cannot receive final production acceptance while Phase 3, Phase 5, and Phase 6 live non-production gates remain blocked.

## Phase 8 — Website-originated customer inquiry workflow

The user corrected the earlier Phase 8 premise: website customers, not the WPF application, originate this workflow. The existing desktop-publication projection remains separate and unchanged. Customer access uses Supabase Auth; v1 accepts PDF, bounded images, XLSX, and CSV; and the later Windows bridge will pull accepted submissions into an isolated inbox without writing desktop SQLite.

### Phase 8A — Website customer inquiry

- [x] Inspect actual auth, Supabase clients, inquiry APIs/UI, schema, Storage policy, and validation helpers.
- [x] Add separate customer submission/document/event TypeScript contracts.
- [x] Add authenticated customer submission UI at `/[locale]/inquiries/submit`, including attention, VAT/TRN, salesman reference, and handover estimator fields.
- [x] Add responsive cumulative drag/drop/file selection with 15-slot count, selected-file cards, per-file replace/remove, remove-all, duplicate-name rejection, size feedback, and upload/verification progress copy.
- [x] Correct Next.js 16 proxy multipart truncation so the authenticated route receives the bounded 15 × 30 MB request; live-submit 13 documents and verify all 13 records and protected downloads.
- [ ] Replace the single multipart request with staged one-file requests that expose genuine per-file/overall progress, transfer rate, estimated remaining time, accessible states, interruption handling, safe retry, and explicit finalization.
- [ ] Add customer-owned saved profile defaults (company, contact, phone, VAT/TRN, and attention) with form prefill.
- [ ] Add customer-owned projects, inquiry type/category, project association, and project/location/type/status/date history grouping and filters.
- [x] Add customer-owned submission history and `/[locale]/inquiries/submissions/[id]` detail UI with card hierarchy and protected back navigation.
- [x] Add bearer-authenticated multipart create/list/detail APIs under `/api/inquiry-submissions`.
- [x] Enforce 15 files maximum and 30 MB per file for customer intake while preserving the desktop-publication 10 MB policy.
- [x] Validate normalized filenames, extension allowlist, detected MIME/signature, SHA-256, UTF-8 CSV, and non-macro XLSX structure server-side before persistence.
- [x] Add additive, unapplied `supabase-inquiry-intake.sql` with separate tables, ownership RLS, constrained RPC writes, private bounded bucket, immutable objects, and audit events.
- [x] Preserve the existing `public.inquiries` desktop projection, APIs, UI, bucket, and bridge.
- [x] Add intake schema/API static security tests and include them in `test:inquiry-security`.
- [x] Execute the base and handoff-field migrations in disposable Supabase; verify objects, customer ownership isolation, private upload/download, audit events, and duplicate-record protection.
- [x] Browser-test OTP session reuse, submission, PDF upload, history, detail, handoff fields, download integrity, file-selection feedback, and responsive card presentation against the disposable project.
- [x] Improve keyboard/screen-reader labels, focus rings, caret visibility, dark-mode input contrast, line height, and one-column mobile field flow.
- [ ] Complete the remaining negative/file-format matrix: over-30-MB, 16-file, spoofed signatures, malformed/macro XLSX, invalid UTF-8 CSV, unsupported formats, direct-object denial, screen-reader announcement testing, and representative physical mobile devices.

### Phase 8B — Internal review

- [ ] Add operator list/detail APIs and UI for website submissions.
- [ ] Add audited `under_review`, `accepted`, `rejected`, and `closed` transitions through the constrained review RPC.
- [ ] Add private operator attachment delivery with integrity verification.
- [ ] Verify customer/operator cross-access denial against real RLS.

### Phase 8C — Cloud-to-Windows inbox bridge

- [ ] Implement a separate pull bridge; do not repurpose the local-to-cloud publisher.
- [ ] Add isolated inbox staging, atomic completion, byte/hash checks, retries, checkpoints, and acknowledgements.
- [ ] Add a genuine remote-plan dry-run that downloads and acknowledges nothing.
- [ ] Test wrong/disabled/revoked bridge identities and duplicate delivery behavior.

### Phase 8D — Verification

- [x] Add initial Phase 8A migration, ownership, route, validation, and immutability security assertions.
- [x] Complete full lint, typecheck, intake/security tests, diff check, and production build for Phase 8A.
- [x] Add the exact non-production Phase 8A procedure in `docs/INQUIRY_INTAKE_TESTING.md`.
- [ ] Complete Phase 8B/8C tests and their exact local runbook before asking for those manual tests.
- [ ] Apply no migration, publication, deployment, or production change without explicit approval.

**Phase 8A local/live evidence (2026-10-16):** the disposable project accepted `supabase-inquiry-intake.sql`, `supabase-inquiry-intake-phase8a-fields.sql`, and `supabase-inquiry-intake-phase8a-limits.sql`. Verification returned 3/3 tables with RLS, valid constrained RPCs/policies, a private 30 MB bucket, the normalized 1–31457280 document constraint, and preserved submission/document counts. Customer A created submissions with private documents, viewed history/detail, downloaded integrity-checked copies, and verified attention, VAT/TRN, salesman reference, handover estimator, responsive attachment cards, and detail-card hierarchy. Customer B saw no Customer A data. Cumulative selection, replacement, removal, remove-all, and duplicate suppression were browser-tested. After correcting Next.js's 10 MB proxy truncation in `deee657`, a real 13-document submission completed and its detail page showed all 13 attachments with protected download controls. Current automated evidence is ESLint and TypeScript clean, inquiry security 38/38, and a 474-page production build.

**Phase 8 status:** IN PROGRESS. The Phase 8A core workflow and 13-document live submission are verified in disposable non-production Supabase. Staged progress/retry/finalization, saved customer profiles, customer projects, the remaining negative/accessibility matrix, and deployment acceptance are still incomplete. Phase 8B–8D remain incomplete. No additional migration, deployment, publication, or production-data change is authorized.

---

# Change log for this programme

| Date | Phase | Change | Reason |
|---|---|---|---|
| 2026-10-16 | 8A.2 | Added a complete pure-white Clay material recipe with neutral elevation shadows and exposed White plus eleven familiar basic/additional colour plates | Correct the mobile evidence that removing forced hue alone left inherited beige/lavender family tokens and an incomplete visible plate set; no inquiry phase advancement. |
| 2026-10-16 | 8A.1 | Corrected the global White appearance plate so achromatic input stays neutral instead of inheriting HSL red/rose from a forced saturation floor | Fix the verified mobile daylight-theme defect without advancing or changing the inquiry phase sequence. |
| 2026-10-16 | 8A | Recorded the live 13-document submission, Next.js body-limit correction, and the staged-upload/profile/project continuation checklist | Preserve verified evidence and explicit unfinished scope before Phase 8B so later work cannot silently skip customer-side requirements. |
| 2026-10-15 | 1 | Created `docs/INQUIRY_ORDER_DOCUMENT_WORKFLOW.md` | Persist verified discovery, architecture gaps, recommendations, acceptance evidence, decisions, and seven-phase checklist. |
| 2026-10-15 | 2 | Recorded approved scope and completed the workflow architecture contract | Define read-only inquiry-attachment publication, stable identity, private document organization, roles, bridge behavior, idempotency, retention, sequences, and failure recovery before implementation. |
| 2026-10-15 | 3 | Added draft private inquiry schema, policies/RPCs, document validation, and security tests | Build an additive, non-production data/security foundation while withholding full acceptance until live non-production RLS/Storage role testing is possible. |
| 2026-10-15 | 4 | Added localized inquiry register/detail UI and protected document presentation | Deliver responsive, accessible, honest read-only screens without mock data while keeping real data/download integration in Phase 5. |
| 2026-10-15 | 5 | Added authenticated inquiry APIs, integrity-checked private downloads, and read-only Windows bridge | Connect the UI to the real Supabase contract and local source workflow without mock fallbacks; retain partial status until non-production/live verification is available. |
| 2026-10-15 | 6 | Expanded inquiry API/schema/bridge tests and ran complete local regression gates | Verify local security boundaries, source-file handling, existing website behavior, build output, and unauthenticated HTTP behavior while recording the unavailable live Supabase matrix as blocking. |
| 2026-10-15 | 7 | Completed repository-level final audit and release-gate report | Review scope, files, tests, security headers, secrets/paths, deployment limitations, unrelated work preservation, and the exact non-production steps still required before production acceptance. |
| 2026-10-15 | 8 | Opened non-production deployment verification and recorded manual-provisioning blocker | Execute the previously blocked live Supabase matrix without authorizing or altering production. |
