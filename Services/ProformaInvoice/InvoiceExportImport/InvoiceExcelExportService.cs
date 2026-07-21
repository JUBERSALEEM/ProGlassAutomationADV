using ClosedXML.Excel;
using ProGlassAutomation.Services.ProformaInvoice.PdfExport;
using System.Linq;

namespace ProGlassAutomation.Services.ProformaInvoice.InvoiceExportImport
{
    public class InvoiceExcelExportService : IInvoiceExportService
    {
        public bool ExportToExcel(PdfExportContext ctx, string outputFilePath)
        {
            using var wb = new XLWorkbook();

            // Sheet 1: Invoice Summary
            var wsSummary = wb.AddWorksheet("Invoice Summary");
            BuildSummarySheet(wsSummary, ctx);

            // Sheet 2: Glass Panels (all spec groups combined)
            var wsGlass = wb.AddWorksheet("Glass Panels");
            BuildGlassSheet(wsGlass, ctx);

            // Sheet 3: Other Charges
            if (ctx.OtherCharges.Any())
            {
                var wsCharges = wb.AddWorksheet("Other Charges");
                BuildChargesSheet(wsCharges, ctx);
            }

            // Sheet 4: Terms & Conditions
            if (ctx.TermClauses.Any())
            {
                var wsTerms = wb.AddWorksheet("Terms & Conditions");
                BuildTermsSheet(wsTerms, ctx);
            }

            wb.SaveAs(outputFilePath);
            return true;
        }

        public bool ExportToCsv(PdfExportContext context, string outputFilePath)
        {
            // CSV uses the dedicated CsvExportService
            return false;
        }

        // ══════════════════════════════════════════════════════════════════
        //  SHEET 1: Invoice Summary
        // ══════════════════════════════════════════════════════════════════

        private void BuildSummarySheet(IXLWorksheet ws, PdfExportContext ctx)
        {
            // Header
            ws.Cell("A1").Value = "PROFORMA INVOICE";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 16;
            ws.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#4F46E5");
            ws.Range("A1:D1").Merge();

            int row = 3;

            // Company
            AddSection(ws, ref row, "COMPANY DETAILS");
            AddField(ws, ref row, "Company Name", ctx.CompanyName);
            AddField(ws, ref row, "Company TRN", ctx.CompanyTrn);
            AddField(ws, ref row, "Location", ctx.CompanyLocation);
            AddField(ws, ref row, "Proforma ID", ctx.ProformaId);
            AddField(ws, ref row, "Issue Date", ctx.IssueDate);
            AddField(ws, ref row, "Valid Until", ctx.ValidUntil);
            AddField(ws, ref row, "Status", ctx.ProformaStatus);
            row++;

            // Client
            AddSection(ws, ref row, "CLIENT DETAILS");
            AddField(ws, ref row, "Customer Name", ctx.CustomerName);
            AddField(ws, ref row, "Customer TRN", ctx.CustomerTrn);
            AddField(ws, ref row, "Attention Name", ctx.AttentionName);
            AddField(ws, ref row, "Contact Phone", ctx.ContactPhone);
            AddField(ws, ref row, "Billing Address", ctx.BillingAddress);
            AddField(ws, ref row, "Customer Reference", ctx.CustomerReference);
            AddField(ws, ref row, "Sales Representative", ctx.SalesRepresentative);
            row++;

            // Project
            AddSection(ws, ref row, "PROJECT DETAILS");
            AddField(ws, ref row, "Project Name", ctx.ProjectName);
            AddField(ws, ref row, "Contract No", ctx.ProjectContractNo);
            AddField(ws, ref row, "Site Delivery", ctx.SiteDelivery);
            AddField(ws, ref row, "LPO Number", ctx.LpoNumber);
            row++;

            // Financial
            AddSection(ws, ref row, "FINANCIAL SUMMARY");
            AddField(ws, ref row, "Glass Materials Total", $"AED {ctx.GlassMaterialsTotal:N2}");
            AddField(ws, ref row, "Other Charges Total", $"AED {ctx.OtherChargesTotal:N2}");
            AddField(ws, ref row, "Net Subtotal", $"AED {ctx.NetSubtotal:N2}");
            if (ctx.DiscountAmount > 0)
                AddField(ws, ref row, "Discount", $"- AED {ctx.DiscountAmount:N2}");
            if (ctx.IsVatApplicable)
                AddField(ws, ref row, $"VAT ({ctx.VatPercent:0.##}%)", $"AED {ctx.VatAmount:N2}");
            AddField(ws, ref row, "GRAND TOTAL (VAT INC)", $"AED {ctx.GrandTotal:N2}");

            // Prepared By
            row++;
            AddSection(ws, ref row, "PREPARED BY");
            AddField(ws, ref row, "Name", ctx.PreparedByName);
            AddField(ws, ref row, "Department", ctx.PreparedByDept);
            AddField(ws, ref row, "Status", ctx.PreparedByStatus);

            // Auto-fit columns
            ws.Column("A").Width = 25;
            ws.Column("B").Width = 40;
        }

