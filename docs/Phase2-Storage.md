# Phase 2 — data-root and SQLite storage policy

## Scope

This note records the source-level storage changes for OPS-001 and DB-001. It does not claim Windows/SMB acceptance; those checks remain pending on a Windows workstation and an isolated test share.

## Fail-closed shared-root selection (OPS-001)

`AppDataRoot.ConfigureFrom` distinguishes an intentionally local profile from a profile whose saved settings require a shared root. If the shared path is empty/invalid or cannot be reached and verified, it retains the configured path for diagnostics, sets `SharedRootFallbackActive`, and records an actionable reason. `CurrentFolder` does not substitute `%APPDATA%\ProGlassAutomation` in that state, and `EnsureCurrentFolder` throws `SharedDataRootUnavailableException` before a database context can open a file.

The startup exception identifies the configured folder, states that local AppData will not be opened, and gives network/remediation steps. The Settings Data card exposes the configured/current root and a separate unavailable mode/warning; it does not label a blocked share as an active local or shared root. However, the current startup sequence aborts before the shell/Settings view is created when the shared-root probe fails, so the Settings card cannot be reached during that failure. The warning is correct when the Settings VM is available, but a recovery-only startup route (or an equivalent secure way to reach Data settings without opening business databases) remains an open UX decision. Until then, use the startup error/remediation message and restart after repairing the share; do not switch to local AppData implicitly.

A test-only folder override remains explicit and does not alter production fallback behavior.

## One path-aware SQLite policy (DB-001)

`SqliteResilience` owns connection-string construction, policy application, and effective-mode verification:

| Database path | Journal mode | Synchronous | Busy timeout | Other connection settings |
|---|---|---|---:|---|
| Local AppData | WAL | NORMAL | 5 seconds | foreign keys enabled, temporary store in memory, local cache/mmap settings |
| Configured, UNC, or mapped shared root | DELETE (rollback journal) | FULL | 15 seconds | foreign keys enabled, default cache; no WAL shared-memory dependency |

Shared-path classification is based on the configured root and the path itself, not on successful reachability. A path that is configured for a share therefore never quietly receives the local WAL profile just because the share is offline. Shared writes fail closed if SQLite cannot apply or verify `DELETE`. Local connections log the required and effective mode and warn if SQLite refuses WAL. Log entries are deduplicated by path/mode until the effective policy changes.

EF contexts call `SqliteResilience.ConfigureFor` and share its connection-open interceptor. Raw writable migration/conversion paths use `BuildConnectionString` plus `ApplyConnectionPolicy`; read-only diagnostics use the matching read-only builder/policy and verify the existing mode without attempting to change it. Shared-root preparation uses the explicit shared override while converting a selected folder before it is active.

The migration runner also applies the same policy to the source database and the pre-migration backup destination. The restore drill uses the central read-only builder/policy. `AuditDbMigrations` remains a separate additive migrator, but its raw connection also uses the central policy.

## Backup/restore boundary

The in-app backup is a managed-file snapshot, not a whole-root archive. It includes top-level SQLite files and existing SQLite sidecars, `config.json` when present in that root, and the three application-owned attachment trees (`Inquiries`, `ProformaInvoices`, and `DeliveryNotes`). It intentionally excludes arbitrary files and nested backup sets.

New backup sets include `backup-manifest.json` with normalized relative paths, lengths, and SHA-256 hashes. Listing checks the inventory and lengths; restore verifies hashes before making the safety set or changing the active root. Restore overlays listed files, reports required-copy failures as incomplete, and does not remove active-root files missing from the set. It also preserves the selected set from retention pruning while creating the pre-restore safety set. Legacy sets without a manifest remain eligible only with an explicit completeness warning.

See [`BACKUP.md`](BACKUP.md) for operator procedures and whole-root recovery.

## Verification status

Source tests cover fail-closed root selection, SQLite policy coverage, migration backup/data preservation, and backup inventory validation. They have not been executed in this environment because the .NET SDK is unavailable. Windows path classification, mapped-drive/UNC behavior, SQLite mode conversion, file locking, and restore procedures still require isolated Windows verification.
