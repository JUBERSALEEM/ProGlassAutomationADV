using ProGlassAutomation.Models.Reports.Sales;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.Export.Pdf.Renderers;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Reports.Export
{
    public class SalesReportPdfService : IPdfLayout
    {
        private readonly List<SalesReport> _records;
        private readonly SalesSummary _summary;
        private readonly SalesFilterModel? _filter;
        private readonly string _statementTitle;

        public SalesReportPdfService(List<SalesReport> records, SalesSummary summary, SalesFilterModel? filter = null, string statementTitle = "SALES STATEMENT OF ACCOUNT")
        {
            _records = records ?? throw new ArgumentNullException(nameof(records));
            _summary = summary ?? throw new ArgumentNullException(nameof(summary));
            _filter = filter;
            _statementTitle = statementTitle;
        }

        public string DocumentPrefix => "STMT";
        public string DocumentId => DateTime.Now.ToString("yyyyMMdd-HHmm");
        public string DocumentTitle => _statementTitle;

        public string CompanyName => _filter?.CustomerName ?? "All Customers";
        public string Reference => _filter?.ProjectName ?? "All Projects";
        public DateTime DocumentDate => DateTime.Today;

        public PageLayout PageLayout => PageLayout.A4Portrait;

        public LetterheadInfo? Letterhead => new LetterheadInfo
        {
            CompanyName = "ProGlass Automation ERP",
            CompanyLocation = "Dubai, UAE",
            CompanyTrn = "100234567800003", // Example Company TRN
            DocumentTitle = DocumentTitle,
            DocumentId = DocumentId,
            SubtitleLine = $"Statement Date: {DateTime.Today:dd MMM yyyy}  ·  Net Sales: AED {_summary.TotalNetSalesAed:N2}"
        };

        public void RenderContent(ColumnDescriptor content)
        {
            // 1. Render Filters/Criteria Block
            content.Item().Element(c => SectionHeaderRenderer.Render(c, "STATEMENT CRITERIA"));
            content.Item().Element(RenderCriteriaBlock);

            // 2. Render Financial Summary KPIs
            content.Item().Element(c => SectionHeaderRenderer.Render(c, "FINANCIAL METRICS SUMMARY"));
            content.Item().Element(RenderFinancialSummaryBlock);

            // 3. Render Invoices Table
            content.Item().Element(c => SectionHeaderRenderer.Render(c, "TRANSACTION INVOICE HISTORY"));
            content.Item().Element(RenderTransactionsTable);

            // 4. Render Sign-Off
            content.Item().Element(c => SectionHeaderRenderer.Render(c, "DECLARATION & SIGNATURE"));
            content.Item().Element(RenderSignOffBlock);
        }

        private void RenderCriteriaBlock(IContainer container)
        {
            container.Border(0.5f).BorderColor(DocumentStyle.ColorBorder).Padding(8).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Customer: {(_filter?.CustomerName ?? "All Customers")}")
                            .FontSize(DocumentStyle.FontSizeSmall).SemiBold();
                        c.Item().Text($"Project: {(_filter?.ProjectName ?? "All Projects")}")
                            .FontSize(DocumentStyle.FontSizeSmall).FontColor(DocumentStyle.ColorMuted);
                        c.Item().Text($"Salesman: {(_filter?.Salesman ?? "All Salesmen")}")
                            .FontSize(DocumentStyle.FontSizeSmall).FontColor(DocumentStyle.ColorMuted);
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Company: {(_filter?.CompanyName ?? "All Companies")}")
                            .FontSize(DocumentStyle.FontSizeSmall).SemiBold();
                        c.Item().Text($"Date Range: {(_filter?.StartDate?.ToString("dd MMM yyyy") ?? "Beginning")} to {(_filter?.EndDate?.ToString("dd MMM yyyy") ?? "Today")}")
                            .FontSize(DocumentStyle.FontSizeSmall).FontColor(DocumentStyle.ColorMuted);
                        c.Item().Text($"Payment Status: {(_filter?.PaymentStatus ?? "All Statuses")}")
                            .FontSize(DocumentStyle.FontSizeSmall).FontColor(DocumentStyle.ColorMuted);
                    });
                });
            });
        }

        private void RenderFinancialSummaryBlock(IContainer container)
        {
            container.Row(row =>
            {
                KPIBlock(row, "TOTAL INVOICED", $"AED {_summary.TotalNetSalesAed:N2}", DocumentStyle.ColorAccent);
                row.ConstantItem(10);
                KPIBlock(row, "TOTAL TAX VAT", $"AED {_summary.TotalVatAed:N2}", DocumentStyle.ColorMuted);
                row.ConstantItem(10);
                KPIBlock(row, "TOTAL PAYMENTS", $"AED {_summary.TotalPaidAmountAed:N2}", "#10B981");
                row.ConstantItem(10);
                KPIBlock(row, "TOTAL OUTSTANDING", $"AED {_summary.TotalOutstandingAmountAed:N2}", DocumentStyle.ColorDanger);
            });
        }

        private void KPIBlock(RowDescriptor row, string label, string value, string colorHex)
        {
            row.RelativeItem()
                .Background(DocumentStyle.ColorTableAlt)
                .Border(0.5f).BorderColor(DocumentStyle.ColorBorder)
                .Padding(6)
                .Column(c =>
                {
                    c.Item().AlignCenter().Text(label)
                        .FontSize(7)
                        .SemiBold()
                        .FontColor(DocumentStyle.ColorMuted);

                    c.Item().PaddingTop(2).AlignCenter().Text(value)
                        .FontSize(DocumentStyle.FontSizeBody)
                        .Bold()
                        .FontColor(colorHex);
                });
        }

        private void RenderTransactionsTable(IContainer container)
        {
            if (_records.Count == 0)
            {
                container.Text("No transactions match the specified reporting criteria.").Italic().FontColor(DocumentStyle.ColorMuted);
                return;
            }

            container.Table(t =>
            {
                t.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(24);   // #
                    cols.ConstantColumn(58);   // Date
                    cols.RelativeColumn(1.2f); // Invoice No
                    cols.RelativeColumn(1.8f); // Customer
                    cols.RelativeColumn(1.8f); // Project
                    cols.ConstantColumn(68);   // Total (AED)
                    cols.ConstantColumn(58);   // Status
                });

                t.Header(h =>
                {
                    HeaderCell(h.Cell(), "#");
                    HeaderCell(h.Cell(), "DATE");
                    HeaderCell(h.Cell(), "INVOICE NO");
                    HeaderCell(h.Cell(), "CUSTOMER");
                    HeaderCell(h.Cell(), "PROJECT");
                    HeaderCell(h.Cell(), "NET SALES");
                    HeaderCell(h.Cell(), "STATUS");
                });

                int idx = 1;
                bool alt = false;
                foreach (var r in _records)
                {
                    string bg = alt ? DocumentStyle.ColorTableAlt : DocumentStyle.ColorWhite;

                    DataCell(t.Cell(), idx.ToString(), bg);
                    DataCell(t.Cell(), r.InvoiceDate.ToString("dd-MM-yy"), bg);
                    DataCell(t.Cell(), r.InvoiceNumber, bg, highlight: true);
                    DataCell(t.Cell(), r.CustomerName, bg, align: false);
                    DataCell(t.Cell(), r.ProjectName, bg, align: false);
                    DataCell(t.Cell(), $"{r.GrandTotalAed:N2}", bg, highlight: true);

                    string statusColor = r.PaymentStatus == "Paid" ? "#10B981" : (r.PaymentStatus == "Partially Paid" ? "#F59E0B" : DocumentStyle.ColorDanger);
                    DataCell(t.Cell(), r.PaymentStatus, bg, highlight: true, highlightColor: statusColor);

                    idx++;
                    alt = !alt;
                }
            });
        }

        private void RenderSignOffBlock(IContainer container)
        {
            container.Row(row =>
            {
                SignatureCell(row, "Prepared & Certified By");
                row.ConstantItem(30);
                SignatureCell(row, "Finance Department Confirmation");
            });
        }

        private static void SignatureCell(RowDescriptor row, string label)
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().PaddingTop(20)
                    .BorderBottom(0.5f)
                    .BorderColor(DocumentStyle.ColorText);
                col.Item().PaddingTop(3).Text(label)
                    .FontSize(DocumentStyle.FontSizeCaption)
                    .FontColor(DocumentStyle.ColorMuted);
                col.Item().Text("Finance Officer Signature & Stamp")
                    .FontSize(DocumentStyle.FontSizeCaption)
                    .FontColor(DocumentStyle.ColorMuted);
            });
        }

        private static void HeaderCell(IContainer cell, string text)
        {
            cell.Background(DocumentStyle.ColorAccent)
                .PaddingHorizontal(4)
                .PaddingVertical(4)
                .AlignCenter()
                .Text(text)
                .FontSize(8)
                .Bold()
                .FontColor(DocumentStyle.ColorWhite);
        }

        private static void DataCell(
            IContainer cell,
            string text,
            string bg,
            bool align = true,
            bool highlight = false,
            string? highlightColor = null)
        {
            var bordered = cell
                .Border(0.5f)
                .BorderColor(DocumentStyle.ColorBorder)
                .Background(bg)
                .PaddingHorizontal(4)
                .PaddingVertical(4);

            var textEl = align ? bordered.AlignCenter().Text(text) : bordered.Text(text);

            textEl.FontSize(8);

            if (highlight)
                textEl.Bold().FontColor(highlightColor ?? DocumentStyle.ColorAccent);
        }
    }
}