        // ══════════════════════════════════════════════════════════════════
        //  SHEET 2: Glass Panels
        //  Phase 7.19d — 12 columns:
        //  Spec # | Spec Name | # | Glass Ref | W1 | H1 | W2 | H2 | Qty | Area | Rate | Total
        // ══════════════════════════════════════════════════════════════════

        private void BuildGlassSheet(IXLWorksheet ws, PdfExportContext ctx)
        {
            // Headers
            var headers = new[]
            {
                "Spec #", "Spec Name", "#", "Glass Ref",
                "W1", "H1", "W2", "H2",
                "Qty", "Area (sqm)", "Rate (AED)", "Total (AED)"
            };
            int totalCols = headers.Length; // 12

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int row = 2;
            foreach (var group in ctx.SpecGroups)
            {
                foreach (var item in group.Rows)
                {
                    ws.Cell(row, 1).Value = group.GroupNumber;
                    ws.Cell(row, 2).Value = group.GroupName;
                    ws.Cell(row, 3).Value = item.Serial;
                    ws.Cell(row, 4).Value = item.GlassRef;
                    ws.Cell(row, 5).Value = item.W1;
                    ws.Cell(row, 6).Value = item.H1;
                    ws.Cell(row, 7).Value = item.W2;
                    ws.Cell(row, 8).Value = item.H2;
                    ws.Cell(row, 9).Value = item.Qty;
                    ws.Cell(row, 10).Value = item.Area;
                    ws.Cell(row, 11).Value = item.Rate;
                    ws.Cell(row, 12).Value = item.Total;

                    // Number formatting
                    ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.000";
                    ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 12).Style.NumberFormat.Format = "#,##0.00";

                    // Alternate row coloring
                    if (row % 2 == 0)
                    {
                        for (int c = 1; c <= totalCols; c++)
                            ws.Cell(row, c).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
                    }

                    row++;
                }

                // Subtotal row per spec group
                ws.Cell(row, 1).Value = "";
                ws.Cell(row, 2).Value = $"SPEC {group.GroupNumber} SUBTOTAL";
                ws.Cell(row, 2).Style.Font.Bold = true;
                ws.Cell(row, 2).Style.Font.FontColor = XLColor.FromHtml("#4F46E5");
                ws.Cell(row, 9).Value = group.TotalQty;
                ws.Cell(row, 9).Style.Font.Bold = true;
                ws.Cell(row, 10).Value = group.TotalSqm;
                ws.Cell(row, 10).Style.Font.Bold = true;
                ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(row, 12).Value = group.TotalValue;
                ws.Cell(row, 12).Style.Font.Bold = true;
                ws.Cell(row, 12).Style.Font.FontColor = XLColor.FromHtml("#047857");
                ws.Cell(row, 12).Style.NumberFormat.Format = "#,##0.00";

                for (int c = 1; c <= totalCols; c++)
                    ws.Cell(row, c).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

                row++;
            }

