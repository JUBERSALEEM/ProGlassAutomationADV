// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 23:
//   Services/Reporting/ReportingExportService.cs — the GENERIC CSV path
//   (GenericCsvLayout.WriteRows + the per-report header/map sets) and
//   Services/Export/FileSystem/ExportFileNamer.cs.
//
// The WPF renders through CsvExportEngine (CsvHelper); this port writes
// the same layout — optional metadata block (REPORT/title + key/value
// rows + blank line), header row, data rows, sectioned reports grouped
// by the first column — as RFC 4180 text: comma separated, CRLF, fields
// containing a comma/quote/newline quoted with inner quotes doubled.
//
// NOT ported (documented deferrals): the Excel engine (needs the
// ClosedXML dependency + a build environment to verify) and the PDF
// engine (user-deferred, never). The CSV carries the same rows.
// ═══════════════════════════════════════════════════════════════════════

using System.Globalization;
using System.Text;

namespace ProGlass.Saas.Application.Reporting;

/// <summary>The export request's descriptive metadata (WPF
/// ReportDocumentContext reduced to what the API layer carries).</summary>
public sealed record CsvExportMetadata(
    string Title,
    IReadOnlyList<(string Key, string Value)> Fields);

public static class ReportingCsv
{
    /// <summary>
    /// Builds the CSV text for a report (the WPF GenericCsvLayout
    /// row-writing semantics).
    /// </summary>
    public static string Build<T>(
        IReadOnlyList<T> rows,
        string reportTitle,
        string[] headers,
        Func<T, string?[]> rowMapper,
        CsvExportMetadata? metadata = null)
    {
        var sb = new StringBuilder();

        // Metadata block: REPORT + title, one key/value row per field,
        // then a blank line (the WPF writes this when a document context
        // is supplied).
        if (metadata != null)
        {
            Field(sb, "REPORT");
            Field(sb, metadata.Title.ToUpperInvariant());
            End(sb);
            foreach (var (key, value) in metadata.Fields)
            {
                Field(sb, key);
                Field(sb, value);
                End(sb);
            }
            End(sb);
        }

        bool sectioned = headers.Length > 0
            && (headers[0].Equals("SECTION", StringComparison.OrdinalIgnoreCase)
                || headers[0].Equals("CATEGORY", StringComparison.OrdinalIgnoreCase));

        if (sectioned)
        {
            if (metadata == null)
            {
                Field(sb, reportTitle.ToUpperInvariant());
                End(sb);
                End(sb);
            }

            foreach (var group in rows
                         .Select(r => rowMapper(r))
                         .GroupBy(f => f.Length > 0 ? f[0] ?? "DETAILS" : "DETAILS"))
            {
                Field(sb, group.Key);
                End(sb);
                foreach (var h in headers.Skip(1)) Field(sb, h);
                End(sb);
                foreach (var fields in group)
                {
                    foreach (var f in fields.Skip(1)) Field(sb, f ?? "");
                    End(sb);
                }
                End(sb);
            }
            return sb.ToString();
        }

        foreach (var h in headers) Field(sb, h);
        End(sb);
        foreach (var r in rows)
        {
            var fields = rowMapper(r);
            foreach (var f in fields) Field(sb, f ?? "");
            End(sb);
        }

        return sb.ToString();
    }

    private static void Field(StringBuilder sb, string? value)
    {
        var v = value ?? "";
        if (v.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
        {
            sb.Append('"').Append(v.Replace("\"", "\"\"")).Append('"');
        }
        else
        {
            sb.Append(v);
        }
        sb.Append(',');
    }

    private static void End(StringBuilder sb)
    {
        if (sb.Length > 0 && sb[sb.Length - 1] == ',') sb.Length--;
        sb.Append("\r\n");
    }
}

/// <summary>
/// Export file naming (the WPF ExportFileNamer port): document id,
/// company and reference segments sanitized to A-Z0-9- (max 48 chars
/// each, repeated hyphens collapsed), joined with underscores, date
/// dd-MM-yyyy, extension last.
/// </summary>
public static class ExportFileNamer
{
    private const int MaxSegmentLength = 48;

    public static string Build(
        string? documentId, string? company, string? reference,
        DateTime documentDate, string extension)
    {
        var segments = new List<string>();

        string id = Sanitize(documentId);
        if (string.IsNullOrWhiteSpace(id)) id = "DOCUMENT";
        segments.Add(id);

        string c = Sanitize(company);
        if (!string.IsNullOrWhiteSpace(c)) segments.Add(c);

        string r = Sanitize(reference);
        if (!string.IsNullOrWhiteSpace(r)) segments.Add(r);

        DateTime date = documentDate == default ? DateTime.Now : documentDate;
        segments.Add(date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));

        return string.Join("_", segments) + "." + extension;
    }

