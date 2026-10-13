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
- Prepare Folder creates a safety backup before seeding a shared root; seeding requires every local database WAL checkpoint to succeed and stages the copy before promoting files into the live share;
- prepared shared roots receive `proglass_shared_root.json`, making it clear the folder is a controlled ProGlass company data root;
- Test Connection reports whether database files and the shared-root manifest already exist.

### Settings UI
Settings → Data now has a **Company LAN / Wi-Fi Database** panel:

- create/publish a host share on the primary PC or find a ProGlass share from a client;
- enter a UNC/mapped path by real host name or IPv4 address;
- choose an explicit discovery candidate and test read/write access;
- prepare only the primary PC's own ProGlassData share; a client cannot run preparation against a different host, and local-data seeding requires the explicit checkbox plus an empty share (copy is off by default);
- **Save + Restart** explicitly enables the tested shared root; **Revert to local workstation database** is the only action that saves local mode. The root setting is committed only when the workstation can restart; declining or failing the restart leaves the previously active/saved root in place. Folder preparation can succeed without switching the live root.

### Primary-PC share creation and client discovery
The Data card now has separate host and client actions:

- On the primary Windows PC, **Create Share on This PC** creates a data-only folder under Public Documents by default, writes the ProGlass manifest, then asks Windows for administrator approval to publish the `ProGlassData` SMB share. A local `C:\...` directory is never presented as a client-ready path. On success the card shows `\\HOSTNAME\ProGlassData` plus verified private IPv4 alternatives; reserve the IP in DHCP if clients must rely on it.
- Share-level access is granted to **Authenticated Users** with Change rights, and NTFS Modify rights are applied to the data folder and its existing contents. The helper adds an inbound TCP 445 firewall rule limited to `LocalSubnet` and the Windows **Private/Domain** profiles. It refuses to open SMB on a Public profile. The share is considered ready only after share/ACL/firewall setup and a read/write probe through the UNC path succeed.
- On client PCs, **Find on LAN** scans only local RFC1918 IPv4 subnets (up to about 2,048 candidate addresses, with a 30-second safety limit) for SMB/TCP 445 hosts with a ProGlass shared-root manifest. Results are candidates, not an automatic connection. Choose one explicitly, **Use Selected Path**, **Test Connection**, review the manifest and database filenames to confirm the intended company dataset, then **Save + Restart**. The scan does not select, merge, copy, or switch roots. If discovery is blocked, type `\\HOSTNAME\ProGlassData` or `\\192.168.1.25\ProGlassData` directly.
- Windows SMB requires authenticated host/domain credentials; the operator may need to sign in with a Windows account permitted on the host (or use Windows Credential Manager). Do not enable Guest/anonymous access. The primary PC must remain on and awake. Public Wi-Fi is not supported; use a trusted Private or Domain network. Third-party firewalls or domain policies may still block SMB and must allow TCP 445 from the local subnet.

### Startup and offline behavior
The app probes the saved path at startup. If it is unavailable, ProGlass continues on this workstation's local AppData database so the application can launch. A visible warning makes clear that local changes do not appear on other workstations. The app does not change roots mid-session. If a workstation used local mode while the share was offline, the next launch probes the saved path but stays local until the operator reviews the local dataset and explicitly reconnects. The local and shared databases are not automatically merged. Prepare Folder can copy local data into an empty share; do not use that action to seed or overwrite an existing company dataset. `\\SERVER` is only a sample placeholder: primary PCs create a local share, while clients use the real host name/IP or Find on LAN.

### Security/process-open support
`ProcessStartGuard` now allows files/folders under the configured shared data root, including UNC paths, while still blocking arbitrary UNC paths outside the approved root.

## Operational guidance
Recommended setup:

1. Choose one stable primary Windows PC/server that remains powered on and connected to the office LAN/Wi-Fi. Set that trusted network to **Private** (or use a managed **Domain** profile); never expose SMB on Public Wi-Fi.
2. On the primary PC, open Settings → Data → Company LAN / Wi-Fi Database and click **Create Share on This PC**. Approve the Windows administrator/UAC prompt. The app creates a dedicated local folder, applies Authenticated Users share Change + NTFS Modify permissions, and adds the local-subnet TCP 445 firewall rule. Note the returned `\\HOSTNAME\ProGlassData` path and any IP fallback. If UAC is cancelled, the directory remains local and is not ready for clients.
3. On the primary PC only, click **Prepare Folder** if the intended company database needs to be seeded into an empty share. Review the safety-backup and copy prompts, and close ProGlass on all other workstations while preparation changes shared journal settings. Copy-local is off by default; do not seed an already populated company share or attempt to merge divergent workstation databases.
4. On each client PC, click **Find on LAN** and explicitly choose the correct candidate, or enter `\\HOSTNAME\ProGlassData` / `\\192.168.1.25\ProGlassData`. If Windows asks, sign in with a host/domain account that has share and NTFS write access. The scan is local-subnet only; manual hostname/IP entry remains available.
5. Click **Use Selected Path** when discovery found the host, then **Test Connection**. Verify the intended company dataset (database count and manifest) before switching.
6. Click **Save + Restart** on each workstation and accept the prompt. This saves shared mode on that PC only; it does not copy/merge its local database. Use **Revert to local workstation database** only when deliberately moving a workstation back to its own local dataset.
7. If connection fails, check the host is awake/on the same network, the active Windows network profile is Private/Domain, TCP 445 is permitted from the local subnet by Windows/third-party firewalls and domain policy, and the client credentials are valid. Avoid cloud-sync folders such as OneDrive/Google Drive/Dropbox for live DB files.

For high concurrency or unreliable Wi-Fi, a real client/server database remains the stronger architecture, but this upgrade makes same-network shared-folder operation much safer and more transparent for a small office.

## Validation
- C# and embedded PowerShell syntax parsers reported no syntax errors in the changed sources; these are not semantic compilation or Windows runtime checks.
- XAML well-formedness check passed.
- `git diff --check` passed.
- Conflict-marker scan passed.
- .NET build and tests were not run because the sandbox does not have the .NET SDK installed.
- Windows UAC, SMB/NTFS permissions, firewall-profile changes, and cross-PC discovery still require a controlled validation on real Windows machines joined to the intended office network.
