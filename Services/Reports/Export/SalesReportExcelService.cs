using ClosedXML.Excel;
using ProGlassAutomation.Models.Reports.Sales;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Excel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Reports.Export
{
    public class SalesReportExcelService : IExcelLayout
    {
        private readonly List<SalesReport> _records;
        private readonly SalesSummary _summary;
        private readonly SalesFilterModel? _filter;
        private readonly string _statementTitle;

        public SalesReportExcelService(List<SalesReport> records, SalesSummary summary, SalesFilterModel? filter = null, string statementTitle = "SALES REPORT SUMMARY")
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

        public void RenderWorkbook(IXLWorkbook workbook)
        {
            // Sheet 1: Dashboard Overview
            var wsDashboard = workbook.Worksheets.Add("Dashboard Summary");
            RenderDashboardSheet(wsDashboard);

            // Sheet 2: Transactions
            var wsTransactions = workbook.Worksheets.Add("Transaction List");
            RenderTransactionsSheet(wsTransactions);
        }

        private void RenderDashboardSheet(IXLWorksheet ws)
        {
            // Title
            ws.Cell("A1").Value = _statementTitle;
            ws.Cell("A1").Style.Font.SetBold();
            ws.Cell("A1").Style.Font.SetFontSize(16);
            ws.Cell("A1").Style.Font.SetFontColor(XLColor.FromHtml("#4338CA"));

            // Criteria
            ws.Cell("A3").Value = "STATEMENT FILTERS";
            ws.Cell("A3").Style.Font.SetBold();
            ws.Cell("A3").Style.Font.SetFontSize(11);
            ws.Cell("A3").Style.Font.SetFontColor(XLColor.FromHtml("#4B5563"));

            ws.Cell("A4").Value = "Customer:";
            ws.Cell("B4").Value = _filter?.CustomerName ?? "All Customers";

            ws.Cell("A5").Value = "Project:";
            ws.Cell("B5").Value = _filter?.ProjectName ?? "All Projects";

            ws.Cell("A6").Value = "Salesman:";
            ws.Cell("B6").Value = _filter?.Salesman ?? "All Salesmen";

            ws.Cell("A7").Value = "Company:";
            ws.Cell("B7").Value = _filter?.CompanyName ?? "All Companies";

            ws.Cell("A8").Value = "Date Range:";
            ws.Cell("B8").Value = $"{(_filter?.StartDate?.ToString("dd MMM yyyy") ?? "Beginning")} to {(_filter?.EndDate?.ToString("dd MMM yyyy") ?? "Today")}";

            ws.Cell("A9").Value = "Payment Status:";
            ws.Cell("B9").Value = _filter?.PaymentStatus ?? "All Statuses";

            // KPI Grid (Right column)
            ws.Cell("D3").Value = "FINANCIAL SUMMARY (AED)";
            ws.Cell("D3").Style.Font.SetBold();
            ws.Cell("D3").Style.Font.SetFontSize(11);
            ws.Cell("D3").Style.Font.SetFontColor(XLColor.FromHtml("#4338CA"));

            ws.Cell("D4").Value = "Gross Sales:";
            ws.Cell("E4").Value = _summary.TotalGrossSalesAed;
            ws.Cell("E4").Style.NumberFormat.Format = "AED #,##0.00";

            ws.Cell("D5").Value = "Discounts:";
            ws.Cell("E5").Value = _summary.TotalDiscountAed;
            ws.Cell("E5").Style.NumberFormat.Format = "AED #,##0.00";

            ws.Cell("D6").Value = "Taxable Sales:";
            ws.Cell("E6").Value = _summary.TotalTaxableSalesAed;
            ws.Cell("E6").Style.NumberFormat.Format = "AED #,##0.00";

            ws.Cell("D7").Value = "Invoiced VAT (5%):";
            ws.Cell("E7").Value = _summary.TotalVatAed;
            ws.Cell("E7").Style.NumberFormat.Format = "AED #,##0.00";

            ws.Cell("D9").Value = "NET SALES (VAT INCL):";
            ws.Cell("D9").Style.Font.SetBold();
            ws.Cell("E9").Value = _summary.TotalNetSalesAed;
            ws.Cell("E9").Style.Font.SetBold();
            ws.Cell("E9").Style.NumberFormat.Format = "AED #,##0.00";
            ws.Cell("E9").Style.Font.SetFontColor(XLColor.FromHtml("#4338CA"));

            ws.Cell("D10").Value = "Total Paid Amount:";
            ws.Cell("E10").Value = _summary.TotalPaidAmountAed;
            ws.Cell("E10").Style.NumberFormat.Format = "AED #,##0.00";
            ws.Cell("E10").Style.Font.SetFontColor(XLColor.FromHtml("#10B981"));

            ws.Cell("D11").Value = "Outstanding Amount:";
            ws.Cell("D11").Style.Font.SetBold();
            ws.Cell("E11").Value = _summary.TotalOutstandingAmountAed;
            ws.Cell("E11").Style.Font.SetBold();
            ws.Cell("E11").Style.NumberFormat.Format = "AED #,##0.00";
            ws.Cell("E11").Style.Font.SetFontColor(XLColor.FromHtml("#EF4444"));

            // Column formatting
            ws.Column(1).Width = 18;
            ws.Column(2).Width = 28;
            ws.Column(4).Width = 24;
            ws.Column(5).Width = 24;
        }

        private void RenderTransactionsSheet(IXLWorksheet ws)
        {
            string[] headers = {
                "Index", "Date", "Invoice No", "Customer", "Project", "Salesman",
                "Subtotal", "Discount", "Taxable Amount", "VAT (5%)", "Grand Total (AED)", "Payment Status"
            };

            for (int col = 0; col < headers.Length; col++)
            {
                var cell = ws.Cell(1, col + 1);
                cell.Value = headers[col];
                cell.Style.Font.SetBold();
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4338CA");
                cell.Style.Font.SetFontColor(XLColor.White);
            }

            int rowIdx = 2;
            foreach (var r in _records)
            {
                ws.Cell(rowIdx, 1).Value = rowIdx - 1;
                ws.Cell(rowIdx, 2).Value = r.InvoiceDate.ToString("yyyy-MM-dd");
                ws.Cell(rowIdx, 3).Value = r.InvoiceNumber;
                ws.Cell(rowIdx, 4).Value = r.CustomerName;
                ws.Cell(rowIdx, 5).Value = r.ProjectName;
                ws.Cell(rowIdx, 6).Value = r.Salesman;
                ws.Cell(rowIdx, 7).Value = r.Subtotal * r.ExchangeRate;
                ws.Cell(rowIdx, 8).Value = r.Discount * r.ExchangeRate;
                ws.Cell(rowIdx, 9).Value = r.TaxableAmountAed;
                ws.Cell(rowIdx, 10).Value = r.VatAmountAed;
                ws.Cell(rowIdx, 11).Value = r.GrandTotalAed;
                ws.Cell(rowIdx, 12).Value = r.PaymentStatus;

                // Numbers formatting
                ws.Cell(rowIdx, 7).Style.NumberFormat.Format = "AED #,##0.00";
                ws.Cell(rowIdx, 8).Style.NumberFormat.Format = "AED #,##0.00";
                ws.Cell(rowIdx, 9).Style.NumberFormat.Format = "AED #,##0.00";
                ws.Cell(rowIdx, 10).Style.NumberFormat.Format = "AED #,##0.00";
                ws.Cell(rowIdx, 11).Style.NumberFormat.Format = "AED #,##0.00";

                rowIdx++;
            }

            ws.Columns().AdjustToContents();
        }
    }
}