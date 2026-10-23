# Phase 1 implementation report — Auth, lifecycle, finance links

**Date:** 2026-10-10<br>
**Branch:** `arena/34ea4608-proglassautomationadv`<br>
**Scope:** AUTH-001, AUTHZ-001, DOC-001, FIN-001.<br>
**Status:** Phase 1 implementation and source audit are complete for review; Windows/.NET verification remains pending. **Phase 2 has not been started** and remains blocked on the user reviewing and approving this report.

## Executive summary

Phase 1 changes were applied in code, not just audited:

- Canonicalized the Administrator role and username identity; first-user setup now fails closed, and disable/demotion/delete paths protect the last active Administrator.
- Added service/repository write authorization and writable-license checks for Production, Sheet Store/stock, Inquiry, and document write paths. Production also checks its DbContext save boundary.
- Centralized document hard-delete/body-lock/void decisions; explicit void writes now also obey the status-transition table. Terminal/unknown states are retained unchanged, and repositories audit deletes/voids.
- Made TI↔DN synchronization validate the existing live invoice owner and complete DN link set, report cross-database partial commits, use a narrow finance-link permission, avoid re-linking void invoices, and surface stale/missing/conflicting data rather than guessing.
- Closed detached-update bypasses in TI/CN: caller-supplied void metadata is rejected, stable invoice/family/sequence identity is immutable, normalized children are keyed to the canonical parent, and updates copy onto tracked rows. JO, PI, and TI snapshot identity/status headers are canonicalized from row state. PI insert/update/void/lifecycle-status writes now fail closed on malformed or structurally blank snapshots instead of serializing the mapper's empty-snapshot fallback.
- Decoupled post-login reconciliation from optional Presence startup, schedule it only for an active link-authorized user, and reset the one-shot guard after failure so a later eligible identity event can retry.
- Fixed the Tax Invoice editor’s body/status mismatch: body writes preserve the stored source status; status transitions use the dedicated validated status-only repository method. A posted TI status-only transition does not send a body update.

The worktree also contains unrelated Phase 0 backup/recovery, licensing, and AI files changed outside this Phase 1 scope. They were not modified as part of this Phase 1 pass and are intentionally excluded from the Phase 1 changed-file inventory below.

## Changed-file inventory (Phase 1 scope)

### Authentication and authorization

- `App.AuthHandlers.cs`
- `App.StartupCore.cs`
- `Models/Audit/AppUser.cs`
- `Services/Audit/AuditDbContext.cs`
- `Services/Audit/AuditDbMigrations.cs`
- `Services/Audit/AuditService.Part1.cs`
- `Services/Audit/AuditService.Part2.cs`
- `Services/Audit/Permissions.cs`
- `Services/Audit/UsernameNormalizer.cs`
- `Services/Audit/UserAdministrationPolicy.cs`
- `Services/Inquiry/InquiryRepository.cs`
- `Services/Production/ProductionDbContext.cs`
- `Services/Production/ProductionReleaseService.cs`
- `Services/Production/ProductionRepository.cs`
- `Services/Production/ProductionWriteGuard.cs`
- `Services/SheetStore/InventoryItemRepository.cs`
- `Services/SheetStore/SheetStoreRepository.cs`
- `Services/SheetStore/SheetStoreHistory/SheetHistoryRepository.cs`
- `Views/Login/FirstRunSetupPanel.xaml.cs`
- `ViewModels/SystemCenter/SystemCenterViewModel.cs`
- `ViewModels/UserCenter/UserCenterViewModel.cs`

### Document lifecycle, TI↔DN finance linking, and UI call paths

- `Services/CreditNote/Repository/CreditNoteRepository.cs`
- `Services/DeliveryNote/Repository/DeliveryNoteRepository.Part1.cs`
- `Services/DeliveryNote/Repository/DeliveryNoteRepository.Part2.cs`
- `Services/DeliveryNote/Repository/IDeliveryNoteRepository.cs`
- `Services/JobOrder/Repository/JobOrderRepository.cs`
- `Services/JobOrder/Repository/IJobOrderRepository.cs`
- `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs`
- `Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceMapper.Part1.cs`
- `Services/TaxInvoice/Repository/TaxInvoiceRepository.cs`
- `Services/TaxInvoice/Mapper/TaxInvoiceMapper.cs`
- `Services/TaxInvoice/Repository/TaxInvoiceDnReconciler.cs`
- `Services/TaxInvoice/Repository/TaxInvoiceLinkSynchronizationException.cs`
- `Services/Workflow/DocumentLifecyclePolicy.cs`
- `Services/Workflow/DocumentOperationCoordinator.cs`
- `ViewModels/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailViewModel.Part2.cs`
- `ViewModels/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailViewModel.Part3.cs`
- `ViewModels/DeliveryNote/DeliveryNoteList/DeliveryNoteListViewModel.Part2.cs`
- `Views/DeliveryNote/DeliveryNoteList/DeliveryNoteListView.xaml.cs`
- `ViewModels/JobOrder/JobOrderDetail/JobOrderDetailViewModel.cs`
- `ViewModels/JobOrder/SavedJobOrdersList/SavedJobOrdersListViewModel.Part1.cs`
- `ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.Part1.cs`
- `ViewModels/ProformaInvoice/SavedInvoicesList/SavedInvoicesListViewModel.Part2.cs`
- `ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.cs`
- `ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.Commands.cs`
- `ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.Save.cs`

