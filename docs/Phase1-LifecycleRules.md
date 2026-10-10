# Phase 1 — Document lifecycle rules

**Prepared:** 2026-10-10<br>
**Scope:** DOC-001. Rules are enforced in repositories/services; UI visibility is not the enforcement boundary.<br>
**Verification:** source review and tests added; no .NET build/test or Windows/WPF execution was possible because `dotnet` is unavailable.

## Policy summary

`DocumentLifecyclePolicy` is the shared authority for hard-delete eligibility, body editability, whether the current status permits the configured void transition, void target status, and required void-reason normalization. Each repository also checks stored state and links before writing; a detached UI object cannot make an issued/voided record appear to be a draft or forge void audit metadata.

| Record | Hard-delete eligibility (must also be unlinked) | Body editable while stored status is | Void status | Link/dependency rule |
|---|---|---|---|---|
| Proforma Invoice (PI) | Draft, Sent, Rejected, Expired | Draft, Sent, Rejected, Expired | Cancelled | Any retained Job Order, Delivery Note, or Tax Invoice blocks PI delete/void. Dependency lookups fail closed if required repositories are unavailable. |
| Job Order (JO) | Draft | Draft, Released, InProduction, Completed | Rejected | Linked Delivery Notes block delete/void. The internal Master DN is not treated as a public child; draft JO deletion removes it best-effort and logs cleanup failure. |
| Delivery Note (DN) | Draft | Draft, Dispatching | Cancelled | A TI-owned invoice link blocks DN body edit, hard-delete, and void. Unlink/void the invoice first. System-managed Master DNs have a separate internal cleanup path. |
| Tax Invoice (TI) | Draft | Draft | Cancelled | A linked DN blocks hard-delete. Explicit void retains the TI and clears its DN links, with verification and surfaced failure. |
| Credit Note (CN) | Draft only when not linked to a source TI | Draft | Cancelled | A non-empty source Tax Invoice reference blocks hard-delete, including for a Draft CN; void only if the status transition allows it, otherwise retain unchanged. |

Statuses not listed as hard-deletable are never physically deleted. A delete request routes to void only when `StatusTransitions` permits the configured void edge. Terminal, unknown, blank, or otherwise non-transitionable statuses are retained unchanged; for example, `Paid` Tax Invoices, `Applied` Credit Notes, and `Received` Delivery Notes cannot be voided because their transition tables make them terminal. A linked record is blocked instead of being orphaned or silently voided out from under its dependents.

### Important special cases

- The Master DN is an internal tracker rather than a customer delivery record. Generic `InsertAsync` rejects Master-DN entities; internal creation uses `CreateMasterDnAsync`. Its repository delete path is reserved for internal cleanup and audits the deletion. Normal DN lifecycle restrictions apply to non-master delivery notes.
- Credit Notes are tied to source Tax Invoices. The repository checks `TaxInvoiceReference` as a retained link, so a linked Draft CN is blocked from physical deletion even though `Draft` is in the general policy’s safe-status set.
- TI delete requests with linked DNs are blocked. The operator must use the explicit void operation, which records the reason and attempts a verified unlink.
- PI deletion checks downstream records across repositories and refuses to delete when a retained child exists. It does not treat its Inquiry back-link as a downstream document.

## Body updates and status-only changes

