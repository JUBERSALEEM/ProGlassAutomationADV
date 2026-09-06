// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Database/SchemaHealthService.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — database diagnostics & emergency rebuild
//
// PURPOSE
// ───────
// Two operations that the rest of the app does not own:
//
//   1. DiagnoseAsync — for every ProGlass Automation database file,
//      reads the row counts of the main tables and the live column
//      list of the Inquiries table. Surfaces drift between the EF
//      model and the actual SQLite file without modifying anything.
//      Cheap, read-only, safe to run on every Settings page open.
//
//   2. RebuildAllAsync — emergency-only "blow away every DB and let
//      EnsureCreated recreate them at the current schema" operation.
//      Moves the live files to a timestamped folder under
//      MigrationBackups/_rebuild_<yyyyMMdd_HHmmss>/ before doing
//      anything, so a mistake is recoverable by hand. Calls
//      EnsureCreated() on every DbContext so the rebuilt files
//      contain the *current* model — exactly what would happen on a
//      fresh install. NEVER called from normal app flow.
//
// WHY THIS IS NOT A "JUST DELETE THE FILE" COMMAND
// ────────────────────────────────────────────────
// A naive Delete() on a live SQLite file under EF Core 8 will:
//   • Fail outright while the pooled connection is still open.
//   • Leave the WAL/SHM files in place, so a recreated DB opens
//     against stale sidecars.
//   • Lose any data the user wanted to keep without a chance to
//     inspect it.
//
// The rebuild flow here addresses all three:
//   • SqliteConnection.ClearAllPools() is called FIRST so the file
//     is not held open.
//   • The .db-wal and .db-shm sidecars are renamed alongside the
//     main file (SQLite only creates them on first write, but a
//     long-running install has them).
//   • The originals are MOVED (not deleted) to MigrationBackups, so
//     a paranoid operator can copy them back.
//
// CALLER RESPONSIBILITY
// ─────────────────────
// RebuildAllAsync is destructive. Callers MUST:
//   1. Confirm with the user (typed confirmation, not just Yes/No).
//   2. Gate behind a Manager-or-higher role.
//   3. Warn that the app must restart afterwards.
//
// SettingsViewModel is the only intended caller.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Data.Sqlite;
using ProGlassAutomation.Services.AI.AIMemory;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database.SchemaMigrations;
using ProGlassAutomation.Services.DeliveryNote.Database;
using ProGlassAutomation.Services.Inquiry;
using ProGlassAutomation.Services.JobOrder.Database;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.MasterData.Database;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
using ProGlassAutomation.Services.Statement.Database;
using ProGlassAutomation.Services.TaxInvoice.Database;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Database
{
    /// <summary>
    /// One row in the "Database health" Settings card. Backs a
    /// read-only grid the user can scan to see which DBs are
    /// populated, which are empty, and whether the schema has any
    /// drift (e.g. the legacy "Salesman" column a customer reported
    /// in commit cf14864).
    /// </summary>
    public sealed class DatabaseHealthRow
    {
        public string DatabaseKey { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string FilePath { get; init; } = "";
        public bool   FileExists { get; init; }
        public long   FileSizeBytes { get; init; }
        public int    UserVersion { get; init; }
        public int    TotalRows { get; init; }
        public IReadOnlyList<TableCount> Tables { get; init; } =
            Array.Empty<TableCount>();
        public IReadOnlyList<string> DriftNotes { get; init; } =
            Array.Empty<string>();
        public string Status => FileExists
            ? (TotalRows == 0 ? "Empty" : "Healthy")
            : "Not created";

        public string TotalRowsDisplay => FileExists
            ? TotalRows.ToString("N0")
            : "—";
        public string FileSizeDisplay => FileExists
            ? FormatBytes(FileSizeBytes)
            : "—";
        public string UserVersionDisplay => FileExists
            ? UserVersion.ToString()
            : "—";
        public string DriftDisplay => DriftNotes.Count == 0
            ? "—"
            : string.Join("  •  ", DriftNotes);

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.0} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):0.00} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):0.00} GB";
        }
    }

    public readonly record struct TableCount(
        string TableName, int RowCount);

    /// <summary>
    /// One entry in the "expected columns" model. The drift
    /// detector only flags columns the model REQUIRES but the live
    /// table is MISSING. Extra columns in the live table are
    /// reported as "extra" but not flagged as drift (they might be
    /// legacy data the schema migration has not yet cleaned up).
    /// </summary>
    public sealed class DatabaseSchemaExpectation
    {
        public string Table { get; init; } = "";
        public IReadOnlyList<string> RequiredColumns { get; init; } =
            Array.Empty<string>();
    }

    /// <summary>
    /// Diagnostics + emergency rebuild for the app's SQLite
    /// databases. See file header for the rules of engagement.
    /// </summary>
    public static class SchemaHealthService
    {
        // ── Registry of every DB the app owns ──────────────────────────
        //
        // Each entry carries the path (resolved lazily so the file
        // may not yet exist on a fresh install) and the list of
        // tables whose row counts are interesting in the Settings
        // preview. The Diagnose call runs PRAGMA table_info +
        // SELECT COUNT(*) on each of those tables.
        //
        // Keep this list in sync with DatabaseInitializer and the
        // schema-migration registry: a new DbContext should add a
        // new entry here so its row count is visible to the user.

        private sealed class DbDescriptor
        {
            public string Key { get; init; } = "";
            public string DisplayName { get; init; } = "";
            public Func<string> PathProvider { get; init; } =
                () => string.Empty;
            public IReadOnlyList<string> CountTables { get; init; } =
                Array.Empty<string>();
            public IReadOnlyList<DatabaseSchemaExpectation> Expect { get; init; } =
                Array.Empty<DatabaseSchemaExpectation>();
            // Recreate() is called by RebuildAllAsync after the file
            // is moved out of the way. Each DbContext's
            // EnsureCreated() does the work.
            public Action Recreate { get; init; } = () => { };
        }

        private static IReadOnlyList<DbDescriptor> BuildRegistry() => new[]
        {
            new DbDescriptor
            {
                Key = "ProGlass",
                DisplayName = "Sheet Store (proglass.db)",
                PathProvider = () => AppDbContext.DatabaseFilePath,
                CountTables = new[] { "GlassSheets", "SheetHistories", "InventoryItems" },
                Expect = Array.Empty<DatabaseSchemaExpectation>(),
                Recreate = () =>
                {
                    using var db = new AppDbContext();
                    db.Database.EnsureCreated();
                }
            },
            new DbDescriptor
            {
                Key = "Inquiries",
                DisplayName = "Inquiries (inquiries.db)",
                PathProvider = () => InquiryDbContext.DatabaseFilePath,
                CountTables = new[]
                {
                    "Inquiries", "InquiryAttachments", "InquiryCommunications"
                },
                Expect = new[]
                {
                    new DatabaseSchemaExpectation
                    {
                        Table = "Inquiries",
                        RequiredColumns = new[]
                        {
                            "Id", "InquiryNumber", "CustomerName", "SalesmanName",
                            "ProjectName", "Status", "Priority", "ReceivedAt",
                            "ProformaInvoiceId", "ProformaNumber", "JobOrderNumber"
                        }
                    }
                },
                Recreate = () =>
                {
                    using var db = new InquiryDbContext();
                    db.Database.EnsureCreated();
                }
            },
            new DbDescriptor
            {
                Key = "ProformaInvoices",
                DisplayName = "Proforma Invoices (proformainvoices.db)",
                PathProvider = () => ProformaInvoicesDbContext.DatabaseFilePath,
                CountTables = new[] { "ProformaInvoices" },
                Expect = Array.Empty<DatabaseSchemaExpectation>(),
                Recreate = () =>
                {
                    using var db = new ProformaInvoicesDbContext();
                    db.Database.EnsureCreated();
                }
            },
            new DbDescriptor
            {
                Key = "JobOrders",
                DisplayName = "Job Orders (joborders.db)",
                PathProvider = () => JobOrdersDbContext.DatabaseFilePath,
                CountTables = new[] { "JobOrders" },
                Expect = Array.Empty<DatabaseSchemaExpectation>(),
                Recreate = () =>
                {
                    using var db = new JobOrdersDbContext();
                    db.Database.EnsureCreated();
                }
            },
            new DbDescriptor
            {
                Key = "DeliveryNotes",
                DisplayName = "Delivery Notes (deliverynotes.db)",
                PathProvider = () => DeliveryNotesDbContext.DatabaseFilePath,
                CountTables = new[] { "DeliveryNotes" },
                Expect = Array.Empty<DatabaseSchemaExpectation>(),
                Recreate = () =>
                {
                    using var db = new DeliveryNotesDbContext();
                    db.Database.EnsureCreated();
                }
            },
            new DbDescriptor
            {
                Key = "TaxInvoices",
                DisplayName = "Tax Invoices (taxinvoices.db)",
                PathProvider = () => TaxInvoiceDbContext.DatabaseFilePath,
                CountTables = new[] { "TaxInvoices" },
                Expect = Array.Empty<DatabaseSchemaExpectation>(),
                Recreate = () =>
                {
                    using var db = new TaxInvoiceDbContext();
                    db.Database.EnsureCreated();
                }
            },
            new DbDescriptor
            {
                Key = "Statements",
                DisplayName = "Statements (statements.db)",
                PathProvider = () => StatementDbContext.DatabaseFilePath,
                CountTables = new[] { "Statements" },
                Expect = Array.Empty<DatabaseSchemaExpectation>(),
                Recreate = () =>
                {
                    using var db = new StatementDbContext();
                    db.Database.EnsureCreated();
                }
            },
            new DbDescriptor
            {
                Key = "Specifications",
                DisplayName = "Specifications (specifications.db)",
                PathProvider = () => SpecificationsDbContext.DatabaseFilePath,
                CountTables = new[] { "Specifications" },
                Expect = Array.Empty<DatabaseSchemaExpectation>(),
                Recreate = () =>
                {
                    using var db = new SpecificationsDbContext();
                    db.Database.EnsureCreated();
                }
            },
            new DbDescriptor
            {
                Key = "MasterData",
                DisplayName = "Master Data (masterdata.db)",
                PathProvider = () => MasterDataDbContext.DatabaseFilePath,
                CountTables = new[]
                {
                    "Companies", "Clients", "Projects", "Salesmen"
                },
                Expect = Array.Empty<DatabaseSchemaExpectation>(),
                Recreate = () =>
                {
                    using var db = new MasterDataDbContext();
                    db.Database.EnsureCreated();
                }
            },
            new DbDescriptor
            {
                Key = "Audit",
                DisplayName = "Audit (audit.db)",
                PathProvider = () => AuditDbContext.DatabaseFilePath,
                CountTables = new[] { "ActivityLog" },
                Expect = Array.Empty<DatabaseSchemaExpectation>(),
                Recreate = () =>
                {
                    using var db = new AuditDbContext();
                    db.Database.EnsureCreated();
                }
            },
            new DbDescriptor
            {
                Key = "AiMemory",
                DisplayName = "AI Memory (aimemory.db)",
                PathProvider = () => AiMemoryDbContext.DatabaseFilePath,
                CountTables = new[] { "AiMemoryEntries" },
                Expect = Array.Empty<DatabaseSchemaExpectation>(),
                Recreate = () =>
                {
                    using var db = new AiMemoryDbContext();
                    db.Database.EnsureCreated();
                }
            }
        };

        // ══════════════════════════════════════════════════════════════
        //  DIAGNOSE
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Reads row counts + schema fingerprints for every database
        /// the app owns. Never modifies the files. Safe to call on
        /// every Settings page open.
        /// </summary>
        public static async Task<ObservableCollection<DatabaseHealthRow>>
            DiagnoseAsync(CancellationToken ct = default)
        {
            var rows = new ObservableCollection<DatabaseHealthRow>();
            var registry = BuildRegistry();

            foreach (var d in registry)
            {
                ct.ThrowIfCancellationRequested();
                var path = d.PathProvider();
                if (string.IsNullOrEmpty(path))
                {
                    rows.Add(new DatabaseHealthRow
                    {
                        DatabaseKey = d.Key,
                        DisplayName = d.DisplayName,
                        FilePath = "",
                        FileExists = false
                    });
                    continue;
                }

                if (!File.Exists(path))
                {
                    rows.Add(new DatabaseHealthRow
                    {
                        DatabaseKey = d.Key,
                        DisplayName = d.DisplayName,
                        FilePath = path,
                        FileExists = false
                    });
                    continue;
                }

                long size;
                int userVersion;
                int totalRows;
                List<TableCount> tableCounts = new();
                List<string> drift = new();

                try
                {
                    size = new FileInfo(path).Length;
                    userVersion = ReadUserVersion(path);
                    tableCounts = await ReadTableCountsAsync(path,
                        d.CountTables, ct);
                    totalRows = tableCounts.Sum(t => t.RowCount);
                    drift = await DetectDriftAsync(path, d.Expect, ct);
                }
                catch (Exception ex)
                {
                    // One bad DB must not stop the rest of the report.
                    AppLogService.Warning(
                        $"[SchemaHealth] diagnose failed for {d.Key}: {ex.Message}",
                        new LogContext
                        {
                            Module = "Database",
                            Class = nameof(SchemaHealthService),
                            Operation = "Diagnose"
                        });
                    size = 0;
                    userVersion = 0;
                    totalRows = 0;
                    drift = new List<string> { $"diagnose failed: {ex.Message}" };
                }

                rows.Add(new DatabaseHealthRow
                {
                    DatabaseKey = d.Key,
                    DisplayName = d.DisplayName,
                    FilePath = path,
                    FileExists = true,
                    FileSizeBytes = size,
                    UserVersion = userVersion,
                    TotalRows = totalRows,
                    Tables = tableCounts,
                    DriftNotes = drift
                });
            }

            return rows;
        }

        private static int ReadUserVersion(string dbPath)
        {
            try
            {
                using var conn = new SqliteConnection(
                    $"Data Source={dbPath};Mode=ReadOnly");
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA user_version;";
                var v = cmd.ExecuteScalar();
                return Convert.ToInt32(v ?? 0);
            }
            catch
            {
                return 0;
            }
        }

        private static async Task<List<TableCount>> ReadTableCountsAsync(
            string dbPath, IReadOnlyList<string> tables,
            CancellationToken ct)
        {
            var list = new List<TableCount>();
            await using var conn = new SqliteConnection(
                $"Data Source={dbPath};Mode=ReadOnly");
            await conn.OpenAsync(ct);
            foreach (var t in tables)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    // Table name is a closed enum compiled into this
                    // file (not user input), so direct interpolation
                    // is safe. If a table doesn't exist, COUNT(*)
                    // throws "no such table" and we record 0.
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"SELECT COUNT(*) FROM \"{t}\";";
                    var v = await cmd.ExecuteScalarAsync(ct);
                    list.Add(new TableCount(t,
                        Convert.ToInt32(v ?? 0)));
                }
                catch
                {
                    list.Add(new TableCount(t, 0));
                }
            }
            return list;
        }

        private static async Task<List<string>> DetectDriftAsync(
            string dbPath,
            IReadOnlyList<DatabaseSchemaExpectation> expect,
            CancellationToken ct)
        {
            var notes = new List<string>();
            if (expect == null || expect.Count == 0) return notes;

            await using var conn = new SqliteConnection(
                $"Data Source={dbPath};Mode=ReadOnly");
            await conn.OpenAsync(ct);

            foreach (var e in expect)
            {
                ct.ThrowIfCancellationRequested();

                HashSet<string> live;
                try
                {
                    live = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    await using var pragma = conn.CreateCommand();
                    pragma.CommandText = $"PRAGMA table_info(\"{e.Table}\");";
                    await using var reader = await pragma.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                        live.Add(reader.GetString(1));
                }
                catch
                {
                    notes.Add($"{e.Table}: table missing");
                    continue;
                }

                if (live.Count == 0)
                {
                    notes.Add($"{e.Table}: empty / missing");
                    continue;
                }

                var missing = e.RequiredColumns
                    .Where(c => !live.Contains(c))
                    .ToList();
                if (missing.Count > 0)
                {
                    notes.Add(
                        $"{e.Table} missing: {string.Join(", ", missing)}");
                }

                // "Extras" are not necessarily a problem (a legacy
                // "Salesman" column waiting for the next migration
                // is fine), but if the same extra name keeps showing
                // up across many runs the operator can decide to
                // run the schema migration.
                var extras = live
                    .Where(c => !e.RequiredColumns.Contains(
                        c, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                if (extras.Count > 0)
                {
                    notes.Add(
                        $"{e.Table} extras: {string.Join(", ", extras)}");
                }
            }

            return notes;
        }

        // ══════════════════════════════════════════════════════════════
        //  REBUILD (emergency)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Moves every live database file (plus its -wal / -shm
        /// sidecars) into a timestamped MigrationBackups folder, then
        /// recreates each one at the current schema by calling
        /// EnsureCreated() on its DbContext. The originals are
        /// NEVER deleted — a paranoid operator can copy them back
        /// by hand.
        /// </summary>
        /// <param name="progress">
        /// Optional callback fired once per step. Strings are
        /// user-friendly and safe to show on the UI.
        /// </param>
        /// <returns>Summary of every move + recreate.</returns>
        public static async Task<string> RebuildAllAsync(
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            // 1) Release pooled connections so the file is not
            //    locked by a long-lived EF context.
            try { SqliteConnection.ClearAllPools(); }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"ClearAllPools failed (non-fatal): {ex.Message}");
            }

            await Task.Yield();
            ct.ThrowIfCancellationRequested();

            var registry = BuildRegistry();
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string dataFolder = AppDataRoot.CurrentFolder;
            string backupRoot = Path.Combine(
                dataFolder, "MigrationBackups", "_rebuild_" + stamp);
            Directory.CreateDirectory(backupRoot);

            var log = new StringBuilder();
            log.AppendLine($"Rebuild started: {stamp}");
            log.AppendLine($"Backup folder:  {backupRoot}");
            log.AppendLine();

            // 2) Move every live file aside. We do this BEFORE
            //    EnsureCreated so the recreated file is unambiguously
            //    the new one. EF's pooled connections are released;
            //    the .db-wal / .db-shm sidecars are moved alongside
            //    the main file in case SQLite left them behind.
            foreach (var d in registry)
            {
                ct.ThrowIfCancellationRequested();
                string path = d.PathProvider();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    progress?.Report($"{d.DisplayName}: nothing to move");
                    log.AppendLine($"SKIP  {d.DisplayName}  (file not present)");
                    continue;
                }

                try
                {
                    string baseName = Path.GetFileName(path);
                    string movedPath = Path.Combine(backupRoot, baseName);
                    File.Move(path, movedPath);

                    // Sidecars (WAL + SHM) are optional. SQLite only
                    // creates them on the first write, so most
                    // installs have them. Move them too so the
                    // recreated file is the only thing EF sees.
                    foreach (string ext in new[] { "-wal", "-shm" })
                    {
                        string side = path + ext;
                        if (File.Exists(side))
                        {
                            try
                            {
                                File.Move(side, movedPath + ext);
                            }
                            catch (Exception sideEx)
                            {
                                AppLogService.Warning(
                                    $"Could not move sidecar {side}: {sideEx.Message}");
                            }
                        }
                    }

                    progress?.Report($"Moved {d.DisplayName} aside");
                    log.AppendLine($"MOVED {d.DisplayName}  →  {movedPath}");
                }
                catch (Exception ex)
                {
                    // One failed move must not abort the rest of the
                    // rebuild — the moved files are still in
                    // MigrationBackups and the user can recover by
                    // hand. But log loud so the operator sees it.
                    AppLogService.Error(
                        $"Could not move {d.DisplayName} aside",
                        ex,
                        new LogContext
                        {
                            Module = "Database",
                            Class = nameof(SchemaHealthService),
                            Operation = "RebuildAll"
                        });
                    log.AppendLine(
                        $"FAIL  {d.DisplayName}  (could not move: {ex.Message})");
                }
            }

            log.AppendLine();
            log.AppendLine("Recreating databases at current schema...");

            // 3) Recreate every DB at the current schema. The
            //    recreate action lives in the descriptor so each
            //    DbContext is exercised exactly the way the rest
            //    of the app exercises it.
            foreach (var d in registry)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await Task.Run(() => d.Recreate(), ct);
                    progress?.Report($"Recreated {d.DisplayName}");
                    log.AppendLine($"OK    {d.DisplayName}");
                }
                catch (Exception ex)
                {
                    AppLogService.Error(
                        $"Recreate failed for {d.DisplayName}",
                        ex,
                        new LogContext
                        {
                            Module = "Database",
                            Class = nameof(SchemaHealthService),
                            Operation = "RebuildAll"
                        });
                    log.AppendLine(
                        $"FAIL  {d.DisplayName}  (recreate: {ex.Message})");
                }
            }

            // 4) Bring the main app DB up to its expected schema
            //    version. DatabaseInitializer normally does this on
            //    every cold start, but a manual rebuild may be
            //    followed by an in-place "Reload" rather than a
            //    full restart, so run the migrations now. Same
            //    registry the app uses, same transaction safety.
            try
            {
                var inquiries = InquiryDbContext.DatabaseFilePath;
                if (File.Exists(inquiries))
                {
                    bool ok = SchemaMigrationRunner.Upgrade(
                        inquiries,
                        AppSchemaMigrations.Inquiries,
                        "Inquiries");
                    log.AppendLine(ok
                        ? "OK    Inquiries schema migrations applied"
                        : "WARN  Inquiries schema migrations FAILED (see logs)");
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Inquiries migration on rebuild failed: {ex.Message}");
                log.AppendLine(
                    $"WARN  Inquiries migration: {ex.Message}");
            }

            // 5) Audit + final log.
            try
            {
                AuditService.Log(
                    AuditSections.Settings,
                    "Emergency database rebuild",
                    $"All DBs moved to {backupRoot} and recreated at current schema.");
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[SchemaHealthService] RebuildAllAsync: audit must not block rebuild: " + ex.Message); }

            AppLogService.Info(
                $"[SchemaHealth] emergency rebuild complete. Backups: {backupRoot}",
                new LogContext
                {
                    Module = "Database",
                    Class = nameof(SchemaHealthService),
                    Operation = "RebuildAll"
                });

            log.AppendLine();
            log.AppendLine(
                "Rebuild complete. Restart ProGlass Automation to load the new schema.");
            return log.ToString();
        }
    }
}
