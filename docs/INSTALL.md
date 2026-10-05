# ProGlass Automation — Installation

## System requirements

- **OS:** Windows 10 (1809 or later) or Windows 11. **x64 only.**
- **.NET:** .NET 8 Desktop Runtime. The installer offers to download and install it for you if it is missing.
- **Disk:** 600 MB for the install. 1 GB+ recommended for working data (one year of a busy glass factory: ~50,000 rows, ~3,000 PDFs).
- **RAM:** 4 GB minimum, 8 GB recommended. The app streams sheets / proformas into memory on first open, so a low-RAM machine still works but a high-RAM one feels instant.
- **Display:** 1366×768 minimum. The shell uses 1280×800 as the design baseline; smaller screens get a scrollable shell.
- **SQLite:** bundled. No external database server is required.
- **PDF / Excel engines:** bundled (QuestPDF + ClosedXML). No Microsoft Office or Adobe install needed.

## Installation

### From a Setup.exe (vendor-issued)

1. Double-click the Setup.exe your vendor sent you.
2. Windows SmartScreen will warn you that the file is from an unknown publisher (until the vendor's code-signing certificate is set up — see the signing notes in your vendor's release kit).
3. Click **More info → Run anyway** to continue.
4. Read the readme, then accept the licence terms.
5. Choose who the install is for: just you, or all users. All users asks Windows for administrator approval. There is no separate data-folder page. Records stay in `%AppData%\ProGlassAutomation\`.
6. Click **Install**. The wizard copies the files, then you can launch ProGlass.
7. FFmpeg and the offline AI model are not in this wizard. The app offers them once, after sign-in. Nothing downloads until you click Install.
8. On first launch, the welcome view appears — see `FIRST_RUN.md`.

### From a portable .zip (developer build)

1. Extract the zip to a folder of your choice, e.g. `D:\ProGlass\`.
2. Run `ProGlassAutomation.exe` from that folder.
3. The data folder is the **same folder as the .exe** by default (portable install). The `Velopack` updater is not active in portable mode.
4. To move to a different machine, copy the whole folder.

## Where the data lives

The app uses `%AppData%\ProGlassAutomation\` for the canonical install:

```
%AppData%\ProGlassAutomation\
├── proglass.db              # Sheet Store
├── inquiries.db
├── proformainvoices.db
├── joborders.db
├── deliverynotes.db
├── taxinvoices.db
├── statements.db
├── masterdata.db
├── audit.db                 # Activity log (login, create, edit, delete, …)
├── aimemory.db              # AI Assistant long-term memory
├── specifications.db
├── config.json              # User preferences (theme, accent, font, …)
├── license.lic              # Your licence (drop here, or use the welcome view)
├── license-keys.json        # Vendor-only, NEVER committed
├── activations.json         # Per-device activation count, vendor-only
├── Inquiries\                # Attachment storage (manifest.json + files)
│   └── {id}\
│       └── Attachments\
├── Jobs\ …                  # Same pattern for every module
├── Backups\                  # Local backup sets (auto + manual)
├── logs\                     # Technical log + crash reports
│   ├── app-yyyymmdd.log
│   └── crash-yyyymmdd_hhmmss.txt
└── MigrationBackups\         # Pre-migration snapshots, never deleted automatically
```

## Verifying the install

1. Open the app and sign in with the seeded admin (created on first run).
2. Open **About → License**. You should see your edition, customer, expiry, and a recent crash-report count.
3. Open **Settings → Data → Database Health**. Click **REFRESH** — every row should show a row count greater than zero (or "Not created" if you have never opened that module).
4. Create a test inquiry, attach a PDF, save, then re-open it. The attachment should still be there.

## Uninstall

The vendor-issued Setup.exe registers ProGlass with Windows Add/Remove Programs. **Two options:**

1. **Standard uninstall** — Windows Settings → Apps → ProGlass Automation → Uninstall. This removes the app, the licence, and any scheduled backup tasks. **It does NOT touch your data folder.** Reinstalling picks up your existing data.
2. **Full uninstall** — same as above, then manually delete `%AppData%\ProGlassAutomation\`. **This deletes all of your data — export a backup first.**

## Silent install (for IT departments)

The vendor-issued Setup.exe supports the standard Velopack silent flags:

```
ProGlassAutomation-1.2.3-Setup.exe --silent --install --norestart
```

For mass deployment, package the silent-install command into your RMM tool of choice (Intune, Workspace ONE, etc.).

## Upgrading an existing install

Re-run the Setup.exe over the existing install. The Setup detects the previous version, runs schema migrations, and launches the new version with your existing data intact. **Your licence file is preserved.**

If you have an old install on the same machine and you want a fresh one (for testing a new release before pushing it to the rest of the team), use the portable zip instead.

## Logs and crash reports

If something goes wrong, the first two places to look:

- **%AppData%\ProGlassAutomation\logs\app-yyyymmdd.log** — the rolling technical log for today. Open in Notepad.
- **%AppData%\ProGlassAutomation\logs\crash-latest.txt** — the most recent unhandled crash. Open in Notepad and share with support (see `TROUBLESHOOTING.md`).

The **About → Recent crash reports** card in the app shows the last 10 with parsed error IDs.

## Telemetry

ProGlass Automation **does not call home** for any reason. There is no analytics SDK, no usage ping, no remote crash reporter (unless your vendor has configured one — see `Settings → Updates` or ask your vendor). The app is fully offline-capable.
