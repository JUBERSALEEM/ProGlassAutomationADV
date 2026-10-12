# Phase 2 — data-root and SQLite storage policy

## Scope

This note records the source-level storage changes for OPS-001 and DB-001. It does not claim Windows/SMB acceptance; those checks remain pending on a Windows workstation and an isolated test share.

## Shared-root startup selection and offline local mode (OPS-001)

`AppDataRoot.ConfigureFrom` distinguishes intentionally local mode from a configured shared root. It probes the saved UNC/mapped path before any DbContext opens. If the path is valid and reachable, the shared root is selected. If it is empty, invalid, or unavailable at startup, the app selects `%APPDATA%\ProGlassAutomation` as one consistent local root for all modules and continues through normal startup. `SharedRootFallbackActive` and the Settings Data banner disclose that local mode is active; changes made there do not appear on other workstations automatically.

Startup never hot-switches roots after databases have opened. When a configured share is unavailable, a persisted `SharedRootOfflineFallbackNeedsReview` marker prevents the next startup from silently switching to the shared dataset after the network returns. The app probes the saved path again, reports when it is reachable, and stays local until an operator reviews the data and explicitly reconnects in Settings → Data. Local/shared databases are not automatically merged. Prepare Folder can seed an empty shared folder from local data; an existing company database requires a separate reconciliation decision.

Root selections are drafts until the operator accepts the restart prompt. Declining the prompt leaves the saved and active root unchanged; a failed restart restores the prior root settings. This avoids continuing writes on one root while a later launch silently opens another. Startup also stops before opening business databases if it cannot persist a newly created offline-review marker.

The saved path is probed, not discovered by scanning the LAN. Use the real host PC name, e.g. `\\OFFICE-PC\ProGlassData`; `SERVER` in examples is a placeholder. A test-only folder override remains explicit and does not change production selection.

## One path-aware SQLite policy (DB-001)

`SqliteResilience` owns connection-string construction, policy application, and effective-mode verification:

| Database path | Journal mode | Synchronous | Busy timeout | Other connection settings |
|---|---|---|---:|---|
| Local AppData | WAL | NORMAL | 5 seconds | foreign keys enabled, temporary store in memory, local cache/mmap settings |
| Configured, UNC, or mapped shared root | DELETE (rollback journal) | FULL | 15 seconds | foreign keys enabled, default cache; no WAL shared-memory dependency |

Shared-path classification is based on the database file path and configured share, not on successful reachability. During offline fallback, every database path is under local AppData and therefore uses the local WAL profile; the unavailable UNC path is not opened. Once the share is explicitly selected, shared writes fail closed if SQLite cannot apply or verify `DELETE`. Local connections log the required and effective mode and warn if SQLite refuses WAL. Log entries are deduplicated by path/mode until the effective policy changes.

EF contexts call `SqliteResilience.ConfigureFor` and share its connection-open interceptor. Raw writable migration/conversion paths use `BuildConnectionString` plus `ApplyConnectionPolicy`; read-only diagnostics use the matching read-only builder/policy and verify the existing mode without attempting to change it. Shared-root preparation uses the explicit shared override while converting a selected folder before it is active.

The migration runner also applies the same policy to the source database and the pre-migration backup destination. The restore drill uses the central read-only builder/policy. `AuditDbMigrations` remains a separate additive migrator, but its raw connection also uses the central policy.

## Backup/restore boundary

The in-app backup is a managed-file snapshot, not a whole-root archive. It includes top-level SQLite files and existing SQLite sidecars, `config.json` when present in that root, and the three application-owned attachment trees (`Inquiries`, `ProformaInvoices`, and `DeliveryNotes`). It intentionally excludes arbitrary files and nested backup sets.

New backup sets include `backup-manifest.json` with normalized relative paths, lengths, and SHA-256 hashes. Listing checks the inventory and lengths; restore verifies hashes before making the safety set or changing the active root. Restore overlays listed files, reports required-copy failures as incomplete, and does not remove active-root files missing from the set. It also preserves the selected set from retention pruning while creating the pre-restore safety set. Legacy sets without a manifest remain eligible only with an explicit completeness warning.

See [`BACKUP.md`](BACKUP.md) for operator procedures and whole-root recovery.

## Verification status

Source tests cover visible local fallback selection, reconnect-review state, SQLite policy coverage, migration backup/data preservation, and backup inventory validation. They have not been executed in this environment because the .NET SDK is unavailable. Windows path classification, mapped-drive/UNC behavior, SQLite mode conversion, file locking, and restore procedures still require isolated Windows verification.
