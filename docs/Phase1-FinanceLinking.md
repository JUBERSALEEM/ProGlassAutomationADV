# Phase 1 — Tax Invoice / Delivery Note linking

**Prepared:** 2026-10-10<br>
**Scope:** FIN-001. This note documents the implemented TI↔DN guarantees and their cross-database limits.<br>
**Verification:** source review and tests added; no .NET build/test or Windows/WPF execution was possible because `dotnet` is unavailable.

## Ownership and permission boundary

A Tax Invoice’s `DeliveryNoteReference` is the desired set of DN identifiers. Each Delivery Note stores the matching `HasTaxInvoice` flag and `TaxInvoiceId`. A set is considered consistent only when every referenced DN exists and points to that TI, and no unreferenced DN still points to it.

TI-owned link writes use the narrow `DeliveryNote.InvoiceLink` permission, not the general DN body-edit permission. Manager, Accountant, and Administrator roles have it. Accountant can maintain finance links without gaining DN body-edit access; Production can perform its permitted DN edits but does not receive this finance-link permission. All writes also pass the writable-license gate. The startup repair is scheduled after sign-in so it cannot bypass the service guard before an identity exists.

## Save/link guarantee

`TaxInvoiceRepository` owns TI persistence and TI↔DN synchronization:

1. Validate the invoice and commit its row in `taxinvoices.db`.
2. Replace the complete DN link set in a Delivery Notes database transaction.
3. Before changing any DN, require the owning TI row to exist and be live; then validate that each target exists, is a non-void/non-master confirmed delivery, has a consistent flag/id pair, and is not owned by a different TI. Validate all targets before mutating any. Recheck that owner before committing the DN transaction. Clearing an empty set is also allowed for an existing void/terminal TI so idempotent void cleanup can retry.
4. Clear stale links currently owned by this TI, set its target links, then verify the target set and confirm that no extra DN still points to the TI before committing the DN transaction.
5. On an insert/update link failure after the TI commit, throw `TaxInvoiceLinkSynchronizationException`, which carries the persisted invoice and the underlying failure. The caller is told explicitly that the TI row committed but the DN-link step did not; this is a partial commit, not a reported rollback.

`DocumentOperationCoordinator.SaveTaxInvoiceWithDnLinkAsync` uses the concrete repository’s combined invariant and does not repeat the link transaction. Alternate repository implementations use the coordinator fallback and are verified after synchronization. The coordinator preserves correlation/error IDs and reports the partial-commit case.

There is no distributed transaction across the two SQLite files. Therefore the guarantee is **validated/verified synchronization or an explicit partial-failure result**, not cross-file atomicity.

## Void/unlink guarantee

- Voiding a TI retains its fiscal row, number, snapshot, and audit data; it sets the void flag/terminal status, stamps actor/time/reason, refreshes its snapshot, increments its row version, and writes the audit event in `taxinvoices.db`.
- The repository then clears all DNs owned by that invoice and verifies that none remain. If this companion write fails, the operation throws an explicit message stating that the TI was voided but its DN links could not be cleared.
- Retrying `VoidAsync` on an already-void TI retries that idempotent clear/verification and lifecycle refresh; it does not rewrite the invoice body or silently report a successful unlink.
- DN hard-delete, body update, or void is blocked while an invoice link remains. The TI must be unlinked/voided first.

## Startup reconciliation behavior

- The sweep runs after `AuditService.CurrentUserChanged` establishes an active signed-in identity, and only schedules for a user with `DeliveryNote.InvoiceLink` permission. It does not run as an unauthenticated startup write. Optional Presence-service startup is independent from auth-event subscription. A failed sweep resets the one-shot guard so a later eligible identity event can retry; success leaves it set for the process.
- Voided TIs are never used to re-mark DNs. If a DN still points at a void TI after a prior partial commit, the sweep reports the stale ownership and explicitly does not relink or unlink automatically; retrying the authorized void operation performs the safe unlink.
- For live TIs, the sweep fails rather than guessing when invoice IDs are blank, DN IDs are duplicated, referenced DNs are missing, multiple invoices contend for a DN, or stale links are found. It does not silently overwrite a different invoice’s link or automatically clear unowned/orphan links.
- A missing link for a valid live TI may be repaired through the same permission-checked, idempotent repository operation and is verified afterward. Repaired counts and failures are logged; `ReconcileWithResultAsync` returns an explicit success/failure result.

## Relevant implementation and tests

Implementation:

- `Services/TaxInvoice/Repository/TaxInvoiceRepository.cs`
- `Services/TaxInvoice/Repository/TaxInvoiceDnReconciler.cs`
- `Services/TaxInvoice/Repository/TaxInvoiceLinkSynchronizationException.cs`
- `Services/DeliveryNote/Repository/DeliveryNoteRepository.Part1.cs`
- `Services/Workflow/DocumentOperationCoordinator.cs`
- `Services/Audit/Permissions.cs`
- `App.AuthHandlers.cs` and `App.StartupCore.cs` (move repair scheduling from pre-login startup to post-login)
- `ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.Save.cs` (status/body write separation)

Tests added/expanded (not run):

- `Tests/Agent/TaxInvoiceLifecycleSourceTests.cs` checks outcome reporting, retried unlink, void-reconciler behavior, authorized/retryable post-login scheduling, live-owner checks, detached TI/CN update protections, and TI/DN status/body boundaries.
- `Tests/Audit/TaxInvoiceDnLinkPermissionTests.cs` pins the narrow role mapping.
- `Tests/Agent/DocumentOperationCoordinatorTests.cs` and `Tests/Agent/TaxInvoiceDnReconcilerTests.cs` already cover parsing/coordinator and reference parsing behavior; these were not executed here.

## Remaining limits / open decisions

1. A cross-database partial commit can remain until an authorized user retries void or a live-TI reconciliation repairs it. The system reports the state; it does not offer a transactional rollback across SQLite files.
2. An unsuccessful sweep retries after a later eligible `CurrentUserChanged` event. There is no timer or manual repair command if no further identity event occurs; decide whether to add a dedicated maintenance trigger.
3. Stale ownership on a void TI is reported but not automatically unlinked by the sweep. This is intentional to keep repair permission-checked and avoid guessing; decide whether an explicit privileged maintenance workflow is needed.
4. If two live TIs reference the same DN, the service refuses the conflicting write/sweep and reports it. Operators need a reconciliation procedure for that data error; no invoice is selected automatically.
