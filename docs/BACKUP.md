# ProGlass Automation — Backup and Restore

## What gets backed up

A backup set is a single timestamped folder containing a copy of every business data file in `%AppData%\ProGlassAutomation\`:

| File                              | What it contains                              |
|-----------------------------------|-----------------------------------------------|
| `proglass.db`                     | Sheet Store (glass inventory).                 |
| `inquiries.db`                    | Customer inquiries.                            |
| `proformainvoices.db`             | Proforma Invoices.                             |
| `joborders.db`                    | Job Orders.                                    |
| `deliverynotes.db`                | Delivery Notes.                                |
| `taxinvoices.db`                  | Tax Invoices.                                  |
| `statements.db`                   | Account statements.                            |
| `masterdata.db`                   | Customers, projects, salesmen, glass types.   |
| `audit.db`                        | User accounts + activity log.                  |
| `config.json`                     | User preferences (theme, accent, …).           |
| `Inquiries\…\Attachments\…`        | Inquiry attachment files (PDFs, images, …).    |
| Same pattern for the other modules | (Jobs\…, DeliveryNotes\…, …).                  |
| `BackupManifest.json`             | Index of what is in this backup.                |

**Not** backed up: `logs\` (the technical log is for the current install only), `MigrationBackups\` (those are pre-migration snapshots, automatically managed), and `license.lic` (your licence stays on the active install — re-issuing one is a single email).

## When backups happen

- **Automatic — on app exit.** Every time the app closes cleanly, the most recent backup is the most recent exit. The default is to keep the last 7 sets; older sets are auto-pruned. The schedule is configurable in `Settings → Data → Backup & Restore`.
- **Manual — from the Settings panel.** Click **CREATE BACKUP NOW** for an on-demand snapshot. The app prompts if a backup was taken in the last few minutes, to avoid flooding the list.
- **Before risky operations** — the database rebuild tool in `Settings → Data → Database Health` and the schema-migration runner both take a backup of the affected file first. The snapshot goes to `MigrationBackups\` and is never auto-pruned.

## Where backups live

Default: `%USERPROFILE%\Documents\ProGlassAutomation\Backups\<yyyyMMdd_hhmmss>\`.

A backup set is a folder, not a zip — easier to inspect, easier to restore a single file by hand. To change the location, edit `config.json` (`backupFolder` key) or use the Settings → Backup panel.

## Restoring a backup

1. Open **Settings → Data → Backup & Restore**.
2. Pick a backup set from the list. Each entry shows when it was taken, how many files it contains, and its total size.
3. Click **RESTORE SELECTED BACKUP**. The app shows a confirmation dialog: "Restore backup from [timestamp]? This OVERWRITES all current data with the backup. A safety snapshot of the current data is taken first."
4. Click **Yes**. The app takes a fresh backup of your CURRENT state (so you can roll back if the restore was wrong), then overwrites every file with the backup's contents.
5. The app tells you a restart is required. The restored data is loaded on next launch.

## Restoring on a different machine

A backup set is a plain folder. To move it to a new machine:

1. Copy the backup folder to a USB drive or to the new machine over the network.
2. On the new machine, install ProGlass Automation (see `INSTALL.md`).
3. **Stop the app** before copying — SQLite is sensitive to file-locking; if the app is running, the restore may produce a "database is locked" error.
4. Copy the backup folder into `%AppData%\ProGlassAutomation\Backups\`.
5. Launch the app, go to Settings → Data → Backup & Restore, and restore.

## Verifying a backup

A backup is just a folder of files — to verify, you can open any of the `.db` files with the [DB Browser for SQLite](https://sqlitebrowser.org) tool and confirm the tables look right. The `BackupManifest.json` is a human-readable JSON file that lists every file the backup contains and its size; open it in Notepad for a quick sanity check.

## Restoring a single file

If you only need to restore one file (for example, the audit log got corrupted and you want last week's):

1. Open the backup folder in Explorer.
2. Find the file (e.g. `audit.db`).
3. Copy it to `%AppData%\ProGlassAutomation\` (overwriting the current one). **Stop the app first.**
4. Launch the app.

This is an "advanced" flow — for routine full-set restores, use the Settings panel.

## Disaster scenarios

**Power outage mid-write.** The app's SQLite mode is journaled; a crash mid-write is recovered on next launch. The data folder is the source of truth; a backup is the second-level safety net.

**Disk failure.** The whole data folder lives on one drive. If the drive dies, so does the data. **Set up a scheduled copy of `%AppData%\ProGlassAutomation\` to a second drive or a network share** — that is the third-level safety net. Windows Task Scheduler can do this with a one-line PowerShell script. Your vendor can ship a turnkey task with the installer.

**Ransomware.** Your backups are read-only (a backup set is never written to once taken) but they live on the same drive as the data. If ransomware encrypts the entire drive, both the data and the backup are gone. **The right mitigation is off-machine backups** — your vendor can configure a daily push to a remote storage bucket on the first install.

## Backup history

The Settings panel shows the last N backup sets (default: most recent 7). The list is a window onto the disk — older sets that were auto-pruned are gone, and there is no "recover pruned" feature. The history is for sanity-checking ("yes, I have a backup from yesterday"), not for auditing.
