using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Linq;

namespace ProGlassAutomation.Services.ProformaInvoice.PdfExport
{
    public class ProformaInvoicePdfService : IProformaInvoicePdfService
    {
        // Brand colors
        private const string ACCENT = "#4F46E5";
        private const string DARK = "#0F172A";
        private const string MUTED = "#475569";
        private const string LINE = "#CBD5E1";
        private const string LIGHT_BG = "#F1F5F9";
        private const string TABLE_HEADER = "#1E293B";
        private const string GREEN = "#047857";
        private const string RED = "#B91C1C";

        public bool GeneratePdf(PdfExportContext ctx, string outputFilePath)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(40);
                    page.MarginVertical(30);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Segoe UI").FontColor(DARK));

                    page.Header().Element(c => ComposeHeader(c, ctx));
                    page.Content().Element(c => ComposeContent(c, ctx));
                    page.Footer().Element(c => ComposePageFooter(c, ctx));
                });
            });

            document.GeneratePdf(outputFilePath);
            return true;
        }

        private void ComposeHeader(IContainer container, PdfExportContext ctx)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text(string.IsNullOrWhiteSpace(ctx.CompanyName) ? "PROGLASS AUTOMATION ERP" : ctx.CompanyName)
                            .FontSize(17).Bold().FontColor(ACCENT);

                        left.Item().Text("Premium Glass Glazing, High-Yield Manufacturing & Logistics Spec")
                            .FontSize(9).FontColor(MUTED);

                        left.Item().Text($"{(string.IsNullOrWhiteSpace(ctx.CompanyLocation) ? "United Arab Emirates" : ctx.CompanyLocation)} • www.proglass-automation.com")
                            .FontSize(9).FontColor(MUTED);

                        if (!string.IsNullOrWhiteSpace(ctx.CompanyTrn))
                            left.Item().Text($"TRN: {ctx.CompanyTrn}").FontSize(9).SemiBold().FontColor(DARK);
                    });

                    row.ConstantItem(200).Border(1).BorderColor(LINE).Background(LIGHT_BG)
                        .Padding(10).Column(badge =>
                        {
                            badge.Item().AlignCenter().Text("PROFORMA INVOICE").FontSize(12).Bold().FontColor(DARK);
                            badge.Item().PaddingTop(6).Text($"Ref: {ctx.ProformaId}").FontSize(9).SemiBold();
                            badge.Item().Text($"Date: {ctx.IssueDate}").FontSize(9).SemiBold();
                        });
                });

                col.Item().PaddingTop(8).LineHorizontal(1).LineColor(LINE);
            });
        }

        private void ComposeContent(IContainer container, PdfExportContext ctx)
        {
            container.Column(col =>
            {
                col.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Element(c => ComposeClientBlock(c, ctx));
                    row.ConstantItem(16);
                    row.RelativeItem().Element(c => ComposeProjectBlock(c, ctx));
                });

                col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(LINE);

                foreach (var group in ctx.SpecGroups)
                    col.Item().Element(c => ComposeSpecTable(c, group));

                col.Item().Element(c => ComposeOtherCharges(c, ctx));
                col.Item().Element(c => ComposeFinancialSummary(c, ctx));

                if (ctx.TermClauses.Any())
                    col.Item().Element(c => ComposeTermsAndConditions(c, ctx));

                col.Item().PaddingTop(10).BorderTop(1).BorderColor(LINE).PaddingTop(6).Column(footer =>
                {
                    footer.Item().Text(ctx.FooterDisclaimer).FontSize(8).Italic().FontColor(MUTED);
                    footer.Item().Text($"Terms: {ctx.TermsText}").FontSize(8).Italic().FontColor(MUTED);
                });

                col.Item().Element(c => ComposeSignatureBlocks(c, ctx));
            });
        }

        private void ComposeClientBlock(IContainer container, PdfExportContext ctx)
        {
            container.Column(col =>
            {
                col.Item().Text("CLIENT / BILL TO:").FontSize(10).Bold().FontColor(DARK);
                AddLabelValue(col, "Customer Name:", ctx.CustomerName);
                AddLabelValue(col, "TRN/VAT Number:", ctx.CustomerTrn);
                AddLabelValue(col, "Attention Name:", ctx.AttentionName);
                AddLabelValue(col, "Contact Details:", ctx.ContactPhone);
                AddLabelValue(col, "Billing Address:", ctx.BillingAddress);
                AddLabelValue(col, "Client Reference:", ctx.CustomerReference);
            });
        }

        private void ComposeProjectBlock(IContainer container, PdfExportContext ctx)
        {
            container.Column(col =>
            {
                col.Item().Text("PROJECT ESTIMATE METRIC:").FontSize(10).Bold().FontColor(DARK);
                AddLabelValue(col, "Project Designation:", ctx.ProjectName);
                AddLabelValue(col, "Project Code / ID:", ctx.ProjectContractNo);
                AddLabelValue(col, "Site Delivery:", ctx.SiteDelivery);
                AddLabelValue(col, "LPO / PO Reference:", ctx.LpoNumber);
                AddLabelValue(col, "Assigned Salesman:", ctx.SalesRepresentative);
                AddLabelValue(col, "Valid Until:", $"{ctx.ValidUntil} • Status: {ctx.ProformaStatus}");
            });
        }

        // ══════════════════════════════════════════════════════════════════
        //  Phase 7.19d — 10 columns: # | GLASS REF | W1 | H1 | W2 | H2 | QTY | AREA | RATE | TOTAL
        // ══════════════════════════════════════════════════════════════════
        private void ComposeSpecTable(IContainer container, PdfSpecGroup group)
        {
            container.PaddingBottom(10).Column(col =>
            {
                string specLabel = !string.IsNullOrWhiteSpace(group.GroupName) ? group.GroupName : $"Spec Group {group.GroupNumber}";
                col.Item().Background(ACCENT).PaddingHorizontal(8).PaddingVertical(5)
                    .Text($"SPECIFICATION {group.GroupNumber}: {specLabel}")
                    .FontSize(10).Bold().FontColor(Colors.White);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(28);       // #
                        c.RelativeColumn(1.6f);     // Glass Ref
                        c.ConstantColumn(35);       // W1
                        c.ConstantColumn(35);       // H1
                        c.ConstantColumn(35);       // W2
                        c.ConstantColumn(35);       // H2
                        c.ConstantColumn(40);       // Qty
                        c.RelativeColumn(0.85f);    // Area
                        c.RelativeColumn(0.75f);    // Rate
                        c.RelativeColumn(0.95f);    // Total
                    });

                    table.Header(header =>
                    {
                        var headers = new[] { "#", "GLASS REF", "W1", "H1", "W2", "H2", "QTY", "AREA", "RATE", "TOTAL" };
                        foreach (var h in headers)
                        {
                            header.Cell().Background(TABLE_HEADER).PaddingHorizontal(6).PaddingVertical(5)
                                .Text(h).FontSize(9).Bold().FontColor(Colors.White);
                        }
                    });

                    foreach (var row in group.Rows)
                    {
                        AddCell(table, row.Serial.ToString());
                        AddCell(table, row.GlassRef);
                        AddCellRight(table, row.W1.ToString());
                        AddCellRight(table, row.H1.ToString());
                        AddCellRight(table, row.W2.ToString());
                        AddCellRight(table, row.H2.ToString());
                        AddCellCenter(table, row.Qty.ToString());
                        AddCellRight(table, row.Area.ToString("0.000"));
                        AddCellRight(table, row.Rate.ToString("N2"));
                        AddCellRight(table, row.Total.ToString("N2"), bold: true);
                    }
                });

                col.Item().Background(LIGHT_BG).BorderBottom(1).BorderColor(LINE)
                    .PaddingHorizontal(6).PaddingVertical(5).Row(r =>
                    {
                        r.RelativeItem().Text($"SPEC SUB-TOTALS:  QTY: {group.TotalQty}  •  AREA: {group.TotalSqm:0.000} sqm")
                            .FontSize(9).Bold().FontColor(ACCENT);
                        r.AutoItem().Text($"VALUE: AED {group.TotalValue:N2}")
                            .FontSize(10).Bold().FontColor(GREEN);
                    });
            });
        }

        private void ComposeOtherCharges(IContainer container, PdfExportContext ctx)
        {
            if (!ctx.OtherCharges.Any()) return;

            container.PaddingTop(4).Column(col =>
            {
                col.Item().Background(ACCENT).PaddingHorizontal(8).PaddingVertical(5)
                    .Text("SUPPLEMENTARY / FABRICATION OTHER CHARGES")
                    .FontSize(10).Bold().FontColor(Colors.White);

                col.Item().Background(LIGHT_BG).Border(1).BorderColor(LINE).Padding(8).Column(charges =>
                {
                    foreach (var charge in ctx.OtherCharges)
                    {
                        charges.Item().PaddingVertical(2).Row(r =>
                        {
                            r.RelativeItem(2).Text(charge.Name).FontSize(9).SemiBold();
                            r.RelativeItem(1.2f).Text(charge.QtyLabel).FontSize(9).FontColor(MUTED);
                            r.RelativeItem(1).Text(charge.RateLabel).FontSize(9).FontColor(MUTED);
                            r.RelativeItem(1).AlignRight().Text($"AED {charge.Amount:N2}").FontSize(10).Bold();
                        });
                    }
                });
            });
        }

        private void ComposeFinancialSummary(IContainer container, PdfExportContext ctx)
        {
            container.PaddingTop(10).BorderTop(1).BorderColor(LINE).PaddingTop(10)
                .AlignRight().Width(320).Column(col =>
                {
                    AddSummaryRow(col, "Glass Materials Total:", $"{ctx.GlassMaterialsTotal:N2} AED");
                    AddSummaryRow(col, "Other Processing/Delivery Total:", $"{ctx.OtherChargesTotal:N2} AED");

                    col.Item().PaddingVertical(3).LineHorizontal(1).LineColor(LINE);

                    AddSummaryRow(col, "Estimated Net Subtotal:", $"{ctx.NetSubtotal:N2} AED", bold: true, size: 10);

                    if (ctx.DiscountAmount > 0)
                        AddSummaryRow(col, "Discount:", $"- {ctx.DiscountAmount:N2} AED", color: RED);

                    if (ctx.IsVatApplicable)
                        AddSummaryRow(col, $"Standard UAE VAT ({ctx.VatPercent:0.##}%):", $"{ctx.VatAmount:N2} AED");

                    col.Item().PaddingTop(6).Background(ACCENT).PaddingHorizontal(12).PaddingVertical(8).Row(r =>
                    {
                        r.RelativeItem().Text("TOTAL EST VALUE (VAT INC):").FontSize(12).Bold().FontColor(Colors.White);
                        r.AutoItem().Text($"{ctx.GrandTotal:N2} AED").FontSize(13).Bold().FontColor(Colors.White);
                    });
                });
        }

        private void ComposeTermsAndConditions(IContainer container, PdfExportContext ctx)
        {
            container.PaddingTop(14).Column(col =>
            {
                col.Item().Background(ACCENT).PaddingHorizontal(8).PaddingVertical(5)
                    .Text("TERMS & CONDITIONS").FontSize(10).Bold().FontColor(Colors.White);

                col.Item().Background(LIGHT_BG).Border(1).BorderColor(LINE).Padding(10).Column(terms =>
                {
                    foreach (var clause in ctx.TermClauses)
                    {
                        terms.Item().PaddingBottom(6).Column(clauseCol =>
                        {
                            clauseCol.Item().Text($"{clause.Order}. {clause.Title?.ToUpper()}")
                                .FontSize(9).Bold().FontColor(DARK);

                            if (!string.IsNullOrWhiteSpace(clause.Body))
                                clauseCol.Item().PaddingLeft(12).Text(clause.Body).FontSize(9).FontColor(DARK);

                            if (clause.SubLines != null)
                            {
                                foreach (var line in clause.SubLines)
                                {
                                    if (string.IsNullOrWhiteSpace(line)) continue;
                                    clauseCol.Item().PaddingLeft(24).Row(r =>
                                    {
                                        r.AutoItem().Text("• ").FontSize(9).Bold().FontColor(ACCENT);
                                        r.RelativeItem().Text(line).FontSize(9).FontColor(DARK);
                                    });
                                }
                            }
                        });
                    }
                });
            });
        }

        private void ComposeSignatureBlocks(IContainer container, PdfExportContext ctx)
        {
            container.PaddingTop(12).Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(LINE).Padding(10).Column(col =>
                {
                    col.Item().Text("PREPARED BY (ERP AUTHENTICATED)").FontSize(9).Bold().FontColor(ACCENT);
                    col.Item().PaddingTop(4).Text(string.IsNullOrWhiteSpace(ctx.PreparedByName) ? "—" : ctx.PreparedByName)
                        .FontSize(10).SemiBold().FontColor(DARK);

                    if (!string.IsNullOrWhiteSpace(ctx.PreparedByDept))
                        col.Item().PaddingTop(2).Text(ctx.PreparedByDept).FontSize(9).FontColor(MUTED);

                    col.Item().PaddingTop(3).Row(r =>
                    {
                        r.AutoItem().Text("Status: ").FontSize(9).FontColor(MUTED);
                        r.AutoItem().Text(string.IsNullOrWhiteSpace(ctx.PreparedByStatus) ? "Draft" : ctx.PreparedByStatus)
                            .FontSize(9).Bold().FontColor(GREEN);
                    });
                });

                row.ConstantItem(16);

                row.RelativeItem().Border(1).BorderColor(LINE).Padding(10).Column(col =>
                {
                    col.Item().Text("CLIENT APPROVAL SIGN & MOB NUMBER").FontSize(9).Bold().FontColor(ACCENT);
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.AutoItem().Text("Approval Sign:").FontSize(9).FontColor(MUTED);
                        r.RelativeItem().PaddingLeft(6).BorderBottom(1).BorderColor(LINE).Height(1);
                    });
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        r.AutoItem().Text("Mobile Number:").FontSize(9).FontColor(MUTED);
                        r.RelativeItem().PaddingLeft(6).BorderBottom(1).BorderColor(LINE).Height(1);
                    });
                    col.Item().PaddingTop(4).Text("Authorized Name & Stamp").FontSize(9).FontColor(MUTED);
                });
            });
        }

        private void ComposePageFooter(IContainer container, PdfExportContext ctx)
        {
            container.AlignCenter().Text(text =>
            {
                text.Span("Page ").FontSize(8).FontColor(MUTED);
                text.CurrentPageNumber().FontSize(8).FontColor(MUTED);
                text.Span(" of ").FontSize(8).FontColor(MUTED);
                text.TotalPages().FontSize(8).FontColor(MUTED);
            });
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static void AddLabelValue(ColumnDescriptor col, string label, string value)
        {
            col.Item().PaddingTop(1).Row(r =>
            {
                r.AutoItem().Text($"{label} ").FontSize(9).FontColor(MUTED);
                r.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                    .FontSize(9).SemiBold().FontColor(DARK);
            });
        }

        private static void AddSummaryRow(ColumnDescriptor col, string label, string value, bool bold = false, int size = 9, string color = null)
        {
            col.Item().PaddingBottom(3).Row(r =>
            {
                var labelText = r.RelativeItem().Text(label).FontSize(size);
                if (bold) labelText.Bold();
                if (color != null) labelText.FontColor(color);

                var valueText = r.AutoItem().Text(value).FontSize(size);
                if (bold) valueText.Bold();
                if (color != null) valueText.FontColor(color);
            });
        }

        private static void AddCell(TableDescriptor table, string text)
        {
            table.Cell().BorderBottom(1).BorderColor(LINE)
                .PaddingHorizontal(6).PaddingVertical(4)
                .Text(text).FontSize(9);
        }

        private static void AddCellCenter(TableDescriptor table, string text)
        {
            table.Cell().BorderBottom(1).BorderColor(LINE)
                .PaddingHorizontal(6).PaddingVertical(4)
                .AlignCenter().Text(text).FontSize(9);
        }

        private static void AddCellRight(TableDescriptor table, string text, bool bold = false)
        {
            var cell = table.Cell().BorderBottom(1).BorderColor(LINE)
                .PaddingHorizontal(6).PaddingVertical(4)
                .AlignRight().Text(text).FontSize(9);
            if (bold) cell.Bold();
        }
    }
}