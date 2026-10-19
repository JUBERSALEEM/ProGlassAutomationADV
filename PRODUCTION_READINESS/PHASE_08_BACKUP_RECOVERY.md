# Phase 8 — Backup, restore and disaster recovery

**Status:** Backup creation now uses SQLite's online backup API for each database; complete recovery acceptance remains open.

## Implemented in this phase

- `BackupService.CopySqliteFileSet` no longer copies a live `.db`, `-wal`, and `-shm` as independent files.
- It opens the source read-only and uses `SqliteConnection.BackupDatabase` to create a consistent snapshot in the backup set.
- Each copied database must pass `PRAGMA integrity_check` before it is counted as copied.
- On failure, the incomplete target database and sidecars are removed where possible, the failure is returned to the backup-set caller, and the event is logged.

## Still required before relying on backups

- [ ] Add integration tests that write concurrently during backup and validate the snapshot's expected committed rows.
- [ ] Verify every restored database's schema and key business records, not just SQLite page integrity.
- [ ] Verify attachment trees, config, database files and manifests are included in a real restore.
- [ ] Prove restore drills use a unique scratch root and never write to the active database root.
- [ ] Test disk-full, permission-denied, interrupted-copy and corrupted-manifest failure modes.
- [ ] Perform a documented restore to a clean machine/data root and reconcile PI, invoice, delivery, stock and statement totals.
- [ ] Define backup retention, off-device copy, encryption/access control and recovery-point/recovery-time objectives with the business owner.

## Operational rule

A backup is not a recovery guarantee until a restore succeeds and business data is reconciled. Do not mark this phase complete based only on the existence of a backup folder or an SQLite `integrity_check`.
