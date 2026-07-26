using ClosedXML.Excel;
using ProGlassAutomation.Models.TaxInvoice;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.TaxInvoice.Mapper;
using ProGlassAutomation.Utilities;
using System;
using System.Linq;

namespace ProGlassAutomation.Services.TaxInvoice.Export
{
    /// <summary>
    /// Excel layout export for Tax Invoices.
    /// Implements IExcelLayout to reuse the existing premium Excel export engine.
    /// </summary>
    public class TaxInvoiceExcelService : IExcelLayout
    {
        private readonly Models.TaxInvoice.TaxInvoice _entity;
        private readonly TaxInvoiceSnapshot _snapshot;

        public TaxInvoiceExcelService(Models.TaxInvoice.TaxInvoice entity)
        {
            _entity = entity ?? throw new ArgumentNullException(nameof(entity));
            _snapshot = TaxInvoiceMapper.DeserializeSnapshot(entity);
        }

        // ── IDocumentLayout ──────────────────────────────────────────────
        public string DocumentPrefix => "INV";
        public string DocumentId => _entity.InvoiceNumber;
        public string DocumentTitle => "TAX INVOICE";

        public string CompanyName => _entity.CustomerName;
        public string Reference => _entity.ProjectName;
        public DateTime DocumentDate => _entity.InvoiceDate;

        // ── IExcelLayout ─────────────────────────────────────────────────
        public void RenderWorkbook(IXLWorkbook workbook)
        {
            // ── Sheet 1: Summary ──────────────────────────────────────────
            var wsSummary = workbook.Worksheets.Add("Summary");
            RenderSummarySheet(wsSummary);

            // ── Sheet 2: Items ────────────────────────────────────────────
            var wsItems = workbook.Worksheets.Add("Line Items");
            RenderItemsSheet(wsItems);
        }

        private void RenderSummarySheet(IXLWorksheet ws)
        {
            ws.Cell("A1").Value = "TAX INVOICE SUMMARY";
            ws.Cell("A1").Style.Font.SetBold();
            ws.Cell("A1").Style.Font.SetFontSize(16);
            ws.Cell("A1").Style.Font.SetFontColor(XLColor.FromHtml("#4338CA"));

            ws.Cell("A3").Value = "Invoice Number:";
            ws.Cell("B3").Value = _entity.InvoiceNumber;
            ws.Cell("B3").Style.Font.SetBold();

            ws.Cell("A4").Value = "Invoice Date:";
            ws.Cell("B4").Value = _entity.InvoiceDate.ToString("dd MMM yyyy");

            ws.Cell("A5").Value = "Customer Name:";
            ws.Cell("B5").Value = _entity.CustomerName;

            ws.Cell("A6").Value = "Project Name:";
            ws.Cell("B6").Value = _entity.ProjectName;

            ws.Cell("A7").Value = "Customer TRN:";
            ws.Cell("B7").Value = string.IsNullOrWhiteSpace(_entity.CustomerTrn) ? "—" : _entity.CustomerTrn;

            ws.Cell("A8").Value = "Company TRN (UAE):";
            ws.Cell("B8").Value = string.IsNullOrWhiteSpace(_entity.CompanyTrn) ? "—" : _entity.CompanyTrn;

            ws.Cell("A9").Value = "Source DN:";
            ws.Cell("B9").Value = _entity.DeliveryNoteId;

            ws.Cell("A10").Value = "Source JO:";
            ws.Cell("B10").Value = _entity.JobOrderId;

            ws.Cell("A11").Value = "Payment Status:";
            ws.Cell("B11").Value = _entity.PaymentStatus;
            ws.Cell("B11").Style.Font.SetBold();

            // Financial Summary Block
            ws.Cell("D3").Value = "FINANCIAL SUMMARY";
            ws.Cell("D3").Style.Font.SetBold();
            ws.Cell("D3").Style.Font.SetFontColor(XLColor.FromHtml("#4338CA"));

            ws.Cell("D5").Value = "Subtotal:";
            ws.Cell("E5").Value = _entity.Subtotal;
            ws.Cell("E5").Style.NumberFormat.Format = "$#,##0.00";

            ws.Cell("D6").Value = "Discount:";
            ws.Cell("E6").Value = _entity.Discount;
            ws.Cell("E6").Style.NumberFormat.Format = "$#,##0.00";

            ws.Cell("D7").Value = "Taxable Amount:";
            ws.Cell("E7").Value = _entity.TaxableAmount;
            ws.Cell("E7").Style.NumberFormat.Format = "$#,##0.00";

            ws.Cell("D8").Value = "VAT Amount (5%):";
            ws.Cell("E8").Value = _entity.VatAmount;
            ws.Cell("E8").Style.NumberFormat.Format = "$#,##0.00";

            ws.Cell("D10").Value = "GRAND TOTAL:";
            ws.Cell("D10").Style.Font.SetBold();
            ws.Cell("E10").Value = _entity.GrandTotal;
            ws.Cell("E10").Style.Font.SetBold();
            ws.Cell("E10").Style.NumberFormat.Format = "$#,##0.00";
            ws.Cell("E10").Style.Font.SetFontColor(XLColor.FromHtml("#047857"));

            ws.Cell("D11").Value = "Currency:";
            ws.Cell("E11").Value = _entity.Currency;
            ws.Cell("E11").Style.Font.SetBold();

            // Formatting columns
            ws.Column(1).Width = 20;
            ws.Column(2).Width = 30;
            ws.Column(4).Width = 20;
            ws.Column(5).Width = 20;
        }

