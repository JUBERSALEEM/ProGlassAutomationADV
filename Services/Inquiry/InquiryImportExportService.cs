using ProGlassAutomation.Models.Inquiry;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Inquiry
{
    /// <summary>Outcome of parsing an Inquiry register CSV export.</summary>
    public sealed class InquiryImportParseResult
    {
        public List<InquiryRecord> Rows { get; } = new();
        /// <summary>Rows skipped because customer/project were missing.</summary>
        public int SkippedInvalidRows { get; set; }
        /// <summary>Numbers blanked because the same number appeared twice in the file.</summary>
        public int DuplicateNumbersRegenerated { get; set; }
    }

    public sealed class InquiryImportExportService
    {
        public async Task ExportCsvAsync(string path, IEnumerable<InquiryListProjection> rows, CancellationToken ct = default)
        {
            static string Q(string? x) => "\"" + (x ?? "").Replace("\"", "\"\"") + "\"";
            var lines = new List<string> { "Inquiry,Reference,Customer,Location,Project,Description,Salesman,Prepared By,Received,Updated,Status,Attachments" };
            lines.AddRange(rows.Select(x => string.Join(",", Q(x.InquiryNumber), Q(x.InquiryReference),
                Q(x.CustomerName), Q(x.CustomerLocation), Q(x.ProjectName), Q(x.ProjectDescription),
                Q(x.SalesmanName), Q(x.PreparedByName), Q(x.ReceivedAt.ToString("o", CultureInfo.InvariantCulture)),
                Q(x.UpdatedAt.ToString("o", CultureInfo.InvariantCulture)), Q(x.Status), x.AttachmentCount.ToString(CultureInfo.InvariantCulture))));
            await File.WriteAllLinesAsync(path, lines, new UTF8Encoding(true), ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Parses a register CSV using record boundaries rather than physical
        /// lines, so valid quoted fields containing commas, quotes, CR/LF, or
        /// embedded newlines round-trip without shifting later columns.
        /// </summary>
        public async Task<InquiryImportParseResult> ParseCsvAsync(string path, CancellationToken ct = default)
        {
            var csv = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var records = ParseCsvRecords(csv);
            var result = new InquiryImportParseResult();
            var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // First record is the exported header. Empty files and header-only
            // files produce an empty result without indexing errors.
            foreach (var c in records.Skip(1))
            {
                ct.ThrowIfCancellationRequested();
                if (c.Count == 1 && string.IsNullOrWhiteSpace(c[0])) continue;
                if (c.Count < 6)
                {
                    result.SkippedInvalidRows++;
                    continue;
                }

                string customer = c[2].Trim(), project = c[4].Trim();
                if (customer.Length == 0 || project.Length == 0)
                {
                    result.SkippedInvalidRows++;
                    continue;
                }

                string number = c[0].Trim();
                if (number.Length > 0 && !seenNumbers.Add(number))
                {
                    result.DuplicateNumbersRegenerated++;
                    number = "";
                }

                result.Rows.Add(new InquiryRecord
                {
                    InquiryNumber = number,
                    InquiryReference = c[1].Trim(),
                    CustomerName = customer,
                    CustomerLocation = c[3].Trim(),
                    ProjectName = project,
                    ProjectDescription = c[5].Trim(),
                    SalesmanName = c.Count > 6 ? c[6].Trim() : "",
                    PreparedByName = c.Count > 7 ? c[7].Trim() : "",
                    ReceivedAt = c.Count > 8 && DateTime.TryParse(c[8], CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var received)
                        ? DateTime.SpecifyKind(received, DateTimeKind.Local) : DateTime.Now,
                    Status = c.Count > 10 ? InquiryStatuses.Normalize(c[10]) : InquiryStatuses.Received,
                    Priority = "Normal"
                });
            }

            return result;
        }

        /// <summary>
        /// RFC-4180-style CSV record parser. A newline ends a record only
        /// outside a quoted field; doubled quotes inside a quoted field
        /// become one literal quote. A malformed unterminated quote is rejected.
        /// </summary>
        private static List<List<string>> ParseCsvRecords(string csv)
        {
            var records = new List<List<string>>();
            var cells = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < csv.Length; i++)
            {
                char ch = csv[i];
                if (ch == '"')
                {
                    if (quoted && i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (ch == ',' && !quoted)
                {
                    cells.Add(field.ToString());
                    field.Clear();
                }
                else if ((ch == '\r' || ch == '\n') && !quoted)
                {
                    cells.Add(field.ToString());
                    field.Clear();
                    records.Add(cells);
                    cells = new List<string>();
                    if (ch == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                        i++;
                }
                else
                {
                    field.Append(ch);
                }
            }

            if (quoted)
                throw new FormatException("The CSV file ends inside a quoted field.");

            if (field.Length > 0 || cells.Count > 0)
            {
                cells.Add(field.ToString());
                records.Add(cells);
            }

            return records;
        }
    }
}
