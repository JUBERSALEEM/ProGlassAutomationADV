# Phase 0 — Windows backup/restore verification checklist

**Purpose:** prove the manual whole-data-root copy restores records created in the actual ProGlass WPF UI. Run only in a disposable Windows VM or a fresh Windows test-user profile. Never use a company/production profile or data root. This checklist is still pending execution; do not record a pass until every required check below is completed on Windows.

The Settings **Restore Selected Backup** action is an overlay, not a clean whole-folder replacement. This test uses File Explorer to replace the complete local root. The optional PowerShell helper is a separate local-only check.

## A. Pre-check — identify and isolate the active root

- [ ] Use a Windows VM or a fresh Windows test account dedicated to this test. Record the Windows version/build (`winver`) and the ProGlass app/build version. If using a VM, take a VM snapshot before starting.
- [ ] Start ProGlass and open **Settings → Data**. Record the displayed **Current Data Root** exactly.
- [ ] Configure **local** data mode: use the Settings control to revert/disable shared data, save, and restart ProGlass if requested. Reopen Settings → Data and confirm the active root is local, with no “LOCAL fallback” warning.
- [ ] Expected local path: `%APPDATA%\ProGlassAutomation` (usually `C:\Users\<test-user>\AppData\Roaming\ProGlassAutomation`). Record its fully expanded path from this test profile. Do not guess or use a configured UNC/mapped share.
- [ ] Confirm the test profile’s `DataConnectivity.UseSharedDataRoot` is false. The optional helper intentionally refuses profiles configured for shared mode, even if the app might have fallen back to local data.

## B. Start with a clean test database and create UI records

- [ ] **Preferred clean start:** use a new Windows test profile or fresh VM. This gives ProGlass a new `%APPDATA%` root without touching another user’s data.
- [ ] If reusing an already isolated test profile, close ProGlass first. Rename its existing `ProGlassAutomation` folder to `ProGlassAutomation_pretest_<date-time>` as a safety copy; do not delete it. Start ProGlass once to initialize the new local root, then close the app before creating the test records.
- [ ] In the ProGlass UI, create and save one record of each type. Put the exact marker in a persistent, visible free-text field (for example, Description, Reference, Remarks, or Notes—use a field actually available in that screen). Record the field name; do not put markers in prices, quantities, tax rates, or formula fields.
  - Inquiry marker: `PHASE0_APP_BACKUP_TEST_INQ`
  - Delivery Note marker: `PHASE0_APP_BACKUP_TEST_DN`
  - Tax Invoice marker: `PHASE0_APP_BACKUP_TEST_TI`
- [ ] Reopen each saved record in the UI before backup. Record its ID/number and verify the marker is still visible.
- [ ] Optional attachment coverage: attach a harmless, clearly named test file to an Inquiry or Delivery Note, record its filename, and verify it opens before backup.

| Document | ID / number | Marker field | Exact marker | Optional attachment filename |
|---|---|---|---|---|
| Inquiry |  |  | `PHASE0_APP_BACKUP_TEST_INQ` |  |
| Delivery Note |  |  | `PHASE0_APP_BACKUP_TEST_DN` |  |
| Tax Invoice |  |  | `PHASE0_APP_BACKUP_TEST_TI` |  |

## C. Manual backup by File Explorer — no PowerShell

- [ ] Exit ProGlass normally. In Task Manager, confirm no `ProGlassAutomation` process remains. The test is local-only, so there should be no other workstations writing to this root.
- [ ] In File Explorer, enter `%APPDATA%` in the address bar. Confirm the source folder name is `ProGlassAutomation` and that it is the path recorded in section A.
- [ ] Copy the **entire `ProGlassAutomation` folder** to a new folder outside the source, such as `Desktop\PG_Phase0Backup`. A convenient layout is `Desktop\PG_Phase0Backup\inquiries.db`—the databases should be directly inside the snapshot folder, not one extra `ProGlassAutomation` level down.
- [ ] Confirm Windows reported no copy errors. Inspect the snapshot and confirm it contains `inquiries.db`, `deliverynotes.db`, `taxinvoices.db`, other present `*.db` files, and the attachment trees/files that exist in the source. Copy any `-wal` / `-shm` sidecars if present; after a clean app exit they may not exist. Do not omit other root files.
- [ ] Keep the snapshot unchanged for the restore. Record the snapshot’s full path and, if available, its folder size.

