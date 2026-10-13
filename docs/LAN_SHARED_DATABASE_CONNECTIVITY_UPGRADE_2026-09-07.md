# Shared ProGlass Data Folder — LAN / Wi-Fi Setup

## How the shared root works

ProGlass routes its shared-capable databases and related data folders through `AppDataRoot.CurrentFolder`. Each workstation stores its own root selection. To work on one company dataset, every workstation must use the same existing UNC path or mapped network drive, for example:

- `\\OFFICE-PC\ProGlassData`
- `\\192.168.1.25\ProGlassData`
- `Z:\ProGlassData` (if the same mapped drive is available on every workstation)

A shared SQLite folder is not a database server. The app keeps its network-safe SQLite profile (DELETE journal mode, FULL synchronous, extended busy timeout) for shared roots; local roots retain the local profile. A root switch requires a restart so active database contexts do not split between locations.

## Simple setup flow

1. On the host PC, create the data folder and configure Windows folder sharing with File Explorer. Make sure the Windows account used by each workstation can read and write the folder. Keep the host powered on and reachable over the office LAN/Wi-Fi or the same remote/VPN network.
2. In ProGlass **Settings → Data**, browse to the existing shared folder or type its UNC path / mapped drive. Use the same path on every workstation, including the host PC.
3. Select **Test Connection**. ProGlass verifies that this workstation can read, write, and delete a temporary test file, then removes that file. It reports how many top-level ProGlass database files it found; an empty folder needs extra care because it may be a new or unintended dataset.
4. After the test succeeds, select **Apply and Restart** and confirm the intended company folder. Repeat on each workstation.

The folder browser is only a way to select a location; it does not publish a Windows share. ProGlass does not create SMB shares, request administrator/UAC approval, modify Windows permissions or firewall settings, or scan/discover computers. Configure sharing and account access with Windows tools. If the folder is not reachable, check the UNC path, host availability, Windows credentials, folder sharing, and network connection.

## Data safety

- Applying a root never copies, merges, or overwrites database files. Confirm that the tested folder contains the intended company dataset before applying it. A folder with no database files can open or create a separate empty dataset.
- The selected root remains a draft until the operator tests it, confirms the change, saves, and restarts. A failed save/restart attempts to restore the prior root settings.
- If a saved network folder is unavailable at startup, ProGlass stays usable on that workstation's local AppData database and displays a warning that those local changes are not shared. It does not switch roots during a live session.
- If the network returns after local work, ProGlass remains in local mode until the operator reviews the local data and explicitly reconnects. Reconnecting does not merge the local and shared datasets.
- **Return to local workstation database** deliberately saves local mode for that workstation after confirmation and restart; other workstations remain unchanged.
- Avoid cloud-sync folders (OneDrive, Google Drive, Dropbox) for live database files. For high concurrency or unreliable Wi-Fi, a client/server database is a stronger architecture.

## Validation notes

- Tests cover path normalization, folder access probing, no-create/no-overwrite behavior, network-path validation, configuration safeguards, and offline fallback warnings.
- The .NET SDK and Windows runtime are not available in the Linux sandbox, so build/tests and Windows SMB behavior require validation on a Windows workstation.
