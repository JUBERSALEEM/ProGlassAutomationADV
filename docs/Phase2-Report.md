# Phase 2 source-level implementation report

## Result

This report summarizes the Phase 2 source changes in this branch. It is not a deployment or Windows/WPF acceptance report; no Windows/WPF app was launched or simulated, and no Phase 3 work is covered.

| Requirement | Source result |
|---|---|
| **OPS-001** | Startup probes the saved share before any DbContext opens. If it is invalid/unavailable, the app continues in a clearly disclosed local AppData mode. A persisted offline-fallback review marker prevents silently switching datasets when the share later returns; operators must explicitly prepare/reconcile and reconnect. A selected share that fails after startup remains fail-closed rather than hot-switching roots mid-session. |
| **DB-001** | Runtime contexts, raw migrations, shared-root preparation, read-only health probes, and the migration-backup destination use the centralized path-aware SQLite policy. Local files request WAL; shared paths require DELETE mode. Effective mode is verified/logged; shared-mode mismatch blocks access. |
| **DB-002** | Migration helpers require explicit preservation before dropping columns, Inquiry legacy values are copied/compared before drop, non-empty unknown fields/conflicts stop migration, unique-key duplicates are reported without rewriting identifiers, and migrations verify required indexes/data plus SQLite `quick_check`. Audit migrations now preflight and verify normalized-user indexes/data as well. |
| **REC-001** | Backup results/listing distinguish unavailable, invalid, legacy, and restorable states. New sets carry a relative-path/length/SHA-256 manifest. Restore validates hashes before the safety set, fails on required-copy errors, protects the selected set from pruning, reports incomplete/overlay semantics, and never describes an overlay as a clean root replacement. Settings and `BACKUP.md` explain the distinction. |
| **CONC-001** | PI update and Result APIs require non-null `int expectedRowVersion`; special PI lifecycle writes and callers pass versions. Other document edit paths use versions. DN insertion recalculates its authoritative number/sequence inside a serializable transaction backed by unique indexes. Tax Invoice already-void unlink retries now work after the original version becomes stale. |
| **STK-001** | Glass and consumable stock-level changes are paired with movement/history rows in the same serializable transaction. Inventory deletion requires the loaded version and records the final balance in the movement ledger before removing the row. |

## Phase 2 source files

The source changes for this phase are concentrated in these files/groups (the checkout also contains preserved earlier Phase 0/1 changes):

- **Storage/root and SQLite policy:** `Services/Database/AppDataRoot.cs`, `SharedDataRootService.cs`, `SqliteResilience.cs`, `AppDbContext.cs`, `SchemaHealthService.cs`, `Services/Audit/AuditDbMigrations.cs`, Settings Data view-model/XAML, and `Services/Database/DatabaseBackup/RestoreDrillService.cs`.
- **Migration framework:** `Services/Database/SchemaMigrations/SchemaMigration.cs`, `SchemaMigrationRunner.cs`, `SchemaMigrationSql.cs`, and `AppSchemaMigrations.cs`.
- **Backup/restore:** `Services/Database/DatabaseBackup/BackupService.cs`, Settings backup view-model/XAML, and `docs/BACKUP.md`.
- **Optimistic concurrency and numbering:** Proforma Invoice, Tax Invoice, Delivery Note, Job Order, Statement, and Order Report models/contexts/interfaces/repositories plus their list/detail/generator callers. Principal changed service files include `Services/ProformaInvoice/ProformaInvoiceRepository/{IProformaInvoiceRepository.cs,ProformaInvoiceRepository.cs}`, `Services/TaxInvoice/Repository/{ITaxInvoiceRepository.cs,TaxInvoiceRepository.cs}`, `Services/DeliveryNote/Repository/{IDeliveryNoteRepository.cs,DeliveryNoteRepository.Part1.cs,DeliveryNoteRepository.Part2.cs}`, and `Services/OrderReport/Repository/{IOrderReportRepository.cs,OrderReportRepository.cs}`.
- **Stock/history:** `Models/SheetStore/InventoryItemMovement.cs`, `Services/SheetStore/{SheetStoreRepository.cs,InventoryItemRepository.cs,SheetStoreHistory/SheetHistoryRepository.cs}`, plus inventory/stock view-model callers.
- **Tests added/updated:** `Tests/Database/{SharedDataRootServiceTests.cs,SqliteResilienceTests.cs,SqliteResilienceCoverageTests.cs,SchemaMigrationSafetyTests.cs,BackupManifestTests.cs,InventoryStockConcurrencyTests.cs,StockConcurrencyTests.cs,OrderReportConcurrencyTests.cs}`, source-contract tests under `Tests/Agent/` for PI, Tax Invoice, and Order Reports, and `Tests/Audit/AuthNormalizationTests.cs`.
- **Documentation:** `docs/Phase2-Storage.md`, `docs/Phase2-Migrations.md`, `docs/Phase2-Concurrency.md`, this report, and `docs/BACKUP.md`.