## D. Manual restore by File Explorer into a fresh root

- [ ] Close ProGlass again and verify no `ProGlassAutomation` process remains.
- [ ] In `%APPDATA%`, rename the current test root to `ProGlassAutomation_pre_restore_<date-time>`. **Do not delete it.** This is the rollback copy.
- [ ] Copy `PG_Phase0Backup` into `%APPDATA%` and rename the copied folder to `ProGlassAutomation`. If Explorer created an extra wrapper level, correct it so the `.db` files are directly under `%APPDATA%\ProGlassAutomation`.
- [ ] Start ProGlass. Before checking records, open **Settings → Data** and confirm the Current Data Root is the exact local path from section A, with no fallback warning.
- [ ] In the UI, open each saved Inquiry, Delivery Note, and Tax Invoice. Confirm each ID/number matches the table in section B and the exact marker is visible in the same field. If an attachment was included, open it too.
- [ ] Record the post-restore result and retain both the original snapshot and `ProGlassAutomation_pre_restore_<date-time>` until the test has been reviewed.

## E. Optional: exercise the PowerShell helper

Run this only in the same isolated local test profile. The app must be closed. The helper refuses a configured shared root, requires a fresh backup destination, and prompts before changing files. Run from the repository checkout root.

1. Create a separate helper snapshot (leave the manual `PG_Phase0Backup` unchanged):

   ```powershell
   $root = Join-Path $env:APPDATA 'ProGlassAutomation'
   $snapshot = Join-Path $env:USERPROFILE 'Desktop\PG_Phase0Backup_Helper'
   .\Tools\DataRootRecovery\ProGlass-LocalDataRoot.ps1 `
     -Action Backup -RootPath $root -SnapshotPath $snapshot
   ```

2. Confirm the helper reports `Backup verified`, the data-root path, and the snapshot path. Do not continue if it reports an error or an inventory mismatch.
3. With ProGlass still closed, restore from that helper snapshot:

   ```powershell
   .\Tools\DataRootRecovery\ProGlass-LocalDataRoot.ps1 `
     -Action Restore -RootPath $root -SnapshotPath $snapshot
   ```

4. At the confirmation prompt, verify the snapshot source and local-root target. On success, expect `Restore verified`, the target data root, and a `ProGlassAutomation_pre_restore_...` path. On error or mismatch, do not open the app; preserve the snapshot and all pre-restore folders and record the full error.
5. Start ProGlass, confirm Settings → Data still points to the local root, then repeat the three record/marker checks in section D.

The helper’s behavior has not been executed or validated on Windows in this development environment. Its successful message is not a substitute for opening the real WPF app and checking the records.

## F. Expected outcomes and failure handling

**Pass only if** all three documents appear in the real ProGlass UI after restore, with the same IDs/numbers and exact marker strings in the recorded fields. If an attachment was included, it must open. Also confirm the app is using the intended local root.

- **A record or marker is missing:** close ProGlass; do not recreate the record or overwrite either safety copy. Check that Settings → Data points to the restored local root, that the snapshot contains the expected database, and that the files are directly in the root rather than nested. Preserve the error/screenshots and ask the Windows developer to investigate before further writes.
- **PowerShell reports a hash/size or file/folder mismatch:** treat the operation as failed. Do not open ProGlass or reuse a damaged destination as a source. Keep the original snapshot unchanged, confirm the app is closed and there is sufficient disk space, and retry only with a new destination. If it repeats, stop and provide the full console output to the Windows developer.
- **ProGlass opens to a blank dataset:** close it immediately. Check Settings → Data for a different root or local-fallback warning; inspect for an extra nested folder level and confirm the restored `.db` files are directly in the active root. Do not create new records in the blank dataset. Keep the pre-restore folder and snapshot intact while correcting the root/configuration.
- **Migration or version error:** stop using the restored root. Prefer the same app build that created the snapshot; never open a database made by a newer build with an older one. Preserve the snapshot before trying a compatible build.

## Record the verification result

- Windows version/build: ____________________
- ProGlass app/build version: ____________________
- Test profile: ____________________
- Active local root: ____________________
- Manual snapshot path: ____________________
- Helper snapshot path (if used): ____________________
- Post-restore UI check (three IDs, markers, attachments): ____________________
- Screenshots/logs or failure details: ____________________

A completed checklist with the record IDs/markers and post-restore result is the required evidence for Phase 0 Windows acceptance.
