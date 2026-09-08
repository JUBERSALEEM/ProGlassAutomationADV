// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingExportService.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Export (CSV / Excel / PDF)
//
// PURPOSE:
//   Every report respects current filters, grouping, visible columns,
//   and selected rows for export. Uses the SAME Theme Store action-
//   button/dropdown pattern for Export CSV / Excel / PDF.
//   All work goes through the single CsvExportEngine / ExcelExportEngine
//   / PdfExportEngine already owned by the application.
//
// DESIGN:
//   • One generic entry per report type — layout classes are tiny
//     and live in this file so the module stays cohesive.
//   • Exports are async, cancellation-aware, and never load the whole
//     DB into memory (they stream the already-filtered PagedResult).
//   • Filenames follow ExportFileNamer: {Report}_{Filter}_{Date}.ext
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CsvHelper;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.FileSystem;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Utilities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProGlassAutomation.Services.Reporting
{
    public static class ReportingExportService
    {
        // ── Public helpers — one per format, generic over row type ─────

        public static async Task<ExportResult> ExportCsvAsync<T>(
            IReadOnlyList<T> rows,
            string reportTitle,
            string[] headers,
            Func<T, string[]> rowMapper,
            ExportRequest? request = null,
            ReportDocumentContext? documentContext = null)
        {
            var layout = new GenericCsvLayout<T>(rows, reportTitle, headers, rowMapper, documentContext);
            var engine = new CsvExportEngine();
            return await engine.ExportAsync(layout, request ?? new ExportRequest());
        }

        public static async Task<ExportResult> ExportExcelAsync<T>(
            IReadOnlyList<T> rows,
            string reportTitle,
            string[] headers,
            Func<T, object[]> rowMapper,
            ExportRequest? request = null,
            ReportDocumentContext? documentContext = null)
        {
            var layout = new GenericExcelLayout<T>(rows, reportTitle, headers, rowMapper, documentContext);
            var engine = new ExcelExportEngine();
            return await engine.ExportAsync(layout, request ?? new ExportRequest());
        }

        public static async Task<ExportResult> ExportPdfAsync<T>(
            IReadOnlyList<T> rows,
            string reportTitle,
            string[] headers,
            Func<T, string[]> rowMapper,
            ExportRequest? request = null,
            ReportDocumentContext? documentContext = null)
        {
            var layout = new GenericPdfLayout<T>(rows, reportTitle, headers, rowMapper, documentContext);
            var engine = new PdfExportEngine();
            return await engine.ExportAsync(layout, request ?? new ExportRequest());
        }

        // ── Generic layouts ────────────────────────────────────────────

        private sealed class GenericCsvLayout<T> : ICsvLayout
        {
            private readonly IReadOnlyList<T> _rows;
            private readonly string _title;
            private readonly string[] _headers;
            private readonly Func<T, string[]> _mapper;
            private readonly ReportDocumentContext? _documentContext;

            public GenericCsvLayout(
                IReadOnlyList<T> rows,
                string title,
                string[] headers,
                Func<T, string[]> mapper,
                ReportDocumentContext? documentContext)
            {
                _rows = rows;
                _title = title;
                _headers = headers;
                _mapper = mapper;
                _documentContext = documentContext;
            }

            public string DocumentPrefix => _documentContext?.DocumentPrefix ?? "REP";
            public string DocumentId => ExportFileNamer.FirstFilled(
                _documentContext?.DocumentId,
                _title.Replace(" ", "-").ToUpperInvariant());
            public string DocumentTitle => _title;
            public string CompanyName => _documentContext?.FilenameCustomer ?? "ProGlass";
            public string Reference => _documentContext?.FilenameReference
                ?? DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            public DateTime DocumentDate => _documentContext?.FilenameDate ?? DateTime.Now;

            private bool IsSectionedReport => _headers.Length > 0 &&
                (_headers[0].Equals("SECTION", StringComparison.OrdinalIgnoreCase) ||
                 _headers[0].Equals("CATEGORY", StringComparison.OrdinalIgnoreCase));

            public void WriteRows(CsvWriter csv)
            {
                if (_documentContext != null)
                {
                    csv.WriteField("REPORT");
                    csv.WriteField(_title.ToUpperInvariant());
                    csv.NextRecord();
                    foreach (var field in _documentContext.MetadataFields)
                    {
                        csv.WriteField(field.Key);
                        csv.WriteField(field.Value);
                        csv.NextRecord();
                    }
                    csv.NextRecord();
                }

                if (IsSectionedReport)
                {
                    if (_documentContext == null)
                    {
                        csv.WriteField(_title.ToUpperInvariant());
                        csv.NextRecord();
                        csv.NextRecord();
                    }

                    foreach (var group in _rows
                        .Select(r => _mapper(r))
                        .GroupBy(f => f.Length > 0 ? f[0] ?? "DETAILS" : "DETAILS"))
                    {
                        csv.WriteField(group.Key);
                        csv.NextRecord();
                        foreach (var h in _headers.Skip(1)) csv.WriteField(h);
                        csv.NextRecord();
                        foreach (var fields in group)
                        {
                            foreach (var f in fields.Skip(1)) csv.WriteField(f ?? "");
                            csv.NextRecord();
                        }
                        csv.NextRecord();
                    }
                    return;
                }

                foreach (var h in _headers) csv.WriteField(h);
                csv.NextRecord();
                foreach (var r in _rows)
                {
                    var fields = _mapper(r);
                    foreach (var f in fields) csv.WriteField(f ?? "");
                    csv.NextRecord();
                }
            }
        }

        private sealed class GenericExcelLayout<T> : IExcelLayout
        {
            private readonly IReadOnlyList<T> _rows;
            private readonly string _title;
            private readonly string[] _headers;
            private readonly Func<T, object[]> _mapper;
            private readonly ReportDocumentContext? _documentContext;

            public GenericExcelLayout(
                IReadOnlyList<T> rows,
                string title,
                string[] headers,
                Func<T, object[]> mapper,
                ReportDocumentContext? documentContext)
            {
                _rows = rows;
                _title = title;
                _headers = headers;
                _mapper = mapper;
                _documentContext = documentContext;
            }

            public string DocumentPrefix => _documentContext?.DocumentPrefix ?? "REP";
            public string DocumentId => ExportFileNamer.FirstFilled(
                _documentContext?.DocumentId,
                _title.Replace(" ", "-").ToUpperInvariant());
            public string DocumentTitle => _title;
            public string CompanyName => _documentContext?.FilenameCustomer ?? "ProGlass";
            public string Reference => _documentContext?.FilenameReference
                ?? DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            public DateTime DocumentDate => _documentContext?.FilenameDate ?? DateTime.Now;

            private bool IsSectionedReport => _headers.Length > 0 &&
                (_headers[0].Equals("SECTION", StringComparison.OrdinalIgnoreCase) ||
                 _headers[0].Equals("CATEGORY", StringComparison.OrdinalIgnoreCase));

            public void RenderWorkbook(IXLWorkbook wb)
            {
                var ws = wb.Worksheets.Add("Report");
                int headerRow;
                if (_documentContext != null)
                {
                    int metadataColumns = Math.Max(1, IsSectionedReport ? _headers.Length - 1 : _headers.Length);
                    ws.Range(1, 1, 1, metadataColumns).Merge();
                    ws.Cell(1, 1).Value = _title.ToUpperInvariant();
                    ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.FromHtml("#1D4ED8"));
                    WriteDocumentMetadata(ws, metadataColumns);
                    headerRow = 6;
                }
                else
                {
                    // Keep the historical generic layout intact for every
                    // caller that did not opt into document identity.
                    ws.Cell(1, 1).Value = _title.ToUpperInvariant();
                    ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.FromHtml("#1D4ED8"));
                    ws.Cell(2, 1).Value = $"Generated {DateTime.Now.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)}  •  {_rows.Count} row(s)";
                    ws.Cell(2, 1).Style.Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#6B7280"));
                    headerRow = 4;
                }
                if (IsSectionedReport)
                {
                    int dataCols = Math.Max(1, _headers.Length - 1);
                    int sectionRow = headerRow;
                    bool firstGroup = true;
                    foreach (var group in _rows.Select(r => _mapper(r))
                        .GroupBy(f => f.Length > 0 ? f[0]?.ToString() ?? "DETAILS" : "DETAILS"))
                    {
                        if (!firstGroup) sectionRow += 2;
                        firstGroup = false;

                        ws.Range(sectionRow, 1, sectionRow, dataCols).Merge();
                        ws.Cell(sectionRow, 1).Value = group.Key.ToUpperInvariant();
                        ws.Cell(sectionRow, 1).Style.Font.SetBold().Font.SetFontSize(13).Font.SetFontColor(XLColor.White);
                        ws.Cell(sectionRow, 1).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#1D4ED8"));
                        ws.Cell(sectionRow, 1).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                        sectionRow++;

                        for (int c = 1; c < _headers.Length; c++)
                        {
                            var cell = ws.Cell(sectionRow, c);
                            cell.Value = _headers[c];
                            cell.Style.Font.SetBold().Font.SetFontSize(10).Fill.SetBackgroundColor(XLColor.FromHtml("#DBEAFE")).Font.SetFontColor(XLColor.FromHtml("#1E3A8A"));
                            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                            cell.Style.Border.SetBottomBorder(XLBorderStyleValues.Thin);
                        }
                        sectionRow++;

                        bool alt = false;
                        foreach (var vals in group)
                        {
                            for (int c = 1; c < vals.Length && c < _headers.Length; c++)
                            {
                                var v = vals[c];
                                var cell = ws.Cell(sectionRow, c);
                                if (v is decimal d) { cell.Value = d; cell.Style.NumberFormat.SetFormat("#,##0.00"); }
                                else if (v is int i) { cell.Value = i; }
                                else if (v is double db) { cell.Value = db; }
                                else cell.Value = v?.ToString() ?? "";
                                cell.Style.Font.SetFontSize(10);
                                cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
                                cell.Style.Alignment.SetWrapText(true);
                                cell.Style.Border.SetBottomBorder(XLBorderStyleValues.Hair);
                                if (alt) cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F8FAFC"));
                            }
                            alt = !alt;
                            sectionRow++;
                        }
                    }

                    for (int c = 1; c <= dataCols; c++)
                    {
                        ws.Column(c).AdjustToContents();
                        if (ws.Column(c).Width > 58) ws.Column(c).Width = 58;
                        if (ws.Column(c).Width < 14) ws.Column(c).Width = 14;
                    }
                    ws.SheetView.FreezeRows(headerRow - 1);
                    return;
                }

                for (int c = 0; c < _headers.Length; c++)
                {
                    var cell = ws.Cell(headerRow, c + 1);
                    cell.Value = _headers[c];
                    cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#1D4ED8")).Font.SetFontColor(XLColor.White);
                    cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    cell.Style.Border.SetBottomBorder(XLBorderStyleValues.Thin);
                }

                int row = headerRow + 1;
                foreach (var item in _rows)
                {
                    var vals = _mapper(item);
                    for (int c = 0; c < vals.Length && c < _headers.Length; c++)
                    {
                        var v = vals[c];
                        var cell = ws.Cell(row, c + 1);
                        if (v is decimal d) { cell.Value = d; cell.Style.NumberFormat.SetFormat("#,##0.00"); }
                        else if (v is int i) { cell.Value = i; }
                        else if (v is double db) { cell.Value = db; }
                        else cell.Value = v?.ToString() ?? "";
                        cell.Style.Font.SetFontSize(10);
                    }
                    bool isTotalOrSection = vals.Length > 0 &&
                        (vals[0]?.ToString()?.IndexOf("TOTAL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         vals[0]?.ToString()?.StartsWith("0") == true);
                    if (isTotalOrSection)
                    {
                        for (int c = 1; c <= _headers.Length; c++)
                        {
                            ws.Cell(row, c).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#E0ECFF"));
                            ws.Cell(row, c).Style.Font.SetBold();
                        }
                    }
                    else if ((row - headerRow) % 2 == 0)
                        for (int c = 1; c <= _headers.Length; c++)
                            ws.Cell(row, c).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F8FAFC"));
                    for (int c = 1; c <= _headers.Length; c++)
                    {
                        ws.Cell(row, c).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
                        ws.Cell(row, c).Style.Alignment.SetWrapText(true);
                    }
                    row++;
                }

                for (int c = 1; c <= _headers.Length; c++)
                {
                    ws.Column(c).AdjustToContents();
                    if (ws.Column(c).Width > 46) ws.Column(c).Width = 46;
                    if (ws.Column(c).Width < 12) ws.Column(c).Width = 12;
                }
                ws.SheetView.FreezeRows(headerRow);
                ws.Range(headerRow, 1, headerRow, _headers.Length).SetAutoFilter();
            }

            private void WriteDocumentMetadata(IXLWorksheet ws, int metadataColumns)
            {
                var context = _documentContext!;
                string[] lines =
                {
                    context.PrimaryMetadataLine,
                    context.SecondaryMetadataLine,
                    $"{context.TertiaryMetadataLine}  •  Generated {context.GeneratedAtDisplay}  •  {_rows.Count} row(s)"
                };

                for (int row = 0; row < lines.Length; row++)
                {
                    int worksheetRow = row + 2;
                    ws.Range(worksheetRow, 1, worksheetRow, metadataColumns).Merge();
                    ws.Cell(worksheetRow, 1).Value = lines[row];
                    ws.Cell(worksheetRow, 1).Style.Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#4B5563"));
                    ws.Cell(worksheetRow, 1).Style.Alignment.SetWrapText(true);
                }
            }
        }

        private sealed class GenericPdfLayout<T> : IPdfLayout
        {
            private readonly IReadOnlyList<T> _rows;
            private readonly string _title;
            private readonly string[] _headers;
            private readonly Func<T, string[]> _mapper;
            private readonly ReportDocumentContext? _documentContext;

            public GenericPdfLayout(
                IReadOnlyList<T> rows,
                string title,
                string[] headers,
                Func<T, string[]> mapper,
                ReportDocumentContext? documentContext)
            {
                _rows = rows;
                _title = title;
                _headers = headers;
                _mapper = mapper;
                _documentContext = documentContext;
            }

            public string DocumentPrefix => _documentContext?.DocumentPrefix ?? "REP";
            public string DocumentId => ExportFileNamer.FirstFilled(
                _documentContext?.DocumentId,
                _title.Replace(" ", "-").ToUpperInvariant());
            public string DocumentTitle => _title;
            public string CompanyName => _documentContext?.FilenameCustomer ?? "ProGlass";
            public string Reference => _documentContext?.FilenameReference
                ?? DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            public DateTime DocumentDate => _documentContext?.FilenameDate ?? DateTime.Now;
            public LetterheadInfo? Letterhead => null;
            public PageLayout PageLayout => new PageLayout { Orientation = _headers.Length > 7 ? PageOrientation.Landscape : PageOrientation.Portrait };

            private bool IsSectionedReport => _headers.Length > 0 &&
                (_headers[0].Equals("SECTION", StringComparison.OrdinalIgnoreCase) ||
                 _headers[0].Equals("CATEGORY", StringComparison.OrdinalIgnoreCase));

            public void RenderContent(ColumnDescriptor column)
            {
                column.Item().Text(_title).FontSize(18).Bold().FontColor("#1D4ED8");
                if (_documentContext != null)
                {
                    column.Item().PaddingTop(3).Text(_documentContext.PrimaryMetadataLine).FontSize(8).FontColor("#4B5563");
                    column.Item().Text(_documentContext.SecondaryMetadataLine).FontSize(8).FontColor("#4B5563");
                    column.Item().Text(
                        $"{_documentContext.TertiaryMetadataLine}  •  Generated {_documentContext.GeneratedAtDisplay}  •  {_rows.Count} row(s)")
                        .FontSize(8)
                        .FontColor("#6B7280");
                }
                else
                {
                    column.Item().Text(
                        $"Generated {DateTime.Now.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)}  •  {_rows.Count} row(s)")
                        .FontSize(8)
                        .FontColor("#6B7280");
                }
                column.Item().PaddingTop(8).LineHorizontal(1).LineColor("#E5E7EB");

                if (IsSectionedReport)
                {
                    int maxCellChars = _headers.Length <= 4 ? 260 : _headers.Length <= 8 ? 170 : 110;
                    foreach (var group in _rows.Select(r => _mapper(r))
                        .GroupBy(f => f.Length > 0 ? f[0] ?? "DETAILS" : "DETAILS"))
                    {
                        column.Item().PaddingTop(12).Border(1).BorderColor("#BFDBFE").Background("#F8FAFC").Padding(8).Column(block =>
                        {
                            block.Item().Text(group.Key.ToUpperInvariant()).FontSize(12).Bold().FontColor("#1D4ED8");
                            block.Item().PaddingTop(5).LineHorizontal(0.5f).LineColor("#DBEAFE");
                            block.Item().PaddingTop(6).Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    for (int i = 1; i < _headers.Length; i++) c.RelativeColumn();
                                });

                                table.Header(h =>
                                {
                                    for (int i = 1; i < _headers.Length; i++)
                                        h.Cell().Background("#DBEAFE").Padding(4).Text(_headers[i]).FontSize(8).Bold().FontColor("#1E3A8A").AlignCenter();
                                });

                                bool alt = false;
                                foreach (var fields in group)
                                {
                                    for (int i = 1; i < fields.Length && i < _headers.Length; i++)
                                    {
                                        var txt = fields[i] ?? "";
                                        if (txt.Length > maxCellChars) txt = txt.Substring(0, Math.Max(1, maxCellChars - 1)) + "…";
                                        table.Cell().Background(alt ? "#FFFFFF" : "#F8FAFC").BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(5).Text(txt).FontSize(_headers.Length <= 5 ? 8.5f : 8f).FontColor("#1F2937");
                                    }
                                    alt = !alt;
                                }
                            });
                        });
                    }
                    return;
                }

                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        foreach (var _ in _headers) c.RelativeColumn();
                    });

                    table.Header(h =>
                    {
                        foreach (var hd in _headers)
                            h.Cell().Background("#1D4ED8").Padding(4).Text(hd).FontSize(8).Bold().FontColor(Colors.White).AlignCenter();
                    });

                    bool alt = false;
                    int maxCellChars = _headers.Length <= 4 ? 170 : _headers.Length <= 8 ? 105 : _headers.Length <= 10 ? 80 : 52;
                    int take = _rows.Count;
                    for (int i = 0; i < take; i++)
                    {
                        var fields = _mapper(_rows[i]);
                        foreach (var f in fields)
                        {
                            var txt = f ?? "";
                            if (txt.Length > maxCellChars) txt = txt.Substring(0, Math.Max(1, maxCellChars - 1)) + "…";
                            table.Cell().Background(alt ? "#F8FAFC" : Colors.White).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(4).Text(txt).FontSize(_headers.Length <= 8 ? 7.5f : 7f).FontColor("#1F2937");
                        }
                        alt = !alt;
                    }
                });
            }
        }

        // ── Preset mappers for each report type (export wiring) ───────

        public static string[] PiHeaders => new[] { "PI #", "Date", "Status", "Prepared By", "Salesman", "Company", "Customer", "Project", "Grand Total", "Rows", "Qty", "SQM", "Has JO", "Has DN", "Has TI" };
        public static string[] PiMap(PiReportRow r) => new[] { r.ProformaId, r.IssueDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), r.Status, r.PreparedBy, r.Salesman, r.CompanyName, r.CustomerName, r.ProjectName, r.GrandTotal.ToString("N2", CultureInfo.InvariantCulture), r.TotalRows.ToString(), r.TotalQty.ToString(), r.TotalSqm.ToString("N3", CultureInfo.InvariantCulture), r.HasJobOrder ? "Yes" : "No", r.HasDelivery ? "Yes" : "No", r.HasTaxInvoice ? "Yes" : "No" };
        public static object[] PiMapExcel(PiReportRow r) => new object[] { r.ProformaId, r.IssueDate, r.Status, r.PreparedBy, r.Salesman, r.CompanyName, r.CustomerName, r.ProjectName, r.GrandTotal, r.TotalRows, r.TotalQty, r.TotalSqm, r.HasJobOrder ? "Yes" : "No", r.HasDelivery ? "Yes" : "No", r.HasTaxInvoice ? "Yes" : "No" };

        public static string[] JoHeaders => new[] { "JO #", "PI #", "Company", "Customer", "Project", "Salesman", "Prepared By", "Status", "Priority", "Delivery Date", "Grand Total", "Qty", "SQM", "Delivered", "Balance" };
        public static string[] JoMap(JoReportRow r) => new[] { r.JobOrderId, r.SourceProformaId, r.CompanyName, r.CustomerName, r.ProjectName, r.Salesman, r.PreparedBy, r.Status, r.Priority, r.DeliveryDate?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "—", r.GrandTotal.ToString("N2", CultureInfo.InvariantCulture), r.TotalQty.ToString(), r.TotalSqm.ToString("N3", CultureInfo.InvariantCulture), r.DeliveredQty.ToString(), r.BalanceQty.ToString() };
        public static object[] JoMapExcel(JoReportRow r) => new object[] { r.JobOrderId, r.SourceProformaId, r.CompanyName, r.CustomerName, r.ProjectName, r.Salesman, r.PreparedBy, r.Status, r.Priority, r.DeliveryDate?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "—", r.GrandTotal, r.TotalQty, r.TotalSqm, r.DeliveredQty, r.BalanceQty };

        // Row-level delivery report: one export row per exact PI spec row.
        public static string[] DnHeaders => new[] { "Sr No", "PI #", "JO #", "Specification", "Company", "Customer", "Project", "Salesman", "Glass Ref", "W1", "H1", "W2", "H2", "Total Qty", "Delivered Qty", "Balance Qty", "Delivery Status" };
        public static string[] DnMap(DeliveryReportRow r) => new[] { r.SerialNumber.ToString(), r.ProformaId, r.JobOrderId, r.Specification, r.CompanyName, r.CustomerName, r.ProjectName, r.Salesman, r.GlassRef, r.W1.ToString(), r.H1.ToString(), r.W2.ToString(), r.H2.ToString(), r.TotalQty.ToString(), r.DeliveredQty.ToString(), r.BalanceQty.ToString(), r.DeliveryStatus };
        public static object[] DnMapExcel(DeliveryReportRow r) => new object[] { r.SerialNumber, r.ProformaId, r.JobOrderId, r.Specification, r.CompanyName, r.CustomerName, r.ProjectName, r.Salesman, r.GlassRef, r.W1, r.H1, r.W2, r.H2, r.TotalQty, r.DeliveredQty, r.BalanceQty, r.DeliveryStatus };

        public static string[] TiHeaders => new[] { "Invoice #", "PI #", "JO #", "DN Ref", "Company", "Customer", "Project", "Salesman", "Prepared By", "Status", "Type", "Issue Date", "Due Date", "Grand Total", "VAT", "Qty", "SQM" };
        public static string[] TiMap(TaxInvoiceReportRow r) => new[] { r.InvoiceNumber, r.ProformaId, r.JobOrderId, r.DeliveryNoteReference, r.CompanyName, r.CustomerName, r.ProjectName, r.Salesman, r.PreparedBy, r.Status, r.InvoiceType, r.IssueDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), r.DueDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), r.GrandTotal.ToString("N2", CultureInfo.InvariantCulture), r.VatAmount.ToString("N2", CultureInfo.InvariantCulture), r.TotalQty.ToString(), r.TotalSqm.ToString("N3", CultureInfo.InvariantCulture) };
        public static object[] TiMapExcel(TaxInvoiceReportRow r) => new object[] { r.InvoiceNumber, r.ProformaId, r.JobOrderId, r.DeliveryNoteReference, r.CompanyName, r.CustomerName, r.ProjectName, r.Salesman, r.PreparedBy, r.Status, r.InvoiceType, r.IssueDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), r.DueDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), r.GrandTotal, r.VatAmount, r.TotalQty, r.TotalSqm };

        public static string[] GlassHeaders => new[] { "Row ID", "PI #", "JO #", "DN #", "TI #", "Glass Ref", "Spec", "Type", "W1", "H1", "W2", "H2", "Qty", "SQM", "Rate", "Amount", "Delivered", "Balance", "Customer", "Project" };
        public static string[] GlassMap(GlassSpecReportRow r) => new[] { r.RowId, r.ProformaId, r.JobOrderId, r.DeliveryNoteId, r.TaxInvoiceNumber, r.GlassRef, r.SpecName, r.GlassType, r.W1.ToString(), r.H1.ToString(), r.W2.ToString(), r.H2.ToString(), r.Qty.ToString(), r.TotalSqm.ToString("N3", CultureInfo.InvariantCulture), r.Rate.ToString("N2", CultureInfo.InvariantCulture), r.Amount.ToString("N2", CultureInfo.InvariantCulture), r.DeliveredQty.ToString(), r.BalanceQty.ToString(), r.CustomerName, r.ProjectName };
        public static object[] GlassMapExcel(GlassSpecReportRow r) => new object[] { r.RowId, r.ProformaId, r.JobOrderId, r.DeliveryNoteId, r.TaxInvoiceNumber, r.GlassRef, r.SpecName, r.GlassType, r.W1, r.H1, r.W2, r.H2, r.Qty, r.TotalSqm, r.Rate, r.Amount, r.DeliveredQty, r.BalanceQty, r.CustomerName, r.ProjectName };

        public static string[] CustomerHeaders => new[] { "Customer", "Salesman", "PIs", "JOs", "DNs", "TIs", "PI Value", "Invoiced", "Outstanding", "Qty", "SQM" };
        public static string[] CustomerMap(CustomerReportRow r) => new[] { r.CustomerName, r.Salesman, r.PiCount.ToString(), r.JoCount.ToString(), r.DnCount.ToString(), r.TiCount.ToString(), r.PiValue.ToString("N2", CultureInfo.InvariantCulture), r.InvoicedValue.ToString("N2", CultureInfo.InvariantCulture), r.Outstanding.ToString("N2", CultureInfo.InvariantCulture), r.TotalQty.ToString(), r.TotalSqm.ToString("N3", CultureInfo.InvariantCulture) };
        public static object[] CustomerMapExcel(CustomerReportRow r) => new object[] { r.CustomerName, r.Salesman, r.PiCount, r.JoCount, r.DnCount, r.TiCount, r.PiValue, r.InvoicedValue, r.Outstanding, r.TotalQty, r.TotalSqm };

        public static string[] CompanyHeaders => new[] { "Company", "TRN", "PIs", "JOs", "DNs", "TIs", "PI Value", "Invoiced", "Statements", "Overdue" };
        public static string[] CompanyMap(CompanyReportRow r) => new[] { r.CompanyName, r.CompanyTrn, r.PiCount.ToString(), r.JobOrderCount.ToString(), r.DeliveryCount.ToString(), r.TaxInvoiceCount.ToString(), r.PiValue.ToString("N2", CultureInfo.InvariantCulture), r.InvoicedValue.ToString("N2", CultureInfo.InvariantCulture), r.StatementCount.ToString(), r.OverdueInvoices.ToString() };
        public static object[] CompanyMapExcel(CompanyReportRow r) => new object[] { r.CompanyName, r.CompanyTrn, r.PiCount, r.JobOrderCount, r.DeliveryCount, r.TaxInvoiceCount, r.PiValue, r.InvoicedValue, r.StatementCount, r.OverdueInvoices };

        public static string[] SalesmanHeaders => new[] { "Salesman", "PIs", "Confirmed", "Confirmed Value", "JOs", "Deliveries", "TIs", "Customers", "Projects", "Revenue", "Profit", "Margin%" };
        public static string[] SalesmanMap(SalesmanReportRow r) => new[] { r.SalesmanName, r.PiCount.ToString(), r.ConfirmedPiCount.ToString(), r.ConfirmedValue.ToString("N2", CultureInfo.InvariantCulture), r.JobOrderCount.ToString(), r.DeliveryCount.ToString(), r.TaxInvoiceCount.ToString(), r.CustomerCount.ToString(), r.ProjectCount.ToString(), r.Revenue.ToString("N2", CultureInfo.InvariantCulture), r.Profit.ToString("N2", CultureInfo.InvariantCulture), r.Margin.ToString("N1", CultureInfo.InvariantCulture) };
        public static object[] SalesmanMapExcel(SalesmanReportRow r) => new object[] { r.SalesmanName, r.PiCount, r.ConfirmedPiCount, r.ConfirmedValue, r.JobOrderCount, r.DeliveryCount, r.TaxInvoiceCount, r.CustomerCount, r.ProjectCount, r.Revenue, r.Profit, r.Margin };

        public static string[] ProjectHeaders => new[] { "Project", "Contract #", "Customer", "Company", "Salesman", "PIs", "JOs", "DNs", "TIs", "PI Value", "Invoiced", "Qty", "SQM" };
        public static string[] ProjectMap(ProjectReportRow r) => new[] { r.ProjectName, r.ProjectContractNo, r.CustomerName, r.CompanyName, r.Salesman, r.PiCount.ToString(), r.JoCount.ToString(), r.DnCount.ToString(), r.TiCount.ToString(), r.PiValue.ToString("N2", CultureInfo.InvariantCulture), r.InvoicedValue.ToString("N2", CultureInfo.InvariantCulture), r.TotalQty.ToString(), r.TotalSqm.ToString("N3", CultureInfo.InvariantCulture) };
        public static object[] ProjectMapExcel(ProjectReportRow r) => new object[] { r.ProjectName, r.ProjectContractNo, r.CustomerName, r.CompanyName, r.Salesman, r.PiCount, r.JoCount, r.DnCount, r.TiCount, r.PiValue, r.InvoicedValue, r.TotalQty, r.TotalSqm };

        public static string[] StatementHeaders => new[] { "Statement #", "Customer", "Company", "Project", "Invoices", "Invoiced", "Outstanding", "Overdue", "Date" };
        public static string[] StatementMap(StatementReportRow r) => new[] { r.StatementNumber, r.CustomerName, r.CompanyName, r.ProjectName, r.InvoiceCount.ToString(), r.TotalInvoiced.ToString("N2", CultureInfo.InvariantCulture), r.TotalOutstanding.ToString("N2", CultureInfo.InvariantCulture), r.OverdueAmount.ToString("N2", CultureInfo.InvariantCulture), r.CreatedAt.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) };
        public static object[] StatementMapExcel(StatementReportRow r) => new object[] { r.StatementNumber, r.CustomerName, r.CompanyName, r.ProjectName, r.InvoiceCount, r.TotalInvoiced, r.TotalOutstanding, r.OverdueAmount, r.CreatedAt.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) };

        // Financial statement ledger (Phase 8): one row per statement line item.
        public static string[] StatementLineHeaders => new[] { "Date", "Document #", "Type", "Description", "PI #", "Debit", "Credit", "Balance", "Statement" };
        public static string[] StatementLineMap(StatementLineRow r) => new[] { r.IssueDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), r.InvoiceNumber, r.DocumentType, r.Description, r.ProformaId, r.Debit.ToString("N2", CultureInfo.InvariantCulture), r.Credit.ToString("N2", CultureInfo.InvariantCulture), r.Balance.ToString("N2", CultureInfo.InvariantCulture), r.StatementNumber };
        public static object[] StatementLineMapExcel(StatementLineRow r) => new object[] { r.IssueDate, r.InvoiceNumber, r.DocumentType, r.Description, r.ProformaId, r.Debit, r.Credit, r.Balance, r.StatementNumber };

        public static string[] ProfitHeaders => new[] { "Document", "Category", "Revenue", "Cost", "Glass Cost", "Prod Cost", "Profit", "Margin%", "Qty", "SQM", "Has Cost?" };
        public static string[] ProfitMap(ProfitabilityRow r) => new[] { r.Name, r.Category, r.Revenue.ToString("N2", CultureInfo.InvariantCulture), r.Cost.ToString("N2", CultureInfo.InvariantCulture), r.GlassCost.ToString("N2", CultureInfo.InvariantCulture), r.ProductionCost.ToString("N2", CultureInfo.InvariantCulture), r.Profit.ToString("N2", CultureInfo.InvariantCulture), r.Margin.ToString("N1", CultureInfo.InvariantCulture), r.Qty.ToString(), r.Sqm.ToString("N3", CultureInfo.InvariantCulture), r.HasCostData ? "Yes" : "No" };
        public static object[] ProfitMapExcel(ProfitabilityRow r) => new object[] { r.Name, r.Category, r.Revenue, r.Cost, r.GlassCost, r.ProductionCost, r.Profit, r.Margin, r.Qty, r.Sqm, r.HasCostData ? "Yes" : "No" };
    }
}