        private void RenderItemsSheet(IXLWorksheet ws)
        {
            // Set Headers
            string[] headers = {
                "Spec Group Number", "Spec Group Name", "Serial Number", "Glass Ref",
                "Width 1", "Height 1", "Width 2", "Height 2", "Delivered Qty", "Invoiced Qty",
                "Sqm (Single)", "Total Sqm", $"Rate ({_entity.Currency})", $"Amount ({_entity.Currency})", "Notes"
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
            foreach (var item in _snapshot.Items.OrderBy(i => i.SpecGroupNumber).ThenBy(i => i.SerialNumber))
            {
                decimal sqm = SqmCalculator.CalculatePanel(item.W1, item.H1);
                decimal totalSqm = Math.Round(sqm * item.InvoicedQty, 3);
                decimal displayRate = Math.Round(item.Rate / _entity.ExchangeRate, 2);
                decimal displayAmount = Math.Round(sqm * item.InvoicedQty * displayRate, 2);

                ws.Cell(rowIdx, 1).Value = item.SpecGroupNumber;
                ws.Cell(rowIdx, 2).Value = item.SpecGroupName;
                ws.Cell(rowIdx, 3).Value = item.SerialNumber;
                ws.Cell(rowIdx, 4).Value = item.GlassRef;
                ws.Cell(rowIdx, 5).Value = item.W1;
                ws.Cell(rowIdx, 6).Value = item.H1;
                ws.Cell(rowIdx, 7).Value = item.W2 > 0 ? item.W2.ToString() : "—";
                ws.Cell(rowIdx, 8).Value = item.H2 > 0 ? item.H2.ToString() : "—";
                ws.Cell(rowIdx, 9).Value = item.DeliveredQty;
                ws.Cell(rowIdx, 10).Value = item.InvoicedQty;
                ws.Cell(rowIdx, 11).Value = sqm;
                ws.Cell(rowIdx, 12).Value = totalSqm;
                ws.Cell(rowIdx, 13).Value = displayRate;
                ws.Cell(rowIdx, 14).Value = displayAmount;
                ws.Cell(rowIdx, 15).Value = item.Notes;

                // Numbers formatting
                ws.Cell(rowIdx, 11).Style.NumberFormat.Format = "0.000";
                ws.Cell(rowIdx, 12).Style.NumberFormat.Format = "0.000";
                ws.Cell(rowIdx, 13).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(rowIdx, 14).Style.NumberFormat.Format = "#,##0.00";

                rowIdx++;
            }

            // Auto fit column widths
            ws.Columns().AdjustToContents();
        }
    }
}