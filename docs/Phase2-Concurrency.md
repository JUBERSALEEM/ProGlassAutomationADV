# Phase 2 — optimistic concurrency and stock integrity

## RowVersion convention (CONC-001)

Editable persisted documents carry an integer `RowVersion` configured as an EF concurrency token. Update/status/special-write repositories require the version the caller loaded, check it before applying changes, increment it with the write, and translate EF's check-then-write race into a `ConcurrencyConflictException`. A missing row is also reported as a conflict rather than silently treated as a successful edit. Callers pass the row's loaded version; they must reload/reconcile after a conflict instead of resubmitting stale state.

### Proforma Invoice API hardening

`IProformaInvoiceRepository.UpdateAsync` has exactly one update contract: `UpdateAsync(SavedProformaInvoice invoice, int expectedRowVersion)`. There is no no-version overload and no nullable expected-version route. The implementation checks the loaded value and lets EF's token protect the actual write. `UpdateWithResultAsync` also requires the same non-null integer and delegates to that versioned path. Delete, void, and the dedicated cancellation/status-sync operation require versions too. The saved-invoice list, printable-invoice update paths, and other PI callers pass the version they loaded. Snapshot-header synchronization is performed before body/lifecycle writes so the indexed header and JSON document do not diverge.

### Other document writes

Tax Invoice body edits, status changes, deletes, explicit voids, and Result deletes carry expected versions. A partial void can commit the Tax Invoice row before a cross-database Delivery Note unlink fails; retrying `VoidAsync` on an already-void invoice now retries the idempotent unlink before checking the now-stale caller version. This retry changes no Tax Invoice fields and does not overwrite another void reason. Ordinary non-void mutations still reject stale versions.

Delivery Note body/status edits, Job Order body/status edits, Credit Note body edits, Statements, and Order Reports use required versions on the edit paths already covered by this change set. Order Report PDF-path changes increment/check RowVersion as well. Its UI deletes the database record before removing PDFs, and distinguishes a failed PDF cleanup from a failed/stale row deletion.

## Delivery Note numbering (CONC-001)

`DeliveryNoteRepository.InsertAsync` owns the authoritative sequence/ID allocation. It reserves a serializable SQLite write transaction before reading the maximum sequence, assigns the next sequence and derived Delivery Note ID, and commits the row in that same transaction. IDs calculated in `DeliveryNoteCreateService` are presentation hints only; repository insertion recalculates them.

Fresh schemas declare unique Delivery Note IDs and a unique `(JobOrderId, SequenceNumber)` index. Existing databases receive versioned migrations for both. Duplicate historical keys stop migration with representative values rather than renumbering DNs that other documents may reference. The unique composite index is checked before use and remains the cross-process backstop if another/older writer does not use the allocator.

## Stock counters and movement history (STK-001)

Glass-sheet and consumables stock writes now keep the counter and movement history in the same serializable transaction. Opening stock, manual stock/used adjustments, receipt, consumption, inventory deletion, and reversal entries are recorded with the corresponding counter mutation. A failure rolls back both sides, so a displayed stock value cannot commit without its movement record. Consumable quantities remain decimal arithmetic in C# because their SQLite columns use TEXT affinity to preserve fractional units.

Inventory-item deletion requires `expectedRowVersion`; it checks the loaded version, writes the final stock/used/balance snapshot as a movement, and deletes the row within one transaction. Receipt/consume and the glass-sheet stock/history operations serialize writers and use row-version tokens where edits are based on a caller-loaded entity.

## Tests and verification status

Source/runtime tests were added or updated for stale Order Report/stock writes, atomic movement-history behavior, DN identity/sequence constraints, PI mandatory version contracts and callers, and Tax Invoice lifecycle retry ordering. They have not been run because `dotnet` is unavailable. Windows SQLite provider behavior and multi-process share contention must still be verified on isolated Windows test data before rollout.
