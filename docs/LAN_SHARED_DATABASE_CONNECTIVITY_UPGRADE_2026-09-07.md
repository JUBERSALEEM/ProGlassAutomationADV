# LAN / Wi-Fi Shared Database Connectivity Upgrade — 2026-09-07

## Senior analysis
The app already centralized most business SQLite paths through `AppDataRoot.CurrentFolder`, but several important paths still used local `%AppData%` directly. That meant each workstation could silently run its own separate dataset even when users expected the whole company to work from one database.

A direct SQLite file on a Wi-Fi/LAN share is not the same as a database server. It can work for a small office on a stable Windows/SMB share, but it needs professional safeguards:

- every workstation must point to the exact same shared folder;
- the folder must be read/write/delete tested before enabling;
- live SQLite WAL mode must not be used on a network share because WAL shared-memory behavior is unsafe across SMB/NAS;
- all business attachments must follow the same shared data root;
- the switch must require restart so active DbContexts do not split between old and new roots;
- audit must record which workstation changed connectivity settings.

## Implementation
### Shared data root configuration
Added `DataConnectivitySettings` in app config:

- `UseSharedDataRoot`
- `SharedDataRootPath`
- `CopyLocalDataOnEnable`
- `WorkstationName`

`App.StartupCore` now applies `AppDataRoot.ConfigureFrom(Config)` immediately after config load and before any DbContext opens.

### Central root routing
Updated the central data root so all shared-capable databases resolve through `AppDataRoot.CurrentFolder`:

- proglass main DB
- inquiry DB
- proforma DB
- specifications DB
- job order DB
- delivery note DB
- master data DB
- tax invoice DB
- statement DB
- order report DB
- audit DB
- AI memory DB
- PI/DN attachment folders used by statement workflow
- backup folder under the active data root

### Network-safe SQLite mode
`SqliteResilience` now uses two profiles:

- Local profile: WAL + NORMAL synchronous + 5s busy timeout.
- Shared-root profile: DELETE journal + FULL synchronous + 15s busy timeout.

This avoids unsafe WAL behavior over a shared LAN/Wi-Fi folder while still keeping foreign keys and lock retries.

### Safety hardening added after senior review
The follow-up senior pass added three important production safeguards:

- backup sets now include SQLite live sidecar files (`*.db-wal`, `*.db-shm`) so local WAL-mode databases are not backed up stale/incomplete;
- Prepare Folder creates a safety backup before seeding a shared root;
- prepared shared roots receive `proglass_shared_root.json`, making it clear the folder is a controlled ProGlass company data root;
- Test Connection reports whether database files and the shared-root manifest already exist.

### Settings UI
Settings → Data now has a **Company LAN / Wi-Fi Database** panel:

- enable/disable shared company database folder;
- enter UNC/mapped path, e.g. `\\OFFICE-PC\ProGlassData` (replace OFFICE-PC with the host PC name);
- workstation name for audit/support;
- test connection;
- prepare folder;
- copy local data if the shared folder is empty;
- apply a root change with explicit restart guidance. The root setting is committed only when the workstation can restart; declining or failing the restart leaves the previously active/saved root in place. Folder preparation can succeed without switching the live root.

### Startup and offline behavior
The app probes the saved path at startup. It does not scan arbitrary LAN shares because Windows discovery is not reliable and could select the wrong company data. Use the real host computer name (for example `\\OFFICE-PC\ProGlassData`); `SERVER` is only a placeholder in examples.

If the configured share is unavailable, ProGlass continues on this workstation's local AppData database so the application can launch. A visible warning makes clear that local changes do not appear on other workstations. The app does not change roots mid-session. If a workstation used local mode while the share was offline, the next launch probes the saved path but stays local until the operator reviews the local dataset and explicitly reconnects. The local and shared databases are not automatically merged. Prepare Folder can copy local data into an empty share; do not use that action to overwrite an existing company dataset.

### Security/process-open support
`ProcessStartGuard` now allows files/folders under the configured shared data root, including UNC paths, while still blocking arbitrary UNC paths outside the approved root.

## Operational guidance
Recommended setup:

1. Create a Windows shared folder on a stable office PC/server/NAS, e.g. `\\OFFICE-SERVER\ProGlassData`.
2. Give ProGlass users modify permissions.
3. On the first workstation, open Settings → Data → Company LAN / Wi-Fi Database.
4. Enter the shared path, enable copy-local-data if this workstation has the current master data, and click **Prepare Folder**. Review the backup/copy confirmation.
5. Accept the restart prompt to save and activate the prepared root on that workstation. If you decline, the folder stays prepared but the active root is unchanged.
6. On every other workstation, enter the same shared path, disable copy if the folder is already populated, then click **Save + Restart** and accept the prompt.
7. Avoid cloud-sync folders such as OneDrive/Google Drive/Dropbox for live DB files.

For high concurrency or unreliable Wi-Fi, a real client/server database remains the stronger architecture, but this upgrade makes same-network shared-folder operation much safer and more transparent for a small office.

## Validation
- C# syntax parsing passed for the changed source files; no compile-risk findings were reported.
- XAML well-formedness check passed.
- `git diff --check` passed.
- Conflict-marker scan passed.
- .NET build and tests were not run because the sandbox does not have the .NET SDK installed.