### Tests added or expanded

- `Tests/Audit/AuthNormalizationTests.cs`
- `Tests/Audit/OperationGuardCoverageTests.cs`
- `Tests/Audit/TaxInvoiceDnLinkPermissionTests.cs`
- `Tests/Agent/OperationGuardTests.cs`
- `Tests/Agent/ProductionWriteGuardTests.cs`
- `Tests/Agent/TaxInvoiceLifecycleSourceTests.cs`
- `Tests/Agent/ProformaInvoiceSnapshotHeaderSourceTests.cs`
- `Tests/ProformaInvoice/SnapshotRoundTripTests.cs` (PI snapshot canonicalization and fail-closed coverage)
- `Tests/CreditNote/CreditNoteLifecycleTests.cs`
- `Tests/Workflow/DocumentLifecyclePolicyTests.cs`

The three requested Phase 1 notes are also delivered: `docs/Phase1-AuthNotes.md`, `docs/Phase1-LifecycleRules.md`, and `docs/Phase1-FinanceLinking.md`.

## Lifecycle rules now enforced

- **PI:** childless Draft/Sent/Rejected/Expired may be hard-deleted. Any retained JO/DN/TI dependency blocks delete and void. Body is editable only in Draft/Sent/Rejected/Expired. Void target is Cancelled.
- **JO:** only a childless Draft may be hard-deleted; linked DNs block delete/void. Body editing follows the shared matrix through Completed; void target is Rejected.
- **DN:** only an unlinked Draft may be hard-deleted; an invoice link blocks body edit/delete/void. Body editing is Draft/Dispatching only; void target is Cancelled. Master DNs use a separate internal cleanup path.
- **TI:** only an unlinked Draft may be physically deleted. Issued and later documents are retained; linked DNs block hard-delete. Void target is Cancelled; a void explicitly clears and verifies its DN links.
- **CN:** Draft is the only policy-level hard-delete state, but a source TI reference is mandatory for a valid CN, so the repository blocks physical deletion of every valid linked CN (including Draft); use the authorized void path when the transition table allows it. Applied/voided/unknown states are retained. Void target is Cancelled.
- Every explicit void reason is trimmed, required, and limited to 500 characters. Delete/void audits are repository-owned to prevent UI duplicates. Status transition tables also gate explicit voids: for example, Paid TIs, Applied CNs, Received DNs, and unknown legacy statuses are retained unchanged. New records cannot carry caller-supplied void audit metadata.
- Status transitions are validated independently from body edits. TI/DN have status-only write paths; the TI editor mismatch that passed the target status into `UpdateAsync` has been corrected. TI and CN ordinary updates cannot persist void flags/reasons, and stable fiscal identities are checked before updating tracked rows.
- JO updates canonicalize the JSON status and identity from the stored row and preserve its embedded PI snapshot. PI insert/body-update/void/downstream-lifecycle-status writes canonicalize duplicated header identity, status, and dates from the row; invalid/blank snapshots are rejected before commit. TI insert/body/status/void writes canonicalize duplicated snapshot-header identity fields. CN/TI normalized child rows are stamped with their canonical parent and family keys.
- Generic DN `InsertAsync` now rejects internal Master-DN objects; internal tracker creation uses the dedicated repository path.

The full matrix, dependency behavior, and noted exceptions are in `docs/Phase1-LifecycleRules.md`.

## TI↔DN guarantees

- A live TI save is persisted, then the complete DN link set is transactionally validated and synchronized inside `deliverynotes.db`; all targets are checked before mutation, the owner TI must exist and be live, and the final set is verified before the DN transaction commits. Owner status is rechecked immediately before commit (the two databases still cannot provide a single atomic transaction).
- Missing/void/master/unconfirmed DNs, inconsistent flag/id pairs, conflicting ownership, stale links, and unavailable link services are not silently overwritten or treated as success.
- Since the invoice database and DN database are separate SQLite files, this is not a distributed atomic transaction. If the TI commits but link synchronization fails, the typed exception/result explicitly identifies the persisted TI and the failed second step.
- Voiding a TI persists its terminal/void state and audit entry, then clears/verifies DN ownership. A failure is surfaced as “TI voided, DN links could not be cleared”; retrying the idempotent void retries unlink and verification.
- Reconciliation is scheduled after sign-in only for an active user with the finance-link permission, so repairs remain subject to identity, permission, and writable-license checks. Subscription no longer depends on Presence startup. A failed sweep clears its one-shot guard so a later eligible user/identity event can retry it. It never re-marks void/terminal invoices and reports leftover links without guessing.
- `DocumentOperationCoordinator` avoids repeating the concrete repository’s combined TI/link transaction and returns correlated partial-failure results.

