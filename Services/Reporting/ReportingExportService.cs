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

using ClosedXML.Excel;
using CsvHelper;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Utilities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            ExportRequest? request = null)
        {
            var layout = new GenericCsvLayout<T>(rows, reportTitle, headers, rowMapper);
            var engine = new CsvExportEngine();
            return await engine.ExportAsync(layout, request ?? new ExportRequest());
        }

        public static async Task<ExportResult> ExportExcelAsync<T>(
            IReadOnlyList<T> rows,
            string reportTitle,
            string[] headers,
            Func<T, object[]> rowMapper,
            ExportRequest? request = null)
        {
            var layout = new GenericExcelLayout<T>(rows, reportTitle, headers, rowMapper);
            var engine = new ExcelExportEngine();
            return await engine.ExportAsync(layout, request ?? new ExportRequest());
        }

        public static async Task<ExportResult> ExportPdfAsync<T>(
            IReadOnlyList<T> rows,
            string reportTitle,
            string[] headers,
            Func<T, string[]> rowMapper,
            ExportRequest? request = null)
        {
            var layout = new GenericPdfLayout<T>(rows, reportTitle, headers, rowMapper);
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

            public GenericCsvLayout(IReadOnlyList<T> rows, string title, string[] headers, Func<T, string[]> mapper)
            {
                _rows = rows; _title = title; _headers = headers; _mapper = mapper;
            }

            public string DocumentPrefix => "REP";
            public string DocumentId => _title.Replace(" ", "-").ToUpperInvariant();
            public string DocumentTitle => _title;
            public string CompanyName => "ProGlass";
            public string Reference => DateTime.Now.ToString("yyyy-MM-dd");
            public DateTime DocumentDate => DateTime.Now;

            public void WriteRows(CsvWriter csv)
            {
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

            public GenericExcelLayout(IReadOnlyList<T> rows, string title, string[] headers, Func<T, object[]> mapper)
            {
                _rows = rows; _title = title; _headers = headers; _mapper = mapper;
            }

            public string DocumentPrefix => "REP";
            public string DocumentId => _title.Replace(" ", "-").ToUpperInvariant();
            public string DocumentTitle => _title;
            public string CompanyName => "ProGlass";
            public string Reference => DateTime.Now.ToString("yyyy-MM-dd");
            public DateTime DocumentDate => DateTime.Now;

            public void RenderWorkbook(IXLWorkbook wb)
            {
                var ws = wb.Worksheets.Add("Report");
                ws.Cell(1, 1).Value = _title.ToUpperInvariant();
                ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.FromHtml("#1D4ED8"));
                ws.Cell(2, 1).Value = $"Generated {DateTime.Now:dd MMM yyyy HH:mm}  •  {_rows.Count} row(s)";
                ws.Cell(2, 1).Style.Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#6B7280"));

                int headerRow = 4;
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
                    if ((row - headerRow) % 2 == 0)
                        for (int c = 1; c <= _headers.Length; c++)
                            ws.Cell(row, c).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F8FAFC"));
                    row++;
                }

                for (int c = 1; c <= _headers.Length; c++)
                {
                    ws.Column(c).AdjustToContents();
                    if (ws.Column(c).Width > 32) ws.Column(c).Width = 32;
                    if (ws.Column(c).Width < 12) ws.Column(c).Width = 12;
                }
                ws.SheetView.FreezeRows(headerRow);
                ws.Range(headerRow, 1, headerRow, _headers.Length).SetAutoFilter();
            }
        }

        private sealed class GenericPdfLayout<T> : IPdfLayout
        {
            private readonly IReadOnlyList<T> _rows;
            private readonly string _title;
            private readonly string[] _headers;
            private readonly Func<T, string[]> _mapper;

            public GenericPdfLayout(IReadOnlyList<T> rows, string title, string[] headers, Func<T, string[]> mapper)
            {
                _rows = rows; _title = title; _headers = headers; _mapper = mapper;
            }

            public string DocumentPrefix => "REP";
            public string DocumentId => _title.Replace(" ", "-").ToUpperInvariant();
            public string DocumentTitle => _title;
            public string CompanyName => "ProGlass";
            public string Reference => DateTime.Now.ToString("yyyy-MM-dd");
            public DateTime DocumentDate => DateTime.Now;
            public LetterheadInfo? Letterhead => null;
            public PageLayout PageLayout => new PageLayout { Orientation = _headers.Length > 7 ? PageOrientation.Landscape : PageOrientation.Portrait };

            public void RenderContent(ColumnDescriptor column)
            {
                column.Item().Text(_title).FontSize(16).Bold().FontColor("#1D4ED8");
                column.Item().Text($"Generated {DateTime.Now:dd MMM yyyy HH:mm}  •  {_rows.Count} row(s)").FontSize(8).FontColor("#6B7280");
                column.Item().PaddingTop(8).LineHorizontal(1).LineColor("#E5E7EB");

                column.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        foreach (var _ in _headers) c.RelativeColumn();
                    });

                    table.Header(h =>
                    {
                        foreach (var hd in _headers)
                            h.Cell().Background("#1D4ED8").Padding(4).Text(hd).FontSize(7).Bold().FontColor(Colors.White).AlignCenter();
                    });

                    bool alt = false;
                    // Paginate: first 40 rows, then continue
                    int take = Math.Min(_rows.Count, 200);
                    for (int i = 0; i < take; i++)
                    {
                        var fields = _mapper(_rows[i]);
                        foreach (var f in fields)
                        {
                            var txt = f ?? "";
                            if (txt.Length > 42) txt = txt.Substring(0, 39) + "…";
                            table.Cell().Background(alt ? "#F8FAFC" : Colors.White).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(3).Text(txt).FontSize(6).FontColor("#1F2937");
                        }
                        alt = !alt;
                    }
                    if (_rows.Count > take)
                    {
                        foreach (var _ in _headers)
                            table.Cell().Padding(4).Text("…").FontSize(6).FontColor("#6B7280").Italic();
                    }
                });

                if (_rows.Count > 200)
                    column.Item().PaddingTop(6).Text($"Showing first 200 of {_rows.Count} rows — export CSV/Excel for the complete dataset.").FontSize(7).FontColor("#6B7280").Italic();
            }
        }

        // ── Preset mappers for each report type (export wiring) ───────

        public static string[] PiHeaders => new[] { "PI #", "Status", "Company", "Customer", "Project", "Salesman", "Issue Date", "Grand Total", "Rows", "Qty", "SQM", "Has JO", "Has DN", "Has TI" };
        public static string[] PiMap(PiReportRow r) => new[] { r.ProformaId, r.Status, r.CompanyName, r.CustomerName, r.ProjectName, r.Salesman, r.IssueDate.ToString("dd MMM yyyy"), r.GrandTotal.ToString("N2"), r.TotalRows.ToString(), r.TotalQty.ToString(), r.TotalSqm.ToString("N3"), r.HasJobOrder ? "Yes" : "No", r.HasDelivery ? "Yes" : "No", r.HasTaxInvoice ? "Yes" : "No" };
        public static object[] PiMapExcel(PiReportRow r) => new object[] { r.ProformaId, r.Status, r.CompanyName, r.CustomerName, r.ProjectName, r.Salesman, r.IssueDate.ToString("dd MMM yyyy"), r.GrandTotal, r.TotalRows, r.TotalQty, r.TotalSqm, r.HasJobOrder ? "Yes" : "No", r.HasDelivery ? "Yes" : "No", r.HasTaxInvoice ? "Yes" : "No" };

        public static string[] JoHeaders => new[] { "JO #", "PI #", "Customer", "Project", "Status", "Priority", "Delivery Date", "Grand Total", "Qty", "SQM", "Delivered", "Balance" };
        public static string[] JoMap(JoReportRow r) => new[] { r.JobOrderId, r.SourceProformaId, r.CustomerName, r.ProjectName, r.Status, r.Priority, r.DeliveryDate?.ToString("dd MMM yyyy") ?? "—", r.GrandTotal.ToString("N2"), r.TotalQty.ToString(), r.TotalSqm.ToString("N3"), r.DeliveredQty.ToString(), r.BalanceQty.ToString() };
        public static object[] JoMapExcel(JoReportRow r) => new object[] { r.JobOrderId, r.SourceProformaId, r.CustomerName, r.ProjectName, r.Status, r.Priority, r.DeliveryDate?.ToString("dd MMM yyyy") ?? "—", r.GrandTotal, r.TotalQty, r.TotalSqm, r.DeliveredQty, r.BalanceQty };

        // Row-level delivery report: one export row per exact PI spec row.
        public static string[] DnHeaders => new[] { "Sr No", "PI #", "JO #", "Specification", "Company", "Customer", "Project", "Salesman", "Glass Ref", "W1", "H1", "W2", "H2", "Total Qty", "Delivered Qty", "Balance Qty", "Delivery Status" };
        public static string[] DnMap(DeliveryReportRow r) => new[] { r.SerialNumber.ToString(), r.ProformaId, r.JobOrderId, r.Specification, r.CompanyName, r.CustomerName, r.ProjectName, r.Salesman, r.GlassRef, r.W1.ToString(), r.H1.ToString(), r.W2.ToString(), r.H2.ToString(), r.TotalQty.ToString(), r.DeliveredQty.ToString(), r.BalanceQty.ToString(), r.DeliveryStatus };
        public static object[] DnMapExcel(DeliveryReportRow r) => new object[] { r.SerialNumber, r.ProformaId, r.JobOrderId, r.Specification, r.CompanyName, r.CustomerName, r.ProjectName, r.Salesman, r.GlassRef, r.W1, r.H1, r.W2, r.H2, r.TotalQty, r.DeliveredQty, r.BalanceQty, r.DeliveryStatus };

        public static string[] TiHeaders => new[] { "Invoice #", "PI #", "JO #", "DN Ref", "Customer", "Company", "Status", "Type", "Issue Date", "Due Date", "Grand Total", "VAT", "Qty", "SQM" };
        public static string[] TiMap(TaxInvoiceReportRow r) => new[] { r.InvoiceNumber, r.ProformaId, r.JobOrderId, r.DeliveryNoteReference, r.CustomerName, r.CompanyName, r.Status, r.InvoiceType, r.IssueDate.ToString("dd MMM yyyy"), r.DueDate.ToString("dd MMM yyyy"), r.GrandTotal.ToString("N2"), r.VatAmount.ToString("N2"), r.TotalQty.ToString(), r.TotalSqm.ToString("N3") };
        public static object[] TiMapExcel(TaxInvoiceReportRow r) => new object[] { r.InvoiceNumber, r.ProformaId, r.JobOrderId, r.DeliveryNoteReference, r.CustomerName, r.CompanyName, r.Status, r.InvoiceType, r.IssueDate.ToString("dd MMM yyyy"), r.DueDate.ToString("dd MMM yyyy"), r.GrandTotal, r.VatAmount, r.TotalQty, r.TotalSqm };

        public static string[] GlassHeaders => new[] { "Row ID", "PI #", "JO #", "DN #", "TI #", "Glass Ref", "Spec", "Type", "W1", "H1", "W2", "H2", "Qty", "SQM", "Rate", "Amount", "Delivered", "Balance", "Customer", "Project" };
        public static string[] GlassMap(GlassSpecReportRow r) => new[] { r.RowId, r.ProformaId, r.JobOrderId, r.DeliveryNoteId, r.TaxInvoiceNumber, r.GlassRef, r.SpecName, r.GlassType, r.W1.ToString(), r.H1.ToString(), r.W2.ToString(), r.H2.ToString(), r.Qty.ToString(), r.TotalSqm.ToString("N3"), r.Rate.ToString("N2"), r.Amount.ToString("N2"), r.DeliveredQty.ToString(), r.BalanceQty.ToString(), r.CustomerName, r.ProjectName };
        public static object[] GlassMapExcel(GlassSpecReportRow r) => new object[] { r.RowId, r.ProformaId, r.JobOrderId, r.DeliveryNoteId, r.TaxInvoiceNumber, r.GlassRef, r.SpecName, r.GlassType, r.W1, r.H1, r.W2, r.H2, r.Qty, r.TotalSqm, r.Rate, r.Amount, r.DeliveredQty, r.BalanceQty, r.CustomerName, r.ProjectName };

        public static string[] CustomerHeaders => new[] { "Customer", "Salesman", "PIs", "JOs", "DNs", "TIs", "PI Value", "Invoiced", "Outstanding", "Qty", "SQM" };
        public static string[] CustomerMap(CustomerReportRow r) => new[] { r.CustomerName, r.Salesman, r.PiCount.ToString(), r.JoCount.ToString(), r.DnCount.ToString(), r.TiCount.ToString(), r.PiValue.ToString("N2"), r.InvoicedValue.ToString("N2"), r.Outstanding.ToString("N2"), r.TotalQty.ToString(), r.TotalSqm.ToString("N3") };
        public static object[] CustomerMapExcel(CustomerReportRow r) => new object[] { r.CustomerName, r.Salesman, r.PiCount, r.JoCount, r.DnCount, r.TiCount, r.PiValue, r.InvoicedValue, r.Outstanding, r.TotalQty, r.TotalSqm };

        public static string[] CompanyHeaders => new[] { "Company", "TRN", "PIs", "JOs", "DNs", "TIs", "PI Value", "Invoiced", "Statements", "Overdue" };
        public static string[] CompanyMap(CompanyReportRow r) => new[] { r.CompanyName, r.CompanyTrn, r.PiCount.ToString(), r.JobOrderCount.ToString(), r.DeliveryCount.ToString(), r.TaxInvoiceCount.ToString(), r.PiValue.ToString("N2"), r.InvoicedValue.ToString("N2"), r.StatementCount.ToString(), r.OverdueInvoices.ToString() };
        public static object[] CompanyMapExcel(CompanyReportRow r) => new object[] { r.CompanyName, r.CompanyTrn, r.PiCount, r.JobOrderCount, r.DeliveryCount, r.TaxInvoiceCount, r.PiValue, r.InvoicedValue, r.StatementCount, r.OverdueInvoices };

        public static string[] SalesmanHeaders => new[] { "Salesman", "PIs", "Confirmed", "Confirmed Value", "JOs", "Deliveries", "TIs", "Customers", "Projects", "Revenue", "Profit", "Margin%" };
        public static string[] SalesmanMap(SalesmanReportRow r) => new[] { r.SalesmanName, r.PiCount.ToString(), r.ConfirmedPiCount.ToString(), r.ConfirmedValue.ToString("N2"), r.JobOrderCount.ToString(), r.DeliveryCount.ToString(), r.TaxInvoiceCount.ToString(), r.CustomerCount.ToString(), r.ProjectCount.ToString(), r.Revenue.ToString("N2"), r.Profit.ToString("N2"), r.Margin.ToString("N1") };
        public static object[] SalesmanMapExcel(SalesmanReportRow r) => new object[] { r.SalesmanName, r.PiCount, r.ConfirmedPiCount, r.ConfirmedValue, r.JobOrderCount, r.DeliveryCount, r.TaxInvoiceCount, r.CustomerCount, r.ProjectCount, r.Revenue, r.Profit, r.Margin };

        public static string[] ProjectHeaders => new[] { "Project", "Contract #", "Customer", "Company", "Salesman", "PIs", "JOs", "DNs", "TIs", "PI Value", "Invoiced", "Qty", "SQM" };
        public static string[] ProjectMap(ProjectReportRow r) => new[] { r.ProjectName, r.ProjectContractNo, r.CustomerName, r.CompanyName, r.Salesman, r.PiCount.ToString(), r.JoCount.ToString(), r.DnCount.ToString(), r.TiCount.ToString(), r.PiValue.ToString("N2"), r.InvoicedValue.ToString("N2"), r.TotalQty.ToString(), r.TotalSqm.ToString("N3") };
        public static object[] ProjectMapExcel(ProjectReportRow r) => new object[] { r.ProjectName, r.ProjectContractNo, r.CustomerName, r.CompanyName, r.Salesman, r.PiCount, r.JoCount, r.DnCount, r.TiCount, r.PiValue, r.InvoicedValue, r.TotalQty, r.TotalSqm };

        public static string[] StatementHeaders => new[] { "Statement #", "Customer", "Company", "Project", "Invoices", "Invoiced", "Outstanding", "Overdue", "Date" };
        public static string[] StatementMap(StatementReportRow r) => new[] { r.StatementNumber, r.CustomerName, r.CompanyName, r.ProjectName, r.InvoiceCount.ToString(), r.TotalInvoiced.ToString("N2"), r.TotalOutstanding.ToString("N2"), r.OverdueAmount.ToString("N2"), r.CreatedAt.ToString("dd MMM yyyy") };
        public static object[] StatementMapExcel(StatementReportRow r) => new object[] { r.StatementNumber, r.CustomerName, r.CompanyName, r.ProjectName, r.InvoiceCount, r.TotalInvoiced, r.TotalOutstanding, r.OverdueAmount, r.CreatedAt.ToString("dd MMM yyyy") };

        public static string[] ProfitHeaders => new[] { "Document", "Category", "Revenue", "Cost", "Glass Cost", "Prod Cost", "Profit", "Margin%", "Qty", "SQM", "Has Cost?" };
        public static string[] ProfitMap(ProfitabilityRow r) => new[] { r.Name, r.Category, r.Revenue.ToString("N2"), r.Cost.ToString("N2"), r.GlassCost.ToString("N2"), r.ProductionCost.ToString("N2"), r.Profit.ToString("N2"), r.Margin.ToString("N1"), r.Qty.ToString(), r.Sqm.ToString("N3"), r.HasCostData ? "Yes" : "No" };
        public static object[] ProfitMapExcel(ProfitabilityRow r) => new object[] { r.Name, r.Category, r.Revenue, r.Cost, r.GlassCost, r.ProductionCost, r.Profit, r.Margin, r.Qty, r.Sqm, r.HasCostData ? "Yes" : "No" };
    }
}
