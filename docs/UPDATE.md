# ProGlass Automation — Updates

## How updates are delivered

ProGlass Automation uses two update channels, picked at install time:

1. **Velopack OTA (vendor-managed)** — the default for production installs. The installer registers ProGlass with the Velopack update service. The app checks the vendor's GitHub release feed on every launch (and once a day while running) and shows a notification when a new version is available.
2. **None (manual)** — for the portable .zip distribution. The app does not auto-check. You re-download the .zip and replace the folder.

## What the user sees

When a new version is available and your licence is still valid, the **shell's update area** (top-right of the main window) shows a badge. Click it to see release notes. The update panel has three buttons:

- **DOWNLOAD** — pulls the update in the background. The app keeps working while the download progresses.
- **INSTALL & RESTART** — applies the update and relaunches the app. The current session's unsaved data is offered a save prompt first.
- **DISMISS** — the badge goes away; the next check is in 24 hours.

The Settings → Updates panel shows the same controls plus a full release-note view, the current version, the last check time, and the update history (every install / dismiss / error).

## What the update contains

A Velopack update is a **delta package** — only the files that changed between your version and the new one. A 200 MB install typically downloads a 10-30 MB delta. The app applies the delta in-place, restarts, and runs the new code with your existing data.

The update contains:

- The new EXE + DLLs.
- Any new XAML resources or themes.
- The new schema (if any) — see "Schema migrations" below.
- Updated licence verification logic (if any).

The update does **not** contain:

- New sample data (the sample data is only loaded via Settings → Data → Sample Data, never over the air).
- New licences (your existing licence is preserved; it carries forward).
- Any change to your data files unless the schema changed, in which case a migration runs automatically (see below).

## Schema migrations

The app's data lives in eleven SQLite files. The schema of each file evolves over time — a new field in `inquiries.db` here, an index there. Schema changes are versioned; the `SchemaMigrationRunner` runs them automatically on app launch, in a single transaction per file, with a backup snapshot taken before any change.

**Migrations are safe.** If a migration fails, the transaction rolls back and the file is left untouched. The pre-migration backup is in `MigrationBackups\` (per-file, never auto-pruned) and can be restored by hand if needed.

**Migrations never destroy data.** The runner only adds columns / tables / indexes. It can rename a column (to preserve your data) but never drops one. If a column is no longer used in the model, the live file keeps the column for one or two versions and then it is dropped — only after your vendor has run the migration on a staging machine first.

## What happens to your data

A Velopack update is an in-place binary replacement. The data files are not touched by the updater. The new app, on first launch with a new schema version, runs the migration runner against the existing data files. If everything is fine, the app starts normally. If the migration fails, the app refuses to start and points you at the pre-migration backup in `MigrationBackups\`.

## Update channel: stable vs preview

Production installs check the `stable` channel — only tagged releases that the vendor has blessed. Developer / QA installs can opt into the `preview` channel from Settings → Updates to get pre-release builds.

## Offline operation

The update check requires the internet to reach the vendor's GitHub. If you are offline, the check fails silently and is retried in 24 hours. The app does not block on the check; the rest of the app starts normally. You can disable the auto-check entirely in Settings → Updates.

## Verifying the update

After the update restarts the app, open **About → License** and confirm:

- The version pill shows the new version number.
- Your licence is still valid (edition, customer, expiry are unchanged).
- The data is intact (open a known inquiry, confirm the row count matches what you remember).

If anything looks wrong, see `TROUBLESHOOTING.md`. The update is reversible — the Velopack installer keeps the previous version's files in a sibling folder, so a "rollback" is "re-run the previous Setup.exe".

## Authenticode signature

Every Setup.exe and every update delta is Authenticode-signed by your vendor's code-signing certificate. The first time the user runs the Setup, Windows SmartScreen warns them that the publisher is unknown (until the vendor's certificate has built reputation). After a few weeks of installs, the warning disappears. The signature is checked on every update apply — a tampered delta is rejected and the update is aborted.

The verification code is in `Services/Updates/UpdateSignatureVerifier.cs`. It uses `WinVerifyTrust` (the only check that validates the file hash, not just the certificate).