- Update methods reload the stored row, reject voided/terminal records, check link and immutability constraints, validate lifecycle state, and (where available) compare the editor’s `RowVersion` before replacing the body. TI/CN detached updates reject caller-supplied void metadata and copy only onto the tracked row; TI invoice/family/type/sequence identity and CN invoice/family/job/sequence identity are immutable.
- User-requested status changes are validated using `StatusTransitions`; they must not be smuggled through an ordinary body update. TI and DN status-only operations use dedicated repository methods. The TI editor now saves draft body changes with the stored status, then calls the status-only method for a validated transition. On an already-posted TI it skips the body write entirely. The narrowly scoped PI downstream-cancellation synchronization is the exception: it requires `DocumentLifecycleSync`, updates the snapshot and `RowVersion`, and writes a status-sync audit entry.
- Snapshot state is refreshed when void/status changes are persisted (PI, JO, DN, TI). Ordinary PI updates canonicalize duplicated identity/status/date header fields from the row; PI insert, body update, void, and downstream lifecycle-status writes reject malformed or structurally blank JSON before saving instead of using the mapper's empty-snapshot fallback. Ordinary JO updates canonicalize duplicated identity/status fields and preserve embedded PI data; TI insert/body/status/void writes canonicalize the snapshot header from the row. Void writes increment `RowVersion` for these records; CN void also increments its version. This keeps retained JSON/status views aligned with the row and makes stale editors detectable.
- Void operations require a reason, trim it, reject blank input, and reject values longer than 500 characters instead of silently truncating the audit context. Void records are audited in the repository after persistence. Physical deletes are also audited there; duplicate UI delete/void audit writes were removed.
- A TI void is a two-database operation. If the TI row commits but DN unlink fails, the failure is returned to the caller. Retrying `VoidAsync` on an already-void TI retries the unlink and verifies that no DN still points at it.

## Delete/void audit behavior and limitations

- Repository boundaries own the durable delete/void audit entry. UI commands no longer add a duplicate entry for operations that repositories already audit.
- Delivery Note and Job Order UI cleanup rechecks that the row is physically gone before deleting related attachment bytes; a retained/voided row keeps its files.
- The legacy `DeleteAsync(int)` APIs remain, so some posted-record delete requests route to `VoidAsync` using the internal reason `Delete requested; lifecycle policy requires retention.` The explicit void UI obtains a user reason; whether every delete button that routes to a void should instead prompt for an operator-supplied reason is an open decision.
- `InquiryLinkSync` is deliberately best-effort today: a PI can commit while its denormalized Inquiry link/status update fails; the helper logs a warning but its caller does not receive a success/failure result. This is listed as an open risk, not represented as an atomic transaction.
- Some document operations span separate SQLite databases and cannot be made atomic by the current design. The TI↔DN path is explicitly reported/retried; the PI↔Inquiry behavior remains best-effort.

## Tests added/expanded (not run)

- `Tests/Workflow/DocumentLifecyclePolicyTests.cs`: hard-delete/body-lock matrix, legal void-transition edges, terminal/unknown retained states, void targets, and required/trimmed/maximum-length void reasons; expanded to include Credit Notes.
- `Tests/CreditNote/CreditNoteLifecycleTests.cs`: Credit Note lifecycle behavior.
- `Tests/Agent/TaxInvoiceLifecycleSourceTests.cs`: TI result outcomes, idempotent unlink retry, stale-link reporting, authorized/retryable post-login reconciliation, detached TI/CN update protection, canonical JO/TI snapshots, TI-owner link validation, Master-DN insert boundary, and DN body/status boundaries.
- `Tests/ProformaInvoice/SnapshotRoundTripTests.cs` and `Tests/Agent/ProformaInvoiceSnapshotHeaderSourceTests.cs`: PI header canonicalization, corrupt/blank-snapshot fail-closed behavior, and insert/update/void/lifecycle-status write-path coverage.
- `Tests/Audit/OperationGuardCoverageTests.cs`: service-layer mutation guard coverage for configured document, Inquiry, and Sheet Store repositories.

## Open decisions

1. Should a list-screen “Delete” that becomes a void collect a human-entered reason, or should it refuse and direct the user to the explicit Void flow? The current legacy API writes the nonblank internal reason stated above.
2. Should PI↔Inquiry link/status synchronization remain best-effort, or should PI callers surface a partial-commit result and offer a retry/reconciliation path?
3. Should Master DN cleanup remain best-effort after a JO is physically deleted, or should a retained orphan tracker block/report the parent deletion as a partial commit?
4. Phase 1 is not complete for rollout until the recorded source tests run on Windows/.NET and the product’s supported database migration path is exercised against a copy of representative schemas (not customer/production data in this environment).