    /// <summary>WPF ExportFileNamer.Sanitize verbatim: whitespace runs
    /// collapse to one hyphen, other characters uppercase, invalid file
    /// name chars and non-alphanumerics are REMOVED, repeated hyphens
    /// collapse, trimmed, capped.</summary>
    private static string Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sb = new StringBuilder(input.Length);
        bool lastWasSpace = false;
        foreach (var ch in input.Trim())
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    sb.Append('-');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(char.ToUpperInvariant(ch));
                lastWasSpace = false;
            }
        }

        var s = sb.ToString();

        foreach (var bad in System.IO.Path.GetInvalidFileNameChars())
            s = s.Replace(bad.ToString(), string.Empty);

        s = RemoveNonAlphaNumOrHyphen(s);
        s = CollapseHyphens(s);
        s = s.Trim('-');

        if (s.Length > MaxSegmentLength)
            s = s[..MaxSegmentLength].Trim('-');

        return s;
    }

    private static string RemoveNonAlphaNumOrHyphen(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (char.IsAsciiLetterOrDigit(ch) || ch == '-')
                sb.Append(ch);
        return sb.ToString();
    }

    private static string CollapseHyphens(string s)
    {
        while (s.Contains("--")) s = s.Replace("--", "-");
        return s;
    }
}

// ── per-report header/map sets (WPF ReportingExportService sets,
//    adapted to the SaaS row shapes; columns the SaaS read model does
//    not carry render "" — documented, never invented) ────────────────

public static class ReportingCsvLayouts
{
    /// <summary>WPF JoHeaders (the SaaS carries no Prepared By on the
    /// analytics row — renders "").</summary>
    public static readonly string[] JoHeaders =
    {
        "JO #", "PI #", "Company", "Customer", "Project", "Salesman",
        "Prepared By", "Status", "Priority", "Delivery Date", "Grand Total",
        "Qty", "SQM", "Delivered", "Balance"
    };

    public static string?[] JoMap(JoReportRow r) => new[]
    {
        r.JobNo,
        r.ProformaNo,
        r.CompanyName,
        r.CustomerName,
        r.ProjectName,
        r.Salesman,
        "",   // Prepared By: not carried on the SaaS analytics row (documented)
        r.Status,
        r.Priority,
        r.DeliveryDate?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "—",
        r.GrandTotal.ToString("N2", CultureInfo.InvariantCulture),
        r.TotalQty.ToString(CultureInfo.InvariantCulture),
        r.TotalSqm.ToString("N3", CultureInfo.InvariantCulture),
        r.DeliveredQty.ToString(CultureInfo.InvariantCulture),
        r.BalanceQty.ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>WPF DnHeaders (Company/Salesman render "" on the SaaS
    /// delivery row).</summary>
    public static readonly string[] DnHeaders =
    {
        "Sr No", "PI #", "JO #", "Specification", "Company", "Customer",
        "Project", "Salesman", "Glass Ref", "W1", "H1", "W2", "H2",
        "Total Qty", "Delivered Qty", "Balance Qty", "Delivery Status"
    };

    public static string?[] DnMap(DeliveryReportRow r) => new[]
    {
        r.SerialNumber.ToString(CultureInfo.InvariantCulture),
        r.ProformaNo,
        r.JobOrderId?.ToString("N") ?? "",
        r.Specification,
        "",   // Company: not carried on the delivery row (documented)
        r.CustomerName,
        r.ProjectName,
        "",   // Salesman: not carried on the delivery row (documented)
        r.GlassRef,
        r.W1.ToString(CultureInfo.InvariantCulture),
        r.H1.ToString(CultureInfo.InvariantCulture),
        r.W2.ToString(CultureInfo.InvariantCulture),
        r.H2.ToString(CultureInfo.InvariantCulture),
        r.TotalQty.ToString(CultureInfo.InvariantCulture),
        r.DeliveredQty.ToString(CultureInfo.InvariantCulture),
        r.BalanceQty.ToString(CultureInfo.InvariantCulture),
        r.DeliveredQty <= 0 ? "Not delivered"
            : r.DeliveredQty >= r.TotalQty ? "Fully delivered"
            : "Partially delivered"
    };

    /// <summary>Profitability headers (the WPF exports the
    /// profitability grid through the same generic engine).</summary>
    public static readonly string[] ProfitabilityHeaders =
    {
        "Key", "Category", "Revenue", "Cost", "Glass Cost", "Production Cost",
        "Profit", "Margin %", "Qty", "SQM", "Has Cost", "Cost Source"
    };

    public static string?[] ProfitabilityMap(ProfitabilityRowDto r) => new[]
    {
        r.Key,
        r.Category,
        r.Revenue.ToString("N2", CultureInfo.InvariantCulture),
        r.Cost.ToString("N2", CultureInfo.InvariantCulture),
        r.GlassCost.ToString("N2", CultureInfo.InvariantCulture),
        r.ProductionCost.ToString("N2", CultureInfo.InvariantCulture),
        r.Profit.ToString("N2", CultureInfo.InvariantCulture),
        r.Margin.ToString("N1", CultureInfo.InvariantCulture),
        r.Qty.ToString(CultureInfo.InvariantCulture),
        r.Sqm.ToString("N3", CultureInfo.InvariantCulture),
        r.HasCostData ? "Yes" : "No",
        r.CostSource
    };
}
