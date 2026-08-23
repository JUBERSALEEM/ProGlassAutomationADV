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
    public sealed class InquiryImportExportService
    {
        public async Task ExportCsvAsync(string path, IEnumerable<InquiryListProjection> rows, CancellationToken ct = default)
        {
            static string Q(string? x) => "\"" + (x ?? "").Replace("\"", "\"\"") + "\"";
            var lines = new List<string> { "Inquiry,Reference,Customer,Location,Project,Description,Salesman,Prepared By,Received,Updated,Status,Attachments" };
            lines.AddRange(rows.Select(x => string.Join(",", Q(x.InquiryNumber), Q(x.InquiryReference),
                Q(x.CustomerName), Q(x.CustomerLocation), Q(x.ProjectName), Q(x.ProjectDescription),
                Q(x.SalesmanName), Q(x.PreparedByName), Q(x.ReceivedAt.ToString("o")),
                Q(x.UpdatedAt.ToString("o")), Q(x.Status), x.AttachmentCount.ToString(CultureInfo.InvariantCulture))));
            await File.WriteAllLinesAsync(path, lines, new UTF8Encoding(true), ct).ConfigureAwait(false);
        }

        public async Task<List<InquiryRecord>> ParseCsvAsync(string path, CancellationToken ct = default)
        {
            var lines = await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false);
            var result = new List<InquiryRecord>();
            foreach (string line in lines.Skip(1))
            {
                var c = ParseCsv(line);
                if (c.Count < 6 || string.IsNullOrWhiteSpace(c[2]) || string.IsNullOrWhiteSpace(c[4])) continue;
                result.Add(new InquiryRecord
                {
                    InquiryNumber = c[0], InquiryReference = c[1], CustomerName = c[2],
                    CustomerLocation = c[3], ProjectName = c[4], ProjectDescription = c[5],
                    SalesmanName = c.Count > 6 ? c[6] : "", PreparedByName = c.Count > 7 ? c[7] : "",
                    ReceivedAt = c.Count > 8 && DateTime.TryParse(c[8], out var received)
                        ? DateTime.SpecifyKind(received, DateTimeKind.Local) : DateTime.Now,
                    Status = c.Count > 10 ? InquiryStatuses.Normalize(c[10]) : InquiryStatuses.Received,
                    Priority = "Normal"
                });
            }
            return result;
        }

        private static List<string> ParseCsv(string line)
        {
            var cells = new List<string>(); var b = new StringBuilder(); bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { b.Append('"'); i++; } else quoted = !quoted; }
                else if (ch == ',' && !quoted) { cells.Add(b.ToString()); b.Clear(); }
                else b.Append(ch);
            }
            cells.Add(b.ToString()); return cells;
        }
    }
}
