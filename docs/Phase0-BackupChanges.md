# Phase 0 backup/restore — change summary

**Status: awaiting real Windows verification.** These changes are prepared for review; this Linux session could not build or run the `net8.0-windows` WPF app or execute PowerShell.

## Files changed

| File | Change and reason |
|---|---|
| `Services/Database/DatabaseBackup/BackupService.cs` | Backup-set names are unique; a set with a copy error or no database is rejected and the partial set is removed. Restore preserves the selected older set while creating its safety set, reports partial copies as failures with the safety-set path, and explicitly remains an overlay rather than a whole-root replacement. |
| `Services/Database/DatabaseBackup/RestoreDrillService.cs` | The scratch restore drill now copies and hashes the managed attachment trees as well as top-level files, disables SQLite pooling during short read-only integrity checks, and returns a failure result when the verification report is not clean. |
| `Tests/Agent/RestoreDrillServiceTests.cs` | Previously skipped isolated component tests were enabled; their SQLite fixtures use non-pooled connections so temporary files can be released on Windows. These tests were not run in this environment. |
| `ViewModels/Settings/SettingsViewModel.Helpers.Data.cs` | The in-app restore confirmation now warns that the operation copies files from the selected set over matching live files and does not remove files absent from the set. |
| `Themes/Languages/en-US.xaml` | English backup/restore descriptions were corrected to describe managed backup sets, overlay restore, and the possibility of a partial restore. |
| `scripts/i18n/msg-spec.json` | The English restore-confirmation source text was corrected to match the overlay behavior. |
| `Tools/DataRootRecovery/ProGlass-LocalDataRoot.ps1` | Added a Windows-only helper for the standard local `%APPDATA%\ProGlassAutomation` root. It refuses a shared-root configuration or a running app, verifies copied file/folder inventories and SHA-256 hashes, stages restores, and preserves the prior root. **It has not been executed or syntax-validated on Windows in this environment.** |
| `docs/BACKUP.md` | Replaced stale backup claims with the code-derived local/shared paths, actual in-app backup-set scope, manual whole-root procedures, helper instructions, and the Windows-verification status/link. |
| `docs/Phase0-BackupVerificationChecklist.md` | Added a step-by-step Windows checklist for the real UI record, file-copy backup, fresh-root restore, and optional helper run. |

## Verification boundary

Static repository checks performed here included `git diff --check`, JSON/XAML parsing, and source-level assertions. There was **no Windows build, no .NET test run, no PowerShell run, no WPF launch, and no app-created test record or backup/restore**. The earlier Linux SQLite copy smoke test used synthetic tables; it verified file-copy mechanics only and is not ProGlass acceptance evidence. Do not mark Phase 0 backup/restore complete until the Windows checklist is executed and its results are recorded.