## Tests and verification

**Not run:** no .NET build, `dotnet test`, or Windows/WPF execution. The environment has no `dotnet` executable. No tests or runtime checks are claimed as passing.

**Source checks performed:**

- Manually inspected the changed auth, document repository, TI/DN, reconciler, editor, and focused Production-to-Stock paths.
- Ran a local static source scan over the configured document/Inquiry/Sheet Store repository set, including block-bodied and expression-bodied mutators: 77 public mutating methods checked, with no missing direct `OperationGuard.Demand` or `ReadOnlyGate.Demand` call (two documented delegated exceptions were excluded).
- Production reservation/allocation/cutting writes target `production.db`; configured public release/completion workflows have explicit guards, and every `ProductionDbContext.SaveChanges`/`SaveChangesAsync` commit boundary revalidates permission and writable-license state. Direct context users in ViewModels/reporting remain covered by that commit guard. A repo-wide source search of production DB handles, raw SQL/ADO.NET, `ExecuteUpdate`/`ExecuteDelete`, and bulk-write APIs found no business-data bypass; schema initialization/migrations are system-owned DDL paths. The stock-availability query only reads `proglass.db`; cutting completion explicitly does not deduct Sheet Store stock. Actual Sheet Store stock changes use guarded Sheet Store repository methods.
- These source checks and prepared tests do not substitute for running the Roslyn tests or compiling.
- Final `git diff --check` completed with no findings. An earlier trailing-whitespace finding in the coverage test was corrected.

**Tests prepared for Windows/.NET execution:**

- Auth normalization, Admin canonicalization, account bootstrap, and last-admin policy/source checks: `Tests/Audit/AuthNormalizationTests.cs`.
- Service authorization and repository guard coverage: `Tests/Agent/OperationGuardTests.cs`, `Tests/Audit/OperationGuardCoverageTests.cs`, `Tests/Agent/ProductionWriteGuardTests.cs`.
- TI-owned DN permission split: `Tests/Audit/TaxInvoiceDnLinkPermissionTests.cs`.
- Lifecycle/status/void policy including legal void edges, retained terminal/unknown states, Credit Notes, and reason/metadata validation: `Tests/Workflow/DocumentLifecyclePolicyTests.cs`, `Tests/CreditNote/CreditNoteLifecycleTests.cs`.
- TI/DN partial commit, unlink retry, eligible/retryable reconciliation, TI owner validation, detached TI/CN updates, canonical snapshots, normalized item keys, Master-DN insert restriction, and status/body separation source checks: `Tests/Agent/TaxInvoiceLifecycleSourceTests.cs`.
- PI snapshot header canonicalization and preservation on corrupt/blank JSON, plus repository insert/update/void/lifecycle-status path checks: `Tests/ProformaInvoice/SnapshotRoundTripTests.cs`, `Tests/Agent/ProformaInvoiceSnapshotHeaderSourceTests.cs`.

## Open decisions for review

1. **PI↔Inquiry partial sync:** `InquiryLinkSync` catches/logs failures and returns control without exposing sync success to PI callers. Decide whether to keep it best-effort or add a caller-visible partial-commit/retry result.
2. **Delete routed to void:** legacy `DeleteAsync` APIs may invoke void with the internal nonblank reason `Delete requested; lifecycle policy requires retention.` Decide whether those UI paths must collect an operator-authored void reason or refuse and direct users to the explicit Void flow.
3. **Live-session revocation:** account disable/demotion in another running client does not currently push immediate session revocation to all workstations; decide whether to add cross-client revocation or per-write persisted-role revalidation.
4. **Username migration collision:** migration intentionally disables sign-in/bootstrap if legacy rows normalize to the same key. Define an operator-controlled duplicate-account resolution procedure before rollout.
5. **Reconciliation retry trigger:** an unsuccessful TI↔DN sweep now retries after a later eligible `CurrentUserChanged` event. There is still no timer or explicit manual repair command if no further sign-in/identity event occurs; decide whether a dedicated maintenance trigger is needed.
6. **Void-TI stale links:** startup reconciliation reports, but does not automatically unlink, DNs still owned by a void TI. The authorized `VoidAsync` retry is safe; decide whether a separate privileged maintenance action is needed.
7. **Master DN cleanup:** JO deletion cleanup remains best-effort and logs a leftover internal Master DN rather than rolling back or reporting the parent delete as a partial commit.
8. **Windows verification:** before rollout, run the full test suite/build on Windows and exercise additive migration/lifecycle flows against copied representative databases. Do not use production/customer data for this check.

## Gate

Phase 1 work is ready for review but is **not approved for rollout**. Phase 2 will not begin unless and until the user reviews and explicitly approves this report.
