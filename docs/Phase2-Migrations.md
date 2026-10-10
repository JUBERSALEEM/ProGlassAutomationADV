# Phase 2 — SQLite migration safety

## Runner contract

`SchemaMigrationRunner` applies pending per-database migrations in version order using `PRAGMA user_version`. Before any schema write, it creates a dedicated pre-migration snapshot with SQLite's online backup API and runs `PRAGMA quick_check` on that snapshot. If the snapshot cannot be created or verified, migration is refused.

The migration connection is opened with the same path-aware journal policy as runtime contexts. Pending migrations run inside one SQLite transaction. Apply actions and optional verification callbacks run before the version marker commits; callbacks are repeated after the full pending set so a later migration cannot silently invalidate an earlier verified invariant. The runner then runs `quick_check`, writes the new `user_version`, and commits. Any apply, verification, or integrity failure rolls back, leaves the version marker unchanged, logs the error and backup path, and returns failure.

## Data preservation and duplicate-key behavior (DB-002)

- A legacy column cannot be dropped through `SchemaMigrationSql.DropColumnIfExists` without an explicit preservation/emptiness check.
- The Inquiry convergence migration renames known old columns when possible. If both old and current names exist, it fills only blank/default current values, rejects conflicting non-empty values, checks that old non-empty values are present and equal in the current column, and only then drops the old name.
- Unknown Inquiry columns are dropped only after proving every value is NULL or whitespace. Non-empty unknown data aborts the transaction and remains in the database for operator export/mapping.
- Unique-index creation preflights duplicate keys and reports representative values. It does not suppress SQLite's unique-index failure, rewrite identifiers, or renumber externally referenced documents. `RequireIndex` checks the resulting index name, target table, uniqueness, and ordered columns.
- Critical post-migration checks include required columns, non-null/non-empty fields, required indexes, and `quick_check`. `SchemaMigrationRunner` re-runs declared `Verify` callbacks before committing.
- Audit/user migrations remain on the pre-existing additive `AuditDbMigrations` path. That path now checks normalized-name collisions before the unique index and verifies required columns, indexes, username keys, and key data before reporting ready. It does not drop old columns. See Open decisions for the remaining runner-parity gap.

## Failure handling

Migration conflicts are fail-closed: the app does not mark a failed upgrade as complete. A duplicate key or conflicting legacy column requires an operator to inspect the specific backup and source rows, decide how to preserve/resolve the data, and retry. The code intentionally does not invent replacement business identifiers.

`OrderReportRepository.InitializeAsync` also refuses to continue initialization if its schema migration reports failure; it no longer logs and proceeds as if the schema were current.

## Source-level tests

`Tests/Database/SchemaMigrationSafetyTests.cs` exercises: (1) copy/verify/drop of a legacy value and contents of the pre-migration backup; and (2) refusal to create a unique index over duplicate data while leaving both source rows intact. Audit source tests pin normalized-key collision detection and post-migration verification. These tests are present but unrun because `dotnet` is unavailable in the environment.

## Open decisions

1. `audit.db` remains on its additive, hand-written migration path rather than `SchemaMigrationRunner`; it now shares connection policy and has postflight checks, but does not get the runner's pre-migration online snapshot, versioned transaction, or common rollback behavior. Folding it into the runner should be a separately reviewed auth/audit change.
2. Legacy duplicate business keys and non-empty unknown Inquiry fields are deliberately not auto-repaired. Deployment/support needs an approved, backed-up remediation procedure for those data conditions; this source change preserves the data and stops safely.