            // Grand total row
            ws.Cell(row, 2).Value = "GRAND TOTAL";
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 2).Style.Font.FontSize = 12;
            ws.Cell(row, 9).Value = ctx.SpecGroups.Sum(g => g.TotalQty);
            ws.Cell(row, 9).Style.Font.Bold = true;
            ws.Cell(row, 10).Value = ctx.SpecGroups.Sum(g => g.TotalSqm);
            ws.Cell(row, 10).Style.Font.Bold = true;
            ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(row, 12).Value = ctx.SpecGroups.Sum(g => g.TotalValue);
            ws.Cell(row, 12).Style.Font.Bold = true;
            ws.Cell(row, 12).Style.Font.FontSize = 12;
            ws.Cell(row, 12).Style.Font.FontColor = XLColor.FromHtml("#047857");
            ws.Cell(row, 12).Style.NumberFormat.Format = "#,##0.00";

            for (int c = 1; c <= totalCols; c++)
                ws.Cell(row, c).Style.Fill.BackgroundColor = XLColor.FromHtml("#4F46E5");

            ws.Cell(row, 2).Style.Font.FontColor = XLColor.White;
            ws.Cell(row, 9).Style.Font.FontColor = XLColor.White;
            ws.Cell(row, 10).Style.Font.FontColor = XLColor.White;
            ws.Cell(row, 12).Style.Font.FontColor = XLColor.White;

            // Auto-fit
            ws.Columns().AdjustToContents();
        }

        // ══════════════════════════════════════════════════════════════════
        //  SHEET 3: Other Charges
        // ══════════════════════════════════════════════════════════════════

        private void BuildChargesSheet(IXLWorksheet ws, PdfExportContext ctx)
        {
            var headers = new[] { "Charge Name", "Quantity", "Rate (AED)", "Amount (AED)" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F46E5");
            }

            int row = 2;
            foreach (var charge in ctx.OtherCharges)
            {
                ws.Cell(row, 1).Value = charge.Name;
                ws.Cell(row, 2).Value = charge.QtyLabel;
                ws.Cell(row, 3).Value = charge.RateLabel;
                ws.Cell(row, 4).Value = charge.Amount;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";

                if (row % 2 == 0)
                    for (int c = 1; c <= 4; c++)
                        ws.Cell(row, c).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");

                row++;
            }

            // Total
            ws.Cell(row, 1).Value = "TOTAL OTHER CHARGES";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 4).Value = ctx.OtherChargesTotal;
            ws.Cell(row, 4).Style.Font.Bold = true;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 4).Style.Font.FontColor = XLColor.FromHtml("#047857");

            ws.Columns().AdjustToContents();
        }

        // ══════════════════════════════════════════════════════════════════
        //  SHEET 4: Terms & Conditions
        // ══════════════════════════════════════════════════════════════════

        private void BuildTermsSheet(IXLWorksheet ws, PdfExportContext ctx)
        {
            ws.Cell("A1").Value = "#";
            ws.Cell("B1").Value = "Title";
            ws.Cell("C1").Value = "Body";
            ws.Range("A1:C1").Style.Font.Bold = true;
            ws.Range("A1:C1").Style.Fill.BackgroundColor = XLColor.FromHtml("#4F46E5");
            ws.Range("A1:C1").Style.Font.FontColor = XLColor.White;

            int row = 2;
            foreach (var clause in ctx.TermClauses)
            {
                ws.Cell(row, 1).Value = clause.Order;
                ws.Cell(row, 2).Value = clause.Title;

                string body = clause.Body ?? "";
                if (clause.SubLines != null && clause.SubLines.Count > 0)
                    body += "\n" + string.Join("\n", clause.SubLines.Select(l => $"• {l}"));

                ws.Cell(row, 3).Value = body;
                ws.Cell(row, 3).Style.Alignment.WrapText = true;
                row++;
            }

            ws.Column("A").Width = 5;
            ws.Column("B").Width = 25;
            ws.Column("C").Width = 60;
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static void AddSection(IXLWorksheet ws, ref int row, string title)
        {
            ws.Cell(row, 1).Value = title;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 11;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#4F46E5");
            ws.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
            row++;
        }

        private static void AddField(IXLWorksheet ws, ref int row, string label, string value)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#475569");
            ws.Cell(row, 2).Value = string.IsNullOrWhiteSpace(value) ? "—" : value;
            ws.Cell(row, 2).Style.Font.Bold = true;
            row++;
        }
    }
}