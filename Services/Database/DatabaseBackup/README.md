# Database Auto-Backup

<!-- pg-nav -->
> **[Repository root](../../../README.md)** · [All documents](../../../docs/README.md) · [Application workflow](../../../docs/WORKFLOW.md) · [Design system](../../../docs/DESIGN_SYSTEM.md)
<!-- pg-nav -->
Automatic backup of the SQLite database on every app exit.
Keeps last 7 timestamped copies for data recovery.

---

## How It Works

1. User closes app (or app exits normally)
2. `App.OnExit()` calls `BackupService.CreateBackup()`
3. Copies `proglass.db` to `Backups/` folder with timestamp
4. Deletes backups older than 7 copies (keeps newest 7)

---

## File Location

Active database and backups live in `%AppData%\ProGlassAutomation\`:

- `proglass.db` — active database
- `config.json` — settings
- `Backups/` — auto-created folder containing timestamped copies

Backup naming format: `proglass_yyyy-MM-dd_HH-mm-ss.db`

Example:
- `proglass_2025-01-15_14-30-00.db` (newest)
- `proglass_2025-01-14_09-15-22.db`
- `proglass_2025-01-09_20-10-45.db` (oldest kept)

---

## API

**Create backup** (called on app exit):
```csharp
BackupService.CreateBackup();
```

**Get available backups** (newest first):
```csharp
FileInfo[] backups = BackupService.GetAvailableBackups();
```

**Restore from a specific backup**:
```csharp
bool success = BackupService.RestoreFromBackup(backupFilePath);
```

---

## Configuration

| Setting | Value | Notes |
|---|---|---|
| MAX_BACKUPS | 7 | Hardcoded in BackupService.cs |
| Naming | proglass_yyyy-MM-dd_HH-mm-ss.db | Timestamp format |
| Trigger | App.OnExit() | Normal close only (not crash) |
| Safety | pre_restore_safety.db | Created before restore overwrites |

---

## File Structure

```
Services/Database/DatabaseBackup/
  BackupService.cs    Auto-backup logic
  README.md           This file
```

---

## Status

### Completed
- Auto-backup on app exit
- Timestamped backup files
- Keep last 7, auto-delete older
- Restore from backup with safety copy
- List available backups (newest first)
- Silent failure (never crashes app)

### To-Do
- Periodic backup (every N minutes while running)
- Backup to cloud (OneDrive/Google Drive)
- Restore UI in Settings drawer
- Backup notification ("Last backup: 5 min ago")
- Compressed backups (.zip) for space saving
- Export backup to USB/external drive

---

## Design Principles

1. **Silent** — never shows errors to user, never crashes
2. **Automatic** — no user action needed
3. **Safe** — creates safety copy before restore
4. **Bounded** — max 7 files, auto-cleanup
5. **Portable** — just .db files, copy anywhere

<!-- pg-nav -->
---

*This README documents `Services/Database/DatabaseBackup/`. For the end-to-end business chain see [`docs/WORKFLOW.md`](../../../docs/WORKFLOW.md); for every other document see [`docs/README.md`](../../../docs/README.md).*
<!-- pg-nav -->
