# Phase 3 — Database and financial-data integrity

**Status:** Main database initialization now fails closed on initialization or migration failure; Tax Invoice migration failure is also checked. Full integration testing and live-write backup validation remain open.

## Scope
Protect all SQLite databases and all related business documents from partial writes, schema drift, duplicate operations, concurrency races, corrupt backups and restore mistakes.

## Existing change in this branch
`Services/TaxInvoice/Repository/TaxInvoiceRepository.cs` now throws when the registered Tax Invoice schema migration returns failure. This is intended to stop invoice initialization against a schema the application knows is not current. It still requires Windows build and regression tests.

## Findings requiring implementation

| ID | Finding | Required fix | Acceptance evidence |
|---|---|---|---|
| DB-01 | Main database migration/init failure must not allow app startup to continue | `DatabaseInitializer.Initialize()` now throws on migration failure and rethrows initialization exceptions after logging; outer startup handler should show the fatal-startup UX and shut down | Windows startup test proves failure exits before module UI and dependent writes |
| DB-02 | Backup service copies SQLite database and WAL/SHM sidecars as separate files | Use SQLite's supported backup/snapshot approach or a quiesced, verified backup protocol; do not assume file-by-file copy is a consistent point-in-time snapshot | Integration test with concurrent writes and integrity/content checks |
| DB-03 | Restore drill must never mutate live data | Inject isolated source/scratch roots; assert all drill writes stay beneath a unique temporary root | Test asserts live database hashes and timestamps remain unchanged |
| DB-04 | Cross-document operations can race between validation and commit | Put invariants in the same transaction/write boundary, add appropriate unique/foreign-key/check constraints | Parallel duplicate-create, delivery/invoice linkage and stock movement tests |
| DB-05 | Migration behavior must be consistent across every repository | Standardize migration result handling; no repository may ignore a failed upgrade | Source-level coverage test plus failed-migration integration test for each database |
| DB-06 | Financial totals can drift across PI, Tax Invoice, Credit Note, Delivery Note and Statement | Use decimal-safe calculations, one rounding policy, explicit document linkage and transactional lifecycle changes | Golden-value tests, reconciliation tests and round-trip persistence tests |

## Mandatory data integrity invariants

- A failed transaction must not leave partial business state.
- A failed migration must not be followed by writes that assume the new schema.
- Invoice totals, VAT, discounts and rounding must be deterministic and consistent between UI, saved records, PDF and exports.
- Duplicate delivery/invoice actions must be rejected at the persistence boundary, including concurrent attempts.
- Stock balance must reconcile to opening stock plus receipts minus consumption/adjustments.
- Restoring a backup must be preceded by a verified safety snapshot and must report partial failure clearly.
- Backup and restore tests must use temporary isolated directories, never the developer's live data root.

## Phase exit criteria

- [ ] Main DB migration failure is fail-closed for every dependent mutation.
- [ ] SQLite backup is consistent while the application is under concurrent write load.
- [ ] Restore drill is isolated and proves actual SQLite integrity and required attachment/config coverage.
- [ ] Cross-document and stock concurrency tests pass.
- [ ] PI, invoice, credit note, delivery and statement reconciliation tests pass.
- [ ] Windows Release build and complete database test suite pass on the exact candidate commit.

**Do not mark Phase 3 complete until the evidence is attached to the release report.**
