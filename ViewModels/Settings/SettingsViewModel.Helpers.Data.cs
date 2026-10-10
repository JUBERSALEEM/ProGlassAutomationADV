// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Helpers.Data.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Settings v3 redesign (2026-09-17): the 580-line InitializeV2Sections
// command-wiring method became one dispatcher + per-domain init methods.
// Same partial class, members moved verbatim — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════
// NOTE: owns backup + database-health wiring and diagnostics
// ═══════════════════════════════════════════════════════════════════════
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Fonts;
using System;
using ProGlassAutomation.Utilities;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
    {
        private void InitializeBackupCommands()
        {
            // ── Backup commands ───────────────────────────────────────
            RefreshBackupsCommand = new RelayCommand(_ =>
            {
                // Visible feedback so the click never feels dead.
                BackupStatus = "Refreshing backup list…";
                BackupStatus = RefreshBackupSets();
            });

            CreateBackupNowCommand = new RelayCommand(_ =>
            {
                // BackupService is a STATIC class — call members directly
                // (a static type cannot be assigned to a variable).

                // Professional behaviour: warn if a backup was made very
                // recently so the list isn't flooded by repeated clicks.
                var sinceLast = Services.Database.DatabaseBackup.BackupService
                    .TimeSinceLastBackup();
                if (sinceLast.HasValue &&
                    sinceLast.Value < Services.Database.DatabaseBackup.BackupService
                        .ManualWarnWindow)
                {
                    var mins = (int)sinceLast.Value.TotalMinutes;
                    var confirm = _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.BackupWasCreatedXMinute"), mins),
                        LocalizationService.Instance.T("Msg.CapBackupAlreadyRecent"),
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);
                    if (confirm != System.Windows.MessageBoxResult.Yes)
                        return;
                }

                var result = Services.Database.DatabaseBackup.BackupService
                    .CreateFullBackupSetDetailed("manual (Settings)");

                RefreshBackupSets();
                BackupStatus = (result.Success ? "✓ " : "✗ ") + result.Message;
            });

            RestoreSelectedBackupCommand = new RelayCommand(_ =>
            {
                if (_database.IsDemo)
                {
                    BackupStatus =
                        "Restore is blocked in Demo Mode. Exit Demo Mode first — " +
                        "restore writes the production folder.";
                    _dialogs.Show(
                        LocalizationService.Instance.T("Msg.DemoModeIsOnRestore"),
                        LocalizationService.Instance.T("Msg.CapRestoreBlocked"),
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var selected = SelectedBackup;
                if (selected == null)
                {
                    // Never fail silently — tell the user what to do.
                    BackupStatus =
                        "Select a backup set from the list first.";
                    return;
                }

                if (!selected.IsRestorable)
                {
                    BackupStatus = "✗ Selected set cannot be restored: " +
                        selected.RestoreValidationMessage + ". No files were changed.";
                    return;
                }

                // This in-app action overlays files included in the backup
                // set; it does NOT replace the complete data-root directory.
                // Keep the scope explicit here so the confirmation cannot
                // imply that stray/unlisted files will be removed.
                string restoreWarning =
                    $"Restore backup set from {selected.CreatedAt:G}?\n\n" +
                    "This copies the database, configuration, and attachment files present " +
                    "in the selected set over matching files in the active data root. Files " +
                    "that are not in the set are left in place. A safety snapshot is taken " +
                    "first. For a clean full-root restore, close ProGlass on every workstation " +
                    "and replace the complete data-root folder using the Windows backup/restore procedure.";
                if (!selected.HasInventoryManifest)
                    restoreWarning += "\n\n" + selected.RestoreValidationMessage +
                        " Only proceed if you trust the contents of this older backup set.";
                var confirm = _dialogs.Show(
                    restoreWarning,
                    LocalizationService.Instance.T("Msg.CapRestoreBackup"),
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm != System.Windows.MessageBoxResult.Yes) return;

                BackupStatus = "Restoring backup — please wait…";

                var (ok, message) = Services.Database.DatabaseBackup
                    .BackupService.RestoreBackupSet(selected.FolderPath);

                BackupStatus = (ok ? "✓ " : "✗ ") + message;
                RefreshBackupSets();

                if (!ok) return;

                // The restored database only loads after a restart —
                // surface that explicitly instead of a tiny status line.
                var restart = _dialogs.Show(
                    LocalizationService.Instance.T("Msg.BackupRestoredSuccessfullyProGlassMust"),
                    LocalizationService.Instance.T("Msg.CapRestartRequired"),
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (restart == System.Windows.MessageBoxResult.Yes)
                    RestartApplication();
            });

            OpenBackupsFolderCommand = new RelayCommand(_ =>
            {
                try
                {
                    AppDataRoot.EnsureCurrentFolder();
                    var folder = Services.Database.DatabaseBackup.BackupService.GetBackupsFolder();
                    System.IO.Directory.CreateDirectory(folder);
                    ProcessStartGuard.TryOpenFolder(folder, CorrelationContext.NewId());
                }
                catch (Exception ex)
                {
                    BackupStatus = "✗ Could not open backup folder: " + ExceptionMessages.Deep(ex);
                    AppLogService.Warning($"Could not open backup folder: {ExceptionMessages.Deep(ex)}");
                }
            });

        }

        private void InitializeDatabaseHealthCommands()
        {
            // ── Database health (diagnostics + emergency rebuild) ─────
            //
            // Diagnostics is read-only and safe for any signed-in user.
            // Rebuild is destructive: it MOVES every .db file to
            // MigrationBackups/_rebuild_<timestamp>/, then recreates the
            // files at the current schema. It is gated behind a Manager+
            // role check (the same gate ClearAllData uses) and a typed
            // confirmation so a misclick cannot destroy data.
            RefreshDatabaseHealthCommand = new RelayCommand(
                async _ => await RunDatabaseDiagnosticsAsync(),
                _ => !IsDiagnosing && !IsRebuilding);

            RebuildAllDatabasesCommand = new RelayCommand(
                async _ => await RebuildAllDatabasesAsync(),
                _ => !IsDiagnosing && !IsRebuilding);

            BackupStatus = RefreshBackupSets();
        }

        private async Task RunDatabaseDiagnosticsAsync()
        {
            if (IsDiagnosing || IsRebuilding) return;
            IsDiagnosing = true;
            DatabaseHealthStatus = "Probing live databases…";
            try
            {
                var rows = await SchemaHealthService.DiagnoseAsync();
                ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() =>
                {
                    DatabaseHealthRows.Clear();
                    foreach (var r in rows) DatabaseHealthRows.Add(r);
                });
                int populated = rows.Count(r => r.FileExists && r.TotalRows > 0);
                int drift = rows.Sum(r => r.DriftNotes.Count);
                DatabaseHealthStatus =
                    $"✓ {rows.Count} database(s) probed — " +
                    $"{populated} contain data, " +
                    $"{drift} schema note(s). " +
                    "If a note lists \"missing\" columns, run the schema " +
                    "migration on next app start (it runs automatically).";
            }
            catch (Exception ex)
            {
                DatabaseHealthStatus = "✗ Diagnostics failed: " + ExceptionMessages.Deep(ex);
                AppLogService.Error(
                    "Database diagnostics failed", ex,
                    new Services.Logging.LogContext
                    {
                        Module = "Database",
                        Class = nameof(SettingsViewModel),
                        Operation = "RunDatabaseDiagnostics"
                    });
            }
            finally
            {
                IsDiagnosing = false;
            }
        }

        private async Task RebuildAllDatabasesAsync()
        {
            if (IsDiagnosing || IsRebuilding) return;

            // Gate 1: Manager+ only. Same role the ClearAllData
            // command uses, so a non-manager can never reach this
            // destructive path even by typing the wrong key.
            string role = Services.Audit.AuditService.CurrentUser?.Role ?? "";
            bool isManagerOrAbove =
                string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
            if (!isManagerOrAbove)
            {
                _dialogs.Show(
                    "Rebuilding all databases is restricted to Managers " +
                    "and Administrators. Your role: " +
                    (string.IsNullOrEmpty(role) ? "none" : role) + ".",
                    LocalizationService.Instance.T("Msg.CapPermissionDenied"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Gate 2: confirm the user knows what they're doing.
            var proceed = _dialogs.Show(
                "EMERGENCY DATABASE REBUILD\n\n" +
                "This will:\n" +
                "  1. Move every .db file to MigrationBackups/_rebuild_<timestamp>/\n" +
                "  2. Recreate each one at the CURRENT schema\n" +
                "  3. Re-run the schema migration on inquiries.db\n\n" +
                "All transactional data (inquiries, PIs, JOs, DNs, TIs, " +
                "statements, sheet store) will be LOST. Master data, audit " +
                "log and AI memory are also wiped.\n\n" +
                "The originals are MOVED (not deleted) and can be copied " +
                "back by hand from the MigrationBackups folder.\n\n" +
                "ProGlass must be restarted afterwards.\n\n" +
                "Continue?",
                "Emergency Database Rebuild",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);

            if (proceed != System.Windows.MessageBoxResult.Yes) return;

            IsRebuilding = true;
            DatabaseHealthStatus = "Rebuilding — this can take a few seconds…";
            try
            {
                var progress = new Progress<string>(msg =>
                    DatabaseHealthStatus = msg);

                var summary = await SchemaHealthService
                    .RebuildAllAsync(progress);

                AppLogService.Info(
                    "[Rebuild] " + summary,
                    new Services.Logging.LogContext
                    {
                        Module = "Database",
                        Class = nameof(SettingsViewModel),
                        Operation = "RebuildAllDatabases"
                    });

                // Refresh the row-count table so the user sees the
                // freshly-empty databases immediately.
                await RunDatabaseDiagnosticsAsync();

                // Offer the restart. The user can defer it (e.g. they
                // want to copy files out of MigrationBackups first) but
                // the app's pooled connections still point at the
                // moved files until restart.
                var restart = _dialogs.Show(
                    "✓ All databases were rebuilt at the current schema.\n\n" +
                    "The originals are saved in:\n" +
                    _database.CurrentFolder + "\\MigrationBackups\\\n\n" +
                    "ProGlass must be restarted to load the new databases.\n\n" +
                    "Restart now?",
                    LocalizationService.Instance.T("Msg.CapRebuildComplete"),
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information,
                    System.Windows.MessageBoxResult.Yes);

                if (restart == System.Windows.MessageBoxResult.Yes)
                    RestartApplication();
            }
            catch (Exception ex)
            {
                DatabaseHealthStatus = "✗ Rebuild failed: " + ExceptionMessages.Deep(ex);
                AppLogService.Error(
                    "Emergency database rebuild failed", ex,
                    new Services.Logging.LogContext
                    {
                        Module = "Database",
                        Class = nameof(SettingsViewModel),
                        Operation = "RebuildAllDatabases"
                    });

                _dialogs.Show(
                    "Rebuild failed: " + ExceptionMessages.Deep(ex) +
                    "\n\nThe original databases are still in " +
                    "MigrationBackups and have NOT been deleted.",
                    LocalizationService.Instance.T("Msg.CapRebuildFailed"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsRebuilding = false;
            }
        }

        private string RefreshBackupSets()
        {
            BackupSets.Clear();
            bool available = Services.Database.DatabaseBackup.BackupService
                .TryGetBackupSets(out var sets, out string message);
            foreach (var set in sets)
                BackupSets.Add(set);

            if (!available)
                return "✗ " + message;

            int restorable = BackupSets.Count(set => set.IsRestorable);
            int invalid = BackupSets.Count - restorable;
            return BackupSets.Count == 0
                ? message
                : $"✓ Listed {BackupSets.Count} set(s): {restorable} restorable, {invalid} not restorable. {message}";
        }

        // Phase 4: Event lifetime — detach static events
    }
}