## Verification performed

- `git diff --check` passed (no whitespace errors in tracked diffs).
- Source searches confirmed there is no PI no-version `UpdateAsync(invoice)` repository/API path and no nullable `expectedRowVersion` route in the PI repository/view-model paths.
- Source inspection verified manifest validation precedes safety backup and active-root copies; `backup-manifest.json` is excluded from live-root restoration; manifest paths outside supported roots are rejected.
- Source inspection verified migration index/data checks, DN uniqueness/transaction boundaries, and movement-history writes in the stock mutation transactions.

## Not run

The .NET SDK is unavailable (`dotnet` is not installed in this environment). No build, unit test, integration test, or Windows/WPF execution was performed. Windows validation remains required for shared-path behavior, provider-specific transaction semantics, live SQLite journal conversion, restore behavior, and the UI.

A separate source scan matching the existing `SqliteResilienceCoverageTests.No_repository_calls_raw_SaveChangesAsync` guard finds 69 direct `SaveChangesAsync` calls across `Services/` and `Agent/` (including unrelated Production code and several transactional inventory/Delivery Note operations). That broad pre-existing guard is not made green by this scoped Phase 2 work; resolving it would require a separate review of transaction/retry semantics across those services.

## Open decisions / rollout gates

1. **Windows acceptance:** verify a reachable and unreachable UNC/mapped share; confirm an unavailable share launches all databases from local AppData before any DbContext opens; confirm a recovered share remains local pending review; inspect required/effective journal logs; test live shared-root conversion with every client closed; and perform backup/overlay/full-root recovery on isolated synthetic data.
2. **Offline data reconciliation:** the app deliberately does not merge local-fallback SQLite files into a non-empty shared dataset. Before reconnecting a workstation that created local records while offline, operators need an approved backup/export/reconciliation procedure; never overwrite or copy over an existing company dataset.
3. **Audit migrator parity:** `audit.db` still uses its existing additive hand-written migrator rather than `SchemaMigrationRunner`. It now shares connection policy and verifies critical schema/data, but lacks runner-style pre-migration snapshots, one transactional versioned batch, and the same rollback guarantees. Keep that separate until auth/audit-specific upgrade tests are available.
4. **Legacy backup sets:** without a manifest, older sets can only be checked for known SQLite headers and are explicitly completeness-unverified. Confirm this compatibility policy before production rollout; do not treat legacy-set success as proof that every attachment was captured.
5. **Legacy duplicate business keys:** migrations deliberately stop rather than renumber. An approved operator procedure is still needed to resolve duplicate historical DN/other business keys while preserving external references and retaining a verified pre-migration backup.
6. **Retry coverage guard:** the existing broad raw-save source guard reports the 69 invocations above. It remains a separate follow-up; do not mechanically wrap transactional stock/number allocation code without validating SQLite transaction behavior first.
