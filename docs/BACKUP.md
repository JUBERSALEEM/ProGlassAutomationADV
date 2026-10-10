# ProGlass data-root backup and restore (Windows)

This guide distinguishes the **in-app backup set** from a **whole data-root copy**. For a clean recovery to a fresh local profile, use a whole-root copy or the local helper below; the Settings restore button overlays the files in one app backup set and does not remove unrelated files.

## Locate the active data root first

- Local mode: `%APPDATA%\ProGlassAutomation` (normally `C:\Users\<user>\AppData\Roaming\ProGlassAutomation`).
- Shared mode: the exact path saved in `DataConnectivity.SharedDataRootPath` in each Windows user's `%APPDATA%\ProGlassAutomation\config.json`. It may be a UNC path or a mapped drive. Confirm the **Current Data Root** in **Settings → Data**; never infer the active root just from a saved share path.
- If a configured share is unavailable or invalid when ProGlass starts, database access is blocked; the workstation does **not** open local AppData. Startup reports the configured path and remediation message. When the application is running, Settings → Data shows the configured root, unavailable mode, and warning. Restore access and restart before backup/restore.
- Demo data is separate: `%APPDATA%\ProGlassAutomation.Demo`.

The SQLite files are top-level files in the active root. Current contexts use `proglass.db`, `inquiries.db`, `proformainvoices.db`, `joborders.db`, `production.db`, `deliverynotes.db`, `taxinvoices.db`, `creditnotes.db`, `statements.db`, `specifications.db`, `masterdata.db`, `orderreports.db`, `audit.db`, `aimemory.db`, and `optimization.db`. SQLite sidecars can be present beside them as `<database>.db-wal` and `<database>.db-shm`; copy the whole folder so no sidecar is accidentally omitted.

Application-managed attachments are stored at:

- `Inquiries\<InquiryId>\Attachments\...`
- `ProformaInvoices\Attachments\<ProformaId>\...` (manifests, images, PDFs)
- `DeliveryNotes\Attachments\Images\...` and `DeliveryNotes\Attachments\PDFs\...`
- Tax Invoice attachment bytes are in `taxinvoices.db`.

Exports saved by users outside the active data root (for example, a PDF saved to Documents) need their own backup. The whole root also contains settings and other app files such as `recordssync.json` and `recordssync-state.json`, which the in-app backup-set whitelist does not include.

## Manual whole-root backup — recommended

**Close ProGlass on every workstation that can use the root before copying.** For local mode, close every ProGlass instance using that Windows profile. For a shared root, every client must be closed; a local workstation cannot prove that another workstation has stopped writing.

1. In **Settings → Data**, record the **Current Data Root** and confirm there is no local-fallback warning.
2. Exit ProGlass normally on all clients. Confirm no `ProGlassAutomation` process remains.
3. In File Explorer, copy the entire active root folder—every file and subfolder—to a dated destination outside the root, preferably on encrypted removable or otherwise protected storage. Do not copy it into its own `Backups` folder.
4. Confirm the copy completed without errors. Check for the `.db` files, any `-wal` / `-shm` sidecars, the `Inquiries`, `ProformaInvoices`, and `DeliveryNotes` trees, and any other root-level files.
5. For a shared root, separately preserve each workstation's `%APPDATA%\ProGlassAutomation\config.json` if workstation settings are needed. `secrets.dat` (and `secrets.dat.bak`) is DPAPI-protected for that Windows user/profile; do not rely on it on another account or machine. Re-enter those credentials there.
6. Keep the dated copy separate from the live root and retain it until a later backup is verified.

The in-app sets are stored **inside the active root** at `<ActiveRoot>\Backups\set_<timestamp>`. They are useful as an additional recovery point, not an independent/off-machine backup.

## Optional local Windows copy/restore helper

`Tools\DataRootRecovery\ProGlass-LocalDataRoot.ps1` performs the same whole-root copy for the standard **local** `%APPDATA%\ProGlassAutomation` path. It requires an explicit root path, checks that shared mode is not configured, checks that ProGlass is closed, rejects a snapshot nested under the live root, and verifies the copied folder inventory plus every file's size and SHA-256. Restore stages and verifies the snapshot before replacing the local root; the previous root is renamed aside and retained.

Run it interactively from the repository root, choosing a new dated destination:

