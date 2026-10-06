using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;

namespace ProGlassAutomation.Services.RecordsSync
{
    /// <summary>
    /// READ-ONLY extraction of business records from the application's own
    /// proforma database — the "ProGlassDatabaseSource" leg of the pipeline.
    ///
    /// Design decisions (see the integration report):
    ///   • Opens proformainvoices.db with Mode=ReadOnly + busy_timeout. The
    ///     database runs in WAL mode (SqliteResilience), so a read-only
    ///     connection coexists with the running application's writes; a
    ///     transient lock surfaces as SqliteException and is handled by the
    ///     caller — never fatal, never blocking.
    ///   • Raw Microsoft.Data.Sqlite instead of ProformaInvoicesDbContext:
    ///     the sync must not participate in EF change tracking, migrations
    ///     or context pooling. It reads columns and deserializes the same
    ///     FullSnapshotJson the repository writes — one source of truth.
    ///   • Voided invoices (IsVoid=1) are excluded: they are soft-closed
    ///     documents hidden from the default list, so they must not appear
    ///     as active records on the website. The reconcile RPC then
    ///     soft-deactivates their remote rows (history preserved there too).
    ///   • Normalization lives here, next to the extraction, because the
    ///     field semantics (which snapshot property IS the salesman, etc.)
    ///     are knowledge about this database, not about Supabase.
    /// </summary>
    public sealed class ProGlassRecordsSource
    {
        // Snapshot JSON is written by System.Text.Json with default
        // (PascalCase) names; case-insensitive matching also survives any
        // legacy rows written with different casing. Unknown fields are
        // ignored by default, so older/newer snapshot schemas both load.
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// Extracts every live proforma invoice as a normalized payload.
        /// Throws only on hard I/O faults (missing file is NOT a fault — it
        /// returns an empty list, e.g. first launch before any PI exists).
        /// Safe to call from a background thread; touches no UI, no EF.
        /// </summary>
        public IReadOnlyList<RecordPayload> Extract(string sourceFile, string sourceWorksheet)
        {
            var dbPath = ProformaInvoicesDbContext.DatabaseFilePath;
            if (!File.Exists(dbPath))
            {
                AppLogService.Info("[RecordsSync] proformainvoices.db not present yet — nothing to sync.");
                return Array.Empty<RecordPayload>();
            }

            var rows = new List<RecordPayload>();

            // Read-only + busy_timeout: coexists with the app's WAL writer;
            // a genuinely stuck lock fails fast into the caller's handler.
            using var connection = new SqliteConnection(
                $"Data Source={dbPath};Mode=ReadOnly");
            connection.Open();

            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA busy_timeout=5000;";
                pragma.ExecuteNonQuery();
            }

            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT ProformaId, CompanyName, ProjectName, Status, IssueDate, FullSnapshotJson " +
                "FROM ProformaInvoices WHERE IsVoid = 0;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                try
                {
                    var payload = MapRow(reader, sourceFile, sourceWorksheet);
                    if (payload != null)
                        rows.Add(payload);
                }
                catch (Exception ex)
                {
                    // One malformed snapshot must not sink the batch — the
                    // row is skipped and named in the log so it can be fixed.
                    var pi = SafeGetString(reader, 0);
                    AppLogService.Error(
                        $"[RecordsSync] skipped malformed PI '{pi}': {ex.Message}");
                }
            }

            return rows;
        }

        private static RecordPayload? MapRow(
            SqliteDataReader reader, string sourceFile, string sourceWorksheet)
        {
            var piNumber = SafeGetString(reader, 0);
            if (string.IsNullOrWhiteSpace(piNumber))
                return null; // no identity → cannot deduplicate → never sync

            var companyName = SafeGetString(reader, 1);
            var projectName = SafeGetString(reader, 2);
            var status = SafeGetString(reader, 3);
            var issueDate = ReadDate(reader, 4);
            var snapshotJson = SafeGetString(reader, 5);

            ProformaInvoiceSnapshot? snapshot = null;
            if (!string.IsNullOrWhiteSpace(snapshotJson) && snapshotJson != "{}")
            {
                try
                {
                    snapshot = JsonSerializer.Deserialize<ProformaInvoiceSnapshot>(
                        snapshotJson, JsonOptions);
                }
                catch (JsonException)
                {
                    snapshot = null; // fall back to indexed columns below
                }
            }

            var client = snapshot?.Client;
            var qty = 0;
            var sqm = 0m;
            if (snapshot?.SpecGroups != null)
            {
                foreach (var group in snapshot.SpecGroups)
                {
                    if (group?.ItemRows == null) continue;
                    foreach (var item in group.ItemRows)
                    {
                        if (item == null) continue;
                        qty += item.Qty;
                        sqm += item.TotalSqm; // computed: W1×H1×Qty via SqmCalculator
                    }
                }
            }

            var preparedBy = FirstNonEmpty(
                client?.DocumentPreparedBy,
                snapshot?.PreparedBy?.PreparedByName);

            return new RecordPayload
            {
                // Website parses DD-MM-YYYY (parseExcelDate).
                Date = issueDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
                Salesman = FirstNonEmpty(
                    client?.SalesRepresentative,
                    snapshot?.Company?.Representative),
                Company = FirstNonEmpty(companyName, snapshot?.Company?.CompanyName),
                PiNumber = piNumber,
                // No dedicated work-type field exists in the PI model; the
                // project name is the nearest native semantic and is what
                // operators actually see per invoice.
                TypeOfWork = FirstNonEmpty(projectName, snapshot?.Project?.ProjectIdentifierName),
                CustomerRef = client?.CustomerReferenceCode ?? "",
                // qty/sqm are TEXT columns in the deployed schema (the
                // Windowfilehost payload sends strings) — invariant culture,
                // qty whole, sqm rounded to 2dp.
                Qty = qty.ToString(CultureInfo.InvariantCulture),
                Sqm = Math.Round(sqm, 2).ToString("0.##", CultureInfo.InvariantCulture),
                PreparedBy = preparedBy,
                // Single native lifecycle feeds both status columns; the
                // website treats them as independent strings.
                Status = status,
                PiStatus = status,
                ZohoStatus = "",
                IsActive = true,
                SourceFile = sourceFile,
                SourceWorksheet = sourceWorksheet,
            };
        }

        private static string SafeGetString(SqliteDataReader reader, int ordinal) =>
            reader.IsDBNull(ordinal) ? "" : (reader.GetValue(ordinal)?.ToString() ?? "");

        private static DateTime ReadDate(SqliteDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return DateTime.MinValue;
            try
            {
                // EF Core stores DateTime as ISO-ish TEXT in SQLite.
                return DateTime.Parse(
                    reader.GetValue(ordinal)?.ToString() ?? "",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None);
            }
            catch (FormatException)
            {
                return DateTime.MinValue;
            }
        }

        private static string FirstNonEmpty(params string?[] candidates)
        {
            foreach (var c in candidates)
                if (!string.IsNullOrWhiteSpace(c))
                    return c!;
            return "";
        }
    }
}