```powershell
.\Tools\DataRootRecovery\ProGlass-LocalDataRoot.ps1 `
  -Action Backup `
  -RootPath "$env:APPDATA\ProGlassAutomation" `
  -SnapshotPath "$env:USERPROFILE\Desktop\PG_Backup_YYYYMMDD_HHMM"
```

To restore that snapshot to the same local profile:

```powershell
.\Tools\DataRootRecovery\ProGlass-LocalDataRoot.ps1 `
  -Action Restore `
  -RootPath "$env:APPDATA\ProGlassAutomation" `
  -SnapshotPath "$env:USERPROFILE\Desktop\PG_Backup_YYYYMMDD_HHMM"
```

The helper is deliberately **local-only**. It refuses shared or mapped-network roots and refuses a profile whose `config.json` enables shared-root mode, because the app may have fallen back to local data. For shared mode, confirm the active path in Settings → Data, close all workstations, and use a file-server-aware whole-root copy/restore while preserving share permissions and path. The helper has not been executed on Windows in this development environment; validate it on an isolated Windows profile before relying on it.

## Manual restore into a fresh local environment

1. Confirm the snapshot is the intended one and note the ProGlass app version used to create it. Prefer the same app version. Never open a database with an older build than the one that created it; a newer build may apply forward migrations, so retain the original snapshot before first launch.
2. Exit ProGlass and confirm no `ProGlassAutomation` process remains.
3. Preserve the current root before replacing anything. The helper above creates a staged copy and renames the old root to `ProGlassAutomation_pre_restore_<timestamp>_<id>`. If using File Explorer, make a verified safety copy outside the root first, then rename the existing root instead of merging over it.
4. Copy the **contents of the snapshot** into `%APPDATA%\ProGlassAutomation` so the databases remain directly in that folder (avoid an accidental nested `ProGlassAutomation\ProGlassAutomation` directory). Do not omit sidecars or attachment trees.
5. Before opening the app, confirm the restored profile is in local mode. A restored `config.json` that enables shared mode can redirect ProGlass to a network root. If Settings → Data later shows a local-fallback warning, close the app and resolve the share configuration before continuing.
6. Open ProGlass on one workstation first. Confirm the intended Inquiry, Delivery Note, Tax Invoice, and their marker text/attachments. Keep the safety copy until these checks pass; then reconnect any other clients.

For a shared-root restore, close **all** clients, keep the share path and permissions stable, make a separate safety copy of the current share, and replace its contents with the selected snapshot. Do not rename the share root itself unless the file-server administrator also updates the share and every workstation's configured path.

## What the in-app backup and restore actually do

- In-app sets are stored at `<ActiveRoot>\Backups\set_<timestamp>` and copy top-level `*.db` files plus existing `-wal` / `-shm` sidecars, `config.json` if present in that root, and the three whitelisted attachment trees above. Tax Invoice blobs travel inside `taxinvoices.db`. New sets also include `backup-manifest.json`, an inventory of relative paths, file sizes, and SHA-256 hashes. Settings checks paths, lengths, and unlisted files when listing; SHA-256 is explicitly marked pending and verified for every file before the safety set or restore starts. Missing, extra, truncated, or same-size-but-changed files are rejected before active-root writes. Older sets without a manifest remain selectable with an explicit completeness warning.
- They do **not** copy arbitrary root-level JSON files, per-user settings stored outside a shared root, or an external copy of the root. They remain on the same storage as live data.
- The Settings restore button first tries to create a safety set, then overlays files from the selected set onto the active root. Files absent from the set are left in place. The result now reports copy failures as incomplete instead of returning a success message. For a clean full-root replacement, close the app and use the manual/PowerShell procedure.
- The restore drill hashes backup-set files and runs SQLite `PRAGMA integrity_check` in a scratch folder. It now includes external attachment trees in the hash-copy check. It does not launch WPF or prove that actual records open in the UI. Running the drill adds a new backup set to the active root.

## Phase 0 Windows verification

For the practical Windows/WPF record-creation, whole-root copy, restore, and post-restore UI checks, follow `docs/Phase0-BackupVerificationChecklist.md`.

A prior isolated Linux SQLite file-copy smoke test used synthetic tables. It checked file-copy mechanics only; it did not use ProGlass schemas, launch the WPF app, or verify real records in the UI. It is **not** acceptance evidence. Phase 0 backup/restore acceptance remains pending until the Windows checklist is completed in an isolated local-only test profile and the same record IDs and marker strings are confirmed after restore.
